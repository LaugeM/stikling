using Stikling.Core.Care;
using Stikling.Core.Models;

namespace Stikling.Core.Today;

/// <summary>
/// What happens when someone answers a plant that may need water, on Today and on a round:
/// it was watered, here is a meter reading, or it isn't ready yet.
/// </summary>
public sealed class WateringAnswers(
    CareService care, ICareLogRepository logs, PutOffService putOffs, TimeProvider time)
{
    /// <summary>Logs today's watering, or topping up for a plant that isn't top watered. Returns the entry.</summary>
    public async Task<CareLog> WaterAsync(Guid plantId, WateringForm form, Func<Enum, string> label)
    {
        var entry = care.Start(plantId, WateringForms.DoneKind(form));
        await care.LogAsync(entry, label);
        return entry;
    }

    /// <summary>
    /// Logs a meter reading for today. When <paramref name="replacing"/> is a reading logged
    /// earlier, that entry gets the new number instead of a second one being added, so changing
    /// the number on a card doesn't leave two readings.
    /// </summary>
    public async Task<CareLog> ReadingAsync(
        Guid plantId, int reading, Guid? replacing, Func<Enum, string> label)
    {
        if (replacing is { } id
            && await logs.GetAsync(id) is { IsDeleted: false, Kind: CareKind.MoistureReading } earlier
            && earlier.PlantId == plantId)
        {
            earlier.Moisture = reading;
            await logs.SaveAsync(earlier);
            return earlier;
        }

        var entry = care.Start(plantId, CareKind.MoistureReading);
        entry.Moisture = reading;
        await care.LogAsync(entry, label);
        return entry;
    }

    /// <summary>
    /// "Still wet" with a meter reading, or "No meter" / "Not yet" with none. The reading is logged
    /// when <paramref name="logReading"/> is set, and not when the caller has logged it already.
    /// A reading moves a guess that is made from the meter. For one made from the days between
    /// waterings it says nothing new, so it waits a while like the other answers. Returns the
    /// key and the day it was put off until, so the page can keep its own copy up to date, or null
    /// when nothing was put off.
    /// </summary>
    public async Task<(string Key, DateOnly Until)?> NotYetAsync(
        Plant plant, WateringGuess guess, int? reading, bool logReading, Func<Enum, string> label)
    {
        if (logReading && reading is { } value)
        {
            var entry = care.Start(plant.Id, CareKind.MoistureReading);
            entry.Moisture = value;
            await care.LogAsync(entry, label);
        }

        if (reading is not null && guess.Mode == WateringMode.Meter)
            return null;

        var key = PutOffs.Water(plant.Id, guess.LastWatered);
        var until = time.Today().AddDays(WateringGuess.StillWetWait(guess.UsualDays ?? 1));
        await putOffs.PutOffAsync(key, until);
        return (key, until);
    }
}
