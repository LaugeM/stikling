using System.Globalization;
using Stikling.Core.Fields;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;

namespace Stikling.Core.Import;

/// <summary>A watering, feeding or repotting from a column, to log on the day it was done.</summary>
public sealed record ImportCare(CareKind Kind, DateOnly On);

/// <summary>
/// One row of the spreadsheet as the plant it would become.
/// </summary>
/// <param name="Line">The row in the file, counting the heading row as 1.</param>
/// <param name="Draft">The plant to add. Its room is not set, see <paramref name="Place"/>.</param>
/// <param name="Place">The room, or "Room / Spot", as written in the file. Made when the plant is added.</param>
/// <param name="Problems">What in the row couldn't be read. That text is kept in About it.</param>
/// <param name="LooksLikeYours">A plant with the same name and date is already in the collection.</param>
/// <param name="CantAdd">Why the row can't be added, or null.</param>
public sealed record ImportRow(
    int Line,
    Plant Draft,
    string? Place,
    IReadOnlyList<ImportCare> Care,
    IReadOnlyList<string> Problems,
    bool LooksLikeYours,
    string? CantAdd)
{
    /// <summary>Ticked in the preview until the person unticks it.</summary>
    public bool TickedByDefault => CantAdd is null && !LooksLikeYours;
}

