using Stikling.Core.Year;

namespace Stikling.Core.Tests;

public class YearVideoPlanTests
{
    [Fact]
    public void Cards_keep_the_order_they_were_given_and_follow_each_other()
    {
        var plan = YearVideoPlan.Create(["title", "added", "collage"]);

        Assert.Equal(["title", "added", "collage"], plan.Shots.Select(s => s.CardId));
        Assert.Equal([0, YearVideoPlan.HoldSeconds, 2 * YearVideoPlan.HoldSeconds], plan.Shots.Select(s => s.Start));
    }

    [Fact]
    public void The_last_card_stays_up_longer_and_the_video_ends_when_it_does()
    {
        var plan = YearVideoPlan.Create(["title", "added"]);

        Assert.Equal(YearVideoPlan.HoldSeconds, plan.Shots[0].Hold);
        Assert.Equal(YearVideoPlan.HoldSeconds + YearVideoPlan.LastExtraSeconds, plan.Shots[1].Hold);
        Assert.Equal(plan.Shots[1].Start + plan.Shots[1].Hold, plan.Total);
    }

    [Fact]
    public void A_card_is_on_screen_on_its_own_for_most_of_its_hold()
    {
        Assert.True(YearVideoPlan.FadeSeconds < YearVideoPlan.HoldSeconds / 2);
    }

    [Fact]
    public void One_card_has_nothing_to_fade_to()
    {
        var plan = YearVideoPlan.Create(["title"]);

        Assert.Equal(0, plan.Crossfade);
        Assert.Equal(YearVideoPlan.HoldSeconds + YearVideoPlan.LastExtraSeconds, plan.Total);
    }

    [Fact]
    public void No_cards_make_an_empty_plan()
    {
        var plan = YearVideoPlan.Create([]);

        Assert.Empty(plan.Shots);
        Assert.Equal(0, plan.Total);
    }
}
