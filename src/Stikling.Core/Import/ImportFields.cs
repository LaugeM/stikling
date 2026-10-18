using System.Text.RegularExpressions;
using Stikling.Core.Backup;
using Stikling.Core.Fields;
using Stikling.Core.Models;

namespace Stikling.Core.Import;

/// <summary>What a column of a spreadsheet becomes on the plant. Only used while importing, never stored.</summary>
public enum ImportField
{
    /// <summary>Not imported.</summary>
    Skip,

    /// <summary>Added to About it as "Heading: value", so nothing in the file is lost.</summary>
    AboutLine,

    Name,
    Number,
    Nickname,
    Genus,
    Species,
    Cultivar,
    NameSure,
    NameNote,
    Place,
    GotOn,
    Origin,
    Raised,
    Source,
    Price,
    Medium,
    Status,
    Tags,
    AboutIt,
    LastWatered,
    LastFed,
    LastRepotted,

    /// <summary>A field of the person's own, an existing one or a new one made by the import. See <see cref="ImportOwnField"/>.</summary>
    OwnField
}

/// <summary>
/// Where a column set to <see cref="ImportField.OwnField"/> goes: a field that is there
/// (<paramref name="ExistingId"/> set, and <paramref name="Kind"/> its kind) or a new one to make.
/// </summary>
public sealed record ImportOwnField(string Name, OwnFieldKind Kind, Guid? ExistingId)
{
    /// <summary>The id the values carry in the preview. A new field made from the name gets this id, unless a renamed field holds it.</summary>
    public Guid PreviewId => ExistingId ?? Models.OwnField.IdFor(Name);

    public bool IsNew => ExistingId is null;
}

/// <summary>What each column of a sheet is guessed to be, and where the columns that are fields of their own go (null for the others).</summary>
public sealed record ImportGuess(IReadOnlyList<ImportField> Matches, IReadOnlyList<ImportOwnField?> Targets);

public static class ImportFields
{
    /// <summary>What the picker shows. The wording follows the plant form where it has the same field.</summary>
    public static string Label(ImportField field) => field switch
    {
        ImportField.Skip => "Leave out",
        ImportField.AboutLine => "A line in About it",
        ImportField.Name => "Name",
        ImportField.Number => "Collection number",
        ImportField.Nickname => "Nickname",
        ImportField.Genus => "Genus",
        ImportField.Species => "Species",
        ImportField.Cultivar => "Cultivar",
        ImportField.NameSure => "How sure the name is",
        ImportField.NameNote => "Locality or code",
        ImportField.Place => "Room",
        ImportField.GotOn => "Date you got it",
        ImportField.Origin => "How you got it",
        ImportField.Raised => "How it was raised",
        ImportField.Source => "Source",
        ImportField.Price => "Price paid",
        ImportField.Medium => "Grows in",
        ImportField.Status => "Status",
        ImportField.Tags => "Tags",
        ImportField.AboutIt => "About it, the notes",
        ImportField.LastWatered => "Last watered",
        ImportField.LastFed => "Last fed",
        ImportField.LastRepotted => "Last repotted",
        ImportField.OwnField => "A field of its own",
        _ => field.ToString()
    };

    /// <summary>The fields in the picker, grouped so the list reads in a few short parts. Every field is in one group.</summary>
    public static IReadOnlyList<(string Group, ImportField[] Fields)> Groups { get; } =
    [
        ("Keep or leave out", [ImportField.AboutLine, ImportField.Skip]),
        ("Name", [ImportField.Name, ImportField.Number, ImportField.Nickname, ImportField.Genus, ImportField.Species, ImportField.Cultivar, ImportField.NameSure, ImportField.NameNote]),
        ("Where and when", [ImportField.Place, ImportField.GotOn, ImportField.Origin, ImportField.Raised, ImportField.Source, ImportField.Price]),
        ("Care", [ImportField.LastWatered, ImportField.LastFed, ImportField.LastRepotted]),
        ("More about it", [ImportField.Medium, ImportField.Status, ImportField.Tags, ImportField.AboutIt]),
        ("Your fields", [ImportField.OwnField])
    ];

    // Our own export, column by column. Name is the display name made from the others, and Added is when
    // the plant was added here, so neither says anything the other columns don't.
    private static readonly ImportField[] OwnExport =
    [
        ImportField.Skip, ImportField.Number, ImportField.Nickname, ImportField.Genus, ImportField.Species, ImportField.Cultivar, ImportField.NameSure, ImportField.NameNote,
        ImportField.Status, ImportField.Place, ImportField.GotOn, ImportField.Origin, ImportField.Raised, ImportField.Source,
        ImportField.Price, ImportField.Tags, ImportField.AboutIt, ImportField.Skip
    ];

