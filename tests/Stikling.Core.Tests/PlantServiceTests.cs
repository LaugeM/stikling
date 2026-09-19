using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class PlantServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 18, 0, 0, TimeSpan.Zero);

    private readonly FakePlantRepository plants = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly PlantService service;

    public PlantServiceTests() => service = new PlantService(plants, timeline, new FixedTime(Now));

    private static string Label(Enum value) => value.ToString();

    [Fact]
    public async Task Create_saves_the_plant_and_records_it_on_the_acquired_date()
    {
        var plant = new Plant { Nickname = "Basil", AcquiredOn = new DateOnly(2026, 5, 1) };

        await service.CreateAsync(plant);

        Assert.Same(plant, plants.Plants[plant.Id]);
        var entry = Assert.Single(timeline.Entries);
        Assert.Equal(TimelineKind.Created, entry.Kind);
        Assert.Equal(plant.Id, entry.SubjectId);
        Assert.Equal(new DateOnly(2026, 5, 1), DateOnly.FromDateTime(entry.OccurredAt.UtcDateTime));
        Assert.Equal("Added to collection", entry.Text);
    }

    [Fact]
    public async Task Create_without_a_date_uses_now()
    {
        await service.CreateAsync(new Plant { Nickname = "Coleus", AcquiredOn = null, Origin = PlantOrigin.Propagated });

        var entry = Assert.Single(timeline.Entries);
        Assert.Equal(Now, entry.OccurredAt);
        Assert.Equal("Added as a propagated plant", entry.Text);
    }

    [Fact]
    public async Task Create_rejects_invalid_plants_without_writing_history()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new Plant()));

        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task Update_records_what_changed_in_one_entry()
    {
        var plant = new Plant { Nickname = "Big alocasia", Location = "Living room" };
        await service.CreateAsync(plant);
        var before = plant.Copy();
        plant.Location = "Bedroom";
        plant.Medium = GrowingMedium.Leca;

        await service.UpdateAsync(before, plant, Label);

        var change = timeline.Entries.Single(e => e.Kind == TimelineKind.Change);
        Assert.Equal("Moved from Living room to Bedroom\nNow grows in Leca (was Soil)", change.Text);
    }

    [Fact]
    public async Task Update_without_tracked_changes_adds_no_entry()
    {
        var plant = new Plant { Nickname = "Jade" };
        await service.CreateAsync(plant);
        var before = plant.Copy();
        plant.Notes = "Only notes";

        await service.UpdateAsync(before, plant, Label);

        Assert.DoesNotContain(timeline.Entries, e => e.Kind == TimelineKind.Change);
    }

    [Fact]
    public async Task SetStatus_skips_plants_that_already_have_it()
    {
        var alive = new Plant { Nickname = "Parsley" };
        var dead = new Plant { Nickname = "Old basil", Status = PlantStatus.Died };
        plants.Plants[alive.Id] = alive;
        plants.Plants[dead.Id] = dead;

        await service.SetStatusAsync([alive, dead], PlantStatus.Died, Label);

        Assert.Equal(PlantStatus.Died, alive.Status);
        var entry = Assert.Single(timeline.Entries);
        Assert.Equal(alive.Id, entry.SubjectId);
        Assert.Equal("Status: Died (was Active)", entry.Text);
    }

    [Fact]
    public async Task AddNote_adds_the_same_trimmed_note_to_each_plant()
    {
        var a = new Plant { Nickname = "Philodendron A" };
        var b = new Plant { Nickname = "Philodendron B" };

        await service.AddNoteAsync([a, b], "  Sprayed for thrips  ");

        Assert.Equal(2, timeline.Entries.Count);
        Assert.All(timeline.Entries, e =>
        {
            Assert.Equal(TimelineKind.Note, e.Kind);
            Assert.Equal("Sprayed for thrips", e.Text);
            Assert.Equal(Now, e.OccurredAt);
        });
        Assert.Equal([a.Id, b.Id], timeline.Entries.Select(e => e.SubjectId));
    }

    [Fact]
    public async Task AddNote_requires_text()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddNoteAsync([new Plant { Nickname = "X" }], "  "));
    }
}
