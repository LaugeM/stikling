using QRCoder;

namespace Stikling.Core.Labels;

/// <summary>
/// Makes the dots of a QR code. QRCoder is a free library that runs on the device: nothing is sent
/// to a service, and the code works with no connection.
/// </summary>
public static class QrCode
{
    /// <summary>
    /// The modules of a QR code for the text, one string per row, with '1' for a dark module and '0' for a
    /// light one. There is no quiet zone, so whoever draws it leaves the margin. Error correction is level M,
    /// which still reads with a bit of the label scratched or worn.
    /// </summary>
    public static IReadOnlyList<string> Rows(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);

        // QRCoder adds a quiet zone of 4 modules on every side
        const int quiet = 4;
        var matrix = data.ModuleMatrix;
        var size = matrix.Count - 2 * quiet;
        var rows = new List<string>(size);
        for (var y = 0; y < size; y++)
        {
            var row = new char[size];
            for (var x = 0; x < size; x++)
                row[x] = matrix[y + quiet][x + quiet] ? '1' : '0';
            rows.Add(new string(row));
        }
        return rows;
    }
}
