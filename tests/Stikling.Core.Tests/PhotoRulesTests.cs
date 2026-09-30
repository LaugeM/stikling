using Stikling.Core.Sync;

namespace Stikling.Core.Tests;

public class PhotoRulesTests
{
    [Fact]
    public void A_jpeg_is_recognised_by_how_it_starts()
    {
        Assert.Equal(ImageFormat.Jpeg, PhotoRules.FormatOf([0xFF, 0xD8, 0xFF, 0xE0, 0, 0]));
    }

    [Fact]
    public void A_webp_is_recognised_by_how_it_starts()
    {
        byte[] webp = [.. "RIFF"u8, 0x10, 0, 0, 0, .. "WEBP"u8, .. "VP8 "u8];
        Assert.Equal(ImageFormat.WebP, PhotoRules.FormatOf(webp));
    }

    [Fact]
    public void A_png_is_not_recognised()
    {
        Assert.Null(PhotoRules.FormatOf([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]));
    }

    [Fact]
    public void A_wave_file_is_not_taken_for_a_webp()
    {
        byte[] wav = [.. "RIFF"u8, 0x10, 0, 0, 0, .. "WAVE"u8];
        Assert.Null(PhotoRules.FormatOf(wav));
    }

    [Fact]
    public void Too_few_bytes_are_not_recognised()
    {
        Assert.Null(PhotoRules.FormatOf([0xFF, 0xD8]));
        Assert.Null(PhotoRules.FormatOf("RIFF"u8));
        Assert.Null(PhotoRules.FormatOf([]));
    }
}
