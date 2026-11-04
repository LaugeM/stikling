using Stikling.Core.Leaves;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class LeafServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 4);

    private sealed class Setup
    {
        public FakeLeafRepository Leaves { get; } = new();
        public FakeTimelineRepository Timeline { get; } = new();
        public LeafService Service { get; }
        public Guid PlantId { get; } = Guid.NewGuid();

        public Setup() => Service = new LeafService(Leaves, Timeline, new FixedTime(Now));

        public Task<Leaf> AddAsync(Variegation rating = Variegation.High, DateOnly? day = null, string? note = null, params Guid[] photos) =>
            Service.AddAsync(SubjectType.Plant, PlantId, rating, day ?? Today, note, photos);
    }

    [Fact]
    public async Task Adding_writes_the_leaf_and_a_line_with_photos_and_note()
    {
        var s = new Setup();
        var photo = Guid.NewGuid();

        var leaf = await s.AddAsync(Variegation.Low, Today.AddDays(-1), "  Mostly green  ", photo);

        Assert.Equal("Mostly green", leaf.Note);
        var entry = Assert.Single(s.Timeline.Entries);
        Assert.Equal(leaf.TimelineEntryId, entry.Id);
        Assert.Equal(TimelineEvent.NewLeaf, entry.Event);
        Assert.Equal(TimelineKind.Change, entry.Kind);
        Assert.Equal(new[] { photo }, entry.PhotoIds);
        Assert.Equal("New leaf: Low\nMostly green", entry.Text);
        Assert.Equal("NewLeaf", entry.Lines![0].Key);
        Assert.Equal(2, entry.Lines.Count);
        Assert.Single(await s.Leaves.GetAllAsync());
    }

    [Fact]
    public async Task A_blank_note_is_kept_as_nothing()
    {
        var s = new Setup();
        var leaf = await s.AddAsync(note: "   ");
        Assert.Null(leaf.Note);
        Assert.Single(s.Timeline.Entries.Single().Lines!);
    }

    [Fact]
    public async Task A_future_day_or_no_rating_is_refused()
    {
        var s = new Setup();
        await Assert.ThrowsAsync<ArgumentException>(() => s.AddAsync(day: Today.AddDays(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.EditAsync(new Leaf { SubjectId = s.PlantId, On = Today }));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.AddAsync(SubjectType.Plant, Guid.Empty, Variegation.Low, Today, null, []));
        Assert.Empty(s.Timeline.Entries);
    }

    [Fact]
    public async Task Editing_rewrites_the_line_and_keeps_the_photos()
    {
        var s = new Setup();
        var photo = Guid.NewGuid();
        var leaf = await s.AddAsync(Variegation.High, Today, null, photo);

        leaf.Rating = Variegation.Reverted;
        leaf.On = Today.AddDays(-2);
        leaf.Note = "Green";
        await s.Service.EditAsync(leaf);

        var entry = Assert.Single(s.Timeline.Entries);
        Assert.Equal("New leaf: Reverted\nGreen", entry.Text);
        Assert.Equal(new[] { photo }, entry.PhotoIds);
        Assert.Equal(new FixedTime(Now).MomentOn(Today.AddDays(-2)), entry.OccurredAt);
        Assert.Equal(Variegation.Reverted, (await s.Leaves.GetAsync(leaf.Id))!.Rating);
    }

    [Fact]
    public async Task Removing_a_leaf_with_photos_keeps_a_photo_entry()
    {
        var s = new Setup();
        var photo = Guid.NewGuid();
        var leaf = await s.AddAsync(photos: photo);

        await s.Service.RemoveAsync(leaf);

        Assert.Empty(await s.Leaves.GetAllAsync());
        var entry = Assert.Single(s.Timeline.Entries, e => !e.IsDeleted);
        Assert.Equal(TimelineKind.Photo, entry.Kind);
        Assert.Null(entry.Event);
        Assert.Null(entry.Lines);
        Assert.Equal(new[] { photo }, entry.PhotoIds);
    }

    [Fact]
    public async Task Removing_a_leaf_without_photos_deletes_its_line()
    {
        var s = new Setup();
        var leaf = await s.AddAsync();

        await s.Service.RemoveAsync(leaf);

        Assert.Empty(await s.Leaves.GetAllAsync());
        Assert.All(s.Timeline.Entries, e => Assert.True(e.IsDeleted));
    }

    [Fact]
    public async Task A_plant_shows_the_cuttings_leaves_up_to_the_day_it_was_potted_up()
    {
        var s = new Setup();
        var cutting = Guid.NewGuid();
        var other = Guid.NewGuid();
        var plant = new Plant
        {
            Id = s.PlantId, FromPropagationId = cutting, AcquiredOn = LooseDate.Of(new DateOnly(2026, 10, 10))
        };
        var before = await s.Service.AddAsync(SubjectType.Propagation, cutting, Variegation.Medium, new DateOnly(2026, 10, 1), null, []);
        var onDay = await s.Service.AddAsync(SubjectType.Propagation, cutting, Variegation.High, new DateOnly(2026, 10, 10), null, []);
        await s.Service.AddAsync(SubjectType.Propagation, cutting, Variegation.Low, new DateOnly(2026, 10, 11), null, []);
        await s.Service.AddAsync(SubjectType.Propagation, other, Variegation.Low, new DateOnly(2026, 10, 5), null, []);
        var own = await s.Service.AddAsync(SubjectType.Plant, s.PlantId, Variegation.High, new DateOnly(2026, 10, 20), null, []);

        var shown = await s.Service.ForPlantAsync(plant);

        Assert.Equal([own.Id, onDay.Id, before.Id], shown.Select(l => l.Leaf.Id));
        Assert.Equal([false, true, true], shown.Select(l => l.AsCutting));
    }

    [Fact]
    public async Task Without_a_pot_up_day_a_plant_shows_all_of_the_cuttings_leaves()
    {
        var s = new Setup();
        var cutting = Guid.NewGuid();
        await s.Service.AddAsync(SubjectType.Propagation, cutting, Variegation.Medium, new DateOnly(2026, 10, 1), null, []);
        await s.Service.AddAsync(SubjectType.Propagation, cutting, Variegation.Low, Today, null, []);

        var shown = await s.Service.ForPlantAsync(new Plant { FromPropagationId = cutting });

        Assert.Equal(2, shown.Count);
        Assert.All(shown, l => Assert.True(l.AsCutting));
        Assert.Empty(await s.Service.ForPlantAsync(new Plant()));
    }

    [Fact]
    public async Task A_cuttings_log_has_its_own_leaves_newest_first()
    {
        var s = new Setup();
        var cutting = Guid.NewGuid();
        var older = await s.Service.AddAsync(SubjectType.Propagation, cutting, Variegation.Medium, Today.AddDays(-3), null, []);
        var newer = await s.Service.AddAsync(SubjectType.Propagation, cutting, Variegation.High, Today, null, []);
        await s.AddAsync();

        var shown = await s.Service.ForPropagationAsync(cutting);

        Assert.Equal([newer.Id, older.Id], shown.Select(l => l.Leaf.Id));
        Assert.All(shown, l => Assert.False(l.AsCutting));
    }
}
