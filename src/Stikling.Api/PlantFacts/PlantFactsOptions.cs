namespace Stikling.Api.PlantFacts;

/// <summary>
/// The <c>PlantFacts</c> section of the configuration. Without a Trefle token the card is Wikipedia only.
/// </summary>
public class PlantFactsOptions
{
    public const string Section = "PlantFacts";

    /// <summary>Trefle's token, a secret. Locally it is set with <c>dotnet user-secrets</c>, hosted from the Key Vault.</summary>
    public string? TrefleToken { get; set; }

    public bool TrefleOn => !string.IsNullOrWhiteSpace(TrefleToken);
}
