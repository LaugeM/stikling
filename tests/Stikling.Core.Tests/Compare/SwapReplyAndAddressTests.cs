using Stikling.Core.Compare;
using Stikling.Core.Models;
using Stikling.Core.Sharing;
using static Stikling.Core.Tests.CompareTestData;

namespace Stikling.Core.Tests;

public class SwapReplyTextTests
{
    private static ListComparisonResult Result(IEnumerable<FriendPlant>? wanted = null, IEnumerable<ForThemMatch>? offered = null) =>
        new(
            [.. (wanted ?? []).Select(p => new ForYouMatch(p, AWish(), MatchKind.Exact, null))],
            [.. offered ?? []],
            []);

    private static ForThemMatch Mine(string name) =>
        new(Guid.NewGuid(), SubjectType.Plant, name, null, TheirWish(name), MatchKind.Exact, null, false, true);

    [Fact]
    public void Both_empty_just_asks_if_they_would_like_to_swap()
    {
        Assert.Equal(
            "Hi! I compared our plant lists in Stikling.\n\nWould you like to swap some plants?\n\nKept in Stikling, stikling.app",
            SwapReplyText.Write(Result(), null).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Lists_theirs_with_what_is_spare_and_mine()
    {
        var text = SwapReplyText.Write(Result(
            [Theirs("Monstera Albo", spare: FriendSpare.Cuttings, count: 2, stage: PropagationStage.Rooted), Theirs("Hoya")],
            [Mine("My Alocasia")]), "Their page").ReplaceLineEndings("\n");

        Assert.Equal(
            "Hi! I compared our plant lists in Stikling.\n\n"
            + "Could I have one of these?\n- Monstera Albo, can spare 2 rooted cuttings\n- Hoya\n\n"
            + "I could offer:\n- My Alocasia\n\n"
            + "Kept in Stikling, stikling.app", text);
    }

    [Fact]
    public void A_section_with_no_rows_is_left_out()
    {
        var onlyMine = SwapReplyText.Write(Result(offered: [Mine("A")]), null);
        var onlyTheirs = SwapReplyText.Write(Result([Theirs("B", spare: FriendSpare.UpForSwap)]), null);

        Assert.DoesNotContain("Could I have", onlyMine);
        Assert.Contains("I could offer:", onlyMine);
        Assert.DoesNotContain("I could offer", onlyTheirs);
        Assert.Contains("- B, up for swap", onlyTheirs);
    }

    [Fact]
    public void Is_written_in_Danish()
    {
        var text = In("da", () => SwapReplyText.Write(Result([Theirs("B")], [Mine("A")]), null));

        Assert.Contains("Hej, jeg har sammenlignet vores plantelister i Stikling.", text);
        Assert.Contains("Må jeg få en af dem her?", text);
        Assert.Contains("Jeg kan tilbyde:", text);
        Assert.EndsWith("Samlet i Stikling, stikling.app", text);
        Assert.Contains("Har du lyst til at bytte nogle planter?", In("da", () => SwapReplyText.Write(Result(), null)));
    }

    [Fact]
    public void Spare_text_for_each_kind()
    {
        Assert.Null(Theirs("a").SpareText());
        Assert.Equal("Cuttings to spare", Theirs("a", spare: FriendSpare.CuttingsToSpare).SpareText());
        Assert.Equal("Up for swap", Theirs("a", spare: FriendSpare.UpForSwap).SpareText());
        Assert.Equal("Can spare 1 rooting cutting", Theirs("a", spare: FriendSpare.Cuttings, count: 1, stage: PropagationStage.Rooting).SpareText());
        Assert.Equal("Can spare 3 cuttings", Theirs("a", spare: FriendSpare.Cuttings, count: 3).SpareText());
    }
}

public class FriendAddressTests
{
    private const string Token = "AbCdEfGhIjKlMnOpQrStUv";

    [Theory]
    [InlineData("https://share.stikling.app/@Fern", "@fern")]
    [InlineData("share.stikling.app/@fern/plants", "@fern")]
    [InlineData("  @Fern-Friend  ", "@fern-friend")]
    [InlineData("@fern", "@fern")]
    [InlineData("http://localhost:5180/s/@fern", "@fern")]
    [InlineData("https://share.stikling.app/@fern?page=2", "@fern")]
    [InlineData("https://share.stikling.app/" + Token, Token)]
    [InlineData("http://localhost:5180/s/" + Token + "/swap", Token)]
    [InlineData("/s/" + Token, Token)]
    public void Finds_the_address(string text, string expected) => Assert.Equal(expected, FriendAddress.Of(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Monstera deliciosa")]
    [InlineData("Monstera\nHoya")]
    [InlineData("@fe")]
    [InlineData("@stikling-team")]
    [InlineData("@fern_friend")]
    [InlineData("https://share.stikling.app/")]
    [InlineData("https://share.stikling.app/short")]
    [InlineData("https://example.com/@fern")]
    [InlineData(Token)]
    public void Anything_else_is_not_an_address(string? text) => Assert.Null(FriendAddress.Of(text));
}
