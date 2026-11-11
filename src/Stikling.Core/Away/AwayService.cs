using Stikling.Core.Models;

namespace Stikling.Core.Away;

public interface IAwayStretchRepository : IRepository<AwayStretch>
{
}

/// <summary>The stretches set aside for being away or taking a break from the app.</summary>
public sealed class AwayService(IAwayStretchRepository stretches)
{
    public Task<IReadOnlyList<AwayStretch>> GetAllAsync() => stretches.GetAllAsync();

    public Task SaveAsync(AwayStretch stretch) => stretches.SaveAsync(stretch);

    public Task DeleteAsync(Guid id) => stretches.DeleteAsync(id);

    /// <summary>
    /// "I'm back": ends the stretch on today, and any joined to it, so Today asks again. One that
    /// would be left with no days is deleted, e.g. one that was to start tomorrow.
    /// </summary>
    public async Task ImBackAsync(DateOnly today)
    {
        var all = await stretches.GetAllAsync();
        if (AwayTime.Current(all, today) is not { } span)
            return;

        foreach (var stretch in span.Stretches.Where(s => s.Until > today))
        {
            stretch.Until = today;
            if (stretch.Until <= stretch.From)
                await stretches.DeleteAsync(stretch.Id);
            else
                await stretches.SaveAsync(stretch);
        }
    }
}
