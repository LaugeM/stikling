using System.Net.Http.Json;
using Stikling.Core.Names;

namespace Stikling.Web.Services;

/// <summary>
/// Reads the plant names that ship with the app from <c>wwwroot/data/plant-names.json</c>. The
/// service worker caches the file with the rest of the app, so it works offline.
/// </summary>
public sealed class BundledPlantNames(HttpClient http) : IPlantNameSource
{
    public async Task<PlantNameData> LoadAsync() =>
        await http.GetFromJsonAsync<PlantNameData>("data/plant-names.json", PlantNameData.JsonOptions) ?? PlantNameData.Empty;
}
