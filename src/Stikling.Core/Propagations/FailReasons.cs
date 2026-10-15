using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>The words for why units failed.</summary>
public static class FailReasons
{
    /// <summary>The reason as it reads in a history line: "2 failed: dried out".</summary>
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
        FailReason.Rotted => "rot",
        _ => Label(reason)
    };

    /// <summary>The reason on a button.</summary>
    public static string Chip(FailReason reason)
    {
        var label = Label(reason);
        return char.ToUpperInvariant(label[0]) + label[1..];
    }
}
