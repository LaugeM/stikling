using System.Globalization;
using System.Net;
using System.Text;
using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Backup;

/// <summary>A file to put in the backup ZIP as text, at the path inside it.</summary>
public sealed record ReadablePage(string Path, string Content);

/// <summary>What the pages need from the app to write in its language, which Core doesn't have.</summary>
public sealed class ReadableOptions
{
    public Func<Enum, string> Label { get; init; } = e => e.ToString();
    public Func<Enum, string> EnglishLabel { get; init; } = e => e.ToString();

    /// <summary>Labels that fit the cutting's type, like Sown instead of Started for seeds.</summary>
    public Func<Propagation, Func<Enum, string>>? PropagationLabel { get; init; }

    public Func<DateOnly, string> Date { get; init; } = d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public Func<LooseDate, string> LooseDate { get; init; } = d => d.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public TimeZoneInfo Zone { get; init; } = TimeZoneInfo.Utc;
    public CultureInfo Culture { get; init; } = CultureInfo.CurrentUICulture;
    public DateOnly Today { get; init; }

    /// <summary>True when the photos on the pages are the thumbnails, because the backup has no full size ones.</summary>
    public bool SmallPhotos { get; init; }
}

/// <summary>
/// The collection written as plain HTML pages that open in any browser, from the ZIP itself and without the
/// app: an index and a page for each plant and cutting. The pages are extra entries in the backup ZIP, so
/// <c>data.json</c> and the format version are untouched and a restore never reads them.
/// Everything is static: inline styles, no scripts, small pictures in the index inline as data addresses
/// (some file managers on Android don't load a picture next to the page), and the photos on a page as relative
/// paths to the photo files that are already in the ZIP.
/// </summary>
public static class ReadablePages
{
    public const string IndexPath = "index.html";
    public const string ReadMePath = "READ ME.txt";

    public static string PlantPath(Guid id) => $"pages/plants/{id}.html";
    public static string CuttingPath(Guid id) => $"pages/propagations/{id}.html";

    /// <summary>
    /// The pages for one part of a backup, or for all of it when the plan has one part.
    /// </summary>
    /// <param name="photoPaths">Each photo whose image is in this ZIP, with its path inside it, like <c>photos/&lt;id&gt;.webp</c>. This is
    /// the thumbnail's path for a backup with only small photos. A photo not in it is left off the page.</param>
    /// <param name="thumbnails">Photo id to a data address of its thumbnail, for the index. See <see cref="ReadableIndex.CoverPhotoIds"/>.</param>
    public static IReadOnlyList<ReadablePage> Build(
        BackupData data, Places places, ReadableOptions options, BackupPlan plan, int partNumber,
        IReadOnlyDictionary<Guid, string> photoPaths, IReadOnlyDictionary<Guid, string> thumbnails)
    {
        var part = plan.Parts[partNumber - 1];
        var writer = new Writer(data, places, options, plan, part, photoPaths, thumbnails);
        var pages = new List<ReadablePage>
        {
            new(IndexPath, writer.Index()),
            new(ReadMePath, Strings.BackupPages.ReadMe)
        };

        foreach (var plant in data.Plants.Where(p => !p.IsDeleted && part.PlantIds.Contains(p.Id)))
            pages.Add(new ReadablePage(PlantPath(plant.Id), writer.PlantPage(plant)));
        foreach (var cutting in data.Propagations.Where(p => !p.IsDeleted && part.PropagationIds.Contains(p.Id)))
            pages.Add(new ReadablePage(CuttingPath(cutting.Id), writer.CuttingPage(cutting)));
        return pages;
    }

