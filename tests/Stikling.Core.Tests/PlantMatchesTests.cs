using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class PlantMatchesTests
{
    private static readonly Plant Thai = new() { Genus = "Monstera", Species = "deliciosa", Cultivar = "Thai Constellation" };
    private static readonly Plant Plain = new() { Genus = "Monstera", Species = "deliciosa" };
    private static readonly Plant Named = new() { Nickname = "Big Bertha", Genus = "Ficus" };
    private static readonly Plant Died = new() { Genus = "Alocasia", Species = "zebrina", Status = PlantStatus.Died };
    private static readonly Plant Deleted = new() { Genus = "Hoya", DeletedAt = DateTimeOffset.UnixEpoch };

    private static readonly Plant[] All = [Thai, Plain, Named, Died, Deleted];

    [Fact]
    public void Matches_the_same_botanical_name_ignoring_case_and_spaces()
    {
        var draft = new Plant { Genus = " monstera", Species = "Deliciosa ", Cultivar = "thai constellation" };

        Assert.Equal([Thai], PlantMatches.SameName(draft, All));
    }

    [Fact]
    public void A_cultivar_makes_it_a_different_plant()
    {
        var draft = new Plant { Genus = "Monstera", Species = "deliciosa" };

        Assert.Equal([Plain], PlantMatches.SameName(draft, All));
    }

    [Fact]
    public void Matches_the_same_nickname()
    {
        var draft = new Plant { Nickname = "big bertha" };

        Assert.Equal([Named], PlantMatches.SameName(draft, All));
    }

    [Fact]
    public void Plants_that_are_gone_or_deleted_and_the_plant_itself_do_not_count()
    {
        Assert.Empty(PlantMatches.SameName(new Plant { Genus = "Alocasia", Species = "zebrina" }, All));
        Assert.Empty(PlantMatches.SameName(new Plant { Genus = "Hoya" }, All));
        Assert.Empty(PlantMatches.SameName(Thai, All));
    }

    [Fact]
    public void An_empty_draft_matches_nothing()
    {
        Assert.Empty(PlantMatches.SameName(new Plant(), [new Plant()]));
    }
}
