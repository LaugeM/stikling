using Stikling.Core.Models;

namespace Stikling.Core.Away;

public interface ISitterSheetRepository
{
    Task<SitterSheet?> GetAsync(Guid id);

    Task SaveAsync(SitterSheet sheet);
}

/// <summary>The choices behind the plant-sitter sheet, kept as one record.</summary>
public sealed class SitterSheetService(ISitterSheetRepository sheets)
{
    /// <summary>The saved sheet, or an unsaved default when nothing has been chosen yet.</summary>
    public async Task<SitterSheet> GetAsync() =>
        await sheets.GetAsync(SitterSheet.SheetId) ?? new SitterSheet();

    public Task SaveAsync(SitterSheet sheet) => sheets.SaveAsync(sheet);
}
