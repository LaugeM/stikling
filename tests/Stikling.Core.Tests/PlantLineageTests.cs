using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Text;

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

    private static Plant Named(string name, Plant? parent = null) =>
        new() { Nickname = name, ParentPlantId = parent?.Id };

    [Fact]
    public void Tree_caps_ancestors_at_three_oldest_first()
    {
        var a = Named("A");
        var b = Named("B", a);
        var c = Named("C", b);
        var d = Named("D", c);
        var e = Named("E", d);
        var tree = PlantLineage.Tree(e, [a, b, c, d, e], []);
        Assert.Equal([b, c, d], tree.Ancestors);
        Assert.True(tree.MoreAbove);

        var shorter = PlantLineage.Tree(d, [a, b, c, d, e], []);
        Assert.Equal([a, b, c], shorter.Ancestors);
        Assert.False(shorter.MoreAbove);
    }

    [Fact]
    public void Tree_siblings_exclude_the_plant_and_deleted_ones()
    {
        childB.DeletedAt = DateTimeOffset.UnixEpoch;
        var other = Named("Coleus C", mother);
        var tree = PlantLineage.Tree(childA, [.. all, other], []);
        Assert.Equal([other], tree.Siblings);
    }

    [Fact]
    public void Tree_has_no_siblings_without_a_parent()
    {
        Assert.Empty(PlantLineage.Tree(mother, all, []).Siblings);
        Assert.Empty(PlantLineage.Tree(stranger, all, []).Siblings);
    }

    [Fact]
    public void Tree_below_counts_every_generation_once()
    {
        var great = Named("Coleus A1x", grandchild);
        var tree = PlantLineage.Tree(mother, [.. all, great], []);
        Assert.Equal([childA, childB], tree.Children.Select(c => c.Plant));
        Assert.Equal(2, tree.Children[0].Below);
        Assert.Equal(0, tree.Children[1].Below);
    }

    [Fact]
    public void Tree_growing_leaves_out_done_failed_and_deleted()
    {
        Propagation Prop(PropagationStage stage, int day) =>
            new() { ParentPlantId = mother.Id, Stage = stage, StartedOn = new DateOnly(2026, 1, day) };
        var late = Prop(PropagationStage.Rooting, 9);
        var early = Prop(PropagationStage.Rooting, 2);
        var gone = Prop(PropagationStage.Rooting, 5);
        gone.DeletedAt = DateTimeOffset.UnixEpoch;
        var tree = PlantLineage.Tree(mother, all, [late, Prop(PropagationStage.Done, 1), Prop(PropagationStage.Failed, 1), gone, early]);
        Assert.Equal([early, late], tree.Growing);
    }

    [Fact]
    public void Tree_survives_a_cycle()
    {
        mother.ParentPlantId = grandchild.Id;
        var tree = PlantLineage.Tree(childA, all, []);
        Assert.Equal([grandchild, mother], tree.Ancestors);
        Assert.Single(tree.Children);
        Assert.All(tree.Children, c => Assert.True(c.Below >= 0));
    }

    [Fact]
    public void Tree_with_a_missing_parent_has_no_ancestors_or_siblings()
    {
        var orphan = Named("Orphan");
        orphan.ParentPlantId = Guid.NewGuid();
        var sibling = new Plant { Nickname = "Sibling", ParentPlantId = orphan.ParentPlantId };
        var tree = PlantLineage.Tree(orphan, [orphan, sibling], []);
        Assert.Empty(tree.Ancestors);
        Assert.Empty(tree.Siblings);
        Assert.True(tree.IsEmpty);
    }

    [Fact]
    public void A_plant_without_a_parent_has_no_generation()
    {
        Assert.Null(PlantLineage.GenerationOf(mother, all));
        Assert.Null(PlantLineage.GenerationOf(stranger, all));
    }

    [Fact]
    public void A_chain_of_three_counts_its_ancestors_and_names_the_oldest()
    {
        var first = PlantLineage.GenerationOf(childA, all);
        var second = PlantLineage.GenerationOf(grandchild, all);

        Assert.Equal(1, first!.Number);
        Assert.Equal(2, second!.Number);
        Assert.Same(mother, second.Oldest);
    }

    [Fact]
    public void A_deleted_ancestor_ends_the_chain()
    {
        childA.DeletedAt = DateTimeOffset.UnixEpoch;

        Assert.Null(PlantLineage.GenerationOf(grandchild, all));
    }

    [Fact]
    public void Ancestors_that_died_or_live_elsewhere_still_count()
    {
        mother.Status = PlantStatus.Died;
        childA.LivesElsewhere = true;

        var generation = PlantLineage.GenerationOf(grandchild, all);

        Assert.Equal(2, generation!.Number);
        Assert.Same(mother, generation.Oldest);
    }

    [Fact]
    public void A_cycle_gives_a_generation_instead_of_hanging()
    {
        mother.ParentPlantId = grandchild.Id;

        Assert.Equal(2, PlantLineage.GenerationOf(grandchild, all)!.Number);
    }

    [Fact]
    public void A_propagation_is_one_generation_below_its_parent()
    {
        var fromMother = new Propagation { ParentPlantId = mother.Id };
        var fromGrandchild = new Propagation { ParentPlantId = grandchild.Id };

        var first = PlantLineage.GenerationOf(fromMother, all);
        var third = PlantLineage.GenerationOf(fromGrandchild, all);

        Assert.Equal(1, first!.Number);
        Assert.Same(mother, first.Oldest);
        Assert.Equal(3, third!.Number);
        Assert.Same(mother, third.Oldest);
    }

    [Fact]
    public void A_propagation_without_a_live_parent_has_no_generation()
    {
        Assert.Null(PlantLineage.GenerationOf(new Propagation(), all));
        Assert.Null(PlantLineage.GenerationOf(new Propagation { ParentPlantId = Guid.NewGuid() }, all));

        mother.DeletedAt = DateTimeOffset.UnixEpoch;
        Assert.Null(PlantLineage.GenerationOf(new Propagation { ParentPlantId = mother.Id }, all));
    }

    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(22, "22nd")]
    [InlineData(101, "101st")]
    [InlineData(111, "111th")]
    public void Ordinals_in_English(int number, string expected) =>
        Assert.Equal(expected, Ordinal.Of(number));

    [Fact]
    public void Ordinals_in_Danish_take_a_dot()
    {
        var before = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("da-DK");
            Assert.Equal("1.", Ordinal.Of(1));
            Assert.Equal("3.", Ordinal.Of(3));
            Assert.Equal("12.", Ordinal.Of(12));
            Assert.Equal("111.", Ordinal.Of(111));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = before;
        }
    }

    [Fact]
    public void The_generation_column_sorts_by_number_with_blanks_last()
    {
        var rows = PlantTable.Rows(all, [], new DateOnly(2026, 11, 3), _ => null, _ => null, null, all);

        var up = PlantTable.Sort(rows, new PlantTableSort(PlantColumn.Generation));
        var down = PlantTable.Sort(rows, new PlantTableSort(PlantColumn.Generation, true));

        Assert.Equal([childA, childB, grandchild], up.Take(3).Select(r => r.Plant));
        Assert.Equal(grandchild, down[0].Plant);
        Assert.All(up.TakeLast(2), r => Assert.Null(r.Generation));
        Assert.All(down.TakeLast(2), r => Assert.Null(r.Generation));
    }
}
