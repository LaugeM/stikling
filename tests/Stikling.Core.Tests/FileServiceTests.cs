using Stikling.Core.Files;
using Stikling.Core.Models;
using Stikling.Core.Sync;

namespace Stikling.Core.Tests;

public class FileServiceTests
{
    private readonly FakeFileRepository repository = new();
    private readonly MovingTime time = new(new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid plant = Guid.NewGuid();

    private FileService Service() => new(repository, time);

    private Task<AttachedFile> Add(string name = "Receipt.pdf", FileSubject subject = FileSubject.Plant, Guid? id = null) =>
        Service().AddAsync(subject, subject == FileSubject.Plant ? plant : Guid.NewGuid(), id ?? Guid.NewGuid(), name, "application/pdf", 1234);

    [Fact]
    public async Task A_file_is_added_with_the_id_its_bytes_are_kept_under()
    {
        var id = Guid.NewGuid();

        var file = await Service().AddAsync(FileSubject.Plant, plant, id, "  Tag.jpg ", "image/webp", 5000);

        Assert.Equal(id, file.Id);
        Assert.Equal(("Tag.jpg", "image/webp", 5000L), (file.Name, file.ContentType, file.Size));
        Assert.Equal((FileSubject.Plant, plant), (file.SubjectType, file.SubjectId));
        Assert.Equal(time.GetUtcNow(), file.AddedAt);
        Assert.Contains(file, repository.Files.Values);
    }

    [Fact]
    public async Task A_blank_name_becomes_file_and_a_long_one_is_cut()
    {
        var blank = await Service().AddAsync(FileSubject.Plant, plant, Guid.NewGuid(), "  ", "application/pdf", 1);
        var long200 = await Service().AddAsync(FileSubject.Plant, plant, Guid.NewGuid(), new string('a', 300), "application/pdf", 1);

        Assert.Equal("File", blank.Name);
        Assert.Equal(AttachedFile.MaxNameLength, long200.Name.Length);
    }

    [Fact]
    public async Task A_subjects_files_come_newest_first_and_only_the_ones_that_are_not_deleted()
    {
        var first = await Add("First.pdf");
        time.Advance(TimeSpan.FromHours(1));
        var second = await Add("Second.pdf");
        time.Advance(TimeSpan.FromHours(1));
        var gone = await Add("Gone.pdf");
        await Service().DeleteAsync(gone.Id);
        await Add("Other plant", FileSubject.Order);

        var files = await Service().ForSubjectAsync(FileSubject.Plant, plant);

        Assert.Equal([second.Id, first.Id], files.Select(f => f.Id));
        Assert.Empty(await Service().ForSubjectAsync(FileSubject.Propagation, plant));
    }

    [Fact]
    public async Task A_subject_takes_twenty_files_and_the_room_left_is_counted()
    {
        for (var i = 0; i < FileRules.MaxFilesPerRecord - 1; i++)
            await Add($"File {i}");

        Assert.Equal(1, await Service().RoomForAsync(FileSubject.Plant, plant));
        Assert.True(await Service().CanAddAsync(FileSubject.Plant, plant, 1));
        Assert.False(await Service().CanAddAsync(FileSubject.Plant, plant, 2));

        await Add("The last");

        Assert.Equal(0, await Service().RoomForAsync(FileSubject.Plant, plant));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Add("One too many"));
    }

    [Fact]
    public async Task Deleting_a_file_makes_room_and_other_subjects_have_their_own_twenty()
    {
        var files = new List<AttachedFile>();
        for (var i = 0; i < FileRules.MaxFilesPerRecord; i++)
            files.Add(await Add($"File {i}"));

        await Service().DeleteAsync(files[0].Id);

        Assert.Equal(1, await Service().RoomForAsync(FileSubject.Plant, plant));
        Assert.Equal(FileRules.MaxFilesPerRecord, await Service().RoomForAsync(FileSubject.Plant, Guid.NewGuid()));
    }

    [Fact]
    public async Task A_merge_that_leaves_more_than_twenty_only_blocks_adding()
    {
        for (var i = 0; i < FileRules.MaxFilesPerRecord + 3; i++)
            repository.Files[Guid.NewGuid()] = new AttachedFile { SubjectType = FileSubject.Plant, SubjectId = plant, Name = $"File {i}" };

        Assert.Equal(0, await Service().RoomForAsync(FileSubject.Plant, plant));
        Assert.Equal(FileRules.MaxFilesPerRecord + 3, (await Service().ForSubjectAsync(FileSubject.Plant, plant)).Count);
    }

    [Fact]
    public async Task A_file_is_renamed_with_its_name_trimmed()
    {
        var file = await Add("Receipt.pdf");

        Assert.True(await Service().RenameAsync(file.Id, "  Pot receipt  "));

        Assert.Equal("Pot receipt", repository.Files[file.Id].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task An_empty_name_is_refused_and_the_old_one_stays(string? name)
    {
        var file = await Add("Receipt.pdf");

        Assert.False(await Service().RenameAsync(file.Id, name));

        Assert.Equal("Receipt.pdf", repository.Files[file.Id].Name);
    }

    [Fact]
    public async Task A_name_over_200_characters_is_refused_and_200_is_taken()
    {
        var file = await Add("Receipt.pdf");

        Assert.False(await Service().RenameAsync(file.Id, new string('a', 201)));
        Assert.True(await Service().RenameAsync(file.Id, new string('b', 200)));

        Assert.Equal(new string('b', 200), repository.Files[file.Id].Name);
    }

    [Fact]
    public async Task Renaming_a_file_that_is_not_there_or_is_deleted_does_nothing()
    {
        var file = await Add();
        await Service().DeleteAsync(file.Id);

        Assert.False(await Service().RenameAsync(file.Id, "New name"));
        Assert.False(await Service().RenameAsync(Guid.NewGuid(), "New name"));
    }

    [Fact]
    public async Task Deleting_is_soft_and_leaves_the_record_with_its_dates()
    {
        var file = await Add();

        await Service().DeleteAsync(file.Id);

        Assert.True(repository.Files[file.Id].IsDeleted);
        Assert.Empty(await repository.GetAllAsync());
    }

    private sealed class MovingTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan by) => current += by;
    }
}
