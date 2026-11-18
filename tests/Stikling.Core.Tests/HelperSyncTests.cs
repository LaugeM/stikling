using System.Text.Json;
using Stikling.Core.People;
using Stikling.Core.Sync;

namespace Stikling.Core.Tests;

public class HelperSyncTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly MemberAccess Helper = new(Me, CollectionRole.Helper, false, false);

    private static JsonElement Record(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private const string KindNote = ",\"kind\":\"Note\"";
    private const string KindChange = ",\"kind\":\"Change\"";
    private const string KindPhoto = ",\"kind\":\"Photo\"";
    private const string Deleted = ",\"deletedAt\":\"2026-10-01T10:00:00+00:00\"";

    private static string Mine(string extra = "") => $$"""{"id":"{{Guid.NewGuid()}}","createdBy":"{{Me}}"{{extra}}}""";

    [Theory]
    [InlineData("careLogs")]
    [InlineData("propagationCareLogs")]
    [InlineData("photos")]
    public void A_helper_sends_care_entries_and_photos_they_added(string kind) =>
        Assert.True(HelperSync.MaySend(kind, Record(Mine()), Helper));

    [Theory]
    [InlineData("plants")]
    [InlineData("propagations")]
    [InlineData("places")]
    [InlineData("settings")]
    [InlineData("pots")]
    public void A_helper_sends_no_other_kind(string kind) =>
        Assert.False(HelperSync.MaySend(kind, Record(Mine()), Helper));

    [Fact]
    public void A_timeline_entry_has_to_be_a_note()
    {
        Assert.True(HelperSync.MaySend("timeline", Record(Mine(KindNote)), Helper));
        Assert.False(HelperSync.MaySend("timeline", Record(Mine(KindChange)), Helper));
        Assert.False(HelperSync.MaySend("timeline", Record(Mine(KindPhoto)), Helper));
        Assert.False(HelperSync.MaySend("timeline", Record(Mine()), Helper));
    }

    [Fact]
    public void A_deleted_timeline_entry_with_no_kind_left_is_fine_as_the_server_reads_it()
    {
        Assert.True(HelperSync.MaySend("timeline", Record(Mine(Deleted)), Helper));
        Assert.False(HelperSync.MaySend("timeline", Record(Mine(Deleted + KindChange)), Helper));
        Assert.False(HelperSync.MaySend("timeline", Record(Mine(",\"deletedAt\":null")), Helper));
    }

    [Fact]
    public void A_second_kind_written_in_another_case_stops_a_timeline_entry()
    {
        var sneaky = Record(Mine(KindNote + ",\"Kind\":\"Change\""));

        Assert.False(HelperSync.MaySend("timeline", sneaky, Helper));
        Assert.Equal(HelperSync.HelperKindProblem, HelperSync.KindProblem("timeline", sneaky));
    }

    [Fact]
    public void A_record_has_to_be_the_helpers_own()
    {
        var theirs = Record($$"""{"id":"{{Guid.NewGuid()}}","createdBy":"{{Guid.NewGuid()}}"}""");
        var nobodys = Record($$"""{"id":"{{Guid.NewGuid()}}","createdBy":null}""");
        var unknown = Record($$"""{"id":"{{Guid.NewGuid()}}"}""");

        Assert.False(HelperSync.MaySend("careLogs", theirs, Helper));
        Assert.False(HelperSync.MaySend("careLogs", nobodys, Helper));
        Assert.False(HelperSync.MaySend("careLogs", unknown, Helper));
    }

    [Fact]
    public void A_viewer_sends_nothing_and_an_editor_everything()
    {
        var viewer = new MemberAccess(Me, CollectionRole.Viewer, false, false);
        var editor = new MemberAccess(Me, CollectionRole.Editor, false, false);
        var plant = Record($$"""{"id":"{{Guid.NewGuid()}}"}""");

        Assert.False(HelperSync.MaySend("careLogs", Record(Mine()), viewer));
        Assert.True(HelperSync.MaySend("plants", plant, editor));
    }

    [Fact]
    public void The_two_helper_reasons_are_known_by_their_text()
    {
        Assert.True(HelperSync.IsHelperReason("A helper can't change this kind of record."));
        Assert.True(HelperSync.IsHelperReason("Only the person who added it can change it."));
        Assert.False(HelperSync.IsHelperReason("The record was changed in the future. Check the date and time on the device."));
        Assert.False(HelperSync.IsHelperReason(null));
    }
}
