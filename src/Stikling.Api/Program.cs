using System.Text.Json.Serialization;
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

builder.Services.AddSingleton(services => new BlobServiceClient(
    services.GetRequiredService<IConfiguration>().GetConnectionString("Photos")
        ?? throw new InvalidOperationException("ConnectionStrings:Photos is not set.")));
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
{
    app.MapOpenApi();

    // Locally the database is created and kept up to date on start. The hosted database gets
    // its migrations from the deploy instead.
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<StiklingDbContext>().Database.MigrateAsync();
}

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
