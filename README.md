# Stikling

**Track your plants and every cutting they give you.**

*Stikling* is Danish for "cutting". It's a free plant and propagation tracker for houseplant hobbyists. Keep track of your plants, the cuttings and corms you take from them, and how they develop, with a photo timeline for each one.

Stikling is a Progressive Web App: it runs in the browser, can be installed on your phone's home screen, and works offline. All data stays on your own device. There are no accounts and nothing is sent to a server.

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
- Hosted for free on GitHub Pages and deployed with GitHub Actions

### Why Blazor WebAssembly?

The app runs entirely in the browser, so it can work offline and keep data on the device without a backend. That means free hosting as static files and no user accounts or personal data to secure. The Razor components can later be reused in a .NET MAUI Blazor Hybrid app if a native Android/iOS version makes sense.

## Project structure

```
src/Stikling.Core/          Models and domain rules (no browser dependencies, unit tested)
src/Stikling.Web/           Blazor WebAssembly PWA
tests/Stikling.Core.Tests/  xUnit tests
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

## Deployment

Every push to `main` runs the tests, publishes the app and deploys it to GitHub Pages. In the repo settings under Pages, the source needs to be set to GitHub Actions.

Because Pages serves the site from `/<repo-name>/`, [`prepare-pages.py`](.github/scripts/prepare-pages.py) rewrites the `<base href>`, updates the service worker's hash for `index.html`, and adds a `404.html` copy so deep links work.

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
