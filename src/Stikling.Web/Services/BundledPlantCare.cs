using System.Net.Http.Json;
using Stikling.Core.Plants;

namespace Stikling.Web.Services;

/// <summary>
/// Reads what common houseplants usually like from <c>wwwroot/data/plant-care.json</c>. The service
/// worker caches it with the rest of the app, so it works offline.
/// </summary>
public sealed class BundledPlantCare(HttpClient http) : IPlantCareSource
{
    public async Task<PlantCareData> LoadAsync() =>
        await http.GetFromJsonAsync<PlantCareData>("data/plant-care.json", Stikling.Core.Names.PlantNameData.JsonOptions) ?? PlantCareData.Empty;
}
