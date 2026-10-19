using System.Text.RegularExpressions;
using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>Another plant or propagation with the same collection number.</summary>
public sealed record CodeClash(Guid Id, string Name, bool IsPropagation);

/// <summary>A plant the number-all plan would give a number.</summary>
public sealed record NumberedPlant(Plant Plant, string Code);

/// <summary>A propagation the number-all plan would give a letter.</summary>
public sealed record LetteredCutting(Propagation Propagation, string Code);

/// <summary>What numbering the collection would do, worked out without saving anything.</summary>
public sealed record NumberAllPlan(IReadOnlyList<NumberedPlant> Plants, IReadOnlyList<LetteredCutting> Cuttings)
{
    public bool IsEmpty => Plants.Count == 0 && Cuttings.Count == 0;

    /// <summary>"Numbers 34 plants, 118 to 151, and gives 6 going cuttings a letter."</summary>
    public string Summary
    {
        get
        {
            var plants = Plants.Count switch
            {
                0 => null,
                1 => $"Numbers 1 plant, {Plants[0].Code}",
                var n => $"Numbers {n} plants, {Plants[0].Code} to {Plants[^1].Code}"
            };
            var cuttings = Cuttings.Count switch
            {
                0 => null,
                1 => "1 going cutting a letter",
                var n => $"{n} going cuttings a letter"
            };
            return (plants, cuttings) switch
            {
                (null, null) => "Every plant here has a number.",
                ({ } p, null) => $"{p}.",
                (null, { } c) => $"Gives {c}.",
                ({ } p, { } c) => $"{p}, and gives {c}."
            };
        }
    }
}

/// <summary>
/// The short number people write on a pot, like 117, and the letters that follow it for cuttings
/// (117a, 117b). Optional, and never forced to be unique, since a merge can make two the same.
/// </summary>
public static partial class CollectionNumbers
{
    /// <summary>
    /// Digits, then letters with optional digits, then any number of groups of a hyphen, letters and
    /// optional digits: 117, 117c, 117c2, 117c-a. Every group after the first starts with a hyphen,
    /// so no two groups can match the same text and the check never backtracks.
    /// </summary>
    [GeneratedRegex(@"^\d{1,6}(?:[a-z]+\d*)?(?:-[a-z]+\d*)*$")]
    private static partial Regex Shape();

    private const int MaxLength = 24;

    /// <summary>What the form says when a code isn't of a shape the app writes.</summary>
    public const string NotValidText = "Use a number, like 117.";

