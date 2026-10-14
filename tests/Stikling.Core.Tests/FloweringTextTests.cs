using Stikling.Core.Flowering;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class FloweringTextTests
{
    private static readonly Guid PlantId = Guid.NewGuid();
    private static int made;

    private static FloweringMark Mark(FloweringStage stage, LooseDate on) =>
        new() { PlantId = PlantId, Stage = stage, On = on, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(made++) };

    private static LooseDate Day(int year, int month, int day) => LooseDate.Of(new DateOnly(year, month, day));

    private static FloweringPeriod Last(params FloweringMark[] marks) => Flowerings.Of(marks, PlantId)[^1];

    [Fact]
    public void An_open_flowering_says_since_when()
    {
        var buds = Last(Mark(FloweringStage.Buds, Day(2026, 3, 3)));
        var flowers = Last(Mark(FloweringStage.Buds, Day(2026, 3, 3)), Mark(FloweringStage.InFlower, Day(2026, 4, 10)));

        Assert.Equal("In bud since 3 Mar", FloweringText.Describe(buds));
        Assert.Equal("In flower since 10 Apr", FloweringText.Describe(flowers));
    }

    [Fact]
    public void A_closed_flowering_joins_what_is_known()
    {
        var all = Last(
            Mark(FloweringStage.Buds, Day(2026, 3, 3)),
            Mark(FloweringStage.InFlower, Day(2026, 4, 10)),
            Mark(FloweringStage.Done, Day(2026, 5, 28)));
        var noBuds = Last(Mark(FloweringStage.InFlower, Day(2026, 4, 10)), Mark(FloweringStage.Done, Day(2026, 5, 28)));
        var budsOnly = Last(Mark(FloweringStage.Buds, Day(2026, 3, 3)), Mark(FloweringStage.Done, Day(2026, 4, 1)));

        Assert.Equal("Spike 3 Mar, in flower 10 Apr to 28 May", FloweringText.Describe(all));
        Assert.Equal("In flower 10 Apr to 28 May", FloweringText.Describe(noBuds));
        Assert.Equal("Spike 3 Mar, done 1 Apr", FloweringText.Describe(budsOnly));
    }

    [Fact]
    public void One_closed_by_a_later_flowering_has_no_end()
    {
        var all = Flowerings.Of(
            [Mark(FloweringStage.InFlower, Day(2025, 4, 10)), Mark(FloweringStage.InFlower, Day(2026, 4, 2))], PlantId);
        // Same stage again only confirms, so start a new flowering with buds
        var closed = Flowerings.Of(
            [Mark(FloweringStage.InFlower, Day(2025, 4, 10)), Mark(FloweringStage.Buds, Day(2026, 3, 1))], PlantId);

        Assert.Single(all);
        Assert.True(closed[0].EndUnknown);
        Assert.Equal("In flower from 10 Apr", FloweringText.Describe(closed[0]));
        Assert.Equal("In bud since 1 Mar", FloweringText.Describe(closed[1]));
    }

    [Fact]
    public void A_lone_done_reads_as_done_flowering()
    {
        Assert.Equal("Done flowering 28 May", FloweringText.Describe(Last(Mark(FloweringStage.Done, Day(2026, 5, 28)))));
    }

    [Fact]
    public void A_month_or_a_year_keeps_its_year_and_a_day_in_another_year_does_too()
    {
        var month = Last(Mark(FloweringStage.InFlower, LooseDate.Of(2025, 5)), Mark(FloweringStage.Done, LooseDate.Of(2025, 6)));
        var across = Last(Mark(FloweringStage.InFlower, Day(2025, 12, 20)), Mark(FloweringStage.Done, Day(2026, 1, 15)));

        Assert.Equal("In flower May 2025 to June 2025", FloweringText.Describe(month));
        Assert.Equal("In flower 20 Dec to 15 Jan 2026", FloweringText.Describe(across));
    }

    [Fact]
    public void The_menu_hint_follows_the_open_flowering()
    {
        var buds = FloweringNow.Of(Flowerings.Of([Mark(FloweringStage.Buds, Day(2026, 3, 3))], PlantId), new DateOnly(2026, 3, 10));
        var flowers = FloweringNow.Of(Flowerings.Of([Mark(FloweringStage.InFlower, Day(2026, 4, 10))], PlantId), new DateOnly(2026, 4, 12));

        Assert.Equal("Spike, buds or flowers", FloweringText.MenuHint(null, 2026));
        Assert.Equal("In bud since 3 Mar", FloweringText.MenuHint(buds, 2026));
        Assert.Equal("In flower since 10 Apr", FloweringText.MenuHint(flowers, 2026));
    }

    [Fact]
    public void Stage_months_and_years_are_worded_plainly()
    {
        Assert.Equal("Spike or buds", FloweringText.StageLabel(FloweringStage.Buds));
        Assert.Equal("Marked done flowering.", FloweringText.Marked(FloweringStage.Done));
        Assert.Equal("November", FloweringText.MonthName(11));
        Assert.Equal("in 2025", FloweringText.InYears([2025]));
        Assert.Equal("in 2024 and 2025", FloweringText.InYears([2025, 2024]));
        Assert.Equal("in 2023, 2024 and 2025", FloweringText.InYears([2023, 2024, 2025]));
    }
}
