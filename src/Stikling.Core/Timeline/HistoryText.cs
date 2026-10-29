using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Stikling.Core.Models;
using Stikling.Core.Problems;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Timeline;

/// <summary>
/// The automatic lines in a plant's or propagation's history ("Moved from Living room to Bedroom"). A line is
/// written as a <see cref="HistoryLine"/>: the name of a template and the values that go in it. The templates
/// are in <c>Resources/History.resx</c> and listed in the catalog below, so every device shows a line in its
/// own language. The entry's <see cref="TimelineEntry.Text"/> still gets the same lines in English, for the code
/// that reads them, for older versions and as the fallback for a line this version doesn't know.
///
/// A key is never renamed once it has been saved, and the English wording of a template is never changed
/// without keeping the old wording readable (add the old one as a template of its own), since
/// <see cref="Parse"/> reads the English text of older entries back into lines.
/// </summary>
public static partial class HistoryText
{
    /// <summary>The key of a care entry. It is put together in code (see <see cref="CareLine"/>), not from one template.</summary>
    public const string CareKey = "Care";

    // What a value in a line is. The enum kinds say how the label is changed on the way in.
    internal enum Kind { Text, Number, Day, Month, Enum, EnumLower, EnumLowerFirst, List, ListSentence, ListLowerFirst }

    internal readonly record struct Arg(Kind Kind, Type? EnumType = null);

    internal sealed record Template(string Key, Arg[] Args, int PluralArg, bool Parse);

    private static readonly List<Template> Catalog = Build();

    private static readonly Dictionary<string, Template> ByKey = Catalog.ToDictionary(t => t.Key);

    // The enums a line can hold, by the name they are stored under
    private static readonly Dictionary<string, Type> EnumTypes = new[]
    {
        typeof(PlantStatus), typeof(GrowingMedium), typeof(LightLevel), typeof(PropagationStage), typeof(PropagationType),
        typeof(RootingAid), typeof(CareKind), typeof(FailReason), typeof(ProblemSign), typeof(ProblemAction),
        typeof(ProblemTrend), typeof(ProblemOutcome)
    }.ToDictionary(t => t.Name);

    /// <summary>The keys of the templates, for checking them against the resource files.</summary>
    public static IReadOnlyList<string> Keys => [.. Catalog.Select(t => t.Key)];

    /// <summary>The resource names a template is written under: the key, or the key with _One and _Other for a count.</summary>
    public static IReadOnlyList<string> ResourceNames(string key) =>
        ByKey.TryGetValue(key, out var t) && t.PluralArg >= 0 ? [key + "_One", key + "_Other"] : [key];

    // ---- making lines ----

    public static HistoryLine Line(string key, params HistoryArg[] args) => new() { Key = key, Args = [.. args] };

    /// <summary>Puts the lines on the entry, and the same lines in English in <see cref="TimelineEntry.Text"/>.</summary>
    public static void Set(TimelineEntry entry, IReadOnlyList<HistoryLine> lines, Func<Enum, string> label)
    {
        entry.Lines = [.. lines];
        entry.Text = English(lines, label);
    }

    /// <summary>The same for lines with no enum in them, which need no label.</summary>
    public static void Set(TimelineEntry entry, IReadOnlyList<HistoryLine> lines) => Set(entry, lines, e => e.ToString());

    // ---- writing ----

    /// <summary>One line in the given language. A key this version doesn't know comes back as the key.</summary>
    public static string Render(HistoryLine line, Func<Enum, string> label, CultureInfo culture) =>
        InCulture(culture, () => TryRender(line, label, culture)) ?? line.Key;

    /// <summary>The lines in English, one on each line. This is what goes into <see cref="TimelineEntry.Text"/>.</summary>
    /// <param name="englishLabel">Turns an enum value into its English label. It is called with English as the language of the app,
    /// so a label from the UI works too.</param>
    public static string English(IEnumerable<HistoryLine> lines, Func<Enum, string> englishLabel) =>
        string.Join("\n", lines.Select(line => Render(line, englishLabel, CultureInfo.InvariantCulture)));

    public static string English(HistoryLine line, Func<Enum, string> englishLabel) =>
        Render(line, englishLabel, CultureInfo.InvariantCulture);

