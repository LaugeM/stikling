namespace Stikling.Core.Calendar;

/// <summary>
/// The kinds of things the calendar link can show, as the keys kept in <c>UserSettings.CalendarLeaveOut</c>.
/// Plain strings, so a key added later never stops an older version of the app reading the settings.
/// </summary>
public static class CalendarParts
{
    /// <summary>Going round a room.</summary>
    public const string Round = "round";

    /// <summary>The soil checks.</summary>
    public const string Soil = "soil";

    /// <summary>Care on an interval, like feeding and repotting.</summary>
    public const string Care = "care";

    /// <summary>Pest treatments, checks and plans.</summary>
    public const string Pests = "pests";

    /// <summary>Looking plants over again, flags, quarantine, transition and rest.</summary>
    public const string Plants = "plants";

    /// <summary>Propagation checks.</summary>
    public const string Propagations = "propagations";

    /// <summary>The photo round.</summary>
    public const string Photos = "photos";

    public static readonly IReadOnlyList<string> All = [Round, Soil, Care, Pests, Plants, Propagations, Photos];
}
