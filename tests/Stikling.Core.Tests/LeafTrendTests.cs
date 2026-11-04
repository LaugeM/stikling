using Stikling.Core.Leaves;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class LeafTrendTests
{
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static int made;

    private static Leaf L(Variegation? rating, int day, bool deleted = false) =>
        new()
        {
            Rating = rating, On = new DateOnly(2026, 10, 1).AddDays(day),
            CreatedAt = Created.AddSeconds(made++), DeletedAt = deleted ? Created : null
        };

    [Fact]
    public void No_leaves_is_no_trend() => Assert.Equal(LeafTrend.None, LeafTrends.Of([]));

    [Fact]
    public void A_reverted_newest_leaf_is_flagged_even_alone()
    {
        Assert.Equal(LeafTrend.NewestReverted, LeafTrends.Of([L(Variegation.Reverted, 0)]));
        Assert.Equal(LeafTrend.NewestReverted, LeafTrends.Of([L(Variegation.High, 0), L(Variegation.Reverted, 1)]));
    }

    [Fact]
    public void An_older_reverted_leaf_is_not_flagged()
    {
        Assert.Equal(LeafTrend.None, LeafTrends.Of([L(Variegation.Reverted, 0), L(Variegation.High, 1)]));
    }

    [Fact]
    public void Two_drops_in_a_row_is_less_twice()
    {
        Assert.Equal(LeafTrend.LessTwice, LeafTrends.Of([L(Variegation.High, 0), L(Variegation.Medium, 1), L(Variegation.Low, 2)]));
    }

    [Fact]
    public void Only_the_last_three_leaves_count()
    {
        Assert.Equal(LeafTrend.LessTwice,
            LeafTrends.Of([L(Variegation.Low, 0), L(Variegation.High, 1), L(Variegation.Medium, 2), L(Variegation.Low, 3)]));
    }

    [Fact]
    public void One_drop_or_two_leaves_is_no_trend()
    {
        Assert.Equal(LeafTrend.None, LeafTrends.Of([L(Variegation.High, 0), L(Variegation.Low, 1)]));
        Assert.Equal(LeafTrend.None, LeafTrends.Of([L(Variegation.High, 0), L(Variegation.High, 1), L(Variegation.Low, 2)]));
    }

    [Fact]
    public void A_step_down_from_all_white_does_not_count()
    {
        Assert.Equal(LeafTrend.None, LeafTrends.Of([L(Variegation.AllWhite, 0), L(Variegation.High, 1), L(Variegation.Medium, 2)]));
        Assert.Equal(LeafTrend.None, LeafTrends.Of([L(Variegation.AllWhite, 0), L(Variegation.High, 1)]));
    }

    [Fact]
    public void Deleted_and_unrated_leaves_are_ignored()
    {
        Assert.Equal(LeafTrend.None,
            LeafTrends.Of([L(Variegation.High, 0), L(Variegation.Reverted, 1, deleted: true), L(null, 2)]));
        Assert.Equal(LeafTrend.LessTwice,
            LeafTrends.Of([L(Variegation.High, 0), L(null, 1), L(Variegation.Medium, 2), L(Variegation.Low, 3)]));
    }

    [Fact]
    public void Leaves_are_ordered_by_day_then_by_when_they_were_logged()
    {
        // Given newest first
        Assert.Equal(LeafTrend.LessTwice,
            LeafTrends.Of([L(Variegation.Low, 2), L(Variegation.Medium, 1), L(Variegation.High, 0)]));
        // Same day: the one logged last is the newest
        var first = L(Variegation.Low, 5);
        var second = L(Variegation.Reverted, 5);
        Assert.Equal(LeafTrend.NewestReverted, LeafTrends.Of([second, first]));
    }
}