    // The same for an export from before the Raised column, before the name columns, and before the Number column
    private static readonly ImportField[] OwnExportBeforeRaised = [.. OwnExport.Where((_, i) => i != 12)];
    private static readonly ImportField[] OwnExportBeforeNameSure = [.. OwnExportBeforeRaised.Where((_, i) => i is not (6 or 7))];
    private static readonly ImportField[] OwnExportBeforeNumber = [.. OwnExportBeforeNameSure.Where((_, i) => i != 1)];

    private static readonly Dictionary<string, ImportField> Words = Build();

    private static Dictionary<string, ImportField> Build()
    {
        var words = new Dictionary<string, ImportField>();
        void Add(ImportField field, params string[] names)
        {
            foreach (var name in names)
                words[name] = field;
        }

        Add(ImportField.Name, "name", "plant", "plant name", "botanical name", "latin name", "scientific name",
            "navn", "plante", "plantenavn", "latinsk navn");
        Add(ImportField.Number, "number", "collection number", "plant number", "no", "nr", "plant no", "nummer");
        Add(ImportField.Nickname, "nickname", "own name", "kaldenavn");
        Add(ImportField.Genus, "genus", "slægt");
        Add(ImportField.Species, "species", "art");
        Add(ImportField.Cultivar, "cultivar", "variety", "variant", "sort", "kultivar");
        Add(ImportField.NameSure, "name sure", "cf aff", "cf", "aff", "cf aff sp", "qualifier", "identification", "id status", "name certainty");
        Add(ImportField.NameNote, "locality or code", "locality", "collector code", "collector number", "collector no", "collection code", "field number", "field no", "voucher", "origin locality");
        Add(ImportField.Place, "room", "location", "place", "spot", "where", "rum", "hvor", "værelse", "placering", "sted");
        Add(ImportField.GotOn, "acquired", "date acquired", "got", "date got", "bought", "purchased", "purchase date",
            "købt", "købsdato", "anskaffet", "dato");
        Add(ImportField.Origin, "origin", "how got", "oprindelse");
        Add(ImportField.Raised, "raised", "raised as", "how raised", "propagation type", "grown as", "tc", "opformering", "dyrket som");
        Add(ImportField.Source, "source", "from", "shop", "store", "bought from", "kilde", "fra", "butik", "forhandler");
        Add(ImportField.Price, "price", "price paid", "cost", "pris", "købspris");
        Add(ImportField.Medium, "medium", "soil", "substrate", "grows in", "jord", "substrat");
        Add(ImportField.Status, "status");
        Add(ImportField.Tags, "tags", "tag", "labels", "mærker");
        Add(ImportField.AboutIt, "notes", "note", "about", "about it", "comments", "comment", "noter",
            "bemærkninger", "kommentar", "kommentarer");
        Add(ImportField.LastWatered, "watered", "last watered", "vandet", "sidst vandet");
        Add(ImportField.LastFed, "fed", "fertilised", "fertilized", "last fed", "gødet", "sidst gødet");
        Add(ImportField.LastRepotted, "repotted", "last repotted", "omplantet", "sidst omplantet");
        return words;
    }

    /// <summary>
    /// A guess at what each column is. Our own export is recognised by its headings. Otherwise the
    /// headings are matched against English and Danish words, and a column that matches nothing, or
    /// a field another column already took, is added to About it so nothing in the file is lost.
    /// </summary>
    public static IReadOnlyList<ImportField> Guess(IReadOnlyList<string> headings)
    {
        // Our own export, with a column after Added for each field of the person's own. Those are kept
        // as a line in About it until the import can make fields of them.
        if (headings.Count >= CsvExport.PlantHeader.Length && headings.Take(CsvExport.PlantHeader.Length).SequenceEqual(CsvExport.PlantHeader))
            return [.. OwnExport, .. Enumerable.Repeat(ImportField.AboutLine, headings.Count - OwnExport.Length)];
        if (headings.SequenceEqual(CsvExport.PlantHeaderBeforeRaised))
            return [.. OwnExportBeforeRaised];
        if (headings.SequenceEqual(CsvExport.PlantHeaderBeforeNameSure))
            return [.. OwnExportBeforeNameSure];
        if (headings.SequenceEqual(CsvExport.PlantHeaderBeforeNumber))
            return [.. OwnExportBeforeNumber];

        var guess = new ImportField[headings.Count];
        var taken = new HashSet<ImportField>();
        for (var i = 0; i < headings.Count; i++)
        {
            guess[i] = ImportField.AboutLine;
            // A # is punctuation, which Simplify drops, so it is looked for as it is
            var word = headings[i].Trim() == "#" ? "number" : Simplify(headings[i]);
            if (Words.TryGetValue(word, out var field) && taken.Add(field))
                guess[i] = field;
        }
        return guess;
    }

