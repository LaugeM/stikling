using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;
using Strings = Stikling.Core.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Import;

/// <summary>What a column of a spreadsheet of propagations becomes. Only used while importing, never stored.</summary>
public enum PropagationImportField
{
    /// <summary>Not imported.</summary>
    Skip,

    /// <summary>Added to About it as "Heading: value", so nothing in the file is lost.</summary>
    AboutLine,

    Name,
    Nickname,
    Genus,
    Species,
    Cultivar,
    NameSure,
    NameNote,

    /// <summary>The plant it was taken from, found among the plants in the collection by its name.</summary>
    Parent,
    Place,
    StartedOn,

    /// <summary>How it was started: cutting, corm, offset, seed, division, air layer or leaf.</summary>
    Method,
    Medium,
    HowMany,
    AboutIt
}

/// <summary>Whether the plant named in a row's parent column was found.</summary>
public enum ParentMatch
{
    /// <summary>The row has no parent, or the sheet has no parent column.</summary>
    NotGiven,

    Found,

    /// <summary>No plant has the name, so the parent is left empty.</summary>
    NotFound,

    /// <summary>More than one plant has the name, so the parent is left empty.</summary>
    Several
}

/// <summary>One row of the spreadsheet as the propagation it would become.</summary>
/// <param name="Draft">The propagation to add. Its room is not set, see <paramref name="Place"/>.</param>
/// <param name="Place">The room, or "Room / Spot", as written in the file. Made when the propagation is added.</param>
/// <param name="ParentText">The parent as written in the file, or null.</param>
/// <param name="ParentName">The name of the plant that was found, for showing it.</param>
public sealed record PropagationImportRow(
    int Line,
    Propagation Draft,
    string? Place,
    string? ParentText,
    ParentMatch Parent,
    string? ParentName,
    IReadOnlyList<string> Problems,
    bool LooksLikeYours,
    string? CantAdd)
{
    /// <summary>Ticked in the preview until the person unticks it.</summary>
    public bool TickedByDefault => CantAdd is null && !LooksLikeYours;
}

public static class PropagationImportFields
{
    /// <summary>What the picker shows. The wording follows the plant import where it has the same field.</summary>
    public static string Label(PropagationImportField field) => field switch
    {
        PropagationImportField.Skip => Strings.Account.FieldSkip,
        PropagationImportField.AboutLine => Strings.Account.FieldAboutLine,
        PropagationImportField.Name => Strings.Account.FieldName,
        PropagationImportField.Nickname => Strings.Account.FieldNickname,
        PropagationImportField.Genus => Strings.Account.FieldGenus,
        PropagationImportField.Species => Strings.Account.FieldSpecies,
        PropagationImportField.Cultivar => Strings.Account.FieldCultivar,
        PropagationImportField.NameSure => Strings.Account.FieldNameSure,
        PropagationImportField.NameNote => Strings.Account.FieldNameNote,
        PropagationImportField.Parent => Strings.ImportPropagations.FieldParent,
        PropagationImportField.Place => Strings.Account.FieldPlace,
        PropagationImportField.StartedOn => Strings.ImportPropagations.FieldStartedOn,
        PropagationImportField.Method => Strings.ImportPropagations.FieldMethod,
        PropagationImportField.Medium => Strings.Account.FieldMedium,
        PropagationImportField.HowMany => Strings.Account.FieldHowMany,
        PropagationImportField.AboutIt => Strings.Account.FieldAboutIt,
        _ => field.ToString()
    };

    /// <summary>The fields in the picker, in the order they are shown.</summary>
    public static IReadOnlyList<PropagationImportField> All { get; } =
    [
        PropagationImportField.AboutLine, PropagationImportField.Skip,
        PropagationImportField.Name, PropagationImportField.Nickname, PropagationImportField.Genus, PropagationImportField.Species,
        PropagationImportField.Cultivar, PropagationImportField.NameSure, PropagationImportField.NameNote,
        PropagationImportField.Parent, PropagationImportField.StartedOn, PropagationImportField.Method, PropagationImportField.Medium,
        PropagationImportField.HowMany, PropagationImportField.Place, PropagationImportField.AboutIt
    ];

    private static readonly Dictionary<string, PropagationImportField> Words = Build();

