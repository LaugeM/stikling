using Stikling.Core.Models;
using Stikling.Core.Propagations;

namespace Stikling.Core.Tests;

public class RootingPaceTests
{
    private static readonly DateOnly Today = new(2026, 9, 19);

    private static Propagation Rooted(int days, GrowingMedium medium = GrowingMedium.Leca, PropagationType type = PropagationType.Corm)
    {
        var started = Today.AddDays(-100);
        return new Propagation
        {
            Nickname = "Earlier batch",
            Type = type,
            Medium = medium,
            StartedOn = started,
            RootedOn = started.AddDays(days),
            Stage = PropagationStage.Rooted
        };
    }

    private static Propagation Waiting(int daysSoFar, PropagationStage stage = PropagationStage.Rooting) => new()
    {
        Nickname = "This batch",
        Type = PropagationType.Corm,
        Medium = GrowingMedium.Leca,
        StartedOn = Today.AddDays(-daysSoFar),
        Stage = stage
    };

    [Fact]
    public void On_time_up_to_the_average_of_the_same_type_and_medium()
    {
        var corms = Waiting(20);
        List<Propagation> all = [corms, Rooted(18), Rooted(20), Rooted(22)];

        var pace = RootingPace.For(corms, all, Today)!;

        Assert.Equal(20, pace.UsualDays);
        Assert.Equal(3, pace.Compared);
        Assert.False(pace.IsSlow);
    }

    [Fact]
    public void Slow_once_past_the_average()
    {
        var corms = Waiting(21, PropagationStage.Started);
        List<Propagation> all = [corms, Rooted(18), Rooted(20), Rooted(22)];

        Assert.True(RootingPace.For(corms, all, Today)!.IsSlow);
    }

    [Fact]
    public void Only_batches_of_the_same_type_and_medium_are_compared()
    {
        var corms = Waiting(10);
        List<Propagation> all =
        [
            corms, Rooted(18), Rooted(20),
            Rooted(5, medium: GrowingMedium.Water),
            Rooted(5, type: PropagationType.Cutting)
        ];

        Assert.Null(RootingPace.For(corms, all, Today));
    }

    [Fact]
    public void Needs_three_rooted_batches_to_compare_with()
    {
        var corms = Waiting(10);
        var deleted = Rooted(30);
        deleted.DeletedAt = DateTimeOffset.UnixEpoch;
        List<Propagation> all = [corms, Rooted(18), Rooted(20), deleted, Waiting(40)];

        Assert.Null(RootingPace.For(corms, all, Today));
    }

    [Fact]
    public void Nothing_to_say_once_rooted_or_while_dormant()
    {
        List<Propagation> earlier = [Rooted(18), Rooted(20), Rooted(22)];
        var rooted = Rooted(30);
        var dormant = Waiting(40);
        dormant.DormantSince = Today;

        Assert.Null(RootingPace.For(rooted, earlier, Today));
        Assert.Null(RootingPace.For(dormant, earlier, Today));
    }

    [Fact]
    public void The_average_is_rounded_to_whole_days()
    {
        var corms = Waiting(21);
        List<Propagation> all = [Rooted(20), Rooted(20), Rooted(21)];

        // 20.33 days rounds to 20, so day 21 is already slow
        var pace = RootingPace.For(corms, all, Today)!;
        Assert.Equal(20, pace.UsualDays);
        Assert.True(pace.IsSlow);
    }
}
