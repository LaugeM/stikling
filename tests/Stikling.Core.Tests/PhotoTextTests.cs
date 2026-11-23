using Stikling.Core.Models;
using Stikling.Core.Photos;

namespace Stikling.Core.Tests;

public class PhotoTextTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   \n\t ", null)]
    [InlineData("  new leaf  ", "new leaf")]
    [InlineData("new   leaf\non the\r\n\n left", "new leaf on the left")]
    public void A_description_is_trimmed_and_whitespace_made_one_space(string? text, string? expected) =>
        Assert.Equal(expected, PhotoText.TidyDescription(text));

    [Fact]
    public void A_description_is_cut_at_the_longest_allowed()
    {
        var tidy = PhotoText.TidyDescription(new string('a', Photo.DescriptionMaxLength + 50));

        Assert.Equal(Photo.DescriptionMaxLength, tidy?.Length);
    }

    [Fact]
    public void A_description_of_exactly_the_longest_allowed_is_kept()
    {
        var text = new string('b', Photo.DescriptionMaxLength);

        Assert.Equal(text, PhotoText.TidyDescription(text));
    }
}
