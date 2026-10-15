using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class PlantCareBookTests
{
    private static PlantCareBook Book() => new(new PlantCareData
    {
        Sources =
        [
            new CareSource("plants.ces.ncsu.edu", "NC State Extension"),
            new CareSource("www.aspca.org", "ASPCA")
        ],
        Plants =
        [
            new PlantCare { Name = "Dracaena", Cats = Toxicity.Toxic },
            new PlantCare { Name = "Dracaena trifasciata", Light = LightLevel.Low },
            new PlantCare { Name = "Dracaena fragrans", Light = LightLevel.BrightIndirect },
            new PlantCare { Name = "Schlumbergera × buckleyi", Humidity = HumidityNeed.High },
            new PlantCare { Name = "Epipremnum aureum", Water = WaterNeed.DryOut },
            new PlantCare { Name = "Monstera", Light = LightLevel.Medium }
        ],
        Formerly = new Dictionary<string, string>
        {
            ["Sansevieria"] = "Dracaena",
            ["Sansevieria trifasciata"] = "Dracaena trifasciata",
            ["Scindapsus aureus"] = "Epipremnum aureum"
        }
    });

    [Fact]
    public void A_species_line_is_found()
    {
        Assert.Equal(LightLevel.Low, Book().Find("Dracaena", "trifasciata")?.Light);
    }

    [Theory]
    [InlineData("dracaena", "TRIFASCIATA")]
    [InlineData("  Dracaena ", " trifasciata  ")]
    public void Case_and_spacing_do_not_count(string genus, string species)
    {
        Assert.Equal("Dracaena trifasciata", Book().Find(genus, species)?.Name);
    }

    [Fact]
    public void The_hybrid_sign_does_not_count()
    {
        Assert.Equal("Schlumbergera × buckleyi", Book().Find("Schlumbergera", "buckleyi")?.Name);
        Assert.Equal("Schlumbergera × buckleyi", Book().Find("schlumbergera", "× buckleyi")?.Name);
    }

    [Fact]
    public void An_old_full_name_finds_the_line_of_the_name_it_goes_by_now()
    {
        Assert.Equal("Epipremnum aureum", Book().Find("Scindapsus", "aureus")?.Name);
        Assert.Equal("Dracaena trifasciata", Book().Find("Sansevieria", "trifasciata")?.Name);
    }

    [Fact]
    public void A_species_without_a_line_uses_the_genus_line()
    {
        Assert.Equal("Monstera", Book().Find("Monstera", "deliciosa")?.Name);
    }

    [Fact]
    public void A_plant_with_only_a_genus_uses_the_genus_line()
    {
        Assert.Equal("Monstera", Book().Find("Monstera", null)?.Name);
        Assert.Equal("Monstera", Book().Find("Monstera", "  ")?.Name);
    }

    [Fact]
    public void A_species_line_wins_and_is_not_merged_with_the_genus_line()
    {
        var care = Book().Find("Dracaena", "trifasciata");

        Assert.Equal("Dracaena trifasciata", care?.Name);
        Assert.Equal(LightLevel.Low, care?.Light);
        Assert.Null(care?.Cats);
    }

    [Fact]
    public void An_old_genus_name_reaches_the_current_genus_line()
    {
        Assert.Equal("Dracaena", Book().Find("Sansevieria", null)?.Name);
        Assert.Equal("Dracaena", Book().Find("Sansevieria", "cylindrica")?.Name);
    }

    [Fact]
    public void An_unknown_name_and_an_empty_genus_give_nothing()
    {
        var book = Book();

        Assert.Null(book.Find("Ficus", "elastica"));
        Assert.Null(book.Find("", "trifasciata"));
        Assert.Null(book.Find(null, null));
    }

    [Fact]
    public void SourceName_is_the_name_for_the_host_or_else_the_host()
    {
        var book = Book();

        Assert.Equal("NC State Extension", book.SourceName("https://plants.ces.ncsu.edu/plants/ficus-elastica/"));
        Assert.Equal("example.org", book.SourceName("https://example.org/page"));
    }

    [Fact]
    public void The_shipped_file_finds_pothos_by_an_old_name_and_leaves_gaps_empty()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "Stikling.Web", "wwwroot", "data", "plant-care.json");
        var data = JsonSerializer.Deserialize<PlantCareData>(File.ReadAllText(path), PlantNameData.JsonOptions)!;
        var book = new PlantCareBook(data);

        Assert.False(book.IsEmpty);
        Assert.NotNull(book.Find("Epipremnum", "aureum"));
        Assert.Equal("Dracaena trifasciata", book.Find("Sansevieria", "trifasciata")?.Name);
        var rubber = book.Find("Ficus", "elastica");
        Assert.NotNull(rubber);
        Assert.Null(rubber.Cats);
    }

    [Fact]
    public async Task The_service_reads_once_and_tries_again_after_a_failure()
    {
        var source = new FakeSource { Fail = true };
        var service = new PlantCareService(source);

        Assert.True((await service.GetBookAsync()).IsEmpty);

        source.Fail = false;
        var book = await service.GetBookAsync();
        Assert.False(book.IsEmpty);
        Assert.Same(book, await service.GetBookAsync());
        Assert.Equal(2, source.Loads);
    }

    private sealed class FakeSource : IPlantCareSource
    {
        public bool Fail { get; set; }
        public int Loads { get; private set; }

        public Task<PlantCareData> LoadAsync()
        {
            Loads++;
            return Fail
                ? throw new HttpRequestException("offline")
                : Task.FromResult(new PlantCareData { Plants = [new PlantCare { Name = "Monstera" }] });
        }
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Stikling.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Stikling.slnx not found above the test folder");
    }
}
