using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

public class ShareCardTextTests
{
    private static readonly DateOnly Started = new(2026, 8, 1);
    private static readonly DateOnly Today = new(2026, 10, 6);

    private static Propagation Cutting(GrowingMedium medium = GrowingMedium.Leca, int count = 1) =>
        new() { Nickname = "Mona", Genus = "monstera", Species = "Deliciosa", Type = PropagationType.Cutting, Medium = medium, StartedOn = Started, InitialCount = count };

    private static ShareCardText Of(Propagation propagation, Plant? parent = null) =>
        ShareCardText.ForPropagation(propagation, parent, Today);

    [Fact]
    public void A_cutting_in_orchid_bark_says_so()
    {
        Assert.Contains("in orchid bark", Of(Cutting(GrowingMedium.OrchidBark)).Line);
    }

    [Fact]
    public void A_rooted_cutting_says_where_it_came_from_how_long_it_took_and_what_it_rooted_in()
    {
        var propagation = Cutting();
        propagation.FirstRootOn = Started.AddDays(18);
        propagation.RecordPottedUp(1, Started.AddDays(40));

        var card = Of(propagation, new Plant { Nickname = "Monstera" });

        Assert.Equal("Cutting from my Monstera, rooted in 18 days in LECA.", card.Line);
        Assert.Equal(new ShareFigure(18, "days to root"), card.Figure);
    }

    [Fact]
    public void A_batch_says_how_many_were_potted_up()
    {
        var propagation = Cutting(GrowingMedium.Water, count: 4);
        propagation.FirstRootOn = Started.AddDays(10);
        propagation.RecordPottedUp(3, Started.AddDays(30));
        propagation.RecordFailed(1, Started.AddDays(30));

        Assert.Equal("Cutting, rooted in 10 days in water. 3 of 4 potted up.", Of(propagation).Line);
    }

    [Fact]
    public void One_that_never_noted_a_root_does_not_claim_it_rooted()
    {
        var propagation = Cutting(GrowingMedium.Water);
        propagation.RecordPottedUp(1, Started.AddDays(42));

        var card = Of(propagation);

        Assert.Equal("Cutting, potted up after 42 days in water.", card.Line);
        Assert.Equal(new ShareFigure(42, "days to pot up"), card.Figure);
    }

    [Fact]
    public void One_that_only_has_the_day_it_reached_rooted_uses_that()
    {
        var propagation = Cutting();
        propagation.RootedOn = Started.AddDays(20);
        propagation.SetStage(PropagationStage.Rooted);

        var card = Of(propagation);

        Assert.Equal("Cutting, rooted in 20 days in LECA.", card.Line);
        Assert.Equal(new ShareFigure(20, "days to root"), card.Figure);
    }

    [Fact]
    public void A_failed_one_says_so_plainly()
    {
        var propagation = Cutting(GrowingMedium.Soil);
        propagation.RecordFailed(1, Started.AddDays(30));

        var card = Of(propagation);

        Assert.Equal("Cutting, didn't make it after 30 days in soil.", card.Line);
        Assert.Equal(new ShareFigure(30, "days it ran"), card.Figure);
    }

    [Fact]
    public void One_still_going_with_a_first_root_says_when_it_showed()
    {
        var propagation = Cutting(GrowingMedium.Water);
        propagation.FirstRootOn = Started.AddDays(18);
        propagation.NoteFirstRoot();

        var card = Of(propagation);

        Assert.Equal("Cutting, first roots on day 18 in water.", card.Line);
        Assert.Equal(new ShareFigure(18, "days to root"), card.Figure);
    }

    [Fact]
    public void One_still_going_with_nothing_yet_says_which_day_it_is()
    {
        var card = Of(Cutting(GrowingMedium.Water));

        Assert.Equal("Cutting, day 66 in water.", card.Line);
        Assert.Equal(new ShareFigure(66, "days so far"), card.Figure);
    }

