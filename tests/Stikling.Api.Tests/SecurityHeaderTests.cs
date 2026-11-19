namespace Stikling.Api.Tests;

/// <summary>The headers every answer from the hosted API carries.</summary>
[Collection(ApiCollection.Name)]
public class SecurityHeaderTests(ApiFactory api)
{
    private static HttpRequestMessage Health(string? forwardedProto)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "http://api.stikling.app/health");
        if (forwardedProto is not null)
            request.Headers.Add("X-Forwarded-Proto", forwardedProto);
        return request;
    }

    [Fact]
    public async Task A_request_that_came_in_over_https_is_told_to_keep_to_it()
    {
        var response = await api.CreateClient().SendAsync(Health("https"));

        Assert.Equal("max-age=31536000", Assert.Single(response.Headers.GetValues("Strict-Transport-Security")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Hsts_is_only_sent_over_https()
    {
        var response = await api.CreateClient().SendAsync(Health(null));

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Hsts_is_left_out_locally()
    {
        var response = await api.InEnvironment("Development").CreateClient().SendAsync(Health("https"));

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }
}