    // ---- showing ----

    /// <summary>
    /// What the screen shows for an entry. An entry with lines is shown from them in the reader's language, and
    /// a line this version doesn't know is shown as it was saved. An older entry has its English text read back
    /// into lines first, and a line that matches no template is shown as it was saved. Notes and photo captions
    /// are the person's own words and come back as they are, and so does everything in English.
    /// </summary>
    public static string Show(TimelineEntry entry, Func<Enum, string> label, Func<Enum, string> englishLabel, CultureInfo culture)
    {
        var text = entry.Text ?? "";
        if (entry.Kind is TimelineKind.Note or TimelineKind.Photo || IsEnglish(culture) || text.Length == 0 && entry.Lines is not { Count: > 0 })
            return text;

        var stored = text.Split('\n');
        if (entry.Lines is { Count: > 0 } lines)
        {
            var shown = new string[lines.Count];
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var rendered = InCulture(culture, () => TryRender(line, label, culture));
                if (rendered is null)
                {
                    if (stored.Length != lines.Count)
                        return text;
                    rendered = stored[i];
                }
                shown[i] = rendered;
            }
            return string.Join("\n", shown);
        }

        // The note under a problem is the person's own, so only its first line is read
        var read = entry.Event == TimelineEvent.Problem ? 1 : stored.Length;
        var parsed = Parse(string.Join("\n", stored.Take(read)), englishLabel);
        var result = new string[stored.Length];
        for (var i = 0; i < stored.Length; i++)
        {
            var line = i < read ? parsed[i] : null;
            result[i] = line is null ? stored[i] : InCulture(culture, () => TryRender(line, label, culture)) ?? stored[i];
        }
        return string.Join("\n", result);
    }

    // ---- reading old text ----

    /// <summary>
    /// Reads the English text of an old entry back into lines, one for each line of the text. A line that matches no template is null.
    /// </summary>
    public static IReadOnlyList<HistoryLine?> Parse(string text, Func<Enum, string> englishLabel)
    {
        var matchers = Matchers.For(englishLabel);
        return [.. text.Split('\n').Select(matchers.Match)];
    }

    // ---- rendering a line ----

    private static bool IsEnglish(CultureInfo culture) =>
        culture.Equals(CultureInfo.InvariantCulture) || culture.TwoLetterISOLanguageName == "en";

    private static T InCulture<T>(CultureInfo culture, Func<T> make)
    {
        var was = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            return make();
        }
        finally
        {
            CultureInfo.CurrentUICulture = was;
        }
    }

    private static string? Res(string name) => Strings.History.ResourceManager.GetString(name, CultureInfo.CurrentUICulture);

    private static string? TryRender(HistoryLine line, Func<Enum, string> label, CultureInfo culture)
    {
        if (line.Key == CareKey)
            return RenderCare(line, label, culture);
        if (!ByKey.TryGetValue(line.Key, out var template) || line.Args.Count != template.Args.Length)
            return null;

        var values = new object[template.Args.Length];
        for (var i = 0; i < values.Length; i++)
        {
            if (Format(line.Args[i], template.Args[i], label, culture) is not { } value)
                return null;
            values[i] = value;
        }

        var name = template.PluralArg < 0 ? template.Key
            : line.Args[template.PluralArg].Number == 1 ? template.Key + "_One" : template.Key + "_Other";
        return Res(name) is { } pattern ? string.Format(culture, pattern, values) : null;
    }

    private static string? Format(HistoryArg arg, Arg kind, Func<Enum, string> label, CultureInfo culture)
    {
        switch (kind.Kind)
        {
            case Kind.Text:
                return arg.Text;
            case Kind.Number:
                return arg.Number is { } number ? Number(number, culture) : null;
            case Kind.Day:
                return arg.Day?.ToString(Res("DayFormat"), DateCulture(culture));
            case Kind.Month:
                return arg.Day?.ToString(Res("MonthFormat"), DateCulture(culture));
            case Kind.Enum or Kind.EnumLower or Kind.EnumLowerFirst:
                return arg.Enum is { } text && Enumerator(text, kind.EnumType) is { } value ? Change(Label(value, label), kind.Kind) : null;
            default:
                if (arg.Enums is not { } texts)
                    return null;
                var labels = new List<string>();
                foreach (var item in texts)
                {
                    if (Enumerator(item, kind.EnumType) is not { } each)
                        return null;
                    var word = Label(each, label);
                    labels.Add(kind.Kind == Kind.List || kind.Kind == Kind.ListSentence && labels.Count == 0 ? word : LowerFirst(word));
                }
                return string.Join(", ", labels);
        }
    }

    private static string Change(string label, Kind kind) => kind switch
    {
        Kind.EnumLower => label.ToLowerInvariant(),
        Kind.EnumLowerFirst => LowerFirst(label),
        _ => label
    };

    private static string LowerFirst(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

    private static string Number(decimal number, CultureInfo culture) =>
        number.ToString("0.##", IsEnglish(culture) ? CultureInfo.InvariantCulture : culture);

    // English reads "19 Sep 2026" (en-GB would write "Sept"), the same as TextCulture
    private static CultureInfo DateCulture(CultureInfo culture) => IsEnglish(culture) ? CultureInfo.InvariantCulture : culture;

    /// <summary>Reads "PlantStatus.Died" back into the value. Null for a type or a name this version doesn't have.</summary>
    internal static Enum? Enumerator(string text, Type? expected)
    {
        var dot = text.IndexOf('.');
        if (dot <= 0 || !EnumTypes.TryGetValue(text[..dot], out var type) || expected is not null && type != expected)
            return null;
        return Enum.TryParse(type, text[(dot + 1)..], out var value) && Enum.IsDefined(type, value!) ? (Enum)value! : null;
    }

    // The enums with their words in Core are labelled here, the others by the label the caller gives
    private static string Label(Enum value, Func<Enum, string> label) => value switch
    {
        FailReason reason => Res("FailReason" + reason) ?? reason.ToString(),
        ProblemSign sign => ProblemText.Sign(sign),
        ProblemAction action => ProblemText.Action(action),
        ProblemTrend trend => ProblemText.Trend(trend),
        ProblemOutcome outcome => ProblemText.Outcome(outcome),
        _ => label(value)
    };

    // ---- care entries ----
    // "Watered", "Fertilised with Aroid feed (2 L): Silica, 0.5 ml/L", "Moisture reading: 4/10 · EC 1.2". The parts
    // that are there change from entry to entry, so the line is put together from small templates.
    // The values, in order: the kind (an enum or an own kind's name), moisture, feed, litres of water,
    // products, note, EC, pH. A part that isn't there is an empty value.

    /// <summary>The line for a care entry.</summary>
    /// <param name="ownName">The name of the entry's own kind, or null for a built-in kind.</param>
    /// <param name="products">The products as written, like "Silica, 0.5 ml/L + Hydro fertiliser, 2 ml/L", or null for none.</param>
    public static HistoryLine CareLine(CareLog entry, string? ownName, string? products)
    {
        string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        return new HistoryLine
        {
            Key = CareKey,
            Args =
            [
                ownName is not null ? HistoryArg.Of(ownName) : HistoryArg.Of(entry.Kind),
                entry.Kind == CareKind.MoistureReading && entry.Moisture is { } moisture ? HistoryArg.Of(moisture) : HistoryArg.None,
                Clean(entry.FeedName) is { } feed ? HistoryArg.Of(feed) : HistoryArg.None,
                entry.WaterLitres is { } litres ? HistoryArg.Of(litres) : HistoryArg.None,
                products is not null ? HistoryArg.Of(products) : HistoryArg.None,
                Clean(entry.Notes) is { } notes ? HistoryArg.Of(notes) : HistoryArg.None,
                entry.Ec is { } ec ? HistoryArg.Of(ec) : HistoryArg.None,
                entry.Ph is { } ph ? HistoryArg.Of(ph) : HistoryArg.None
            ]
        };
    }

    private static string? RenderCare(HistoryLine line, Func<Enum, string> label, CultureInfo culture)
    {
        if (line.Args.Count != 8)
            return null;
        var a = line.Args;
        string? what;
        if (a[0].Enum is { } kindText)
            what = Enumerator(kindText, typeof(CareKind)) is { } kind ? Label(kind, label) : null;
        else
            what = a[0].Text;
        if (what is null)
            return null;

        string F(string name, params object[] values) => string.Format(culture, Res(name) ?? "{0}", values);

        if (a[1].Number is { } moisture)
            what = F("CareMoisture", what, Number(moisture, culture));
        if (a[2].Text is { } feed)
            what = F("CareWith", what, feed);
        if (a[3].Number is { } litres)
            what = F("CareWater", what, Doses.Water(litres));
        if (a[4].Text is { } products)
            what = a[5].Text is { } withNote ? F("CareTailNotes", what, products, withNote) : F("CareTail", what, products);
        else if (a[5].Text is { } note)
            what = F("CareTail", what, note);

        var readings = new List<string>();
        if (a[6].Number is { } ec)
            readings.Add($"EC {ec.ToString("0.0#", CultureInfo.InvariantCulture)}");
        if (a[7].Number is { } ph)
            readings.Add($"pH {ph.ToString("0.0#", CultureInfo.InvariantCulture)}");
        return readings.Count == 0 ? what : $"{what} · {string.Join(" · ", readings)}";
    }

    // ---- reading English back into lines ----

    private sealed class Matchers
    {
        private static readonly ConditionalWeakTable<Func<Enum, string>, Matchers> Cache = new();

        private sealed record Compiled(Template Template, bool IsOne, bool IsPlural, Regex Pattern, int[] Order);

        private readonly List<Compiled> compiled = [];
        private Regex? care;
        private readonly Dictionary<string, Enum> careKinds = [];
        private readonly Dictionary<(Type, Kind), Dictionary<string, Enum>> maps = [];
        private readonly Func<Enum, string> label;

        private Matchers(Func<Enum, string> label)
        {
            this.label = label;
        }

        public static Matchers For(Func<Enum, string> label) =>
            Cache.TryGetValue(label, out var known) ? known : Cache.GetValue(label, l => SavedText.InEnglish(() => new Matchers(l).Compile()));

        private Matchers Compile()
        {
            foreach (var template in Catalog.Where(t => t.Parse))
            {
                if (template.PluralArg < 0)
                    Add(template, template.Key, false, false);
                else
                {
                    Add(template, template.Key + "_One", true, true);
                    Add(template, template.Key + "_Other", false, true);
                }
            }

            foreach (CareKind kind in Enum.GetValues<CareKind>())
                careKinds.TryAdd(HistoryText.Label(kind, label), kind);
            var kinds = string.Join("|", careKinds.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape));
            care = new Regex(
                $@"^(?<kind>{kinds})(?:: (?<m>\d+)/10)?(?: with (?<feed>.+?))?(?: \((?<w>\d+(?:\.\d+)? (?:ml|L))\))?(?:: (?<tail>.+?))?(?: · EC (?<ec>\d+\.\d+))?(?: · pH (?<ph>\d+\.\d+))?$");
            return this;
        }

        private void Add(Template template, string resource, bool isOne, bool isPlural)
        {
            var english = Strings.History.ResourceManager.GetString(resource, CultureInfo.InvariantCulture);
            if (english is null)
                return;

            var pattern = new StringBuilder("^");
            var order = new List<int>();
            var at = 0;
            foreach (Match slot in Regex.Matches(english, @"\{(\d+)\}"))
            {
                pattern.Append(Regex.Escape(english[at..slot.Index]));
                var index = int.Parse(slot.Groups[1].Value, CultureInfo.InvariantCulture);
                pattern.Append(Group(template.Args[index]));
                order.Add(index);
                at = slot.Index + slot.Length;
            }
            pattern.Append(Regex.Escape(english[at..])).Append('$');
            compiled.Add(new Compiled(template, isOne, isPlural, new Regex(pattern.ToString()), [.. order]));
        }

        private string Group(Arg arg)
        {
            switch (arg.Kind)
            {
                case Kind.Text:
                    return "(.+?)";
                case Kind.Number:
                    return @"(-?\d+(?:\.\d+)?)";
                case Kind.Day:
                    return @"(\d{1,2} [A-Za-z]{3,4} \d{4})";
                case Kind.Month:
                    return @"([A-Za-z]+ \d{4})";
                case Kind.Enum or Kind.EnumLower or Kind.EnumLowerFirst:
                    return "(" + string.Join("|", Map(arg).Keys.OrderByDescending(k => k.Length).Select(Regex.Escape)) + ")";
                default:
                    return "(.*?)";
            }
        }

        // English label to value. A list in a sentence has the first word as written and the rest in lower case
        private Dictionary<string, Enum> Map(Arg arg)
        {
            var key = (arg.EnumType!, arg.Kind);
            if (maps.TryGetValue(key, out var known))
                return known;

            var map = new Dictionary<string, Enum>();
            foreach (Enum value in Enum.GetValues(arg.EnumType!))
            {
                var word = HistoryText.Label(value, label);
                map.TryAdd(arg.Kind switch
                {
                    Kind.EnumLower => word.ToLowerInvariant(),
                    Kind.EnumLowerFirst or Kind.ListLowerFirst => LowerFirst(word),
                    _ => word
                }, value);
                if (arg.Kind == Kind.ListSentence)
                    map.TryAdd(LowerFirst(word), value);
            }
            return maps[key] = map;
        }

        public HistoryLine? Match(string line)
        {
            foreach (var each in compiled)
            {
                var match = each.Pattern.Match(line);
                if (match.Success && Read(each, match) is { } read)
                    return read;
            }

            return ReadCare(line);
        }

        private HistoryLine? Read(Compiled each, Match match)
        {
            var template = each.Template;
            var args = new HistoryArg?[template.Args.Length];
            for (var g = 0; g < each.Order.Length; g++)
            {
                var index = each.Order[g];
                if (Value(template.Args[index], match.Groups[g + 1].Value) is not { } arg)
                    return null;
                args[index] = arg;
            }
            if (args.Any(a => a is null))
                return null;
            if (each.IsPlural && args[template.PluralArg]!.Number is { } count && (count == 1) != each.IsOne)
                return null;
            return new HistoryLine { Key = template.Key, Args = [.. args.Select(a => a!)] };
        }

        private HistoryArg? Value(Arg arg, string text)
        {
            switch (arg.Kind)
            {
                case Kind.Text:
                    return HistoryArg.Of(text);
                case Kind.Number:
                    return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? HistoryArg.Of(number) : null;
                case Kind.Day:
                    return DateOnly.TryParseExact(text, "d MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? HistoryArg.Of(day) : null;
                case Kind.Month:
                    return DateOnly.TryParseExact(text, "MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month) ? HistoryArg.Of(month) : null;
                case Kind.Enum or Kind.EnumLower or Kind.EnumLowerFirst:
                    return Map(arg).TryGetValue(text, out var value) ? HistoryArg.Of(value) : null;
                default:
                    if (text.Length == 0)
                        return null;
                    var map = Map(arg);
                    var texts = new List<string>();
                    foreach (var item in text.Split(", "))
                    {
                        if (!map.TryGetValue(item, out var each))
                            return null;
                        texts.Add(HistoryArg.EnumText(each));
                    }
                    return new HistoryArg { Enums = texts };
            }
        }

        // Products and notes read the same in every language, so what follows the colon is kept as the note
        private HistoryLine? ReadCare(string line)
        {
            if (care!.Match(line) is not { Success: true } match)
                return null;

            HistoryArg Part(string group, Func<string, HistoryArg?> make) =>
                match.Groups[group].Success && make(match.Groups[group].Value) is { } arg ? arg : HistoryArg.None;
            HistoryArg? Dec(string text) =>
                decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? HistoryArg.Of(number) : null;

            return new HistoryLine
            {
                Key = CareKey,
                Args =
                [
                    HistoryArg.Of(careKinds[match.Groups["kind"].Value]),
                    Part("m", Dec),
                    Part("feed", HistoryArg.Of),
                    Part("w", text =>
                    {
                        var parts = text.Split(' ');
                        return Dec(parts[0]) is { Number: { } amount } ? HistoryArg.Of(parts[1] == "ml" ? amount / 1000 : amount) : null;
                    }),
                    HistoryArg.None,
                    Part("tail", HistoryArg.Of),
                    Part("ec", Dec),
                    Part("ph", Dec)
                ]
            };
        }
    }
}
