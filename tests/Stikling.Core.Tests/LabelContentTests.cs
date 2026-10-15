using Stikling.Core.Labels;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class LabelContentTests
{
    private static Plant Monstera(Action<Plant>? set = null)
    {
        var plant = new Plant { Nickname = "Big Monstera", Genus = "Monstera", Species = "deliciosa", CollectionNumber = "117" };
        set?.Invoke(plant);
        return plant;
    }

    [Fact]
    public void A_plant_label_has_number_name_and_botanical_name()
    {
        var label = LabelContent.For(Monstera());

        Assert.Equal("117", label.Number);
        Assert.Equal("Big Monstera", label.Name);
        Assert.Equal("Monstera deliciosa", label.Botanical);
        Assert.Null(label.DateLine);
    }

    [Fact]
    public void The_botanical_name_is_left_out_when_it_is_the_name()
    {
        var label = LabelContent.For(Monstera(p => p.Nickname = null));

        Assert.Equal("Monstera deliciosa", label.Name);
        Assert.Null(label.Botanical);
    }

    [Fact]
    public void The_botanical_name_is_left_out_when_it_is_the_name_in_other_letters()
    {
        var label = LabelContent.For(Monstera(p => p.Nickname = " monstera DELIciosa "));

        Assert.Null(label.Botanical);
    }

    [Fact]
    public void A_plant_without_a_number_has_none_on_its_label()
    {
        Assert.Null(LabelContent.For(Monstera(p => p.CollectionNumber = null)).Number);
        Assert.Null(LabelContent.For(Monstera(p => p.CollectionNumber = "  ")).Number);
    }

    [Fact]
    public void The_date_line_follows_how_exact_the_date_is()
    {
        Assert.Equal("Got 3 Mar 2025", LabelContent.For(Monstera(p => p.AcquiredOn = LooseDate.Of(new DateOnly(2025, 3, 3)))).DateLine);
        Assert.Equal("Got Mar 2025", LabelContent.For(Monstera(p => p.AcquiredOn = LooseDate.Of(2025, 3))).DateLine);
        Assert.Equal("Got 2025", LabelContent.For(Monstera(p => p.AcquiredOn = LooseDate.Of(2025))).DateLine);
    }

    [Fact]
    public void A_cutting_says_when_it_was_started()
    {
        var cutting = new Propagation
        {
            Nickname = "Monstera cutting", Genus = "Monstera", CollectionNumber = "117a", StartedOn = new DateOnly(2026, 10, 3), InitialCount = 1
        };

        var label = LabelContent.For(cutting);

        Assert.Equal("117a", label.Number);
        Assert.Equal("Monstera cutting", label.Name);
        Assert.Equal("Monstera", label.Botanical);
        Assert.Equal("Started 3 Oct 2026", label.DateLine);
    }

    [Fact]
    public void File_names_use_the_number_or_else_the_name_and_never_repeat()
    {
        var labels = new[]
        {
            new LabelContent("117", "Big Monstera", null, null),
            new LabelContent(null, "Pothos 'N Joy'", null, null),
            new LabelContent(null, "Pothos 'N Joy'", null, null),
            new LabelContent(null, "", null, null),
        };

        Assert.Equal(
            ["stikling-label-117.png", "stikling-label-pothos-n-joy.png", "stikling-label-pothos-n-joy-2.png", "stikling-label.png"],
            LabelContent.FileNames(labels));
    }

    [Fact]
    public void Number_name_and_botanical_name_are_on_by_default_and_the_date_is_off()
    {
        var parts = new LabelParts();

        Assert.True(parts.Number);
        Assert.True(parts.Name);
        Assert.True(parts.Botanical);
        Assert.False(parts.Date);
    }
}

public class LabelSettingsTests
{
    [Fact]
    public void Nothing_saved_gives_the_defaults()
    {
        var settings = LabelSettings.FromJson(null);

        Assert.Equal(LabelTab.App, settings.Tab);
        Assert.Equal(new LabelParts(), settings.Parts);
    }

