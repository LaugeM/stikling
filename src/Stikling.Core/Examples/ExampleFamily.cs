using Stikling.Core.Models;

namespace Stikling.Core.Examples;

/// <summary>Everything the example family is made of.</summary>
public sealed record ExampleRecords(
    IReadOnlyList<Plant> Plants,
    IReadOnlyList<Propagation> Propagations,
    IReadOnlyList<Photo> Photos,
    IReadOnlyList<TimelineEntry> Entries);

/// <summary>
/// An example family for someone who opens the app with nothing in it: a coleus, a young plant
/// potted up from an earlier cutting, and a tip cutting rooting in water now. Coleus roots in about
/// a week, so the whole story fits in the last couple of months. Every record has a fixed id, so
/// devices that load it never end up with two copies, and the dates count back from the day it is
/// loaded.
/// </summary>
public static class ExampleFamily
{
    public static readonly Guid MotherId = new("7db60cf3-d06e-457c-a97c-ff19251aa1a5");
    public static readonly Guid YoungPlantId = new("12144010-7f4f-46b1-a1d8-375e6e9f8b84");
    public static readonly Guid FirstCuttingId = new("dcb06f2f-8458-4ffe-bcb0-98dcecfaa774");
    public static readonly Guid CurrentCuttingId = new("3a2b6072-d59d-48ef-a2b8-9a0509697dd4");

    public static readonly Guid MotherPhotoId = new("891ec11d-4cfd-4d27-b77f-80b1760313f8");
    public static readonly Guid DayOnePhotoId = new("76c4d9f6-b797-4073-967e-69a1d079496d");
    public static readonly Guid FirstRootsPhotoId = new("9948c964-d5be-4864-af62-7bb79dea41f3");
    public static readonly Guid PottedUpPhotoId = new("4bcae88b-f56c-4c89-babf-d95c8a7664ed");
    public static readonly Guid YoungPlantPhotoId = new("fdbe328c-e5c0-48ce-945a-ec66a0734be0");

    private static readonly Guid MotherCreatedId = new("82d45105-f10e-437d-9bc0-000d99eed95f");
    private static readonly Guid MotherFirstCuttingId = new("bfb9db5a-3627-44a8-8ce0-3acb63934d9d");
    private static readonly Guid MotherNoteId = new("2d464788-f76c-4a80-b73b-3947d0d3339d");
    private static readonly Guid MotherCurrentCuttingId = new("917b69e2-0a6e-4063-922c-1e673bf4468c");
    private static readonly Guid FirstCuttingCreatedId = new("fe68092a-dd99-4fd6-81d0-835675e545d5");
    private static readonly Guid FirstCuttingPottedUpId = new("ea1ee814-27f7-4513-bce6-72679dfddec2");
    private static readonly Guid YoungCreatedId = new("59f52745-2fc5-4429-aa06-281688921a15");
    private static readonly Guid YoungPhotoEntryId = new("0d73aad4-c1f9-407e-b98f-44192fcbe52a");
    private static readonly Guid YoungNoteId = new("17635168-99dd-414d-bb7b-cc56edf75e45");
    private static readonly Guid CurrentCreatedId = new("6efde6e3-e041-4272-8ba5-ae5fe59a9935");
    private static readonly Guid CurrentDayOneId = new("d109a720-6efc-4c07-b380-64b7d30a7fe1");
    private static readonly Guid CurrentFirstRootId = new("5e8ca736-bfb6-4b53-aa77-7c110684baea");
    private static readonly Guid CurrentRootsNoteId = new("a124e968-365e-4e27-9e62-ada00a36cec3");

    /// <summary>The five bundled images: the photo id and the file name under wwwroot/example.</summary>
    public static readonly IReadOnlyList<(Guid PhotoId, string FileName)> Images =
    [
        (MotherPhotoId, "mother.jpg"),
        (DayOnePhotoId, "cutting-day-1.jpg"),
        (FirstRootsPhotoId, "first-roots.jpg"),
        (PottedUpPhotoId, "potted-up.jpg"),
        (YoungPlantPhotoId, "young-plant.jpg")
    ];

    /// <summary>The mother plant's nickname, which Today uses while the example is loaded.</summary>
    public const string MotherNickname = "Big Coleus";

