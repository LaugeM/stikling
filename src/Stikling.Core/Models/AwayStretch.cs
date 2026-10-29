using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// A stretch of days set aside, either away from home or as a break from the app. While one is
/// on, Today stops asking for anything. It belongs to the collection.
/// </summary>
public sealed class AwayStretch : Entity
{
    /// <summary>The first day of the stretch.</summary>
    public DateOnly From { get; set; }

    /// <summary>The day Today asks again, i.e. the day back. The same meaning as <see cref="PutOff.Until"/>.</summary>
    public DateOnly Until { get; set; }

    public AwayKind Kind { get; set; } = AwayKind.Away;

    /// <summary>True on the days from <see cref="From"/> up to, but not including, <see cref="Until"/>.</summary>
    public bool IsOn(DateOnly day) => From <= day && day < Until;

    public IReadOnlyList<string> Validate() =>
        Until > From ? [] : [Strings.Care.AwayBackAfterFirst];
}
