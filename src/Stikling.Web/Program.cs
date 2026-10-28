using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sentry;
using Stikling.Core.Away;
using Stikling.Core.Care;
using Stikling.Core.Examples;
using Stikling.Core.Feeds;
using Stikling.Core.Fields;
using Stikling.Core.Flowering;
using Stikling.Core.Wishlist;
using Stikling.Core.Import;
using Stikling.Core.Lights;
using Stikling.Core.Names;
using Stikling.Core.Pests;
using Stikling.Core.Problems;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Products;
using Stikling.Core.SoilMixes;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Search;
using Stikling.Core.Sellers;
using Stikling.Core.Settings;
using Stikling.Core.Sharing;
using Stikling.Core.Timeline;
using Stikling.Core.Today;
using Stikling.Web;
using Stikling.Web.Services;

// English until the language is worked out below, once the host exists. English keeps en-GB, so dates
// read the way they always have and not the way the browser's language writes them
SetCulture(AppLanguages.English);

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Error reports. With no address in appsettings.Production.json the SDK is off. SentrySetup has the
// rest: anonymous, no query strings, and nothing sent once the person has turned reports off.
builder.UseSentry(options => SentrySetup.Configure(options, builder.Configuration["Sentry:Dsn"]));
builder.Logging.AddSentry(options =>
{
    options.InitializeSdk = false;
    options.MinimumEventLevel = LogLevel.Error;
    options.MinimumBreadcrumbLevel = LogLevel.Warning;
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<LanguageService>();
builder.Services.AddScoped<IAppLanguage>(services => services.GetRequiredService<LanguageService>());
builder.Services.AddScoped<ErrorReports>();
builder.Services.AddScoped<IndexedDb>();
builder.Services.AddScoped<IPlantRepository, IndexedDbPlantRepository>();
builder.Services.AddScoped<IPropagationRepository, IndexedDbPropagationRepository>();
builder.Services.AddScoped<ICareLogRepository, IndexedDbCareLogRepository>();
builder.Services.AddScoped<IPropagationCareLogRepository, IndexedDbPropagationCareLogRepository>();
builder.Services.AddScoped<IPestCaseRepository, IndexedDbPestCaseRepository>();
builder.Services.AddScoped<IPestTreatmentRepository, IndexedDbPestTreatmentRepository>();
builder.Services.AddScoped<ITreatmentRecipeRepository, IndexedDbTreatmentRecipeRepository>();
builder.Services.AddScoped<ITreatmentPlanRepository, IndexedDbTreatmentPlanRepository>();
builder.Services.AddScoped<IPotRepository, IndexedDbPotRepository>();
builder.Services.AddScoped<IPlaceRepository, IndexedDbPlaceRepository>();
builder.Services.AddScoped<ISoilMixRepository, IndexedDbSoilMixRepository>();
builder.Services.AddScoped<IProductRepository, IndexedDbProductRepository>();
builder.Services.AddScoped<IFeedRepository, IndexedDbFeedRepository>();
builder.Services.AddScoped<ISettingsRepository, IndexedDbSettingsRepository>();
builder.Services.AddScoped<IPutOffRepository, IndexedDbPutOffRepository>();
builder.Services.AddScoped<IGrowLightRepository, IndexedDbGrowLightRepository>();
builder.Services.AddScoped<IAwayStretchRepository, IndexedDbAwayStretchRepository>();
builder.Services.AddScoped<ISitterSheetRepository, IndexedDbSitterSheetRepository>();
builder.Services.AddScoped<IFloweringMarkRepository, IndexedDbFloweringMarkRepository>();
builder.Services.AddScoped<IProblemRepository, IndexedDbProblemRepository>();
builder.Services.AddScoped<IWishRepository, IndexedDbWishRepository>();
builder.Services.AddScoped<ISellerRepository, IndexedDbSellerRepository>();
builder.Services.AddScoped<IOwnCareKindRepository, IndexedDbOwnCareKindRepository>();
builder.Services.AddScoped<IOwnFieldRepository, IndexedDbOwnFieldRepository>();
builder.Services.AddScoped<ICareIntervalRepository, IndexedDbCareIntervalRepository>();
builder.Services.AddScoped<IProblemUpdateRepository, IndexedDbProblemUpdateRepository>();
builder.Services.AddScoped<IReceivedHistoryRepository, IndexedDbReceivedHistoryRepository>();
builder.Services.AddScoped<ITimelineRepository, IndexedDbTimelineRepository>();
builder.Services.AddScoped<IPhotoRepository, IndexedDbPhotoRepository>();
builder.Services.AddScoped<IPlantNameSource, BundledPlantNames>();
builder.Services.AddScoped<IPlantCareSource, BundledPlantCare>();
builder.Services.AddScoped<IDeviceLanguages, BrowserLanguages>();
builder.Services.AddScoped<TimelineService>();
builder.Services.AddScoped<PlantService>();
builder.Services.AddScoped<PlantForms>();
builder.Services.AddScoped<PropagationForms>();
builder.Services.AddScoped<UnitForms>();
builder.Services.AddScoped<CollectionNumberService>();
builder.Services.AddScoped<PropagationService>();
builder.Services.AddScoped<PlantNameService>();
builder.Services.AddScoped<PlantCareService>();
builder.Services.AddScoped<RoomService>();
builder.Services.AddScoped<SellerService>();
builder.Services.AddScoped<OwnCareKindService>();
builder.Services.AddScoped<OwnFieldService>();
builder.Services.AddScoped<CareIntervalService>();
builder.Services.AddScoped<PotService>();
builder.Services.AddScoped<SoilMixService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<FeedService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<PutOffService>();
builder.Services.AddScoped<WateringAnswers>();
builder.Services.AddScoped<TodayService>();
builder.Services.AddScoped<SubjectPhotos>();
builder.Services.AddScoped<GrowLightService>();
builder.Services.AddScoped<AwayService>();
builder.Services.AddScoped<SitterSheetService>();
builder.Services.AddScoped<FloweringService>();
builder.Services.AddScoped<ProblemService>();
builder.Services.AddScoped<WishService>();
builder.Services.AddScoped<CareService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<SpreadsheetImportService>();
builder.Services.AddScoped<PropagationCareService>();
builder.Services.AddScoped<PhotoService>();
builder.Services.AddScoped<IExamplePhotos, ExamplePhotos>();
builder.Services.AddScoped<ExampleService>();
builder.Services.AddScoped<TourService>();
builder.Services.AddScoped<CoverPhotos>();
builder.Services.AddSingleton(GoogleSettings.From(builder.Configuration));
builder.Services.AddScoped<GooglePhotosService>();
builder.Services.AddScoped<ShareCardService>();
builder.Services.AddScoped<LabelPrinter>();
builder.Services.AddScoped<YearCardService>();
builder.Services.AddScoped<SwapCardService>();
builder.Services.AddScoped<TimelapseService>();
builder.Services.AddScoped<DeviceFiles>();
builder.Services.AddScoped<AppBadge>();
builder.Services.AddScoped<SitterSheetActions>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddSingleton(AccountSettings.From(builder.Configuration, builder.HostEnvironment.IsDevelopment()));
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<StiklingApi>();
builder.Services.AddScoped<IShareLinkServer>(services => services.GetRequiredService<StiklingApi>());
builder.Services.AddScoped<ShareLinkService>();
builder.Services.AddScoped<SharedCopies>();
builder.Services.AddScoped<IShareCopySource>(services => services.GetRequiredService<SharedCopies>());
builder.Services.AddScoped<IReceivedPhotos>(services => services.GetRequiredService<SharedCopies>());
builder.Services.AddScoped<ReceivingService>();
builder.Services.AddScoped<IndexedDbSyncStore>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<SyncRunner>();

var host = builder.Build();
var jsRuntime = (IJSInProcessRuntime)host.Services.GetRequiredService<IJSRuntime>();
SentrySetup.Runtime = jsRuntime;

// The language is chosen before the app runs, since its text is looked up as it renders. It comes from the
// copy of the saved choice in localStorage, or else the browser. Languages still being translated are only
// offered in development builds.
try
{
    var chosen = jsRuntime.Invoke<string?>("stiklingLanguage.get");
    var browser = jsRuntime.Invoke<string[]>("stiklingLanguage.browser");
    SetCulture(AppLanguages.Resolve(chosen, browser, builder.HostEnvironment.IsDevelopment()));
}
catch (Exception)
{
    // Without it the app is in English
}

await host.RunAsync();

static void SetCulture(string language)
{
    var culture = new CultureInfo(AppLanguages.CultureOf(language));
    CultureInfo.DefaultThreadCurrentCulture = culture;
    CultureInfo.DefaultThreadCurrentUICulture = culture;
    CultureInfo.CurrentCulture = culture;
    CultureInfo.CurrentUICulture = culture;
}
