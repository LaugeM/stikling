using Stikling.Core.Backup;

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
    Source,
    Price,
    Medium,
    Status,
    Tags,
    AboutIt,
    LastWatered,
    LastFed,
    LastRepotted
}

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
        ImportField.Source => "Source",
        ImportField.Price => "Price paid",
        ImportField.Medium => "Grows in",
        ImportField.Status => "Status",
        ImportField.Tags => "Tags",
        ImportField.AboutIt => "About it, the notes",
        ImportField.LastWatered => "Last watered",
        ImportField.LastFed => "Last fed",
        ImportField.LastRepotted => "Last repotted",
        _ => field.ToString()
    };

    /// <summary>The fields in the picker, grouped so the list reads in a few short parts. Every field is in one group.</summary>
    public static IReadOnlyList<(string Group, ImportField[] Fields)> Groups { get; } =
    [
        ("Keep or leave out", [ImportField.AboutLine, ImportField.Skip]),
        ("Name", [ImportField.Name, ImportField.Number, ImportField.Nickname, ImportField.Genus, ImportField.Species, ImportField.Cultivar, ImportField.NameSure, ImportField.NameNote]),
        ("Where and when", [ImportField.Place, ImportField.GotOn, ImportField.Origin, ImportField.Source, ImportField.Price]),
        ("Care", [ImportField.LastWatered, ImportField.LastFed, ImportField.LastRepotted]),
        ("More about it", [ImportField.Medium, ImportField.Status, ImportField.Tags, ImportField.AboutIt])
    ];

    // Our own export, column by column. Name is the display name made from the others, and Added is when
    // the plant was added here, so neither says anything the other columns don't.
    private static readonly ImportField[] OwnExport =
    [
        ImportField.Skip, ImportField.Number, ImportField.Nickname, ImportField.Genus, ImportField.Species, ImportField.Cultivar, ImportField.NameSure, ImportField.NameNote,
        ImportField.Status, ImportField.Place, ImportField.GotOn, ImportField.Origin, ImportField.Source,
        ImportField.Price, ImportField.Tags, ImportField.AboutIt, ImportField.Skip
    ];

    // The same for an export from before the name columns, and from before the Number column
    private static readonly ImportField[] OwnExportBeforeNameSure = [.. OwnExport.Where((_, i) => i is not (6 or 7))];
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
        if (headings.SequenceEqual(CsvExport.PlantHeader))
            return [.. OwnExport];
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

    // Lower case, with punctuation and underscores as spaces and the spaces made single
    private static string Simplify(string heading) =>
        string.Join(' ', new string([.. heading.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
