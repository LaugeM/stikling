using Strings = Stikling.Web.Resources;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Stikling.Core.Calendar;
using Stikling.Core.Compare;
using Stikling.Core.Notifications;
using Stikling.Core.People;
using Stikling.Core.PlantFacts;
using Stikling.Core.Sharing;
using Stikling.Core.Sync;

namespace Stikling.Web.Services;

/// <summary>The signed-in person and the collections they are in, from <c>GET /me</c>.</summary>
public sealed record Me(Guid PersonId, List<MeCollection> Collections, string? Name = null);

/// <param name="Role">"Editor", "Helper" or "Viewer".</param>
/// <param name="ManagesPeople">Whether they can invite people and change viewers and helpers. Only editors can.</param>
/// <param name="IsOwner">Whether it is their own collection, with all it brings.</param>
public sealed record MeCollection(Guid Id, string Name, string Role, bool ManagesPeople = false, bool IsOwner = false);

/// <summary><c>GET /collections/{id}</c>: a collection and the people in it.</summary>
public sealed record CollectionDetails(
    Guid Id, string Name, Guid? OwnerPersonId, List<CollectionMember> Members, List<FormerCollectionMember> FormerMembers);

public sealed record CollectionMember(Guid PersonId, string Role, string? Name, bool ManagesPeople, bool IsOwner, DateTimeOffset JoinedAt)
{
    public CollectionRole RoleKind => CollectionRoles.Parse(Role);
}

public sealed record FormerCollectionMember(Guid PersonId, string? Name);

/// <summary>An open invite. <see cref="Token"/> is null when the caller may not have the link, as for one that lets someone add people.</summary>
public sealed record InviteInfo(
    Guid Id, string? Token, string Role, bool ManagesPeople, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string? CreatedByName)
{
    public RoleChoice Choice => RoleChoice.Of(CollectionRoles.Parse(Role), ManagesPeople);
}

/// <summary>What the person who opened an invite link is told before they join.</summary>
public sealed record InvitePreviewInfo(
    Guid CollectionId, string CollectionName, string? InvitedByName, string Role, bool ManagesPeople, bool AlreadyMember)
{
    public RoleChoice Choice => RoleChoice.Of(CollectionRoles.Parse(Role), ManagesPeople);
}

/// <summary>Why a call about people failed, so the screen can pick the words.</summary>
public enum PeopleFailure
{
    /// <summary>The API couldn't be reached, or no sign-in could be had.</summary>
    Offline,

    /// <summary>401. The sign-in ran out.</summary>
    SignedOut,

    /// <summary>403. The person may not do this, usually since their place changed.</summary>
    Forbidden,

    /// <summary>404. Not found, like an invite link that doesn't exist.</summary>
    NotFound,

    /// <summary>410. An invite that has run out, been used or withdrawn.</summary>
    Gone,

    /// <summary>409 with problem "full". The collection has as many people as it can have.</summary>
    Full,

    /// <summary>409 with problem "owner". The owner can't leave.</summary>
    Owner,

    /// <summary>429. Too many invites.</summary>
    TooMany,

    /// <summary>400 with a field that broke a rule, named in <see cref="PeopleOutcome{T}.Field"/>.</summary>
    Invalid,

    Other,
}

/// <summary>The answer to a call about people: the value, or why it failed with the status and the field a rule was broken in.</summary>
public sealed record PeopleOutcome<T>(T? Value, PeopleFailure? Failure = null, int Status = 0, string? Field = null)
{
    public bool Ok => Failure is null;

    public static PeopleOutcome<T> Success(T value) => new(value);

    public static PeopleOutcome<T> Failed(PeopleFailure failure, int status = 0, string? field = null) => new(default, failure, status, field);
}

/// <summary>
/// Calls the sync API as whoever is signed in. Every request carries a fresh session token from
/// <see cref="AccountService"/>, which the API checks. A request that can't reach the API, or
/// that it turns away, throws <see cref="HttpRequestException"/>. Photo images are sent and
/// fetched through <see cref="PhotoService"/>, which keeps them in JavaScript.
/// </summary>
public sealed class StiklingApi(AccountSettings settings, AccountService account, PhotoService photos, DeviceFiles files) : ISyncServer, IPhotoServer, IShareLinkServer, IPublicPageServer, IFriendPageServer
{
    private readonly HttpClient http = new() { BaseAddress = settings.ApiAddress };

