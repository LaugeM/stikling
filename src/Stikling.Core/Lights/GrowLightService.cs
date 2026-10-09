using Stikling.Core.Models;

namespace Stikling.Core.Lights;

/// <summary>Grow lights, and the quick way of saying a room or spot has one.</summary>
public sealed class GrowLightService(IGrowLightRepository lights)
{
    /// <summary>The name given to a light added by saying a place is lit.</summary>
    public const string DefaultName = "Grow light";

    public Task<IReadOnlyList<GrowLight>> GetAllAsync() => lights.GetAllAsync();

    /// <summary>Saves a light with its name trimmed. Throws if it has no name.</summary>
    public async Task SaveAsync(GrowLight light)
    {
        light.Name = light.Name?.Trim() ?? "";
        light.Notes = string.IsNullOrWhiteSpace(light.Notes) ? null : light.Notes.Trim();
        var errors = light.Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException(errors[0]);
        await lights.SaveAsync(light);
    }

    public Task DeleteAsync(Guid id) => lights.DeleteAsync(id);

    /// <summary>
    /// Says a room or spot is lit or not. Turning it on adds one light there if there is none, and
    /// turning it off removes every light pointing at it, since one left behind would keep it lit.
    /// </summary>
    public async Task SetLitAsync(Guid placeId, bool lit)
    {
        var here = (await lights.GetAllAsync()).Where(l => !l.IsDeleted && l.PlaceId == placeId).ToList();
        if (lit)
        {
            if (here.Count == 0)
                await SaveAsync(new GrowLight { Name = DefaultName, PlaceId = placeId });
            return;
        }

        foreach (var light in here)
            await lights.DeleteAsync(light.Id);
    }
}
