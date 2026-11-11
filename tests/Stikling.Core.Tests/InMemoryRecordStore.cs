using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Sync;

namespace Stikling.Core.Tests;

/// <summary>
/// An <see cref="IRecordStore"/> like IndexedDB: each record is kept as its JSON, keyed by kind and
/// id, a later put replaces it, and every read returns fresh copies.
/// </summary>
public sealed class InMemoryRecordStore : IRecordStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, Dictionary<Guid, string>> kinds = [];

    public Task<List<T>> GetAllAsync<T>(RecordKind<T> kind) where T : Entity =>
        Task.FromResult(Stored(kind).Values.Select(json => JsonSerializer.Deserialize<T>(json, Json)!).ToList());

    public Task PutAsync<T>(RecordKind<T> kind, T value) where T : Entity
    {
        Stored(kind)[value.Id] = JsonSerializer.Serialize(value, Json);
        return Task.CompletedTask;
    }

    /// <summary>The ids stored for a kind, for tests that go through every kind without knowing its type.</summary>
    public IReadOnlyCollection<Guid> IdsOf(RecordKind kind) =>
        kinds.TryGetValue(kind.Name, out var stored) ? [.. stored.Keys] : [];

    private Dictionary<Guid, string> Stored(RecordKind kind)
    {
        if (!kinds.TryGetValue(kind.Name, out var stored))
            kinds[kind.Name] = stored = [];
        return stored;
    }
}