    /// <summary>
    /// The same guess, and for a column that matches no field a field of its own: the live field with
    /// the same name (ignoring case) if there is one, otherwise a new field named after the heading
    /// with a kind guessed from what is in the column. A blank heading, one that only repeats a field
    /// another column took, and a second column with the same name stay as a line in About it. Our
    /// own export maps its columns as it did, and the columns after them become fields by name.
    /// </summary>
    public static ImportGuess Guess(Sheet sheet, IReadOnlyList<OwnField> fields, DateOrder order, DateOnly today)
    {
        var headings = sheet.Headings;
        var matches = new List<ImportField>(Guess(headings));
        var targets = new ImportOwnField?[headings.Count];
        var usedNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        var fromOwnExport = headings.Count >= CsvExport.PlantHeader.Length
            && headings.Take(CsvExport.PlantHeader.Length).SequenceEqual(CsvExport.PlantHeader);
        for (var i = 0; i < headings.Count; i++)
        {
            // Only a column the guess left as a line in About it. In our own export that is any column
            // after the built-in ones. Elsewhere it is only a heading that matches nothing, never one that
            // repeats a field another column took.
            if (matches[i] != ImportField.AboutLine)
                continue;
            if (fromOwnExport ? i < OwnExport.Length : Words.ContainsKey(Simplify(headings[i])) || headings[i].Trim() == "#")
                continue;

            var name = headings[i].Trim();
            if (name.Length > OwnField.MaxNameLength)
                name = name[..OwnField.MaxNameLength].TrimEnd();
            if (name.Length == 0 || GeneratedHeading.IsMatch(name) || !usedNames.Add(name))
                continue;

            var live = fields.FirstOrDefault(f => !f.IsDeleted && string.Equals(f.Name.Trim(), name, StringComparison.CurrentCultureIgnoreCase));
            targets[i] = live is not null
                ? new ImportOwnField(live.Name, live.Kind, live.Id)
                : new ImportOwnField(name, GuessKind(Column(sheet, i), order, today), null);
            matches[i] = ImportField.OwnField;
        }
        return new ImportGuess(matches, targets);
    }

    // "Column 1", "Column 2": what a sheet without headings is given, which say nothing
    private static readonly Regex GeneratedHeading = new(@"^Column \d+$", RegexOptions.Compiled);

    private static IEnumerable<string> Column(Sheet sheet, int column) =>
        sheet.Rows.Select(r => column < r.Count ? r[column] : "");

    /// <summary>
    /// The kind of field that fits what a column holds, looking at the cells that aren't empty. Nothing
    /// gives Text. All numbers give Number, all whole days Date, and a few values repeated over the
    /// rows (at most 10, and fewer than there are cells) give Choice. Otherwise Text.
    /// </summary>
    public static OwnFieldKind GuessKind(IEnumerable<string> cells, DateOrder order, DateOnly today)
    {
        var values = cells.Select(c => (c ?? "").Trim()).Where(c => c.Length > 0).ToList();
        if (values.Count == 0)
            return OwnFieldKind.Text;
        if (values.All(v => OwnFieldValues.Normalize(OwnFieldKind.Number, v, out var error) is not null && error is null))
            return OwnFieldKind.Number;
        if (values.All(v => ImportValues.ReadDate(v, order, today) is { Precision: DatePrecision.Day }))
            return OwnFieldKind.Date;
        var distinct = values.Distinct(StringComparer.CurrentCultureIgnoreCase).Count();
        return distinct <= 10 && distinct < values.Count ? OwnFieldKind.Choice : OwnFieldKind.Text;
    }

    // Lower case, with punctuation and underscores as spaces and the spaces made single
    private static string Simplify(string heading) =>
        string.Join(' ', new string([.. heading.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
