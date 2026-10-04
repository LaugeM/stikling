using Stikling.Api.Auth;

namespace Stikling.Api.Tests;

public class AppOriginsTests
{
    private static readonly AppOrigins Origins = new() { Origins = ["https://stikling.app", "http://localhost:*"] };

    [Theory]
    [InlineData("https://stikling.app")]
    [InlineData("HTTPS://Stikling.app")]
    [InlineData("http://localhost:5170")]
    [InlineData("http://localhost:5171")]
    public void Allows_the_listed_origins_and_any_port_where_the_port_is_a_star(string origin)
    {
        Assert.True(Origins.Allows(origin));
    }

    [Theory]
    [InlineData("https://stikling.app:8443")]
    [InlineData("https://www.stikling.app")]
    [InlineData("https://localhost:5170")]
    [InlineData("http://localhost")]
    [InlineData("http://localhost:")]
    [InlineData("http://localhost:5170.example.com")]
    [InlineData("http://localhost.example.com:5170")]
    public void Turns_away_anything_else(string origin)
    {
        Assert.False(Origins.Allows(origin));
    }
}
