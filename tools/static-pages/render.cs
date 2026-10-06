// Renders the app's Help and Privacy pages to plain HTML, so the live site can serve /help and /privacy as
// pages that search engines, AI tools and sign-in providers can read without running the app. The deploy runs
// it before prepare-pages.py, which puts the HTML into help.html, privacy.html and llms.txt.
// Run from the repository root: dotnet run tools/static-pages/render.cs -- <output folder>

#:project ../../src/Stikling.Web/Stikling.Web.csproj
#:property PublishAot=false

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Stikling.Web.Pages;
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
await Render<Help>("help.html");
await Render<Privacy>("privacy.html");
return 0;

async Task Render<TPage>(string name) where TPage : IComponent
{
    var html = await renderer.Dispatcher.InvokeAsync(async () =>
        (await renderer.RenderComponentAsync<TPage>()).ToHtmlString());
    var path = Path.Combine(args[0], name);
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
