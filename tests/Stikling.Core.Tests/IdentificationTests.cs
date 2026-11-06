using System.Text.Json;
using Stikling.Core.Identification;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class IdentificationTests
{
    [Theory]
    [InlineData(1.0, IdentificationMatch.Good)]
    [InlineData(0.5, IdentificationMatch.Good)]
    [InlineData(0.4999, IdentificationMatch.Possible)]
    [InlineData(0.15, IdentificationMatch.Possible)]
    [InlineData(0.1499, IdentificationMatch.LongShot)]
    [InlineData(0.0, IdentificationMatch.LongShot)]
    public void The_score_is_put_in_words(double score, IdentificationMatch expected) =>
        Assert.Equal(expected, IdentificationRules.Match(score));

    [Theory]
    [InlineData("Monstera", "deliciosa", "Albo", "Monstera", "deliciosa", "Albo")]
    [InlineData("monstera", " Deliciosa ", "Albo", "Monstera", "deliciosa", "Albo")]
    [InlineData("Monstera", "adansonii", "Albo", "Monstera", "deliciosa", null)]
    [InlineData("Philodendron", "deliciosa", "Albo", "Monstera", "deliciosa", null)]
    [InlineData(null, null, "Albo", "Monstera", "deliciosa", null)]
    public void A_cultivar_is_kept_only_when_the_species_stays(
        string? genus, string? species, string? cultivar, string newGenus, string newSpecies, string? expected) =>
        Assert.Equal(expected, IdentificationRules.CultivarAfterPick(genus, species, cultivar, newGenus, newSpecies));

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("", null, true)]
    [InlineData("  ", null, true)]
    [InlineData("Monstera", null, false)]
    [InlineData("Monstera", NameQualifier.Cf, true)]
    [InlineData("Monstera", NameQualifier.Aff, true)]
    [InlineData("Monstera", NameQualifier.Sp, true)]
    [InlineData("Monstera", NameQualifier.Unknown, true)]
    [InlineData(null, NameQualifier.Cf, true)]
    public void A_name_is_wanted_when_missing_or_unsure(string? genus, NameQualifier? qualifier, bool expected) =>
        Assert.Equal(expected, IdentificationRules.NameWanted(genus, qualifier));

    [Theory]
    [InlineData(null, NameQualifier.Cf)]
    [InlineData(NameQualifier.Cf, NameQualifier.Cf)]
    [InlineData(NameQualifier.Aff, NameQualifier.Aff)]
    [InlineData(NameQualifier.Sp, NameQualifier.Cf)]
    [InlineData(NameQualifier.Unknown, NameQualifier.Cf)]
    public void A_name_picked_on_the_page_is_marked_cf(NameQualifier? before, NameQualifier? expected) =>
        Assert.Equal(expected, IdentificationRules.QualifierAfterPick(before));

    private static string Item(string full, string genus, double score, string? commonNames = "[\"Swiss cheese plant\"]") =>
        $$"""
        { "score": {{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
          "species": { "scientificNameWithoutAuthor": "{{full}}", "genus": { "scientificNameWithoutAuthor": "{{genus}}" }
          {{(commonNames is null ? "" : $", \"commonNames\": {commonNames}")}} } }
        """;

    private static string Results(params string[] items) => $$"""{ "results": [ {{string.Join(",", items)}} ] }""";

    [Fact]
    public void An_item_gives_genus_species_score_and_common_names()
    {
        var found = Assert.Single(PlantNetResults.Parse(Results(Item("Monstera deliciosa", "Monstera", 0.83)))!);

        Assert.Equal("Monstera", found.Genus);
        Assert.Equal("deliciosa", found.Species);
        Assert.Equal(0.83, found.Score);
        Assert.Equal(["Swiss cheese plant"], found.CommonNames);
    }

    [Fact]
    public void A_hybrid_keeps_its_cross_sign()
    {
        var found = Assert.Single(PlantNetResults.Parse(Results(Item("Mentha × piperita", "Mentha", 0.4)))!);

        Assert.Equal("Mentha", found.Genus);
        Assert.Equal("× piperita", found.Species);
    }

    [Fact]
    public void A_name_that_does_not_split_is_skipped()
    {
        var found = PlantNetResults.Parse(Results(
            Item("× Fatshedera lizei", "Fatshedera", 0.9),
            Item("Monstera", "Monstera", 0.8),
            Item("Ficus lyrata", "Ficus", 0.2)))!;

        var kept = Assert.Single(found);
        Assert.Equal("lyrata", kept.Species);
    }

    [Fact]
    public void Missing_common_names_give_an_empty_list()
    {
        var found = Assert.Single(PlantNetResults.Parse(Results(Item("Ficus lyrata", "Ficus", 0.3, commonNames: null)))!);

        Assert.Empty(found.CommonNames);
    }

    [Fact]
    public void Suggestions_come_best_first_and_at_most_five()
    {
        var items = Enumerable.Range(1, 8).Select(i => Item($"Ficus s{i}", "Ficus", i / 10.0)).ToArray();

        var found = PlantNetResults.Parse(Results(items))!;

        Assert.Equal(5, found.Count);
        Assert.Equal("s8", found[0].Species);
        Assert.Equal("s4", found[^1].Species);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{ \"message\": \"nope\" }")]
    [InlineData("[]")]
    public void Text_that_is_not_an_answer_is_null(string? json) =>
        Assert.Null(PlantNetResults.Parse(json));

    [Fact]
    public void An_answer_with_no_results_is_an_empty_list() =>
        Assert.Empty(PlantNetResults.Parse("""{ "results": [] }""")!);

    [Fact]
    public void Our_own_response_is_read_back()
    {
        var json = JsonSerializer.Serialize(
            new IdentificationResponse([new("Ficus", "lyrata", 0.6, ["Fiddle-leaf fig"])]), JsonSerializerOptions.Web);

        var outcome = IdentificationOutcome.From(200, json, ownKey: false);

        Assert.True(outcome.Succeeded);
        Assert.Equal("lyrata", Assert.Single(outcome.Suggestions).Species);
    }

    [Theory]
    [InlineData(-1, false, IdentificationProblem.Offline)]
    [InlineData(0, true, IdentificationProblem.Missing)]
    [InlineData(503, false, IdentificationProblem.NotAvailable)]
    [InlineData(401, true, IdentificationProblem.KeyRejected)]
    [InlineData(403, true, IdentificationProblem.KeyRejected)]
    [InlineData(429, true, IdentificationProblem.KeyLimit)]
    [InlineData(502, false, IdentificationProblem.Failed)]
    [InlineData(500, true, IdentificationProblem.Failed)]
    [InlineData(404, false, IdentificationProblem.Failed)]
    [InlineData(401, false, IdentificationProblem.Failed)]
    public void A_status_becomes_a_problem(int status, bool ownKey, IdentificationProblem expected) =>
        Assert.Equal(expected, IdentificationOutcome.From(status, null, ownKey).Problem);

    [Theory]
    [InlineData("""{ "reason": "person" }""", IdentificationProblem.PersonLimit)]
    [InlineData("""{ "status": 429, "reason": "total" }""", IdentificationProblem.TotalLimit)]
    [InlineData("""{ "status": 429 }""", IdentificationProblem.Failed)]
    [InlineData(null, IdentificationProblem.Failed)]
    public void The_apis_429_says_whose_limit_it_is(string? body, IdentificationProblem expected) =>
        Assert.Equal(expected, IdentificationOutcome.From(429, body, ownKey: false).Problem);

    [Fact]
    public void Plantnet_not_finding_a_species_is_no_suggestions_not_a_failure()
    {
        var outcome = IdentificationOutcome.From(404, null, ownKey: true);

        Assert.True(outcome.Succeeded);
        Assert.Empty(outcome.Suggestions);
    }

    [Fact]
    public void An_answer_that_cannot_be_read_is_a_failure()
    {
        Assert.Equal(IdentificationProblem.Failed, IdentificationOutcome.From(200, "<html>", ownKey: true).Problem);
        Assert.Equal(IdentificationProblem.Failed, IdentificationOutcome.From(200, "<html>", ownKey: false).Problem);
    }
}