    [Fact]
    public void Settings_survive_being_saved_and_read_back()
    {
        var settings = new LabelSettings
        {
            Tab = LabelTab.Sheet, Paper = LabelPaper.Letter, Layout = LabelSheetLayout.Stakes, TapeMm = 18,
            Parts = new LabelParts(Number: true, Name: false, Botanical: true, Date: true)
        };

        var back = LabelSettings.FromJson(settings.ToJson());

        Assert.Equal(LabelTab.Sheet, back.Tab);
        Assert.Equal(LabelPaper.Letter, back.Paper);
        Assert.Equal(LabelSheetLayout.Stakes, back.Layout);
        Assert.Equal(18, back.TapeMm);
        Assert.Equal(new LabelParts(true, false, true, true), back.Parts);
    }

    [Fact]
    public void A_missing_part_keeps_its_default()
    {
        var settings = LabelSettings.FromJson("""{"tab":"Tape","parts":{"date":true}}""");

        Assert.Equal(LabelTab.Tape, settings.Tab);
        Assert.Equal(new LabelParts(true, true, true, true), settings.Parts);
    }

    [Fact]
    public void Unreadable_or_out_of_range_settings_come_back_as_something_the_page_offers()
    {
        Assert.Equal(LabelTab.App, LabelSettings.FromJson("not json").Tab);

        var settings = LabelSettings.FromJson("""{"tapeMm":15,"tapeLengthMm":99,"appSize":"huge","customWidthMm":500,"customLengthMm":1}""");

        Assert.Equal(12, settings.TapeMm);
        Assert.Equal(40, settings.TapeLengthMm);
        Assert.Equal("12x40", settings.AppSize);
        Assert.Equal(100, settings.CustomWidthMm);
        Assert.Equal(10, settings.CustomLengthMm);
    }

    [Fact]
    public void Each_tab_has_its_own_label_shape()
    {
        Assert.Equal(new LabelShape(40, 12), new LabelSettings { AppSize = "12x40" }.Shape());
        Assert.Equal(new LabelShape(25, 60), new LabelSettings { AppSize = "custom", CustomWidthMm = 25, CustomLengthMm = 60 }.Shape());
        Assert.Equal(new LabelShape(50, 18), new LabelSettings { Tab = LabelTab.Tape, TapeMm = 18, TapeLengthMm = 50 }.Shape());
        Assert.Equal(new LabelShape(50, 25, "box"), new LabelSettings { Tab = LabelTab.Sheet }.Shape());
        Assert.Equal(new LabelShape(15, 120, "point"), new LabelSettings { Tab = LabelTab.Sheet, Layout = LabelSheetLayout.Stakes }.Shape());
    }

    [Fact]
    public void Low_labels_are_one_line_and_tall_ones_are_stakes()
    {
        Assert.True(new LabelShape(60, 12).OneLine);
        Assert.True(new LabelShape(30, 9).OneLine);
        Assert.False(new LabelShape(40, 18).OneLine);
        Assert.True(new LabelShape(15, 120).Stake);
        Assert.False(new LabelShape(15, 120).OneLine);
        Assert.False(new LabelShape(50, 25).Stake);
    }

    [Fact]
    public void Tape_and_sheet_have_a_page_rule_and_the_app_tab_has_none()
    {
        Assert.Null(new LabelSettings().PageRule());
        Assert.Equal("@page { size: 50mm 18mm; margin: 0 }", new LabelSettings { Tab = LabelTab.Tape, TapeMm = 18, TapeLengthMm = 50 }.PageRule());
        Assert.Equal("@page { size: A4; margin: 10mm }", new LabelSettings { Tab = LabelTab.Sheet }.PageRule());
        Assert.Equal("@page { size: letter; margin: 10mm }", new LabelSettings { Tab = LabelTab.Sheet, Paper = LabelPaper.Letter }.PageRule());
    }

    [Fact]
    public void The_no_room_note_names_the_tape()
    {
        Assert.Equal("No room on 12 mm tape.", new LabelSettings { Tab = LabelTab.Tape, TapeMm = 12 }.NoRoomNote());
        Assert.Equal("No room on 9 mm tape.", new LabelSettings { Tab = LabelTab.Tape, TapeMm = 9 }.NoRoomNote());
        Assert.Null(new LabelSettings { Tab = LabelTab.Tape, TapeMm = 18 }.NoRoomNote());
    }
}
