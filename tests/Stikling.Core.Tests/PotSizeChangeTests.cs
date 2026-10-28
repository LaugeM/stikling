using Stikling.Core.Models;
using Stikling.Core.Pots;

namespace Stikling.Core.Tests;

public class PotSizeChangeTests
{
    private static Pot P(decimal? top) => new() { Name = "Pot", TopCm = top };

    [Fact]
    public void Warns_when_wider_by_more_than_the_limit()
    {
        var c = PotSizeChange.Between(P(8), P(14), 5);
        Assert.NotNull(c);
        Assert.True(c.Warn);
        Assert.StartsWith("From 8 cm to 14 cm across the top. That's a lot bigger", c.Text);
    }

    [Fact]
    public void Does_not_warn_at_exactly_the_limit()
    {
        var c = PotSizeChange.Between(P(8), P(13), 5)!;
        Assert.False(c.Warn);
        Assert.Equal("From 8 cm to 13 cm across the top.", c.Text);
    }

    [Fact]
    public void Smaller_or_same_does_not_warn()
    {
        Assert.Equal("From 12 cm to 10 cm across the top.", PotSizeChange.Between(P(12), P(10))!.Text);
        Assert.Equal("From 12 cm to 12 cm across the top.", PotSizeChange.Between(P(12), P(12))!.Text);
    }

    [Fact]
    public void Shows_decimals_only_when_not_whole()
    {
        Assert.Equal("From 8.5 cm to 10 cm across the top.", PotSizeChange.Between(P(8.5m), P(10))!.Text);
    }

    [Fact]
    public void Shows_nothing_when_a_pot_or_width_is_missing()
    {
        Assert.Null(PotSizeChange.Between(null, P(10)));
        Assert.Null(PotSizeChange.Between(P(10), null));
        Assert.Null(PotSizeChange.Between(P(null), P(10)));
        Assert.Null(PotSizeChange.Between(P(10), P(null)));
    }

    [Fact]
    public void A_null_setting_means_five()
    {
        var settings = new UserSettings();
        Assert.Equal(5, PotSizeChange.WarnLimit(settings));
        Assert.False(PotSizeChange.Between(P(8), P(13), PotSizeChange.WarnLimit(settings))!.Warn);
        Assert.True(PotSizeChange.Between(P(8), P(14), PotSizeChange.WarnLimit(settings))!.Warn);
        settings.PotUpWarnCm = 2;
        Assert.Equal(2, PotSizeChange.WarnLimit(settings));
    }
}
