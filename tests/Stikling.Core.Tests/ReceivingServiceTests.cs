using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

internal sealed class FakeReceivedPhotos : IReceivedPhotos
{
    /// <summary>Giver photo ids that can't be fetched.</summary>
    public HashSet<Guid> Failing { get; } = [];

    public List<(string Token, Guid GiverId, Guid StoreAs)> Fetched { get; } = [];

    public Task<bool> FetchAsync(string token, Guid giverPhotoId, Guid storeAs)
    {
        lock (Fetched)
            Fetched.Add((token, giverPhotoId, storeAs));
        return Task.FromResult(!Failing.Contains(giverPhotoId));
    }
}

public class ReceivingServiceTests
{
    // Noon on 12 Oct 2026, in UTC
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 12);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly FakePhotoRepository photos = new();
    private readonly FakeReceivedHistoryRepository histories = new();
    private readonly FakeReceivedPhotos fetcher = new();
    private readonly ReceivingService service;

    public ReceivingServiceTests() =>
        service = new ReceivingService(plants, propagations, timeline, photos, histories, fetcher, new FixedTime(Now));

    private static readonly Guid GiverPlant = Guid.NewGuid();
    private static readonly Guid PhotoA = Guid.NewGuid();
    private static readonly Guid PhotoB = Guid.NewGuid();
    private static readonly Guid PhotoC = Guid.NewGuid();

    private static ShareCopyPhoto CopyPhoto(Guid id, int month, int day) =>
        new(id, 1600, 1200, new PhotoFrame(0.3, 0.6, 2), new DateTimeOffset(2026, month, day, 12, 0, 0, TimeSpan.FromHours(2)));

    private static ShareCopy PlantCopy(Guid? cover = null) =>
        new(1, SubjectType.Plant, GiverPlant, "Mona", "Monstera", "deliciosa", "Thai Constellation", null, cover,
        [
            new ShareCopyItem(new DateOnly(2026, 9, 5), "3 days", ShareItemKind.Photos, null, [CopyPhoto(PhotoB, 9, 5)]),
            new ShareCopyItem(new DateOnly(2026, 9, 1), "first day", ShareItemKind.Photos, null, [CopyPhoto(PhotoA, 9, 1)]),
            new ShareCopyItem(new DateOnly(2026, 9, 8), "1 week", ShareItemKind.Note, "Growing", [CopyPhoto(PhotoC, 9, 8)])
        ]);

    private static ShareCopy PropagationCopy(DateOnly? firstRoot = null, DateOnly? rooted = null, DateOnly? started = null) =>
        new(1, SubjectType.Propagation, Guid.NewGuid(), "Mona cutting", null, null, null,
            new ShareCopyPropagation(PropagationType.Corm, GrowingMedium.Leca, started ?? new DateOnly(2026, 8, 1), firstRoot, null, null, rooted),
            null,
            [new ShareCopyItem(new DateOnly(2026, 8, 1), "day 0", ShareItemKind.Milestone, "Started in LECA", [])]);

    [Fact]
    public async Task A_plant_is_added_as_a_gift_from_today_with_the_copys_names()
    {
        var copy = PlantCopy();

        var result = await service.AddAsync("tok", copy,
            new ReceiveRequest("  Mona Jr ", " Anna ", Guid.Parse("11111111-1111-1111-1111-111111111111")));

        var plant = plants.Plants[result.SubjectId];
        Assert.Equal(ReceivingService.SubjectIdOf(copy), plant.Id);
        Assert.Equal("Mona Jr", plant.Nickname);
        Assert.Equal("Monstera", plant.Genus);
        Assert.Equal("deliciosa", plant.Species);
        Assert.Equal("Thai Constellation", plant.Cultivar);
        Assert.Equal(PlantOrigin.Gift, plant.Origin);
        Assert.Equal(LooseDate.Of(Today), plant.AcquiredOn);
        Assert.Equal("Anna", plant.Source);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), plant.PlaceId);
        Assert.Equal(PlantStatus.Active, plant.Status);
    }

    [Fact]
    public void The_subject_ids_come_from_the_giver_so_the_same_copy_makes_the_same_ones()
    {
        var copy = PlantCopy();

        Assert.Equal(NameBasedId.For($"received/Plant/{GiverPlant}"), ReceivingService.SubjectIdOf(copy));
        Assert.Equal(NameBasedId.For($"{ReceivingService.SubjectIdOf(copy)}/history"), ReceivingService.HistoryIdOf(copy));
        Assert.Equal(NameBasedId.For($"{ReceivingService.SubjectIdOf(copy)}/arrived"), ReceivingService.ArrivedEntryIdOf(copy));
        Assert.Equal(NameBasedId.For($"{ReceivingService.HistoryIdOf(copy)}/{PhotoA}"), ReceivingService.PhotoIdOf(copy, PhotoA));
    }

    [Fact]
    public async Task An_empty_nickname_falls_back_to_the_name_only_when_the_copy_has_no_genus()
    {
        var bare = PlantCopy() with { Genus = null, Species = null, Cultivar = null };

        var result = await service.AddAsync("tok", bare, new ReceiveRequest("  ", null, null));
        var named = await service.AddAsync("tok", PlantCopy() with { SubjectId = Guid.NewGuid() }, new ReceiveRequest(null, "", null));

        Assert.Equal("Mona", plants.Plants[result.SubjectId].Nickname);
        Assert.Null(plants.Plants[named.SubjectId].Nickname);
        Assert.Null(plants.Plants[named.SubjectId].Source);
    }

    [Fact]
    public async Task The_arrived_entry_and_the_history_are_written_with_the_derived_ids()
    {
        var copy = PlantCopy();

        var result = await service.AddAsync("tok", copy, new ReceiveRequest(null, "Anna", null));

        var entry = Assert.Single(timeline.Entries);
        Assert.Equal(ReceivingService.ArrivedEntryIdOf(copy), entry.Id);
        Assert.Equal(TimelineKind.Created, entry.Kind);
        Assert.Equal(TimelineEvent.Arrived, entry.Event);
        Assert.Equal(SubjectType.Plant, entry.SubjectType);
        Assert.Equal(result.SubjectId, entry.SubjectId);
        Assert.Equal("From Anna", entry.Text);
        Assert.Equal(Now, entry.OccurredAt);

        var history = Assert.Single(histories.Histories.Values);
        Assert.Equal(ReceivingService.HistoryIdOf(copy), history.Id);
        Assert.Equal(result.SubjectId, history.SubjectId);
        Assert.Equal("Mona", history.Name);
        Assert.Equal(Today, history.ArrivedOn);
        Assert.Equal(3, history.Items.Count);
        Assert.Equal("3 days", history.Items[0].DayCount);
        Assert.Equal("Growing", history.Items[2].Text);
        Assert.Equal([ReceivingService.PhotoIdOf(copy, PhotoB)], history.Items[0].PhotoIds);
    }

    [Fact]
    public async Task Without_a_giver_the_arrived_entry_has_no_text()
    {
        await service.AddAsync("tok", PlantCopy(), new ReceiveRequest(null, null, null));

        Assert.Null(timeline.Entries.Single().Text);
    }

    [Fact]
    public async Task Photos_are_fetched_oldest_first_and_stored_as_the_friends_own_with_the_cover()
    {
        var copy = PlantCopy(cover: PhotoB);

        var result = await service.AddAsync("tok", copy, new ReceiveRequest(null, null, null));

        Assert.Equal(new ReceiveResult(result.SubjectId, 3, 0), result);
        Assert.Equal([PhotoA, PhotoB, PhotoC], fetcher.Fetched.Select(f => f.GiverId).ToArray());
        Assert.All(fetcher.Fetched, f => Assert.Equal("tok", f.Token));
        var photo = photos.Photos[ReceivingService.PhotoIdOf(copy, PhotoA)];
        Assert.Equal(SubjectType.Plant, photo.SubjectType);
        Assert.Equal(result.SubjectId, photo.SubjectId);
        Assert.Equal(ReceivingService.HistoryIdOf(copy), photo.ReceivedHistoryId);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.FromHours(2)), photo.TakenAt);
        Assert.Equal((1600, 1200), (photo.Width, photo.Height));
        Assert.Equal(new PhotoFrame(0.3, 0.6, 2), photo.Frame);
        Assert.Equal(ReceivingService.PhotoIdOf(copy, PhotoB), plants.Plants[result.SubjectId].CoverPhotoId);
    }

    [Fact]
    public async Task Photos_taken_at_the_same_moment_are_fetched_in_id_order_whatever_order_the_copy_lists_them()
    {
        Guid low = Guid.Parse("00000000-0000-0000-0000-000000000001"), high = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var copy = PlantCopy() with
        {
            Items =
            [
                new ShareCopyItem(new DateOnly(2026, 9, 5), "3 days", ShareItemKind.Photos, null, [CopyPhoto(high, 9, 5)]),
                new ShareCopyItem(new DateOnly(2026, 9, 5), "3 days", ShareItemKind.Photos, null, [CopyPhoto(low, 9, 5)]),
                new ShareCopyItem(new DateOnly(2026, 9, 1), "first day", ShareItemKind.Photos, null, [CopyPhoto(PhotoA, 9, 1)])
            ]
        };

        await service.AddAsync("tok", copy, new ReceiveRequest(null, null, null));

        Assert.Equal([PhotoA, low, high], fetcher.Fetched.Select(f => f.GiverId).ToArray());
    }

    [Fact]
    public async Task A_photo_that_fails_gets_no_record_and_is_counted_and_a_failed_cover_is_not_set()
    {
        var copy = PlantCopy(cover: PhotoB);
        fetcher.Failing.Add(PhotoB);
        var reports = new List<ReceiveProgress>();

        var result = await service.AddAsync("tok", copy, new ReceiveRequest(null, null, null), new Progress(reports));

        Assert.Equal(2, result.Copied);
        Assert.Equal(1, result.Failed);
        Assert.False(photos.Photos.ContainsKey(ReceivingService.PhotoIdOf(copy, PhotoB)));
        Assert.Equal(2, photos.Photos.Count);
        Assert.Null(plants.Plants[result.SubjectId].CoverPhotoId);
        Assert.Equal(new ReceiveProgress(3, 3), reports.Last());
        Assert.Equal(3, reports.Count(r => r.Done > 0));
        // The history still lists the id, so a later fetch can fill it in
        Assert.Contains(ReceivingService.PhotoIdOf(copy, PhotoB), histories.Histories.Values.Single().Items.SelectMany(i => i.PhotoIds));
    }

    private sealed class Progress(List<ReceiveProgress> reports) : IProgress<ReceiveProgress>
    {
        public void Report(ReceiveProgress value)
        {
            lock (reports)
                reports.Add(value);
        }
    }

    [Fact]
    public async Task A_propagation_gets_the_copys_dates_clamped_to_today_and_a_stage()
    {
        var copy = PropagationCopy(firstRoot: new DateOnly(2026, 8, 19), rooted: new DateOnly(2026, 10, 13));

        var result = await service.AddAsync("tok", copy, new ReceiveRequest("Cutting", "Anna", null, Count: 3));

        var propagation = propagations.Propagations[result.SubjectId];
        Assert.Equal(PropagationType.Corm, propagation.Type);
        Assert.Equal(GrowingMedium.Leca, propagation.Medium);
        Assert.Equal(new DateOnly(2026, 8, 1), propagation.StartedOn);
        Assert.Equal(new DateOnly(2026, 8, 19), propagation.FirstRootOn);
        Assert.Equal(Today, propagation.RootedOn);
        Assert.Equal(PropagationStage.Rooted, propagation.Stage);
        Assert.Equal(3, propagation.InitialCount);
        Assert.Equal("Anna", propagation.Source);
        Assert.Equal("Cutting", propagation.Nickname);
        Assert.Equal(SubjectType.Propagation, timeline.Entries.Single().SubjectType);
    }

    [Theory]
    [InlineData(false, PropagationStage.Started)]
    [InlineData(true, PropagationStage.Rooting)]
    public async Task A_propagations_stage_follows_its_first_root(bool root, PropagationStage expected)
    {
        var copy = PropagationCopy(firstRoot: root ? new DateOnly(2026, 8, 19) : null);

        var result = await service.AddAsync("tok", copy, new ReceiveRequest(null, null, null, Count: 0));

        var propagation = propagations.Propagations[result.SubjectId];
        Assert.Equal(expected, propagation.Stage);
        Assert.Equal(1, propagation.InitialCount);
    }

    [Fact]
    public async Task A_start_day_ahead_of_here_is_clamped_to_today()
    {
        var copy = PropagationCopy(started: new DateOnly(2026, 10, 13));

        var result = await service.AddAsync("tok", copy, new ReceiveRequest(null, null, null));

        Assert.Equal(Today, propagations.Propagations[result.SubjectId].StartedOn);
    }

    [Fact]
    public async Task Check_tells_new_here_and_deleted_and_counts_missing_photos()
    {
        var copy = PlantCopy();
        var fresh = await service.CheckAsync(copy);
        Assert.Equal(ReceivedState.New, fresh.State);
        Assert.Equal(3, fresh.MissingPhotos);

        fetcher.Failing.Add(PhotoC);
        var added = await service.AddAsync("tok", copy, new ReceiveRequest(null, null, null));
        var here = await service.CheckAsync(copy);
        Assert.Equal(new ReceivedCheck(ReceivedState.Here, added.SubjectId, 1), here);

        await plants.DeleteAsync(added.SubjectId);
        var deleted = await service.CheckAsync(copy);
        Assert.Equal(ReceivedState.Deleted, deleted.State);
    }

    [Fact]
    public async Task Check_works_for_a_propagation_too()
    {
        var copy = PropagationCopy();
        Assert.Equal(ReceivedState.New, (await service.CheckAsync(copy)).State);

        var added = await service.AddAsync("tok", copy, new ReceiveRequest(null, null, null));
        Assert.Equal(ReceivedState.Here, (await service.CheckAsync(copy)).State);

        await propagations.DeleteAsync(added.SubjectId);
        Assert.Equal(ReceivedState.Deleted, (await service.CheckAsync(copy)).State);
    }

    [Fact]
    public async Task Bringing_it_back_after_a_delete_uses_the_same_ids_and_adds_nothing_twice()
    {
        var copy = PlantCopy(cover: PhotoA);
        var first = await service.AddAsync("tok", copy, new ReceiveRequest("Mona", "Anna", null));
        await plants.DeleteAsync(first.SubjectId);
        await histories.DeleteAsync(ReceivingService.HistoryIdOf(copy));

        var again = await service.AddAsync("tok", copy, new ReceiveRequest("Mona", "Anna", null));

        Assert.Equal(first.SubjectId, again.SubjectId);
        Assert.Single(plants.Plants);
        Assert.False(plants.Plants[again.SubjectId].IsDeleted);
        Assert.Single(timeline.Entries);
        Assert.Single(histories.Histories);
        Assert.False(histories.Histories.Values.Single().IsDeleted);
        Assert.Equal(3, photos.Photos.Count);
        Assert.Equal(ReceivedState.Here, (await service.CheckAsync(copy)).State);
    }

    [Fact]
    public async Task Adding_a_copy_that_is_already_here_writes_nothing_over_it()
    {
        var copy = PlantCopy();
        var first = await service.AddAsync("tok", copy, new ReceiveRequest("Mona", null, null));
        plants.Plants[first.SubjectId].Nickname = "Renamed";
        fetcher.Fetched.Clear();

        var again = await service.AddAsync("tok", copy, new ReceiveRequest("Mona", null, null));

        Assert.Equal(new ReceiveResult(first.SubjectId, 0, 0), again);
        Assert.Equal("Renamed", plants.Plants[first.SubjectId].Nickname);
        Assert.Empty(fetcher.Fetched);
    }

    [Fact]
    public async Task Fetching_what_is_missing_fetches_only_the_missing_photos()
    {
        var copy = PlantCopy();
        fetcher.Failing.Add(PhotoB);
        await service.AddAsync("tok", copy, new ReceiveRequest(null, null, null));
        fetcher.Failing.Clear();
        fetcher.Fetched.Clear();

        var result = await service.FetchMissingAsync("tok", copy);

        Assert.Equal(1, result.Copied);
        Assert.Equal(0, result.Failed);
        Assert.Equal(PhotoB, Assert.Single(fetcher.Fetched).GiverId);
        Assert.Equal(3, photos.Photos.Count);
        Assert.Equal(0, (await service.CheckAsync(copy)).MissingPhotos);
    }

    [Fact]
    public async Task A_cover_that_failed_becomes_the_cover_once_it_is_fetched()
    {
        var copy = PlantCopy(cover: PhotoB);
        fetcher.Failing.Add(PhotoB);
        var first = await service.AddAsync("tok", copy, new ReceiveRequest(null, null, null));
        Assert.Null(plants.Plants[first.SubjectId].CoverPhotoId);
        fetcher.Failing.Clear();

        await service.FetchMissingAsync("tok", copy);

        Assert.Equal(ReceivingService.PhotoIdOf(copy, PhotoB), plants.Plants[first.SubjectId].CoverPhotoId);
    }

    [Fact]
    public async Task Fetching_what_is_missing_keeps_a_cover_picked_meanwhile()
    {
        var copy = PlantCopy(cover: PhotoB);
        fetcher.Failing.Add(PhotoB);
        var first = await service.AddAsync("tok", copy, new ReceiveRequest(null, null, null));
        var picked = ReceivingService.PhotoIdOf(copy, PhotoA);
        plants.Plants[first.SubjectId].CoverPhotoId = picked;
        fetcher.Failing.Clear();

        await service.FetchMissingAsync("tok", copy);

        Assert.Equal(picked, plants.Plants[first.SubjectId].CoverPhotoId);
    }
}
