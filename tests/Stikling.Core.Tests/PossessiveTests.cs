using System.Globalization;
using Stikling.Core.Text;

namespace Stikling.Core.Tests;

public class PossessiveTests
{
    [Theory]
    [InlineData("en", "Alma", "Alma's")]
    [InlineData("en", "Jens", "Jens's")]
    [InlineData("da", "Alma", "Almas")]
    [InlineData("da", "Jens", "Jens'")]
    [InlineData("da", "Max", "Max'")]
    [InlineData("da", " Bo ", "Bos")]
    public void Adds_the_ending_the_language_uses(string language, string name, string expected) =>
        Assert.Equal(expected, Possessive.Of(name, CultureInfo.GetCultureInfo(language)));
}
