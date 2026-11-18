using System.Globalization;
using Stikling.Core.Text;

namespace Stikling.Core.Tests;

public class FileSizesTests
{
    private static string In(string culture, long bytes)
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            return FileSizes.Of(bytes);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Theory]
    [InlineData(0, "0 kB")]
    [InlineData(1, "1 kB")]
    [InlineData(552_960, "540 kB")]
    [InlineData(1_048_575, "1.0 MB")]
    [InlineData(1_048_576, "1.0 MB")]
    [InlineData(1_258_291, "1.2 MB")]
    [InlineData(10_485_760, "10.0 MB")]
    public void Sizes_are_kilobytes_under_a_megabyte_and_megabytes_with_a_decimal_above(long bytes, string expected) =>
        Assert.Equal(expected, In("en-US", bytes));

    [Fact]
    public void The_decimal_separator_follows_the_language() =>
        Assert.Equal("1,2 MB", In("da-DK", 1_258_291));

    [Fact]
    public void A_negative_size_reads_as_nothing() =>
        Assert.Equal("0 kB", In("en-US", -5));
}