    public static readonly IReadOnlyList<Guid> PlantIds = [MotherId, YoungPlantId];
    public static readonly IReadOnlyList<Guid> PropagationIds = [FirstCuttingId, CurrentCuttingId];

    public static readonly IReadOnlyList<Guid> PhotoIds = [.. Images.Select(i => i.PhotoId)];

    public static readonly IReadOnlyList<Guid> EntryIds =
    [
        MotherCreatedId, MotherFirstCuttingId, MotherNoteId, MotherCurrentCuttingId,
        FirstCuttingCreatedId, FirstCuttingPottedUpId,
        YoungCreatedId, YoungPhotoEntryId, YoungNoteId,
        CurrentCreatedId, CurrentDayOneId, CurrentFirstRootId, CurrentRootsNoteId
    ];

    /// <summary>True for the plants and propagations of the example, so the page can tell them from the person's own.</summary>
    public static bool IsExample(Guid id) => PlantIds.Contains(id) || PropagationIds.Contains(id);

    /// <param name="sizes">The size of each stored image by photo id. A photo missing from it is 1200 x 1600.</param>
    public static ExampleRecords Build(TimeProvider time, IReadOnlyDictionary<Guid, (int Width, int Height)>? sizes = null)
    {
        var today = time.Today();
        DateOnly Ago(int days) => today.AddDays(-days);
        DateTimeOffset NoonOn(DateOnly day) => time.MomentAt(day.ToDateTime(new TimeOnly(12, 0)));

        const string batch = "1× cutting in Water";
        var acquired = today.AddMonths(-5);
        var acquiredOn = LooseDate.Of(acquired.Year, acquired.Month);
        var acquiredAt = NoonOn(acquiredOn.Start);

        var mother = new Plant
        {
            Id = MotherId,
            Nickname = MotherNickname,
            Genus = "Coleus",
            Species = "scutellarioides",
            Origin = PlantOrigin.Purchased,
            AcquiredOn = acquiredOn,
            Medium = GrowingMedium.Soil,
            Tags = ["example"],
            CoverPhotoId = MotherPhotoId,
            CreatedAt = acquiredAt,
            Notes = "An example plant, here so you can see what Stikling looks like with a plant and its cuttings in it. Remove it from Today when you've seen enough."
        };
        var young = new Plant
        {
            Id = YoungPlantId,
            Nickname = "Little Coleus",
            Genus = "Coleus",
            Species = "scutellarioides",
            Origin = PlantOrigin.Propagated,
            AcquiredOn = LooseDate.Of(Ago(28)),
            Medium = GrowingMedium.Soil,
            Tags = ["example"],
            ParentPlantId = MotherId,
            FromPropagationId = FirstCuttingId,
            CoverPhotoId = YoungPlantPhotoId,
            CreatedAt = NoonOn(Ago(28)),
            Notes = $"Potted up from the first cutting of {MotherNickname}. An example plant, like the others."
        };

        Propagation Cutting(Guid id, string nickname, int startedAgo) => new()
        {
            Id = id,
            Nickname = nickname,
            Genus = "Coleus",
            Species = "scutellarioides",
            ParentPlantId = MotherId,
            Type = PropagationType.Cutting,
            Medium = GrowingMedium.Water,
            Container = "Glass jar",
            InitialCount = 1,
            StartedOn = Ago(startedAgo),
            CreatedAt = NoonOn(Ago(startedAgo))
        };
        var first = Cutting(FirstCuttingId, "First coleus cutting", 45);
        first.FirstRootOn = Ago(39);
        first.RootedOn = Ago(35);
        first.FirstLeafOn = Ago(33);
        first.PottedUpCount = 1;
        first.Stage = PropagationStage.Done;
        first.FinishedOn = Ago(28);
        var current = Cutting(CurrentCuttingId, "Coleus tip cutting", 12);
        current.FirstRootOn = Ago(6);
        current.Stage = PropagationStage.Rooting;
        current.CoverPhotoId = FirstRootsPhotoId;
        current.Notes = "A tip pinched off the mother plant, with the lowest leaves taken off, in water on a bright windowsill.";

        Photo ShotOf(Guid id, SubjectType type, Guid subject, int daysAgo)
        {
            var (width, height) = sizes is not null && sizes.TryGetValue(id, out var size) ? size : (1200, 1600);
            return new Photo
            {
                Id = id, SubjectType = type, SubjectId = subject, TakenAt = NoonOn(Ago(daysAgo)),
                Width = width, Height = height, CreatedAt = NoonOn(Ago(daysAgo))
            };
        }
        var photos = new List<Photo>
        {
            ShotOf(MotherPhotoId, SubjectType.Plant, MotherId, 12),
            ShotOf(DayOnePhotoId, SubjectType.Propagation, CurrentCuttingId, 12),
            ShotOf(FirstRootsPhotoId, SubjectType.Propagation, CurrentCuttingId, 6),
            ShotOf(PottedUpPhotoId, SubjectType.Plant, YoungPlantId, 28),
            ShotOf(YoungPlantPhotoId, SubjectType.Plant, YoungPlantId, 2)
        };

        TimelineEntry Entry(Guid id, SubjectType type, Guid subject, DateTimeOffset at, TimelineKind kind, string? text,
            Guid? photo = null, SubjectType? relatedType = null, Guid? relatedId = null, TimelineEvent? @event = null) => new()
        {
            Id = id, SubjectType = type, SubjectId = subject, OccurredAt = at, CreatedAt = at, Kind = kind, Text = text,
            PhotoIds = photo is { } p ? [p] : [], RelatedType = relatedType, RelatedId = relatedId, Event = @event
        };
        var plant = SubjectType.Plant;
        var propagation = SubjectType.Propagation;

        var entries = new List<TimelineEntry>
        {
            Entry(MotherCreatedId, plant, MotherId, acquiredAt, TimelineKind.Created, $"Added to collection, {acquiredOn.StoredText()}"),
            Entry(MotherFirstCuttingId, plant, MotherId, NoonOn(Ago(45)), TimelineKind.Propagated, batch, null, propagation, FirstCuttingId),
            Entry(MotherNoteId, plant, MotherId, NoonOn(Ago(12)), TimelineKind.Note,
                "Pinched out the tips to keep it bushy. One of them went into water.", MotherPhotoId),
            Entry(MotherCurrentCuttingId, plant, MotherId, NoonOn(Ago(12)), TimelineKind.Propagated, batch, null, propagation, CurrentCuttingId),

            Entry(FirstCuttingCreatedId, propagation, FirstCuttingId, NoonOn(Ago(45)), TimelineKind.Created, $"Started {batch}"),
            Entry(FirstCuttingPottedUpId, propagation, FirstCuttingId, NoonOn(Ago(28)), TimelineKind.Change,
                "Potted up 1: Little Coleus\nFinished: 1 potted up", null, plant, YoungPlantId, TimelineEvent.PottedUp),

            Entry(YoungCreatedId, plant, YoungPlantId, NoonOn(Ago(28)), TimelineKind.Created, "Potted up from a propagation",
                null, propagation, FirstCuttingId),
            Entry(YoungPhotoEntryId, plant, YoungPlantId, NoonOn(Ago(28)), TimelineKind.Photo, null, PottedUpPhotoId),
            Entry(YoungNoteId, plant, YoungPlantId, NoonOn(Ago(2)), TimelineKind.Note,
                "Pinched the top for the first time, and it's branching out below.", YoungPlantPhotoId),

            Entry(CurrentCreatedId, propagation, CurrentCuttingId, NoonOn(Ago(12)), TimelineKind.Created, $"Started {batch}"),
            Entry(CurrentDayOneId, propagation, CurrentCuttingId, NoonOn(Ago(12)), TimelineKind.Note,
                "Day 1. Cut just below a pair of leaves and took the lowest ones off.", DayOnePhotoId),
            Entry(CurrentFirstRootId, propagation, CurrentCuttingId, NoonOn(Ago(6)), TimelineKind.Note,
                "A week in, and the roots are coming fast.", FirstRootsPhotoId),
            Entry(CurrentRootsNoteId, propagation, CurrentCuttingId, NoonOn(Ago(2)), TimelineKind.Note,
                "Changed the water. The roots are long enough to pot it up soon.")
        };

        return new ExampleRecords([mother, young], [first, current], photos, entries);
    }
}
