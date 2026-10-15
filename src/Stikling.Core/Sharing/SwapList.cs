using System.Text;
using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Sharing;

/// <summary>One line of the swap list: a name, how many, and for a cutting its stage.</summary>
public sealed record SwapLine(string Name, int Count, PropagationStage? Stage = null, Guid? SubjectId = null);

/// <summary>What is available for swap, in the three groups the page and the shared text use.</summary>
/// <param name="Cuttings">Cuttings that exist, from propagations still going.</param>
/// <param name="OnRequest">Plants that can spare cuttings, which aren't started yet.</param>
/// <param name="Plants">Plants that are up for swap themselves.</param>
public sealed record SwapList(IReadOnlyList<SwapLine> Cuttings, IReadOnlyList<SwapLine> OnRequest, IReadOnlyList<SwapLine> Plants)
{
    public bool IsEmpty => Cuttings.Count == 0 && OnRequest.Count == 0 && Plants.Count == 0;
}

/// <summary>Works out the swap list from the records, and writes lists of plants as text for sharing.</summary>
public static class SwapLists
{
    public const string Footer = "Kept in Stikling, stikling.app";

    /// <summary>
    /// Only plants still here and propagations still going with something left count, so marks on
    /// plants that have left are ignored rather than cleared.
    /// </summary>
    public static SwapList Build(IEnumerable<Plant> plants, IEnumerable<Propagation> propagations)
    {
        var byName = StringComparer.CurrentCultureIgnoreCase;
        var cuttings = propagations
            .Where(p => p.SpareAvailable > 0)
            .OrderBy(p => p.DisplayName, byName)
            .Select(p => new SwapLine(p.DisplayName, p.SpareAvailable, p.Stage, p.Id))
            .ToList();
        var offered = plants.Where(p => p.IsOffered).OrderBy(p => p.DisplayName, byName).ToList();

        return new SwapList(cuttings,
            [.. offered.Where(p => p.Spare == SpareMark.CuttingsToSpare).Select(p => new SwapLine(p.DisplayName, 1, null, p.Id))],
            [.. offered.Where(p => p.Spare == SpareMark.UpForSwap).Select(p => new SwapLine(p.DisplayName, 1, null, p.Id))]);
    }

    /// <summary>"Cuttings to spare" or "Up for swap".</summary>
    public static string MarkText(SpareMark mark) => mark == SpareMark.CuttingsToSpare ? "Cuttings to spare" : "Up for swap";

    /// <summary>The name shared for a plant: botanical with the everyday name, or the nickname when it has no botanical name.</summary>
    public static string ShareName(string? nickname, string? genus, string? species, string? cultivar, string? everyday, bool nicknames,
        NameQualifier? qualifier = null, string? note = null)
    {
        var botanical = PlantNames.Botanical(genus, species, cultivar, qualifier, note);
        var nick = string.IsNullOrWhiteSpace(nickname) ? null : nickname.Trim();
        if (botanical is null)
            return nick ?? "Unnamed plant";

        var name = string.IsNullOrWhiteSpace(everyday) ? botanical : $"{botanical} ({everyday.Trim()})";
        return nicknames && nick is not null ? $"{name} \"{nick}\"" : name;
    }

    /// <summary>"#117 Big Monstera" for a plant with a number, else the name as it is.</summary>
    public static string WithNumber(string? number, string name) => number is null ? name : $"#{number} {name}";

    internal static string StageText(PropagationStage stage) => stage switch
    {
        PropagationStage.Rooted => "rooted",
        PropagationStage.Rooting => "rooting",
        _ => "just started"
    };

    /// <summary>
    /// The swap list as text: cuttings, cuttings on request and plants, with counts and stages.
    /// The same name and stage are added up into one line.
    /// </summary>
    public static string WriteSwap(
        IEnumerable<Plant> plants,
        IEnumerable<Propagation> propagations,
        Func<string?, string?, string?, string?> everyday,
        bool nicknames,
        bool footer)
    {
        string NameOf(string? nick, string? g, string? s, string? c, NameQualifier? q, string? n) =>
            ShareName(nick, g, s, c, everyday(g, PlantNames.SpeciesToLookUp(s, q), c), nicknames, q, n);
        var byName = StringComparer.CurrentCultureIgnoreCase;

        var cuttings = propagations.Where(p => p.SpareAvailable > 0)
            .GroupBy(p => (Name: NameOf(p.Nickname, p.Genus, p.Species, p.Cultivar, p.NameQualifier, p.NameNote), p.Stage))
            .OrderBy(g => g.Key.Name, byName).ThenBy(g => g.Key.Stage)
            .Select(g => $"{g.Key.Name} ×{g.Sum(p => p.SpareAvailable)}, {StageText(g.Key.Stage)}")
            .ToList();

        var offered = plants.Where(p => p.IsOffered).ToList();
        // A plant with a number is listed on its own, with the number before its name
        List<string> Counted(SpareMark mark) => [.. offered.Where(p => p.Spare == mark)
            .GroupBy(p => (Number: CollectionNumbers.Normalize(p.CollectionNumber), Name: NameOf(p.Nickname, p.Genus, p.Species, p.Cultivar, p.NameQualifier, p.NameNote)))
            .OrderBy(g => g.Key.Name, byName).ThenBy(g => g.Key.Number, StringComparer.Ordinal)
            .Select(g => WithNumber(g.Key.Number, g.Count() > 1 ? $"{g.Key.Name} ×{g.Count()}" : g.Key.Name))];

        var text = new StringBuilder();
        text.AppendLine("Available for swap");
        foreach (var (heading, lines) in new[]
        {
            ("Cuttings", cuttings),
            ("Cuttings on request", Counted(SpareMark.CuttingsToSpare)),
            ("Plants", Counted(SpareMark.UpForSwap))
        })
        {
            if (lines.Count == 0)
                continue;
            text.AppendLine();
            text.AppendLine(heading);
            foreach (var line in lines)
                text.AppendLine(line);
        }

        if (footer)
        {
            text.AppendLine();
            text.AppendLine(Footer);
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// The plants shown as text: the botanical name with the everyday name, the same plants counted
    /// once, sorted by name. A plant with no botanical name uses its nickname. Never rooms, prices,
    /// where it came from or tags.
    /// </summary>
    public static string WritePlants(
        IEnumerable<Plant> plants,
        Func<string?, string?, string?, string?> everyday,
        bool nicknames,
        bool footer)
    {
        var lines = plants
            .Where(p => !p.IsDeleted)
            .GroupBy(p => (Number: CollectionNumbers.Normalize(p.CollectionNumber),
                           Name: ShareName(p.Nickname, p.Genus, p.Species, p.Cultivar, everyday(p.Genus, PlantNames.SpeciesToLookUp(p.Species, p.NameQualifier), p.Cultivar), nicknames, p.NameQualifier, p.NameNote),
                           Mark: p.IsOffered ? p.Spare : null))
            .OrderBy(g => g.Key.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(g => g.Key.Number, StringComparer.Ordinal).ThenBy(g => g.Key.Mark)
            .Select(g =>
            {
                var line = WithNumber(g.Key.Number, g.Key.Name);
                if (g.Count() > 1)
                    line += $" ×{g.Count()}";
                return g.Key.Mark is { } mark ? $"{line}, {MarkText(mark).ToLowerInvariant()}" : line;
            })
            .ToList();

        var text = new StringBuilder();
        foreach (var line in lines)
            text.AppendLine(line);
        if (footer)
        {
            if (lines.Count > 0)
                text.AppendLine();
            text.AppendLine(Footer);
        }

        return text.ToString().TrimEnd();
    }
}
