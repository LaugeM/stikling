using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Stikling.Core.Care;
using Stikling.Core.Pests;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;
using Stikling.Web;
using Stikling.Web.Services;

// The app is in English, so dates read "19 Sep 2026" regardless of the browser's language
var culture = new CultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<IndexedDb>();
builder.Services.AddScoped<IPlantRepository, IndexedDbPlantRepository>();
builder.Services.AddScoped<IPropagationRepository, IndexedDbPropagationRepository>();
builder.Services.AddScoped<ICareLogRepository, IndexedDbCareLogRepository>();
builder.Services.AddScoped<IPestCaseRepository, IndexedDbPestCaseRepository>();
builder.Services.AddScoped<IPestTreatmentRepository, IndexedDbPestTreatmentRepository>();
builder.Services.AddScoped<IPotRepository, IndexedDbPotRepository>();
builder.Services.AddScoped<ITimelineRepository, IndexedDbTimelineRepository>();
builder.Services.AddScoped<IPhotoRepository, IndexedDbPhotoRepository>();
builder.Services.AddScoped<PlantService>();
builder.Services.AddScoped<PropagationService>();
builder.Services.AddScoped<RoomService>();
builder.Services.AddScoped<PotService>();
builder.Services.AddScoped<CareService>();
builder.Services.AddScoped<PhotoService>();
builder.Services.AddScoped<DeviceFiles>();
builder.Services.AddScoped<BackupService>();

await builder.Build().RunAsync();
