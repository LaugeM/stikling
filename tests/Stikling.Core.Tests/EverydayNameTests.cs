using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Plants;
using Stikling.Core.Settings;

namespace Stikling.Core.Tests;

public class EverydayNameTests
{
    private static readonly PlantNameData Data = new()
    {
        Genera =
        [
            new GenusNames { Name = "Crassula", Species = ["ovata", "perforata"] },
            new GenusNames
            {
                Name = "Dracaena",
                Species = ["fragrans", "trifasciata"],
                Cultivars = [new("Moonshine", "trifasciata")]
            },
            new GenusNames
            {
                Name = "Epipremnum",
                Species = ["aureum", "pinnatum"],
                Cultivars = [new("Marble Queen", "aureum")]
            },
            new GenusNames { Name = "Hoya", Species = ["carnosa", "obovata"] },
            new GenusNames { Name = "Pachira", Species = ["aquatica"] },
            new GenusNames { Name = "Pothos", Species = ["scandens"] },
            new GenusNames
            {
                Name = "Sansevieria",
                Now = "Dracaena",
                Synonyms = new Dictionary<string, string> { ["trifasciata"] = "Dracaena trifasciata" }
            }
        ]
    };

    private static readonly EverydayNameData English = new()
    {
        Language = "en",
        Sources = [new NameSource("Wikidata", "Everyday names", "CC0", "https://www.wikidata.org")],
        Plants =
        [
            new("Crassula", "ovata", null, ["Jade plant", "Money tree"]),
            new("Dracaena", "trifasciata", null, ["Snake plant", "Mother-in-law's tongue"]),
            new("Epipremnum", "aureum", null, ["Pothos", "Devil's ivy"]),
            new("Epipremnum", "aureum", "Marble Queen", ["Marble queen pothos"]),
            new("Hoya", null, null, ["Wax plant"]),
            new("Hoya", "carnosa", null, ["Hoya carnosa", "Porcelain flower"]),
            new("Pachira", "aquatica", null, ["Money tree"])
        ]
    };

    private static readonly EverydayNameData Danish = new()
    {
        Language = "da",
        Plants =
        [
            new("Dracaena", "trifasciata", null, ["Svigermors tunge"]),
            new("Crassula", "ovata", null, ["Pengetræ"])
        ]
    };

    private static PlantDictionary Dictionary(string shown = "en") => new(Data, [English, Danish], shown);

    private static NameSuggester Suggester(params Plant[] plants) => new(Dictionary(), plants, []);

    private static PlantNameService Service(FakePlantNameSource source, FakeSettingsRepository settings, params string[] device) =>
        new(source, new FakePlantRepository(), new FakePropagationRepository(), new SettingsService(settings), new FakeDeviceLanguages(device));

    [Fact]
    public void Typing_an_everyday_name_finds_the_plant()
    {
        var found = Suggester().Search("snake");

        Assert.Equal(new NameSuggestion("Dracaena", "trifasciata", Everyday: "Snake plant"), found[0]);
    }

    [Fact]
    public void A_later_word_of_an_everyday_name_finds_it_too()
    {
        var tongue = new NameSuggestion("Dracaena", "trifasciata", Everyday: "Mother-in-law's tongue");

        Assert.Contains(tongue, Suggester().Search("tongue"));
        Assert.Contains(tongue, Suggester().Search("mother in law"));
    }

    [Fact]
    public void A_botanical_name_is_found_whole_and_says_what_it_is_called()
    {
        var found = Suggester().Search("dracaena tri");

        Assert.Equal(new NameSuggestion("Dracaena", "trifasciata", Everyday: "Snake plant"), found[0]);
        Assert.Equal(new NameSuggestion("Dracaena", "trifasciata", "Moonshine", Everyday: "Snake plant"), found[1]);
    }

    [Fact]
    public void An_old_name_finds_the_one_it_goes_by_now()
    {
        var found = Suggester().Search("Sansevieria tri");

        Assert.Contains(new NameSuggestion("Dracaena", "trifasciata", Formerly: "Sansevieria trifasciata", Everyday: "Snake plant"), found);
    }

    [Fact]
    public void A_plant_is_listed_once_however_many_of_its_names_match()
    {
        var found = Suggester().Search("pothos");

        Assert.Single(found, f => f is { Genus: "Epipremnum", Species: "aureum", Cultivar: null });
        Assert.Contains(found, f => f.Cultivar == "Marble Queen");
        // The genus Pothos is real too, and still found
        Assert.Contains(found, f => f is { Genus: "Pothos", Species: null });
    }

