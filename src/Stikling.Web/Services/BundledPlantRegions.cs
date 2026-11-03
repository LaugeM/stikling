using System.Net.Http.Json;
using Stikling.Core.Names;

namespace Stikling.Web.Services;

/// <summary>
/// Reads each genus's family and native areas from <c>wwwroot/data/plant-regions.json</c>. The service
/// worker caches it with the rest of the app, so it works offline.
/// </summary>
public sealed class BundledPlantRegions(HttpClient http) : IPlantRegionSource
{
    public async Task<PlantRegionData> LoadAsync() =>
        await http.GetFromJsonAsync<PlantRegionData>("data/plant-regions.json", PlantNameData.JsonOptions) ?? PlantRegionData.Empty;
}
