using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Settings;

namespace Stikling.Core.Names;

/// <summary>Reads the plant names that come with the app. In the browser those are files next to the app.</summary>
public interface IPlantNameSource
{
    Task<PlantNameData> LoadAsync();

    /// <summary>The everyday names in one language, "en" or "da".</summary>
    Task<EverydayNameData> LoadEverydayAsync(string language);
}

/// <summary>The languages the device says its person reads, most preferred first, like "da-DK" and "en".</summary>
public interface IDeviceLanguages
{
    Task<IReadOnlyList<string>> GetAsync();
}

/// <summary>Name suggestions for the plant and propagation forms, and the everyday names shown on plants.</summary>
public sealed class PlantNameService(
    IPlantNameSource source,
    IPlantRepository plants,
    IPropagationRepository propagations,
    SettingsService settings,
    IDeviceLanguages deviceLanguages)
{
    private PlantNameData? data;
    private readonly Dictionary<string, EverydayNameData> everyday = [];
    private Task<Languages>? languages;
    private Task<(EverydayNameBook Book, bool Complete)>? book;
    private (Languages Languages, int Files, PlantDictionary Dictionary)? dictionary;

    /// <summary>
    /// The names that come with the app, read once. If they can't be read, e.g. offline before the
    /// app has ever cached them, there are none this time and the next call tries again. Typing a
    /// name never depends on it.
    /// </summary>
    public async Task<PlantDictionary> GetDictionaryAsync()
    {
        var wanted = await GetLanguagesAsync();
        if (dictionary is { } built && built.Languages == wanted && built.Files == wanted.Loaded.Length)
            return built.Dictionary;

        try
        {
            data ??= await source.LoadAsync();
        }
        catch (Exception)
        {
            return PlantDictionary.Empty;
        }

        // A file that couldn't be read is tried again next time, but the dictionary is only built
        // again when one more of them has come in
        var (files, _) = await LoadEverydayAsync(wanted);
        if (dictionary is { } partial && partial.Languages == wanted && partial.Files == files.Count)
            return partial.Dictionary;

        var made = new PlantDictionary(data, files, wanted.Shown);
        dictionary = (wanted, files.Count, made);
        return made;
    }

    /// <summary>
    /// The everyday names to show on plants, which only needs the small files of everyday names, not
    /// the botanical ones. Read once and shared by every list and page.
    /// </summary>
    public async Task<EverydayNameBook> GetEverydayNamesAsync()
    {
        var loading = book ??= LoadBookAsync();
        var (names, complete) = await loading;
        // Offline before the files were ever cached: none this time, and the next call tries again
        if (!complete && book == loading)
            book = null;
        return names;
    }

    /// <summary>The language of the everyday names shown, as chosen in Settings or else taken from the device.</summary>
    public async Task<EverydayNameLanguage> GetEverydayLanguageAsync() =>
        (await GetLanguagesAsync()).Shown == "da" ? EverydayNameLanguage.Danish : EverydayNameLanguage.English;

    public async Task SetEverydayLanguageAsync(EverydayNameLanguage language)
    {
        await settings.SetEverydayNamesAsync(language);
        SettingsChanged();
    }

    /// <summary>
    /// Reads the language chosen again next time, after the settings came from somewhere else:
    /// another device through sync, or a backup.
    /// </summary>
    public void SettingsChanged()
    {
        languages = null;
        book = null;
    }

    /// <summary>Suggestions from the names that come with the app and the ones on your plants and propagations.</summary>
    public async Task<NameSuggester> GetSuggesterAsync() =>
        new(await GetDictionaryAsync(), await plants.GetAllAsync(), await propagations.GetAllAsync());

    private async Task<(EverydayNameBook, bool)> LoadBookAsync()
    {
        var wanted = await GetLanguagesAsync();
        var (files, complete) = await LoadEverydayAsync(wanted);
        return (new EverydayNameBook(files, wanted.Shown), complete);
    }

    private async Task<(List<EverydayNameData> Files, bool Complete)> LoadEverydayAsync(Languages wanted)
    {
        var files = new List<EverydayNameData>();
        var complete = true;
        foreach (var language in wanted.Loaded)
        {
            if (!everyday.TryGetValue(language, out var file))
            {
                try
                {
                    everyday[language] = file = await source.LoadEverydayAsync(language);
                }
                catch (Exception)
                {
                    complete = false;
                    continue;
                }
            }
            files.Add(file);
        }
        return (files, complete);
    }

    private Task<Languages> GetLanguagesAsync() => languages ??= DecideLanguagesAsync();

    // English is always loaded. Danish is loaded on a device whose browser lists Danish, so typing
    // "svigermors tunge" works there whichever names are shown, and wherever Danish is chosen.
    private async Task<Languages> DecideLanguagesAsync()
    {
        var chosen = (await settings.GetAsync()).EverydayNames;
        bool danishDevice;
        try
        {
            danishDevice = (await deviceLanguages.GetAsync()).Any(l => l.StartsWith("da", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            danishDevice = false;
        }

        var shown = chosen switch
        {
            EverydayNameLanguage.Danish => "da",
            EverydayNameLanguage.English => "en",
            _ => danishDevice ? "da" : "en"
        };
        return new Languages(danishDevice || shown == "da" ? "en,da" : "en", shown);
    }

    /// <param name="List">The languages loaded, comma separated, so two choices compare by value.</param>
    private sealed record Languages(string List, string Shown)
    {
        public string[] Loaded => List.Split(',');
    }
}
