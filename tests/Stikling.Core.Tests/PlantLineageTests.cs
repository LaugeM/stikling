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

    private Propagation Batch(PropagationType type, int count, Plant? parent = null) =>
        new() { Type = type, InitialCount = count, ParentPlantId = (parent ?? mother).Id };

    [Fact]
    public void Given_counts_units_by_type_and_leaves_out_deleted()
    {
        var gone = Batch(PropagationType.Cutting, 9);
        gone.DeletedAt = DateTimeOffset.UnixEpoch;
        var props = new[] { Batch(PropagationType.Cutting, 5), Batch(PropagationType.Cutting, 3), Batch(PropagationType.Offset, 4), gone, Batch(PropagationType.Cutting, 7, stranger) };

        var given = PlantLineage.Given(mother, all, props);

        Assert.Equal(
            [new KeyValuePair<PropagationType, int>(PropagationType.Cutting, 8), new KeyValuePair<PropagationType, int>(PropagationType.Offset, 4)],
            given.TakenByType);
        Assert.Equal(12, given.Taken);
    }

    [Fact]
    public void Given_counts_rooted_units_like_the_results()
    {
        var rooted = Batch(PropagationType.Cutting, 5);
        rooted.Stage = PropagationStage.Rooted;
        rooted.PottedUpCount = 2;
        var growing = Batch(PropagationType.Cutting, 4);

        Assert.Equal(5, PlantLineage.Given(mother, all, [rooted, growing]).Rooted);
    }

    [Fact]
    public void Given_counts_given_away_sold_and_further_descendants()
    {
        childA.Status = PlantStatus.GivenAway;
        childB.Status = PlantStatus.Sold;

        var given = PlantLineage.Given(mother, all, []);

        Assert.Equal(1, given.GivenAway);
        Assert.Equal(1, given.Sold);
        Assert.Equal(1, given.FromThoseInTurn);
        Assert.True(PlantLineage.Given(stranger, all, []).IsEmpty);
    }

    [Fact]
    public void Given_does_not_hang_on_a_cycle()
    {
        mother.ParentPlantId = grandchild.Id;

        Assert.Equal(1, PlantLineage.Given(mother, all, []).FromThoseInTurn);
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
