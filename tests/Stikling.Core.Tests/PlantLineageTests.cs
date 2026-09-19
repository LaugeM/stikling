using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class PlantLineageTests
{
    // Family: Mother -> Child A -> Grandchild, and Mother -> Child B. Stranger is unrelated.
    private readonly Plant mother = new() { Nickname = "Mother coleus" };
    private readonly Plant childA = new() { Nickname = "Coleus A" };
    private readonly Plant childB = new() { Nickname = "Coleus B" };
    private readonly Plant grandchild = new() { Nickname = "Coleus A1" };
    private readonly Plant stranger = new() { Nickname = "Monstera" };
    private readonly List<Plant> all;

    public PlantLineageTests()
    {
        childA.ParentPlantId = mother.Id;
        childB.ParentPlantId = mother.Id;
        grandchild.ParentPlantId = childA.Id;
        all = [mother, childA, childB, grandchild, stranger];
    }

    [Fact]
    public void Offspring_are_direct_children_only()
    {
        Assert.Equal([childA, childB], PlantLineage.Offspring(mother, all));
        Assert.Equal([grandchild], PlantLineage.Offspring(childA, all));
        Assert.Empty(PlantLineage.Offspring(stranger, all));
    }

    [Fact]
    public void Offspring_skip_deleted_plants()
    {
        childB.DeletedAt = DateTimeOffset.UnixEpoch;

        Assert.Equal([childA], PlantLineage.Offspring(mother, all));
    }

    [Fact]
    public void Ancestors_are_listed_nearest_first()
    {
        Assert.Equal([childA, mother], PlantLineage.Ancestors(grandchild, all));
        Assert.Empty(PlantLineage.Ancestors(mother, all));
    }

    [Fact]
    public void Ancestors_stop_on_a_cycle_instead_of_looping_forever()
    {
        mother.ParentPlantId = grandchild.Id; // corrupt data: a loop

        var ancestors = PlantLineage.Ancestors(grandchild, all);

        Assert.Equal([childA, mother], ancestors);
    }

    [Fact]
    public void PossibleParents_exclude_the_plant_and_its_descendants()
    {
        var options = PlantLineage.PossibleParents(childA, all);

        Assert.Equal([childB, stranger, mother], options);
    }

    [Fact]
    public void PossibleParents_for_a_new_plant_include_everything()
    {
        var options = PlantLineage.PossibleParents(new Plant(), all);

        Assert.Equal(5, options.Count);
    }
}
