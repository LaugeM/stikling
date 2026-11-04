using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Settings;

namespace Stikling.Core.Tests;

public class DictionaryTests
{
    private static readonly PlantNameData Data = new()
    {
        Genera =
        [
            new GenusNames
            {
                Name = "Epipremnum",
                Family = "Araceae",
                Species = ["aureum", "pinnatum"],
                Cultivars = [new CultivarName("Marble Queen", "aureum"), new CultivarName("Neon", "aureum"), new CultivarName("Cebu Blue", "pinnatum")],
            },
            new GenusNames
            {
                Name = "Scindapsus",
                Family = "Araceae",
                Species = ["pictus"],
                Synonyms = new Dictionary<string, string> { ["aureus"] = "Epipremnum aureum" },
            },
            new GenusNames { Name = "Rhaphidophora", Family = "Araceae", Now = "Epipremnum", Species = ["x"], Synonyms = new Dictionary<string, string> { ["korthalsii"] = "Epipremnum pinnatum" } },
            new GenusNames { Name = "Pinguicula", Species = ["moranensis", "vulgaris"] },
            new GenusNames { Name = "Dracaena", Family = "Asparagaceae", Species = ["trifasciata", "fragrans"] },
            new GenusNames { Name = "Sansevieria", Family = "Asparagaceae", Now = "Dracaena", Synonyms = new Dictionary<string, string> { ["trifasciata"] = "Dracaena trifasciata" } },
            new GenusNames { Name = "Hoya", Family = "Apocynaceae", Species = ["carnosa", "carnosa var. compacta", "pubicalyx", "carnosa subsp. x"] },
            new GenusNames { Name = "Begonia", Family = "Begoniaceae", Species = ["rex"] },
        ],
    };

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeWishRepository wishes = new();

    private DictionaryService Service()
    {
        var names = new PlantNameService(new FakePlantNameSource(Data), plants, propagations,
            new SettingsService(new FakeSettingsRepository()), new FakeDeviceLanguages("en-GB"));
        return new DictionaryService(names, plants, propagations, wishes);
    }

    private static DictionaryBrowser Browser() => new(Data, EverydayNameBook.Empty);

    [Fact]
    public void Groups_follow_family_and_genus()
    {
        Assert.Equal(DictionaryGroup.Aroids, DictionaryGroups.Of("Monstera", "Araceae"));
        Assert.Equal(DictionaryGroup.Carnivorous, DictionaryGroups.Of("pinguicula", null));
        Assert.Equal(DictionaryGroup.Succulents, DictionaryGroups.Of("Haworthia", "Asphodelaceae"));
        Assert.Equal(DictionaryGroup.Carnivorous, DictionaryGroups.Of("Nepenthes", "Nepenthaceae"));
        Assert.Equal(DictionaryGroup.Cacti, DictionaryGroups.Of("Mammillaria", "Cactaceae"));
        Assert.Equal(DictionaryGroup.Other, DictionaryGroups.Of("Hoya", "Apocynaceae"));
        Assert.Equal(DictionaryGroup.Other, DictionaryGroups.Of("Mystery", null));
    }

    [Fact]
    public void Groups_list_all_nine_with_counts_and_leave_out_old_genera()
    {
        var groups = Browser().Groups;
        Assert.Equal(Enum.GetValues<DictionaryGroup>(), groups.Select(g => g.Group));
        var aroids = groups[0];
        Assert.Equal(2, aroids.Genera);
        Assert.Equal(3, aroids.Species);
        Assert.Equal(1, groups.Single(g => g.Group == DictionaryGroup.Carnivorous).Genera);
        Assert.Equal(0, groups.Single(g => g.Group == DictionaryGroup.Orchids).Genera);
        Assert.Equal(["Dracaena", "Hoya"], Browser().GeneraIn(DictionaryGroup.Other).Select(g => g.Name));
    }

    [Fact]
    public void Families_are_sorted_and_skip_genera_without_one()
    {
        var families = Browser().Families;
        Assert.Equal(["Apocynaceae", "Araceae", "Asparagaceae", "Begoniaceae"], families.Select(f => f.Name));
        Assert.Equal(new FamilySummary("Araceae", DictionaryGroup.Aroids, 2, 3), families[1]);
        Assert.Equal(["Epipremnum", "Scindapsus"], Browser().GeneraInFamily("araceae").Select(g => g.Name));
        Assert.Empty(Browser().GeneraInFamily("Nothing"));
    }

    [Fact]
    public void Old_genus_is_found_with_its_new_one_but_not_listed()
    {
        var old = Browser().Genus("rhaphidophora");
        Assert.Equal("Epipremnum", old!.Now);
        Assert.Equal("korthalsii", old.OldNames.Single().Old);
        Assert.Equal(new OldName("korthalsii", "Epipremnum", "pinnatum"), old.OldNames.Single());
        Assert.DoesNotContain(Browser().GeneraInFamily("Araceae"), g => g.Name == "Rhaphidophora");
        Assert.Null(Browser().Genus("Nothing"));
    }

    [Fact]
    public void Genus_entry_lists_species_and_cultivars_sorted()
    {
        var genus = Browser().Genus("Epipremnum")!;
        Assert.Equal(DictionaryGroup.Aroids, genus.Group);
        Assert.Equal(["aureum", "pinnatum"], genus.Species.Select(s => s.Name));
        Assert.Equal(["Cebu Blue", "Marble Queen", "Neon"], genus.Cultivars.Select(c => c.Name));
    }

