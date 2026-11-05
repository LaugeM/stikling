using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class AnniversariesTests
{
    private static Plant Acquired(LooseDate? on, string name = "Monstera", bool inCollection = true) => new()
    {
        Nickname = name,
        AcquiredOn = on,
        Status = inCollection ? PlantStatus.Active : PlantStatus.Died
    };

    private static IReadOnlyList<Anniversary> On(DateOnly day, params Plant[] plants) =>
        Anniversaries.For(plants, day, PutOffs.Empty());

    [Fact]
    public void A_day_shows_from_the_day_through_six_days_after()
    {
        var plant = Acquired(LooseDate.Of(new DateOnly(2024, 6, 12)));

        Assert.Empty(On(new DateOnly(2026, 6, 11), plant));
        var first = Assert.Single(On(new DateOnly(2026, 6, 12), plant));
        Assert.Equal(2, first.Years);
        Assert.Equal(new DateOnly(2026, 6, 18), first.ShowUntil);
        Assert.Single(On(new DateOnly(2026, 6, 18), plant));
    }

    [Fact]
    public void Day_seven_is_gone()
    {
        var plant = Acquired(LooseDate.Of(new DateOnly(2024, 6, 12)));

        Assert.Empty(On(new DateOnly(2026, 6, 19), plant));
    }

    [Fact]
    public void The_29th_of_February_is_the_28th_in_other_years()
    {
        var plant = Acquired(LooseDate.Of(new DateOnly(2024, 2, 29)));

        Assert.Empty(On(new DateOnly(2025, 2, 27), plant));
        Assert.Single(On(new DateOnly(2025, 2, 28), plant));
        Assert.Single(On(new DateOnly(2028, 2, 29), Acquired(LooseDate.Of(new DateOnly(2024, 2, 29)))));
    }

    [Fact]
    public void A_month_shows_from_the_first_through_the_seventh()
    {
        var plant = Acquired(LooseDate.Of(2024, 6));

        Assert.Empty(On(new DateOnly(2026, 5, 31), plant));
        Assert.Single(On(new DateOnly(2026, 6, 1), plant));
        Assert.Single(On(new DateOnly(2026, 6, 7), plant));
        Assert.Empty(On(new DateOnly(2026, 6, 8), plant));
    }

    [Fact]
    public void A_year_alone_never_shows()
    {
        var plant = Acquired(LooseDate.Of(2024));

        Assert.Empty(On(new DateOnly(2026, 1, 1), plant));
        Assert.Empty(On(new DateOnly(2026, 6, 12), plant));
    }

    [Fact]
    public void A_window_that_started_in_december_runs_into_january()
    {
        var plant = Acquired(LooseDate.Of(new DateOnly(2023, 12, 29)));

        var shown = Assert.Single(On(new DateOnly(2026, 1, 2), plant));
        Assert.Equal(2, shown.Years);
        Assert.Equal(new DateOnly(2026, 1, 4), shown.ShowUntil);
        Assert.Equal(2025, shown.WindowYear);
        Assert.Empty(On(new DateOnly(2026, 1, 5), plant));
    }

    [Fact]
    public void Every_year_counts_and_the_first_day_is_not_one()
    {
        var plant = Acquired(LooseDate.Of(new DateOnly(2020, 3, 3)));

        Assert.Equal(1, Assert.Single(On(new DateOnly(2021, 3, 3), plant)).Years);
        Assert.Equal(7, Assert.Single(On(new DateOnly(2027, 3, 4), plant)).Years);
        Assert.Empty(On(new DateOnly(2020, 3, 3), plant));
        Assert.Empty(On(new DateOnly(2019, 3, 3), plant));
    }

    [Fact]
    public void Plants_not_in_the_collection_are_skipped()
    {
        var gone = Acquired(LooseDate.Of(new DateOnly(2024, 6, 12)), inCollection: false);
        var deleted = Acquired(LooseDate.Of(new DateOnly(2024, 6, 12)));
        deleted.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Empty(On(new DateOnly(2026, 6, 12), gone, deleted));
    }

    [Fact]
    public void A_put_off_hides_it_and_it_is_back_next_year()
    {
        var plant = Acquired(LooseDate.Of(new DateOnly(2024, 6, 12)));
        var putOffs = PutOffs.Empty();
        var shown = Assert.Single(Anniversaries.For([plant], new DateOnly(2026, 6, 13), putOffs));
        putOffs.PutOff(PutOffs.Anniversary(plant.Id, shown.WindowYear), shown.ShowUntil.AddDays(1));

        Assert.Empty(Anniversaries.For([plant], new DateOnly(2026, 6, 14), putOffs));
        Assert.Empty(Anniversaries.For([plant], new DateOnly(2026, 6, 18), putOffs));
        var next = Assert.Single(Anniversaries.For([plant], new DateOnly(2027, 6, 12), putOffs));
        Assert.Equal(3, next.Years);
    }

    [Fact]
    public void Most_years_come_first_then_the_name()
    {
        var young = Acquired(LooseDate.Of(new DateOnly(2025, 6, 12)), "Ficus");
        var olderB = Acquired(LooseDate.Of(new DateOnly(2022, 6, 12)), "Pothos");
        var olderA = Acquired(LooseDate.Of(new DateOnly(2022, 6, 13)), "Alocasia");

        var shown = On(new DateOnly(2026, 6, 14), young, olderB, olderA);

        Assert.Equal(["Alocasia", "Pothos", "Ficus"], shown.Select(a => a.Plant.Nickname));
    }
}
