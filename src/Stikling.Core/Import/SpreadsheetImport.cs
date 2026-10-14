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
        DateOnly today)
    {
        var fields = new Dictionary<ImportField, int>();
        var aboutLines = new List<int>();
        for (var column = 0; column < sheet.Headings.Count; column++)
        {
            var field = column < matches.Count ? matches[column] : ImportField.AboutLine;
            if (field == ImportField.Skip)
                continue;
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

            var cantAdd = string.IsNullOrWhiteSpace(draft.Nickname) && string.IsNullOrWhiteSpace(draft.Genus)
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

    private static readonly (ImportField Field, CareKind Kind)[] CareColumns =
    [
        (ImportField.LastWatered, CareKind.Watered),
        (ImportField.LastFed, CareKind.Fertilised),
        (ImportField.LastRepotted, CareKind.Repotted)
    ];
}
