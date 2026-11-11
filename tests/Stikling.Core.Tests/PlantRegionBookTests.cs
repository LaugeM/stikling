using Stikling.Core.Models;
using Stikling.Core.Names;

namespace Stikling.Core.Tests;

public class PlantRegionBookTests
{
    private static PlantRegionBook Book() => new(new PlantRegionData
    {
        Genera =
        [
            new GenusRegions
            {
                Name = "Monstera",
                Family = "Araceae",
                Species = new Dictionary<string, string>
                {
                    ["deliciosa"] = "GUA MXG MXS",
                    ["adansonii"] = "BZE COL",
                    ["adansonii var. laniata"] = "PAN",
                },
            },
            new GenusRegions { Name = "Thaumatophyllum", Now = "Philodendron" },
            new GenusRegions
            {
                Name = "Philodendron",
                Family = "Araceae",
                Species = new Dictionary<string, string> { ["hederaceum"] = "CUB" },
            },
        ],
    });

    [Fact]
    public void Genus_gives_family()
    {
        var origin = Book().Find("Monstera", null, null);
        Assert.Equal("Araceae", origin!.Family);
        Assert.Empty(origin.Areas);
    }

    [Fact]
    public void Species_gives_areas()
    {
        var origin = Book().Find("Monstera", "deliciosa", null);
        Assert.Equal(["GUA", "MXG", "MXS"], origin!.Areas);
        Assert.True(origin.SpeciesFound);
    }

    [Fact]
    public void Case_and_spacing_dont_matter()
    {
        var origin = Book().Find("  monstera ", "DELICIOSA", null);
        Assert.Equal(["GUA", "MXG", "MXS"], origin!.Areas);
    }

    [Fact]
    public void Old_genus_is_followed_to_the_new_one()
    {
        var origin = Book().Find("Thaumatophyllum", "hederaceum", null);
        Assert.Equal("Araceae", origin!.Family);
        Assert.Equal(["CUB"], origin.Areas);
        Assert.Equal("Philodendron", origin.Genus);
    }

    [Fact]
    public void A_current_genus_is_found_under_its_own_name()
    {
        Assert.Equal("Monstera", Book().Find("monstera", null, null)?.Genus);
    }

    [Fact]
    public void Variety_not_listed_uses_its_species()
    {
        var origin = Book().Find("Monstera", "deliciosa var. borsigiana", null);
        Assert.Equal(["GUA", "MXG", "MXS"], origin!.Areas);
        Assert.True(origin.SpeciesFound);
    }

    [Fact]
    public void Variety_with_its_own_entry_uses_it()
    {
        var origin = Book().Find("Monstera", "adansonii var. laniata", null);
        Assert.Equal(["PAN"], origin!.Areas);
    }

    [Fact]
    public void Subspecies_not_listed_uses_its_species()
    {
        var origin = Book().Find("Monstera", "deliciosa subsp. x", null);
        Assert.Equal(["GUA", "MXG", "MXS"], origin!.Areas);
    }

    [Fact]
    public void Unsure_name_gives_the_family_only()
    {
        var origin = Book().Find("Monstera", "deliciosa", NameQualifier.Sp);
        Assert.Equal("Araceae", origin!.Family);
        Assert.Empty(origin.Areas);
    }

    [Fact]
    public void Unknown_genus_gives_null()
    {
        Assert.Null(Book().Find("Nothingia", "deliciosa", null));
        Assert.Null(Book().Find(null, null, null));
    }

    [Fact]
    public void Unknown_species_gives_family_and_no_areas()
    {
        var origin = Book().Find("Monstera", "nothing", null);
        Assert.Equal("Araceae", origin!.Family);
        Assert.Empty(origin.Areas);
        Assert.False(origin.SpeciesFound);
    }

    [Fact]
    public async Task A_failing_source_gives_an_empty_book_and_is_tried_again()
    {
        var source = new FlakySource();
        var service = new PlantRegionService(source);

        Assert.True((await service.GetBookAsync()).IsEmpty);
        Assert.False((await service.GetBookAsync()).IsEmpty);
        Assert.Equal(2, source.Calls);
    }

    private sealed class FlakySource : IPlantRegionSource
    {
        public int Calls;

        public Task<PlantRegionData> LoadAsync()
        {
            if (++Calls == 1)
                throw new HttpRequestException();
            return Task.FromResult(new PlantRegionData { Genera = [new GenusRegions { Name = "Monstera", Family = "Araceae" }] });
        }
    }
}
