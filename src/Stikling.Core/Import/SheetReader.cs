using System.Text;

namespace Stikling.Core.Import;

/// <summary>
/// A table read from a file or pasted cells.
/// </summary>
/// <param name="Headings">One per column: the first row, or "Column 1", "Column 2"...</param>
/// <param name="Rows">The rows under the headings, each as wide as the headings. Blank rows are left out.</param>
/// <param name="Separator">What separated the cells, or null when there is only one column.</param>
/// <param name="RowNumbers">
/// The row of each row in the file, counting the heading row as 1 and the blank rows that were
/// left out. Null when the sheet was made by hand, and then the rows are numbered in order
/// from 2.
/// </param>
public sealed record Sheet(
    IReadOnlyList<string> Headings,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    char? Separator,
    IReadOnlyList<int>? RowNumbers = null)
{
    /// <summary>The row in the file of the row at this index, counting the heading row as row 1.</summary>
    public int RowNumber(int index) => RowNumbers is { } numbers && index < numbers.Count ? numbers[index] : index + 2;
}

/// <summary>Turns the bytes of a .csv file, or pasted text, into a <see cref="Sheet"/>.</summary>
public static class SheetReader
{
    private static readonly char[] Candidates = ['\t', ';', ','];

    // Windows-1252 differs from Latin-1 only in 0x80 to 0x9F. The five bytes it leaves out stay as the control characters.
    private const string Windows1252High =
        "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008DŽ\u008F" +
        "\u0090‘’“”•–—˜™š›œ\u009DžŸ";

    /// <summary>
    /// The text in a file's bytes. A byte order mark says which encoding it is (and is dropped).
    /// Without one it is UTF-8 when the bytes are valid UTF-8, and otherwise Windows-1252, which is
    /// what Excel's plain "CSV" save writes. Windows-1252 is done by hand because the code pages
    /// provider it needs may not be there in the browser.
    /// </summary>
    public static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return string.Create(bytes.Length, bytes, (span, source) =>
            {
                for (var i = 0; i < source.Length; i++)
                    span[i] = source[i] is >= 0x80 and <= 0x9F ? Windows1252High[source[i] - 0x80] : (char)source[i];
            });
        }
    }

    /// <summary>
    /// Reads cells separated by tabs, semicolons or commas, whichever the first lines use, in the
    /// CSV way: quotes around a cell can hold separators and line breaks, and "" is a quote.
    /// </summary>
    public static Sheet Read(string text, bool firstRowIsHeadings = true)
    {
        text = (text ?? "").TrimStart('﻿');
        var separator = DetectSeparator(text);
        var (records, numbers) = Parse(text, separator);

        var headings = new List<string>();
        if (firstRowIsHeadings && records.Count > 0)
        {
            headings.AddRange(records[0]);
            records.RemoveAt(0);
            numbers.RemoveAt(0);
        }

        // A row longer than the headings makes extra columns, and the shorter rows are padded to match
        var width = Math.Max(headings.Count, records.Count == 0 ? 0 : records.Max(r => r.Count));
        for (var i = 0; i < width; i++)
        {
            if (i >= headings.Count)
                headings.Add($"Column {i + 1}");
            else if (headings[i].Length == 0)
                headings[i] = $"Column {i + 1}";
        }

        var rows = records
            .Select(r => (IReadOnlyList<string>)[.. r, .. Enumerable.Repeat("", width - r.Count)])
            .ToList();
        return new Sheet(headings, rows, separator, numbers);
    }

    // The most consistent non-zero count over the first lines wins. Quoted text doesn't count.
    private static char? DetectSeparator(string text)
    {
        var lines = new List<int[]>();
        var counts = new int[Candidates.Length];
        var blank = true;
        var inQuotes = false;

        void EndLine()
        {
            if (!blank)
                lines.Add(counts);
            counts = new int[Candidates.Length];
            blank = true;
        }

        for (var i = 0; i < text.Length && lines.Count < 10; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                blank = false;
            }
            else if (inQuotes)
                blank = false;
            else if (c is '\n' or '\r')
                EndLine();
            else
            {
                var index = Array.IndexOf(Candidates, c);
                if (index >= 0)
                    counts[index]++;
                if (!char.IsWhiteSpace(c) || index >= 0)
                    blank = false;
            }
        }
        if (lines.Count < 10)
            EndLine();

        // Strictly greater, so a tie keeps the earlier candidate: tab, then semicolon, then comma
        char? best = null;
        var bestScore = 0;
        for (var k = 0; k < Candidates.Length; k++)
        {
            var nonZero = lines.Select(l => l[k]).Where(n => n > 0).ToList();
            if (nonZero.Count == 0)
                continue;
            var score = nonZero.GroupBy(n => n).Max(g => g.Count());
            if (score > bestScore)
            {
                best = Candidates[k];
                bestScore = score;
            }
        }
        return best;
    }

    private static (List<List<string>> Records, List<int> Numbers) Parse(string text, char? separator)
    {
        var records = new List<List<string>>();
        var numbers = new List<int>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        var number = 1;
        var pending = false; // something has been read since the last line end

        void EndCell()
        {
            row.Add(cell.ToString().Trim());
            cell.Clear();
        }

        void EndRow()
        {
            EndCell();
            if (row.Any(c => c.Length > 0))
            {
                records.Add(row);
                numbers.Add(number);
            }
            row = [];
            number++;
            pending = false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (c == '"')
                    inQuotes = false;
                else
                    cell.Append(c);
            }
            else if (c == '"')
            {
                inQuotes = true;
                pending = true;
            }
            else if (separator is { } s && c == s)
            {
                EndCell();
                pending = true;
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                EndRow();
            }
            else
            {
                cell.Append(c);
                pending = true;
            }
        }
        if (pending)
            EndRow();

        return (records, numbers);
    }
}
