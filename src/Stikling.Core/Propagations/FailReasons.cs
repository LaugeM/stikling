using Stikling.Core.Models;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Propagations;

/// <summary>The words for why units failed.</summary>
public static class FailReasons
{
    /// <summary>The reason as it reads in a history line: "2 failed: dried out". In English, since the line is saved as it was written.</summary>
    public static string Label(FailReason reason) => reason switch
    {
        FailReason.Rotted => "rotted",
        FailReason.DriedOut => "dried out",
        FailReason.Pests => "pests",
        FailReason.Mould => "mould",
        FailReason.NeverRooted => "never rooted",
        _ => "don't know"
    };

    /// <summary>The reason as a short name for what a medium loses cuttings to: "rot 5, dried out 2".</summary>
    public static string LostTo(FailReason reason) => reason switch
    {
        FailReason.Rotted => Strings.Propagations.LostToRotted,
        FailReason.DriedOut => Strings.Propagations.LostToDriedOut,
        FailReason.Pests => Strings.Propagations.LostToPests,
        FailReason.Mould => Strings.Propagations.LostToMould,
        FailReason.NeverRooted => Strings.Propagations.LostToNeverRooted,
        _ => Strings.Propagations.LostToUnknown
    };

    /// <summary>The reason on a button.</summary>
    public static string Chip(FailReason reason) => reason switch
    {
        FailReason.Rotted => Strings.Propagations.ChipRotted,
        FailReason.DriedOut => Strings.Propagations.ChipDriedOut,
        FailReason.Pests => Strings.Propagations.ChipPests,
        FailReason.Mould => Strings.Propagations.ChipMould,
        FailReason.NeverRooted => Strings.Propagations.ChipNeverRooted,
        _ => Strings.Propagations.ChipUnknown
    };
}
