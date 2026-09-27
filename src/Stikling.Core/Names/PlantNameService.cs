using Stikling.Core.Plants;
using Stikling.Core.Propagations;

namespace Stikling.Core.Names;

/// <summary>Reads the plant names that come with the app. In the browser that's a file next to the app.</summary>
public interface IPlantNameSource
{
    Task<PlantNameData> LoadAsync();
}

/// <summary>Name suggestions for the plant and propagation forms.</summary>
public sealed class PlantNameService(IPlantNameSource source, IPlantRepository plants, IPropagationRepository propagations)
{
    private PlantDictionary? dictionary;

    /// <summary>
    /// The names that come with the app, read once. If they can't be read, e.g. offline before the
    /// app has ever cached them, there are none this time and the next call tries again. Typing a
    /// name never depends on it.
    /// </summary>
    public async Task<PlantDictionary> GetDictionaryAsync()
    {
        if (dictionary is not null)
            return dictionary;

        try
        {
            return dictionary = new PlantDictionary(await source.LoadAsync());
        }
        catch (Exception)
        {
            return PlantDictionary.Empty;
        }
    }

    /// <summary>Suggestions from the names that come with the app and the ones on your plants and propagations.</summary>
    public async Task<NameSuggester> GetSuggesterAsync() =>
        new(await GetDictionaryAsync(), await plants.GetAllAsync(), await propagations.GetAllAsync());
}
