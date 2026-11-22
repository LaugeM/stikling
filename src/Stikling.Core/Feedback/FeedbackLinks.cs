namespace Stikling.Core.Feedback;

/// <summary>The three links on the Write to me sheet. The app sends nothing itself: each one opens a page or a mail draft the person sees first.</summary>
/// <param name="Bug">A GitHub issue form for something broken.</param>
/// <param name="Idea">A GitHub issue form for an idea.</param>
/// <param name="Mail">An email draft.</param>
public sealed record FeedbackLinks(string Bug, string Idea, string Mail)
{
    private const string NewIssue = "https://github.com/LaugeM/stikling/issues/new";

    /// <summary>The address the sheet writes to, the same as on the privacy and deletion pages.</summary>
    public const string Address = "support@stikling.app";

    // The text that goes into the drafts is for the person reading them, so it stays English like the issue forms.

    /// <summary>
    /// Builds the links. Only the version, the browser, the language and the question are put in, never anything from the collection.
    /// </summary>
    /// <param name="version">The app version, like "2026.11.22 (abc1234)".</param>
    /// <param name="browser">The browser and system, like "Firefox 133 on Android".</param>
    /// <param name="language">The two-letter code of the app's language.</param>
    /// <param name="question">The Help question the person came from, if any.</param>
    public static FeedbackLinks Create(string version, string browser, string language, string? question = null)
    {
        var line = $"{browser} · Stikling {version} · {language}";
        question = question?.Trim();
        var hasQuestion = !string.IsNullOrEmpty(question);
        var about = hasQuestion ? $"About \"{question}\": " : "";

        var bug = $"{NewIssue}?template=bug_report.yml"
            + Title(question)
            + $"&what={Escape(about)}&device={Escape(line)}";
        var idea = $"{NewIssue}?template=feature_request.yml"
            + Title(question)
            + $"&what={Escape(about + "\n\n" + line)}";
        var mail = $"mailto:{Address}?subject={Escape(hasQuestion ? question! : "Stikling")}&body={Escape("\n\n" + line)}";
        return new FeedbackLinks(bug, idea, mail);
    }

    private static string Title(string? question) =>
        string.IsNullOrEmpty(question) ? "" : $"&title={Escape(question)}";

    private static string Escape(string text) => Uri.EscapeDataString(text);
}
