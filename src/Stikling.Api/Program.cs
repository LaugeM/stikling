using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.AspNetCore.HttpOverrides;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Auth;
using Stikling.Api.Collections;
using Stikling.Api.Data;
using Stikling.Api.People;
using Stikling.Api.Photos;
using Stikling.Api.Sharing;
using Stikling.Api.Sync;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<StiklingDbContext>((services, options) =>
    options.UseSqlServer(services.GetRequiredService<IConfiguration>().GetConnectionString("Stikling")
        ?? throw new InvalidOperationException("ConnectionStrings:Stikling is not set.")));

builder.Services.AddSingleton(services =>
{
    var photos = services.GetRequiredService<IConfiguration>().GetConnectionString("Photos")
        ?? throw new InvalidOperationException("ConnectionStrings:Photos is not set.");

    // Hosted, it's the storage account's address, and the API signs in with its managed identity.
    // Locally it's Azurite's connection string.
    return Uri.TryCreate(photos, UriKind.Absolute, out var address) && address.Scheme == Uri.UriSchemeHttps
        ? new BlobServiceClient(address, new DefaultAzureCredential())
        : new BlobServiceClient(photos);
});
builder.Services.AddSingleton<PhotoStorage>();
builder.Services.Configure<PhotoOptions>(builder.Configuration.GetSection("Photos"));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentPerson>();

builder.Services.AddClerkAuthentication();
builder.Services.AddTestSignIn();
builder.Services.AddCollectionAuthorization();
builder.Services.AddRequestLimits(builder.Configuration);
builder.Services.Configure<ShareOptions>(builder.Configuration.GetSection(ShareOptions.Section));

// Writes the HTML of the share pages. Text is encoded as HTML, but letters like ø and · are left as
// they are instead of numbered references.
builder.Services.AddRazorComponents();
builder.Services.AddWebEncoders(encoders => encoders.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

var appOrigins = new AppOrigins { Origins = AppOrigins.From(builder.Configuration) };
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
    .SetIsOriginAllowed(appOrigins.Allows)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddHealthChecks().AddDbContextCheck<StiklingDbContext>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// The database is created and kept up to date on start, locally and hosted. The hosted API runs
// as one copy at most, and EF Core holds a lock while it migrates in any case. The tests migrate
// on their own.
if (app.Configuration.GetValue("Database:MigrateOnStart", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<StiklingDbContext>().Database.MigrateAsync();
}

// The client's address is in X-Forwarded-For, added by the Container Apps ingress, which is the only
// way in. The public pages are limited by it, so it has to be read first. With one proxy in front, only
// the last address is taken, which is the one the ingress saw and not one a caller made up.
var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor, ForwardLimit = 1 };
forwarded.KnownIPNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

// A deleted account answers 410 Gone, and anything else that goes wrong 500 with no details
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = e => e is AccountDeletedException ? StatusCodes.Status410Gone : StatusCodes.Status500InternalServerError,
});

// The share host serves its pages from the root, and the routes are under /s. This has to run before
// routing picks the endpoint, and routing is started here instead of by default to make sure it does.
app.UseShareHost();
app.UseRouting();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapMe();
app.MapExport();
app.MapSettings();
app.MapCollections();
app.MapRecords();
app.MapPhotos();
app.MapShareLinks();
app.MapSharePages();

app.Run();
