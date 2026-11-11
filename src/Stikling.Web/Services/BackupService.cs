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

/// <summary>How many photos went into the photo folders download, and how many couldn't be found on this device.</summary>
public sealed record PhotoFolderSummary(int Added, int Skipped);

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

    /// <summary>
    /// True when the device has anything a restore could remove or overwrite, deleted records included.
    /// Only the kinds of record with <see cref="RecordKind.IsContent"/> count.
    /// </summary>
    public async Task<bool> HasAnythingToSaveAsync()
    {
        foreach (var kind in RecordKinds.All.Where(k => k.IsContent))
        {
            if ((await db.GetAllAsync<JsonElement>(kind.Name)).Count > 0)
                return true;
        }
        return false;
    }

    /// <summary>Builds the backup and hands it to the browser as a download, named with the prefix and today's date.</summary>
    public async Task<BackupCounts> ExportAsync(string filePrefix = "stikling-backup")
    {
        // Deleted items travel too, so a restore doesn't bring back what was deleted elsewhere
        var data = new BackupData { ExportedAt = time.GetUtcNow() };
        foreach (var list in BackupData.Lists)
            await list.ExportAsync(data, db);

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
        await files.DownloadAsync($"{filePrefix}-{today:yyyy-MM-dd}.zip", buffer.ToArray());
        await files.SetLastBackupAsync(today);

        return data.Counts;
    }

    /// <summary>
    /// Downloads every photo, full size, in a ZIP with a folder for each plant, propagation and pot.
    /// It is for looking through outside the app, so it is not a backup and doesn't count as one.
    /// </summary>
    public async Task<PhotoFolderSummary> DownloadPhotoFoldersAsync(PhotoFolderOptions options, IProgress<(int Done, int Total)>? progress = null)
    {
        var plan = PhotoFolders.Plan(
            await db.GetAllAsync(RecordKinds.Plants), await db.GetAllAsync(RecordKinds.Propagations),
            await db.GetAllAsync(RecordKinds.Pots), await db.GetAllAsync(RecordKinds.Photos), options, time.LocalTimeZone,
            new Stikling.Core.Rooms.Places(await db.GetAllAsync(RecordKinds.Places)));

        if (plan.Count == 0)
            return new PhotoFolderSummary(0, 0);

        var added = 0;
        var skipped = 0;
        progress?.Report((0, plan.Count));

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var done = 0;
            foreach (var (photoId, path) in plan)
            {
                if (!await photos.HasBytesAsync(photoId))
                    await sync.FetchPhotoAsync(photoId, PhotoSize.Full);

                if (await photos.GetBytesAsync(photoId, thumbnail: false) is { } bytes)
                {
                    // The format is only known from the bytes, since photos are WebP or JPEG
                    var extension = (PhotoRules.FormatOf(bytes) ?? ImageFormat.Jpeg).Extension;
                    await using var entry = zip.CreateEntry($"{path}.{extension}", CompressionLevel.NoCompression).Open();
                    await entry.WriteAsync(bytes);
                    added++;
                }
                else
                {
                    skipped++;
                }
                progress?.Report((++done, plan.Count));
            }
        }

        await files.DownloadAsync($"stikling-photos-{time.Today():yyyy-MM-dd}.zip", buffer.ToArray());
        return new PhotoFolderSummary(added, skipped);
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

        var counts = new MergeCounts();
        MergeResult<Photo>? photoMeta = null;
        foreach (var list in BackupData.Lists)
        {
            if (ReferenceEquals(list, BackupData.PhotoList))
            {
                photoMeta = await BackupData.PhotoList.MergeAsync(data, db);
                counts += photoMeta.Counts;
            }
            else
            {
                counts += await list.RestoreAsync(data, db);
            }
        }

        // A delete carried over from the backup frees the image too
        foreach (var photo in (photoMeta?.ToSave ?? []).Where(p => p.IsDeleted))
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
        return new ImportSummary(counts.Added, counts.Updated, counts.BroughtBack, counts.Skipped, restoredPhotos);
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
