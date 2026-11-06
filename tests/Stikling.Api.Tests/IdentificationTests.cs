using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sentry;
using Stikling.Api.Data;
using Stikling.Api.Identification;
using Stikling.Api.Reports;
using Stikling.Core.Identification;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

/// <summary>Stands in for Pl@ntNet. It answers what it is told to, and keeps what it was sent.</summary>
public class FakePlantIdentifier : IPlantIdentifier
{
    public record Asked(int Bytes, string ContentType, string Language);

    private readonly object _lock = new();
    private readonly List<Asked> _asked = [];

    public static readonly IReadOnlyList<IdentificationSuggestion> Fiddle =
        [new("Ficus", "lyrata", 0.82, ["Fiddle-leaf fig"]), new("Ficus", "elastica", 0.12, [])];

    /// <summary>How long it takes to answer, so calls can overlap.</summary>
    public TimeSpan Delay { get; set; }

    public PlantNetAnswer Answer { get; set; } = new(PlantNetStatus.Ok, Fiddle);

    public List<Asked> Requests
    {
        get { lock (_lock) return [.. _asked]; }
    }

    public void Reset()
    {
        lock (_lock) _asked.Clear();
        Answer = new(PlantNetStatus.Ok, Fiddle);
        Delay = TimeSpan.Zero;
    }

    public async Task<PlantNetAnswer> IdentifyAsync(byte[] image, string contentType, string language, CancellationToken cancellationToken)
    {
        lock (_lock) _asked.Add(new(image.Length, contentType, language));
        if (Delay > TimeSpan.Zero)
            await Task.Delay(Delay, CancellationToken.None);
        return Answer;
    }
}