    internal const string Css = """
        :root { color-scheme: light; }
        * { box-sizing: border-box; }
        body { margin: 0; background: #f6f7f4; color: #1d2520; font: 1rem/1.5 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; }
        main { max-width: 42rem; margin: 0 auto; padding: 1rem; }
        h1, h2, h3 { font-family: "Bricolage Grotesque", system-ui, sans-serif; line-height: 1.2; color: #1f5a37; }
        h1 { font-size: 1.8rem; margin: 0.5rem 0 0.25rem; }
        h2 { font-size: 1.3rem; margin: 2rem 0 0.5rem; }
        h3 { font-size: 1.05rem; margin: 1.25rem 0 0.5rem; color: #1d2520; }
        a { color: #2f7d4f; }
        .muted { color: #5f6b63; }
        .small { font-size: 0.85rem; }
        .note { background: #f6e6dc; border-radius: 0.5rem; padding: 0.5rem 0.75rem; }
        ul.list { list-style: none; margin: 0; padding: 0; }
        ul.list li { background: #fff; border: 1px solid #dde3dc; border-radius: 0.5rem; margin: 0 0 0.5rem; }
        ul.list a, ul.list .item { display: flex; gap: 0.75rem; align-items: center; padding: 0.5rem; color: inherit; text-decoration: none; }
        ul.list a:hover strong { text-decoration: underline; }
        .thumb { width: 3.5rem; height: 3.5rem; flex: none; border-radius: 0.4rem; object-fit: cover; background: #e6f2ea; }
        .who { min-width: 0; overflow-wrap: anywhere; }
        dl { display: grid; grid-template-columns: max-content 1fr; gap: 0.25rem 1rem; margin: 0.5rem 0; }
        dt { color: #5f6b63; }
        dd { margin: 0; overflow-wrap: anywhere; }
        .photos { display: grid; grid-template-columns: repeat(auto-fill, minmax(11rem, 1fr)); gap: 0.75rem; }
        figure { margin: 0; }
        figure img { display: block; width: 100%; height: auto; border-radius: 0.4rem; background: #e6f2ea; }
        figcaption { font-size: 0.85rem; color: #5f6b63; margin-top: 0.25rem; overflow-wrap: anywhere; }
        .history { list-style: none; margin: 0; padding: 0; }
        .history li { border-top: 1px solid #dde3dc; padding: 0.5rem 0; }
        .history .when { display: block; font-size: 0.85rem; color: #5f6b63; }
        @media print {
          body { background: #fff; }
          main { max-width: none; padding: 0; }
          a { color: inherit; text-decoration: none; }
          ul.list li, figure, .history li, dl { break-inside: avoid; }
          h1, h2, h3 { break-after: avoid; }
          .note { background: none; border: 1px solid #999; }
        }
        """;

    internal static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");

    // Text the person wrote, encoded, with its line breaks kept
    internal static string Multiline(string? text) =>
        Encode(text?.Trim()).Replace("\r\n", "\n").Replace("\n", "<br>");

