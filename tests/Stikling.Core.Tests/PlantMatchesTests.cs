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

    [Fact]
    public void DiedBefore_matches_the_same_nickname_or_botanical_name_among_plants_that_died()
    {
        var zebrina = new Plant { Genus = "alocasia", Species = "Zebrina ", Status = PlantStatus.Died };
        var bertha = new Plant { Nickname = "Big Bertha", Status = PlantStatus.Died };
        Plant[] plants = [zebrina, bertha, Named, Thai];

        Assert.Equal([zebrina], PlantMatches.DiedBefore(new Plant { Genus = "Alocasia", Species = "zebrina" }, plants));
        Assert.Equal([bertha], PlantMatches.DiedBefore(new Plant { Nickname = "big bertha" }, plants));
    }

    [Fact]
    public void DiedBefore_ignores_active_deleted_and_other_gone_plants()
    {
        var gaveAway = new Plant { Genus = "Hoya", Status = PlantStatus.GivenAway };
        var deletedDied = new Plant { Genus = "Hoya", Status = PlantStatus.Died, DeletedAt = DateTimeOffset.UnixEpoch };
        var active = new Plant { Genus = "Hoya" };

        Assert.Empty(PlantMatches.DiedBefore(new Plant { Genus = "Hoya" }, [gaveAway, deletedDied, active]));
    }

    [Fact]
    public void DiedBefore_ignores_the_draft_itself()
    {
        Assert.Empty(PlantMatches.DiedBefore(Died, [Died]));
    }

    [Fact]
    public void DiedBefore_is_sorted_by_name()
    {
        var b = new Plant { Nickname = "B", Genus = "Ficus", Status = PlantStatus.Died };
        var a = new Plant { Nickname = "a", Genus = "Ficus", Status = PlantStatus.Died };

        Assert.Equal([a, b], PlantMatches.DiedBefore(new Plant { Genus = "Ficus" }, [b, a]));
    }
}
