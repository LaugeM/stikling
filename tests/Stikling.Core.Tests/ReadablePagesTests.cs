using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Stikling.Core.Backup;
using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class ReadablePagesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    private static readonly Place Kitchen = new() { Id = Guid.NewGuid(), Name = "Kitchen", CreatedAt = Now };
    private static readonly Place Hall = new() { Id = Guid.NewGuid(), Name = "Hall", CreatedAt = Now.AddDays(1) };

    private static Plant Plant(string name, Place? place = null, string? notes = null, string? number = null) =>
        new() { Id = Guid.NewGuid(), Nickname = name, PlaceId = place?.Id, Notes = notes, CollectionNumber = number, CreatedAt = Now };

    private static Photo PhotoOf(Plant plant, int daysAgo, int width = 100, int height = 100) =>
        new() { Id = Guid.NewGuid(), SubjectType = SubjectType.Plant, SubjectId = plant.Id, TakenAt = Now.AddDays(-daysAgo), Width = width, Height = height };

    private static ReadableOptions Options(bool small = false) => new()
    {
        Culture = English,
        Today = new DateOnly(2026, 11, 22),
        SmallPhotos = small
    };

    private static Dictionary<string, string> Build(BackupData data, int part = 1, BackupPlan? plan = null,
        IReadOnlyDictionary<Guid, string>? images = null, bool small = false)
    {
        var places = new Places(data.Places);
        plan ??= BackupParts.Plan(data, places, culture: English);
        images ??= data.Photos.Where(p => !p.IsDeleted).ToDictionary(p => p.Id, Uri);
        return ReadablePages.Build(data, places, Options(small), plan, part, images)
            .ToDictionary(p => p.Path, p => p.Content);
    }

    private static string Uri(Photo photo) => $"data:image/webp;base64,{photo.Id:N}";

    // The section of one plant or cutting in the file
    private static string Section(string html, string anchor)
    {
        var start = html.IndexOf($"<article class=\"page\" id=\"{anchor}\">", StringComparison.Ordinal);
        Assert.True(start >= 0, $"No section {anchor}");
        return html[start..html.IndexOf("</article>", start, StringComparison.Ordinal)];
    }

    private static BackupData Collection(out Plant monstera, out Plant gone, out Propagation cutting)
    {
        monstera = Plant("Monstera", Kitchen, number: "12");
        gone = Plant("Deleted plant", Kitchen);
        gone.DeletedAt = Now;
        var cuttingOf = new Propagation
        {
            Id = Guid.NewGuid(), Nickname = "Monstera cutting", ParentPlantId = monstera.Id,
            StartedOn = new DateOnly(2026, 10, 1), CreatedAt = Now
        };
        cutting = cuttingOf;
        return new BackupData
        {
            Places = [Kitchen, Hall],
            Plants = [monstera, gone],
            Propagations = [cuttingOf]
        };
    }

    [Fact]
    public void The_index_lists_living_plants_and_leaves_out_deleted_ones()
    {
        var data = Collection(out var monstera, out var gone, out var cutting);

        var pages = Build(data);

        var index = pages["index.html"];
        Assert.Contains("Monstera", index);
        Assert.Contains("Kitchen", index);
        Assert.DoesNotContain("Deleted plant", index);
        Assert.DoesNotContain(gone.Id.ToString(), index);
        Section(index, ReadablePages.PlantAnchor(monstera.Id));
        Section(index, ReadablePages.CuttingAnchor(cutting.Id));
        Assert.Equal(["index.html", "READ ME.txt"], pages.Keys);
    }

    [Fact]
    public void Every_link_and_picture_stays_inside_the_one_file()
    {
        // A phone opens the file on its own, so a link or a picture pointing at another file never loads
        var data = Collection(out var monstera, out _, out _);
        data.Photos = [PhotoOf(monstera, 3)];

        var html = Build(data)["index.html"];

        var anchors = Regex.Matches(html, "id=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        var links = Regex.Matches(html, "href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(links);
        Assert.All(links, href => Assert.StartsWith("#", href));
        Assert.All(links, href => Assert.Contains(href[1..], anchors));
        var sources = Regex.Matches(html, "src=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(sources);
        Assert.All(sources, src => Assert.StartsWith("data:", src));
    }

    [Fact]
    public void A_cutting_links_to_its_parent_and_the_parent_lists_the_cutting()
    {
        var data = Collection(out var monstera, out _, out var cutting);

        var pages = Build(data);

        var html = pages["index.html"];
        Assert.Contains($"href=\"#{ReadablePages.PlantAnchor(monstera.Id)}\"", Section(html, ReadablePages.CuttingAnchor(cutting.Id)));
        Assert.Contains($"href=\"#{ReadablePages.CuttingAnchor(cutting.Id)}\"", Section(html, ReadablePages.PlantAnchor(monstera.Id)));
    }

    [Fact]
    public void Text_the_person_wrote_is_escaped()
    {
        var data = Collection(out var monstera, out _, out _);
        monstera.Nickname = "<script>alert(1)</script> & \"Co\"";
        monstera.Notes = "Line one <b>bold</b>\nLine two";
        data.Timeline =
        [
            new TimelineEntry { SubjectType = SubjectType.Plant, SubjectId = monstera.Id, Kind = TimelineKind.Note, Text = "<img src=x onerror=1>", OccurredAt = Now }
        ];

        var pages = Build(data);

        foreach (var html in pages.Where(p => p.Key.EndsWith(".html")).Select(p => p.Value))
        {
            Assert.DoesNotContain("<script>alert", html);
            Assert.DoesNotContain("<b>bold</b>", html);
            Assert.DoesNotContain("<img src=x", html);
        }
        var page = Section(pages["index.html"], ReadablePages.PlantAnchor(monstera.Id));
        Assert.Contains("&lt;script&gt;", page);
        Assert.Contains("Line one &lt;b&gt;bold&lt;/b&gt;<br>Line two", page);
        Assert.Contains("&lt;img src=x onerror=1&gt;", page);
    }

    [Fact]
    public void A_page_shows_its_photos_newest_first_and_skips_deleted_ones_and_ones_not_in_the_zip()
    {
        var data = Collection(out var monstera, out _, out _);
        var older = PhotoOf(monstera, 10);
        var newer = PhotoOf(monstera, 2);
        var deleted = PhotoOf(monstera, 1);
        deleted.DeletedAt = Now;
        var missing = PhotoOf(monstera, 5);
        data.Photos = [older, newer, deleted, missing];
        var images = new[] { older, newer, deleted }.ToDictionary(p => p.Id, Uri);

        var page = Section(Build(data, images: images)["index.html"], ReadablePages.PlantAnchor(monstera.Id));

        Assert.Contains($"src=\"{Uri(newer)}\"", page);
        Assert.True(page.IndexOf(Uri(newer), StringComparison.Ordinal) < page.IndexOf(Uri(older), StringComparison.Ordinal));
        Assert.DoesNotContain(Uri(deleted), page);
        Assert.DoesNotContain(missing.Id.ToString("N"), page);
    }

    [Fact]
    public void The_index_has_thumbnails_inline_and_no_scripts()
    {
        var data = Collection(out var monstera, out _, out _);
        var photo = PhotoOf(monstera, 1);
        data.Photos = [photo];
        var thumbs = new Dictionary<Guid, string> { [photo.Id] = "data:image/webp;base64,AAAA" };

        var pages = Build(data, images: thumbs);

        Assert.Contains("src=\"data:image/webp;base64,AAAA\"", pages["index.html"]);
        Assert.All(pages.Where(p => p.Key.EndsWith(".html")), p => Assert.DoesNotContain("<script", p.Value));
        Assert.Contains("<html lang=\"en\">", pages["index.html"]);
        Assert.Equal(photo.Id, ReadableIndex.CoverPhotoIds(data)[monstera.Id]);
    }

    [Fact]
    public void Plants_are_listed_by_room_then_name_and_gone_plants_last()
    {
        var data = Collection(out var monstera, out _, out _);
        var fern = Plant("Fern", Hall);
        var alocasia = Plant("Alocasia", Kitchen);
        var nowhere = Plant("Nowhere plant");
        var died = Plant("Died plant", Kitchen);
        died.Status = PlantStatus.Died;
        data.Plants = [monstera, fern, alocasia, nowhere, died];

        var groups = ReadableIndex.Plants(data, new Places(data.Places), English);

        Assert.Equal(["Kitchen", "Hall", null, null], groups.Select(g => g.Room));
        Assert.Equal(["Alocasia", "Monstera"], groups[0].Plants.Select(p => p.DisplayName));
        Assert.True(groups[^1].Left);
        Assert.Equal(["Died plant"], groups[^1].Plants.Select(p => p.DisplayName));
    }

    [Fact]
    public void Small_photos_are_said_on_every_page()
    {
        var data = Collection(out var monstera, out _, out _);

        var pages = Build(data, small: true);

        Assert.Contains("small size only", pages["index.html"]);
        Assert.Contains("small size only", Section(pages["index.html"], ReadablePages.PlantAnchor(monstera.Id)));
    }

    [Fact]
    public void A_collection_that_fits_is_one_part_with_everything()
    {
        var data = Collection(out var monstera, out _, out var cutting);
        data.Photos = [PhotoOf(monstera, 1)];

        var plan = BackupParts.Plan(data, new Places(data.Places), culture: English);

        var part = Assert.Single(plan.Parts);
        Assert.Contains(monstera.Id, part.PlantIds);
        Assert.Contains(cutting.Id, part.PropagationIds);
        Assert.Equal(data.Photos[0].Id, Assert.Single(part.PhotoIds));
        Assert.Equal(1, plan.PartOf(monstera.Id));
    }

    [Fact]
    public void A_large_collection_is_split_and_a_photo_stays_with_its_record()
    {
        var data = Collection(out var monstera, out _, out var cutting);
        var alocasia = Plant("Alocasia", Kitchen);
        var fern = Plant("Fern", Hall);
        data.Plants = [monstera, alocasia, fern];
        var photos = new[] { PhotoOf(monstera, 1), PhotoOf(monstera, 2), PhotoOf(alocasia, 1), PhotoOf(fern, 1) };
        var cuttingPhoto = new Photo { Id = Guid.NewGuid(), SubjectType = SubjectType.Propagation, SubjectId = cutting.Id, TakenAt = Now };
        var potPhoto = new Photo { Id = Guid.NewGuid(), SubjectType = SubjectType.Pot, SubjectId = Guid.NewGuid(), TakenAt = Now };
        var deletedPhoto = PhotoOf(fern, 3);
        deletedPhoto.DeletedAt = Now;
        data.Photos = [.. photos, cuttingPhoto, potPhoto, deletedPhoto];

        // Each photo counts as 100, and a part takes 250
        var plan = BackupParts.Plan(data, new Places(data.Places), maxBytes: 250, sizeOf: _ => 100, culture: English);

        Assert.True(plan.Count > 1);
        // Each plant and cutting is in exactly one part
        foreach (var id in new[] { monstera.Id, alocasia.Id, fern.Id, cutting.Id })
            Assert.Single(plan.Parts, p => p.PlantIds.Contains(id) || p.PropagationIds.Contains(id));
        // Each live photo is in exactly one part, and a plant's photos are with the plant
        foreach (var photo in data.Photos.Where(p => !p.IsDeleted))
            Assert.Single(plan.Parts, p => p.PhotoIds.Contains(photo.Id));
        Assert.DoesNotContain(plan.Parts, p => p.PhotoIds.Contains(deletedPhoto.Id));
        foreach (var plant in data.Plants)
        {
            var part = plan.Parts.Single(p => p.PlantIds.Contains(plant.Id));
            Assert.All(data.Photos.Where(p => !p.IsDeleted && p.SubjectId == plant.Id), p => Assert.Contains(p.Id, part.PhotoIds));
        }
        Assert.Equal(Enumerable.Range(1, plan.Count), plan.Parts.Select(p => p.Number));
    }

    [Fact]
    public void A_record_larger_than_the_limit_gets_a_part_of_its_own()
    {
        var data = Collection(out var monstera, out _, out _);
        data.Propagations = [];
        var alocasia = Plant("Alocasia", Kitchen);
        data.Plants = [monstera, alocasia];
        data.Photos = [PhotoOf(monstera, 1), PhotoOf(alocasia, 1)];

        var plan = BackupParts.Plan(data, new Places(data.Places), maxBytes: 10, sizeOf: _ => 1000, culture: English);

        Assert.Equal(2, plan.Count);
    }

    [Fact]
    public void An_empty_collection_has_one_empty_part()
    {
        var plan = BackupParts.Plan(new BackupData(), Places.None);

        Assert.Single(plan.Parts);
    }

    [Fact]
    public void Each_part_has_an_index_of_everything_and_links_only_to_its_own_pages()
    {
        var data = Collection(out var monstera, out _, out var cutting);
        var fern = Plant("Fern", Hall);
        data.Plants = [monstera, fern];
        data.Photos = [PhotoOf(monstera, 1), PhotoOf(fern, 1)];
        var plan = BackupParts.Plan(data, new Places(data.Places), maxBytes: 150, sizeOf: _ => 100, culture: English);
        Assert.True(plan.Count >= 2);

        var partOfFern = plan.PartOf(fern.Id)!.Value;
        var other = partOfFern == 1 ? 2 : 1;

        var inFern = Build(data, partOfFern, plan);
        var inOther = Build(data, other, plan);

        // Both indexes list everything. A record in another part has no link and says where it is.
        Assert.Contains("Fern", inOther["index.html"]);
        Assert.Contains("Monstera", inFern["index.html"]);
        Assert.Contains($"in part {partOfFern}", inOther["index.html"]);
        var fernLink = $"href=\"#{ReadablePages.PlantAnchor(fern.Id)}\"";
        Assert.DoesNotContain(fernLink, inOther["index.html"]);
        Assert.Contains(fernLink, inFern["index.html"]);
        Section(inFern["index.html"], ReadablePages.PlantAnchor(fern.Id));
        Assert.DoesNotContain($"id=\"{ReadablePages.PlantAnchor(fern.Id)}\"", inOther["index.html"]);
        Assert.Contains($"part {other} of {plan.Count}", inOther["index.html"]);
    }

    [Fact]
    public void Part_files_are_named_with_their_number_and_small_before_the_date()
    {
        var day = new DateOnly(2026, 11, 22);

        Assert.Equal("stikling-backup-2026-11-22-part-1-of-3.zip", BackupParts.FileName("stikling-backup", day, 1, 3));
        Assert.Equal("stikling-backup-2026-11-22.zip", BackupParts.FileName("stikling-backup", day, 1, 1));
        Assert.Equal("stikling-backup-small-2026-11-22.zip", BackupParts.FileName("stikling-backup", day, 1, 1, small: true));
    }

    [Fact]
    public async Task A_zip_with_the_pages_still_reads_back_from_data_json_and_keeps_the_format_version()
    {
        // BackupService lives in the web project, so this reads the ZIP the way its ReadAsync does: data.json only, by name.
        // Older versions refuse a newer Version, so the pages must not change it.
        var data = Collection(out var monstera, out _, out _);
        var pages = Build(data);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using (var entry = zip.CreateEntry("data.json").Open())
                await JsonSerializer.SerializeAsync(entry, data, json);
            foreach (var (path, content) in pages)
            {
                await using var entry = zip.CreateEntry(path).Open();
                await entry.WriteAsync(new UTF8Encoding(false).GetBytes(content));
            }
        }

        buffer.Position = 0;
        using var read = new ZipArchive(buffer, ZipArchiveMode.Read);
        await using var stream = read.GetEntry("data.json")!.Open();
        var restored = await JsonSerializer.DeserializeAsync<BackupData>(stream, json);

        Assert.Equal(1, BackupData.CurrentVersion);
        Assert.Equal(BackupData.CurrentVersion, restored!.Version);
        Assert.Equal(2, restored.Plants.Count);
        Assert.Contains(restored.Plants, p => p.Id == monstera.Id);
    }
}
