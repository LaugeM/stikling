using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class RequestLimitTests(ApiFactory api)
{
    private const int Burst = 3;

    // A small burst, so a few requests in a row go past it
    private WebApplicationFactory<Program> Limited() => api.InEnvironment("Testing", new()
    {
        ["RequestLimits:Burst"] = Burst.ToString(),
        ["RequestLimits:PerMinute"] = "60",
    });

    private HttpClient ClientFor(WebApplicationFactory<Program> server, string clerkUserId)
    {
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", api.TokenFor(clerkUserId));
        return client;
    }

    private static async Task<List<HttpResponseMessage>> GetMeTimes(HttpClient client, int times)
    {
        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < times; i++)
            responses.Add(await client.GetAsync("/me"));
        return responses;
    }

    [Fact]
    public async Task Too_many_requests_at_once_are_answered_with_when_to_try_again()
    {
        var server = Limited();

        var responses = await GetMeTimes(ClientFor(server, ApiFactory.NewClerkUserId()), Burst + 5);

        Assert.All(responses.Take(Burst), r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        // The bucket gets one request back each second, so not every one after the burst is refused
        var refused = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.NotNull(refused);
        Assert.True(refused.Headers.RetryAfter?.Delta >= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task One_person_going_over_the_limit_doesnt_slow_down_anyone_else()
    {
        var server = Limited();
        await GetMeTimes(ClientFor(server, ApiFactory.NewClerkUserId()), Burst + 5);

        var someoneElse = await GetMeTimes(ClientFor(server, ApiFactory.NewClerkUserId()), Burst);

        Assert.All(someoneElse, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    [Fact]
    public async Task The_health_check_is_never_limited()
    {
        var client = Limited().CreateClient();

        for (var i = 0; i < Burst + 5; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.GetAsync("/health")).StatusCode);
    }
}
