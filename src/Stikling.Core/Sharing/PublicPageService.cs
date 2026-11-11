using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Timeline;

namespace Stikling.Core.Sharing;

/// <summary>
/// The public collection page endpoints of the API, as the signed-in person. A call the server turned
/// away comes back as a failure with its status, so the app can use its own words for it.
/// </summary>
public interface IPublicPageServer
{
    /// <summary>The collection's page. A collection with no page on is a success with no value.</summary>
    Task<ShareOutcome<PublicPageInfo?>> GetAsync(Guid collectionId);

    Task<ShareOutcome<PublicPageInfo>> TurnOnAsync(Guid collectionId, TurnOnPublicPageRequest request);

    Task<ShareOutcome<PublicPageInfo>> UpdateAsync(Guid collectionId, UpdatePublicPageRequest request);

    Task<ShareOutcome<PublicPageInfo>> SetNameAsync(Guid collectionId, SetPublicPageNameRequest request);

    Task<ShareOutcome<bool>> TurnOffAsync(Guid collectionId);
}

/// <summary>
/// The collection's public page, kept in memory. Nothing is saved on the device: the page lives on the
/// server, so it is fetched after each sync and is unknown when nobody is signed in or the server
/// can't be reached. Hiding a plant, cutting or photo is saved on the record itself and syncs like any
/// other change.
/// </summary>
public sealed class PublicPageService(
    IPublicPageServer server, PlantService plants, PropagationService propagations, TimelineService timeline)
{
    private readonly SemaphoreSlim saving = new(1, 1);
    private Guid? collectionId;

    /// <summary>The page that is on, or null when it is off or not known yet. See <see cref="Known"/>.</summary>
    public PublicPageInfo? Page { get; private set; }

    /// <summary>True once the server has said whether a page is on, so a null <see cref="Page"/> means Off.</summary>
    public bool Known { get; private set; }

    /// <summary>Raised when the page, or whether it is known, changed, so a screen showing it can draw it again.</summary>
    public event Action? Changed;

    /// <summary>Fetches the collection's page. When that fails the last known state is kept. A different collection starts unknown. Never throws.</summary>
    public async Task RefreshAsync(Guid forCollection)
    {
        if (collectionId != forCollection)
            Forget();
        collectionId = forCollection;

        ShareOutcome<PublicPageInfo?> found;
        try
        {
            found = await server.GetAsync(forCollection);
        }
        catch (Exception)
        {
            // A refresh must never stop a sync, and a brief outage keeps what was known
            return;
        }

        if (found.Ok)
            Keep(found.Value);
    }

    /// <summary>
    /// Asks the server now, for the settings screen. A failure keeps what was known and says why.
    /// A device that hasn't synced yet doesn't know the collection and answers as if offline.
    /// </summary>
    public async Task<ShareOutcome<PublicPageInfo?>> LoadAsync()
    {
        if (collectionId is not { } collection)
            return ShareOutcome<PublicPageInfo?>.Failure("No collection yet.", offline: true);

        ShareOutcome<PublicPageInfo?> found;
        try
        {
            found = await server.GetAsync(collection);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return ShareOutcome<PublicPageInfo?>.Failure(e.Message, offline: true);
        }

        if (found.Ok)
            Keep(found.Value);
        return found;
    }

    /// <summary>Forgets the page, e.g. when nobody is signed in.</summary>
    public void Clear()
    {
        collectionId = null;
        Forget();
    }

    // The collection stays known, so the page can still be asked for once the server answers
    private void Forget()
    {
        if (Page is null && !Known)
            return;

        Page = null;
        Known = false;
        Changed?.Invoke();
    }

    private void Keep(PublicPageInfo? page)
    {
        if (Known && Page == page)
            return;

        Page = page;
        Known = true;
        Changed?.Invoke();
    }

    /// <summary>Turns the page on, written in the app's language. If one is on already, that one comes back.</summary>
    /// <param name="timeZone">The device's IANA time zone id.</param>
    public async Task<ShareOutcome<PublicPageInfo>> TurnOnAsync(string? timeZone, string? language)
    {
        if (collectionId is not { } collection)
            return NotSynced<PublicPageInfo>();

        var result = await server.TurnOnAsync(collection, new TurnOnPublicPageRequest(timeZone, PublicPageLanguages.Normalize(language)));
        if (result.Ok && result.Value is { } page)
            Keep(page);
        return result;
    }

    /// <summary>
    /// Saves the settings of the page whole, with the title and the line cleaned. Saves made one after the
    /// other reach the server in that order.
    /// </summary>
    public async Task<ShareOutcome<PublicPageInfo>> UpdateAsync(PublicPageInfo settings, string? timeZone, string? language)
    {
        if (collectionId is not { } collection)
            return NotSynced<PublicPageInfo>();

        await saving.WaitAsync();
        try
        {
            var result = await server.UpdateAsync(collection, new UpdatePublicPageRequest(
                ShareLinkRules.Clean(settings.Title),
                ShareLinkRules.Clean(settings.Line),
                settings.Layout,
                settings.Order,
                settings.ShowPropagations,
                settings.ShowSwap,
                settings.ShowWishlist,
                settings.ShowStories,
                settings.Searchable,
                PublicPageLanguages.Normalize(language),
                timeZone,
                settings.SwapRequests,
                PublicPageRules.CleanContact(settings.SwapContact)));
            if (result.Ok && result.Value is { } saved)
                Keep(saved);
            return result;
        }
        finally
        {
            saving.Release();
        }
    }

    /// <summary>
    /// Gives the page a name, or gives its name up with null or nothing. A name that breaks the rules is
    /// turned away here with the problem, without asking the server.
    /// </summary>
    public async Task<ShareOutcome<PublicPageInfo>> SetNameAsync(string? name)
    {
        if (collectionId is not { } collection)
            return NotSynced<PublicPageInfo>();

        var normalized = PublicPageRules.NormalizeName(name);
        if (normalized is not null && PublicPageRules.CheckName(normalized) is { } problem)
            return ShareOutcome<PublicPageInfo>.Failure("The name can't be used.", status: 400, nameProblem: problem);

        await saving.WaitAsync();
        try
        {
            var result = await server.SetNameAsync(collection, new SetPublicPageNameRequest(normalized));
            if (result.Ok && result.Value is { } saved)
                Keep(saved);
            return result;
        }
        finally
        {
            saving.Release();
        }
    }

    /// <summary>Turns the page off for good. Turning it on again makes a new address.</summary>
    public async Task<ShareOutcome<bool>> TurnOffAsync()
    {
        if (collectionId is not { } collection)
            return NotSynced<bool>();

        var result = await server.TurnOffAsync(collection);
        if (result.Ok)
            Keep(null);
        return result;
    }

    private static ShareOutcome<T> NotSynced<T>() => ShareOutcome<T>.Failure("No collection yet.", offline: true);

    /// <summary>Keeps a plant off the page, or lets it show again. It reaches the page after the next sync.</summary>
    public Task SetPlantHiddenAsync(Plant plant, bool hidden) => plants.SetHiddenFromPublicPageAsync(plant, hidden);

    /// <summary>Keeps a cutting off the page, or lets it show again.</summary>
    public Task SetPropagationHiddenAsync(Propagation propagation, bool hidden) => propagations.SetHiddenFromPublicPageAsync(propagation, hidden);

    /// <summary>Keeps a photo off the page, or lets it show again.</summary>
    public Task SetPhotoHiddenAsync(Photo photo, bool hidden) => timeline.SetPhotoHiddenFromPublicPageAsync(photo, hidden);
}
