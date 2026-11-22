using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Stikling.Core.Backup;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
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

/// <summary>What a backup made: what it holds, how many ZIP files it was split into, and whether it was made without the full size photos.</summary>
/// <param name="Folder">The name of the folder the backup was saved in, or null when it was downloaded.</param>
/// <param name="FirstFile">The name the first file was saved or downloaded as, which is what the folder gave it when an old one was already there.</param>
/// <param name="FolderFailed">True when a folder is set but couldn't be written to, so the backup was downloaded instead.</param>
public sealed record BackupExport(BackupCounts Counts, int Parts, bool Small, string? Folder = null, bool FolderFailed = false, string? FirstFile = null);

/// <summary>How many photos went into the photo folders download, and how many couldn't be found on this device.</summary>
public sealed record PhotoFolderSummary(int Added, int Skipped);

/// <summary>
/// Backup to a ZIP file and back again. The ZIP holds data.json with everything the app
/// stores, plus the photos as ordinary .webp or .jpg files, whichever each was saved as, so the pictures are usable on their own,
/// and the attached files under files/ as the PDFs and images they are.
/// </summary>
public sealed class BackupService(IndexedDb db, PhotoService photos, AttachmentService attachments, SyncRunner sync, DeviceFiles files, BackupFolder folder, TimeProvider time)
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

    /// <summary>
    /// Builds the backup and hands it to the browser as a download, named with the prefix and today's date. What goes in it
    /// follows the choices on the backup page (<see cref="BackupOptions"/>), which are kept on the device. A collection with
    /// more photos than <see cref="BackupParts.MaxPartBytes"/> is saved as several ZIP files, each a whole backup.
    /// </summary>
    /// <param name="allowSmall">False for a backup that is a safety net, like the one before a restore, which is always whole.</param>
    public async Task<BackupExport> ExportAsync(string filePrefix = "stikling-backup", bool allowSmall = true)
    {
        // Before anything slow: the browser only gives the permission to write to the folder back on a tap, and the
        // tap stops counting after a few seconds, which building the ZIP takes longer than
        var useFolder = false;
        var folderFailed = false;
        if (await folder.GetNameAsync() is not null)
        {
            useFolder = await folder.RequestPermissionAsync();
            folderFailed = !useFolder;
        }

        var options = await files.GetBackupOptionsAsync();
        var small = options.Small && allowSmall;
        var savedInFolder = false;
        string? firstFile = null;

        // Deleted items travel too, so a restore doesn't bring back what was deleted elsewhere
        var data = new BackupData { ExportedAt = time.GetUtcNow() };
        foreach (var list in BackupData.Lists)
            await list.ExportAsync(data, db);

        var culture = CultureInfo.CurrentUICulture;
        var places = new Places(data.Places);
        var plan = small
            ? BackupParts.Plan(data, places, SmallPartBytes, _ => SmallPhotoBytes, culture)
            : BackupParts.Plan(data, places, culture: culture);
        var covers = options.Readable ? await ThumbnailUrisAsync(ReadableIndex.CoverPhotoIds(data).Values) : new Dictionary<Guid, string>();
        var today = time.Today();

        foreach (var part in plan.Parts)
        {
            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                await using (var entry = zip.CreateEntry(DataFile, CompressionLevel.SmallestSize).Open())
                    await JsonSerializer.SerializeAsync(entry, data, Json);

                var partPhotoIds = new List<Guid>();
                foreach (var photo in data.Photos.Where(p => !p.IsDeleted && part.PhotoIds.Contains(p.Id)))
                {
                    // Only photos of plants and cuttings are shown in the readable file
                    if (photo.SubjectType is SubjectType.Plant or SubjectType.Propagation)
                        partPhotoIds.Add(photo.Id);
                    if (small)
                    {
                        await AddPhotoAsync(zip, photo.Id, thumbnail: true);
                        continue;
                    }

                    // On a synced device, a photo from another device only has its full size here once opened
                    if (!await photos.HasBytesAsync(photo.Id))
                        await sync.FetchPhotoAsync(photo.Id, PhotoSize.Full);

                    await AddPhotoAsync(zip, photo.Id, thumbnail: false);
                    await AddPhotoAsync(zip, photo.Id, thumbnail: true);
                }

                foreach (var file in data.Files.Where(f => !f.IsDeleted && part.FileIds.Contains(f.Id)))
                {
                    // A file added on another device only has its bytes here once it has been opened. One that
                    // still can't be had is left out, and the record stays in data.json.
                    if (!await attachments.HasBytesAsync(file.Id))
                        await sync.FetchFileAsync(file.Id);

                    await AddFileAsync(zip, file.Id);
                }

                if (options.Readable)
                {
                    // The readable file is opened on its own on a phone, so its pictures are inside it
                    var images = new Dictionary<Guid, string>(covers);
                    foreach (var (id, uri) in await ThumbnailUrisAsync(partPhotoIds.Where(id => !covers.ContainsKey(id))))
                        images[id] = uri;

                    var pageOptions = new ReadableOptions
                    {
                        Label = Labels.For,
                        EnglishLabel = Labels.English,
                        PropagationLabel = Labels.For,
                        Date = Labels.Date,
                        LooseDate = Labels.Date,
                        Zone = time.LocalTimeZone,
                        Culture = culture,
                        Today = today,
                        SmallPhotos = small
                    };
                    foreach (var page in ReadablePages.Build(data, places, pageOptions, plan, part.Number, images))
                    {
                        await using var entry = zip.CreateEntry(page.Path, CompressionLevel.Optimal).Open();
                        await entry.WriteAsync(new UTF8Encoding(false).GetBytes(page.Content));
                    }
                }
            }

            var fileName = BackupParts.FileName(filePrefix, today, part.Number, plan.Count, small);
            var bytes = buffer.ToArray();
            if (useFolder)
            {
                if (await folder.WriteAsync(fileName, bytes) is { } writtenAs)
                {
                    firstFile ??= writtenAs;
                    savedInFolder = true;
                    continue;
                }

                // The folder was removed or the browser said no, so this part and the rest are downloaded
                useFolder = false;
                folderFailed = true;
            }
            firstFile ??= fileName;
            await files.DownloadAsync(fileName, bytes);
        }

        // A backup without the full size photos can't bring them back, so it doesn't count as the last one
        if (!small)
            await files.SetLastBackupAsync(today);

        return new BackupExport(data.Counts, plan.Count, small, savedInFolder ? await folder.GetNameAsync() : null, folderFailed, firstFile);
    }

    // What a thumbnail is counted as when working out the parts of a backup that only has those
    private const long SmallPhotoBytes = 40_000;

    // The part limit for a backup with only thumbnails. Every thumbnail in a part is also inline in its readable
    // file as base64 text, held several times over while the file is written (the strings, the page, its bytes),
    // so a part of thumbnails costs many times its size in memory where full size photos don't.
    private const long SmallPartBytes = 75_000_000;

    // Thumbnails as data addresses for the readable file. A photo without a thumbnail on this device is left out.
    private async Task<Dictionary<Guid, string>> ThumbnailUrisAsync(IEnumerable<Guid> photoIds)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var id in photoIds.Distinct())
        {
            if (await photos.GetBytesAsync(id, thumbnail: true) is not { } bytes)
                continue;
            var type = (PhotoRules.FormatOf(bytes) ?? ImageFormat.Jpeg).ContentType;
            result[id] = $"data:{type};base64,{Convert.ToBase64String(bytes)}";
        }
        return result;
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
                // A file the merge deleted frees its bytes when its record is written, in db.js
                counts += await list.RestoreAsync(data, db);
            }
        }

        // A delete carried over from the backup keeps its image like any other deleted photo: it is freed by the
        // start-up sweep (FreeOldDeletedAsync) once DeletedAt is more than 30 days old, so it can be put back until then

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

        // Likewise for the attached files whose bytes are missing here. They are listed to be sent to the server.
        // A file the backup has is never deleted here after the merge, since it comes back.
        foreach (var file in BackupMerge.Newest(data.Files).Where(f => !f.IsDeleted))
        {
            if (await attachments.HasBytesAsync(file.Id))
                continue;
            if (await ReadFileAsync(zip, file.Id) is not { } bytes)
                continue;
            await attachments.PutBytesAsync(file.Id, bytes);
        }

        Restored?.Invoke();
        return new ImportSummary(counts.Added, counts.Updated, counts.BroughtBack, counts.Skipped, restoredPhotos);
    }

    /// <summary>Adds the image and returns its path in the ZIP, or null when it isn't on this device.</summary>
    private async Task<string?> AddPhotoAsync(ZipArchive zip, Guid id, bool thumbnail)
    {
        if (await photos.GetBytesAsync(id, thumbnail) is not { } bytes)
            return null;

        // WebP and JPEG data is already compressed; packing it again only costs time
        var extension = (PhotoRules.FormatOf(bytes) ?? ImageFormat.Jpeg).Extension;
        var path = PhotoPath(id, thumbnail, extension);
        await using var entry = zip.CreateEntry(path, CompressionLevel.NoCompression).Open();
        await entry.WriteAsync(bytes);
        return path;
    }

    private async Task AddFileAsync(ZipArchive zip, Guid id)
    {
        if (await attachments.GetBytesAsync(id) is not { } bytes)
            return;

        // PDFs and images are already compressed
        var extension = (FileRules.FormatOf(bytes) ?? ImageFormat.Jpeg).Extension;
        await using var entry = zip.CreateEntry(FilePath(id, extension), CompressionLevel.NoCompression).Open();
        await entry.WriteAsync(bytes);
    }

    private static async Task<byte[]?> ReadFileAsync(ZipArchive zip, Guid id) =>
        await ReadEntryAsync(zip, FilePath(id, ImageFormat.Pdf.Extension))
        ?? await ReadEntryAsync(zip, FilePath(id, ImageFormat.WebP.Extension))
        ?? await ReadEntryAsync(zip, FilePath(id, ImageFormat.Jpeg.Extension));

    private static string FilePath(Guid id, string extension) => $"files/{id}.{extension}";

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
