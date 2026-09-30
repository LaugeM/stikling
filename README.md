# Stikling

**Track your plants and every cutting they give you.**

*Stikling* is Danish for "cutting". It's a free plant and propagation tracker for houseplant hobbyists. Keep track of your plants, the cuttings and corms you take from them, and how they develop, with a photo timeline for each one.

Stikling is a Progressive Web App: it runs in the browser, can be installed on your phone's home screen, and works offline. Everything is kept on your own device, and the app works fully without an account. Signing in keeps your plants and photos the same on all your devices. Try it at [stikling.app](https://stikling.app).

> Status: v1 features are in place. See the [feature list](docs/FEATURES.md) for everything the app could do, and the [Roadmap](#roadmap) for where it is now.

## Features (planned for v1)

- **Plants**: name, genus/species/cultivar, location, where it came from, status and a cover photo.
- **Propagations**: cuttings, corms, offsets, seeds, divisions and air layers, linked to their parent plant. Track the medium (water, perlite, sphagnum, LECA, PON, soil), stage and count. Promote rooted propagations to plants while keeping the family tree.
- **Photo timeline**: progress photos and notes for every plant and propagation.
- **Today**: propagations that haven't been checked in a while.
- **Backup**: export and import everything, photos included, as a ZIP file.

The longer list, with the ideas for later versions, is in [docs/FEATURES.md](docs/FEATURES.md).

## Tech stack

- C# / .NET 10, **Blazor WebAssembly** (standalone, PWA)
- Bootstrap 5
- IndexedDB for on-device storage (via a small JS interop module)
- xUnit for the domain logic
- The sync API: ASP.NET Core minimal API with EF Core on SQL Server, photos in Azure Blob Storage, sign-in through Clerk, run locally with Docker Compose
- The app on GitHub Pages, the API on Azure Container Apps with Azure SQL, described in Bicep and deployed with GitHub Actions

### Why Blazor WebAssembly?

The app runs entirely in the browser, so it can work offline and keep data on the device without a backend. The app itself is hosted for free as static files, and the sync API is only used by people who sign in. The Razor components can later be reused in a .NET MAUI Blazor Hybrid app if a native Android/iOS version makes sense.

## Project structure

```
src/Stikling.Core/          Models and domain rules (no browser dependencies, unit tested)
src/Stikling.Web/           Blazor WebAssembly PWA
src/Stikling.Api/           The sync API for accounts, records and photos
tests/Stikling.Core.Tests/  xUnit tests for Core
tests/Stikling.Api.Tests/   xUnit tests for the API, against SQL Server and Azurite in Docker
tools/plant-names/          Builds the plant names the app suggests while you type
infra/                     The Azure resources for the hosted API, in Bicep
.github/                   Build/test/deploy workflow and GitHub Pages prep script
```

## Running locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/Stikling.Web
```

Run the tests:

```bash
dotnet test Stikling.slnx
```

The API tests start SQL Server and Azurite (a local stand-in for Azure Blob Storage) in containers, so [Docker Desktop](https://www.docker.com/products/docker-desktop/) has to be running.

### The sync API

The API, its database and Azurite for the photos run in Docker:

```bash
docker compose up --build
```

The API is then on http://localhost:5180, with `/health` to check it's up. It creates the database on the first start. To run the API from Visual Studio or with `dotnet run` instead, start only the database and Azurite with `docker compose up sql azurite`.

### Signing in

When the app runs in Development, it reads `src/Stikling.Web/wwwroot/appsettings.Development.json`, which points at Clerk's development instance and the local API. Settings then has an Account row at the top. The published site reads `appsettings.Production.json` instead, which points at the production instance and the hosted API.

To try it, start the API as above and run the app on one of the origins the API accepts (`AppOrigins` in `src/Stikling.Api/appsettings.Development.json`), for example:

```bash
dotnet run --project src/Stikling.Web --urls http://localhost:5170
```

## Deployment

Every push to `main` runs the tests, deploys the app to GitHub Pages and the API to Azure. In the repo settings under Pages, the source needs to be set to GitHub Actions, and the custom domain to stikling.app.

[`prepare-pages.py`](.github/scripts/prepare-pages.py) sets the `<base href>`, updates the service worker's hash for `index.html`, and adds a `404.html` copy so deep links work.

[docs/hosting.md](docs/hosting.md) has how the API is hosted and how to set it up again.

## Roadmap

1. Scaffold, layout and deployment ✅
2. Plants ✅
3. Photos and timeline ✅
4. Propagations and lineage ✅
5. Backup/restore and the Today screen ✅
6. Later: care and fertiliser log, pest treatment tracking, your own soil mixes, success rates per medium, QR labels, optional accounts and sync

The full [feature list](docs/FEATURES.md) has the details and everything else that is on the table.

## About

A hobby project by [LaugeM](https://github.com/LaugeM).
