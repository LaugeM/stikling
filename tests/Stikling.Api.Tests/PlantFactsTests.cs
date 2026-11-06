using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sentry;
using Stikling.Api.PlantFacts;
using Stikling.Api.Reports;
using Stikling.Core.PlantFacts;
using Facts = Stikling.Core.PlantFacts.PlantFacts;

namespace Stikling.Api.Tests;

/// <summary>Stands in for Wikipedia. Answers are set per genus, and every call is recorded.</summary>
public sealed class FakeWikipedia : IWikipediaSource
{
    public ConcurrentDictionary<string, SourceResult<WikipediaSummary>> Answers { get; } = new();

    public ConcurrentQueue<(string Genus, string? Species, string Language)> Calls { get; } = new();

    public Task<SourceResult<WikipediaSummary>> FindAsync(string genus, string? species, string language, CancellationToken cancellationToken)
    {
        Calls.Enqueue((genus, species, language));
        return Task.FromResult(Answers.GetValueOrDefault(genus, SourceResult<WikipediaSummary>.NotFound)!);
    }

    public int CallsFor(string genus) => Calls.Count(c => c.Genus == genus);
}

/// <summary>Stands in for Trefle. Answers are set per genus, and every call is recorded.</summary>
public sealed class FakeTrefle : ITrefleSource
{
    public ConcurrentDictionary<string, SourceResult<TrefleFacts>> Answers { get; } = new();

    public ConcurrentQueue<(string Genus, string Species)> Calls { get; } = new();

    public Task<SourceResult<TrefleFacts>> FindAsync(string genus, string species, CancellationToken cancellationToken)
    {
        Calls.Enqueue((genus, species));
        return Task.FromResult(Answers.GetValueOrDefault(genus, SourceResult<TrefleFacts>.NotFound)!);
    }

    public int CallsFor(string genus) => Calls.Count(c => c.Genus == genus);
}

[Collection(ApiCollection.Name)]
public class PlantFactsTests(ApiFactory api)
{
    private static readonly WikipediaSummary Article =
        new("A climbing plant.", "en", "Test plant", "https://en.wikipedia.org/wiki/Test_plant");

    private static readonly TrefleFacts Data =
        new(300, [3, 4], "Vine", [new FactSource("Kew", "https://kew.example/")]);

