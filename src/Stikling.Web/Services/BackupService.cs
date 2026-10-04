using System.IO.Compression;
using System.Text.Json;
using Stikling.Core.Backup;
using Stikling.Core.Models;
using Stikling.Core.Sync;

namespace Stikling.Web.Services;

/// <summary>What a restore did, for the line shown afterwards.</summary>
/// <param name="BroughtBack">Things deleted on this device that the backup still had.</param>
/// <param name="Photos">Images put back because they were missing here.</param>
public sealed record ImportSummary(int Added, int Updated, int BroughtBack, int Kept, int Photos)
{
    /// <summary>True when any records changed. Photos alone can come back without one.</summary>
    public bool ChangedRecords => Added > 0 || Updated > 0 || BroughtBack > 0;
}

/// <summary>
/// Backup to a ZIP file and back again. The ZIP holds data.json with everything the app
/// stores, plus the photos as ordinary .webp or .jpg files, whichever each was saved as, so the pictures are usable on their own.
/// </summary>
public sealed class BackupService(IndexedDb db, PhotoService photos, SyncRunner sync, DeviceFiles files, TimeProvider time)
{
    internal const string DataFile = "data.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    /// <summary>Raised after a restore, so what the layout shows (like the Pests tab) can look again.</summary>
    public event Action? Restored;

    /// <summary>Builds the backup and hands it to the browser as a download.</summary>
    public async Task<BackupCounts> ExportAsync()
    {
        // Deleted items travel too, so a restore doesn't bring back what was deleted elsewhere
        var data = new BackupData
        {
            ExportedAt = time.GetUtcNow(),
            Plants = await db.GetAllAsync<Plant>(Stores.Plants),
            Propagations = await db.GetAllAsync<Propagation>(Stores.Propagations),
            Timeline = await db.GetAllAsync<TimelineEntry>(Stores.Timeline),
            Photos = await db.GetAllAsync<Photo>(Stores.Photos),
            CareLogs = await db.GetAllAsync<CareLog>(Stores.CareLogs),
            PestCases = await db.GetAllAsync<PestCase>(Stores.PestCases),
            PestTreatments = await db.GetAllAsync<PestTreatment>(Stores.PestTreatments),
            Pots = await db.GetAllAsync<Pot>(Stores.Pots),
            SoilMixes = await db.GetAllAsync<SoilMix>(Stores.SoilMixes),
            Products = await db.GetAllAsync<Product>(Stores.Products),
            Feeds = await db.GetAllAsync<Feed>(Stores.Feeds),
            TreatmentRecipes = await db.GetAllAsync<TreatmentRecipe>(Stores.TreatmentRecipes),
            Places = await db.GetAllAsync<Place>(Stores.Places),
            Settings = await db.GetAllAsync<UserSettings>(Stores.Settings),
            PutOffs = await db.GetAllAsync<PutOff>(Stores.PutOffs)
        };

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using (var entry = zip.CreateEntry(DataFile, CompressionLevel.SmallestSize).Open())
                await JsonSerializer.SerializeAsync(entry, data, Json);

            foreach (var photo in data.Photos.Where(p => !p.IsDeleted))
            {
                // On a synced device, a photo from another device only has its full size here once opened
                if (!await photos.HasBytesAsync(photo.Id))
                    await sync.FetchPhotoAsync(photo.Id, PhotoSize.Full);

                await AddPhotoAsync(zip, photo.Id, thumbnail: false);
                await AddPhotoAsync(zip, photo.Id, thumbnail: true);
            }
        }

        var today = time.Today();
        await files.DownloadAsync($"stikling-backup-{today:yyyy-MM-dd}.zip", buffer.ToArray());
        await files.SetLastBackupAsync(today);

