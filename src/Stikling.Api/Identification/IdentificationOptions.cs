namespace Stikling.Api.Identification;

/// <summary>
/// The Pl@ntNet key the API identifies plants with, and how many identifications it allows. Without
/// the key <c>POST /identify</c> answers 503. Pl@ntNet's free plan allows 500 a day, which is what
/// <see cref="DailyLimitTotal"/> keeps to.
/// </summary>
public class IdentificationOptions
{
    public const string Section = "Identification";

    public string? PlantNetApiKey { get; set; }

    public int DailyLimitPerPerson { get; set; } = 10;

    public int DailyLimitTotal { get; set; } = 500;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PlantNetApiKey);
}
