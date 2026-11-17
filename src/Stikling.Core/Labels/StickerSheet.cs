namespace Stikling.Core.Labels;

/// <summary>Where one sticker is on its sheet, counted from 1, and where its corner is in millimetres.</summary>
public sealed record StickerPlace(int Sheet, int Row, int Column, double XMm, double YMm);

/// <summary>
/// A sheet of sticker paper: the grid the stickers sit in, in millimetres. The printed page is laid out
/// from these numbers exactly, so the labels land on the stickers.
/// </summary>
public sealed record StickerSheet(
    int Columns, int Rows,
    double LabelWidthMm, double LabelHeightMm,
    double TopMm, double LeftMm,
    double ColumnGapMm, double RowGapMm,
    double PaperWidthMm, double PaperHeightMm)
{
    /// <summary>A4: the 63.5 × 38.1 mm size most office shops sell, 21 to a sheet.</summary>
    public static readonly StickerSheet A4 = new(3, 7, 63.5, 38.1, 15.15, 7.25, 2.54, 0, 210, 297);

    /// <summary>Letter: the common address-label sheet of 30, 66.675 × 25.4 mm.</summary>
    public static readonly StickerSheet Letter = new(3, 10, 66.675, 25.4, 12.7, 4.7625, 3.175, 0, 215.9, 279.4);

    public static StickerSheet For(LabelPaper paper) => paper == LabelPaper.A4 ? A4 : Letter;

    public int PerSheet => Columns * Rows;

    /// <summary>The number to start at, brought into 1 to the number of stickers on a sheet.</summary>
    public int ClampStart(int start) => Math.Clamp(start, 1, PerSheet);

    /// <summary>Row and column, counted from 1, of sticker number <paramref name="start"/> on a sheet. Stickers are numbered along the rows.</summary>
    public (int Row, int Column) CellOf(int start)
    {
        var index = ClampStart(start) - 1;
        return (index / Columns + 1, index % Columns + 1);
    }

    /// <summary>How many sheets <paramref name="count"/> labels take when the first sheet is started at sticker <paramref name="start"/>.</summary>
    public int Sheets(int start, int count) =>
        count <= 0 ? 0 : (ClampStart(start) - 1 + count + PerSheet - 1) / PerSheet;

    /// <summary>Where label number <paramref name="index"/> (from 0) goes. The first sheet leaves the stickers before <paramref name="start"/> empty.</summary>
    public StickerPlace Place(int start, int index)
    {
        var slot = ClampStart(start) - 1 + index;
        var onSheet = slot % PerSheet;
        var row = onSheet / Columns;
        var column = onSheet % Columns;
        return new StickerPlace(
            slot / PerSheet + 1, row + 1, column + 1,
            LeftMm + column * (LabelWidthMm + ColumnGapMm),
            TopMm + row * (LabelHeightMm + RowGapMm));
    }
}
