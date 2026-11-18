using Stikling.Core.Models;
using Stikling.Core.People;

namespace Stikling.Core.Tests;

public class EntryAccessTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static MemberAccess As(CollectionRole role) => new(Me, role, ManagesPeople: false, IsOwner: false);

    [Theory]
    [InlineData(TimelineKind.Note)]
    [InlineData(TimelineKind.Photo)]
    [InlineData(TimelineKind.Change)]
    public void A_viewer_adds_nothing(TimelineKind kind) => Assert.False(As(CollectionRole.Viewer).CanAdd(kind));

    [Theory]
    [InlineData(TimelineKind.Note, true)]
    [InlineData(TimelineKind.Photo, false)]
    [InlineData(TimelineKind.Change, false)]
    [InlineData(TimelineKind.Created, false)]
    [InlineData(TimelineKind.Propagated, false)]
    public void A_helper_adds_notes_only(TimelineKind kind, bool allowed) => Assert.Equal(allowed, As(CollectionRole.Helper).CanAdd(kind));

    [Fact]
    public void An_editor_adds_every_kind() =>
        Assert.All(Enum.GetValues<TimelineKind>(), kind => Assert.True(As(CollectionRole.Editor).CanAdd(kind)));

    [Fact]
    public void A_helper_changes_their_own_note_and_nobody_elses()
    {
        var helper = As(CollectionRole.Helper);

        Assert.True(helper.CanChangeEntry(new TimelineEntry { Kind = TimelineKind.Note, CreatedBy = Me }));
        Assert.False(helper.CanChangeEntry(new TimelineEntry { Kind = TimelineKind.Note, CreatedBy = Other }));
        Assert.False(helper.CanChangeEntry(new TimelineEntry { Kind = TimelineKind.Note, CreatedBy = null }));
    }

    [Theory]
    [InlineData(TimelineKind.Photo)]
    [InlineData(TimelineKind.Change)]
    [InlineData(TimelineKind.Created)]
    public void A_helper_cannot_change_other_kinds_even_with_their_name_on_them(TimelineKind kind) =>
        Assert.False(As(CollectionRole.Helper).CanChangeEntry(new TimelineEntry { Kind = kind, CreatedBy = Me }));

    [Fact]
    public void A_viewer_changes_nothing()
    {
        var viewer = As(CollectionRole.Viewer);

        Assert.False(viewer.CanChangeEntry(new TimelineEntry { Kind = TimelineKind.Note, CreatedBy = Me }));
        Assert.False(viewer.CanChangeEntry(new CareLog { CreatedBy = Me }));
        Assert.False(viewer.CanChangeEntry(new PropagationCareLog { CreatedBy = Me }));
        Assert.False(viewer.CanChangeEntry(new Photo { CreatedBy = Me }));
    }

    [Fact]
    public void A_helper_changes_their_own_care_entries_and_photos_only()
    {
        var helper = As(CollectionRole.Helper);

        Assert.True(helper.CanChangeEntry(new CareLog { CreatedBy = Me }));
        Assert.False(helper.CanChangeEntry(new CareLog { CreatedBy = Other }));
        Assert.True(helper.CanChangeEntry(new PropagationCareLog { CreatedBy = Me }));
        Assert.False(helper.CanChangeEntry(new PropagationCareLog { CreatedBy = null }));
        Assert.True(helper.CanChangeEntry(new Photo { CreatedBy = Me }));
        Assert.False(helper.CanChangeEntry(new Photo { CreatedBy = Other }));
    }

    [Fact]
    public void An_editor_changes_everything_and_so_does_someone_on_their_own_device()
    {
        var editor = As(CollectionRole.Editor);
        var own = MemberAccess.Full(Guid.Empty);

        Assert.True(editor.CanChangeEntry(new TimelineEntry { Kind = TimelineKind.Change, CreatedBy = Other }));
        Assert.True(editor.CanChangeEntry(new CareLog { CreatedBy = Other }));
        Assert.True(own.CanChangeEntry(new Photo()));
        Assert.True(own.CanChangeEntry(new TimelineEntry { Kind = TimelineKind.Change }));
    }
}
