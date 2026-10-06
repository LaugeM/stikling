using Stikling.Core.Models;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class PropagationOutcomeTests
{
    private static readonly DateOnly Started = new(2026, 8, 1);

    private static Propagation Finished(int count = 3, int pottedUp = 2, int failed = 1, PropagationType type = PropagationType.Cutting)
    {
        var propagation = new Propagation { Nickname = "Monstera cutting", Type = type, StartedOn = Started, InitialCount = count };
        propagation.RecordPottedUp(pottedUp, Started.AddDays(30));
        if (failed > 0)
            propagation.RecordFailed(failed, Started.AddDays(30));
        return propagation;
    }

    private static Plant From(Propagation propagation, string name, PlantStatus status = PlantStatus.Active, Guid? placeId = null) =>
        new() { Nickname = name, FromPropagationId = propagation.Id, Status = status, PlaceId = placeId };

    [Fact]
    public void A_propagation_still_going_has_no_outcome()
    {
        var going = new Propagation { Nickname = "Going", StartedOn = Started, InitialCount = 2 };

        Assert.Null(PropagationOutcome.For(going, [], Places.None));
    }

    [Fact]
    public void It_reports_how_long_it_ran_and_how_the_units_went()
    {
        var propagation = Finished();

        var outcome = PropagationOutcome.For(propagation, [], Places.None)!;

        Assert.Equal(30, outcome.DaysRan);
        Assert.Equal(3, outcome.Started);
        Assert.Equal(2, outcome.PottedUp);
        Assert.Equal(1, outcome.Failed);
    }

    [Fact]
    public void Days_to_root_is_the_first_root_when_one_was_noted()
    {
        var propagation = Finished();
        propagation.FirstRootOn = Started.AddDays(12);
        propagation.RootedOn = Started.AddDays(20);

        Assert.Equal(12, PropagationOutcome.For(propagation, [], Places.None)!.DaysToRoot);
    }

    [Fact]
    public void Days_to_root_falls_back_to_the_day_it_reached_rooted()
    {
        var propagation = Finished();
        propagation.RootedOn = Started.AddDays(20);

        Assert.Equal(20, PropagationOutcome.For(propagation, [], Places.None)!.DaysToRoot);
    }

    [Fact]
    public void Days_to_root_is_empty_when_it_never_rooted()
    {
        Assert.Null(PropagationOutcome.For(Finished(), [], Places.None)!.DaysToRoot);
    }

    [Fact]
    public void Seeds_count_to_the_first_seedling()
    {
        var propagation = Finished(type: PropagationType.Seed);
        propagation.FirstGerminatedOn = Started.AddDays(9);
        propagation.FirstRootOn = Started.AddDays(2);

        Assert.Equal(9, PropagationOutcome.For(propagation, [], Places.None)!.DaysToRoot);
    }

    [Fact]
    public void A_propagation_finished_before_the_date_was_kept_uses_the_fallback()
    {
        var propagation = Finished();
        propagation.FinishedOn = null;

        Assert.Equal(40, PropagationOutcome.For(propagation, [], Places.None, Started.AddDays(40))!.DaysRan);
        Assert.Null(PropagationOutcome.For(propagation, [], Places.None)!.DaysRan);
    }

    [Fact]
    public void Plants_come_with_their_room_and_whether_they_are_still_here()
    {
        var propagation = Finished();
        var room = new Place { Name = "Kitchen" };
        var kept = From(propagation, "Mona", placeId: room.Id);
        var gone = From(propagation, "Ben", PlantStatus.GivenAway);

        var outcome = PropagationOutcome.For(propagation, [gone, kept], new Places([room]))!;

        Assert.Equal(["Mona", "Ben"], outcome.Plants.Select(p => p.Plant.DisplayName));
        Assert.Equal("Kitchen", outcome.Plants[0].Room);
        Assert.True(outcome.Plants[0].StillHere);
        Assert.False(outcome.Plants[1].StillHere);
    }

    [Fact]
    public void Deleted_plants_and_plants_from_other_propagations_are_left_out()
    {
        var propagation = Finished();
        var deleted = From(propagation, "Deleted");
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        var other = From(Finished(), "Other");

        var outcome = PropagationOutcome.For(propagation, [deleted, other], Places.None)!;

        Assert.Empty(outcome.Plants);
    }
}
