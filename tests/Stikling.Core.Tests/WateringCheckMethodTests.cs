using Stikling.Core.Care;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class WateringCheckMethodTests
{
    [Fact]
    public void A_plant_with_no_way_of_watering_set_adds_no_line()
    {
        Assert.Empty(WaterChecks.ByMethod([new Plant()]));
    }

    [Fact]
    public void Each_way_among_the_plants_gets_one_line_in_a_fixed_order()
    {
        var lines = WaterChecks.ByMethod([
            new Plant { WaterBy = WaterMethod.Soak },
            new Plant { WaterBy = WaterMethod.Bottom },
            new Plant { WaterBy = WaterMethod.Bottom },
            new Plant()
        ]);

        Assert.Equal(
            [.. WaterChecks.ByMethod([new Plant { WaterBy = WaterMethod.Bottom }]), .. WaterChecks.ByMethod([new Plant { WaterBy = WaterMethod.Soak }])],
            lines);
    }
}
