using System.Text.Json.Serialization;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Auth;
using Stikling.Api.Collections;
using Stikling.Api.Data;
using Stikling.Api.People;
using Stikling.Api.Photos;
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
builder.Services.AddCollectionAuthorization();

builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
    .WithOrigins(AppOrigins.From(builder.Configuration))
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

// A deleted account answers 410 Gone, and anything else that goes wrong 500 with no details
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = e => e is AccountDeletedException ? StatusCodes.Status410Gone : StatusCodes.Status500InternalServerError,
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapMe();
app.MapSettings();
app.MapCollections();
app.MapRecords();
app.MapPhotos();

app.Run();