        return data.Counts;
    }

    /// <summary>Reads a backup without changing anything, so it can be described before restoring.</summary>
    public static async Task<BackupData> ReadAsync(Stream zipStream)
    {
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        return await ReadAsync(zip);
    }

    private static async Task<BackupData> ReadAsync(ZipArchive zip)
    {
        var entry = zip.GetEntry(DataFile)
            ?? throw new InvalidOperationException("This doesn't look like a Stikling backup: data.json is missing.");

        await using var stream = entry.Open();
        var data = await JsonSerializer.DeserializeAsync<BackupData>(stream, Json)
            ?? throw new InvalidOperationException("The backup is empty.");

        if (data.Version > BackupData.CurrentVersion)
            throw new InvalidOperationException("This backup was made with a newer version of Stikling. Update the app and try again.");

        return data;
    }

    /// <summary>
    /// Restores a backup. Anything already here is kept unless the backup has a newer version
    /// of it, so restoring onto a device that has been used since doesn't lose anything.
    /// Things deleted here since the backup was made come back.
    /// </summary>
    public async Task<ImportSummary> ImportAsync(Stream zipStream)
    {
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        var data = await ReadAsync(zip);

        // One tally for every store, so adding a store is one line instead of four sums to keep in step
        var added = 0;
        var updated = 0;
        var broughtBack = 0;
        var kept = 0;

        async Task<MergeResult<T>> Restore<T>(string store, List<T> incoming) where T : Entity
        {
            var result = await MergeAsync(store, incoming);
            added += result.Added;
            updated += result.Updated;
            broughtBack += result.BroughtBack;
            kept += result.Skipped;
            return result;
        }

        await Restore(Stores.Plants, data.Plants);
        await Restore(Stores.Propagations, data.Propagations);
        await Restore(Stores.Timeline, data.Timeline);
        var photoMeta = await Restore(Stores.Photos, data.Photos);
        await Restore(Stores.CareLogs, data.CareLogs);
        await Restore(Stores.PestCases, data.PestCases);
        await Restore(Stores.PestTreatments, data.PestTreatments);
        await Restore(Stores.Pots, data.Pots);
        await Restore(Stores.SoilMixes, data.SoilMixes);
        await Restore(Stores.Products, data.Products);
        await Restore(Stores.Feeds, data.Feeds);
        await Restore(Stores.TreatmentRecipes, data.TreatmentRecipes);
        await Restore(Stores.Places, data.Places);
        await Restore(Stores.Settings, data.Settings);
        await Restore(Stores.PutOffs, data.PutOffs);

        // A delete carried over from the backup frees the image too
        foreach (var photo in photoMeta.ToSave.Where(p => p.IsDeleted))
            await photos.RemoveBytesAsync(photo.Id);

        // Every photo in the backup gets its image back when it's missing here, not only the ones
        // saved just now. A restore that stopped halfway, e.g. when storage ran out, has already
        // saved the photo details, so restoring again would otherwise leave those photos blank.
        // A photo the backup has is never deleted here after the merge, since it comes back.
        var restoredPhotos = 0;
        foreach (var photo in BackupMerge.Newest(data.Photos).Where(p => !p.IsDeleted))
        {
            if (await photos.HasBytesAsync(photo.Id))
                continue;
            if (await ReadPhotoAsync(zip, photo.Id, thumbnail: false) is not { } full)
                continue;
            var thumbnail = await ReadPhotoAsync(zip, photo.Id, thumbnail: true);
            await photos.PutBytesAsync(photo.Id, full, thumbnail);
            restoredPhotos++;
        }

        Restored?.Invoke();
        return new ImportSummary(added, updated, broughtBack, kept, restoredPhotos);
    }

    private async Task<MergeResult<T>> MergeAsync<T>(string store, List<T> incoming) where T : Entity
    {
        var result = BackupMerge.Merge(await db.GetAllAsync<T>(store), incoming);
        foreach (var item in result.ToSave)
            await db.PutAsync(store, item);
        return result;
    }

    private async Task AddPhotoAsync(ZipArchive zip, Guid id, bool thumbnail)
    {
        if (await photos.GetBytesAsync(id, thumbnail) is not { } bytes)
            return;

        // WebP and JPEG data is already compressed; packing it again only costs time
        var extension = (PhotoRules.FormatOf(bytes) ?? ImageFormat.Jpeg).Extension;
        await using var entry = zip.CreateEntry(PhotoPath(id, thumbnail, extension), CompressionLevel.NoCompression).Open();
        await entry.WriteAsync(bytes);
    }

    private static async Task<byte[]?> ReadEntryAsync(ZipArchive zip, string path)
    {
        if (zip.GetEntry(path) is not { } entry)
            return null;

        using var buffer = new MemoryStream();
        await using (var stream = entry.Open())
            await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    /// <summary>The photo's file in the backup, whichever of the two formats it was saved as. Older backups only have .jpg.</summary>
    private static async Task<byte[]?> ReadPhotoAsync(ZipArchive zip, Guid id, bool thumbnail) =>
        await ReadEntryAsync(zip, PhotoPath(id, thumbnail, ImageFormat.WebP.Extension))
        ?? await ReadEntryAsync(zip, PhotoPath(id, thumbnail, ImageFormat.Jpeg.Extension));

    private static string PhotoPath(Guid id, bool thumbnail, string extension) =>
        thumbnail ? $"photos/{id}-small.{extension}" : $"photos/{id}.{extension}";
}