    [Fact]
    public void Names_on_your_own_plants_come_first()
    {
        var found = Suggester(new Plant { Genus = "Crassula", Species = "perforata" }).Search("crassula");

        Assert.Equal(new NameSuggestion("Crassula", "perforata"), found[0]);
    }

    [Fact]
    public void One_letter_finds_nothing_yet()
    {
        Assert.Empty(Suggester().Search("s"));
        Assert.Empty(Suggester().Search(" "));
    }

    [Fact]
    public void Danish_names_are_found_when_they_are_read()
    {
        Assert.Contains(new NameSuggestion("Dracaena", "trifasciata", Everyday: "Svigermors tunge"), Suggester().Search("svigermor"));
    }

    [Fact]
    public void A_cultivar_with_a_name_of_its_own_shows_it()
    {
        var names = Dictionary().EverydayNames;

        Assert.Equal("Marble queen pothos", names.For("Epipremnum", "aureum", "Marble Queen"));
        Assert.Equal("Pothos", names.For("Epipremnum", "aureum", "N'Joy"));
        Assert.Equal("Snake plant", names.For("dracaena", "Trifasciata", "Moonshine"));
    }

    [Fact]
    public void The_genus_name_is_only_shown_for_a_plant_with_no_species()
    {
        var names = Dictionary().EverydayNames;

        Assert.Equal("Wax plant", names.For("Hoya", null, null));
        Assert.Null(names.For("Hoya", "obovata", null));
        Assert.Null(names.For(null, null, null));
    }

    [Fact]
    public void A_name_that_only_repeats_the_botanical_one_is_skipped()
    {
        Assert.Equal("Porcelain flower", Dictionary().EverydayNames.For("Hoya", "carnosa", null));
    }

    [Fact]
    public void The_chosen_language_is_shown_and_English_fills_in()
    {
        var names = Dictionary("da").EverydayNames;

        Assert.Equal("Svigermors tunge", names.For("Dracaena", "trifasciata", null));
        Assert.Equal("Pothos", names.For("Epipremnum", "aureum", null));
    }

    [Fact]
    public async Task A_device_set_to_Danish_shows_Danish_names_and_finds_both()
    {
        var source = new FakePlantNameSource(Data, English, Danish);
        var service = Service(source, new FakeSettingsRepository(), "da-DK", "en-US");

        Assert.Equal("Svigermors tunge", (await service.GetEverydayNamesAsync()).For("Dracaena", "trifasciata", null));
        Assert.Equal(EverydayNameLanguage.Danish, await service.GetEverydayLanguageAsync());
        Assert.Contains((await service.GetSuggesterAsync()).Search("snake plant"), f => f.Species == "trifasciata");
    }

    [Fact]
    public async Task Other_devices_only_read_the_English_names()
    {
        var source = new FakePlantNameSource(Data, English, Danish);
        var service = Service(source, new FakeSettingsRepository(), "en-GB", "de");

        Assert.Equal("Snake plant", (await service.GetEverydayNamesAsync()).For("Dracaena", "trifasciata", null));
        await service.GetSuggesterAsync();
        Assert.Equal(["en"], source.EverydayLoads);
    }

    [Fact]
    public async Task The_language_chosen_in_Settings_wins_over_the_device()
    {
        var settings = new FakeSettingsRepository();
        var source = new FakePlantNameSource(Data, English, Danish);
        var service = Service(source, settings, "da");
        await service.GetEverydayNamesAsync();

        await service.SetEverydayLanguageAsync(EverydayNameLanguage.English);

        Assert.Equal("Snake plant", (await service.GetEverydayNamesAsync()).For("Dracaena", "trifasciata", null));
        Assert.Equal(EverydayNameLanguage.English, settings.Settings[UserSettings.SettingsId].EverydayNames);
        // Still read, so svigermors tunge is found on a Danish device whichever names are shown
        Assert.Contains((await service.GetSuggesterAsync()).Search("svigermors"), f => f.Species == "trifasciata");
    }

    [Fact]
    public async Task Danish_chosen_on_another_device_is_read_here_too()
    {
        var settings = new FakeSettingsRepository();
        settings.Settings[UserSettings.SettingsId] = new UserSettings { EverydayNames = EverydayNameLanguage.Danish };
        var source = new FakePlantNameSource(Data, English, Danish);
        var service = Service(source, settings, "en-GB");

        Assert.Equal("Pengetræ", (await service.GetEverydayNamesAsync()).For("Crassula", "ovata", null));
    }

