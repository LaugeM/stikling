// Renders the app's Help, Privacy and Deletion pages and the front page to plain HTML, so the live site can serve
// them as pages that search engines, AI tools and sign-in providers can read without running the app. It does so
// once for each language that is ready, into <output folder>/<code>/: help.html, privacy.html, deletion.html and
// front.html, plus text.json with the titles, descriptions and the text around the app in index.html. It also
// writes <output folder>/languages.json, the ready languages in order. The deploy runs it before
// prepare-pages.py, which puts the HTML into the pages of the site, the sitemap and llms.txt.
// Run from the repository root: dotnet run tools/static-pages/render.cs -- <output folder>

#:project ../../src/Stikling.Web/Stikling.Web.csproj
#:property PublishAot=false

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Stikling.Core.Settings;
using Stikling.Web.Pages;
using Stikling.Web.StaticPages;
using Stikling.Web.Services;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run tools/static-pages/render.cs -- <output folder>");
    return 1;
}

// The live site's settings, so the answers about signing in read as they do there
var config = new ConfigurationBuilder()
    .AddJsonFile(Path.GetFullPath("src/Stikling.Web/wwwroot/appsettings.Production.json"), optional: false)
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IJSRuntime, NoJSRuntime>();
services.AddSingleton<NavigationManager, StaticNavigationManager>();
services.AddSingleton<DeviceFiles>();
services.AddSingleton(AccountSettings.From(config, isDevelopment: false));
services.AddSingleton<AccountService>();
await using var provider = services.BuildServiceProvider();

await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
Directory.CreateDirectory(args[0]);

// Each ready language, whatever language the machine running this is set to. Dates and numbers follow the
// language's culture, as in the app (see SetCulture in Program.cs).
var ready = AppLanguages.Offered(includeInProgress: false);
foreach (var language in ready)
{
    var culture = new CultureInfo(AppLanguages.CultureOf(language.Code));
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;

    var folder = Path.Combine(args[0], language.Code);
    Directory.CreateDirectory(folder);
    await Render<Help>(folder, "help.html");
    await Render<Privacy>(folder, "privacy.html");
    await Render<Deletion>(folder, "deletion.html");
    await Render<FrontPage>(folder, "front.html");
    File.WriteAllText(Path.Combine(folder, "text.json"), JsonSerializer.Serialize(StaticPageText.Current(), new JsonSerializerOptions { WriteIndented = true }));
}

File.WriteAllText(Path.Combine(args[0], "languages.json"),
    JsonSerializer.Serialize(ready.Select(l => new { code = l.Code, name = l.NativeName }), new JsonSerializerOptions { WriteIndented = true }));
return 0;

async Task Render<TPage>(string folder, string name) where TPage : IComponent
{
    var html = await renderer.Dispatcher.InvokeAsync(async () =>
        (await renderer.RenderComponentAsync<TPage>()).ToHtmlString());
    var path = Path.Combine(folder, name);
    File.WriteAllText(path, html);
    Console.WriteLine($"Rendered {typeof(TPage).Name} to {path}");
}

// Nothing on the pages needs a browser to render. Calls into JavaScript happen after rendering, which
// HtmlRenderer never gets to, so one arriving here is a mistake.
sealed class NoJSRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        throw new InvalidOperationException($"The static page called into JavaScript ({identifier})");

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);
}

sealed class StaticNavigationManager : NavigationManager
{
    public StaticNavigationManager() => Initialize("https://stikling.app/", "https://stikling.app/");
}