    /// <summary>
    /// Asks the API to start, without waiting for it. When nobody has used it for a while it takes
    /// a few seconds to start, so this is called while Clerk loads or someone signs in, and the
    /// sync after it doesn't have to wait as long. It needs no sign-in, and never throws.
    /// </summary>
    public void Wake()
    {
        if (settings.ApiAddress is null)
            return;

        _ = WakeAsync();

        async Task WakeAsync()
        {
            try
            {
                using var response = await http.GetAsync("health");
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                // Sync finds out about a server that can't be reached on its own
            }
        }
    }

    /// <summary>
    /// Who the API thinks is signed in. The first call after signing up also creates the person
    /// and their own collection on the server.
    /// </summary>
    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task<Me> GetMeAsync()
    {
        using var request = await RequestAsync(HttpMethod.Get, "me");
        return await SendAsync<Me>(request);
    }

    /// <summary>A collection and the people in it, who have left it too. Any member can ask.</summary>
    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task<CollectionDetails> GetCollectionAsync(Guid id)
    {
        using var request = await RequestAsync(HttpMethod.Get, $"collections/{id}");
        return await SendAsync<CollectionDetails>(request);
    }

    // People in a collection. A call that fails comes back as an outcome with the reason, since the screen
    // chooses its own words by it.

    /// <summary>Sets the name other people in a shared collection see.</summary>
    public async Task<PeopleOutcome<bool>> SetNameAsync(string name) =>
        await PeopleCallAsync<bool>(HttpMethod.Put, "me/name", new { name });

    public async Task<PeopleOutcome<bool>> RenameCollectionAsync(Guid id, string name) =>
        await PeopleCallAsync<bool>(HttpMethod.Put, $"collections/{id}/name", new { name });

    public async Task<PeopleOutcome<bool>> ChangeMemberAsync(Guid id, Guid personId, CollectionRole role, bool managesPeople) =>
        await PeopleCallAsync<bool>(HttpMethod.Put, $"collections/{id}/members/{personId}", new { role = role.ToString(), managesPeople });

    public async Task<PeopleOutcome<bool>> RemoveMemberAsync(Guid id, Guid personId) =>
        await PeopleCallAsync<bool>(HttpMethod.Delete, $"collections/{id}/members/{personId}", null);

    /// <summary>The caller leaves. The owner can't, which comes back as <see cref="PeopleFailure.Owner"/>.</summary>
    public async Task<PeopleOutcome<bool>> LeaveAsync(Guid id) =>
        await PeopleCallAsync<bool>(HttpMethod.Post, $"collections/{id}/leave", null);

    /// <summary>The owner gives the collection to another member, who becomes an editor that adds people.</summary>
    public async Task<PeopleOutcome<bool>> HandOverAsync(Guid id, Guid personId) =>
        await PeopleCallAsync<bool>(HttpMethod.Put, $"collections/{id}/owner", new { personId });

    public async Task<PeopleOutcome<InviteInfo>> CreateInviteAsync(Guid id, CollectionRole role, bool managesPeople) =>
        await PeopleCallAsync<InviteInfo>(HttpMethod.Post, $"collections/{id}/invites", new { role = role.ToString(), managesPeople });

    public async Task<PeopleOutcome<List<InviteInfo>>> GetInvitesAsync(Guid id) =>
        await PeopleCallAsync<List<InviteInfo>>(HttpMethod.Get, $"collections/{id}/invites", null);

    public async Task<PeopleOutcome<bool>> WithdrawInviteAsync(Guid id, Guid inviteId) =>
        await PeopleCallAsync<bool>(HttpMethod.Delete, $"collections/{id}/invites/{inviteId}", null);

    public async Task<PeopleOutcome<InvitePreviewInfo>> PreviewInviteAsync(string token) =>
        await PeopleCallAsync<InvitePreviewInfo>(HttpMethod.Get, $"invites/{Uri.EscapeDataString(token)}", null);