    /// <summary>Trims, lower-cases and removes a leading #. Null when nothing is left.</summary>
    public static string? Normalize(string? code)
    {
        var text = (code ?? "").Trim().ToLowerInvariant();
        if (text.StartsWith('#'))
            text = text[1..].Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>Whether a normalized code has a shape the app writes: 117, 117a, 117c2, 117c-a.</summary>
    public static bool IsValid(string code) => code.Length <= MaxLength && Shape().IsMatch(code);

    private static int? Leading(string? code)
    {
        if (code is null)
            return null;
        var digits = 0;
        while (digits < code.Length && char.IsAsciiDigit(code[digits]))
            digits++;
        return digits is > 0 and <= 9 ? int.Parse(code[..digits]) : null;
    }

    /// <summary>
    /// The number to suggest for the next plant: one more than the highest leading number among all
    /// plants that aren't deleted, whatever their status, so an old label is never reused.
    /// </summary>
    public static int NextFree(IEnumerable<Plant> plants) =>
        plants.Where(p => !p.IsDeleted).Select(p => Leading(Normalize(p.CollectionNumber))).Max() + 1 ?? 1;

    /// <summary>
    /// The code for a new cutting from the parent: its code and the first letters (a to z, then aa,
    /// ab and so on) that no plant or propagation has with that code. Null when the parent has no number.
    /// </summary>
    public static string? NextLetter(Plant parent, IEnumerable<Propagation> propagations, IEnumerable<Plant> plants)
    {
        if (Normalize(parent.CollectionNumber) is not { } code)
            return null;
        var used = propagations.Where(p => !p.IsDeleted).Select(p => p.CollectionNumber)
            .Concat(plants.Where(p => !p.IsDeleted).Select(p => p.CollectionNumber))
            .Select(Normalize)
            .OfType<string>()
            .ToHashSet();
        return FirstFree(code, used);
    }

    private static string FirstFree(string parentCode, HashSet<string> used)
    {
        // Once a code has a letter in it, a hyphen keeps the next letters apart from it
        var prefix = parentCode.Any(char.IsAsciiLetter) ? parentCode + "-" : parentCode;
        for (var i = 1; ; i++)
        {
            var candidate = prefix + Letters(i);
            if (!used.Contains(candidate))
                return candidate;
        }
    }

    // 1 is a, 26 is z, 27 is aa, 28 is ab
    private static string Letters(int n)
    {
        var text = "";
        while (n > 0)
        {
            n--;
            text = (char)('a' + n % 26) + text;
            n /= 26;
        }
        return text;
    }

    /// <summary>
    /// The codes for plants potted up from a propagation, one per plant. A single plant from the
    /// first pot-up keeps the propagation's code. Otherwise each gets a digit counting on from what
    /// was potted up before. Ask before <see cref="Propagation.RecordPottedUp"/> changes the count.
    /// Null codes when the propagation has no number.
    /// </summary>
    public static IReadOnlyList<string?> PottedUpCodes(Propagation propagation, int count)
    {
        if (Normalize(propagation.CollectionNumber) is not { } code)
            return [.. Enumerable.Repeat((string?)null, count)];
        if (propagation.PottedUpCount == 0 && count == 1)
            return [code];
        return [.. Enumerable.Range(1, count).Select(i => (string?)(code + (propagation.PottedUpCount + i)))];
    }

    /// <summary>
    /// The other plants and propagations that aren't deleted and have this code. A propagation and the
    /// plant it was potted up into share a code on purpose, so they don't count against each other.
    /// </summary>
    public static IReadOnlyList<CodeClash> Clashes(
        string? code, Guid selfId, IEnumerable<Plant> plants, IEnumerable<Propagation> propagations)
    {
        if (Normalize(code) is not { } wanted)
            return [];
        var allPlants = plants.Where(p => !p.IsDeleted).ToList();
        var allPropagations = propagations.Where(p => !p.IsDeleted).ToList();
        var selfFrom = allPlants.FirstOrDefault(p => p.Id == selfId)?.FromPropagationId;

        bool Same(string? other) => Normalize(other) == wanted;
        return
        [
            .. allPlants
                .Where(p => p.Id != selfId && Same(p.CollectionNumber) && p.FromPropagationId != selfId)
                .Select(p => new CodeClash(p.Id, p.DisplayName, false)),
            .. allPropagations
                .Where(p => p.Id != selfId && Same(p.CollectionNumber) && p.Id != selfFrom)
                .Select(p => new CodeClash(p.Id, p.DisplayName, true))
        ];
    }

    /// <summary>"Big Monstera also has #117." or "Big Monstera, Small Monstera also have #117."</summary>
    public static string ClashNote(string code, IReadOnlyList<CodeClash> clashes) =>
        $"{string.Join(", ", clashes.Select(c => c.Name))} also {(clashes.Count == 1 ? "has" : "have")} #{Normalize(code)}.";

    /// <summary>
    /// Whether a search word is a number or the start of one. The word may begin with #. Only digits,
    /// letters and hyphens count, so a name typed in doesn't match by chance.
    /// </summary>
    public static bool MatchesSearch(string? code, string? search)
    {
        if (Normalize(code) is not { } have || Normalize(search) is not { } word)
            return false;
        return word.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') && have.StartsWith(word, StringComparison.Ordinal);
    }

    /// <summary>For sorting: the leading number, then the whole code as text. Null for no code, which sorts last.</summary>
    public static IComparable? SortKey(string? code)
    {
        if (Normalize(code) is not { } text)
            return null;
        return new SortCode(Leading(text) ?? int.MaxValue, text);
    }

    private sealed record SortCode(int Number, string Text) : IComparable
    {
        public int CompareTo(object? other) =>
            other is SortCode o
                ? Number != o.Number ? Number.CompareTo(o.Number) : string.CompareOrdinal(Text, o.Text)
                : 1;
    }

    /// <summary>
    /// Who would get a number or a letter if the collection were numbered: the plants still here
    /// without a number, oldest first, counting on from the highest number ever used, and the
    /// propagations still going without one whose plant has a number. Saves nothing.
    /// </summary>
    public static NumberAllPlan Plan(IEnumerable<Plant> plants, IEnumerable<Propagation> propagations)
    {
        var allPlants = plants.Where(p => !p.IsDeleted).ToList();
        var allPropagations = propagations.Where(p => !p.IsDeleted).ToList();

        var used = allPlants.Select(p => Normalize(p.CollectionNumber))
            .Concat(allPropagations.Select(p => Normalize(p.CollectionNumber)))
            .OfType<string>()
            .ToHashSet();
        var codeOf = allPlants.Where(p => Normalize(p.CollectionNumber) is not null)
            .ToDictionary(p => p.Id, p => Normalize(p.CollectionNumber)!);

        var next = NextFree(allPlants);
        var numbered = new List<NumberedPlant>();
        foreach (var plant in allPlants
            .Where(p => p.InCollection && Normalize(p.CollectionNumber) is null)
            .OrderBy(p => p.AcquiredOn?.Start ?? DateOnly.FromDateTime(p.CreatedAt.DateTime))
            .ThenBy(p => p.CreatedAt)
            .ThenBy(p => p.Id))
        {
            var code = next++.ToString();
            numbered.Add(new NumberedPlant(plant, code));
            codeOf[plant.Id] = code;
            used.Add(code);
        }

        var lettered = new List<LetteredCutting>();
        foreach (var cutting in allPropagations
            .Where(p => p.IsActive && Normalize(p.CollectionNumber) is null && p.ParentPlantId is not null)
            .OrderBy(p => p.StartedOn)
            .ThenBy(p => p.CreatedAt)
            .ThenBy(p => p.Id))
        {
            if (!codeOf.TryGetValue(cutting.ParentPlantId!.Value, out var parentCode))
                continue;
            var code = FirstFree(parentCode, used);
            lettered.Add(new LetteredCutting(cutting, code));
            used.Add(code);
        }

        return new NumberAllPlan(numbered, lettered);
    }
}
