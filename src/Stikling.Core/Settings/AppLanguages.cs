namespace Stikling.Core.Settings;

/// <summary>
/// One language the app can be shown in.
/// </summary>
/// <param name="Code">The language code, like "da". It is also the culture in the name of the translation files.</param>
/// <param name="NativeName">The name in its own language, like "Dansk", so it can be found without reading the current one.</param>
/// <param name="Ready">Whether enough of the app is translated to offer it to everyone. See TranslationFileTests.</param>
/// <param name="CultureName">The culture that sets dates and numbers, when it isn't the code itself.</param>
public sealed record AppLanguage(string Code, string NativeName, bool Ready, string? CultureName = null)
{
    public string Culture => CultureName ?? Code;
}

/// <summary>
/// The languages the app has, and which one to use. A language added by translators gets a line here
/// once its translation files are in. It is offered to everyone when <see cref="AppLanguage.Ready"/> is set,
/// and only in development builds before that.
/// </summary>
public static class AppLanguages
{
    public const string English = "en";

    public static IReadOnlyList<AppLanguage> All { get; } =
    [
        // English keeps en-GB, so dates and numbers read the way they always have
        new("en", "English", Ready: true, CultureName: "en-GB"),
        new("da", "Dansk", Ready: true, CultureName: "da-DK")
    ];

    /// <summary>The languages that can be used: the ready ones, and the ones still being translated when asked for.</summary>
    public static IReadOnlyList<AppLanguage> Offered(bool includeInProgress) =>
        [.. All.Where(l => l.Ready || includeInProgress)];

    /// <summary>
    /// The language to use: the one chosen in Settings if it is offered, else the first language the
    /// browser lists that is (so "da-DK" gives "da"), else English.
    /// </summary>
    public static string Resolve(string? chosen, IReadOnlyList<string> browserLanguages, bool includeInProgress)
    {
        var offered = Offered(includeInProgress);

        if (Find(offered, chosen) is { } picked)
            return picked.Code;

        foreach (var language in browserLanguages)
        {
            var baseCode = (language ?? "").Split('-', '_')[0];
            if (Find(offered, baseCode) is { } match)
                return match.Code;
        }

        return English;
    }

    /// <summary>The culture name for a language code, English for one that isn't known.</summary>
    public static string CultureOf(string? code) =>
        (Find(All, code) ?? All[0]).Culture;

    private static AppLanguage? Find(IReadOnlyList<AppLanguage> languages, string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : languages.FirstOrDefault(l => string.Equals(l.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
}