    [Fact]
    public void Species_entry_gathers_cultivars_and_old_names_from_other_genera()
    {
        var species = Browser().Species("Epipremnum", "AUREUM")!;
        Assert.Equal(["Marble Queen", "Neon"], species.Cultivars.Select(c => c.Name));
        Assert.Equal(["Scindapsus aureus"], species.OldNames);
        Assert.Equal(["Rhaphidophora korthalsii"], Browser().Species("Epipremnum", "pinnatum")!.OldNames);
        Assert.Null(Browser().Species("Epipremnum", "nothing"));
        Assert.Null(Browser().Species("Nothing", "aureum"));
    }

    [Fact]
    public void Cultivar_entry_has_its_species_and_group()
    {
        var cultivar = Browser().Cultivar("epipremnum", "marble queen")!;
        Assert.Equal("Marble Queen", cultivar.Name);
        Assert.Equal("aureum", cultivar.Species);
        Assert.Equal(DictionaryGroup.Aroids, cultivar.Group);
        Assert.Null(Browser().Cultivar("Epipremnum", "Nothing"));
    }

    [Fact]
    public async Task Yours_matches_through_an_old_name_and_leaves_out_what_is_gone()
    {
        await plants.SaveAsync(new Plant { Genus = "Sansevieria", Species = "trifasciata" });
        await plants.SaveAsync(new Plant { Genus = "Dracaena", Species = "trifasciata", Status = PlantStatus.Died });
        await plants.SaveAsync(new Plant { Genus = "Dracaena", Species = "fragrans" });
        await propagations.SaveAsync(new Propagation { Genus = "Dracaena", Species = "trifasciata", StartedOn = new(2026, 1, 1) });
        await propagations.SaveAsync(new Propagation { Genus = "Dracaena", Species = "trifasciata", StartedOn = new(2026, 1, 1), Stage = PropagationStage.Failed });
        await wishes.SaveAsync(new Wish { Genus = "Dracaena", Species = "trifasciata" });
        await wishes.SaveAsync(new Wish { Genus = "Dracaena", Species = "trifasciata", GotOn = new(2026, 2, 1) });

        var yours = await Service().GetYoursAsync("dracaena", "Trifasciata");
        Assert.Single(yours.Plants);
        Assert.Single(yours.Propagations);
        Assert.Single(yours.Wishes);

        var wholeGenus = await Service().GetYoursAsync("Dracaena");
        Assert.Equal(2, wholeGenus.Plants.Count);
        Assert.Single(wholeGenus.Propagations);
        Assert.Single(wholeGenus.Wishes);
    }

    [Fact]
    public async Task Yours_species_covers_varieties_and_subspecies_and_cultivar_narrows()
    {
        await plants.SaveAsync(new Plant { Genus = "Hoya", Species = "carnosa" });
        await plants.SaveAsync(new Plant { Genus = "Hoya", Species = "carnosa var. compacta" });
        await plants.SaveAsync(new Plant { Genus = "Hoya", Species = "carnosa subsp. x", Cultivar = "Krimson Queen" });
        await plants.SaveAsync(new Plant { Genus = "Hoya", Species = "pubicalyx" });

        Assert.Equal(3, (await Service().GetYoursAsync("Hoya", "carnosa")).Plants.Count);
        Assert.Single((await Service().GetYoursAsync("Hoya", "carnosa var. compacta")).Plants);
        Assert.Single((await Service().GetYoursAsync("Hoya", "carnosa", "krimson queen")).Plants);
        Assert.Equal(4, (await Service().GetYoursAsync("Hoya")).Plants.Count);
    }

    [Fact]
    public async Task Owned_counts_follow_old_names_and_skip_what_is_gone()
    {
        await plants.SaveAsync(new Plant { Genus = "Sansevieria", Species = "trifasciata" });
        await plants.SaveAsync(new Plant { Genus = "Dracaena", Species = "fragrans" });
        await plants.SaveAsync(new Plant { Genus = "Dracaena", Species = "fragrans", Status = PlantStatus.Sold });
        await plants.SaveAsync(new Plant { Genus = "Hoya", Species = "carnosa var. compacta" });
        await propagations.SaveAsync(new Propagation { Genus = "Dracaena", Species = "trifasciata", StartedOn = new(2026, 1, 1) });
        await propagations.SaveAsync(new Propagation { Genus = "Dracaena", StartedOn = new(2026, 1, 1), Stage = PropagationStage.Failed });
        await wishes.SaveAsync(new Wish { Genus = "Dracaena", Species = "fragrans" });

        var counts = await Service().GetOwnedCountsAsync();
        Assert.Equal(3, counts.Genus("dracaena"));
        Assert.Equal(2, counts.Species("Dracaena", "trifasciata"));
        Assert.Equal(1, counts.Species("Dracaena", "fragrans"));
        Assert.Equal(1, counts.Species("Hoya", "carnosa"));
        Assert.Equal(1, counts.Species("Hoya", "carnosa var. compacta"));
        Assert.Equal(0, counts.Genus("Begonia"));
    }

    [Fact]
    public async Task Browser_is_built_once_and_again_after_a_failed_load()
    {
        var source = new FakePlantNameSource(Data) { Fail = true };
        var names = new PlantNameService(source, plants, propagations, new SettingsService(new FakeSettingsRepository()), new FakeDeviceLanguages("en-GB"));
        var service = new DictionaryService(names, plants, propagations, wishes);

        Assert.Empty((await service.GetBrowserAsync()).GeneraIn(DictionaryGroup.Aroids));
        source.Fail = false;
        var browser = await service.GetBrowserAsync();
        Assert.Equal(2, browser.GeneraIn(DictionaryGroup.Aroids).Count);
        Assert.Same(browser, await service.GetBrowserAsync());
    }
}
