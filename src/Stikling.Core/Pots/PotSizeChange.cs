using Stikling.Core.Models;

namespace Stikling.Core.Pots;

/// <summary>
/// How much wider a new pot is across the top than the one before it. A wide pot keeps the soil
/// wet longer, so Pot up says so past a limit. It is only a note, any pot can still be picked.
/// </summary>
public sealed record PotSizeChange(decimal FromCm, decimal ToCm, bool Warn)
{
    /// <summary>The limit in cm when the person hasn't set one.</summary>
    public const decimal DefaultWarnCm = 5;

    /// <summary>The limit in force: the setting, or <see cref="DefaultWarnCm"/> when it's null.</summary>
    public static decimal WarnLimit(UserSettings settings) => settings.PotUpWarnCm ?? DefaultWarnCm;

    /// <summary>Null when either pot is missing or has no width.</summary>
    public static PotSizeChange? Between(Pot? from, Pot? to, decimal warnCm = DefaultWarnCm) =>
        from?.TopCm is { } a && to?.TopCm is { } b ? new(a, b, b - a > warnCm) : null;

    /// <summary>The note shown under the pot picker.</summary>
    public string Text =>
        $"From {Pot.Size(FromCm)} cm to {Pot.Size(ToCm)} cm across the top."
        + (Warn ? " That's a lot bigger, so the soil will stay wet longer. Water less until the roots fill it." : "");
}
