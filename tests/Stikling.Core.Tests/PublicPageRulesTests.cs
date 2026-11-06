using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

public class PublicPageRulesTests
{
    [Theory]
    [InlineData("  @Fern-Lover ", "fern-lover")]
    [InlineData("ABC", "abc")]
    [InlineData("@", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void A_name_is_trimmed_lowered_and_loses_its_at_sign(string? typed, string? expected) =>
        Assert.Equal(expected, PublicPageRules.NormalizeName(typed));

    [Theory]
    [InlineData("fern", null)]
    [InlineData("a1b", null)]
    [InlineData("my-plants-2", null)]
    [InlineData("ab", NameProblem.TooShort)]
    [InlineData("", NameProblem.TooShort)]
    [InlineData("abcdefghijklmnopqrstuvwxyz12345", NameProblem.TooLong)]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234", null)]
    [InlineData("fern.garden", NameProblem.Characters)]
    [InlineData("plantæ", NameProblem.Characters)]
    [InlineData("my_plants", NameProblem.Characters)]
    [InlineData("-fern", NameProblem.Hyphens)]
    [InlineData("fern-", NameProblem.Hyphens)]
    [InlineData("fern--lover", NameProblem.Hyphens)]
    [InlineData("admin", NameProblem.Reserved)]
    [InlineData("plants", NameProblem.Reserved)]
    [InlineData("stikling", NameProblem.Reserved)]
    [InlineData("stikling-team", NameProblem.Reserved)]
    [InlineData("stiklingfan", NameProblem.Reserved)]
    [InlineData("sign-in", NameProblem.Reserved)]
    public void A_name_is_checked(string name, NameProblem? problem) =>
        Assert.Equal(problem, PublicPageRules.CheckName(name));

    [Fact]
    public void A_name_hashes_to_64_lowercase_hex_characters()
    {
        var hash = PublicPageRules.NameHash("fern");

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.Equal(hash, PublicPageRules.NameHash("fern"));
        Assert.NotEqual(hash, PublicPageRules.NameHash("ferns"));
        // SHA-256 of "abc"
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", PublicPageRules.NameHash("abc"));
    }

    [Theory]
    [InlineData("da", "da")]
    [InlineData("da-DK", "da")]
    [InlineData("DA", "da")]
    [InlineData("en", "en")]
    [InlineData("de", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void The_language_is_danish_or_english(string? language, string expected) =>
        Assert.Equal(expected, PublicPageLanguages.Normalize(language));
}
