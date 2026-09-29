using System.Text;
using Stikling.Core.Backup;
using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class CsvExportTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static string[] Lines(string csv) => csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Plants_start_with_a_header_row_and_use_crlf()
    {
        var csv = CsvExport.Plants([new Plant { Nickname = "Basil", CreatedAt = Now }], Places.None);

        Assert.Equal("Name,Nickname,Genus,Species,Cultivar,Status,Room,Acquired,Origin,Source,Tags,Notes,Added", Lines(csv)[0]);
        Assert.EndsWith("\r\n", csv);
        Assert.DoesNotContain("\n", csv.Replace("\r\n", ""));
    }

    [Fact]
    public void Fields_with_commas_quotes_and_newlines_are_quoted()
    {
        var plant = new Plant { Nickname = "Big, green", Notes = "She said \"hi\"\nsecond line", CreatedAt = Now };

        var csv = CsvExport.Plants([plant], Places.None);

        Assert.Contains("\"Big, green\"", csv);
        Assert.Contains("\"She said \"\"hi\"\"\nsecond line\"", csv);
    }

    [Fact]
    public void Deleted_plants_are_skipped_and_the_rest_sorted_by_name()
    {
        var plants = new[]
        {
            new Plant { Nickname = "Zebra", CreatedAt = Now },
            new Plant { Nickname = "Gone", CreatedAt = Now, DeletedAt = Now },
            new Plant { Nickname = "Aloe", CreatedAt = Now, Status = PlantStatus.Died }
        };

        var lines = Lines(CsvExport.Plants(plants, Places.None));

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Aloe,", lines[1]);
        Assert.Contains(",Died,", lines[1]);
        Assert.StartsWith("Zebra,", lines[2]);
    }

    [Fact]
    public void Tags_are_joined_and_the_added_date_has_no_time()
    {
        var plant = new Plant { Nickname = "Basil", Tags = ["rare", "for swap"], CreatedAt = Now };

        var line = Lines(CsvExport.Plants([plant], Places.None))[1];

        Assert.Contains(",rare; for swap,", line);
        Assert.Matches(@",\d{4}-\d{2}-\d{2}$", line);
    }

    [Fact]
    public void Propagations_name_their_parent_plant()
    {
        var parent = new Plant { Nickname = "Mother, big", CreatedAt = Now };
        var propagations = new[]
        {
            new Propagation { Nickname = "Cutting", ParentPlantId = parent.Id, StartedOn = new DateOnly(2026, 9, 1) },
            new Propagation { Nickname = "Seeds" },
            new Propagation { Nickname = "Gone", DeletedAt = Now }
        };

        var lines = Lines(CsvExport.Propagations(propagations, [parent], Places.None));

        Assert.Equal("Name,Parent plant,Stage,Medium,Started,Room,Notes", lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.Equal("Cutting,\"Mother, big\",Started,Water,2026-09-01,,", lines[1]);
        Assert.StartsWith("Seeds,,", lines[2]);
    }

    [Fact]
    public void Encoded_bytes_start_with_a_byte_order_mark()
    {
        var bytes = CsvExport.Encode("æ");

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("æ", Encoding.UTF8.GetString(bytes[3..]));
    }
}
