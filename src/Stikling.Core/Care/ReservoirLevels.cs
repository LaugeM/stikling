using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Care;

/// <summary>
/// How full a reservoir is, as the four steps people pick from. The level is stored as a number
/// from 0 to 100, so finer values added later still read.
/// </summary>
public static class ReservoirLevels
{
    public const int Empty = 0;
    public const int Low = 25;
    public const int Half = 50;
    public const int Full = 100;

    /// <summary>The levels offered, emptiest first.</summary>
    public static readonly IReadOnlyList<int> Choices = [Empty, Low, Half, Full];

    /// <summary>The word for a level: Empty at 0, Low up to 37, Half up to 75, then Full.</summary>
    public static string Text(int level) => level switch
    {
        <= 0 => Strings.Care.ReservoirEmpty,
        <= 37 => Strings.Care.ReservoirLow,
        <= 75 => Strings.Care.ReservoirHalf,
        _ => Strings.Care.ReservoirFull
    };

    /// <summary>The step a level belongs to, as the value stored when it is picked.</summary>
    public static int Nearest(int level) => level switch
    {
        <= 0 => Empty,
        <= 37 => Low,
        <= 75 => Half,
        _ => Full
    };
}
