using Stikling.Core.Models;

namespace Stikling.Core.Sharing;

/// <summary>
/// How a call to the share link endpoints went. A call that failed has the words to show the person.
/// </summary>
/// <param name="Value">What came back. Not set when the call failed.</param>
/// <param name="Problem">The server's own explanation, or a plain one when it couldn't be reached. Null when it worked.</param>
/// <param name="Offline">True when the server couldn't be reached, as opposed to turning the call away.</param>
public sealed record ShareOutcome<T>(T? Value, string? Problem = null, bool Offline = false)
{
    public bool Ok => Problem is null;

    public static ShareOutcome<T> Success(T value) => new(value);

    public static ShareOutcome<T> Failure(string problem, bool offline = false) => new(default, problem, offline);
}

/// <summary>The share link endpoints of the API, as the signed-in person.</summary>
public interface IShareLinkServer
{
    /// <summary>The links that are turned on in the collection.</summary>
    Task<ShareOutcome<IReadOnlyList<ShareLinkInfo>>> ListAsync(Guid collectionId);

    Task<ShareOutcome<ShareLinkInfo>> CreateAsync(Guid collectionId, CreateShareLinkRequest request);

    Task<ShareOutcome<ShareLinkInfo>> UpdateAsync(Guid collectionId, Guid id, UpdateShareLinkRequest request);

    Task<ShareOutcome<bool>> TurnOffAsync(Guid collectionId, Guid id);
}

/// <summary>
/// The share links that are on in the collection, kept in memory by the subject they are for.
/// Nothing is saved on the device: a link lives on the server, so the list is fetched after each
/// sync and is empty when nobody is signed in or the server can't be reached.
/// </summary>
public sealed class ShareLinkService(IShareLinkServer server)
{
    private static readonly IReadOnlyList<Guid> NoPhotos = [];

    private readonly Dictionary<Guid, ShareLinkInfo> links = [];
    private readonly SemaphoreSlim saving = new(1, 1);
    private Guid? collectionId;

    /// <summary>Raised when the list changed, so a screen showing a link can draw it again.</summary>
    public event Action? Changed;

    /// <summary>The link that is on for a plant or propagation, or null.</summary>
    public ShareLinkInfo? For(Guid subjectId) => links.GetValueOrDefault(subjectId);

    /// <summary>Fetches the collection's links. When that fails the list is emptied. Never throws.</summary>
    public async Task RefreshAsync(Guid forCollection)
    {
        collectionId = forCollection;
        ShareOutcome<IReadOnlyList<ShareLinkInfo>> found;
        try
        {
            found = await server.ListAsync(forCollection);
        }
        catch (Exception)
        {
            // A refresh must never stop a sync
            Empty();
            return;
        }

        if (!found.Ok || found.Value is null)
        {
            Empty();
            return;
        }

        var fetched = found.Value.GroupBy(l => l.SubjectId).ToDictionary(g => g.Key, g => g.First());
        if (links.Count == fetched.Count && links.All(l => fetched.TryGetValue(l.Key, out var other) && Same(other, l.Value)))
            return;

        links.Clear();
        foreach (var (subject, link) in fetched)
            links[subject] = link;
        Changed?.Invoke();
    }

    /// <summary>Empties the list, e.g. when nobody is signed in.</summary>
    public void Clear()
    {
        collectionId = null;
        Empty();
    }

    // The collection stays known, so a link can still be made once the server answers
    private void Empty()
    {
        if (links.Count == 0)
            return;

        links.Clear();
        Changed?.Invoke();
    }

    /// <summary>Turns sharing on for a plant or propagation that has been synced.</summary>
    /// <param name="timeZone">The device's IANA time zone id.</param>
    public async Task<ShareOutcome<ShareLinkInfo>> CreateAsync(SubjectType type, Guid subjectId, string? timeZone)
    {
        if (collectionId is not { } collection)
            return ShareOutcome<ShareLinkInfo>.Failure("It isn't on the server yet. Sync and try again.");

        var result = await server.CreateAsync(collection, new CreateShareLinkRequest(type, subjectId, timeZone));
        if (result.Ok && result.Value is { } link)
            Keep(link);
        return result;
    }

    /// <summary>Saves the settings of a link whole. Saves made one after the other reach the server in that order.</summary>
    public async Task<ShareOutcome<ShareLinkInfo>> UpdateAsync(
        ShareLinkInfo link, bool showNotes, string? name, string? line, IReadOnlyList<Guid> leftOut, string? timeZone)
    {
        if (collectionId is not { } collection)
            return ShareOutcome<ShareLinkInfo>.Failure("Sync and try again.");

        await saving.WaitAsync();
        try
        {
            var result = await server.UpdateAsync(collection, link.Id, new UpdateShareLinkRequest(
                showNotes, ShareLinkRules.Clean(name), ShareLinkRules.Clean(line), leftOut, timeZone));
            if (result.Ok && result.Value is { } saved)
                Keep(saved);
            return result;
        }
        finally
        {
            saving.Release();
        }
    }

    /// <summary>Turns the link off. The address then says the page is gone, and sharing again makes a new one.</summary>
    public async Task<ShareOutcome<bool>> TurnOffAsync(ShareLinkInfo link)
    {
        if (collectionId is not { } collection)
            return ShareOutcome<bool>.Failure("Sync and try again.");

        var result = await server.TurnOffAsync(collection, link.Id);
        if (result.Ok && links.GetValueOrDefault(link.SubjectId)?.Id == link.Id)
        {
            links.Remove(link.SubjectId);
            Changed?.Invoke();
        }
        return result;
    }

    // The list inside a record is compared by reference, so it is compared by what is in it
    private static bool Same(ShareLinkInfo a, ShareLinkInfo b) =>
        a with { LeftOutPhotoIds = NoPhotos } == b with { LeftOutPhotoIds = NoPhotos }
        && a.LeftOutPhotoIds.SequenceEqual(b.LeftOutPhotoIds);

    private void Keep(ShareLinkInfo link)
    {
        links[link.SubjectId] = link;
        Changed?.Invoke();
    }
}
