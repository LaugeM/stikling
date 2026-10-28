using Stikling.Core.Examples;
using Stikling.Core.Tour;

namespace Stikling.Core.Tests;

public class GuidedTourTests
{
    [Fact]
    public void It_is_off_until_started_and_starts_on_the_first_step()
    {
        var tour = new GuidedTour();
        Assert.False(tour.Active);
        Assert.False(tour.Next());

        tour.Start();

        Assert.True(tour.Active);
        Assert.Equal(1, tour.Number);
        Assert.Equal("Today", tour.Current.Title);
    }

    [Fact]
    public void Next_moves_on_and_after_the_fifth_stop_comes_the_closing_card()
    {
        var tour = new GuidedTour();
        tour.Start();

        for (var i = 0; i < 4; i++)
            Assert.True(tour.Next());
        Assert.Equal(5, tour.Number);
        Assert.False(tour.IsClosing);

        Assert.True(tour.Next());
        Assert.True(tour.IsClosing);
        Assert.Equal(0, tour.Number);
        Assert.Null(tour.Current.Target);
        Assert.Equal("", tour.Current.Route);
    }

    [Fact]
    public void Next_on_the_closing_card_ends_the_tour()
    {
        var tour = new GuidedTour();
        tour.Start();
        for (var i = 0; i < 5; i++)
            tour.Next();

        tour.Next();

        Assert.False(tour.Active);
    }

    [Fact]
    public void Back_stops_at_the_first_step_and_goes_back_from_the_closing_card()
    {
        var tour = new GuidedTour();
        tour.Start();
        Assert.False(tour.CanGoBack);
        Assert.False(tour.Back());
        Assert.Equal(1, tour.Number);

        for (var i = 0; i < 5; i++)
            tour.Next();
        Assert.True(tour.Back());

        Assert.Equal(5, tour.Number);
        Assert.Equal("Your data", tour.Current.Title);
    }

    [Fact]
    public void Skip_ends_the_tour_from_any_step_and_a_new_start_begins_at_the_first()
    {
        var tour = new GuidedTour();
        tour.Start();
        tour.Next();
        tour.Next();

        Assert.True(tour.Skip());
        Assert.False(tour.Active);
        Assert.False(tour.Skip());

        tour.Start();
        Assert.Equal(1, tour.Number);
    }

    [Fact]
    public void The_stops_visit_the_example_by_its_ids()
    {
        Assert.Equal(5, GuidedTour.Steps.Count);
        Assert.Equal("", GuidedTour.Steps[0].Route);
        Assert.Equal($"plants/{ExampleFamily.MotherId}", GuidedTour.Steps[1].Route);
        Assert.Equal($"propagations/{ExampleFamily.CurrentCuttingId}", GuidedTour.Steps[2].Route);
        Assert.Equal("water", GuidedTour.Steps[3].Route);
        Assert.Equal("backup", GuidedTour.Steps[4].Route);
        Assert.All(GuidedTour.Steps, step => Assert.False(string.IsNullOrWhiteSpace(step.Target)));
        Assert.Equal(5, GuidedTour.Steps.Select(s => s.Target).Distinct().Count());
    }
}