    [Fact]
    public void One_started_today_has_no_number_to_show()
    {
        var propagation = Cutting();
        propagation.StartedOn = Today;

        var card = Of(propagation);

        Assert.Equal("Cutting, started today in LECA.", card.Line);
        Assert.Null(card.Figure);
    }

    [Fact]
    public void Seeds_come_up_instead_of_rooting()
    {
        var propagation = Cutting(GrowingMedium.Soil, count: 6);
        propagation.Type = PropagationType.Seed;
        propagation.FirstGerminatedOn = Started.AddDays(9);
        propagation.RecordPottedUp(4, Started.AddDays(60));
        propagation.RecordFailed(2, Started.AddDays(60));

        var card = Of(propagation);

        Assert.Equal("Seeds, came up in 9 days in soil. 4 of 6 potted up.", card.Line);
        Assert.Equal(new ShareFigure(9, "days to come up"), card.Figure);
    }

    [Fact]
    public void A_medium_the_app_cant_name_is_left_out()
    {
        var propagation = Cutting(GrowingMedium.Other);
        propagation.FirstRootOn = Started.AddDays(5);

        Assert.Equal("Cutting, first roots on day 5.", Of(propagation).Line);
    }

    [Fact]
    public void The_botanical_name_goes_under_a_nickname_but_not_under_itself()
    {
        var named = Cutting();
        named.Cultivar = "Thai Constellation";
        var unnamed = Cutting();
        unnamed.Nickname = null;

        var withNickname = Of(named);
        var withoutNickname = Of(unnamed);

        Assert.Equal("Mona", withNickname.Name);
        Assert.Equal("Monstera deliciosa", withNickname.Latin);
        Assert.Equal("Thai Constellation", withNickname.Cultivar);
        Assert.Equal("Monstera deliciosa", withoutNickname.Name);
        Assert.Null(withoutNickname.Latin);
        Assert.Null(withoutNickname.Cultivar);
    }

    private static Plant Mona(PlantOrigin origin = PlantOrigin.Propagated) =>
        new() { Nickname = "Mona", Origin = origin, AcquiredOn = LooseDate.Of(2024, 6) };

    private static ShareCardText Of(Plant plant, Plant? parent = null, Propagation? from = null, params Propagation[] taken) =>
        ShareCardText.ForPlant(plant, parent, from, taken, Today);

    [Fact]
    public void A_plant_from_a_propagation_tells_how_it_started()
    {
        var from = Cutting();
        from.FirstRootOn = Started.AddDays(18);

        var card = Of(Mona(), new Plant { Nickname = "Monstera" }, from);

        Assert.Equal("Propagated from my Monstera, rooted in 18 days in LECA. With me since June 2024.", card.Line);
        Assert.Equal(new ShareFigure(18, "days to root"), card.Figure);
    }

    [Fact]
    public void A_plant_that_was_bought_only_says_how_long_it_has_been_here()
    {
        var card = Of(Mona(PlantOrigin.Purchased));

        Assert.Equal("With me since June 2024.", card.Line);
        Assert.Null(card.Figure);
    }

    [Fact]
    public void A_plant_with_nothing_to_say_has_no_line()
    {
        var plant = Mona(PlantOrigin.Unknown);
        plant.AcquiredOn = null;

        Assert.Null(Of(plant).Line);
    }

    [Theory]
    [InlineData(PlantOrigin.GrownFromSeed, "Grown from seed.")]
    [InlineData(PlantOrigin.Gift, "A gift.")]
    [InlineData(PlantOrigin.Swap, "From a swap.")]
    public void The_origin_is_told_in_a_few_words(PlantOrigin origin, string line)
    {
        var plant = Mona(origin);
        plant.AcquiredOn = null;

        Assert.Equal(line, Of(plant).Line);
    }

    [Fact]
    public void A_plant_that_left_is_not_said_to_be_with_me()
    {
        var plant = Mona(PlantOrigin.Purchased);
        plant.Status = PlantStatus.Sold;

        Assert.Null(Of(plant).Line);
    }

