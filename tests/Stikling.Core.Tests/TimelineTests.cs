using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class PlantChangesTests
{
    private static string Label(Enum value) => value.ToString();

    private static readonly Plant Original = new()
    {
        Nickname = "Big alocasia",
        Location = "Living room",
        Medium = GrowingMedium.Soil,
        Container = "Plastic nursery pot"
    };

    [Fact]
    public void No_changes_gives_no_entries()
    {
        var edited = Original.Copy();
        edited.Notes = "Only the notes changed";
        edited.Location = " living ROOM ";

        Assert.Empty(PlantChanges.Describe(Original, edited, Label));
    }

    [Fact]
    public void Moving_to_semi_hydro_is_described()
    {
        var edited = Original.Copy();
        edited.Medium = GrowingMedium.Leca;
        edited.Container = "Lechuza pot";

        Assert.Equal(
            ["Now grows in Leca (was Soil)", "New pot: Lechuza pot"],
            PlantChanges.Describe(Original, edited, Label));
    }

    [Theory]
    [InlineData("Living room", "Bedroom", "Moved from Living room to Bedroom")]
    [InlineData(null, "Kitchen", "Placed in Kitchen")]
    [InlineData("Kitchen", "  ", "Removed from Kitchen")]
    public void Location_changes_are_described(string? from, string? to, string expected)
    {
        var before = Original.Copy();
        before.Location = from;
        var after = Original.Copy();
        after.Location = to;

        Assert.Equal([expected], PlantChanges.Describe(before, after, Label));
    }

    [Fact]
    public void Status_change_uses_the_label_function()
    {
        var edited = Original.Copy();
        edited.Status = PlantStatus.GivenAway;

        var changes = PlantChanges.Describe(Original, edited, v => v is PlantStatus.GivenAway ? "Given away" : v.ToString());

        Assert.Equal(["Status: Given away (was Active)"], changes);
    }

    [Fact]
    public void Removing_the_pot_text_is_not_reported()
    {
        var edited = Original.Copy();
        edited.Container = null;

        Assert.Empty(PlantChanges.Describe(Original, edited, Label));
    }

    [Fact]
    public void Copy_is_independent_of_the_original()
    {
        var copy = Original.Copy();
        copy.Nickname = "Changed";

        Assert.Equal("Big alocasia", Original.Nickname);
        Assert.Equal(Original.Id, copy.Id);
    }

    [Fact]
    public void Potting_into_a_pot_from_the_library_is_written_down()
    {
        var pot = Guid.NewGuid();
        var edited = Original.Copy();
        edited.InnerPotId = pot;

        Assert.Equal(
            ["Potted into Clear nursery pot, 13 cm"],
            PlantChanges.Describe(Original, edited, Label, _ => "Clear nursery pot, 13 cm"));
    }

    [Fact]
    public void A_pot_nobody_can_name_is_left_unsaid()
    {
        var edited = Original.Copy();
        edited.InnerPotId = Guid.NewGuid();

        Assert.Empty(PlantChanges.Describe(Original, edited, Label));
    }

    [Fact]
    public void Taking_a_plant_out_of_its_outer_pot_is_written_down()
    {
        var before = Original.Copy();
        before.OuterPotId = Guid.NewGuid();
        before.WaterInOuterPot = true;
        var edited = before.Copy();
        edited.OuterPotId = null;
        edited.WaterInOuterPot = false;

        Assert.Equal(
            ["No longer in an outer pot", "No longer watered in the outer pot"],
            PlantChanges.Describe(before, edited, Label));
    }

    [Fact]
    public void Moving_a_plant_into_a_saved_mix_is_written_down()
    {
        var edited = Original.Copy();
        edited.SoilMixId = Guid.NewGuid();

        Assert.Equal(
            ["Now in Chunky soil"],
            PlantChanges.Describe(Original, edited, Label, mixName: _ => "Chunky soil"));
    }
}

public class TimelineOrderTests
{
    private static TimelineEntry At(string time, string? text = null, string? created = null) => new()
    {
        OccurredAt = DateTimeOffset.Parse(time),
        CreatedAt = DateTimeOffset.Parse(created ?? time),
        Text = text
    };

    [Fact]
    public void Newest_first_and_ties_broken_by_recording_time()
    {
        var older = At("2026-09-01T10:00:00Z", "older");
        var first = At("2026-09-05T10:00:00Z", "first", created: "2026-09-05T10:00:00Z");
        var second = At("2026-09-05T10:00:00Z", "second", created: "2026-09-05T10:00:05Z");

        var ordered = TimelineOrder.NewestFirst([older, first, second]);

        Assert.Equal(["second", "first", "older"], ordered.Select(e => e.Text));
    }

    [Fact]
    public void Deleted_entries_are_left_out()
    {
        var kept = At("2026-09-01T10:00:00Z", "kept");
        var deleted = At("2026-09-02T10:00:00Z", "deleted");
        deleted.DeletedAt = DateTimeOffset.UnixEpoch;

        Assert.Equal(["kept"], TimelineOrder.NewestFirst([kept, deleted]).Select(e => e.Text));
    }

    [Fact]
    public void Entries_are_grouped_by_local_day()
    {
        // 23:30 UTC on the 1st is already the 2nd in Denmark (UTC+2 in summer)
        var lateEvening = At("2026-09-01T23:30:00Z", "late");
        var morning = At("2026-09-02T08:00:00Z", "morning");
        var earlier = At("2026-08-30T12:00:00Z", "earlier");

        var days = TimelineOrder.ByDay([lateEvening, morning, earlier], TimeSpan.FromHours(2));

        Assert.Equal([new DateOnly(2026, 9, 2), new DateOnly(2026, 8, 30)], days.Select(d => d.Day));
        Assert.Equal(["morning", "late"], days[0].Entries.Select(e => e.Text));
    }
}
