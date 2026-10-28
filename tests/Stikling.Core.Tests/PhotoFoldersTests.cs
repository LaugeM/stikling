using Stikling.Core.Backup;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class PhotoFoldersTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(2), "test", "test");
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly PhotoFolderOptions All = new(IncludeGone: true);

    private static Plant APlant(string? nickname, string? number = null, PlantStatus status = PlantStatus.Active, int day = 1) =>
        new() { Nickname = nickname, CollectionNumber = number, Status = status, CreatedAt = Created.AddDays(day) };

    private static Photo APhoto(SubjectType type, Guid subject, DateTimeOffset taken) =>
        new() { SubjectType = type, SubjectId = subject, TakenAt = taken, CreatedAt = Created };

    private static DateTimeOffset At(int hour, int minute, int second = 0) => new(2026, 10, 15, hour, minute, second, TimeSpan.Zero);

    private static List<string> Paths(
        IEnumerable<Plant>? plants = null, IEnumerable<Propagation>? propagations = null, IEnumerable<Pot>? pots = null,
        IEnumerable<Photo>? photos = null, PhotoFolderOptions? options = null) =>
        PhotoFolders.Plan(plants ?? [], propagations ?? [], pots ?? [], photos ?? [], options ?? All, Zone)
            .Select(p => p.Path).ToList();

    [Fact]
    public void A_plant_folder_has_the_collection_number_and_name_when_there_is_a_number()
    {
        var numbered = APlant("Big Monstera", "117");
        var plain = APlant("Ficus");
        var paths = Paths([numbered, plain],
            photos: [APhoto(SubjectType.Plant, numbered.Id, At(10, 0)), APhoto(SubjectType.Plant, plain.Id, At(11, 0))]);

        Assert.Equal(["Plants/117 Big Monstera/2026-10-15 12-00", "Plants/Ficus/2026-10-15 13-00"], paths);
    }

    [Fact]
    public void Files_are_named_by_the_local_time_taken()
    {
        var plant = APlant("Ficus");
        var paths = Paths([plant], photos: [APhoto(SubjectType.Plant, plant.Id, At(23, 32))]);

        Assert.Equal(["Plants/Ficus/2026-10-16 01-32"], paths);
    }

    [Fact]
    public void A_propagation_folder_has_its_name_and_the_month_it_started()
    {
        var propagation = new Propagation { Nickname = "Coleus", StartedOn = new DateOnly(2026, 9, 3), CreatedAt = Created };
        var paths = Paths(propagations: [propagation], photos: [APhoto(SubjectType.Propagation, propagation.Id, At(8, 0))]);

        Assert.Equal(["Propagations/Coleus 2026-09/2026-10-15 10-00"], paths);
    }

    [Fact]
    public void A_pot_folder_has_its_name_or_Pot_without_one()
    {
        var named = new Pot { Name = "Clear nursery pot", CreatedAt = Created };
        var unnamed = new Pot { Name = null, CreatedAt = Created.AddDays(1) };
        var paths = Paths(pots: [named, unnamed],
            photos: [APhoto(SubjectType.Pot, named.Id, At(8, 0)), APhoto(SubjectType.Pot, unnamed.Id, At(9, 0))]);

        Assert.Equal(["Pots/Clear nursery pot/2026-10-15 10-00", "Pots/Pot/2026-10-15 11-00"], paths);
    }

    [Theory]
    [InlineData("a/b\\c:d*e?f\"g<h>i|j", "a b c d e f g h i j")]
    [InlineData("  too   many   spaces  ", "too many spaces")]
    [InlineData("Ends with dots...", "Ends with dots")]
    [InlineData("Tab\there\u0001", "Tabhere")]
    [InlineData("", "Unnamed")]
    [InlineData("???", "Unnamed")]
    [InlineData(null, "Unnamed")]
    [InlineData("CON", "_CON")]
    [InlineData("com1.old", "_com1.old")]
    [InlineData("Console", "Console")]
    public void Names_are_made_safe_for_file_systems(string? name, string expected) =>
        Assert.Equal(expected, PhotoFolders.SafeName(name));

    [Fact]
    public void Long_names_are_cut_at_80_characters_without_a_trailing_space()
    {
        var name = new string('a', 79) + " bbb";
        var safe = PhotoFolders.SafeName(name);

        Assert.Equal(new string('a', 79), safe);
        Assert.True(PhotoFolders.SafeName(new string('x', 200)).Length <= 80);
    }

    [Fact]
    public void Subjects_with_the_same_folder_name_are_numbered_oldest_first()
    {
        var newer = APlant("Pothos", day: 5);
        var older = APlant("pothos", day: 2);
        var oldest = APlant("Pothos", day: 1);
        var paths = Paths([newer, older, oldest], photos:
        [
            APhoto(SubjectType.Plant, newer.Id, At(1, 0)),
            APhoto(SubjectType.Plant, older.Id, At(2, 0)),
            APhoto(SubjectType.Plant, oldest.Id, At(3, 0))
        ]);

        Assert.Equal(
            ["Plants/Pothos (3)/2026-10-15 03-00", "Plants/pothos (2)/2026-10-15 04-00", "Plants/Pothos/2026-10-15 05-00"],
            paths);
    }

    [Fact]
    public void A_second_photo_in_the_same_minute_in_the_same_folder_is_numbered()
    {
        var plant = APlant("Ficus");
        var other = APlant("Hoya");
        var paths = Paths([plant, other], photos:
        [
            APhoto(SubjectType.Plant, plant.Id, At(10, 5, 10)),
            APhoto(SubjectType.Plant, plant.Id, At(10, 5, 40)),
            APhoto(SubjectType.Plant, plant.Id, At(10, 5, 50)),
            APhoto(SubjectType.Plant, other.Id, At(10, 5, 20))
        ]);

        Assert.Equal(
            [
                "Plants/Ficus/2026-10-15 12-05", "Plants/Ficus/2026-10-15 12-05 (2)", "Plants/Ficus/2026-10-15 12-05 (3)",
                "Plants/Hoya/2026-10-15 12-05"
            ],
            paths.OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void Gone_plants_and_finished_propagations_are_left_out_unless_included()
    {
        var active = APlant("Active");
        var died = APlant("Died", status: PlantStatus.Died);
        var sold = APlant("Sold", status: PlantStatus.Sold);
        var going = new Propagation { Nickname = "Going", Stage = PropagationStage.Rooting, StartedOn = new DateOnly(2026, 1, 1), CreatedAt = Created };
        var done = new Propagation { Nickname = "Done", Stage = PropagationStage.Done, StartedOn = new DateOnly(2026, 1, 1), CreatedAt = Created };
        var failed = new Propagation { Nickname = "Failed", Stage = PropagationStage.Failed, StartedOn = new DateOnly(2026, 1, 1), CreatedAt = Created };
        List<Photo> photos =
        [
            .. new[] { active, died, sold }.Select(p => APhoto(SubjectType.Plant, p.Id, At(1, 0))),
            .. new[] { going, done, failed }.Select(p => APhoto(SubjectType.Propagation, p.Id, At(2, 0)))
        ];

        var kept = Paths([active, died, sold], [going, done, failed], photos: photos, options: new(IncludeGone: false));
        var everything = Paths([active, died, sold], [going, done, failed], photos: photos, options: All);

        Assert.Equal(2, kept.Count);
        Assert.Contains(kept, p => p.StartsWith("Plants/Active/"));
        Assert.Contains(kept, p => p.StartsWith("Propagations/Going "));
        Assert.Equal(6, everything.Count);
    }

    [Fact]
    public void Deleted_photos_and_photos_of_deleted_or_missing_subjects_are_left_out()
    {
        var plant = APlant("Ficus");
        var deletedPlant = APlant("Gone");
        deletedPlant.DeletedAt = Created;
        var deletedPhoto = APhoto(SubjectType.Plant, plant.Id, At(1, 0));
        deletedPhoto.DeletedAt = Created;
        var kept = APhoto(SubjectType.Plant, plant.Id, At(2, 0));

        var plan = PhotoFolders.Plan(
            [plant, deletedPlant], [], [],
            [deletedPhoto, kept, APhoto(SubjectType.Plant, deletedPlant.Id, At(3, 0)), APhoto(SubjectType.Plant, Guid.NewGuid(), At(4, 0))],
            All, Zone);

        Assert.Equal([kept.Id], plan.Select(p => p.PhotoId));
    }

    [Fact]
    public void Reference_photos_go_in_their_own_folder_or_are_left_out()
    {
        var plant = new Plant { Nickname = "Fern" };
        var own = new Photo { SubjectType = SubjectType.Plant, SubjectId = plant.Id, TakenAt = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero) };
        var reference = new Photo { SubjectType = SubjectType.Plant, SubjectId = plant.Id, IsReference = true, TakenAt = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero) };

        var all = PhotoFolders.Plan([plant], [], [], [own, reference], new(IncludeGone: true), TimeZoneInfo.Utc);
        var without = PhotoFolders.Plan([plant], [], [], [own, reference], new(IncludeGone: true, IncludeReferences: false), TimeZoneInfo.Utc);

        Assert.Equal(["Plants/Fern/2026-10-01 12-00", "Plants/Fern/Reference/2026-10-02 12-00"], all.Select(p => p.Path));
        Assert.Equal(["Plants/Fern/2026-10-01 12-00"], without.Select(p => p.Path));
    }
}
