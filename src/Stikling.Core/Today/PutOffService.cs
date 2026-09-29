using Stikling.Core.Models;

namespace Stikling.Core.Today;

public interface IPutOffRepository
{
    Task<IReadOnlyList<PutOff>> GetAllAsync();

    Task<PutOff?> GetAsync(Guid id);

    Task SaveAsync(PutOff putOff);
}

/// <summary>What has been put off on Today.</summary>
public sealed class PutOffService(IPutOffRepository putOffs)
{
    /// <summary>What is still put off on the given day.</summary>
    public async Task<PutOffs> GetAsync(DateOnly today) =>
        PutOffs.From(await putOffs.GetAllAsync(), today);

    /// <summary>Hides the thing under the key until the given day, when it shows again.</summary>
    public async Task PutOffAsync(string key, DateOnly until)
    {
        var putOff = await putOffs.GetAsync(PutOff.IdFor(key)) ?? new PutOff { Id = PutOff.IdFor(key), Key = key };
        putOff.Until = until;
        await putOffs.SaveAsync(putOff);
    }
}