/// <summary>Turns the rows of a spreadsheet into plants to check before they are added.</summary>
public static class SpreadsheetImport
{
    /// <summary>
    /// One <see cref="ImportRow"/> per row of the sheet. <paramref name="matches"/> says what each
    /// column is. A column set to more than one field only counts the first time, and anything that
    /// has no field of its own, or can't be read, goes into About it so nothing in the file is lost.
    /// </summary>
    public static IReadOnlyList<ImportRow> Preview(
        Sheet sheet,
        IReadOnlyList<ImportField> matches,
        DateOrder order,
        PlantDictionary dictionary,
        IReadOnlyList<Plant> existing,
        DateOnly today,
        IReadOnlyList<ImportOwnField?>? targets = null)
    {
        var fields = new Dictionary<ImportField, int>();
        var aboutLines = new List<int>();
        var ownColumns = new List<(int Column, ImportOwnField Target)>();
        for (var column = 0; column < sheet.Headings.Count; column++)
        {
            var field = column < matches.Count ? matches[column] : ImportField.AboutLine;
            if (field == ImportField.Skip)
                continue;
            if (field == ImportField.OwnField)
            {
                // Without a target, or into a field another column fills, it is kept in About it
                var target = targets is not null && column < targets.Count ? targets[column] : null;
                if (target is null || ownColumns.Any(o => o.Target.PreviewId == target.PreviewId))
                    aboutLines.Add(column);
                else
                    ownColumns.Add((column, target));
                continue;
            }
            if (field == ImportField.AboutLine || !fields.TryAdd(field, column))
                aboutLines.Add(column);
        }

        var rows = new List<ImportRow>();
        for (var index = 0; index < sheet.Rows.Count; index++)
        {
            var cells = sheet.Rows[index];
            string? Get(ImportField field) =>
                fields.TryGetValue(field, out var column) && column < cells.Count && cells[column].Trim() is { Length: > 0 } text
                    ? text
                    : null;

            var problems = new List<string>();
            var unread = new List<(int Column, string Text)>();
            void CantRead(ImportField field, string problem)
            {
                problems.Add(problem);
                unread.Add((fields[field], Get(field)!));
            }

            var draft = new Plant();
            var leftover = (string?)null;
            var nameUnused = false;

            // Set first, since a name typed whole is compared with the name the plant would show
            if (Get(ImportField.NameSure) is { } sure)
            {
                if (PlantNames.ParseQualifier(sure) is { } qualifier)
                    draft.NameQualifier = qualifier;
                else
                    CantRead(ImportField.NameSure, $"Didn't know \"{sure}\" as how sure the name is, kept in About it");
            }
            draft.NameNote = PlantNames.Note(Get(ImportField.NameNote));

            var nickname = Get(ImportField.Nickname);
            var name = Get(ImportField.Name);
            if (Get(ImportField.Genus) is { } genus)
            {
                draft.Genus = genus;
                draft.Species = Get(ImportField.Species);
                draft.Cultivar = PlantNames.Cultivar(Get(ImportField.Cultivar));
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
                draft.Species = Get(ImportField.Species) ?? read.Species;
                draft.Cultivar = PlantNames.Cultivar(Get(ImportField.Cultivar)) ?? read.Cultivar;
                draft.Nickname = nickname ?? read.Nickname;
                leftover = read.Notes;
                nameUnused = nickname is not null && read.Nickname is not null;
            }

            // A name the Nickname column took the place of is kept in About it rather than lost
            if (nameUnused && !string.Equals(name, nickname, StringComparison.CurrentCultureIgnoreCase))
                unread.Add((fields[ImportField.Name], name!));

            if (Get(ImportField.Number) is { } number)
            {
                if (CollectionNumbers.Normalize(number) is { } code && CollectionNumbers.IsValid(code))
                    draft.CollectionNumber = code;
                else
                    CantRead(ImportField.Number, $"Couldn't use \"{number}\" as a number, kept in About it");
            }

            draft.Source = Get(ImportField.Source);
            draft.Tags = Get(ImportField.Tags) is { } tags ? ImportValues.ReadTags(tags) : [];

            if (Get(ImportField.GotOn) is { } gotOn)
            {
                var date = ImportValues.ReadDate(gotOn, order, today);
                if (date is null)
                    CantRead(ImportField.GotOn, $"Couldn't read \"{gotOn}\" as a date, kept in About it");
                else if (date.Start > today)
                    CantRead(ImportField.GotOn, $"\"{gotOn}\" is in the future, kept in About it");
                else
                    draft.AcquiredOn = date;
            }

            if (Get(ImportField.Origin) is { } origin)
            {
                if (ImportValues.ReadOrigin(origin) is { } value)
                    draft.Origin = value;
                else
                    CantRead(ImportField.Origin, $"Didn't know \"{origin}\" as an origin, kept in About it");
            }

            if (Get(ImportField.Raised) is { } raised)
            {
                if (ImportValues.ReadRaised(raised) is { } value)
                    draft.RaisedAs = value;
                else
                    CantRead(ImportField.Raised, $"Didn't know \"{raised}\" as how it was raised, kept in About it");
            }

            if (Get(ImportField.Medium) is { } medium)
            {
                if (ImportValues.ReadMedium(medium) is { } value)
                    draft.Medium = value;
                else
                    CantRead(ImportField.Medium, $"Didn't know \"{medium}\" as a medium, kept in About it");
            }

            if (Get(ImportField.Status) is { } status)
            {
                // Only the status: the day it changed is unknown, and the model reads that as null
                if (ImportValues.ReadStatus(status) is { } value)
                    draft.Status = value;
                else
                    CantRead(ImportField.Status, $"Didn't know \"{status}\" as a status, kept in About it");
            }

            if (Get(ImportField.Price) is { } price)
            {
                if (ImportValues.ReadPrice(price) is { } value)
                    draft.PricePaid = value;
                else
                    CantRead(ImportField.Price, $"Couldn't read \"{price}\" as a price, kept in About it");
            }

            if (Get(ImportField.HowMany) is { } howMany)
            {
                if (int.TryParse(howMany.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var howManyValue) && howManyValue >= 1)
                    draft.Count = howManyValue == 1 ? null : howManyValue;
                else
                    CantRead(ImportField.HowMany, $"Couldn't read \"{howMany}\" as a number of plants, kept in About it");
            }

            var care = new List<ImportCare>();
            foreach (var (field, kind) in CareColumns)
            {
                if (Get(field) is not { } text)
                    continue;
                var date = ImportValues.ReadDate(text, order, today);
                if (date is null)
                    CantRead(field, $"Couldn't read \"{text}\" as a date, kept in About it");
                else if (date.Start > today)
                    CantRead(field, $"\"{text}\" is in the future, kept in About it");
                else if (date.Precision != DatePrecision.Day)
                    CantRead(field, $"\"{text}\" needs a whole day, kept in About it");
                else
                    care.Add(new ImportCare(kind, date.Start));
            }

            foreach (var (column, target) in ownColumns)
            {
                if (column >= cells.Count || cells[column].Trim() is not { Length: > 0 } cell)
                    continue;
                var stored = ReadOwnValue(target, cell, order, today, out var problem);
                if (stored is not null)
                {
                    draft.OwnFields[target.PreviewId] = stored;
                    continue;
                }
                problems.Add(problem!);
                unread.Add((column, cell));
            }

            var notes = new List<string>();
            if (Get(ImportField.AboutIt) is { } about)
                notes.Add(about);
            if (leftover is not null)
                notes.Add(leftover);
            foreach (var column in aboutLines)
                if (column < cells.Count && cells[column].Trim() is { Length: > 0 } text)
                    notes.Add($"{sheet.Headings[column]}: {text}");
            notes.AddRange(unread.OrderBy(u => u.Column).Select(u => $"{sheet.Headings[u.Column]}: {u.Text}"));
            draft.Notes = notes.Count == 0 ? null : string.Join('\n', notes);

            var cantAdd = string.IsNullOrWhiteSpace(draft.Nickname) && string.IsNullOrWhiteSpace(draft.Genus) && draft.NameQualifier != NameQualifier.Unknown
                ? "No name"
                : draft.Validate(today).FirstOrDefault();
            var looksLikeYours = PlantMatches.SameNameEver(draft, existing).Any(p => p.AcquiredOn == draft.AcquiredOn);

            rows.Add(new ImportRow(
                sheet.RowNumber(index),
                draft,
                RoomName.Clean(Get(ImportField.Place)),
                care,
                problems,
                looksLikeYours,
                cantAdd));
        }
        return rows;
    }