    // A genus of its own for each test, so answers kept by one don't reach another
    private static string NewGenus() => "Genus" + string.Concat(Guid.NewGuid().ToString("N").Take(12).Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16))));

    private WebApplicationFactory<Program> WithTrefle() =>
        api.InEnvironment("Testing", new() { ["PlantFacts:TrefleToken"] = "test-token" });

    private static async Task<Facts> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Facts>(ApiFactory.Json))!;

    [Theory]
    [InlineData("")]
    [InlineData("genus=")]
    [InlineData("genus=Monstera1")]
    [InlineData("genus=Mon%2Fstera")]
    [InlineData("genus=Monstera&species=deli%3Cb%3E")]
    [InlineData("genus=Monstera&species=1")]
    [InlineData("genus=Monstera&language=fr")]
    public async Task Bad_names_or_language_are_answered_with_400(string query)
    {
        var response = await api.CreateClient().GetAsync("/plant-facts?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Names_over_60_characters_are_answered_with_400()
    {
        var client = api.CreateClient();
        var long61 = new string('a', 61);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/plant-facts?genus={long61}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/plant-facts?genus=Monstera&species={long61}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/plant-facts?genus={new string('a', 60)}")).StatusCode);
    }

    [Fact]
    public async Task It_needs_no_sign_in_and_answers_with_the_summary_and_a_day_of_cache()
    {
        var genus = NewGenus();
        api.Wikipedia.Answers[genus] = SourceResult<WikipediaSummary>.Found(Article);

        var response = await api.CreateClient().GetAsync($"/plant-facts?genus={genus.ToLowerInvariant()}&species=Deliciosa&language=da");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=86400", response.Headers.CacheControl?.ToString());
        var facts = await ReadAsync(response);
        Assert.Equal(Article, facts.Summary);
        Assert.Null(facts.Facts);

        // The names reach the source tidied the way botanical names are written
        var call = Assert.Single(api.Wikipedia.Calls, c => c.Genus == genus);
        Assert.Equal("deliciosa", call.Species);
        Assert.Equal("da", call.Language);
    }

    [Fact]
    public async Task Nothing_found_is_still_200_with_both_parts_empty()
    {
        var response = await api.CreateClient().GetAsync($"/plant-facts?genus={NewGenus()}&species=nothing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var facts = await ReadAsync(response);
        Assert.Null(facts.Summary);
        Assert.Null(facts.Facts);
    }

    [Fact]
    public async Task Trefle_is_not_asked_without_a_token()
    {
        var genus = NewGenus();
        api.Wikipedia.Answers[genus] = SourceResult<WikipediaSummary>.Found(Article);
        api.Trefle.Answers[genus] = SourceResult<TrefleFacts>.Found(Data);

        var response = await api.CreateClient().GetAsync($"/plant-facts?genus={genus}&species=species");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var facts = await ReadAsync(response);
        Assert.NotNull(facts.Summary);
        Assert.Null(facts.Facts);
        Assert.Equal(0, api.Trefle.CallsFor(genus));
    }

    [Fact]
    public async Task Trefle_facts_come_with_a_token_for_a_species_but_not_for_a_genus()
    {
        var genus = NewGenus();
        api.Wikipedia.Answers[genus] = SourceResult<WikipediaSummary>.Found(Article);
        api.Trefle.Answers[genus] = SourceResult<TrefleFacts>.Found(Data);
        var client = WithTrefle().CreateClient();

        var species = await ReadAsync(await client.GetAsync($"/plant-facts?genus={genus}&species=species"));
        var genusOnly = await ReadAsync(await client.GetAsync($"/plant-facts?genus={genus}"));

        Assert.Equal(Article, species.Summary);
        Assert.NotNull(species.Facts);
        Assert.Equal(300, species.Facts.MaxHeightCm);
        Assert.Equal([3, 4], species.Facts.BloomMonths);
        Assert.Equal("Vine", species.Facts.GrowthHabit);
        Assert.Equal(new FactSource("Kew", "https://kew.example/"), Assert.Single(species.Facts.Sources));
        Assert.Null(genusOnly.Facts);
        Assert.Equal(1, api.Trefle.CallsFor(genus));
    }

    [Fact]
    public async Task A_second_request_is_answered_from_the_cache()
    {
        var genus = NewGenus();
        api.Wikipedia.Answers[genus] = SourceResult<WikipediaSummary>.Found(Article);
        var client = api.CreateClient();

        var first = await ReadAsync(await client.GetAsync($"/plant-facts?genus={genus}&species=species&language=en"));
        var second = await ReadAsync(await client.GetAsync($"/plant-facts?genus={genus.ToUpperInvariant()}&species=SPECIES&language=en"));

        Assert.Equal(first, second);
        Assert.Equal(1, api.Wikipedia.CallsFor(genus));

        // Another language is another answer
        await client.GetAsync($"/plant-facts?genus={genus}&species=species&language=da");
        Assert.Equal(2, api.Wikipedia.CallsFor(genus));
    }

    [Fact]
    public async Task An_answer_with_nothing_in_it_is_cached_too()
    {
        var genus = NewGenus();
        var client = api.CreateClient();

        await client.GetAsync($"/plant-facts?genus={genus}");
        await client.GetAsync($"/plant-facts?genus={genus}");

        Assert.Equal(1, api.Wikipedia.CallsFor(genus));
    }

    [Fact]
    public async Task It_is_502_only_when_wikipedia_failed_and_there_are_no_facts()
    {
        var genus = NewGenus();
        api.Wikipedia.Answers[genus] = SourceResult<WikipediaSummary>.Error;
        var client = api.CreateClient();

        Assert.Equal(HttpStatusCode.BadGateway, (await client.GetAsync($"/plant-facts?genus={genus}&species=species")).StatusCode);

        // The failure is remembered for a few minutes, so a source that is down isn't asked again at once
        Assert.Equal(HttpStatusCode.BadGateway, (await client.GetAsync($"/plant-facts?genus={genus}&species=species")).StatusCode);
        Assert.Equal(1, api.Wikipedia.CallsFor(genus));
    }

    [Fact]
    public async Task Facts_from_trefle_are_answered_even_when_wikipedia_failed_but_not_kept_by_the_browser()
    {
        var genus = NewGenus();
        api.Wikipedia.Answers[genus] = SourceResult<WikipediaSummary>.Error;
        api.Trefle.Answers[genus] = SourceResult<TrefleFacts>.Found(Data);
        var client = WithTrefle().CreateClient();

        var response = await client.GetAsync($"/plant-facts?genus={genus}&species=species");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var facts = await ReadAsync(response);
        Assert.Null(facts.Summary);
        Assert.NotNull(facts.Facts);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        await client.GetAsync($"/plant-facts?genus={genus}&species=species");
        Assert.Equal(1, api.Wikipedia.CallsFor(genus));
    }

    [Fact]
    public async Task Wikipedia_alone_is_enough_when_trefle_failed_but_the_answer_is_no_store()
    {
        var genus = NewGenus();
        api.Wikipedia.Answers[genus] = SourceResult<WikipediaSummary>.Found(Article);
        api.Trefle.Answers[genus] = SourceResult<TrefleFacts>.Error;
        var client = WithTrefle().CreateClient();

        var response = await client.GetAsync($"/plant-facts?genus={genus}&species=species");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull((await ReadAsync(response)).Summary);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        // Remembered for a few minutes on the server, and still sent as no-store
        var again = await client.GetAsync($"/plant-facts?genus={genus}&species=species");
        Assert.Equal("no-store", again.Headers.CacheControl?.ToString());
        Assert.Equal(1, api.Wikipedia.CallsFor(genus));
    }

    [Fact]
    public async Task One_address_is_limited_on_the_plant_facts_without_touching_the_share_pages()
    {
        var server = api.InEnvironment("Testing", new()
        {
            ["RequestLimits:FactsBurst"] = "3",
            ["RequestLimits:FactsPerMinute"] = "60",
        });
        var client = server.CreateClient();
        var genus = NewGenus();

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < 8; i++)
            responses.Add(await client.GetAsync($"/plant-facts?genus={genus}"));

        Assert.All(responses.Take(3), r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var refused = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.NotNull(refused);
        Assert.True(refused.Headers.RetryAfter?.Delta >= TimeSpan.FromSeconds(1));

        // The share pages have a bucket of their own for the same address
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.GetAsync("/s/unknown")).StatusCode);
    }

    [Fact]
    public async Task A_signed_in_person_keeps_the_per_person_bucket()
    {
        var server = api.InEnvironment("Testing", new()
        {
            ["RequestLimits:FactsBurst"] = "1",
            ["RequestLimits:FactsPerMinute"] = "60",
        });
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", api.TokenFor(ApiFactory.NewClerkUserId()));
        var genus = NewGenus();

        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/plant-facts?genus={genus}")).StatusCode);
    }
}

