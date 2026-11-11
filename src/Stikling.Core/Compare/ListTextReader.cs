using System.Globalization;
using System.Text.RegularExpressions;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Plants;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Compare;

/// <summary>What a pasted text turned out to be.</summary>
public enum ListTextKind
{
    /// <summary>Nothing but blank lines.</summary>
    Empty,

    /// <summary>A list of plants: Stikling's swap list or plant list.</summary>
    Plants,

    /// <summary>Stikling's shared wishlist.</summary>
    Wishlist,

    /// <summary>A list typed by hand. Every line is in both lists, and the app asks which one it is.</summary>
    Plain
}

/// <param name="Plants">The plants in the text. For a plain list, every line.</param>
/// <param name="Wishes">The wishes in the text. For a plain list, every line.</param>
/// <param name="UnreadLines">Lines that couldn't be read as a name, so the person can see what was skipped.</param>
public sealed record ListTextResult(
    ListTextKind Kind,
    IReadOnlyList<FriendPlant> Plants,
    IReadOnlyList<FriendWish> Wishes,
    IReadOnlyList<string> UnreadLines);

/// <summary>
/// Reads a list a friend sent: the texts Stikling writes for a swap list, a plant list and a wishlist,
/// in English or Danish, or a list typed by hand. The headings are compared with the words in the
/// resource files in both languages, so the reader follows the writers.
/// </summary>
public static partial class ListTextReader
{
    private const int LongestLine = 120;

    private static readonly CultureInfo[] Cultures = [CultureInfo.InvariantCulture, new CultureInfo("da")];

    private static string[] Words(string key) =>
        [.. Cultures.Select(c => Strings.Shared.ResourceManager.GetString(key, c)).OfType<string>().Select(w => w.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)];

    private static bool IsOneOf(string line, string[] words) =>
        words.Any(w => string.Equals(line.Trim(), w, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"^#\S+\s+")]
    private static partial Regex NumberRegex();

    [GeneratedRegex("\\s*[\"\u201C\u201D][^\"\u201C\u201D]*[\"\u201C\u201D]\\s*$")]
    private static partial Regex NicknameRegex();

    [GeneratedRegex(@"\s*\([^)]*\)\s*$")]
    private static partial Regex EverydayRegex();

    [GeneratedRegex("\\s+\u00D7\\s*\\d+\\s*$")]
    private static partial Regex CountRegex();

    [GeneratedRegex("^(?<name>.+?)\\s+\u00D7\\s*(?<count>\\d+)\\s*,\\s*(?<stage>.+)$")]
    private static partial Regex CuttingLineRegex();

    [GeneratedRegex(@"^(?<before>[^']*?)\s*'(?<cultivar>[^']+)'$")]
    private static partial Regex QuotedCultivarRegex();

    public static ListTextResult Read(string? text, PlantDictionary dictionary)
    {
        var lines = (text ?? "").ReplaceLineEndings("\n").Split('\n').Select(l => l.TrimEnd()).ToList();
        if (lines.All(l => l.Trim().Length == 0))
            return new ListTextResult(ListTextKind.Empty, [], [], []);

        if (lines.Any(l => IsOneOf(l, Words("WishlistTextHeading"))))
            return ReadWishlist(lines, dictionary);
        if (lines.Any(l => IsOneOf(l, Words("SwapListHeading"))))
            return ReadSwap(lines, dictionary);
        if (lines.Any(l => IsOneOf(l, Words("KeptInStikling"))))
            return ReadPlantList(lines, dictionary);
        return ReadPlain(text!, dictionary);
    }

    private static bool Unreadable(string line) => !line.Any(char.IsLetter) || line.Length > LongestLine;

    // "- Monstera deliciosa (Swiss cheese plant)", with notes and prices on lines indented under it
    private static ListTextResult ReadWishlist(List<string> lines, PlantDictionary dictionary)
    {
        var wishes = new List<FriendWish>();
        var unread = new List<string>();
        foreach (var line in lines)
        {
            if (!line.StartsWith("- ", StringComparison.Ordinal))
                continue;
            var item = line[2..].Trim();
            if (Unreadable(item))
                unread.Add(item);
            else
                wishes.Add(WishOf(item, dictionary));
        }
        return new ListTextResult(ListTextKind.Wishlist, [], wishes, unread);
    }

    // The swap list: a heading for cuttings, cuttings on request and plants, and a line for each
    private static ListTextResult ReadSwap(List<string> lines, PlantDictionary dictionary)
    {
        var stages = new (PropagationStage Stage, string[] Words)[]
        {
            (PropagationStage.Rooted, Words("SwapStageRooted")),
            (PropagationStage.Rooting, Words("SwapStageRooting")),
            (PropagationStage.Started, Words("SwapStageStarted")),
        };
        var cuttings = Words("SwapListCuttings");
        var onRequest = Words("SwapListOnRequest");
        var plantsHeading = Words("SwapListPlants");
        var footer = Words("KeptInStikling");
        var heading = Words("SwapListHeading");

        FriendSpare? section = null;
        var plants = new List<FriendPlant>();
        var unread = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || IsOneOf(line, heading) || IsOneOf(line, footer))
                continue;
            if (IsOneOf(line, cuttings)) { section = FriendSpare.Cuttings; continue; }
            if (IsOneOf(line, onRequest)) { section = FriendSpare.CuttingsToSpare; continue; }
            if (IsOneOf(line, plantsHeading)) { section = FriendSpare.UpForSwap; continue; }
            if (section is not { } spare)
                continue;

