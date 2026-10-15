namespace Stikling.Core.Plants;

/// <summary>Reads the care file that comes with the app. In the browser that is a file next to the app.</summary>
public interface IPlantCareSource
{
    Task<PlantCareData> LoadAsync();
}

/// <summary>What common houseplants usually like, read once and shared by the plant form and pages.</summary>
public sealed class PlantCareService(IPlantCareSource source)
{
    private PlantCareBook? book;

    /// <summary>
    /// The book, read once. If it can't be read, e.g. offline before the app has ever cached it, it is
    /// empty this time and the next call tries again.
    /// </summary>
    public async Task<PlantCareBook> GetBookAsync()
    {
        if (book is not null)
            return book;

        try
        {
            return book = new PlantCareBook(await source.LoadAsync());
        }
        catch (Exception)
        {
            return PlantCareBook.Empty;
        }
    }
}
