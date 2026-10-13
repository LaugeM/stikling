using System.Globalization;
using Stikling.Core.Feeds;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Products;
using Stikling.Core.Timeline;

namespace Stikling.Core.Care;

/// <summary>One plant watered, or topped up, on the watering screen, with the feed picked for it (or none).</summary>
public sealed record PassWatering(Guid PlantId, CareKind Kind, Feed? Feed);

/// <summary>
/// Writing to the care log. The entry is always saved; the plant's history only gets a line
/// for the notable kinds, so a weekly watering doesn't bury the photos and notes.
/// </summary>
public sealed class CareService(
    ICareLogRepository logs,
    ITimelineRepository timeline,
    IPlantRepository plants,
    IFeedRepository feeds,
    IProductRepository products,
    TimeProvider time)
{
    private DateOnly Today => time.Today();

    /// <summary>A blank entry for one plant, dated today.</summary>
    public CareLog Start(Guid plantId, CareKind kind = CareKind.Watered) =>
        new() { PlantId = plantId, Kind = kind, OccurredOn = Today };

    /// <summary>
    /// Saves one care entry and, for the notable kinds, records it on the plant's history. A
    /// watering of a plant set to get a feed with the water also logs the feed, when it is due.
    /// </summary>
    public async Task LogAsync(CareLog entry, Func<Enum, string> label)
    {
        // The watering is what was done, so a feed entry that doesn't pass is left out rather than stopping it
        var extra = await PlanFeedAsync(entry, null);
        if (extra is not null && extra.Validate(Today).Count > 0)
            extra = null;

        await logs.SaveAsync(entry);
        await RecordAsync(entry, label);
        if (extra is not null)
        {
            await logs.SaveAsync(extra);
            await RecordAsync(extra, label);
        }
    }

    /// <summary>
    /// The same thing done to several plants at once, e.g. "fertilised all the semi-hydro
    /// plants". Each plant gets its own entry so its own history stays right.
    /// </summary>
    public async Task<int> LogManyAsync(
        IEnumerable<Guid> plantIds,
        CareKind kind,
        DateOnly occurredOn,
        string? notes,
        Func<Enum, string> label,
        int? moisture = null,
        IReadOnlyList<ProductDose>? products = null,
        Feed? feed = null,
        decimal? waterLitres = null,
        decimal? ec = null,
        decimal? ph = null)
    {
        var used = Tidy(products);
        var entries = plantIds
            .Distinct()
            .Select(id => new CareLog
            {
                PlantId = id,
                Kind = kind,
                OccurredOn = occurredOn,
                Moisture = moisture,
                // Each entry gets its own copy, so changing one later can't change the others
                Products = [.. used.Select(p => p.Copy())],
                FeedId = feed?.Id,
                FeedName = Clean(feed?.Name),
                WaterLitres = waterLitres,
                Ec = ec,
                Ph = ph,
                Notes = Clean(notes)
            })
            .ToList();

        // A plant set to get a feed with the watering gets it as well, by its own setting
        var all = IsWatering(kind) ? await logs.GetAllAsync() : null;
        var extras = new List<CareLog>();
        foreach (var entry in entries)
            if (await PlanFeedAsync(entry, all) is { } extra && extra.Validate(Today).Count == 0)
                extras.Add(extra);

        // Check everything before saving anything, the way potting up does
        if (entries.Concat(extras).SelectMany(e => e.Validate(Today)).FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);

        foreach (var entry in entries)
        {
            await logs.SaveAsync(entry);
            await RecordAsync(entry, label);
            foreach (var extra in extras.Where(x => x.PlantId == entry.PlantId))
            {
                await logs.SaveAsync(extra);
                await RecordAsync(extra, label);
            }
        }

        return entries.Count;
    }

    /// <summary>
    /// The waterings from the watering screen, one entry per plant dated today. The feed is the
    /// one picked on the screen, and no feed means none, even for a plant set to get its feed with
    /// the watering, so this doesn't plan a feed of its own. With a feed, a watering gets a separate
    /// Fertilised entry and a topping up takes the products itself. Everything is checked before
    /// anything is saved. Returns how many plants were logged.
    /// </summary>
    public async Task<int> LogWateringsAsync(IReadOnlyList<PassWatering> items, Func<Enum, string> label)
    {
        var allProducts = items.Any(i => i.Feed is { Products.Count: > 0 }) ? await products.GetAllAsync() : [];

        var planned = new List<(CareLog Entry, CareLog? Extra)>();
        foreach (var item in items)
        {
            var entry = Start(item.PlantId, item.Kind);
            CareLog? extra = null;

            if (item.Feed is { Products.Count: > 0 } feed)
            {
                var doses = feed.Doses(allProducts);
                if (item.Kind == CareKind.ToppedUp)
                {
                    entry.Products = doses;
                    entry.FeedId = feed.Id;
                    entry.FeedName = Clean(feed.Name);
                }
                else if (item.Kind == CareKind.Watered)
                {
                    extra = new CareLog
                    {
                        PlantId = item.PlantId,
                        Kind = CareKind.Fertilised,
                        OccurredOn = entry.OccurredOn,
                        Products = doses,
                        FeedId = feed.Id,
                        FeedName = Clean(feed.Name)
                    };
                }
            }

            planned.Add((entry, extra));
        }

        var errors = planned
            .SelectMany(p => p.Entry.Validate(Today).Concat(p.Extra?.Validate(Today) ?? []))
            .ToList();
        if (items.Any(i => !IsWatering(i.Kind)))
            errors.Insert(0, "Only a watering or a topping up can be logged here.");
        if (errors.FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);

        foreach (var (entry, extra) in planned)
        {
            await logs.SaveAsync(entry);
            await RecordAsync(entry, label);
            if (extra is not null)
            {
                await logs.SaveAsync(extra);
                await RecordAsync(extra, label);
            }
        }

        return planned.Count;
    }

    public Task DeleteAsync(Guid id) => logs.DeleteAsync(id);

    private static bool IsWatering(CareKind kind) => kind is CareKind.Watered or CareKind.ToppedUp;

    /// <summary>
    /// For a new watering or topping up of a plant that gets a feed with the water, when the feed
    /// is due. A ToppedUp entry without products of its own gets the feed's products itself and
    /// null is returned. For Watered the separate Fertilised entry to save is returned.
    /// </summary>
    private async Task<CareLog?> PlanFeedAsync(CareLog entry, IReadOnlyList<CareLog>? all)
    {
        if (!IsWatering(entry.Kind))
            return null;
        if (await plants.GetAsync(entry.PlantId) is not { WateringFeedId: { } feedId } plant)
            return null;
        if (await feeds.GetAsync(feedId) is not { IsDeleted: false } feed || feed.Products.Count == 0)
            return null;

        all ??= await logs.GetAllAsync();
        // Saving an entry that was already logged isn't a new watering
        if (all.Any(l => l.Id == entry.Id))
            return null;
        if (!FeedWithWatering.IsDue(all.Where(l => l.PlantId == entry.PlantId), entry, plant.WateringFeedEvery))
            return null;

        if (entry.Kind == CareKind.ToppedUp)
        {
            if (entry.Products.Count == 0)
            {
                entry.Products = feed.Doses(await products.GetAllAsync());
                entry.FeedId = feed.Id;
                entry.FeedName = Clean(feed.Name);
            }
            return null;
        }

        return new CareLog
        {
            PlantId = entry.PlantId,
            Kind = CareKind.Fertilised,
            OccurredOn = entry.OccurredOn,
            Products = feed.Doses(await products.GetAllAsync()),
            FeedId = feed.Id,
            FeedName = Clean(feed.Name)
        };
    }

    /// <summary>
    /// "Repotted", "Watered: rainwater", "Fertilised: Hydro fertiliser, 2 ml/L · half strength"
    /// or "Fertilised with Aroid feed (2 L): Silica, 0.5 ml/L + Hydro fertiliser, 2 ml/L".
    /// </summary>
    public static string Describe(CareLog entry, Func<Enum, string> label)
    {
        var what = label(entry.Kind);
        if (entry.Kind == CareKind.MoistureReading && entry.Moisture is { } moisture)
            what = $"{what}: {moisture}/10";
        if (Clean(entry.FeedName) is { } feed)
            what = $"{what} with {feed}";
        if (entry.WaterLitres is { } litres)
            what = $"{what} ({Doses.Water(litres)})";

        var notes = Clean(entry.Notes);
        var readings = Readings(entry);
        if (entry.Products.Count == 0)
            return Join(notes is null ? what : $"{what}: {notes}", readings);

        var products = string.Join(" + ", entry.Products);
        return Join(notes is null ? $"{what}: {products}" : $"{what}: {products} · {notes}", readings);
    }

    /// <summary>"EC 1.2 · pH 6.0", or nothing when neither was measured.</summary>
    private static string? Readings(CareLog entry)
    {
        var parts = new List<string>();
        if (entry.Ec is { } ec)
            parts.Add($"EC {ec.ToString("0.0#", CultureInfo.InvariantCulture)}");
        if (entry.Ph is { } ph)
            parts.Add($"pH {ph.ToString("0.0#", CultureInfo.InvariantCulture)}");
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static string Join(string line, string? readings) =>
        readings is null ? line : $"{line} · {readings}";

    private Task RecordAsync(CareLog entry, Func<Enum, string> label)
    {
        if (!CareKinds.IsNotable(entry.Kind))
            return Task.CompletedTask;

        return timeline.AddAsync(new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = entry.PlantId,
            Kind = TimelineKind.Change,
            OccurredAt = time.MomentOn(entry.OccurredOn),
            Text = Describe(entry, label)
        });
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // Rows without a name are dropped rather than refused, the way a blank soil mix row is
    private static List<ProductDose> Tidy(IReadOnlyList<ProductDose>? products) =>
        [.. (products ?? []).Where(p => !string.IsNullOrWhiteSpace(p.Name)).Select(p =>
        {
            var copy = p.Copy();
            copy.Name = p.Name.Trim();
            return copy;
        })];
}