    /// <summary>Joins. Someone who is in already gets the collection back, as when the invite is for them twice.</summary>
    public async Task<PeopleOutcome<MeCollection>> AcceptInviteAsync(string token) =>
        await PeopleCallAsync<MeCollection>(HttpMethod.Post, $"invites/{Uri.EscapeDataString(token)}/accept", null);

    private async Task<PeopleOutcome<T>> PeopleCallAsync<T>(HttpMethod method, string path, object? body)
    {
        try
        {
            using var request = await RequestAsync(method, path);
            if (body is not null)
                request.Content = JsonContent.Create(body, body.GetType());
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return await PeopleFailureOfAsync<T>(response);

            if (typeof(T) == typeof(bool))
                return PeopleOutcome<T>.Success((T)(object)true);

            return await response.Content.ReadFromJsonAsync<T>() is { } value
                ? PeopleOutcome<T>.Success(value)
                : PeopleOutcome<T>.Failed(PeopleFailure.Other, (int)response.StatusCode);
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
        {
            return PeopleOutcome<T>.Failed(PeopleFailure.SignedOut, 401);
        }
        catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException or JsonException)
        {
            return PeopleOutcome<T>.Failed(PeopleFailure.Offline);
        }
    }

    // The API names what went wrong in "problem" for the cases that have one, and the field in "errors" for a rule a value broke
    private static async Task<PeopleOutcome<T>> PeopleFailureOfAsync<T>(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        string? problem = null;
        string? field = null;
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = document.RootElement;
            if (root.TryGetProperty("problem", out var code) && code.ValueKind == JsonValueKind.String)
                problem = code.GetString();
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                field = errors.EnumerateObject().FirstOrDefault().Name;
        }
        catch (Exception e) when (e is JsonException or HttpRequestException)
        {
            // Then the status is all there is
        }