    [Fact]
    public async Task When_the_everyday_names_cannot_be_read_the_next_call_tries_again()
    {
        var source = new FakePlantNameSource(Data, English) { Fail = true };
        var service = Service(source, new FakeSettingsRepository(), "en");

        Assert.Null((await service.GetEverydayNamesAsync()).For("Dracaena", "trifasciata", null));

        source.Fail = false;
        Assert.Equal("Snake plant", (await service.GetEverydayNamesAsync()).For("Dracaena", "trifasciata", null));
    }

    [Fact]
    public async Task A_missing_Danish_file_is_tried_again_without_building_the_names_again()
    {
        var source = new FakePlantNameSource(Data, English, Danish);
        source.Missing.Add("da");
        var service = Service(source, new FakeSettingsRepository(), "da");

        var first = await service.GetDictionaryAsync();
        Assert.Same(first, await service.GetDictionaryAsync());
        Assert.Equal("Snake plant", first.EverydayNames.For("Dracaena", "trifasciata", null));

        source.Missing.Clear();
        var later = await service.GetDictionaryAsync();
        Assert.NotSame(first, later);
        Assert.Equal("Svigermors tunge", later.EverydayNames.For("Dracaena", "trifasciata", null));
    }

    [Fact]
    public async Task A_language_chosen_on_another_device_is_used_once_the_settings_arrive()
    {
        var settings = new FakeSettingsRepository();
        var service = Service(new FakePlantNameSource(Data, English, Danish), settings, "en");
        Assert.Equal("Snake plant", (await service.GetEverydayNamesAsync()).For("Dracaena", "trifasciata", null));

        // What sync or a restore does: the record changes underneath, and the service is told
        settings.Settings[UserSettings.SettingsId] = new UserSettings { EverydayNames = EverydayNameLanguage.Danish };
        service.SettingsChanged();

        Assert.Equal("Svigermors tunge", (await service.GetEverydayNamesAsync()).For("Dracaena", "trifasciata", null));
        Assert.Equal(EverydayNameLanguage.Danish, await service.GetEverydayLanguageAsync());
    }

    [Fact]
    public void Quick_add_knows_everyday_names()
    {
        var plant = Assert.Single(QuickAdd.Parse("Snake plant from the market", Dictionary()));

        Assert.Equal(("Dracaena", "trifasciata"), (plant.Genus, plant.Species));
        Assert.Equal("from the market", plant.Notes);
        Assert.Null(plant.Nickname);
    }

    [Fact]
    public void Quick_add_leaves_an_everyday_name_for_two_plants_as_the_nickname()
    {
        var plant = Assert.Single(QuickAdd.Parse("Money tree", Dictionary()));

        Assert.Equal("Money tree", plant.Nickname);
        Assert.Null(plant.Genus);
    }

    [Fact]
    public void The_files_that_come_with_the_app_can_be_read()
    {
        var data = Read<PlantNameData>("plant-names.json");
        var english = Read<EverydayNameData>("everyday-names.en.json");
        var danish = Read<EverydayNameData>("everyday-names.da.json");
        var dictionary = new PlantDictionary(data, [english, danish], "en");
        var suggester = new NameSuggester(dictionary, [], []);

        Assert.Equal("Snake plant", dictionary.EverydayNames.For("Dracaena", "trifasciata", null));
        Assert.Equal(new NameSuggestion("Dracaena", "trifasciata", Everyday: "Snake plant"), suggester.Search("snake plant")[0]);
        Assert.Contains(suggester.Search("svigermors tunge"), f => f is { Genus: "Dracaena", Species: "trifasciata" });
        Assert.Contains(suggester.Search("rubber plant"), f => f is { Genus: "Ficus", Species: "elastica" });
        Assert.Equal("Julestjerne", new PlantDictionary(data, [english, danish], "da").EverydayNames.For("Euphorbia", "pulcherrima", null));

        // Every everyday name is for a genus or species the app knows
        var species = data.Genera.SelectMany(g => g.Species.Select(s => (g.Name, s))).ToHashSet();
        var genera = data.Genera.Select(g => g.Name).ToHashSet();
        foreach (var plant in english.Plants.Concat(danish.Plants))
            Assert.True(plant.Species is null ? genera.Contains(plant.Genus) : species.Contains((plant.Genus, plant.Species)), $"{plant.Genus} {plant.Species}");
    }

    private static T Read<T>(string file) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file)), PlantNameData.JsonOptions)!;
}