    [Fact]
    public void The_number_on_a_plant_is_how_many_propagations_were_taken_from_it()
    {
        var card = Of(Mona(), taken: [Cutting(), Cutting(), Cutting()]);

        Assert.Equal(new ShareFigure(3, "propagations"), card.Figure);
        Assert.Equal(new ShareFigure(1, "propagation"), Of(Mona(), taken: [Cutting()]).Figure);
    }

    [Fact]
    public void A_plant_gotten_on_a_known_day_counts_the_time_it_has_been_here()
    {
        var plant = Mona(PlantOrigin.Purchased);

        plant.AcquiredOn = LooseDate.Of(Today.AddDays(-12));
        Assert.Equal(new ShareFigure(12, "days with me"), Of(plant).Figure);

        plant.AcquiredOn = LooseDate.Of(new DateOnly(2026, 3, 20));
        Assert.Equal(new ShareFigure(6, "months with me"), Of(plant).Figure);

        plant.AcquiredOn = LooseDate.Of(new DateOnly(2023, 10, 7));
        Assert.Equal(new ShareFigure(2, "years with me"), Of(plant).Figure);

        plant.AcquiredOn = LooseDate.Of(new DateOnly(2025, 10, 6));
        Assert.Equal(new ShareFigure(12, "months with me"), Of(plant).Figure);
    }

    [Fact]
    public void A_month_or_a_year_is_not_counted_from()
    {
        var plant = Mona(PlantOrigin.Purchased);

        plant.AcquiredOn = LooseDate.Of(2024, 6);
        Assert.Null(Of(plant).Figure);

        plant.AcquiredOn = LooseDate.Of(2024);
        Assert.Null(Of(plant).Figure);
    }

    [Fact]
    public void A_plant_gotten_in_the_future_has_no_number()
    {
        var plant = Mona(PlantOrigin.Purchased);
        plant.AcquiredOn = LooseDate.Of(Today.AddDays(3));

        Assert.Null(Of(plant).Figure);
    }

    [Fact]
    public void Photos_on_the_same_day_have_no_apart_line()
    {
        Assert.Null(ShareCardText.ApartLine(Started, Started));
    }

    [Fact]
    public void Photos_in_the_wrong_order_have_no_apart_line_since_the_caller_puts_the_older_first()
    {
        Assert.Null(ShareCardText.ApartLine(Started.AddDays(5), Started));
    }

    [Theory]
    [InlineData(1, "1 day apart.")]
    [InlineData(46, "46 days apart.")]
    [InlineData(59, "59 days apart.")]
    public void Under_sixty_days_counts_days(int days, string expected)
    {
        Assert.Equal(expected, ShareCardText.ApartLine(Started, Started.AddDays(days)));
    }

    [Fact]
    public void Sixty_days_is_counted_in_months()
    {
        Assert.Equal("1 month apart.", ShareCardText.ApartLine(new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 30)));
        Assert.Equal("8 months apart.", ShareCardText.ApartLine(new DateOnly(2025, 1, 15), new DateOnly(2025, 9, 20)));
    }

    [Fact]
    public void Months_hold_up_to_twenty_four_and_then_years_count()
    {
        Assert.Equal("23 months apart.", ShareCardText.ApartLine(new DateOnly(2024, 1, 10), new DateOnly(2025, 12, 10)));
        Assert.Equal("2 years apart.", ShareCardText.ApartLine(new DateOnly(2024, 1, 10), new DateOnly(2026, 1, 10)));
        Assert.Equal("2 years apart.", ShareCardText.ApartLine(new DateOnly(2024, 1, 10), new DateOnly(2026, 12, 9)));
    }

    [Fact]
    public void A_single_year_is_never_said_because_it_is_twelve_months()
    {
        Assert.Equal("12 months apart.", ShareCardText.ApartLine(new DateOnly(2025, 1, 10), new DateOnly(2026, 1, 10)));
    }
}
