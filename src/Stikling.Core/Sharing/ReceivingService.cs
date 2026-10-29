using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Timeline;

namespace Stikling.Core.Sharing;

/// <summary>How fetching a link's copy went.</summary>
public enum ShareCopyStatus
{
    Ok,

    /// <summary>The link doesn't exist, or there is nothing to keep on it.</summary>
    NotFound,

    /// <summary>The link was turned off.</summary>
    Gone,

    /// <summary>The server couldn't be reached.</summary>
    Unreachable
}

public sealed record ShareCopyOutcome(ShareCopyStatus Status, ShareCopy? Copy);

/// <summary>Fetches the frozen copy of a share link from the API. The app implements it.</summary>
public interface IShareCopySource
{
    Task<ShareCopyOutcome> GetAsync(string token);
}

/// <summary>Fetches the images of a share link. The app implements it in JavaScript, like all other photo work.</summary>
public interface IReceivedPhotos
{
    /// <summary>
    /// Fetches the full image of a photo on the link, makes a thumbnail and stores both under
    /// <paramref name="storeAs"/>. Returns false when it couldn't.
    /// </summary>
    Task<bool> FetchAsync(string token, Guid giverPhotoId, Guid storeAs);
}

/// <summary>Whether a copy was received here before.</summary>
public enum ReceivedState
{
    /// <summary>Not here.</summary>
    New,

    /// <summary>Here already, and not deleted.</summary>
    Here,

    /// <summary>Was here and was deleted. Adding it again brings it back with the same ids.</summary>
    Deleted
}

/// <param name="SubjectId">The id the plant or propagation has (or would have) here.</param>
/// <param name="MissingPhotos">How many photos of the copy aren't stored here.</param>
public sealed record ReceivedCheck(ReceivedState State, Guid SubjectId, int MissingPhotos);

/// <param name="Nickname">What the friend calls it. Empty falls back to the giver's name when the copy has no genus.</param>
/// <param name="GivenBy">Who it came from.</param>
/// <param name="Count">How many units, for a propagation.</param>
public sealed record ReceiveRequest(string? Nickname, string? GivenBy, Guid? PlaceId, int Count = 1);

public sealed record ReceiveProgress(int Done, int Total);

public sealed record ReceiveResult(Guid SubjectId, int Copied, int Failed);