            if (Unreadable(line))
            {
                unread.Add(line);
                continue;
            }

            if (spare == FriendSpare.Cuttings)
            {
                var count = 1;
                PropagationStage? stage = null;
                var name = line;
                if (CuttingLineRegex().Match(line) is { Success: true } m)
                {
                    name = m.Groups["name"].Value;
                    count = int.TryParse(m.Groups["count"].Value, out var n) && n > 0 ? n : 1;
                    var said = m.Groups["stage"].Value.Trim();
                    stage = stages.Where(s => IsOneOf(said, s.Words)).Select(s => (PropagationStage?)s.Stage).FirstOrDefault();
                }
                plants.Add(PlantOf(name, SubjectType.Propagation, FriendSpare.Cuttings, count, stage, dictionary));
            }
            else
                plants.Add(PlantOf(line, SubjectType.Plant, spare, 0, null, dictionary));
        }
        return new ListTextResult(ListTextKind.Plants, plants, [], unread);
    }

    // The plant list: a line for each plant, with a mark after the name when it is up for swap
    private static ListTextResult ReadPlantList(List<string> lines, PlantDictionary dictionary)
    {
        var footer = Words("KeptInStikling");
        var marks = new (FriendSpare Spare, string[] Words)[]
        {
            (FriendSpare.CuttingsToSpare, Words("SwapMarkCuttings")),
            (FriendSpare.UpForSwap, Words("SwapMarkUpForSwap")),
        };

        var plants = new List<FriendPlant>();
        var unread = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || IsOneOf(line, footer))
                continue;
            if (Unreadable(line))
            {
                unread.Add(line);
                continue;
            }

            var spare = FriendSpare.None;
            foreach (var (mark, words) in marks)
            {
                var hit = words.FirstOrDefault(w => line.EndsWith(", " + w, StringComparison.OrdinalIgnoreCase));
                if (hit is null)
                    continue;
                spare = mark;
                line = line[..^(hit.Length + 2)].TrimEnd();
                break;
            }
            plants.Add(PlantOf(line, SubjectType.Plant, spare, 0, null, dictionary));
        }
        return new ListTextResult(ListTextKind.Plants, plants, [], unread);
    }

    // Typed by hand: one name on each line, and commas and semicolons separate names too
    private static ListTextResult ReadPlain(string text, PlantDictionary dictionary)
    {
        var plants = new List<FriendPlant>();
        var wishes = new List<FriendWish>();
        var unread = new List<string>();
        foreach (var item in text.Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Unreadable(item))
            {
                unread.Add(item);
                continue;
            }
            plants.Add(PlantOf(item, SubjectType.Plant, FriendSpare.None, 0, null, dictionary));
            wishes.Add(WishOf(item, dictionary));
        }
        return new ListTextResult(ListTextKind.Plain, plants, wishes, unread);
    }

    private static FriendPlant PlantOf(string item, SubjectType type, FriendSpare spare, int count, PropagationStage? stage, PlantDictionary dictionary)
    {
        var (name, genus, species, cultivar) = ReadName(item, dictionary);
        return new FriendPlant(null, type, name, genus, species, cultivar, spare, count, stage, null);
    }

    private static FriendWish WishOf(string item, PlantDictionary dictionary)
    {
        var (name, genus, species, cultivar) = ReadName(item, dictionary);
        return new FriendWish(name, genus, species, cultivar);
    }

    /// <summary>
    /// One name from a line. A "#117 " number and a " ×2" count are dropped, and a trailing nickname in
    /// quotes and everyday name in brackets are left out of what is matched but kept in the name.
    /// </summary>
    private static (string Name, string? Genus, string? Species, string? Cultivar) ReadName(string item, PlantDictionary dictionary)
    {
        var name = NumberRegex().Replace(CountRegex().Replace(item.Trim(), ""), "").Trim();
        var matched = EverydayRegex().Replace(NicknameRegex().Replace(name, ""), "").Trim();
        if (matched.Length == 0)
            return (name, null, null, null);

        // "Genus species 'Cultivar'" is taken as it stands, since the dictionary may not know the cultivar
        if (QuotedCultivarRegex().Match(matched) is { Success: true } q)
        {
            var words = q.Groups["before"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words is { Length: >= 1 and <= 4 } && char.IsLetter(words[0][0]))
            {
                var rest = words.Skip(1).ToList();
                // "cf." and "aff." only say how sure the name is, and "sp." that there is no species
                var species = rest is ["sp.", ..] ? null : string.Join(' ', rest.Where(w => w is not ("cf." or "aff.")));
                return (name, PlantNames.Genus(words[0]), PlantNames.Species(species), PlantNames.Cultivar(q.Groups["cultivar"].Value));
            }
        }

        var plant = QuickAdd.ReadName(matched, dictionary);
        return plant.Genus is null
            ? (name, null, null, null)
            : (name, PlantNames.Genus(plant.Genus), PlantNames.Species(plant.Species), PlantNames.Cultivar(plant.Cultivar));
    }
}