    private sealed class Writer(
        BackupData data, Places places, ReadableOptions o, BackupPlan plan, BackupPart part,
        IReadOnlyDictionary<Guid, string> photoPaths, IReadOnlyDictionary<Guid, string> thumbnails)
    {
        private readonly Dictionary<Guid, Plant> plants = data.Plants.Where(p => !p.IsDeleted).ToDictionary(p => p.Id);
        private readonly Dictionary<Guid, Propagation> cuttings = data.Propagations.Where(p => !p.IsDeleted).ToDictionary(p => p.Id);
        private readonly IReadOnlyDictionary<Guid, Guid> covers = ReadableIndex.CoverPhotoIds(data);

        // ---- the index ----

        public string Index()
        {
            var b = new StringBuilder();
            Start(b, Strings.BackupPages.IndexTitle);
            b.Append("<h1>").Append(Encode(Strings.BackupPages.IndexTitle)).Append("</h1>\n");
            b.Append("<p class=\"muted\">").Append(Encode(F(Strings.BackupPages.IndexIntro, o.Date(o.Today)))).Append("</p>\n");
            if (plan.Count > 1)
                b.Append("<p class=\"note small\">").Append(Encode(F(Strings.BackupPages.PartNote, part.Number, plan.Count))).Append("</p>\n");
            if (o.SmallPhotos)
                b.Append("<p class=\"note small\">").Append(Encode(Strings.BackupPages.SmallPhotosNote)).Append("</p>\n");

            var plantGroups = ReadableIndex.Plants(data, places, o.Culture);
            var cuttingGroups = ReadableIndex.Cuttings(data, o.Culture);
            if (plantGroups.Count == 0 && cuttingGroups.Count == 0)
                b.Append("<p>").Append(Encode(Strings.BackupPages.NothingYet)).Append("</p>\n");

            if (plantGroups.Count > 0)
            {
                var count = plantGroups.Sum(g => g.Plants.Count);
                b.Append("<h2>").Append(Encode(Strings.BackupPages.PlantsHeading)).Append(" <span class=\"muted small\">")
                    .Append(Encode(Plural(count, Strings.BackupPages.PlantCount_One, Strings.BackupPages.PlantCount_Other))).Append("</span></h2>\n");
                foreach (var group in plantGroups)
                {
                    var heading = group.Left ? Strings.BackupPages.LeftCollection : group.Room ?? Strings.BackupPages.NoRoom;
                    b.Append("<section>\n<h3>").Append(Encode(heading)).Append("</h3>\n<ul class=\"list\">\n");
                    foreach (var plant in group.Plants)
                        ListItem(b, plant.Id, Link(plant.Id, SubjectType.Plant), plant.DisplayName, PlantDetails(plant));
                    b.Append("</ul>\n</section>\n");
                }
            }

            if (cuttingGroups.Count > 0)
            {
                var count = cuttingGroups.Sum(g => g.Cuttings.Count);
                b.Append("<h2>").Append(Encode(Strings.BackupPages.CuttingsHeading)).Append(" <span class=\"muted small\">")
                    .Append(Encode(Plural(count, Strings.BackupPages.CuttingCount_One, Strings.BackupPages.CuttingCount_Other))).Append("</span></h2>\n");
                foreach (var group in cuttingGroups)
                {
                    b.Append("<section>\n<h3>").Append(Encode(StageLabel(group.Cuttings[0]))).Append("</h3>\n<ul class=\"list\">\n");
                    foreach (var cutting in group.Cuttings)
                        ListItem(b, cutting.Id, Link(cutting.Id, SubjectType.Propagation), cutting.DisplayName, CuttingDetails(cutting));
                    b.Append("</ul>\n</section>\n");
                }
            }

            End(b);
            return b.ToString();
        }

        private void ListItem(StringBuilder b, Guid id, string? href, string name, string details)
        {
            var inner = new StringBuilder();
            if (covers.TryGetValue(id, out var coverId) && thumbnails.TryGetValue(coverId, out var uri))
                inner.Append("<img class=\"thumb\" alt=\"\" src=\"").Append(Encode(uri)).Append("\">");
            else
                inner.Append("<span class=\"thumb\"></span>");
            inner.Append("<span class=\"who\"><strong>").Append(Encode(name)).Append("</strong>");
            if (plan.PartOf(id) is { } n && n != part.Number)
                details = Join(details, F(Strings.BackupPages.InPart, n));
            if (details.Length > 0)
                inner.Append("<br><span class=\"muted small\">").Append(Encode(details)).Append("</span>");
            inner.Append("</span>");

            b.Append("<li>");
            b.Append(href is null ? $"<div class=\"item\">{inner}</div>" : $"<a href=\"{Encode(href)}\">{inner}</a>");
            b.Append("</li>\n");
        }

        private string PlantDetails(Plant plant) => Join(
            Clean(plant.CollectionNumber) is { } number ? F(Strings.BackupPages.NumberOf, number) : null,
            places.NameOf(plant.PlaceId),
            plant.AcquiredOn is { } on ? F(Strings.BackupPages.ReceivedOn, o.LooseDate(on)) : null);

        private string CuttingDetails(Propagation cutting) => Join(
            Clean(cutting.CollectionNumber) is { } number ? F(Strings.BackupPages.NumberOf, number) : null,
            places.NameOf(cutting.PlaceId),
            F(Strings.BackupPages.StartedOn, o.Date(cutting.StartedOn)));

        // ---- a plant's page ----

        public string PlantPage(Plant plant)
        {
            var b = new StringBuilder();
            Start(b, plant.DisplayName);
            PageHead(b, plant.DisplayName, plant.BotanicalName);

            var facts = new List<(string, string)>();
            Fact(facts, Strings.BackupPages.Number, Clean(plant.CollectionNumber));
            Fact(facts, Strings.BackupPages.Place, places.NameOf(plant.PlaceId));
            Fact(facts, Strings.BackupPages.Status, o.Label(plant.Status));
            if (plant.AcquiredOn is { } on)
                Fact(facts, Strings.BackupPages.Received, o.LooseDate(on));
            Fact(facts, Strings.BackupPages.Source, Clean(plant.Source));
            if (plant.StatusSince is { } since && plant.Status != PlantStatus.Active)
                Fact(facts, o.Label(plant.Status), o.Date(since));
            if (CareSummary.Latest(data.CareLogs, plant.Id, CareKind.Watered, o.Today) is { } watered)
                Fact(facts, Strings.BackupPages.LastWatered, o.Date(watered.On));
            if (CareSummary.Latest(data.CareLogs, plant.Id, CareKind.Fertilised, o.Today) is { } fed)
                Fact(facts, Strings.BackupPages.LastFed, o.Date(fed.On));
            Facts(b, facts, ParentRow(plant.ParentPlantId, plant.ParentIsGuess, plant.FromPropagationId));

            Notes(b, plant.Notes);

            var children = data.Propagations.Where(p => !p.IsDeleted && p.ParentPlantId == plant.Id)
                .Select(p => (p.DisplayName, Link(p.Id, SubjectType.Propagation), p.Id, p.StartedOn.ToDateTime(TimeOnly.MinValue)))
                .Concat(data.Plants.Where(p => !p.IsDeleted && p.ParentPlantId == plant.Id)
                    .Select(p => (p.DisplayName, Link(p.Id, SubjectType.Plant), p.Id, p.CreatedAt.UtcDateTime)))
                .OrderBy(c => c.Item4).ToList();
            if (children.Count > 0)
            {
                b.Append("<h2>").Append(Encode(Strings.BackupPages.TakenFromIt)).Append("</h2>\n<ul>\n");
                foreach (var (name, href, id, _) in children)
                    b.Append("<li>").Append(Name(name, href, id)).Append("</li>\n");
                b.Append("</ul>\n");
            }

            Photos(b, SubjectType.Plant, plant.Id);
            History(b, SubjectType.Plant, plant.Id, o.Label);
            End(b);
            return b.ToString();
        }

        // ---- a cutting's page ----

        public string CuttingPage(Propagation cutting)
        {
            var b = new StringBuilder();
            Start(b, cutting.DisplayName);
            PageHead(b, cutting.DisplayName, cutting.BotanicalName);

            var label = o.PropagationLabel?.Invoke(cutting) ?? o.Label;
            var facts = new List<(string, string)>();
            Fact(facts, Strings.BackupPages.Number, Clean(cutting.CollectionNumber));
            Fact(facts, Strings.BackupPages.Type, label(cutting.Type));
            Fact(facts, Strings.BackupPages.Status, label(cutting.Stage));
            Fact(facts, Strings.BackupPages.Place, places.NameOf(cutting.PlaceId));
            Fact(facts, Strings.BackupPages.Started, o.Date(cutting.StartedOn));
            if (cutting.RootedOn is { } rooted)
                Fact(facts, Strings.BackupPages.Rooted, o.Date(rooted));
            if (cutting.FinishedOn is { } finished)
                Fact(facts, Strings.BackupPages.Finished, o.Date(finished));
            Fact(facts, Strings.BackupPages.Source, Clean(cutting.Source));
            Facts(b, facts, ParentRow(cutting.ParentPlantId, cutting.ParentIsGuess, null));

            Notes(b, cutting.Notes);
            Photos(b, SubjectType.Propagation, cutting.Id);
            History(b, SubjectType.Propagation, cutting.Id, label);
            End(b);
            return b.ToString();
        }

        // ---- parts of a page ----

        private void PageHead(StringBuilder b, string name, string? botanical)
        {
            b.Append("<p class=\"small\"><a href=\"../../").Append(IndexPath).Append("\">").Append(Encode(Strings.BackupPages.BackToIndex)).Append("</a></p>\n");
            b.Append("<h1>").Append(Encode(name)).Append("</h1>\n");
            if (botanical is not null && botanical != name)
                b.Append("<p class=\"muted\"><em>").Append(Encode(botanical)).Append("</em></p>\n");
            if (o.SmallPhotos)
                b.Append("<p class=\"note small\">").Append(Encode(Strings.BackupPages.SmallPhotosNote)).Append("</p>\n");
        }

        private static void Fact(List<(string, string)> facts, string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                facts.Add((label, value));
        }

        // The parent is a row of HTML, since it can be a link
        private (string Label, string Html)? ParentRow(Guid? parentId, bool guess, Guid? fromCuttingId)
        {
            if (parentId is { } id && plants.TryGetValue(id, out var parent))
                return (guess ? Strings.BackupPages.ProbablyFrom : Strings.BackupPages.From,
                    Name(parent.DisplayName, Link(id, SubjectType.Plant), id));
            if (fromCuttingId is { } cuttingId && cuttings.TryGetValue(cuttingId, out var cutting))
                return (Strings.BackupPages.From, Name(cutting.DisplayName, Link(cuttingId, SubjectType.Propagation), cuttingId));
            return null;
        }

        private static void Facts(StringBuilder b, List<(string Label, string Value)> facts, (string Label, string Html)? parent)
        {
            if (facts.Count == 0 && parent is null)
                return;
            b.Append("<dl>\n");
            foreach (var (label, value) in facts)
                b.Append("<dt>").Append(Encode(label)).Append("</dt><dd>").Append(Encode(value)).Append("</dd>\n");
            if (parent is { } p)
                b.Append("<dt>").Append(Encode(p.Label)).Append("</dt><dd>").Append(p.Html).Append("</dd>\n");
            b.Append("</dl>\n");
        }

        private static void Notes(StringBuilder b, string? notes)
        {
            if (Clean(notes) is null)
                return;
            b.Append("<h2>").Append(Encode(Strings.BackupPages.NotesHeading)).Append("</h2>\n<p>").Append(Multiline(notes)).Append("</p>\n");
        }

        private void Photos(StringBuilder b, SubjectType type, Guid id)
        {
            var photos = data.Photos
                .Where(p => !p.IsDeleted && p.SubjectType == type && p.SubjectId == id && photoPaths.ContainsKey(p.Id))
                .OrderByDescending(p => p.TakenAt).ThenBy(p => p.Id).ToList();
            if (photos.Count == 0)
                return;

            // A photo's caption is the text of the entry it was added with
            var captions = new Dictionary<Guid, string>();
            foreach (var entry in data.Timeline.Where(e => !e.IsDeleted && e.PhotoIds.Count > 0 && Clean(e.Text) is not null))
                foreach (var photoId in entry.PhotoIds)
                    captions.TryAdd(photoId, entry.Text!.Trim());

            b.Append("<h2>").Append(Encode(Strings.BackupPages.PhotosHeading)).Append("</h2>\n<div class=\"photos\">\n");
            foreach (var photo in photos)
            {
                var src = "../../" + photoPaths[photo.Id];
                var taken = o.Date(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(photo.TakenAt, o.Zone).DateTime));
                var caption = Join(taken, photo.IsReference ? Strings.BackupPages.ReferencePhoto : null, captions.GetValueOrDefault(photo.Id));
                b.Append("<figure><a href=\"").Append(Encode(src)).Append("\"><img loading=\"lazy\" alt=\"")
                    .Append(Encode(captions.GetValueOrDefault(photo.Id) ?? taken)).Append("\" src=\"").Append(Encode(src)).Append("\"></a>")
                    .Append("<figcaption>").Append(Encode(caption)).Append("</figcaption></figure>\n");
            }
            b.Append("</div>\n");
        }

