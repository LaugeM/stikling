namespace Stikling.Core.People;

/// <summary>
/// Which collection this device has open. The main database, <c>stikling</c>, holds one collection.
/// Every other collection the person is in has a database of its own, <c>stikling-{id}</c>. The choice
/// is kept in localStorage (<c>stikling-open-collection</c>), which <c>db.js</c> reads when it first
/// opens the database, before any of the app is running.
/// </summary>
public static class OpenCollection
{
    /// <summary>The key in <c>DeviceFiles</c>. In localStorage it has the <c>stikling-</c> prefix every such key has.</summary>
    public const string Key = "open-collection";

    public const string MainDatabase = "stikling";

    /// <summary>The id the text names, or null for anything that isn't a lower case GUID with dashes, which means the main database.</summary>
    public static Guid? Parse(string? text) =>
        text is not null && Guid.TryParseExact(text, "D", out var id) && id.ToString("D") == text ? id : null;

    public static string DatabaseName(Guid? collectionId) =>
        collectionId is { } id ? $"{MainDatabase}-{id:D}" : MainDatabase;

    /// <summary>The key for something kept per database, like when it last synced. The main database keeps the plain key.</summary>
    public static string KeyFor(string key, Guid? collectionId) =>
        collectionId is { } id ? $"{key}-{id:D}" : key;
}