    private static Dictionary<string, PropagationImportField> Build()
    {
        var words = new Dictionary<string, PropagationImportField>();
        void Add(PropagationImportField field, params string[] names)
        {
            foreach (var name in names)
                words[name] = field;
        }

        Add(PropagationImportField.Name, "name", "plant", "plant name", "cutting", "botanical name", "latin name", "scientific name",
            "navn", "plante", "plantenavn", "stikling", "latinsk navn");
        Add(PropagationImportField.Nickname, "nickname", "own name", "kaldenavn");
        Add(PropagationImportField.Genus, "genus", "slægt");
        Add(PropagationImportField.Species, "species", "art");
        Add(PropagationImportField.Cultivar, "cultivar", "variety", "variant", "sort", "kultivar");
        Add(PropagationImportField.NameSure, "name sure", "cf aff", "cf", "aff", "cf aff sp", "qualifier", "identification", "name certainty");
        Add(PropagationImportField.NameNote, "locality or code", "locality", "collector code", "collector number", "field number", "voucher");
        Add(PropagationImportField.Parent, "parent", "parent plant", "mother", "mother plant", "taken from", "from plant", "cut from",
            "moder", "moderplante", "mor", "taget fra");
        Add(PropagationImportField.Place, "room", "location", "place", "spot", "where", "rum", "hvor", "værelse", "placering", "sted");
        Add(PropagationImportField.StartedOn, "started", "started on", "start date", "date started", "date", "sat", "startet", "startdato", "dato", "sat i vand");
        Add(PropagationImportField.Method, "method", "type", "how", "propagation method", "metode", "fremgangsmåde");
        Add(PropagationImportField.Medium, "medium", "rooting medium", "substrate", "in", "soil", "jord", "substrat", "vækstmedie");
        Add(PropagationImportField.HowMany, "how many", "count", "qty", "quantity", "antal");
        Add(PropagationImportField.AboutIt, "notes", "note", "about", "about it", "comments", "comment", "noter", "bemærkninger", "kommentar", "kommentarer");
        return words;
    }

    /// <summary>
    /// A guess at what each column is, from English and Danish headings. A column that matches nothing, or a
    /// field another column already took, is added to About it so nothing in the file is lost.
    /// </summary>
    public static IReadOnlyList<PropagationImportField> Guess(IReadOnlyList<string> headings)
    {
        var guess = new PropagationImportField[headings.Count];
        var taken = new HashSet<PropagationImportField>();
        for (var i = 0; i < headings.Count; i++)
        {
            guess[i] = PropagationImportField.AboutLine;
            if (Words.TryGetValue(Simplify(headings[i]), out var field) && taken.Add(field))
                guess[i] = field;
        }
        return guess;
    }

