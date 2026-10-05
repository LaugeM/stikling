using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Settings;

namespace Stikling.Core.Tests;

public class PlantNameTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly PlantNameData Data = new()
    {
        Genera =
        [
            new GenusNames
            {
                Name = "Alocasia",
                Family = "Araceae",
                Species = ["baginda", "reginula", "zebrina", "× mortfontanensis"],
                Cultivars = [new("Polly"), new("Dragon Scale", "baginda"), new("Silver Dragon", "baginda")]
            },
            new GenusNames
            {
                Name = "Epipremnum",
                Family = "Araceae",
                Species = ["aureum", "pinnatum"],
                Cultivars = [new("Marble Queen", "aureum"), new("Cebu Blue", "pinnatum")]
            },
            new GenusNames
            {
                Name = "Monstera",
                Family = "Araceae",
                Species = ["adansonii", "deliciosa"],
                Synonyms = new Dictionary<string, string> { ["borsigiana"] = "Monstera deliciosa" },
                Cultivars = [new("Albo Variegata", "deliciosa"), new("Albo Variegata", "standleyana"), new("Thai Constellation", "deliciosa")]
            },
            new GenusNames
            {
                Name = "Philodendron",
                Family = "Araceae",
                Species = ["hederaceum", "hederaceum var. oxycardium", "xanadu"]
            },
            new GenusNames
            {
                Name = "Scindapsus",
                Family = "Araceae",
                Species = ["pictus"],
                Synonyms = new Dictionary<string, string> { ["aureus"] = "Epipremnum aureum" }
            },
            new GenusNames { Name = "Thaumatophyllum", Family = "Araceae", Now = "Philodendron" }
        ]
    };

    private static NameSuggester Suggester(params Plant[] plants) => new(new PlantDictionary(Data), plants, []);

    private static PlantNameService Service(FakePlantNameSource source, FakePlantRepository? plants = null) =>
        new(source, plants ?? new FakePlantRepository(), new FakePropagationRepository(),
            new SettingsService(new FakeSettingsRepository()), new FakeDeviceLanguages("en-GB"));

    [Fact]
    public void Genera_are_suggested_from_the_first_letters()
    {
        var suggestions = Suggester().Suggest(NameField.Genus, "al", null, null);

        Assert.Equal([new NameSuggestion("Alocasia")], suggestions);
    }

    [Fact]
    public void An_old_genus_suggests_the_one_it_is_now_part_of()
    {
        var suggestion = Assert.Single(Suggester().Suggest(NameField.Genus, "Thauma", null, null));

        Assert.Equal(new NameSuggestion("Philodendron", Formerly: "Thaumatophyllum"), suggestion);
    }

    [Fact]
    public void Species_are_limited_to_the_genus_typed()
    {
        var suggestions = Suggester().Suggest(NameField.Species, "alocasia", "", null);

        Assert.Empty(suggestions); // nothing typed and no plants of your own yet
        Assert.Equal(["zebrina"], Suggester().Suggest(NameField.Species, "alocasia", "z", null).Select(s => s.Species));
        Assert.Empty(Suggester().Suggest(NameField.Species, "Monstera", "z", null));
    }

    [Fact]
    public void A_hybrid_is_found_without_typing_the_hybrid_sign()
    {
        var suggestion = Assert.Single(Suggester().Suggest(NameField.Species, "Alocasia", "mort", null));

        Assert.Equal("× mortfontanensis", suggestion.Species);
    }

    [Fact]
    public void A_variety_is_found_from_its_own_name()
    {
        var suggestion = Assert.Single(Suggester().Suggest(NameField.Species, "Philodendron", "oxy", null));

        Assert.Equal("hederaceum var. oxycardium", suggestion.Species);
    }

    [Fact]
    public void An_old_species_name_fills_in_the_name_it_goes_by_now()
    {
        var suggestion = Assert.Single(Suggester().Suggest(NameField.Species, "Scindapsus", "aur", null));

        Assert.Equal(new NameSuggestion("Epipremnum", "aureum", Formerly: "Scindapsus aureus"), suggestion);
    }

    [Fact]
    public void An_old_name_for_a_species_already_offered_is_left_out()
    {
        var suggestions = Suggester().Suggest(NameField.Species, null, "au", null);

        Assert.Equal([new NameSuggestion("Epipremnum", "aureum")], suggestions);
    }

    [Fact]
    public void Without_a_genus_a_species_takes_two_letters_and_fills_in_the_genus()
    {
        Assert.Empty(Suggester().Suggest(NameField.Species, null, "d", null));

        var suggestion = Assert.Single(Suggester().Suggest(NameField.Species, " ", "deli", null));
        Assert.Equal(new NameSuggestion("Monstera", "deliciosa"), suggestion);
    }

    [Fact]
    public void A_later_word_in_a_cultivar_name_matches_too()
    {
        var suggestion = Assert.Single(Suggester().Suggest(NameField.Cultivar, "Monstera", null, "const"));

        Assert.Equal(new NameSuggestion("Monstera", "deliciosa", "Thai Constellation"), suggestion);
    }

    [Fact]
    public void Cultivars_follow_the_species_and_include_the_ones_without_a_species()
    {
        var suggestions = Suggester().Suggest(NameField.Cultivar, "Alocasia", "baginda", "d");

        Assert.Equal(["Dragon Scale", "Silver Dragon"], suggestions.Select(s => s.Cultivar));
        Assert.Equal(["Polly"], Suggester().Suggest(NameField.Cultivar, "Alocasia", "baginda", "p").Select(s => s.Cultivar));
        Assert.Empty(Suggester().Suggest(NameField.Cultivar, "Alocasia", "zebrina", "dragon"));
    }

    [Fact]
    public void The_cultivars_of_a_species_are_offered_before_anything_is_typed()
    {
        var suggestions = Suggester().Suggest(NameField.Cultivar, "Epipremnum", "aureum", "");

        Assert.Equal(["Marble Queen"], suggestions.Select(s => s.Cultivar));
        Assert.Empty(Suggester().Suggest(NameField.Cultivar, null, null, ""));
    }

    [Fact]
    public void The_same_cultivar_name_on_two_species_is_offered_for_each()
    {
        var suggestions = Suggester().Suggest(NameField.Cultivar, "Monstera", null, "albo");

        Assert.Equal(["deliciosa", "standleyana"], suggestions.Select(s => s.Species));
    }

    [Fact]
    public void A_cultivar_on_its_own_fills_in_genus_and_species()
    {
        var suggestion = Assert.Single(Suggester().Suggest(NameField.Cultivar, null, null, "marble"));

        Assert.Equal(new NameSuggestion("Epipremnum", "aureum", "Marble Queen"), suggestion);
    }

    [Fact]
    public void Nothing_is_offered_that_is_already_filled_in()
    {
        Assert.Empty(Suggester().Suggest(NameField.Genus, "Alocasia", null, null));
        Assert.Empty(Suggester().Suggest(NameField.Species, "Monstera", "deliciosa", null));
        Assert.Empty(Suggester().Suggest(NameField.Cultivar, "Monstera", "deliciosa", "Thai Constellation"));
    }

    [Fact]
    public void Your_own_names_come_first_and_are_offered_before_anything_is_typed()
    {
        var suggester = Suggester(
            new Plant { Genus = "alocasia", Species = "Zebrina" },
            new Plant { Genus = "Alocasia", Species = "reginula", Cultivar = "Black Velvet" },
            new Plant { Genus = "Alocasia", Species = "reginula" },
            new Plant { Genus = "Hoya", Species = "kerrii" });

        Assert.Equal(["Alocasia", "Hoya"], suggester.Suggest(NameField.Genus, "", null, null).Select(s => s.Genus));
        Assert.Equal(["reginula", "zebrina"], suggester.Suggest(NameField.Species, "Alocasia", null, null).Select(s => s.Species));
        Assert.Equal(["kerrii"], suggester.Suggest(NameField.Species, "Hoya", "", null).Select(s => s.Species));
        Assert.Equal(["Black Velvet", "Polly"], suggester.Suggest(NameField.Cultivar, "Alocasia", "reginula", "").Select(s => s.Cultivar));
    }

    [Fact]
    public void Your_own_spelling_of_a_name_that_is_also_in_the_list_is_offered_once()
    {
        var suggester = Suggester(new Plant { Genus = "Alocasia", Species = "zebrina" });

        var suggestion = Assert.Single(suggester.Suggest(NameField.Species, "Alocasia", "zeb", null));
        Assert.Equal(new NameSuggestion("Alocasia", "zebrina"), suggestion);
    }

    [Fact]
    public void Deleted_plants_and_propagations_are_left_out()
    {
        var suggester = new NameSuggester(
            PlantDictionary.Empty,
            [new Plant { Genus = "Hoya", DeletedAt = Now }],
            [new Propagation { Genus = "Pilea", DeletedAt = Now }, new Propagation { Genus = "Ficus" }]);

        Assert.Equal(["Ficus"], suggester.Suggest(NameField.Genus, "", null, null).Select(s => s.Genus));
    }

    [Fact]
    public void Suggestions_stop_at_the_limit()
    {
        var suggestions = Suggester().Suggest(NameField.Cultivar, "Alocasia", null, "d", max: 1);

        Assert.Equal(["Dragon Scale"], suggestions.Select(s => s.Cultivar));
    }

    [Fact]
    public async Task The_names_are_read_once()
    {
        var source = new FakePlantNameSource(Data);
        var service = Service(source);

        await service.GetDictionaryAsync();
        await service.GetDictionaryAsync();

        Assert.Equal(1, source.Loads);
    }

    [Fact]
    public async Task When_the_names_cannot_be_read_the_forms_still_work_and_the_next_call_tries_again()
    {
        var source = new FakePlantNameSource(Data) { Fail = true };
        var plants = new FakePlantRepository();
        await plants.SaveAsync(new Plant { Genus = "Hoya" });
        var service = Service(source, plants);

        var offline = await service.GetSuggesterAsync();
        Assert.Equal(["Hoya"], offline.Suggest(NameField.Genus, "", null, null).Select(s => s.Genus));
        Assert.Empty(offline.Suggest(NameField.Genus, "al", null, null));

        source.Fail = false;
        var online = await service.GetSuggesterAsync();
        Assert.Equal(["Alocasia"], online.Suggest(NameField.Genus, "al", null, null).Select(s => s.Genus));
    }

    [Fact]
    public void The_file_that_comes_with_the_app_can_be_read()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "plant-names.json"));
        var data = JsonSerializer.Deserialize<PlantNameData>(json, PlantNameData.JsonOptions)!;
        var suggester = new NameSuggester(new PlantDictionary(data), [], []);

        Assert.Contains(data.Sources, s => s.Name == "Catalogue of Life");
        // An old Alocasia by another author is now Arisaema. The genus people mean has to win.
        Assert.Contains(new NameSuggestion("Alocasia"), suggester.Suggest(NameField.Genus, "Alocasi", null, null));
        Assert.DoesNotContain(suggester.Suggest(NameField.Genus, "Alocasi", null, null), s => s.Formerly == "Alocasia");
        Assert.Contains(new NameSuggestion("Monstera", "deliciosa"), suggester.Suggest(NameField.Species, "Monstera", "deli", null));
        Assert.Contains(new NameSuggestion("Epipremnum", "aureum", Formerly: "Scindapsus aureus"), suggester.Suggest(NameField.Species, "Scindapsus", "aureus", null));
        Assert.Contains(new NameSuggestion("Monstera", "deliciosa", "Thai Constellation"), suggester.Suggest(NameField.Cultivar, "Monstera", null, "thai"));
    }

    [Theory]
    [InlineData("monstera", "deliciosa", "Thai Constellation", "Monstera deliciosa 'Thai Constellation'")]
    [InlineData(" Alocasia ", " × Mortfontanensis", "'Polly'", "Alocasia × mortfontanensis 'Polly'")]
    [InlineData(null, null, "''", null)]
    public void Botanical_names_are_tidied(string? genus, string? species, string? cultivar, string? expected)
    {
        Assert.Equal(expected, PlantNames.Botanical(genus, species, cultivar));
    }
}
