using Stikling.Core.Sync;

namespace Stikling.Core.Tests;

public class FileRulesTests
{
    [Fact]
    public void A_pdf_is_recognised_by_how_it_starts()
    {
        var format = FileRules.FormatOf("%PDF-1.7\n%\u00e2\u00e3"u8);

        Assert.Equal(ImageFormat.Pdf, format);
        Assert.Equal(("application/pdf", "pdf"), (format!.ContentType, format.Extension));
    }

    [Fact]
    public void Images_are_recognised_as_photos_are()
    {
        Assert.Equal(ImageFormat.Jpeg, FileRules.FormatOf([0xFF, 0xD8, 0xFF, 0xE0, 0, 0]));
        Assert.Equal(ImageFormat.WebP, FileRules.FormatOf([.. "RIFF"u8, 0x10, 0, 0, 0, .. "WEBP"u8]));
    }

    [Fact]
    public void Anything_else_is_not_recognised()
    {
        Assert.Null(FileRules.FormatOf([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]));
        Assert.Null(FileRules.FormatOf("<html>%PDF-</html>"u8));
        Assert.Null(FileRules.FormatOf("%PDF"u8));
        Assert.Null(FileRules.FormatOf([]));
    }

    [Fact]
    public void A_photo_still_only_knows_images()
    {
        Assert.Null(PhotoRules.FormatOf("%PDF-1.7"u8));
    }
}