        private void History(StringBuilder b, SubjectType type, Guid id, Func<Enum, string> label)
        {
            var entries = TimelineOrder.NewestFirst(data.Timeline.Where(e => e.SubjectType == type && e.SubjectId == id && e.Kind != TimelineKind.Photo))
                .Select(e => (Entry: e, Text: HistoryText.Show(e, label, o.EnglishLabel, o.Culture).Trim()))
                .Where(e => e.Text.Length > 0)
                .ToList();
            if (entries.Count == 0)
                return;

            b.Append("<h2>").Append(Encode(Strings.BackupPages.HistoryHeading)).Append("</h2>\n<ul class=\"history\">\n");
            foreach (var (entry, text) in entries)
            {
                var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(entry.OccurredAt, o.Zone).DateTime);
                b.Append("<li><span class=\"when\">").Append(Encode(o.Date(day))).Append("</span>").Append(Multiline(text)).Append("</li>\n");
            }
            b.Append("</ul>\n");
        }

        // ---- helpers ----

        // A link to a record's page from the index. Null when
        // the page isn't in this ZIP, which is the case for records in another part.
        private string? Link(Guid id, SubjectType type)
        {
            var inThisPart = type == SubjectType.Plant ? part.PlantIds.Contains(id) : part.PropagationIds.Contains(id);
            if (!inThisPart)
                return null;
            return type == SubjectType.Plant ? PlantPath(id) : CuttingPath(id);
        }

        private string Name(string name, string? href, Guid id)
        {
            var text = Encode(name);
            if (href is not null)
                return $"<a href=\"{Encode(PageRelative(href))}\">{text}</a>";
            return plan.PartOf(id) is { } n && n != part.Number
                ? $"{text} <span class=\"muted\">({Encode(F(Strings.BackupPages.InPart, n))})</span>"
                : text;
        }

        // Links made for the index start with pages/, and a page is already inside it
        private static string PageRelative(string href) =>
            href.StartsWith("pages/", StringComparison.Ordinal) ? "../" + href["pages/".Length..] : href;

        private string StageLabel(Propagation sample) => (o.PropagationLabel?.Invoke(sample) ?? o.Label)(sample.Stage);

        private void Start(StringBuilder b, string title)
        {
            b.Append("<!doctype html>\n<html lang=\"").Append(Encode(o.Culture.TwoLetterISOLanguageName)).Append("\">\n<head>\n")
                .Append("<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
                .Append("<title>").Append(Encode(title)).Append(" · Stikling</title>\n<style>\n").Append(Css).Append("\n</style>\n</head>\n<body>\n<main>\n");
        }

        private static void End(StringBuilder b) => b.Append("</main>\n</body>\n</html>\n");

        private string F(string format, params object?[] args) => string.Format(o.Culture, format, args);

        private string Plural(int count, string one, string other) => F(count == 1 ? one : other, count);

        private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        private static string Join(params string?[] parts) => string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));
    }
}
