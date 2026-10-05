using System.Net.Http.Json;
using Stikling.Core.Names;

namespace Stikling.Web.Services;

/// <summary>
/// Reads the plant names that ship with the app from <c>wwwroot/data</c>. The service worker caches
/// the botanical names and the English everyday names with the rest of the app, so they work offline.
/// Another language is cached the first time a device uses it.
/// </summary>
public sealed class BundledPlantNames(HttpClient http) : IPlantNameSource
{
    public async Task<PlantNameData> LoadAsync() =>
        await http.GetFromJsonAsync<PlantNameData>("data/plant-names.json", PlantNameData.JsonOptions) ?? PlantNameData.Empty;

    public async Task<EverydayNameData> LoadEverydayAsync(string language) =>
        await http.GetFromJsonAsync<EverydayNameData>($"data/everyday-names.{language}.json", PlantNameData.JsonOptions) ?? EverydayNameData.Empty;
}