/// <summary>
/// Keeps a share link's copy as the friend's own plant or propagation, with the history it had
/// before as a <see cref="ReceivedHistory"/>. Every record has an id made from the copy, so
/// receiving the same copy twice writes the same records and a delete is undone by writing them again.
/// </summary>
public sealed class ReceivingService(
    IPlantRepository plants,
    IPropagationRepository propagations,
    ITimelineRepository timeline,
    IPhotoRepository photos,
    IReceivedHistoryRepository histories,
    IReceivedPhotos receivedPhotos,
    TimeProvider time)
{
    private const int PhotosAtATime = 3;

    public static Guid SubjectIdOf(ShareCopy copy) => NameBasedId.For($"received/{copy.SubjectType}/{copy.SubjectId}");

    public static Guid HistoryIdOf(ShareCopy copy) => NameBasedId.For($"{SubjectIdOf(copy)}/history");

    public static Guid ArrivedEntryIdOf(ShareCopy copy) => NameBasedId.For($"{SubjectIdOf(copy)}/arrived");

    public static Guid PhotoIdOf(ShareCopy copy, Guid giverPhotoId) => NameBasedId.For($"{HistoryIdOf(copy)}/{giverPhotoId}");

    public async Task<ReceivedCheck> CheckAsync(ShareCopy copy)
    {
        var subjectId = SubjectIdOf(copy);
        var missing = await MissingAsync(copy);

        var here = copy.SubjectType == SubjectType.Plant
            ? await plants.GetAsync(subjectId) is not null
            : await propagations.GetAsync(subjectId) is not null;
        if (here)
            return new ReceivedCheck(ReceivedState.Here, subjectId, missing.Count);

        var deleted = copy.SubjectType == SubjectType.Plant
            ? await plants.IsDeletedAsync(subjectId)
            : await propagations.IsDeletedAsync(subjectId);
        return new ReceivedCheck(deleted ? ReceivedState.Deleted : ReceivedState.New, subjectId, missing.Count);
    }

    /// <summary>
    /// Adds the plant or propagation, its first entry, the history and the photos. A copy that is
    /// already here is left as it is, so nothing the person changed is written over.
    /// </summary>
    public async Task<ReceiveResult> AddAsync(string token, ShareCopy copy, ReceiveRequest request, IProgress<ReceiveProgress>? progress = null)
    {
        var subjectId = SubjectIdOf(copy);
        if ((await CheckAsync(copy)).State == ReceivedState.Here)
            return new ReceiveResult(subjectId, 0, 0);

        var today = time.Today();
        var givenBy = Clean(request.GivenBy);
        var nickname = Clean(request.Nickname);
        if (nickname is null && Clean(copy.Genus) is null)
            nickname = Clean(copy.Name);

        if (copy.SubjectType == SubjectType.Plant)
        {
            await plants.SaveAsync(new Plant
            {
                Id = subjectId,
                Nickname = nickname,
                Genus = Clean(copy.Genus),
                Species = Clean(copy.Species),
                Cultivar = Clean(copy.Cultivar),
                NameQualifier = copy.NameQualifier,
                NameNote = Clean(copy.NameNote),
                Origin = PlantOrigin.Gift,
                AcquiredOn = LooseDate.Of(today),
                Source = givenBy,
                PlaceId = request.PlaceId
            });
        }
        else
        {
            await propagations.SaveAsync(PropagationOf(copy, subjectId, nickname, givenBy, request, today));
        }

        var arrived = new TimelineEntry
        {
            Id = ArrivedEntryIdOf(copy),
            SubjectType = copy.SubjectType,
            SubjectId = subjectId,
            Kind = TimelineKind.Created,
            Event = TimelineEvent.Arrived,
            OccurredAt = time.GetLocalNow()
        };
        if (givenBy is not null)
            HistoryText.Set(arrived, [HistoryText.Line("ReceivedFrom", HistoryArg.Of(givenBy))]);
        await timeline.AddAsync(arrived);

        var historyId = HistoryIdOf(copy);
        await histories.SaveAsync(new ReceivedHistory
        {
            Id = historyId,
            SubjectType = copy.SubjectType,
            SubjectId = subjectId,
            Name = copy.Name,
            ArrivedOn = today,
            Items = copy.Items.Select(item => new ReceivedItem
            {
                Day = item.Day,
                DayCount = item.DayCount,
                Kind = item.Kind,
                Text = item.Text,
                PhotoIds = item.Photos.Select(p => PhotoIdOf(copy, p.Id)).ToList()
            }).ToList()
        });

        var everything = Distinct(copy);
        var (copied, failed, done) = await CopyPhotosAsync(token, copy, everything, progress);

        await SetCoverAsync(copy, subjectId, done, onlyWhenNone: false);
        return new ReceiveResult(subjectId, copied, failed);
    }

    /// <summary>Fetches the photos of the copy that aren't stored here, for when some failed the first time.</summary>
    public async Task<ReceiveResult> FetchMissingAsync(string token, ShareCopy copy, IProgress<ReceiveProgress>? progress = null)
    {
        var (copied, failed, done) = await CopyPhotosAsync(token, copy, await MissingAsync(copy), progress);
        // A cover that failed the first time becomes the cover now, unless one was picked meanwhile
        await SetCoverAsync(copy, SubjectIdOf(copy), done, onlyWhenNone: true);
        return new ReceiveResult(SubjectIdOf(copy), copied, failed);
    }

    // The giver's cover becomes the cover here once its photo has been copied
    private async Task SetCoverAsync(ShareCopy copy, Guid subjectId, IReadOnlySet<Guid> done, bool onlyWhenNone)
    {
        if (copy.CoverPhotoId is not { } cover || !done.Contains(cover))
            return;
        var coverId = PhotoIdOf(copy, cover);
        if (copy.SubjectType == SubjectType.Plant)
        {
            if (await plants.GetAsync(subjectId) is { } plant && !(onlyWhenNone && plant.CoverPhotoId is not null))
            {
                plant.CoverPhotoId = coverId;
                await plants.SaveAsync(plant);
            }
        }
        else if (await propagations.GetAsync(subjectId) is { } propagation && !(onlyWhenNone && propagation.CoverPhotoId is not null))
        {
            propagation.CoverPhotoId = coverId;
            await propagations.SaveAsync(propagation);
        }
    }

    private static Propagation PropagationOf(ShareCopy copy, Guid subjectId, string? nickname, string? givenBy, ReceiveRequest request, DateOnly today)
    {
        // The giver's time zone can be a day ahead of here
        DateOnly? Cap(DateOnly? day) => day is { } d && d > today ? today : day;

        var dates = copy.Propagation;
        var started = dates is null ? today : Cap(dates.StartedOn)!.Value;
        var propagation = new Propagation
        {
            Id = subjectId,
            Nickname = nickname,
            Genus = Clean(copy.Genus),
            Species = Clean(copy.Species),
            Cultivar = Clean(copy.Cultivar),
            NameQualifier = copy.NameQualifier,
            NameNote = Clean(copy.NameNote),
            Source = givenBy,
            PlaceId = request.PlaceId,
            StartedOn = started,
            InitialCount = Math.Max(1, request.Count)
        };
        if (dates is not null)
        {
            propagation.Type = dates.Type;
            propagation.Medium = dates.Medium;
            propagation.FirstRootOn = Cap(dates.FirstRootOn);
            propagation.FirstLeafOn = Cap(dates.FirstLeafOn);
            propagation.FirstGerminatedOn = Cap(dates.FirstGerminatedOn);
            propagation.RootedOn = Cap(dates.RootedOn);
        }
        propagation.Stage = propagation.RootedOn is not null ? PropagationStage.Rooted
            : propagation.FirstRootOn is not null || propagation.FirstGerminatedOn is not null ? PropagationStage.Rooting
            : PropagationStage.Started;
        return propagation;
    }

    // Every photo of the copy once, oldest first, and by id when two were taken at the same moment
    private static List<ShareCopyPhoto> Distinct(ShareCopy copy) =>
        copy.Items.SelectMany(i => i.Photos)
            .GroupBy(p => p.Id).Select(g => g.First())
            .OrderBy(p => p.TakenAt).ThenBy(p => p.Id)
            .ToList();

    private async Task<List<ShareCopyPhoto>> MissingAsync(ShareCopy copy)
    {
        var missing = new List<ShareCopyPhoto>();
        foreach (var photo in Distinct(copy))
        {
            if (await photos.GetAsync(PhotoIdOf(copy, photo.Id)) is null)
                missing.Add(photo);
        }
        return missing;
    }

    // Fetches at most three at a time, starting them oldest first, and stores a record for each one that came
    private async Task<(int Copied, int Failed, HashSet<Guid> Done)> CopyPhotosAsync(
        string token, ShareCopy copy, List<ShareCopyPhoto> wanted, IProgress<ReceiveProgress>? progress)
    {
        var subjectId = SubjectIdOf(copy);
        var historyId = HistoryIdOf(copy);
        var done = new HashSet<Guid>();
        var failed = 0;
        var finished = 0;
        var gate = new SemaphoreSlim(1);
        var slots = new SemaphoreSlim(PhotosAtATime);

        progress?.Report(new ReceiveProgress(0, wanted.Count));
        // Each fetch is started from this loop, so they begin in the order of the list.
        // Parallel.ForEachAsync takes them in order too, but its workers race to start them.
        var running = new List<Task>();
        foreach (var photo in wanted)
        {
            await slots.WaitAsync();
            running.Add(CopyOneAsync(photo));
        }
        await Task.WhenAll(running);

        return (done.Count, failed, done);

        async Task CopyOneAsync(ShareCopyPhoto photo)
        {
            try
            {
                var storeAs = PhotoIdOf(copy, photo.Id);
                var fetched = await receivedPhotos.FetchAsync(token, photo.Id, storeAs);
                await StoreAsync(photo, storeAs, fetched);
            }
            finally
            {
                slots.Release();
            }
        }

        async Task StoreAsync(ShareCopyPhoto photo, Guid storeAs, bool fetched)
        {
            await gate.WaitAsync();
            try
            {
                if (fetched)
                {
                    await photos.AddAsync(new Photo
                    {
                        Id = storeAs,
                        SubjectType = copy.SubjectType,
                        SubjectId = subjectId,
                        TakenAt = photo.TakenAt,
                        Width = photo.Width,
                        Height = photo.Height,
                        Frame = photo.Frame,
                        ReceivedHistoryId = historyId
                    });
                    done.Add(photo.Id);
                }
                else
                {
                    failed++;
                }
                progress?.Report(new ReceiveProgress(++finished, wanted.Count));
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private static string? Clean(string? text) => ShareLinkRules.Clean(text);
}
