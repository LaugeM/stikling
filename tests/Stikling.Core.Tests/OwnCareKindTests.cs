using Stikling.Core.Care;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class OwnCareKindTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 20);

    private readonly Guid plant = Guid.NewGuid();
    private readonly FakeOwnCareKindRepository kinds = new();
    private readonly FakeCareLogRepository logs = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly OwnCareKindService service;
    private readonly CareService care;

    public OwnCareKindTests()
    {
        service = new OwnCareKindService(kinds, logs);
        care = new CareService(logs, timeline, new FakePlantRepository(), new FakeFeedRepository(), new FakeProductRepository(), kinds, new FixedTime(Now));
    }

    private static string Label(Enum value) => value.ToString();

    private CareLog Own(OwnCareKind kind, DateOnly? on = null) => new()
    {
        PlantId = plant, Kind = kind.LogKind, OwnKindId = kind.Id, OwnKindName = kind.Name, OccurredOn = on ?? Today
    };

    private CareKind StoredKind(CareLog entry) => logs.Logs.Single(l => l.Id == entry.Id).Kind;

    // Adding

    [Fact]
    public async Task The_same_name_twice_is_one_kind()
    {
        var first = await service.AddAsync("Misted", null, false);
        var second = await service.AddAsync(" misted ", CareKind.Watered, true);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(kinds.Kinds);
        Assert.Null(second.CountsAs);
    }

    [Fact]
    public async Task The_id_comes_from_the_name_and_is_the_same_for_every_spelling()
    {
        var kind = await service.AddAsync("Misted", null, false);

        Assert.Equal(OwnCareKind.IdFor("Misted"), kind.Id);
        Assert.Equal(OwnCareKind.IdFor("Misted"), OwnCareKind.IdFor(" misted "));
        Assert.NotEqual(OwnCareKind.IdFor("Misted"), OwnCareKind.IdFor("Wired"));
    }

    [Fact]
    public async Task A_removed_kind_comes_back_with_the_new_settings()
    {
        var first = await service.AddAsync("Misted", null, false);
        await service.RemoveAsync(first.Id);

        var again = await service.AddAsync("Misted", CareKind.Watered, true);

        Assert.Equal(first.Id, again.Id);
        Assert.Single(kinds.Kinds);
        Assert.False(again.IsDeleted);
        Assert.Equal((CareKind.Watered, true), (again.CountsAs, again.OnHistory));
    }

    [Fact]
    public async Task A_renamed_kinds_old_name_gets_a_new_random_id()
    {
        var first = await service.AddAsync("Misted", null, false);
        await service.RenameAsync(first.Id, "Sprayed");

        var again = await service.AddAsync("Misted", null, false);

        Assert.NotEqual(first.Id, again.Id);
        Assert.NotEqual(OwnCareKind.IdFor("Misted"), again.Id);
        Assert.Equal(2, kinds.Kinds.Count);
        Assert.Equal(first.Id, (await service.GetAllAsync()).Single(k => k.Name == "Sprayed").Id);
    }

    [Fact]
    public async Task A_kind_needs_a_name()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync("  ", null, false));
    }

    [Fact]
    public void A_kind_can_only_count_as_a_watering_a_feed_or_neither()
    {
        Assert.Empty(new OwnCareKind { Name = "X", CountsAs = CareKind.Watered }.Validate());
        Assert.Empty(new OwnCareKind { Name = "X", CountsAs = CareKind.Fertilised }.Validate());
        Assert.Empty(new OwnCareKind { Name = "X" }.Validate());
        Assert.NotEmpty(new OwnCareKind { Name = "X", CountsAs = CareKind.Pruned }.Validate());
    }

    // Changing what it counts as

    [Fact]
    public async Task Changing_what_a_kind_counts_as_leaves_the_old_entries_alone_unless_asked()
    {
        var kind = await service.AddAsync("Bottom watered", null, false);
        var entry = Own(kind);
        await logs.SaveAsync(entry);

        Assert.Equal(0, await service.SetCountsAsAsync(kind.Id, CareKind.Watered, false));

        Assert.Equal(CareKind.Watered, kinds.Kinds[kind.Id].CountsAs);
        Assert.Equal(CareKind.Rotated, entry.Kind);
    }

    [Fact]
    public async Task Rewriting_what_it_counts_as_changes_only_that_kinds_live_entries()
    {
        var kind = await service.AddAsync("Bottom watered", null, false);
        var other = await service.AddAsync("Misted", null, false);
        var mine = Own(kind);
        var gone = Own(kind);
        gone.DeletedAt = Now;
        var theirs = Own(other);
        var builtIn = new CareLog { PlantId = plant, Kind = CareKind.Rotated, OccurredOn = Today };
        foreach (var log in new[] { mine, gone, theirs, builtIn })
            await logs.SaveAsync(log);

        var changed = await service.SetCountsAsAsync(kind.Id, CareKind.Watered, true);

        Assert.Equal(1, changed);
        Assert.Equal(CareKind.Watered, StoredKind(mine));
        Assert.Equal(CareKind.Rotated, StoredKind(gone));
        Assert.Equal(CareKind.Rotated, StoredKind(theirs));
        Assert.Equal(CareKind.Rotated, StoredKind(builtIn));
        Assert.Equal(1, await service.EntryCountAsync(kind.Id));
    }

    [Fact]
    public async Task An_entry_with_products_cant_be_rewritten_to_a_plain_kind_and_is_left_as_it_was()
    {
        var kind = await service.AddAsync("Foliar feed", CareKind.Fertilised, false);
        var fed = Own(kind);
        fed.Products = [new ProductDose { Name = "Silica", Amount = 1, Per = 1 }];
        var plain = Own(kind);
        await logs.SaveAsync(fed);
        await logs.SaveAsync(plain);

        var changed = await service.SetCountsAsAsync(kind.Id, null, true);

        Assert.Equal(1, changed);
        Assert.Equal(CareKind.Fertilised, StoredKind(fed));
        Assert.Equal(CareKind.Rotated, StoredKind(plain));
    }

    [Fact]
    public async Task Removing_a_kind_leaves_its_entries_alone()
    {
        var kind = await service.AddAsync("Misted", null, false);
        var entry = Own(kind);
        await logs.SaveAsync(entry);

        await service.RemoveAsync(kind.Id);

        Assert.Empty(await service.GetAllAsync());
        Assert.Single(await logs.GetAllAsync());
        Assert.Equal("Misted", entry.OwnKindName);
    }

    // Validation

    [Fact]
    public void An_entry_of_an_own_kind_needs_its_name_and_a_kind_it_can_count_as()
    {
        var entry = new CareLog { PlantId = Guid.NewGuid(), Kind = CareKind.Rotated, OwnKindId = Guid.NewGuid(), OwnKindName = "Misted" };
        Assert.Empty(entry.Validate(Today));

        entry.OwnKindName = " ";
        Assert.NotEmpty(entry.Validate(Today));

        entry.OwnKindName = "Misted";
        entry.Kind = CareKind.Pruned;
        Assert.NotEmpty(entry.Validate(Today));
    }

    // What it counts as decides the rest

    [Fact]
    public async Task An_own_kind_counted_as_a_watering_is_the_last_watering()
    {
        var kind = await service.AddAsync("Bottom watered", CareKind.Watered, false);

        var last = CareSummary.LastWater([Own(kind, Today.AddDays(-2))], plant, Today);

        Assert.NotNull(last);
        Assert.Equal(2, last.DaysAgo);
        Assert.Equal(kind.Id, last.OwnKindId);
        Assert.NotNull(CareSummary.Latest([Own(kind)], plant, CareKind.Watered, Today));
    }

    [Fact]
    public async Task An_own_kind_counted_as_neither_is_not_a_rotation()
    {
        var kind = await service.AddAsync("Misted", null, false);
        var real = new CareLog { PlantId = plant, Kind = CareKind.Rotated, OccurredOn = Today.AddDays(-5) };

        Assert.Null(CareSummary.Latest([Own(kind)], plant, CareKind.Rotated, Today));
        Assert.Equal(5, CareSummary.Latest([Own(kind), real], plant, CareKind.Rotated, Today)!.DaysAgo);
        Assert.Null(CareSummary.LastWater([Own(kind)], plant, Today));
    }

    [Fact]
    public async Task Each_own_kind_gets_its_own_line_in_the_last_per_kind_list()
    {
        var misted = await service.AddAsync("Misted", null, false);
        var wired = await service.AddAsync("Wired", null, false);

        var lines = CareSummary.LastPerKind([Own(misted), Own(wired), Own(misted, Today.AddDays(-3))], plant, Today);

        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public async Task An_own_kind_counted_as_a_feed_can_carry_products()
    {
        var kind = await service.AddAsync("Foliar feed", CareKind.Fertilised, false);
        var entry = Own(kind);
        entry.Products = [new ProductDose { Name = "Silica", Amount = 1, Per = 1 }];

        Assert.Empty(entry.Validate(Today));
    }

    [Fact]
    public async Task A_log_for_several_plants_keeps_the_own_kind_on_each_entry()
    {
        var kind = await service.AddAsync("Bottom watered", CareKind.Watered, false);
        var second = Guid.NewGuid();

        await care.LogManyAsync([plant, second], kind.LogKind, Today, null, Label, ownKindId: kind.Id, ownKindName: kind.Name);

        var saved = await logs.GetAllAsync();
        Assert.Equal(2, saved.Count);
        Assert.All(saved, l => Assert.Equal((CareKind.Watered, kind.Id, "Bottom watered"), (l.Kind, l.OwnKindId, l.OwnKindName)));
    }

    // The history

    [Fact]
    public async Task An_own_kind_on_the_history_writes_a_line_with_its_name()
    {
        var kind = await service.AddAsync("Wired", null, true);

        await care.LogAsync(Own(kind), Label);

        var written = Assert.Single(timeline.Entries);
        Assert.Equal("Wired", written.Text);
        Assert.Equal(plant, written.SubjectId);
    }

    [Fact]
    public async Task An_own_kind_off_the_history_writes_nothing()
    {
        var kind = await service.AddAsync("Misted", null, false);

        await care.LogAsync(Own(kind), Label);

        Assert.Empty(timeline.Entries);
        Assert.Single(await logs.GetAllAsync());
    }

    // Labels

    [Fact]
    public async Task An_entry_shows_the_kinds_current_name_and_then_the_name_it_was_logged_with()
    {
        var kind = await service.AddAsync("Misted", null, false);
        var entry = Own(kind);

        Assert.Equal("Misted", CareService.Describe(entry, Label, CareLabels.Lookup(await kinds.GetAllAsync())));

        await service.RenameAsync(kind.Id, "Sprayed");
        Assert.Equal("Sprayed", CareService.Describe(entry, Label, CareLabels.Lookup(await kinds.GetAllAsync())));

        await service.RemoveAsync(kind.Id);
        Assert.Equal("Misted", CareService.Describe(entry, Label, CareLabels.Lookup(await kinds.GetAllAsync())));
        Assert.Equal("Misted", CareService.Describe(entry, Label));
    }

    [Fact]
    public void A_built_in_entry_uses_the_built_in_label()
    {
        var entry = new CareLog { PlantId = plant, Kind = CareKind.Rotated, OccurredOn = Today };

        Assert.Equal("Rotated", CareLabels.For(entry, null, Label));
    }

    [Fact]
    public void An_own_entry_with_no_name_left_is_never_called_rotated()
    {
        var entry = new CareLog { PlantId = plant, Kind = CareKind.Rotated, OwnKindId = Guid.NewGuid() };

        Assert.NotEqual("Rotated", CareLabels.For(entry, null, Label));
    }
}
