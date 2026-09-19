using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;

namespace Stikling.Core.Tests;

public class PropagationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 19);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly PropagationService service;

    private readonly Plant parent = new() { Nickname = "Big alocasia", Genus = "Alocasia", Species = "zebrina", Location = "Living room" };

    public PropagationServiceTests()
    {
        service = new PropagationService(propagations, plants, timeline, new FixedTime(Now));
        plants.Plants[parent.Id] = parent;
    }

    private static string Label(Enum value) => value switch
    {
        GrowingMedium.Leca => "LECA",
        _ => value.ToString()
    };

    private IEnumerable<TimelineEntry> HistoryOf(Guid id) => timeline.Entries.Where(e => e.SubjectId == id);

    private async Task<Propagation> StartCormsAsync(int count = 3)
    {
        var corms = service.StartFrom(parent);
        corms.Type = PropagationType.Corm;
        corms.Medium = GrowingMedium.Leca;
        corms.InitialCount = count;
        await service.CreateAsync(corms, Label);
        return corms;
    }

    [Fact]
    public void Start_from_copies_names_and_room_from_the_parent()
    {
        var corms = service.StartFrom(parent);

        Assert.Equal(parent.Id, corms.ParentPlantId);
        Assert.Equal("Alocasia zebrina", corms.DisplayName);
        Assert.Null(corms.Nickname);
        Assert.Equal("Living room", corms.Location);
        Assert.Equal(Today, corms.StartedOn);
    }

    [Fact]
    public void Start_from_keeps_the_nickname_when_the_parent_has_no_genus()
    {
        var cutting = service.StartFrom(new Plant { Nickname = "Kitchen basil" });

        Assert.Equal("Kitchen basil", cutting.Nickname);
        Assert.Empty(cutting.Validate());
    }

    [Fact]
    public async Task Create_records_the_start_on_the_propagation_and_the_parent()
    {
        var corms = await StartCormsAsync();

        Assert.Same(corms, propagations.Propagations[corms.Id]);

        var started = Assert.Single(HistoryOf(corms.Id));
        Assert.Equal(TimelineKind.Created, started.Kind);
        Assert.Equal("Started 3× corm in LECA", started.Text);

        var onParent = Assert.Single(HistoryOf(parent.Id));
        Assert.Equal(TimelineKind.Propagated, onParent.Kind);
        Assert.Equal("3× corm in LECA", onParent.Text);
        Assert.Equal(SubjectType.Propagation, onParent.RelatedType);
        Assert.Equal(corms.Id, onParent.RelatedId);
    }

    [Fact]
    public async Task Create_without_a_parent_only_writes_its_own_history()
    {
        var seeds = new Propagation { Genus = "Laurus", Species = "nobilis", Type = PropagationType.Seed, Medium = GrowingMedium.Soil, InitialCount = 5, StartedOn = new DateOnly(2026, 3, 1), Source = "Seed shop" };

        await service.CreateAsync(seeds, Label);

        var entry = Assert.Single(timeline.Entries);
        Assert.Equal(seeds.Id, entry.SubjectId);
        Assert.Equal(new DateOnly(2026, 3, 1), DateOnly.FromDateTime(entry.OccurredAt.UtcDateTime));
    }

    [Fact]
    public async Task Stage_change_is_recorded()
    {
        var corms = await StartCormsAsync();

        await service.SetStageAsync(corms, PropagationStage.Rooting, Label);
        await service.SetStageAsync(corms, PropagationStage.Rooting, Label); // no change, no entry

        var change = Assert.Single(HistoryOf(corms.Id), e => e.Kind == TimelineKind.Change);
        Assert.Equal("Stage: Rooting (was Started)", change.Text);
        Assert.Equal(Now, change.OccurredAt);
    }

    [Fact]
    public async Task Update_records_count_and_setup_changes()
    {
        var corms = await StartCormsAsync();
        var before = corms.Copy();
        corms.InitialCount = 4;
        corms.Medium = GrowingMedium.Sphagnum;
        corms.Container = "Humidity box";

        await service.UpdateAsync(before, corms, Label);

        var change = Assert.Single(HistoryOf(corms.Id), e => e.Kind == TimelineKind.Change);
        Assert.Equal("Count: 4 (was 3)\nNow grows in Sphagnum (was LECA)\nNew setup: Humidity box", change.Text);
    }

    [Fact]
    public async Task Pot_up_creates_plants_with_the_lineage()
    {
        var corms = await StartCormsAsync();

        var made = await service.PotUpAsync(corms, new PotUpRequest(2, Today, "Zebrina", "Bedroom", GrowingMedium.Pon, "Lechuza pot"));

        Assert.Equal(2, made.Count);
        Assert.All(made, plant =>
        {
            Assert.Same(plant, plants.Plants[plant.Id]);
            Assert.Equal(parent.Id, plant.ParentPlantId);
            Assert.Equal(corms.Id, plant.FromPropagationId);
            Assert.Equal(PlantOrigin.Propagated, plant.Origin);
            Assert.Equal("Alocasia zebrina", plant.BotanicalName);
            Assert.Equal("Bedroom", plant.Location);
            Assert.Equal(GrowingMedium.Pon, plant.Medium);
            Assert.Equal(Today, plant.AcquiredOn);

            var created = Assert.Single(HistoryOf(plant.Id));
            Assert.Equal("Potted up from a propagation", created.Text);
            Assert.Equal(corms.Id, created.RelatedId);
        });
        Assert.Equal(["Zebrina 1", "Zebrina 2"], made.Select(p => p.Nickname));

        Assert.Equal(1, corms.RemainingCount);
        Assert.True(corms.IsActive);
        var change = Assert.Single(HistoryOf(corms.Id), e => e.Kind == TimelineKind.Change);
        Assert.Equal("Potted up 2: Zebrina 1, Zebrina 2", change.Text);
        Assert.Null(change.RelatedId); // several plants: they're listed on the page instead

        // The parent now lists them as offspring
        Assert.Equal(2, PlantLineage.Offspring(parent, plants.Plants.Values).Count);
    }

    [Fact]
    public async Task Potting_up_the_last_one_finishes_the_propagation()
    {
        var corms = await StartCormsAsync(2);
        await service.MarkFailedAsync(corms, 1, "rotted");

        var made = await service.PotUpAsync(corms, new PotUpRequest(1, Today));

        Assert.Equal(PropagationStage.Done, corms.Stage);
        var last = HistoryOf(corms.Id).Last();
        Assert.Equal("Potted up 1: Alocasia zebrina\nFinished: 1 potted up, 1 failed", last.Text);
        Assert.Equal(SubjectType.Plant, last.RelatedType);
        Assert.Equal(made[0].Id, last.RelatedId);
    }

    [Fact]
    public async Task Seeds_become_plants_grown_from_seed()
    {
        var seeds = new Propagation { Genus = "Laurus", Type = PropagationType.Seed, InitialCount = 1, StartedOn = Today, Source = "Seed shop" };
        await service.CreateAsync(seeds, Label);

        var made = await service.PotUpAsync(seeds, new PotUpRequest(1, Today));

        Assert.Equal(PlantOrigin.GrownFromSeed, made[0].Origin);
        Assert.Null(made[0].ParentPlantId);
        Assert.Equal("Seed shop", made[0].Source);
    }

    [Fact]
    public async Task Pot_up_saves_nothing_when_the_count_is_too_high()
    {
        var corms = await StartCormsAsync(1);
        var entriesBefore = timeline.Entries.Count;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.PotUpAsync(corms, new PotUpRequest(2, Today)));

        Assert.Single(plants.Plants); // only the parent
        Assert.Equal(entriesBefore, timeline.Entries.Count);
        Assert.Equal(0, corms.PottedUpCount);
    }

    [Fact]
    public async Task Pot_up_saves_nothing_when_the_plants_would_have_no_name()
    {
        var cutting = new Propagation { Nickname = "Kitchen basil", StartedOn = Today };
        await service.CreateAsync(cutting, Label);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PotUpAsync(cutting, new PotUpRequest(1, Today)));

        Assert.Equal(0, cutting.PottedUpCount);
        Assert.Single(plants.Plants);
    }

    [Fact]
    public async Task Marking_all_as_failed_finishes_it_as_failed()
    {
        var corms = await StartCormsAsync(2);

        await service.MarkFailedAsync(corms, 2, "  ");

        Assert.Equal(PropagationStage.Failed, corms.Stage);
        Assert.Equal("2 failed\nFinished: 2 failed", HistoryOf(corms.Id).Last().Text);
    }

    [Theory]
    [InlineData(null, 2, 0, new string?[] { null, null })]
    [InlineData("Coleus", 1, 0, new string?[] { "Coleus" })]
    [InlineData("Coleus", 2, 0, new string?[] { "Coleus 1", "Coleus 2" })]
    [InlineData("Coleus", 1, 2, new string?[] { "Coleus 3" })]
    public void Pot_up_names_are_numbered_within_a_batch(string? nickname, int count, int already, string?[] expected)
    {
        Assert.Equal(expected, PropagationService.PotUpNames(nickname, count, already));
    }
}
