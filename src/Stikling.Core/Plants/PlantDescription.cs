using Stikling.Core.Models;
using Stikling.Core.Problems;
using Strings = Stikling.Core.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Plants;

/// <summary>The parts of the text, in the order they are written and listed. Never stored.</summary>
public enum DescribePart { Name, Room, Pot, Light, Likes, Care, Lately, Problems, CameFrom, Price, Tags }

/// <summary>One part of the text and its lines, already worded.</summary>
public sealed record DescribeSection(DescribePart Part, IReadOnlyList<string> Lines);

/// <summary>
/// "Describe my plant": a short plain text about a plant to paste into a forum post or send to a friend.
/// The page words each part, and this puts the chosen ones together. Nothing here is stored.
/// </summary>
public static class PlantDescription
{
    /// <summary>Room, CameFrom, Price and Tags start unticked, everything else ticked.</summary>
    public static bool OnAtFirst(DescribePart part) =>
        part is not (DescribePart.Room or DescribePart.CameFrom or DescribePart.Price or DescribePart.Tags);

    /// <summary>
    /// The chosen sections that have lines, in <see cref="DescribePart"/> order, one line per line,
    /// joined with a line break and no trailing one. Empty when none is left.
    /// </summary>
    public static string Write(IEnumerable<DescribeSection> sections, IReadOnlySet<DescribePart> chosen) =>
        string.Join("\n", sections
            .Where(s => chosen.Contains(s.Part) && s.Lines.Count > 0)
            .OrderBy(s => s.Part)
            .SelectMany(s => s.Lines));

    /// <summary>The lines of the Problems part for the open problems, oldest problem first.</summary>
    public static IReadOnlyList<string> ProblemLines(
        IEnumerable<Problem> open, ILookup<Guid, ProblemUpdate> updates, DateOnly today)
    {
        var lines = new List<string>();
        foreach (var problem in open.Where(p => !p.IsDeleted).OrderBy(p => p.StartedOn))
        {
            var header = F(Strings.Pests.DescribeProblem, ProblemText.Title(problem), ProblemText.ShortDay(problem.StartedOn),
                ProblemText.Duration(ProblemText.DaysLasted(problem, today)));
            if (!string.IsNullOrWhiteSpace(problem.Note))
                header += ". " + problem.Note.Trim();
            lines.Add(header);

            foreach (var update in updates[problem.Id].Where(u => !u.IsDeleted).OrderBy(u => u.OccurredOn))
            {
                var parts = new List<string>();
                if (update.Trend is { } trend)
                    parts.Add(ProblemText.Trend(trend));
                if (update.KnownDid.Count > 0)
                    parts.Add(ProblemText.Actions(update.KnownDid));
                if (!string.IsNullOrWhiteSpace(update.Note))
                    parts.Add(update.Note.Trim());
                if (parts.Count > 0)
                    lines.Add("  " + F(Strings.Pests.DescribeProblemUpdate, ProblemText.ShortDay(update.OccurredOn), string.Join(". ", parts)));
            }
        }

        return lines;
    }
}
