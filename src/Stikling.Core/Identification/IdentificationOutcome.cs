using System.Text.Json;

namespace Stikling.Core.Identification;

/// <summary>Why an identification gave nothing to show.</summary>
public enum IdentificationProblem
{
    None,

    /// <summary>Nobody is signed in and there is no key of the person's own.</summary>
    NeedsAccount,

    /// <summary>No connection.</summary>
    Offline,

    /// <summary>The server has no Pl@ntNet key set.</summary>
    NotAvailable,

    /// <summary>The person has used their identifications for today.</summary>
    PersonLimit,

    /// <summary>Everyone's identifications for today are used up.</summary>
    TotalLimit,

    /// <summary>Pl@ntNet didn't accept the person's own key.</summary>
    KeyRejected,

    /// <summary>The person's own key has used today's identifications.</summary>
    KeyLimit,

    /// <summary>The photo isn't on this device.</summary>
    Missing,

    Failed,
}

/// <summary>What an identification came to: suggestions (maybe none), or why not.</summary>
public sealed record IdentificationOutcome(IReadOnlyList<IdentificationSuggestion> Suggestions, IdentificationProblem Problem)
{
    public bool Succeeded => Problem == IdentificationProblem.None;

    public static IdentificationOutcome Failure(IdentificationProblem problem) => new([], problem);

    /// <summary>
    /// Reads what identify.js handed over: the HTTP status (-1 offline, 0 when the photo isn't on this
    /// device) and the text of the answer, which is only kept for a 200 and for the API's 429.
    /// </summary>
    /// <param name="ownKey">True when Pl@ntNet was asked directly, false when it went through Stikling's API.</param>
    public static IdentificationOutcome From(int status, string? body, bool ownKey)
    {
        switch (status)
        {
            case -1:
                return Failure(IdentificationProblem.Offline);
            case 0:
                return Failure(IdentificationProblem.Missing);
            case 200:
                var parsed = ownKey ? PlantNetResults.Parse(body) : ParseResponse(body);
                return parsed is null ? Failure(IdentificationProblem.Failed) : new(parsed, IdentificationProblem.None);
            case 404 when ownKey:
                // Pl@ntNet's way of saying it found no species
                return new([], IdentificationProblem.None);
            case 503 when !ownKey:
                return Failure(IdentificationProblem.NotAvailable);
            case 401 or 403 when ownKey:
                return Failure(IdentificationProblem.KeyRejected);
            case 429 when ownKey:
                return Failure(IdentificationProblem.KeyLimit);
            case 429:
                return Failure(Reason(body) switch
                {
                    "person" => IdentificationProblem.PersonLimit,
                    "total" => IdentificationProblem.TotalLimit,
                    _ => IdentificationProblem.Failed,
                });
            default:
                return Failure(IdentificationProblem.Failed);
        }
    }

    private static IReadOnlyList<IdentificationSuggestion>? ParseResponse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            return JsonSerializer.Deserialize<IdentificationResponse>(body, JsonSerializerOptions.Web)?.Suggestions;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Reason(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String
                ? reason.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
