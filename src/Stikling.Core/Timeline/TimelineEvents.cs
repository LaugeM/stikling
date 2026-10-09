using System.Text.RegularExpressions;
using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

/// <summary>Tells what a change entry on a propagation was about.</summary>
public static partial class TimelineEvents
{
    /// <summary>
    /// The entry's event. Entries from before it was kept have none, so those are recognised from
    /// the text the app wrote ("Potted up 2: ...", "1 failed: rotted"). Nothing stored is changed.
    /// </summary>
    public static TimelineEvent? Of(TimelineEntry entry)
    {
        if (entry.Event is { } known)
            return known;
        if (entry.Kind != TimelineKind.Change || entry.SubjectType != SubjectType.Propagation || entry.Text is not { } text)
            return null;

        // A finished propagation adds a "Finished: ..." line after the first
        var first = text.Split('\n', 2)[0];
        if (PottedUpText().IsMatch(first))
            return TimelineEvent.PottedUp;
        if (FailedText().IsMatch(first))
            return TimelineEvent.Failed;
        return null;
    }

    [GeneratedRegex(@"^Potted up \d+:")]
    private static partial Regex PottedUpText();

    [GeneratedRegex(@"^\d+ failed(: .+)?$")]
    private static partial Regex FailedText();
}
