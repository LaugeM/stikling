using System.Globalization;
using Stikling.Core.Text;
using Xunit;

namespace Stikling.Core.Tests;

public class FormatTests
{
    [Fact]
    public void F_follows_the_current_culture()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("da-DK");
            Assert.Equal("1,5", Format.F("{0}", 1.5));
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            Assert.Equal("1.5", Format.F("{0}", 1.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Theory]
    [InlineData(1, "1 plant")]
    [InlineData(0, "0 plants")]
    [InlineData(2, "2 plants")]
    public void F_with_a_count_picks_one_or_other(int count, string expected) =>
        Assert.Equal(expected, Format.F(count, "{0} plant", "{0} plants", count));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  fern ", "fern")]
    [InlineData("fern", "fern")]
    public void Clean_trims_and_gives_null_for_nothing(string? text, string? expected) =>
        Assert.Equal(expected, Format.Clean(text));
}