    // Lower case, with punctuation and underscores as spaces and the spaces made single
    private static string Simplify(string heading) =>
        string.Join(' ', new string([.. heading.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>Turns the rows of a spreadsheet of propagations into propagations to check before they are added.</summary>
public static class PropagationImport
{
    private static readonly Dictionary<string, PropagationType> Methods = BuildMethods();

    private static Dictionary<string, PropagationType> BuildMethods()
    {
        var words = new Dictionary<string, PropagationType>();
        foreach (var value in Enum.GetValues<PropagationType>())
            words[Key(value.ToString())] = value;
        foreach (var (text, value) in new (string, PropagationType)[]
        {
            ("stem cutting", PropagationType.Cutting), ("stikling", PropagationType.Cutting), ("cuttings", PropagationType.Cutting),
            ("knold", PropagationType.Corm), ("corms", PropagationType.Corm),
            ("kindel", PropagationType.Offset), ("pup", PropagationType.Offset), ("offshoot", PropagationType.Offset), ("sideskud", PropagationType.Offset),
            ("seeds", PropagationType.Seed), ("sown", PropagationType.Seed), ("frø", PropagationType.Seed),
            ("deling", PropagationType.Division), ("divided", PropagationType.Division),
            ("air layering", PropagationType.AirLayer), ("aflægger", PropagationType.AirLayer),
            ("leaf cutting", PropagationType.Leaf), ("blad", PropagationType.Leaf), ("bladstikling", PropagationType.Leaf),
            ("andet", PropagationType.Other)
        })
            words[Key(text)] = value;
        return words;
    }

    // Case, spaces, hyphens and underscores don't count
    private static string Key(string text) => new([.. text.ToLowerInvariant().Where(c => c is not (' ' or '-' or '_'))]);

    /// <summary>How a propagation was started, from the words in a cell. Null when they aren't recognised.</summary>
    public static PropagationType? ReadMethod(string? text) =>
        Methods.TryGetValue(Key(text ?? ""), out var value) ? value : null;

    /// <summary>
    /// The plant a parent column names. A plant is matched among the active plants by its nickname, and when
    /// none has that nickname by its full botanical name, each trimmed and ignoring case. More than one match,
    /// or none, finds no plant.
    /// </summary>
    public static (ParentMatch Match, Plant? Plant) FindParent(string? text, IEnumerable<Plant> plants)
    {
        var typed = text?.Trim();
        if (string.IsNullOrEmpty(typed))
            return (ParentMatch.NotGiven, null);

        var active = plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active).ToList();

        static bool Same(string? a, string typed) =>
            a is not null && string.Equals(a.Trim(), typed, StringComparison.CurrentCultureIgnoreCase);

        var byNickname = active.Where(p => Same(p.Nickname, typed)).ToList();
        if (byNickname.Count > 0)
            return byNickname.Count == 1 ? (ParentMatch.Found, byNickname[0]) : (ParentMatch.Several, null);

        var byBotanical = active.Where(p => Same(p.BotanicalName, typed)).ToList();
        return byBotanical.Count switch
        {
            0 => (ParentMatch.NotFound, null),
            1 => (ParentMatch.Found, byBotanical[0]),
            _ => (ParentMatch.Several, null)
        };
    }

    /// <summary>
    /// One <see cref="PropagationImportRow"/> per row of the sheet. <paramref name="matches"/> says what each
    /// column is. A column set to more than one field only counts the first time, and anything that has no
    /// field of its own, or can't be read, goes into About it so nothing in the file is lost.
    /// </summary>
    public static IReadOnlyList<PropagationImportRow> Preview(
        Sheet sheet,
        IReadOnlyList<PropagationImportField> matches,
        DateOrder order,
        PlantDictionary dictionary,
        IReadOnlyList<Plant> plants,
        IReadOnlyList<Propagation> existing,
        DateOnly today)
    {
        var fields = new Dictionary<PropagationImportField, int>();
        var aboutLines = new List<int>();
        for (var column = 0; column < sheet.Headings.Count; column++)
        {
            var field = column < matches.Count ? matches[column] : PropagationImportField.AboutLine;
            if (field == PropagationImportField.Skip)
                continue;
            if (field == PropagationImportField.AboutLine || !fields.TryAdd(field, column))
                aboutLines.Add(column);
        }

        var rows = new List<PropagationImportRow>();
        for (var index = 0; index < sheet.Rows.Count; index++)
        {
            var cells = sheet.Rows[index];
            string? Get(PropagationImportField field) =>
                fields.TryGetValue(field, out var column) && column < cells.Count && cells[column].Trim() is { Length: > 0 } text
                    ? text
                    : null;

            var problems = new List<string>();
            var unread = new List<(int Column, string Text)>();
            void CantRead(PropagationImportField field, string problem)
            {
                problems.Add(problem);
                unread.Add((fields[field], Get(field)!));
            }

            var draft = new Propagation { StartedOn = today };
            string? leftover = null;
            var nameUnused = false;

            if (Get(PropagationImportField.NameSure) is { } sure)
            {
                if (PlantNames.ParseQualifier(sure) is { } qualifier)
                    draft.NameQualifier = qualifier;
                else
                    CantRead(PropagationImportField.NameSure, F(Strings.Account.ProblemNameSure, sure));
            }
            draft.NameNote = PlantNames.Note(Get(PropagationImportField.NameNote));

            var nickname = Get(PropagationImportField.Nickname);
            var name = Get(PropagationImportField.Name);
            if (Get(PropagationImportField.Genus) is { } genus)
            {
                draft.Genus = genus;
                draft.Species = Get(PropagationImportField.Species);
                draft.Cultivar = PlantNames.Cultivar(Get(PropagationImportField.Cultivar));
                draft.Nickname = nickname;
                var other = name is not null
                    && !string.Equals(name, draft.BotanicalName, StringComparison.CurrentCultureIgnoreCase);
                if (nickname is null && other)
                    draft.Nickname = name;
                nameUnused = nickname is not null && other;
            }
            else
            {
                var read = name is null ? new Plant() : QuickAdd.ReadName(name, dictionary);
                draft.Genus = read.Genus;
                draft.Species = Get(PropagationImportField.Species) ?? read.Species;
                draft.Cultivar = PlantNames.Cultivar(Get(PropagationImportField.Cultivar)) ?? read.Cultivar;
                draft.Nickname = nickname ?? read.Nickname;
                leftover = read.Notes;
                nameUnused = nickname is not null && read.Nickname is not null;
            }

            // A name the Nickname column took the place of is kept in About it rather than lost
            if (nameUnused && !string.Equals(name, nickname, StringComparison.CurrentCultureIgnoreCase))
                unread.Add((fields[PropagationImportField.Name], name!));

            if (Get(PropagationImportField.StartedOn) is { } started)
            {
                var date = ImportValues.ReadDate(started, order, today);
                if (date is null)
                    CantRead(PropagationImportField.StartedOn, F(Strings.Account.ProblemDate, started));
                else if (date.Start > today)
                    CantRead(PropagationImportField.StartedOn, F(Strings.Account.ProblemFuture, started));
                else if (date.Precision != DatePrecision.Day)
                    CantRead(PropagationImportField.StartedOn, F(Strings.Account.ProblemWholeDay, started));
                else
                    draft.StartedOn = date.Start;
            }

            if (Get(PropagationImportField.Method) is { } method)
            {
                if (ReadMethod(method) is { } type)
                    draft.Type = type;
                else
                    CantRead(PropagationImportField.Method, F(Strings.ImportPropagations.ProblemMethod, method));
            }

            if (Get(PropagationImportField.Medium) is { } medium)
            {
                if (ImportValues.ReadMedium(medium) is { } value)
                    draft.Medium = value;
                else
                    CantRead(PropagationImportField.Medium, F(Strings.Account.ProblemMedium, medium));
            }

            if (Get(PropagationImportField.HowMany) is { } howMany)
            {
                if (int.TryParse(howMany.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count >= 1)
                    draft.InitialCount = count;
                else
                    CantRead(PropagationImportField.HowMany, F(Strings.ImportPropagations.ProblemHowMany, howMany));
            }

            // No match, or more than one, leaves it empty. The row says so, and the text isn't lost: it goes in About it.
            var parentText = Get(PropagationImportField.Parent);
            var (parentMatch, parent) = FindParent(parentText, plants);
            if (parent is not null)
                draft.ParentPlantId = parent.Id;
            else if (parentText is not null)
                unread.Add((fields[PropagationImportField.Parent], parentText));

            var notes = new List<string>();
            if (Get(PropagationImportField.AboutIt) is { } about)
                notes.Add(about);
            if (leftover is not null)
                notes.Add(leftover);
            foreach (var column in aboutLines)
                if (column < cells.Count && cells[column].Trim() is { Length: > 0 } text)
                    notes.Add($"{sheet.Headings[column]}: {text}");
            notes.AddRange(unread.OrderBy(u => u.Column).Select(u => $"{sheet.Headings[u.Column]}: {u.Text}"));
            draft.Notes = notes.Count == 0 ? null : string.Join('\n', notes);

            var cantAdd = string.IsNullOrWhiteSpace(draft.Nickname) && string.IsNullOrWhiteSpace(draft.Genus) && draft.NameQualifier != NameQualifier.Unknown
                ? Strings.Account.CantAddNoName
                : draft.Validate(today).FirstOrDefault();
            var looksLikeYours = existing.Any(p => !p.IsDeleted && p.StartedOn == draft.StartedOn
                && string.Equals(p.DisplayName, draft.DisplayName, StringComparison.CurrentCultureIgnoreCase));

            rows.Add(new PropagationImportRow(
                sheet.RowNumber(index),
                draft,
                RoomName.Clean(Get(PropagationImportField.Place)),
                parentText,
                parentMatch,
                parent?.DisplayName,
                problems,
                looksLikeYours,
                cantAdd));
        }
        return rows;
    }
}
