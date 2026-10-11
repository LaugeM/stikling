using Stikling.Core.ErrorReports;

namespace Stikling.Core.Tests;

public class ReportScrubbingTests
{
    [Theory]
    [InlineData("https://stikling.app/plants/1b2c?name=Monstera", "https://stikling.app/plants/1b2c")]
    [InlineData("https://stikling.app/plants/1b2c#notes", "https://stikling.app/plants/1b2c")]
    [InlineData("https://stikling.app/today?x=1#top", "https://stikling.app/today")]
    [InlineData("/plants/1b2c?name=Monstera", "/plants/1b2c")]
    [InlineData("https://stikling.app/plants/1b2c", "https://stikling.app/plants/1b2c")]
    [InlineData("", "")]
    public void The_query_string_and_fragment_are_removed(string url, string expected) =>
        Assert.Equal(expected, ReportScrubbing.WithoutQueryOrFragment(url));

    [Fact]
    public void Null_stays_null() => Assert.Null(ReportScrubbing.WithoutQueryOrFragment(null));
}