[Collection(ApiCollection.Name)]
public class IdentificationTests : IDisposable
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    private readonly ApiFactory api;

    public IdentificationTests(ApiFactory api)
    {
        this.api = api;
        api.Identifier.Reset();
    }

    public void Dispose() => api.Identifier.Reset();

    private record SignedIn(HttpClient Client, Guid PersonId);

    private async Task<SignedIn> SignIn(HttpClient? client = null)
    {
        client ??= api.ClientFor(ApiFactory.NewClerkUserId());
        var me = await client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json);
        return new SignedIn(client, me!.PersonId);
    }

    private static MultipartFormDataContent Form(byte[] bytes, string contentType = "image/jpeg", string? lang = null)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "image", "photo.jpg" } };
        if (lang is not null)
            form.Add(new StringContent(lang), "lang");
        return form;
    }

    private static async Task<string?> Reason(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reason").GetString();

    private async Task<int> UsedToday(Guid personId)
    {
        var count = 0;
        await api.WithDbAsync(async db =>
            count = await db.IdentificationUses.Where(u => u.PersonId == personId).SumAsync(u => u.Count));
        return count;
    }

    [Fact]
    public async Task Without_a_key_it_says_it_is_not_set_up()
    {
        using var factory = api.InEnvironment("Testing", new() { ["Identification:PlantNetApiKey"] = "" });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", api.TokenFor(ApiFactory.NewClerkUserId()));

        var response = await client.PostAsync("/identify", Form(Jpeg));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(api.Identifier.Requests);
    }

    [Fact]
    public async Task Without_a_sign_in_it_is_unauthorized()
    {
        var response = await api.CreateClient().PostAsync("/identify", Form(Jpeg));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(api.Identifier.Requests);
    }

    [Fact]
    public async Task Something_that_is_not_a_jpeg_or_png_is_refused_whatever_it_says_it_is()
    {
        var me = await SignIn();

        var text = await me.Client.PostAsync("/identify", Form("hello"u8.ToArray(), "image/jpeg"));
        var webp = await me.Client.PostAsync("/identify", Form("RIFF\0\0\0\0WEBP"u8.ToArray(), "image/webp"));
        var none = await me.Client.PostAsync("/identify", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, text.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, webp.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Empty(api.Identifier.Requests);
        Assert.Equal(0, await UsedToday(me.PersonId));
    }

    [Fact]
    public async Task An_image_over_10_MB_is_refused()
    {
        var me = await SignIn();
        var big = new byte[IdentificationEndpoints.MaxImageBytes + 1];
        Jpeg.CopyTo(big, 0);

        var response = await me.Client.PostAsync("/identify", Form(big));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(api.Identifier.Requests);
    }

    [Fact]
    public async Task A_jpeg_gets_the_suggestions_and_counts_once()
    {
        var me = await SignIn();

        var response = await me.Client.PostAsync("/identify", Form(Jpeg, lang: "da"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<IdentificationResponse>(ApiFactory.Json);
        Assert.Equal(2, body!.Suggestions.Count);
        Assert.Equal("Ficus", body.Suggestions[0].Genus);
        Assert.Equal("lyrata", body.Suggestions[0].Species);
        Assert.Equal(0.82, body.Suggestions[0].Score);
        Assert.Equal(["Fiddle-leaf fig"], body.Suggestions[0].CommonNames);
        var asked = Assert.Single(api.Identifier.Requests);
        Assert.Equal(("image/jpeg", "da", Jpeg.Length), (asked.ContentType, asked.Language, asked.Bytes));
        Assert.Equal(1, await UsedToday(me.PersonId));
    }

    [Fact]
    public async Task A_png_is_passed_on_as_a_png_and_the_language_defaults_to_english()
    {
        var me = await SignIn();

        var response = await me.Client.PostAsync("/identify", Form(Png, "image/png", lang: "xx"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var asked = Assert.Single(api.Identifier.Requests);
        Assert.Equal(("image/png", "en"), (asked.ContentType, asked.Language));
    }

    [Fact]
    public async Task Plantnet_finding_no_species_is_an_empty_list_and_counts()
    {
        var me = await SignIn();
        api.Identifier.Answer = PlantNetAnswer.Of(PlantNetStatus.NotFound);

        var response = await me.Client.PostAsync("/identify", Form(Jpeg));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<IdentificationResponse>(ApiFactory.Json);
        Assert.Empty(body!.Suggestions);
        Assert.Equal(1, await UsedToday(me.PersonId));
    }

    [Fact]
    public async Task A_person_gets_ten_a_day_and_then_a_429_for_the_person()
    {
        var me = await SignIn();
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await me.Client.PostAsync("/identify", Form(Jpeg))).StatusCode);

        var eleventh = await me.Client.PostAsync("/identify", Form(Jpeg));

        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
        Assert.Equal("person", await Reason(eleventh));
        Assert.Equal(10, api.Identifier.Requests.Count);
        Assert.Equal(10, await UsedToday(me.PersonId));

        // Someone else has their own ten
        var other = await SignIn();
        Assert.Equal(HttpStatusCode.OK, (await other.Client.PostAsync("/identify", Form(Jpeg))).StatusCode);
    }

    [Fact]
    public async Task Calls_sent_at_once_cannot_get_past_the_limit()
    {
        var me = await SignIn();
        api.Identifier.Delay = TimeSpan.FromMilliseconds(300);

        var responses = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => me.Client.PostAsync("/identify", Form(Jpeg))));

        Assert.Equal(10, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(20, responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests));
        Assert.Equal(10, api.Identifier.Requests.Count);
        Assert.Equal(10, await UsedToday(me.PersonId));
    }

    [Fact]
    public async Task When_everyones_total_is_reached_it_is_a_429_for_the_total()
    {
        var already = 0;
        await api.WithDbAsync(async db =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            already = await db.IdentificationUses.Where(u => u.Day == today).SumAsync(u => u.Count);
        });
        using var factory = api.InEnvironment("Testing", new() { ["Identification:DailyLimitTotal"] = (already + 1).ToString() });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", api.TokenFor(ApiFactory.NewClerkUserId()));

        var first = await client.PostAsync("/identify", Form(Jpeg));
        var second = await client.PostAsync("/identify", Form(Jpeg));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("total", await Reason(second));
        Assert.Single(api.Identifier.Requests);
    }

    [Fact]
    public async Task Plantnet_answering_429_is_a_429_for_the_total_and_does_not_count()
    {
        var me = await SignIn();
        api.Identifier.Answer = PlantNetAnswer.Of(PlantNetStatus.TooMany);

        var response = await me.Client.PostAsync("/identify", Form(Jpeg));

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("total", await Reason(response));
        Assert.Equal(0, await UsedToday(me.PersonId));
    }

    [Fact]
    public async Task Plantnet_failing_is_a_502_and_does_not_count()
    {
        var me = await SignIn();
        api.Identifier.Answer = PlantNetAnswer.Of(PlantNetStatus.Failed);

        var response = await me.Client.PostAsync("/identify", Form(Jpeg));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(0, await UsedToday(me.PersonId));
    }

    [Fact]
    public async Task Rows_older_than_yesterday_are_deleted_when_someone_identifies()
    {
        var me = await SignIn();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await api.WithDbAsync(async db =>
        {
            db.IdentificationUses.AddRange(
                new IdentificationUse { PersonId = me.PersonId, Day = today.AddDays(-2), Count = 3 },
                new IdentificationUse { PersonId = me.PersonId, Day = today.AddDays(-1), Count = 4 });
            await db.SaveChangesAsync();
        });

        await me.Client.PostAsync("/identify", Form(Jpeg));

        await api.WithDbAsync(async db =>
        {
            var days = await db.IdentificationUses.Where(u => u.PersonId == me.PersonId).Select(u => u.Day).ToListAsync();
            Assert.DoesNotContain(today.AddDays(-2), days);
            Assert.Contains(today.AddDays(-1), days);
            Assert.Contains(today, days);
        });
    }

    [Fact]
    public async Task Deleting_the_account_removes_the_counts()
    {
        var me = await SignIn();
        await me.Client.PostAsync("/identify", Form(Jpeg));
        Assert.Equal(1, await UsedToday(me.PersonId));

        Assert.Equal(HttpStatusCode.NoContent, (await me.Client.DeleteAsync("/me")).StatusCode);

        Assert.Equal(0, await UsedToday(me.PersonId));
    }

    // The key is in the address Pl@ntNet is called on

    private sealed class CapturingLogs : ILoggerProvider, ILogger
    {
        public List<string> Lines { get; } = [];

        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Dispose() { }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Lines)
                Lines.Add(formatter(state, exception) + " " + exception);
        }
    }

    private sealed class Answering(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? Called { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Called = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task The_key_in_the_address_reaches_no_log(HttpStatusCode status)
    {
        var logs = new CapturingLogs();
        var handler = new Answering(status, """{ "results": [] }""");
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddIdentification(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Identification:PlantNetApiKey"] = ApiFactory.PlantNetKey })
            .Build());
        services.AddHttpClient<IPlantIdentifier, PlantNetIdentifier>().ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IPlantIdentifier>().IdentifyAsync(Jpeg, "image/jpeg", "en", CancellationToken.None);

        // It did call Pl@ntNet with the key, and wrote about it, but never the key
        Assert.Contains(ApiFactory.PlantNetKey, handler.Called!.Query);
        Assert.DoesNotContain(logs.Lines, line => line.Contains(ApiFactory.PlantNetKey) || line.Contains("api-key"));
    }

    [Fact]
    public void A_breadcrumb_about_a_plantnet_request_is_dropped()
    {
        var withKey = new Breadcrumb("POST https://my-api.plantnet.org/v2/identify/all?api-key=secret", "http");
        var inData = new Breadcrumb("request", "http", new Dictionary<string, string> { ["url"] = "https://my-api.plantnet.org/v2/identify/all" });
        var other = new Breadcrumb("GET https://api.stikling.app/health", "http");

        Assert.Null(ErrorReporting.ScrubBreadcrumb(withKey, new SentryHint()));
        Assert.Null(ErrorReporting.ScrubBreadcrumb(inData, new SentryHint()));
        Assert.Same(other, ErrorReporting.ScrubBreadcrumb(other, new SentryHint()));
    }
}
