using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Settings;

namespace Stikling.Core.Tests;

public class NameNoteTests
{
    private static readonly PlantNameData Data = new()
    {
        Genera =
        [
            new GenusNames { Name = "Dracaena", Family = "Asparagaceae", Species = ["trifasciata", "fragrans"] },
            new GenusNames { Name = "Sansevieria", Family = "Asparagaceae", Now = "Dracaena", Synonyms = new Dictionary<string, string> { ["trifasciata"] = "Dracaena trifasciata" } },
            new GenusNames { Name = "Hoya", Family = "Apocynaceae", Species = ["carnosa"] },
        ],
    };

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeWishRepository wishes = new();
    private readonly FakeNameNoteRepository notes = new();

    private PlantNameService Names() =>
        new(new FakePlantNameSource(Data), plants, propagations,
            new SettingsService(new FakeSettingsRepository()), new FakeDeviceLanguages("en-GB"));

    private NameNoteService Service(FakeNameNoteRepository? repository = null) =>
        new(repository ?? notes, Names());

    [Fact]
    public void Keys_ignore_case_and_quotes_and_leave_the_species_out_of_a_cultivar()
    {
        Assert.Equal("hoya", NameNoteService.KeyOf(NameLevel.Genus, " HOYA ", null, null));
        Assert.Equal("hoya carnosa", NameNoteService.KeyOf(NameLevel.Species, "Hoya", "Carnosa", null));
        Assert.Equal("hoya|krimson queen", NameNoteService.KeyOf(NameLevel.Cultivar, "Hoya", "carnosa", "'Krimson Queen'"));
        Assert.Equal("hoya|krimson queen", NameNoteService.KeyOf(NameLevel.Cultivar, "Hoya", null, "Krimson Queen"));
        Assert.Null(NameNoteService.KeyOf(NameLevel.Species, "Hoya", null, null));
        Assert.Null(NameNoteService.KeyOf(NameLevel.Cultivar, "Hoya", "carnosa", " "));
        Assert.Null(NameNoteService.KeyOf(NameLevel.Genus, null, null, null));
    }

    [Fact]
    public async Task Two_devices_that_write_on_the_same_name_write_the_same_record()
    {
        var other = new FakeNameNoteRepository();
        var a = await Service().SaveAsync(NameLevel.Species, "Hoya", "carnosa", null, "Mine like it tight");
        var b = await Service(other).SaveAsync(NameLevel.Species, "hoya ", "Carnosa", null, "Dry between waterings");

        Assert.Equal(a!.Id, b!.Id);
        Assert.Equal(NameNote.IdFor(NameLevel.Species, "hoya carnosa"), a.Id);
        // The levels are told apart, so a genus and a species of the same word aren't one record
        Assert.NotEqual(NameNote.IdFor(NameLevel.Genus, "hoya"), a.Id);
        Assert.NotEqual(NameNote.IdFor(NameLevel.Species, "hoya carnosa"), NameNote.IdFor(NameLevel.Cultivar, "hoya carnosa"));
    }

    [Fact]
    public async Task An_old_name_finds_and_writes_the_note_on_the_current_one()
    {
        await Service().SaveAsync(NameLevel.Species, "Dracaena", "trifasciata", null, "Hardy");

        var found = await Service().GetAsync(NameLevel.Species, "Sansevieria", "trifasciata");
        Assert.Equal("Hardy", found!.Text);
        Assert.Equal("dracaena trifasciata", found.Key);

        // Writing on the old name edits the same note instead of making another
        await Service().SaveAsync(NameLevel.Species, "Sansevieria", "trifasciata", null, "Very hardy");
        Assert.Single(notes.Notes);
        Assert.Equal("Very hardy", (await Service().GetAsync(NameLevel.Species, "Dracaena", "trifasciata"))!.Text);

        // The genus is followed too
        await Service().SaveAsync(NameLevel.Genus, "Sansevieria", null, null, "Snake plants");
        Assert.Equal("dracaena", (await Service().GetAsync(NameLevel.Genus, "Dracaena"))!.Key);
    }

    [Fact]
    public async Task Empty_text_removes_the_note_and_writing_again_brings_it_back()
    {
        var first = await Service().SaveAsync(NameLevel.Genus, "Hoya", null, null, "Likes it tight");
        Assert.Null(await Service().SaveAsync(NameLevel.Genus, "Hoya", null, null, "   "));
        Assert.Null(await Service().GetAsync(NameLevel.Genus, "Hoya"));
        Assert.True(notes.Notes[first!.Id].IsDeleted);

        var back = await Service().SaveAsync(NameLevel.Genus, "Hoya", null, null, "Likes it tighter");

        Assert.Equal(first.Id, back!.Id);
        Assert.Single(notes.Notes);
        Assert.False(back.IsDeleted);
        Assert.Equal("Likes it tighter", (await Service().GetAsync(NameLevel.Genus, "Hoya"))!.Text);
    }

