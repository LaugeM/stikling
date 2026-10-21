using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Timeline;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class SemiHydroTransitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 21);

    private readonly FakePlantRepository plants = new();
    private readonly FakeTimelineRepository timeline = new();

    private static string Label(Enum value) => value switch
    {
        GrowingMedium.Leca => "LECA",
        GrowingMedium.Pon => "PON",
        _ => value.ToString()
    };

    private PlantService Service() => new(plants, timeline, new FixedTime(Now));

    private static Plant Moving(int daysAgo, Action<Plant>? change = null)
    {
        var plant = new Plant { Nickname = "Monstera", Medium = GrowingMedium.Leca, TransitionSince = Today.AddDays(-daysAgo), TransitionTo = GrowingMedium.Leca };
        change?.Invoke(plant);
        return plant;
    }

    [Fact]
    public async Task Starting_ticking_steps_and_settling_is_told_on_the_history()
    {
        var service = Service();
        var plant = new Plant { Nickname = "Monstera", Medium = GrowingMedium.Leca };
        var since = Today.AddDays(-35);

        await service.StartTransitionAsync(plant, since, GrowingMedium.Leca, 6, Label);
        Assert.True(plant.InTransition);
        Assert.Null(plant.TransitionCheckWeeks);

        await service.SetTransitionStepAsync(plant, TransitionStep.RootsWashed, since, Label);
        await service.SetTransitionStepAsync(plant, TransitionStep.FirstWaterRoot, since.AddDays(14), Label);
        await service.SetTransitionStepAsync(plant, TransitionStep.FirstWaterRoot, since.AddDays(14), Label); // no change
        Assert.True(plant.InTransition);
        await service.SetTransitionStepAsync(plant, TransitionStep.FirstNewLeaf, Today, Label);

        Assert.False(plant.InTransition);
        Assert.True(plant.TransitionSettled);
        Assert.Equal(Today, plant.TransitionSettledOn);
        Assert.Equal(
            [
                "Started the move to LECA",
                "Roots washed",
                "First new water root",
                "First new leaf\nSettled in to LECA after 5 weeks"
            ],
            timeline.Entries.Select(e => e.Text));
    }

    [Fact]
    public void Settling_after_less_than_two_weeks_says_one_week()
    {
        var before = Moving(10, p => p.FirstWaterRootOn = Today.AddDays(-3));
        var after = before.Copy();
        after.FirstNewLeafOn = Today;

        Assert.Equal(["First new leaf", "Settled in to LECA after 1 week"], PlantChanges.Describe(before, after, Label));
    }

    [Fact]
    public void Clearing_a_step_adds_nothing()
    {
        var before = Moving(10, p => p.RootsWashedOn = Today.AddDays(-5));
        var after = before.Copy();
        after.RootsWashedOn = null;

        Assert.Empty(PlantChanges.Describe(before, after, Label));
    }

    [Fact]
    public async Task Stopping_clears_the_fields_and_is_told()
    {
        var service = Service();
        var plant = Moving(20, p =>
        {
            p.TransitionCheckWeeks = 8;
            p.RootsWashedOn = Today.AddDays(-19);
        });

        await service.StopTransitionAsync(plant, Label);

        Assert.Null(plant.TransitionSince);
        Assert.Null(plant.TransitionTo);
        Assert.Null(plant.TransitionCheckWeeks);
        Assert.Null(plant.RootsWashedOn);
        Assert.Equal("Stopped tracking the move to LECA", Assert.Single(timeline.Entries).Text);
    }

    [Fact]
    public async Task A_settled_move_cannot_be_stopped()
    {
        var plant = Moving(40, p => { p.FirstWaterRootOn = Today.AddDays(-10); p.FirstNewLeafOn = Today.AddDays(-5); });

        await Service().StopTransitionAsync(plant, Label);

        Assert.True(plant.TransitionSettled);
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task A_second_move_starts_clean()
    {
        var plant = Moving(40, p => { p.FirstWaterRootOn = Today.AddDays(-10); p.FirstNewLeafOn = Today.AddDays(-5); });

        await Service().StartTransitionAsync(plant, Today, GrowingMedium.Pon, 10, Label);

        Assert.True(plant.InTransition);
        Assert.Null(plant.FirstWaterRootOn);
        Assert.Null(plant.FirstNewLeafOn);
        Assert.Equal(10, plant.TransitionCheckWeeks);
        Assert.Equal("Started the move to PON", Assert.Single(timeline.Entries).Text);
    }

    [Fact]
    public void A_duplicate_does_not_copy_the_move()
    {
        var copy = Moving(10, p => { p.RootsWashedOn = Today; p.TransitionCheckWeeks = 8; }).Duplicate();

        Assert.Null(copy.TransitionSince);
        Assert.Null(copy.TransitionTo);
        Assert.Null(copy.RootsWashedOn);
        Assert.Null(copy.TransitionCheckWeeks);
        Assert.False(copy.InTransition);
    }

    [Theory]
    [InlineData(GrowingMedium.Soil, GrowingMedium.Leca, true)]
    [InlineData(GrowingMedium.Soil, GrowingMedium.Pon, true)]
    [InlineData(GrowingMedium.Water, GrowingMedium.Pon, true)]
    [InlineData(GrowingMedium.Water, GrowingMedium.Leca, true)]
    [InlineData(GrowingMedium.Leca, GrowingMedium.Pon, false)]
    [InlineData(GrowingMedium.Soil, GrowingMedium.Perlite, false)]
    [InlineData(GrowingMedium.Pon, GrowingMedium.Soil, false)]
    public void Only_soil_or_water_into_leca_or_pon_offers_the_tracker(GrowingMedium before, GrowingMedium after, bool offered) =>
        Assert.Equal(offered, PlantService.OffersTransition(before, after));

    [Fact]
    public void The_dates_and_weeks_are_checked()
    {
        List<string> Errors(Action<Plant> change)
        {
            var plant = Moving(10);
            change(plant);
            plant.Nickname = "A";
            return [.. plant.Validate(Today)];
        }

        Assert.Empty(Errors(_ => { }));
        Assert.Contains("The move to semi-hydro can't start in the future.", Errors(p => p.TransitionSince = Today.AddDays(1)));
        Assert.Contains("A step can't be in the future.", Errors(p => p.FirstNewLeafOn = Today.AddDays(1)));
        Assert.Contains("A step can't come before the move started.", Errors(p => p.RootsWashedOn = Today.AddDays(-11)));
        Assert.Contains("Check after 1 to 52 weeks.", Errors(p => p.TransitionCheckWeeks = 0));
        Assert.Contains("Check after 1 to 52 weeks.", Errors(p => p.TransitionCheckWeeks = 53));
        Assert.Empty(Errors(p => p.TransitionCheckWeeks = 52));
    }

    [Fact]
    public void Today_asks_exactly_when_the_weeks_have_passed()
    {
        Assert.Empty(TodayBoard.TransitionQuiet([Moving(41)], Today));
        Assert.Single(TodayBoard.TransitionQuiet([Moving(42)], Today));
        Assert.Single(TodayBoard.TransitionQuiet([Moving(60)], Today));
    }

    [Fact]
    public void A_new_step_starts_the_wait_again()
    {
        var plant = Moving(60, p => p.RootsWashedOn = Today.AddDays(-10));

        Assert.Empty(TodayBoard.TransitionQuiet([plant], Today));
        Assert.Equal(Today.AddDays(-10), plant.TransitionLastNews);
        Assert.NotEqual(PutOffs.Transition(plant.Id, Today.AddDays(-60)), PutOffs.Transition(plant.Id, Today.AddDays(-10)));
    }

    [Fact]
    public void The_weeks_chosen_are_used()
    {
        var plant = Moving(30, p => p.TransitionCheckWeeks = 4);

        Assert.Single(TodayBoard.TransitionQuiet([plant], Today));
        Assert.Empty(TodayBoard.TransitionQuiet([Moving(30, p => p.TransitionCheckWeeks = 5)], Today));
    }

    [Fact]
    public void Settled_left_deleted_and_potted_in_plants_never_show()
    {
        var settled = Moving(90, p => { p.FirstWaterRootOn = Today.AddDays(-80); p.FirstNewLeafOn = Today.AddDays(-79); });
        var left = Moving(90, p => p.Status = PlantStatus.GivenAway);
        var deleted = Moving(90, p => p.DeletedAt = Now);
        var potted = Moving(90, p => p.PottedWithPlantId = Guid.NewGuid());
        var untracked = new Plant { Nickname = "Pothos" };

        Assert.Empty(TodayBoard.TransitionQuiet([settled, left, deleted, potted, untracked], Today));
    }

    [Fact]
    public void The_quietest_plant_comes_first()
    {
        var quieter = Moving(80, p => p.Nickname = "B");
        var recent = Moving(50, p => p.Nickname = "A");

        Assert.Equal([quieter, recent], TodayBoard.TransitionQuiet([recent, quieter], Today));
    }

    [Fact]
    public void The_card_stays_for_a_week_after_settling()
    {
        Plant Settled(int daysAgo) => Moving(60, p => { p.FirstWaterRootOn = Today.AddDays(-daysAgo - 3); p.FirstNewLeafOn = Today.AddDays(-daysAgo); });

        Assert.True(Moving(10).ShowsTransitionCard(Today));
        Assert.True(Settled(0).ShowsTransitionCard(Today));
        Assert.True(Settled(7).ShowsTransitionCard(Today));
        Assert.False(Settled(8).ShowsTransitionCard(Today));
        Assert.False(new Plant().ShowsTransitionCard(Today));
        Assert.False(Moving(10, p => p.Status = PlantStatus.Died).ShowsTransitionCard(Today));

        // Unticking a step makes it a move in progress again
        var again = Settled(2);
        again.FirstNewLeafOn = null;
        Assert.True(again.InTransition);
        Assert.True(again.ShowsTransitionCard(Today));
    }

    [Fact]
    public async Task A_plant_that_leaves_keeps_its_move()
    {
        var plant = Moving(10);

        await Service().SetStatusAsync([plant], PlantStatus.Died, Label);

        Assert.NotNull(plant.TransitionSince);
    }

    [Fact]
    public void The_fields_are_stored_as_text_and_older_plants_read_as_not_moving()
    {
        var plant = Moving(10, p => p.RootsWashedOn = Today.AddDays(-9));

        var json = JsonSerializer.Serialize(plant);
        Assert.Contains("\"TransitionTo\":\"Leca\"", json);
        var back = JsonSerializer.Deserialize<Plant>(json)!;
        Assert.Equal(plant.TransitionSince, back.TransitionSince);
        Assert.Equal(plant.RootsWashedOn, back.RootsWashedOn);
        Assert.True(back.InTransition);

        var older = JsonSerializer.Deserialize<Plant>("{\"Nickname\":\"Old\"}")!;
        Assert.False(older.InTransition);
        Assert.False(older.TransitionSettled);
        Assert.Null(older.TransitionTo);
        Assert.Null(older.TransitionLastNews);
        Assert.Null(older.TransitionSettledOn);
        Assert.Empty(older.Validate(Today));
    }
}
