using System.Text.Json;

namespace Stikling.Core.People;

/// <summary>One collection the person is in, as this device last heard from the server.</summary>
/// <param name="OwnerName">The owner's name, once it has been fetched with the people of the collection.</param>
/// <param name="Gone">
/// True when the server no longer lists the person in it, because someone removed them. Its database
/// stays on the device until the person removes it.
/// </param>
/// <param name="People">
/// The names of the people in the collection and of those who have left it, by person id, so the name
/// next to an entry shows offline. Only the open collection is refreshed, on each of its syncs.
/// </param>
public sealed record RegisteredCollection(
    Guid Id,
    string Name,
    CollectionRole Role,
    bool ManagesPeople,
    bool IsOwner,
    string? OwnerName = null,
    bool Gone = false,
    Dictionary<Guid, string>? People = null)
{
    public Dictionary<Guid, string> People { get; init; } = People ?? [];

    /// <summary>The person's place here, as the rules read it. A collection that is gone reads as a viewer's.</summary>
    public MemberAccess AccessFor(Guid personId) =>
        Gone ? new MemberAccess(personId, CollectionRole.Viewer, ManagesPeople: false, IsOwner: false)
            : new MemberAccess(personId, Role, ManagesPeople, IsOwner);
}

/// <summary>
/// The collections this device knows about, kept in <c>DeviceFiles</c> under <c>collections</c> and
/// refreshed from <c>GET /me</c> on every sync. It is for this device only: the server is the truth.
/// </summary>
/// <param name="MainCollectionId">The collection the main database holds.</param>
public sealed record CollectionRegistry(
    Guid PersonId,
    string? PersonName,
    Guid? MainCollectionId,
    List<RegisteredCollection> Collections)
{
    public const string Key = "collections";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public RegisteredCollection? Find(Guid id) => Collections.FirstOrDefault(c => c.Id == id);

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>Reads what <see cref="ToJson"/> wrote. Null when there is nothing, or it can't be read.</summary>
    public static CollectionRegistry? FromJson(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CollectionRegistry>(text, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Brings the registry up to date with what <c>/me</c> says.
    /// <list type="bullet">
    /// <item>Someone else signing in on the device starts a new registry. The previous person's shared databases
    /// are returned in <see cref="RegistryUpdate.Discard"/>, since their data is on the server.</item>
    /// <item>The main database holds the collection in its stored sync state if the person is still in it, else
    /// the one they own, else the first. When the stored one is gone, its sync state is forgotten
    /// (<see cref="RegistryUpdate.ForgetMainState"/>), so the next sync sends everything and it merges
    /// into the person's own collection.</item>
    /// <item>A collection that has left <c>/me</c> is kept as gone, except the main database's, which is dropped.</item>
    /// </list>
    /// </summary>
    /// <param name="previous">The registry from before, if any.</param>
    /// <param name="mine">The collections <c>/me</c> lists, with only what it says about them.</param>
    /// <param name="storedMain">
    /// The collection the main database syncs with, from its sync state. When a shared database is open and
    /// the main one can't be read, the registry's own record of it. Null when it has never synced.
    /// </param>
    public static RegistryUpdate Merge(
        CollectionRegistry? previous, Guid personId, string? personName,
        IReadOnlyList<RegisteredCollection> mine, Guid? storedMain)
    {
        var discard = new List<Guid>();
        if (previous is not null && previous.PersonId != personId)
        {
            discard.AddRange(previous.Collections.Where(c => c.Id != previous.MainCollectionId).Select(c => c.Id));
            previous = null;
        }

        var forget = false;
        Guid? main = null;
        if (mine.Count > 0)
        {
            if (storedMain is { } stored && mine.Any(c => c.Id == stored))
                main = stored;
            else
            {
                forget = storedMain is not null;
                main = (mine.FirstOrDefault(c => c.IsOwner) ?? mine[0]).Id;
            }
        }

        var collections = new List<RegisteredCollection>();
        foreach (var c in mine)
        {
            var before = previous?.Find(c.Id);
            collections.Add(c with
            {
                Gone = false,
                OwnerName = c.IsOwner ? personName ?? before?.OwnerName : before?.OwnerName,
                People = before?.People ?? [],
            });
        }

        // One that left /me is kept as gone, so its data can still be seen and saved. Not the one the main
        // database held: that data merges into the person's own collection, so it isn't one to open
        var mainWas = previous?.MainCollectionId;
        foreach (var before in previous?.Collections ?? [])
        {
            if (mine.Any(c => c.Id == before.Id) || before.Id == mainWas || before.Id == storedMain)
                continue;
            collections.Add(before with { Gone = true });
        }

        return new RegistryUpdate(new CollectionRegistry(personId, personName, main, collections), forget, discard);
    }
}

/// <param name="Registry">The registry as it is now.</param>
/// <param name="ForgetMainState">True when the main database's sync state should be forgotten, so it starts over with the person's own collection.</param>
/// <param name="Discard">Shared databases of a person who isn't signed in any more, to delete.</param>
public sealed record RegistryUpdate(CollectionRegistry Registry, bool ForgetMainState, IReadOnlyList<Guid> Discard);