    [Fact]
    public async Task Emptying_a_name_that_has_no_note_makes_nothing()
    {
        Assert.Null(await Service().SaveAsync(NameLevel.Genus, "Hoya", null, null, ""));
        Assert.Empty(notes.Notes);
    }

    [Fact]
    public async Task Saving_the_same_text_again_does_not_touch_the_note()
    {
        await Service().SaveAsync(NameLevel.Genus, "Hoya", null, null, "Tight");
        var saves = notes.Saves;
        await Service().SaveAsync(NameLevel.Genus, "Hoya", null, null, " Tight ");
        Assert.Equal(saves, notes.Saves);
    }

    [Fact]
    public async Task A_name_without_what_the_level_needs_is_refused()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().SaveAsync(NameLevel.Species, "Hoya", null, null, "x"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().SaveAsync(NameLevel.Genus, null, null, null, "x"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().SaveAsync(NameLevel.Genus, "Hoya", null, null, new string('a', NameNote.MaxLength + 1)));
    }

    [Fact]
    public async Task A_plant_gets_the_notes_that_are_written_on_its_names_genus_first()
    {
        await Service().SaveAsync(NameLevel.Cultivar, "Hoya", "carnosa", "Krimson Queen", "Slow");
        await Service().SaveAsync(NameLevel.Genus, "Hoya", null, null, "Tight pots");

        var found = await Service().ForNameAsync("Hoya", "carnosa", "krimson queen");
        Assert.Equal([NameLevel.Genus, NameLevel.Cultivar], found.Select(n => n.Level));
        Assert.Single(await Service().ForNameAsync("Hoya", "pubicalyx", null));
        Assert.Empty(await Service().ForNameAsync("Dracaena", "fragrans", null));
        Assert.Empty(await Service().ForNameAsync(null, null, null));
    }

    [Fact]
    public async Task Yours_carries_the_note_what_was_had_and_how_each_medium_went()
    {
        var service = new DictionaryService(Names(), plants, propagations, wishes, Service());
        await plants.SaveAsync(new Plant { Genus = "Hoya", Species = "carnosa" });
        await plants.SaveAsync(new Plant { Genus = "Hoya", Species = "carnosa", Status = PlantStatus.Died });
        await plants.SaveAsync(new Plant { Genus = "Hoya", Species = "carnosa", Status = PlantStatus.Sold });
        await plants.SaveAsync(new Plant { Genus = "Dracaena", Species = "fragrans", Status = PlantStatus.Died });
        await propagations.SaveAsync(new Propagation { Genus = "Hoya", Species = "carnosa", StartedOn = new(2026, 1, 1), Medium = GrowingMedium.Water, Stage = PropagationStage.Failed });
        await propagations.SaveAsync(new Propagation { Genus = "Hoya", Species = "carnosa", StartedOn = new(2026, 2, 1), Medium = GrowingMedium.Water });
        await propagations.SaveAsync(new Propagation { Genus = "Hoya", Species = "carnosa", StartedOn = new(2026, 2, 1), Medium = GrowingMedium.Perlite });
        await propagations.SaveAsync(new Propagation { Genus = "Dracaena", StartedOn = new(2026, 2, 1), Medium = GrowingMedium.Water });
        await Service().SaveAsync(NameLevel.Species, "Hoya", "carnosa", null, "Tight");

        var species = await service.GetYoursAsync("Hoya", "carnosa");
        Assert.Single(species.Plants);
        Assert.Equal(2, species.Had.Count);
        Assert.Equal(2, species.Propagations.Count);
        Assert.Single(species.Past);
        Assert.Equal("Tight", species.Note!.Text);
        Assert.True(species.HasAnything);
        Assert.Equal(2, species.Results.Single(r => r.Key == GrowingMedium.Water).Batches);
        Assert.Equal(2, species.Results.Count);

        // The genus has no note of its own, and covers the species' plants
        var genus = await service.GetYoursAsync("Hoya");
        Assert.Null(genus.Note);
        Assert.Equal(2, genus.Had.Count);

        var nothing = await service.GetYoursAsync("Hoya", "carnosa", "Nothing");
        Assert.False(nothing.HasAnything);
        Assert.Null(nothing.Note);
        Assert.Empty(nothing.Results);
    }
}