/// <summary>The real sources, against canned answers instead of the internet. No Docker needed.</summary>
public class PlantFactsSourceTests
{
    private const string EnglishPage = """
        {"type":"standard","title":"Test plant","description":"Species of plant","extract":"An English text.",
         "content_urls":{"desktop":{"page":"https://en.wikipedia.org/wiki/Test_plant"}}}
        """;

    private const string DanishPage = """
        {"type":"standard","title":"Testplante","description":"Plantearter","extract":"En dansk tekst.",
         "content_urls":{"desktop":{"page":"https://da.wikipedia.org/wiki/Testplante"}}}
        """;

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private static WikipediaSource Wikipedia(Handler handler, ILogger<WikipediaSource>? logger = null)
    {
        var http = new HttpClient(handler);
        http.DefaultRequestHeaders.UserAgent.ParseAdd(WikipediaSource.UserAgent);
        return new WikipediaSource(http, logger ?? NullLogger<WikipediaSource>.Instance);
    }

    [Fact]
    public async Task Danish_is_asked_first_and_used_when_it_has_the_article()
    {
        var handler = new Handler(r => r.RequestUri!.Host == "da.wikipedia.org" ? Json(DanishPage) : Json(EnglishPage));

        var result = await Wikipedia(handler).FindAsync("Monstera", "deliciosa", "da", default);

        Assert.Equal("da", result.Value?.Language);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://da.wikipedia.org/api/rest_v1/page/summary/Monstera_deliciosa", request.RequestUri!.ToString());
        Assert.Equal("Stikling/1.0 (https://stikling.app)", request.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task Danish_falls_back_to_english_on_404()
    {
        var handler = new Handler(r => r.RequestUri!.Host == "da.wikipedia.org"
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : Json(EnglishPage));

        var result = await Wikipedia(handler).FindAsync("Monstera", "deliciosa", "da", default);

        Assert.False(result.Failed);
        Assert.Equal("en", result.Value?.Language);
        Assert.Equal(["da.wikipedia.org", "en.wikipedia.org"], handler.Requests.Select(r => r.RequestUri!.Host));
    }

    [Fact]
    public async Task English_is_not_asked_twice()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await Wikipedia(handler).FindAsync("Monstera", "deliciosa", "en", default);

        Assert.Null(result.Value);
        Assert.False(result.Failed);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_server_error_is_a_failure_and_not_a_not_found()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await Wikipedia(handler).FindAsync("Monstera", "deliciosa", "en", default);

        Assert.True(result.Failed);
    }

    [Fact]
    public async Task A_genus_is_looked_up_by_itself_and_checked_for_being_a_plant()
    {
        var handler = new Handler(_ => Json(EnglishPage.Replace("Species of plant", "Village").Replace("An English text.", "A village.")));

        var result = await Wikipedia(handler).FindAsync("Ficus", null, "en", default);

        Assert.Null(result.Value);
        Assert.False(result.Failed);
        Assert.EndsWith("/page/summary/Ficus", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task Trefle_is_asked_by_slug_and_a_404_is_not_found()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var source = new TrefleSource(new HttpClient(handler), Options.Create(new PlantFactsOptions { TrefleToken = "secret-token" }), NullLogger<TrefleSource>.Instance);

        var result = await source.FindAsync("Monstera", "deliciosa", default);

        Assert.Null(result.Value);
        Assert.False(result.Failed);
        Assert.StartsWith("https://trefle.io/api/v1/species/monstera-deliciosa?token=", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task Trefle_facts_are_parsed_from_the_answer()
    {
        var handler = new Handler(_ => Json("""{"data":{"specifications":{"maximum_height":{"cm":150}},"growth":{"bloom_months":["jun"]}}}"""));
        var source = new TrefleSource(new HttpClient(handler), Options.Create(new PlantFactsOptions { TrefleToken = "t" }), NullLogger<TrefleSource>.Instance);

        var result = await source.FindAsync("Monstera", "deliciosa", default);

        Assert.Equal(150, result.Value?.MaxHeightCm);
        Assert.Equal([6], result.Value?.BloomMonths);
    }

    [Fact]
    public async Task The_token_never_reaches_the_logs_when_trefle_fails()
    {
        var logger = new CapturingLogger<TrefleSource>();
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var source = new TrefleSource(new HttpClient(handler), Options.Create(new PlantFactsOptions { TrefleToken = "secret-token" }), logger);

        var result = await source.FindAsync("Monstera", "deliciosa", default);

        Assert.True(result.Failed);
        Assert.NotEmpty(logger.Lines);
        Assert.Contains(logger.Lines, l => l.Contains("401"));
        Assert.All(logger.Lines, l => Assert.DoesNotContain("secret-token", l));
        Assert.All(logger.Lines, l => Assert.DoesNotContain("trefle.io", l));
    }

    [Fact]
    public async Task The_token_never_reaches_the_logs_when_trefle_cant_be_reached()
    {
        var logger = new CapturingLogger<TrefleSource>();
        var http = new HttpClient(new ThrowingHandler());
        var source = new TrefleSource(http, Options.Create(new PlantFactsOptions { TrefleToken = "secret-token" }), logger);

        var result = await source.FindAsync("Monstera", "deliciosa", default);

        Assert.True(result.Failed);
        Assert.All(logger.Lines, l => Assert.DoesNotContain("secret-token", l));
    }

    [Fact]
    public void Error_reports_drop_the_breadcrumbs_for_outgoing_requests_but_keep_the_rest()
    {
        var http = new Breadcrumb("GET https://trefle.io/api/v1/species/x?token=secret-token", "http",
            new Dictionary<string, string> { ["url"] = "https://trefle.io/api/v1/species/x", ["http.query"] = "token=secret-token" }, "HttpClient");
        var log = new Breadcrumb("Something happened", "default");

        Assert.Null(ErrorReporting.ScrubBreadcrumb(http, new SentryHint()));
        Assert.Same(log, ErrorReporting.ScrubBreadcrumb(log, new SentryHint()));
    }

    [Fact]
    public void Error_reports_keep_no_query_from_the_request_address()
    {
        var e = new SentryEvent { Request = new SentryRequest { Url = "https://trefle.io/api/v1/species/x?token=secret-token", QueryString = "token=secret-token" } };

        var scrubbed = ErrorReporting.Scrub(e, new SentryHint());

        Assert.DoesNotContain("secret-token", scrubbed!.Request.Url);
        Assert.True(string.IsNullOrEmpty(scrubbed.Request.QueryString));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("no connection to " + request.RequestUri);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Lines.Add(formatter(state, exception));
            if (exception is not null)
                Lines.Add(exception.ToString());
        }
    }
}