    // The text a plant stores for a cell, or null with the problem to show when it can't be read as the field's kind
    private static string? ReadOwnValue(ImportOwnField target, string cell, DateOrder order, DateOnly today, out string? problem)
    {
        problem = null;
        switch (target.Kind)
        {
            case OwnFieldKind.Number:
                var number = OwnFieldValues.Normalize(OwnFieldKind.Number, cell, out var error);
                if (error is null && number is not null)
                    return number;
                // "150 kr" reads too
                if (ImportValues.ReadPrice(cell) is { } price)
                    return price.ToString("0.####", CultureInfo.InvariantCulture);
                problem = $"Couldn't read \"{cell}\" as a number for {target.Name}, kept in About it";
                return null;
            case OwnFieldKind.Date:
                if (ImportValues.ReadDate(cell, order, today) is not { } date)
                {
                    problem = $"Couldn't read \"{cell}\" as a date for {target.Name}, kept in About it";
                    return null;
                }
                if (date.Precision != DatePrecision.Day)
                {
                    problem = $"\"{cell}\" needs a whole day for {target.Name}, kept in About it";
                    return null;
                }
                return date.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            default:
                if (cell.Length > OwnFieldValues.MaxTextLength)
                {
                    problem = $"\"{cell}\" is too long for {target.Name}, kept in About it";
                    return null;
                }
                return cell;
        }
    }

    private static readonly (ImportField Field, CareKind Kind)[] CareColumns =
    [
        (ImportField.LastWatered, CareKind.Watered),
        (ImportField.LastFed, CareKind.Fertilised),
        (ImportField.LastRepotted, CareKind.Repotted)
    ];
}