        var failure = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => PeopleFailure.SignedOut,
            HttpStatusCode.Forbidden => PeopleFailure.Forbidden,
            HttpStatusCode.NotFound => PeopleFailure.NotFound,
            HttpStatusCode.Gone => PeopleFailure.Gone,
            HttpStatusCode.TooManyRequests => PeopleFailure.TooMany,
            HttpStatusCode.Conflict when string.Equals(problem, "full", StringComparison.OrdinalIgnoreCase) => PeopleFailure.Full,
            HttpStatusCode.Conflict when string.Equals(problem, "owner", StringComparison.OrdinalIgnoreCase) => PeopleFailure.Owner,
            HttpStatusCode.BadRequest when field is not null => PeopleFailure.Invalid,
            _ => PeopleFailure.Other,
        };
        return PeopleOutcome<T>.Failed(failure, status, field);
    }

    /// <summary>
    /// Erases the account on the server: the collections nobody else is in, with their records and
    /// photos, and the person's settings. Asking again after it's done does no harm.
    /// </summary>
    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task DeleteMeAsync()
    {
        using var request = await RequestAsync(HttpMethod.Delete, "me");
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Saves a ZIP of everything the server holds about the signed-in person, with what Clerk has
    /// about them added. It goes from the API to the file in JavaScript, since the photos in it
    /// can come to a lot.
    /// </summary>
    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task DownloadMyDataAsync(string fileName)
    {
        var body = new { account = await account.GetProfileAsync() };
        var status = await files.DownloadFromAsync(new Uri(http.BaseAddress!, "me/export"), await TokenAsync(), body, fileName);
        if (status is < 200 or >= 300)
            throw Failed(status);
    }

    public async Task<PullResponse> PullAsync(Guid collectionId, long after)
    {
        using var request = await RequestAsync(HttpMethod.Get, $"collections/{collectionId}/records?after={after}");
        return await SendAsync<PullResponse>(request);
    }

    public async Task<PushResponse> PushAsync(Guid collectionId, IReadOnlyList<SyncRecord> records)
    {
        using var request = await RequestAsync(HttpMethod.Post, $"collections/{collectionId}/records");
        request.Content = JsonContent.Create(new PushRequest([.. records]));
        return await SendAsync<PushResponse>(request);
    }

    public async Task<JsonElement?> GetSettingsAsync()
    {
        using var request = await RequestAsync(HttpMethod.Get, "me/settings");
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return response.StatusCode == HttpStatusCode.NoContent
            ? null
            : await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<JsonElement?> PutSettingsAsync(JsonElement settings)
    {
        using var request = await RequestAsync(HttpMethod.Put, "me/settings");
        request.Content = JsonContent.Create(settings);
        using var response = await http.SendAsync(request);

        // The server didn't take them, e.g. dated too far ahead by a clock that is wrong
        if (response.StatusCode == HttpStatusCode.BadRequest)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // The daily notification. A failure to reach the server or a refusal throws, except where the
    // status itself is the answer.

    /// <summary>
    /// The key a device subscribes with, or null when the server has none (404 or 503). Needs no sign-in.
    /// </summary>
    /// <exception cref="HttpRequestException">The API couldn't be reached.</exception>
    public async Task<string?> GetNotificationKeyAsync()
    {
        using var response = await http.GetAsync("notifications/key");
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.ServiceUnavailable)
            return null;

        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.TryGetProperty("publicKey", out var key) ? key.GetString() : null;
    }

    /// <summary>
    /// The short card about a species (or a genus, when <paramref name="species"/> is null): the
    /// Wikipedia summary and a few facts from Trefle, which the server asks for. Needs no sign-in and
    /// sends only the name. Nothing is stored on the device.
    /// </summary>
    /// <param name="language">"en" or "da".</param>
    /// <exception cref="HttpRequestException">
    /// The API couldn't be reached (<see cref="HttpRequestException.StatusCode"/> is null), or it
    /// answered with an error, like 502 when Wikipedia failed.
    /// </exception>
    /// <exception cref="TaskCanceledException">The request timed out.</exception>
    public async Task<PlantFacts> GetPlantFactsAsync(string genus, string? species, string language)
    {
        var query = $"plant-facts?genus={Uri.EscapeDataString(genus)}&language={Uri.EscapeDataString(language)}";
        if (!string.IsNullOrWhiteSpace(species))
            query += $"&species={Uri.EscapeDataString(species)}";

        using var response = await http.GetAsync(query);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PlantFacts>()
            ?? throw new HttpRequestException("The plant facts answer was empty.");
    }

    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task PutSubscriptionAsync(SubscriptionRequest subscription) =>
        await SendNotificationAsync(HttpMethod.Put, "me/notifications/subscriptions", subscription);

    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task RemoveSubscriptionAsync(string endpoint) =>
        await SendNotificationAsync(HttpMethod.Post, "me/notifications/subscriptions/remove", new EndpointRequest(endpoint));

    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task PutNotificationDaysAsync(DaysRequest days) =>
        await SendNotificationAsync(HttpMethod.Put, "me/notifications/days", days);

    /// <summary>
    /// Asks the server to send a test to this device. 204 sent, 404 the server doesn't have this
    /// endpoint, 410 the push service says it is gone, 502 the push service refused.
    /// </summary>
    /// <exception cref="HttpRequestException">The API couldn't be reached.</exception>
    public async Task<HttpStatusCode> SendNotificationTestAsync(string endpoint)
    {
        using var request = await RequestAsync(HttpMethod.Post, "me/notifications/test");
        request.Content = JsonContent.Create(new EndpointRequest(endpoint));
        using var response = await http.SendAsync(request);
        return response.StatusCode;
    }

    // The calendar link. A failure to reach the server or a refusal throws, except where the status
    // itself is the answer.

    /// <summary>Whether the person has a calendar link on.</summary>
    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task<bool> GetCalendarStatusAsync()
    {
        using var request = await RequestAsync(HttpMethod.Get, "me/calendar");
        return (await SendAsync<CalendarStatusResponse>(request)).On;
    }

    /// <summary>
    /// Makes a new link, or replaces the old one, and gives back its address. The server shows the
    /// token only this once. The address is built here, from the API's address.
    /// </summary>
    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task<string> NewCalendarLinkAsync()
    {
        using var request = await RequestAsync(HttpMethod.Post, "me/calendar");
        var token = (await SendAsync<CalendarLinkResponse>(request)).Token;
        return new Uri(settings.ApiAddress ?? throw new HttpRequestException("There is no API address."), $"calendar/{token}.ics").ToString();
    }

    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task TurnOffCalendarAsync()
    {
        using var request = await RequestAsync(HttpMethod.Delete, "me/calendar");
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Uploads the days for the link. False when the person has no link on, which is 404.</summary>
    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task<bool> PutCalendarDaysAsync(Guid collectionId, CalendarDaysRequest days)
    {
        using var request = await RequestAsync(HttpMethod.Put, $"collections/{collectionId}/calendar");
        request.Content = JsonContent.Create(days);
        using var response = await http.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    private async Task SendNotificationAsync(HttpMethod method, string path, object body)
    {
        using var request = await RequestAsync(method, path);
        request.Content = JsonContent.Create(body, body.GetType());
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<StoredPhoto>> GetStoredAsync(Guid collectionId, IReadOnlyList<Guid> ids)
    {
        using var request = await RequestAsync(HttpMethod.Post, $"collections/{collectionId}/photos/stored");
        request.Content = JsonContent.Create(new StoredPhotosRequest([.. ids]));
        return (await SendAsync<StoredPhotosResponse>(request)).Photos;
    }

    /// <summary>How much space the collection's photos take on the server, and how much they may.</summary>
    public async Task<PhotoUsage> GetPhotoUsageAsync(Guid collectionId)
    {
        using var request = await RequestAsync(HttpMethod.Get, $"collections/{collectionId}/photos/usage");
        return await SendAsync<PhotoUsage>(request);
    }

    // The share links. A call that fails comes back with the problem's own words instead of throwing,
    // since they are shown on the Share sheet as they are.

    public async Task<ShareOutcome<IReadOnlyList<ShareLinkInfo>>> ListAsync(Guid collectionId) =>
        await ShareCallAsync<List<ShareLinkInfo>, IReadOnlyList<ShareLinkInfo>>(HttpMethod.Get, $"collections/{collectionId}/shares", null, list => list);

    public async Task<ShareOutcome<ShareLinkInfo>> CreateAsync(Guid collectionId, CreateShareLinkRequest request) =>
        await ShareCallAsync<ShareLinkInfo, ShareLinkInfo>(HttpMethod.Post, $"collections/{collectionId}/shares", request, link => link);

    public async Task<ShareOutcome<ShareLinkInfo>> UpdateAsync(Guid collectionId, Guid id, UpdateShareLinkRequest request) =>
        await ShareCallAsync<ShareLinkInfo, ShareLinkInfo>(HttpMethod.Put, $"collections/{collectionId}/shares/{id}", request, link => link);

    public async Task<ShareOutcome<bool>> TurnOffAsync(Guid collectionId, Guid id) =>
        await ShareCallAsync<object, bool>(HttpMethod.Delete, $"collections/{collectionId}/shares/{id}", null, _ => true, hasBody: false);

    private async Task<ShareOutcome<TResult>> ShareCallAsync<TBody, TResult>(
        HttpMethod method, string path, object? body, Func<TBody, TResult> result, bool hasBody = true)
    {
        try
        {
            using var request = await RequestAsync(method, path);
            if (body is not null)
                request.Content = JsonContent.Create(body, body.GetType());
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return ShareOutcome<TResult>.Failure(await ProblemOfAsync(response));

            if (!hasBody)
                return ShareOutcome<TResult>.Success(result(default!));
            return await response.Content.ReadFromJsonAsync<TBody>() is { } value
                ? ShareOutcome<TResult>.Success(result(value))
                : ShareOutcome<TResult>.Failure(Strings.Account.ShareEmptyAnswer);
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
        {
            return ShareOutcome<TResult>.Failure(Strings.Account.ShareSignInRanOut);
        }
        catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException)
        {
            return ShareOutcome<TResult>.Failure(Strings.Account.ShareUnreachable, offline: true);
        }
    }

    // The public collection page. A call that fails comes back with its status, since the page chooses its
    // own words by it. The one thing the API says in words of its own is which rule a name broke.

    public async Task<ShareOutcome<PublicPageInfo?>> GetAsync(Guid collectionId) =>
        await PublicPageCallAsync<PublicPageInfo?>(HttpMethod.Get, $"collections/{collectionId}/public-page", null, notFoundIsEmpty: true);

    public async Task<ShareOutcome<PublicPageInfo>> TurnOnAsync(Guid collectionId, TurnOnPublicPageRequest request) =>
        Required(await PublicPageCallAsync<PublicPageInfo?>(HttpMethod.Post, $"collections/{collectionId}/public-page", request));

    public async Task<ShareOutcome<PublicPageInfo>> UpdateAsync(Guid collectionId, UpdatePublicPageRequest request) =>
        Required(await PublicPageCallAsync<PublicPageInfo?>(HttpMethod.Put, $"collections/{collectionId}/public-page", request));

    public async Task<ShareOutcome<PublicPageInfo>> SetNameAsync(Guid collectionId, SetPublicPageNameRequest request) =>
        Required(await PublicPageCallAsync<PublicPageInfo?>(HttpMethod.Put, $"collections/{collectionId}/public-page/name", request));

    public async Task<ShareOutcome<bool>> TurnOffAsync(Guid collectionId)
    {
        var result = await PublicPageCallAsync<object?>(HttpMethod.Delete, $"collections/{collectionId}/public-page", null, hasBody: false);
        return result.Ok ? ShareOutcome<bool>.Success(true) : ShareOutcome<bool>.Failure(result.Problem!, result.Offline, result.Status);
    }

    private static ShareOutcome<PublicPageInfo> Required(ShareOutcome<PublicPageInfo?> result) =>
        !result.Ok ? ShareOutcome<PublicPageInfo>.Failure(result.Problem!, result.Offline, result.Status, result.NameProblem)
        : result.Value is { } page ? ShareOutcome<PublicPageInfo>.Success(page)
        : ShareOutcome<PublicPageInfo>.Failure(Strings.Account.ShareEmptyAnswer);

    private async Task<ShareOutcome<T?>> PublicPageCallAsync<T>(
        HttpMethod method, string path, object? body, bool hasBody = true, bool notFoundIsEmpty = false)
    {
        try
        {
            using var request = await RequestAsync(method, path);
            if (body is not null)
                request.Content = JsonContent.Create(body, body.GetType());
            using var response = await http.SendAsync(request);
            if (notFoundIsEmpty && response.StatusCode == HttpStatusCode.NotFound)
                return ShareOutcome<T?>.Success(default);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                return ShareOutcome<T?>.Failure($"The API answered {status}.", status: status, nameProblem: await NameProblemOfAsync(response));
            }

            return hasBody
                ? ShareOutcome<T?>.Success(await response.Content.ReadFromJsonAsync<T>())
                : ShareOutcome<T?>.Success(default);
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
        {
            return ShareOutcome<T?>.Failure(Strings.Account.ShareSignInRanOut, status: 401);
        }
        catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException or JsonException)
        {
            return ShareOutcome<T?>.Failure(Strings.Account.ShareUnreachable, offline: true);
        }
    }

    // A friend's page as a list to compare with. It needs no sign-in, so no token is sent, and the
    // statuses 404 and 410 come back as they are for the page to explain.

    public async Task<ShareOutcome<FriendPageResponse>> GetFriendPageAsync(string address)
    {
        if (settings.ApiAddress is null)
            return ShareOutcome<FriendPageResponse>.Failure(Strings.Account.ShareUnreachable, offline: true);

        try
        {
            // The @ in a name stays as it is, which the route reads the same either way
            var path = $"s/{Uri.EscapeDataString(address).Replace("%40", "@")}/compare";
            using var response = await http.GetAsync(path);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                return ShareOutcome<FriendPageResponse>.Failure($"The API answered {status}.", status: status);
            }

            return await response.Content.ReadFromJsonAsync<FriendPageResponse>() is { } page
                ? ShareOutcome<FriendPageResponse>.Success(page)
                : ShareOutcome<FriendPageResponse>.Failure(Strings.Account.ShareEmptyAnswer);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            return ShareOutcome<FriendPageResponse>.Failure(Strings.Account.ShareUnreachable, offline: true);
        }
    }

    // A refused name carries the rule it broke as text in the problem details' "problem"
    private static async Task<NameProblem?> NameProblemOfAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.BadRequest)
            return null;

        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (document.RootElement.TryGetProperty("problem", out var problem)
                && problem.ValueKind == JsonValueKind.String
                && Enum.TryParse<NameProblem>(problem.GetString(), ignoreCase: true, out var found))
                return found;
        }
        catch (Exception e) when (e is JsonException or HttpRequestException)
        {
            // Then it is some other 400
        }

        return null;
    }

    // The API explains a refusal in "detail", or for a field that is too long in "errors"
    private static async Task<string> ProblemOfAsync(HttpResponseMessage response)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = document.RootElement;
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String && detail.GetString() is { Length: > 0 } text)
                return text;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errors.EnumerateObject())
                {
                    if (field.Value.ValueKind == JsonValueKind.Array && field.Value.EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.String } first)
                        return first.GetString()!;
                }
            }
        }
        catch (Exception e) when (e is JsonException or HttpRequestException)
        {
            // Falls through to the plain words
        }

        return response.StatusCode == HttpStatusCode.NotFound
            ? Strings.Account.ShareLinkGone
            : Strings.Account.ShareTurnedDown;
    }

    // The images go between the device database and the API in photos.js, so they never pass through .NET

    public async Task<UploadOutcome> UploadAsync(Guid collectionId, Guid photoId, PhotoSize size)
    {
        var status = await UntilNotTooManyAsync(async () =>
            await photos.UploadAsync(ImageAddress(collectionId, photoId, size), await TokenAsync(), photoId, size == PhotoSize.Thumbnail));
        return status switch
        {
            0 => UploadOutcome.NotHere,
            >= 200 and < 300 => UploadOutcome.Uploaded,
            404 => UploadOutcome.NotYet,
            410 => UploadOutcome.Gone,
            507 => UploadOutcome.Full,
            413 or 415 => UploadOutcome.Refused,
            _ => throw Failed(status),
        };
    }

    public async Task<bool> DownloadAsync(Guid collectionId, Guid photoId, PhotoSize size)
    {
        var status = await UntilNotTooManyAsync(async () =>
            await photos.DownloadAsync(ImageAddress(collectionId, photoId, size), await TokenAsync(), photoId, size == PhotoSize.Thumbnail));
        return status switch
        {
            200 => true,
            404 => false,
            _ => throw Failed(status),
        };
    }

    /// <summary>How long a photo waits when the server says this account has sent too much at once.</summary>
    private static readonly TimeSpan TooManyWait = TimeSpan.FromSeconds(10);

    /// <summary>How many times it is sent before the sync gives up until next time.</summary>
    private const int TooManyTries = 30;

    // A first sync with a lot of photos can go past what the server takes from one account at
    // once (RequestLimits in the API). It answers 429, and the photos carry on more slowly.
    private static async Task<int> UntilNotTooManyAsync(Func<Task<int>> send)
    {
        var status = await send();
        for (var tries = 1; status == 429 && tries < TooManyTries; tries++)
        {
            await Task.Delay(TooManyWait);
            status = await send();
        }
        return status;
    }

    private Uri ImageAddress(Guid collectionId, Guid photoId, PhotoSize size) =>
        new(http.BaseAddress!, $"collections/{collectionId}/photos/{photoId}/{size.PathName()}");

    private static HttpRequestException Failed(int status) =>
        status < 0
            ? new HttpRequestException("The API couldn't be reached.")
            : new HttpRequestException($"The API answered {status}.", null, (HttpStatusCode)status);

    private async Task<T> SendAsync<T>(HttpRequestMessage request)
    {
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new HttpRequestException($"The API sent an empty answer to {request.RequestUri}.");
    }

    private async Task<HttpRequestMessage> RequestAsync(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync());
        return request;
    }

    // Answered like the API would answer a request without a sign-in
    private async Task<string> TokenAsync() =>
        await account.GetTokenAsync()
            ?? throw new HttpRequestException("Nobody is signed in.", null, HttpStatusCode.Unauthorized);
}
