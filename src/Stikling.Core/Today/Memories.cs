using Stikling.Core.Models;

namespace Stikling.Core.Today;

/// <summary>An old photo of a plant from this day some time ago, with its newest one.</summary>
/// <param name="Months">How long ago the old photo was taken: 6, or 12 for each year.</param>
public sealed record Memory(Plant Plant, Photo Old, Photo Newest, int Months)
{
    public string Heading => Months switch
    {
        6 => "Six months ago today",
        12 => "A year ago today",
        _ => $"{Months / 12} years ago today"
    };
}

/// <summary>
/// The memory on Today: a plant with a photo from exactly this day a year (or two, or more) ago,
/// or six months ago, shown next to its newest one.
/// </summary>
public static class Memories
{
    /// <summary>The newest photo has to be at least this many days after the old one.</summary>
    public const int MinimumDaysApart = 30;

    /// <summary>
    /// One plant: the longest gap wins, then the plant with the most photos, then the name.
    /// Null when no plant has a photo from the right day.
    /// </summary>
    public static Memory? Pick(
        IEnumerable<Plant> plants,
        IEnumerable<Photo> photos,
        DateOnly today,
        Func<DateTimeOffset, DateOnly> localDay)
    {
        var byPlant = photos
            .Where(p => !p.IsDeleted && p.SubjectType == SubjectType.Plant)
            .GroupBy(p => p.SubjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var found = new List<(Memory Memory, int Count)>();
        foreach (var plant in plants)
        {
            if (plant.IsDeleted || plant.Status != PlantStatus.Active || !byPlant.TryGetValue(plant.Id, out var own))
                continue;

            var newest = own.OrderByDescending(p => p.TakenAt).First();
            Memory? best = null;
            foreach (var photo in own.OrderBy(p => p.TakenAt))
            {
                var months = MonthsAgo(localDay(photo.TakenAt), today);
                if (months is null || best is not null && best.Months >= months)
                    continue;
                if (photo.Id == newest.Id
                    || localDay(newest.TakenAt).DayNumber - localDay(photo.TakenAt).DayNumber < MinimumDaysApart)
                    continue;
                best = new Memory(plant, photo, newest, months.Value);
            }

            if (best is not null)
                found.Add((best, own.Count));
        }

        return found
            .OrderByDescending(f => f.Memory.Months)
            .ThenByDescending(f => f.Count)
            .ThenBy(f => f.Memory.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(f => f.Memory.Plant.Id)
            .Select(f => f.Memory)
            .FirstOrDefault();
    }

    // 12 per year back, or 6 for six months ago; null when the day isn't one of them
    private static int? MonthsAgo(DateOnly day, DateOnly today)
    {
        var years = today.Year - day.Year;
        if (years >= 1 && today.AddYears(-years) == day)
            return years * 12;
        return today.AddMonths(-6) == day ? 6 : null;
    }
}
