using Stikling.Core.Models;
using Stikling.Core.Sync;

namespace Stikling.Core.Files;

public interface IFileRepository : IRepository<AttachedFile>
{
}

/// <summary>
/// The files attached to plants, propagations and orders. This keeps the records. The bytes are
/// kept on the device by the file's id, and the device side frees them when a record is deleted.
/// </summary>
public sealed class FileService(IFileRepository files, TimeProvider time)
{
    /// <summary>The files on one plant, propagation or order, newest first.</summary>
    public async Task<IReadOnlyList<AttachedFile>> ForSubjectAsync(FileSubject subjectType, Guid subjectId) =>
    [
        .. (await files.GetAllAsync())
            .Where(f => f.SubjectType == subjectType && f.SubjectId == subjectId)
            .OrderByDescending(f => f.AddedAt)
            .ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
    ];

    /// <summary>How many more files fit on this plant, propagation or order. A merge can leave more than the limit, which gives 0.</summary>
    public async Task<int> RoomForAsync(FileSubject subjectType, Guid subjectId) =>
        Math.Max(0, FileRules.MaxFilesPerRecord - (await ForSubjectAsync(subjectType, subjectId)).Count);

    /// <summary>True when <paramref name="count"/> more files fit on this plant, propagation or order.</summary>
    public async Task<bool> CanAddAsync(FileSubject subjectType, Guid subjectId, int count = 1) =>
        count <= await RoomForAsync(subjectType, subjectId);

    /// <summary>
    /// Writes the record of a file whose bytes are already kept here under <paramref name="id"/>.
    /// The name is trimmed and cut to the longest allowed, and a blank one becomes "File".
    /// Throws when there is no room, since only adding is limited.
    /// </summary>
    public async Task<AttachedFile> AddAsync(FileSubject subjectType, Guid subjectId, Guid id, string name, string contentType, long size)
    {
        if (!await CanAddAsync(subjectType, subjectId))
            throw new InvalidOperationException($"A file can only be added to a record that has fewer than {FileRules.MaxFilesPerRecord}.");

        var file = new AttachedFile
        {
            Id = id,
            SubjectType = subjectType,
            SubjectId = subjectId,
            Name = CleanName(name) ?? "File",
            ContentType = contentType,
            Size = size,
            AddedAt = time.GetUtcNow()
        };
        await files.SaveAsync(file);
        return file;
    }

    /// <summary>
    /// Gives a file a new name, trimmed. False, with nothing changed, when the name is empty, is longer
    /// than <see cref="AttachedFile.MaxNameLength"/> or the file isn't there.
    /// </summary>
    public async Task<bool> RenameAsync(Guid id, string? name)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0 || trimmed.Length > AttachedFile.MaxNameLength)
            return false;
        if (await files.GetAsync(id) is not { } file)
            return false;

        if (file.Name != trimmed)
        {
            file.Name = trimmed;
            await files.SaveAsync(file);
        }
        return true;
    }

    /// <summary>Soft deletes the record. The bytes on the device are freed when the record is saved deleted, in the device's database.</summary>
    public Task DeleteAsync(Guid id) => files.DeleteAsync(id);

    private static string? CleanName(string? name)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            return null;
        return trimmed.Length > AttachedFile.MaxNameLength ? trimmed[..AttachedFile.MaxNameLength].TrimEnd() : trimmed;
    }
}
