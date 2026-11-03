namespace Stikling.Core.Names;

/// <summary>Reads the regions file that comes with the app. In the browser that is a file next to the app.</summary>
public interface IPlantRegionSource
{
    Task<PlantRegionData> LoadAsync();
}

/// <summary>Where plants grow wild, read once and shared by the plant pages.</summary>
public sealed class PlantRegionService(IPlantRegionSource source)
{
    private PlantRegionBook? book;

    /// <summary>
    /// The book, read once. If it can't be read, e.g. offline before the app has ever cached it, it is
    /// empty this time and the next call tries again.
    /// </summary>
    public async Task<PlantRegionBook> GetBookAsync()
    {
        if (book is not null)
            return book;

        try
        {
            return book = new PlantRegionBook(await source.LoadAsync());
        }
        catch (Exception)
        {
            return PlantRegionBook.Empty;
        }
    }
}
