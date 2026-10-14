using System.Text;
using Stikling.Core.Import;

namespace Stikling.Core.Tests;

public class SheetReaderTests
{
    [Fact]
    public void A_comma_file()
    {
        var sheet = SheetReader.Read("Name,Room\nMonstera,Kitchen\nAlocasia,Hall\n");
        Assert.Equal(',', sheet.Separator);
        Assert.Equal(["Name", "Room"], sheet.Headings);
        Assert.Equal(2, sheet.Rows.Count);
        Assert.Equal(["Monstera", "Kitchen"], sheet.Rows[0]);
    }

    [Fact]
    public void A_semicolon_file_with_decimal_commas()
    {
        var sheet = SheetReader.Read("Name;Price\nMonstera;12,50\nAlocasia;3,5\n");
        Assert.Equal(';', sheet.Separator);
        Assert.Equal(["Monstera", "12,50"], sheet.Rows[0]);
    }

    [Fact]
    public void A_tab_file_as_pasted_from_a_spreadsheet()
    {
        var sheet = SheetReader.Read("Name\tRoom\tNotes\nMonstera\tKitchen\tbig, green\n");
        Assert.Equal('\t', sheet.Separator);
        Assert.Equal(["Monstera", "Kitchen", "big, green"], sheet.Rows[0]);
    }

    [Fact]
    public void One_column_has_no_separator()
    {
        var sheet = SheetReader.Read("Plants\nMonstera\nAlocasia");
        Assert.Null(sheet.Separator);
        Assert.Equal(["Plants"], sheet.Headings);
        Assert.Equal(2, sheet.Rows.Count);
    }

    [Fact]
    public void A_tie_goes_to_tab_then_semicolon_then_comma()
    {
        Assert.Equal('\t', SheetReader.Read("a\tb;c,d\n1\t2;3,4").Separator);
        Assert.Equal(';', SheetReader.Read("a;b,c\n1;2,3").Separator);
    }

    [Fact]
    public void Quotes_can_hold_commas_quotes_and_line_breaks()
    {
        var sheet = SheetReader.Read("Name,Notes\n\"A, b\",\"say \"\"hi\"\"\"\n\"line 1\nline 2\",x\n");
        Assert.Equal(["A, b", "say \"hi\""], sheet.Rows[0]);
        Assert.Equal(["line 1\nline 2", "x"], sheet.Rows[1]);
    }

    [Theory]
    [InlineData("a,b\r\n1,2\r\n3,4\r\n")]
    [InlineData("a,b\n1,2\n3,4\n")]
    [InlineData("a,b\r1,2\r3,4\r")]
    public void Every_kind_of_line_end(string text)
    {
        var sheet = SheetReader.Read(text);
        Assert.Equal(["a", "b"], sheet.Headings);
        Assert.Equal(2, sheet.Rows.Count);
        Assert.Equal(["3", "4"], sheet.Rows[1]);
    }

    [Fact]
    public void Cells_are_trimmed_and_blank_rows_dropped_but_still_counted()
    {
        var sheet = SheetReader.Read("a,b\n 1 , 2 \n\n , \n3,4");
        Assert.Equal(2, sheet.Rows.Count);
        Assert.Equal(["1", "2"], sheet.Rows[0]);
        Assert.Equal([2, 5], sheet.RowNumbers);
    }

    [Fact]
    public void Ragged_rows_are_padded_and_a_long_row_adds_columns()
    {
        var sheet = SheetReader.Read("a,b,c\n1\n1,2,3,4");
        Assert.Equal(["a", "b", "c", "Column 4"], sheet.Headings);
        Assert.Equal(["1", "", "", ""], sheet.Rows[0]);
        Assert.Equal(["1", "2", "3", "4"], sheet.Rows[1]);
    }

    [Fact]
    public void Without_headings_the_columns_are_numbered_and_the_first_row_is_data()
    {
        var sheet = SheetReader.Read("Monstera,Kitchen\nAlocasia,Hall", firstRowIsHeadings: false);
        Assert.Equal(["Column 1", "Column 2"], sheet.Headings);
        Assert.Equal(2, sheet.Rows.Count);
        Assert.Equal([1, 2], sheet.RowNumbers);
    }

    [Fact]
    public void A_heading_left_empty_gets_a_number()
    {
        var sheet = SheetReader.Read("a,,c\n1,2,3");
        Assert.Equal(["a", "Column 2", "c"], sheet.Headings);
    }

    [Fact]
    public void An_empty_text_is_an_empty_sheet()
    {
        var sheet = SheetReader.Read("");
        Assert.Empty(sheet.Headings);
        Assert.Empty(sheet.Rows);
    }

    private const string Danish = "Rødt æøå ÆØÅ";

    [Fact]
    public void Windows_1252_bytes_are_read_without_a_code_pages_provider()
    {
        // Excel's plain CSV: æ E6, ø F8, å E5, Æ C6, Ø D8, Å C5, and the euro sign at 0x80
        byte[] bytes = [.. "R"u8, 0xF8, .. "dt "u8, 0xE6, 0xF8, 0xE5, 0x20, 0xC6, 0xD8, 0xC5, 0x20, 0x80, 0x93, 0x94];
        Assert.Equal("Rødt æøå ÆØÅ €“”", SheetReader.Decode(bytes));
    }

    [Fact]
    public void UTF_8_with_and_without_a_byte_order_mark()
    {
        var plain = Encoding.UTF8.GetBytes(Danish);
        Assert.Equal(Danish, SheetReader.Decode(plain));
        Assert.Equal(Danish, SheetReader.Decode([.. Encoding.UTF8.GetPreamble(), .. plain]));
    }

    [Fact]
    public void UTF_16_with_a_byte_order_mark()
    {
        Assert.Equal(Danish, SheetReader.Decode([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(Danish)]));
        Assert.Equal(Danish, SheetReader.Decode([.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(Danish)]));
    }
}
