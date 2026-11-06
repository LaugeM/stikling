using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class PestPhotoTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 6);

    private static string English(Enum value) => value is Pest.SpiderMites ? "Spider mites" : value.ToString();

    private sealed class Setup
    {
        public FakePestCaseRepository Cases { get; } = new();
        public FakePestTreatmentRepository Treatments { get; } = new();
        public FakeTimelineRepository Timeline { get; } = new();
        public PestPhotoService Service { get; }
        public Guid PlantId { get; } = Guid.NewGuid();
        public PestCase Item { get; }

        public Setup()
        {
            Service = new PestPhotoService(Timeline, Treatments, Cases, new FixedTime(Now));
            Item = new PestCase { Pest = Pest.SpiderMites, StartedOn = Today.AddDays(-10), PlantIds = [PlantId] };
            Cases.Cases[Item.Id] = Item;
        }

        public PestTreatment Treatment(PestTreatmentKind kind = PestTreatmentKind.Treated, string? what = null, int daysAgo = 0)
        {
            var treatment = new PestTreatment { CaseId = Item.Id, Kind = kind, What = what, OccurredOn = Today.AddDays(-daysAgo) };
            Treatments.Treatments[treatment.Id] = treatment;
            return treatment;
        }

        public TimelineEntry FoundLine(params Guid[] photos)
        {
            var entry = new TimelineEntry
            {
                SubjectType = SubjectType.Plant,
                SubjectId = PlantId,
                Kind = TimelineKind.Change,
                Event = TimelineEvent.Pest,
                PestCaseId = Item.Id,
                OccurredAt = Now.AddDays(-10),
                PhotoIds = [.. photos]
            };
            HistoryText.Set(entry, [HistoryText.Line("PestFound", HistoryArg.Of(Item.Pest))], English);
            Timeline.Entries.Add(entry);
            return entry;
        }
    }

    [Fact]
    public async Task A_treatment_with_photos_gets_a_line_with_what_was_used()
    {
        var s = new Setup();
        var treatment = s.Treatment(what: " neem oil ", daysAgo: 2);
        var photos = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        await s.Service.AddLineAsync(s.Item, treatment, s.PlantId, photos, English);

        var line = Assert.Single(s.Timeline.Entries);
        Assert.Equal((SubjectType.Plant, s.PlantId, TimelineKind.Change, TimelineEvent.Pest), (line.SubjectType, line.SubjectId, line.Kind, line.Event));
        Assert.Equal((s.Item.Id, treatment.Id), (line.PestCaseId, line.PestTreatmentId));
        Assert.Equal(photos, line.PhotoIds);
        Assert.NotSame(photos, line.PhotoIds);
        Assert.Equal(new FixedTime(Now).MomentOn(Today.AddDays(-2)), line.OccurredAt);
        Assert.Equal("PestTreatedWith", Assert.Single(line.Lines!).Key);
        Assert.Equal("Treated for spider mites: neem oil", line.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task A_treatment_without_text_gets_the_plain_line(string? what)
    {
        var s = new Setup();

        await s.Service.AddLineAsync(s.Item, s.Treatment(what: what), s.PlantId, [Guid.NewGuid()], English);

        var line = Assert.Single(s.Timeline.Entries);
        Assert.Equal("PestTreated", Assert.Single(line.Lines!).Key);
        Assert.Equal("Treated for spider mites", line.Text);
    }

    [Fact]
    public async Task A_check_gets_the_checked_line()
    {
        var s = new Setup();

        await s.Service.AddLineAsync(s.Item, s.Treatment(PestTreatmentKind.Checked), s.PlantId, [Guid.NewGuid()], English);

        var line = Assert.Single(s.Timeline.Entries);
        Assert.Equal("PestChecked", Assert.Single(line.Lines!).Key);
        Assert.Equal("Checked for spider mites", line.Text);
    }

    [Fact]
    public async Task Nothing_is_written_without_photos()
    {
        var s = new Setup();

        await s.Service.AddLineAsync(s.Item, s.Treatment(), s.PlantId, [], English);
        await s.Service.AddToFoundLineAsync(s.Item, s.PlantId, [], English);

        Assert.Empty(s.Timeline.Entries);
    }

    [Fact]
    public async Task Photos_are_added_to_the_found_line_once()
    {
        var s = new Setup();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var line = s.FoundLine(first);

        await s.Service.AddToFoundLineAsync(s.Item, s.PlantId, [first, second], English);

        Assert.Same(line, Assert.Single(s.Timeline.Entries));
        Assert.Equal([first, second], line.PhotoIds);
        Assert.Contains(line.Id, s.Timeline.Updated);
    }

    [Fact]
    public async Task A_treatment_line_is_not_taken_for_the_found_line()
    {
        var s = new Setup();
        var other = new TimelineEntry
        {
            SubjectId = s.PlantId, Kind = TimelineKind.Change, Event = TimelineEvent.Pest,
            PestCaseId = s.Item.Id, PestTreatmentId = Guid.NewGuid(), PhotoIds = [Guid.NewGuid()]
        };
        s.Timeline.Entries.Add(other);

        await s.Service.AddToFoundLineAsync(s.Item, s.PlantId, [Guid.NewGuid()], English);

        Assert.Equal(2, s.Timeline.Entries.Count);
        Assert.Single(other.PhotoIds);
    }

    [Fact]
    public async Task A_found_line_is_written_when_the_plant_has_none()
    {
        var s = new Setup();
        var photo = Guid.NewGuid();

        await s.Service.AddToFoundLineAsync(s.Item, s.PlantId, [photo], English);

        var line = Assert.Single(s.Timeline.Entries);
        Assert.Equal((s.Item.Id, (Guid?)null), (line.PestCaseId, line.PestTreatmentId));
        Assert.Equal(TimelineEvent.Pest, line.Event);
        Assert.Equal([photo], line.PhotoIds);
        Assert.Equal(new FixedTime(Now).MomentOn(s.Item.StartedOn), line.OccurredAt);
        Assert.Equal("Spider mites found", line.Text);
    }

    [Fact]
    public async Task Lines_for_a_case_leave_out_other_cases_and_lines_without_photos_and_come_newest_first()
    {
        var s = new Setup();
        var old = s.FoundLine(Guid.NewGuid());
        var treatment = s.Treatment(daysAgo: 1);
        await s.Service.AddLineAsync(s.Item, treatment, s.PlantId, [Guid.NewGuid()], English);
        s.FoundLine(); // no photos
        s.Timeline.Entries.Add(new TimelineEntry
        {
            SubjectId = s.PlantId, Kind = TimelineKind.Change, Event = TimelineEvent.Pest,
            PestCaseId = Guid.NewGuid(), PhotoIds = [Guid.NewGuid()], OccurredAt = Now
        });

        var lines = await s.Service.LinesForAsync(s.Item.Id);

        Assert.Equal(2, lines.Count);
        Assert.Equal(treatment.Id, lines[0].PestTreatmentId);
        Assert.Same(old, lines[1]);
    }

    [Fact]
    public async Task The_photo_count_is_of_distinct_photos_on_the_treatments_lines()
    {
        var s = new Setup();
        var treatment = s.Treatment();
        var shared = Guid.NewGuid();
        await s.Service.AddLineAsync(s.Item, treatment, s.PlantId, [shared, Guid.NewGuid()], English);
        await s.Service.AddLineAsync(s.Item, treatment, Guid.NewGuid(), [shared], English);

        Assert.Equal(2, await s.Service.PhotoCountAsync(treatment));
        Assert.Equal(0, await s.Service.PhotoCountAsync(s.Treatment()));
    }

    [Fact]
    public async Task Removing_a_treatment_turns_its_line_into_a_photo_entry()
    {
        var s = new Setup();
        var treatment = s.Treatment(what: "Soap");
        var photo = Guid.NewGuid();
        await s.Service.AddLineAsync(s.Item, treatment, s.PlantId, [photo], English);
        var line = s.Timeline.Entries.Single();

        await s.Service.RemoveTreatmentAsync(treatment);

        Assert.True(treatment.IsDeleted);
        Assert.False(line.IsDeleted);
        Assert.Equal(TimelineKind.Photo, line.Kind);
        Assert.Null(line.Event);
        Assert.Null(line.Text);
        Assert.Null(line.Lines);
        Assert.Null(line.PestCaseId);
        Assert.Null(line.PestTreatmentId);
        Assert.Equal([photo], line.PhotoIds);
        Assert.Contains(line.Id, s.Timeline.Updated);
    }

    [Fact]
    public async Task Removing_a_treatment_leaves_other_lines_alone()
    {
        var s = new Setup();
        var kept = s.Treatment();
        var removed = s.Treatment();
        await s.Service.AddLineAsync(s.Item, kept, s.PlantId, [Guid.NewGuid()], English);
        var found = s.FoundLine(Guid.NewGuid());

        await s.Service.RemoveTreatmentAsync(removed);

        Assert.False(kept.IsDeleted);
        Assert.Equal(TimelineEvent.Pest, found.Event);
        Assert.All(s.Timeline.Entries, e => Assert.False(e.IsDeleted));
        Assert.Equal(2, (await s.Service.LinesForAsync(s.Item.Id)).Count);
    }

    [Fact]
    public async Task Deleting_a_case_removes_its_treatments_and_keeps_every_photo()
    {
        var s = new Setup();
        var treated = s.Treatment();
        var checkedOnce = s.Treatment(PestTreatmentKind.Checked, daysAgo: 1);
        var trap = s.Treatment(PestTreatmentKind.TrapCount);
        await s.Service.AddLineAsync(s.Item, treated, s.PlantId, [Guid.NewGuid()], English);
        await s.Service.AddLineAsync(s.Item, checkedOnce, s.PlantId, [Guid.NewGuid()], English);
        var found = s.FoundLine(Guid.NewGuid());

        await s.Service.DeleteCaseAsync(s.Item);

        Assert.True(s.Item.IsDeleted);
        Assert.All(new[] { treated, checkedOnce, trap }, t => Assert.True(t.IsDeleted));
        var photoEntries = s.Timeline.Entries.Where(e => e.Kind == TimelineKind.Photo).ToList();
        Assert.Equal(2, photoEntries.Count);
        Assert.All(photoEntries, e => Assert.False(e.IsDeleted));
        Assert.All(photoEntries, e => Assert.Single(e.PhotoIds));
        Assert.False(found.IsDeleted);
        Assert.Equal(TimelineKind.Change, found.Kind);
        Assert.Equal(s.Item.Id, found.PestCaseId);
    }
}
