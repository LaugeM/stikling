using Stikling.Core.Examples;
using Stikling.Core.Models;
using Stikling.Core.People;

namespace Stikling.Core.Tests;

public class CollectionCountsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_active_plant_here_is_counted()
    {
        var (plants, cuttings) = CollectionCounts.Of([new Plant(), new Plant()], [new Propagation()]);

        Assert.Equal(2, plants);
        Assert.Equal(1, cuttings);
    }

    [Fact]
    public void A_deleted_plant_or_cutting_is_left_out()
    {
        var (plants, cuttings) = CollectionCounts.Of(
            [new Plant(), new Plant { DeletedAt = Now }], [new Propagation(), new Propagation { DeletedAt = Now }]);

        Assert.Equal(1, plants);
        Assert.Equal(1, cuttings);
    }

    [Fact]
    public void A_plant_that_is_not_here_any_more_is_left_out()
    {
        var (plants, _) = CollectionCounts.Of([new Plant(), new Plant { Status = PlantStatus.Died }, new Plant { Status = PlantStatus.GivenAway }], []);

        Assert.Equal(1, plants);
    }

    [Fact]
    public void A_cutting_that_is_done_or_failed_is_left_out()
    {
        var (_, cuttings) = CollectionCounts.Of(
            [], [new Propagation(), new Propagation { Stage = PropagationStage.Done }, new Propagation { Stage = PropagationStage.Failed }]);

        Assert.Equal(1, cuttings);
    }

    [Fact]
    public void The_example_plants_and_cuttings_are_left_out()
    {
        var (plants, cuttings) = CollectionCounts.Of(
            [new Plant { Id = ExampleFamily.MotherId }, new Plant()],
            [new Propagation { Id = ExampleFamily.FirstCuttingId }, new Propagation()]);

        Assert.Equal(1, plants);
        Assert.Equal(1, cuttings);
    }

    [Fact]
    public void An_empty_collection_has_none()
    {
        Assert.Equal((0, 0), CollectionCounts.Of([], []));
    }
}
