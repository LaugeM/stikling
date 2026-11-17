using Stikling.Core.Labels;
using ZXing;
using ZXing.Common;

namespace Stikling.Core.Tests;

public class LabelQrTests
{
    private static readonly Guid Id = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
    private const string Base = "https://stikling.app/";
    private const string Code = "3f2504e04f8911d39a0c0305e82c3301";

    [Fact]
    public void A_label_link_is_in_capitals_with_the_id_as_32_digits()
    {
        var link = LabelLinks.For(Id, Base);

        Assert.Equal("HTTPS://STIKLING.APP/L/3F2504E04F8911D39A0C0305E82C3301", link);
        Assert.Equal(55, link.Length);
    }

    [Fact]
    public void A_label_link_keeps_the_port_and_the_path_of_the_base()
    {
        Assert.Equal("HTTP://LOCALHOST:5000/L/3F2504E04F8911D39A0C0305E82C3301", LabelLinks.For(Id, "http://localhost:5000/"));
        Assert.Equal("HTTPS://EXAMPLE.ORG/stikling/L/3F2504E04F8911D39A0C0305E82C3301", LabelLinks.For(Id, "https://example.org/stikling/"));
    }

    [Theory]
    [InlineData("https://stikling.app/l/3f2504e04f8911d39a0c0305e82c3301")]
    [InlineData("HTTPS://STIKLING.APP/L/3F2504E04F8911D39A0C0305E82C3301")]
    [InlineData("https://Stikling.App/L/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [InlineData("https://stikling.app/l/3F2504E04F8911D39A0C0305E82C3301/")]
    [InlineData("  https://stikling.app/l/3f2504e04f8911d39a0c0305e82c3301?x=1#y  ")]
    [InlineData("http://localhost:5000/l/3f2504e04f8911d39a0c0305e82c3301")]
    public void A_label_link_opens_the_label_path(string scanned)
    {
        Assert.Equal($"l/{Code}", LabelLinks.AppPath(scanned, "http://localhost:5000/"));
    }

    [Theory]
    [InlineData("https://stikling.app/plants/3f2504e0-4f89-11d3-9a0c-0305e82c3301", "plants/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [InlineData("https://stikling.app/PLANTS/3F2504E0-4F89-11D3-9A0C-0305E82C3301", "plants/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [InlineData("https://stikling.app/propagations/3f2504e0-4f89-11d3-9a0c-0305e82c3301", "propagations/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    public void An_older_link_opens_the_plant_or_cutting(string scanned, string expected)
    {
        Assert.Equal(expected, LabelLinks.AppPath(scanned, Base));
    }

    [Fact]
    public void The_host_of_the_base_is_accepted_with_its_path()
    {
        Assert.Equal($"l/{Code}", LabelLinks.AppPath("https://example.org/stikling/L/3F2504E04F8911D39A0C0305E82C3301", "https://example.org/stikling/"));
        Assert.Null(LabelLinks.AppPath("https://example.org/L/3F2504E04F8911D39A0C0305E82C3301", "https://example.org/stikling/"));
    }

    [Theory]
    [InlineData("https://example.com/l/3f2504e04f8911d39a0c0305e82c3301")]
    [InlineData("https://stikling.app.evil.com/l/3f2504e04f8911d39a0c0305e82c3301")]
    [InlineData("https://evil.com/?https://stikling.app/l/3f2504e04f8911d39a0c0305e82c3301")]
    [InlineData("https://stikling.app/l/not-an-id")]
    [InlineData("https://stikling.app/l/3f2504e04f8911d39a0c0305e82c33")]
    [InlineData("https://stikling.app/l/00000000000000000000000000000000")]
    [InlineData("https://stikling.app/l/{3f2504e0-4f89-11d3-9a0c-0305e82c3301}")]
    [InlineData("https://stikling.app/plants/not-an-id")]
    [InlineData("https://stikling.app/plants/3f2504e04f8911d39a0c0305e82c3301/edit")]
    [InlineData("https://stikling.app/settings")]
    [InlineData("https://stikling.app/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://stikling.app/l/3f2504e04f8911d39a0c0305e82c3301")]
    [InlineData("3f2504e04f8911d39a0c0305e82c3301")]
    [InlineData("a plain sentence")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_else_is_not_a_stikling_link(string? scanned)
    {
        Assert.Null(LabelLinks.AppPath(scanned, Base));
    }

    [Theory]
    [InlineData("3f2504e04f8911d39a0c0305e82c3301")]
    [InlineData("3F2504E04F8911D39A0C0305E82C3301")]
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [InlineData("3F2504E0-4F89-11D3-9A0C-0305E82C3301")]
    public void A_code_is_read_in_both_forms_and_any_case(string code)
    {
        Assert.Equal(Id, LabelLinks.ParseCode(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("hello")]
    [InlineData("3f2504e04f8911d39a0c0305e82c33")]
    [InlineData("zf2504e04f8911d39a0c0305e82c3301")]
    [InlineData("{3f2504e0-4f89-11d3-9a0c-0305e82c3301}")]
    public void A_bad_code_is_null(string? code)
    {
        Assert.Null(LabelLinks.ParseCode(code));
    }

    [Theory]
    [InlineData("l/3F2504E04F8911D39A0C0305E82C3301", true)]
    [InlineData("settings?section=account", true)]
    [InlineData("7", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("/settings", false)]
    [InlineData("//evil.com", false)]
    [InlineData("a//b", false)]
    [InlineData("https://evil.com", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("a\\b", false)]
    [InlineData("a\nb", false)]
    public void A_return_path_must_stay_inside_the_app(string? path, bool safe)
    {
        Assert.Equal(safe, ReturnPath.IsSafe(path));
    }

    [Fact]
    public void The_qr_code_of_a_label_link_is_29_modules_square()
    {
        var rows = QrCode.Rows(LabelLinks.For(Id, Base));

        Assert.Equal(29, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal(29, row.Length);
            Assert.All(row, c => Assert.True(c is '0' or '1'));
        });
    }

    [Fact]
    public void The_qr_code_reads_back_as_the_link()
    {
        var link = LabelLinks.For(Id, Base);
        var rows = QrCode.Rows(link);

        const int scale = 4, border = 4;
        var side = (rows.Count + 2 * border) * scale;
        var pixels = new byte[side * side];
        Array.Fill(pixels, (byte)255);
        for (var y = 0; y < rows.Count; y++)
            for (var x = 0; x < rows[y].Length; x++)
                if (rows[y][x] == '1')
                    for (var dy = 0; dy < scale; dy++)
                        for (var dx = 0; dx < scale; dx++)
                            pixels[((y + border) * scale + dy) * side + (x + border) * scale + dx] = 0;

        var reader = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { PossibleFormats = [BarcodeFormat.QR_CODE], TryHarder = true }
        };
        var result = reader.Decode(new RGBLuminanceSource(pixels, side, side, RGBLuminanceSource.BitmapFormat.Gray8));

        Assert.NotNull(result);
        Assert.Equal(link, result.Text);
    }

    private static LabelShape Tape(int mm, int length = 40) => new LabelSettings { Tab = LabelTab.Tape, TapeMm = mm, TapeLengthMm = length }.Shape();

    [Fact]
    public void A_qr_code_needs_room_for_9_mm_of_it()
    {
        Assert.False(Tape(9).QrFits);
        Assert.True(Tape(12).QrFits);
        Assert.True(Tape(12, 30).QrFits);
        Assert.True(Tape(18).QrFits);
        Assert.True(new LabelShape(30, 14).QrFits);
        Assert.True(new LabelShape(50, 25).QrFits);
        Assert.True(new LabelShape(40, 12).QrFits);
        Assert.False(new LabelShape(10, 10).QrFits);
        Assert.False(new LabelShape(10, 25).QrFits);
    }

    [Fact]
    public void A_flat_label_keeps_room_for_text_beside_the_qr_code()
    {
        // Tall enough for the code, but nothing left of the width for text
        Assert.False(new LabelShape(20, 14).QrFits);
        Assert.True(new LabelShape(30, 14).QrFits);
    }

    [Fact]
    public void The_stake_and_the_stickers_have_room()
    {
        Assert.True(new LabelSettings { Tab = LabelTab.Sheet, Layout = LabelSheetLayout.Stakes }.Shape().QrFits);
        Assert.True(new LabelSettings { Tab = LabelTab.Sheet, Layout = LabelSheetLayout.Pots }.Shape().QrFits);
        Assert.True(new LabelSettings { Tab = LabelTab.Sheet, Layout = LabelSheetLayout.Stickers, Paper = LabelPaper.A4 }.Shape().QrFits);
        Assert.True(new LabelSettings { Tab = LabelTab.Sheet, Layout = LabelSheetLayout.Stickers, Paper = LabelPaper.Letter }.Shape().QrFits);
    }

    [Fact]
    public void The_no_room_note_says_so_only_when_there_is_none()
    {
        Assert.Equal("There's no room for a QR code on 9 mm tape.", new LabelSettings { Tab = LabelTab.Tape, TapeMm = 9 }.QrNoRoomNote());
        Assert.Null(new LabelSettings { Tab = LabelTab.Tape, TapeMm = 12 }.QrNoRoomNote());
        Assert.Equal(
            "There's no room for a QR code on a label this small.",
            new LabelSettings { AppSize = LabelSettings.Custom, CustomWidthMm = 10, CustomLengthMm = 10 }.QrNoRoomNote());
        Assert.Null(new LabelSettings().QrNoRoomNote());
    }

    [Fact]
    public void Settings_saved_before_the_qr_code_have_it_off()
    {
        var settings = LabelSettings.FromJson("""{"tab":"Tape","parts":{"number":true,"name":false,"botanical":true,"date":true}}""");

        Assert.Equal(LabelTab.Tape, settings.Tab);
        Assert.False(settings.Parts.Qr);
        Assert.False(settings.Parts.Name);
        Assert.True(settings.Parts.Date);
    }

    [Fact]
    public void The_qr_choice_is_kept()
    {
        var json = new LabelSettings { Parts = new LabelParts(Qr: true) }.ToJson();

        Assert.True(LabelSettings.FromJson(json).Parts.Qr);
    }

    [Fact]
    public void Settings_with_a_layout_this_version_does_not_know_fall_back_to_the_defaults()
    {
        Assert.Equal(LabelSheetLayout.Pots, LabelSettings.FromJson("""{"layout":"Banners"}""").Layout);
    }

    [Fact]
    public void A_sticker_sheet_shapes_the_label_and_the_page()
    {
        var a4 = new LabelSettings { Tab = LabelTab.Sheet, Layout = LabelSheetLayout.Stickers, Paper = LabelPaper.A4 };
        Assert.Equal(new LabelShape(63.5, 38.1, "none"), a4.Shape());
        Assert.Equal("@page { size: A4; margin: 0 }", a4.PageRule());

        var letter = new LabelSettings { Tab = LabelTab.Sheet, Layout = LabelSheetLayout.Stickers, Paper = LabelPaper.Letter };
        Assert.Equal(new LabelShape(66.675, 25.4, "none"), letter.Shape());
        Assert.Equal("@page { size: letter; margin: 0 }", letter.PageRule());

        Assert.Equal("@page { size: A4; margin: 10mm }", new LabelSettings { Tab = LabelTab.Sheet }.PageRule());
    }

    [Fact]
    public void Each_sticker_sheet_fills_its_paper()
    {
        var a4 = StickerSheet.For(LabelPaper.A4);
        Assert.Equal(21, a4.PerSheet);
        Assert.Equal(210, a4.LeftMm * 2 + a4.Columns * a4.LabelWidthMm + (a4.Columns - 1) * a4.ColumnGapMm, 0.1);
        Assert.True(a4.TopMm + a4.Rows * a4.LabelHeightMm + (a4.Rows - 1) * a4.RowGapMm <= 297);

        var letter = StickerSheet.For(LabelPaper.Letter);
        Assert.Equal(30, letter.PerSheet);
        Assert.Equal(215.9, letter.LeftMm * 2 + letter.Columns * letter.LabelWidthMm + (letter.Columns - 1) * letter.ColumnGapMm, 0.1);
        Assert.True(letter.TopMm + letter.Rows * letter.LabelHeightMm + (letter.Rows - 1) * letter.RowGapMm <= 279.4);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(3, 1, 3)]
    [InlineData(4, 2, 1)]
    [InlineData(21, 7, 3)]
    [InlineData(0, 1, 1)]
    [InlineData(99, 7, 3)]
    public void The_start_sticker_is_a_row_and_a_column(int start, int row, int column)
    {
        Assert.Equal((row, column), StickerSheet.A4.CellOf(start));
    }

    [Fact]
    public void Labels_start_at_the_chosen_sticker_and_go_on_to_a_new_sheet()
    {
        var sheet = StickerSheet.A4;

        var first = sheet.Place(4, 0);
        Assert.Equal((1, 2, 1), (first.Sheet, first.Row, first.Column));
        Assert.Equal(7.25, first.XMm, 3);
        Assert.Equal(15.15 + 38.1, first.YMm, 3);

        var second = sheet.Place(4, 1);
        Assert.Equal(7.25 + 63.5 + 2.54, second.XMm, 3);

        // 18 stickers are left on the first sheet from number 4, so the 19th label opens a second
        Assert.Equal(1, sheet.Place(4, 17).Sheet);
        var next = sheet.Place(4, 18);
        Assert.Equal((2, 1, 1), (next.Sheet, next.Row, next.Column));

        Assert.Equal(1, sheet.Sheets(1, 21));
        Assert.Equal(2, sheet.Sheets(1, 22));
        Assert.Equal(2, sheet.Sheets(4, 19));
        Assert.Equal(0, sheet.Sheets(1, 0));
    }
}
