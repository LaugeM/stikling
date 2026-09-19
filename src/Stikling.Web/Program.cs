using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Stikling.Core.Plants;
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
builder.Services.AddScoped<ITimelineRepository, IndexedDbTimelineRepository>();
builder.Services.AddScoped<IPhotoRepository, IndexedDbPhotoRepository>();
builder.Services.AddScoped<PlantService>();
builder.Services.AddScoped<PhotoService>();

await builder.Build().RunAsync();
