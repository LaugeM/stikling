# Contributing to Stikling

Thanks for taking a look. Bug reports, questions, ideas and pull requests all go through GitHub.

## Issues

- **Bugs:** [open a bug report](https://github.com/LaugeM/stikling/issues/new?template=bug_report.yml). Say what device and browser you used, whether you used the installed app or a browser tab, and whether you were signed in, since those change how the app behaves.
- **Ideas:** check [docs/FEATURES.md](docs/FEATURES.md) first. It lists what's built and a lot that's planned, so your idea may already be on it. If it isn't, [open a feature request](https://github.com/LaugeM/stikling/issues/new?template=feature_request.yml).
- **Your account or data:** don't put personal details in an issue. Email support@stikling.app instead.

## Pull requests

Small fixes, like a typo, a broken link or an obvious bug, can go straight to a pull request. For a new feature or a bigger change, open an issue first, so we can agree on how it should work before you spend time on it.

1. Fork the repository and make a branch, e.g. `feature/moisture-reminders` or `fix/photo-rotation`.
2. Make the change, with tests for any new rules in `Stikling.Core`.
3. Run the build and the tests (see below). For a change to the interface, try it at phone width in the browser.
4. Open a pull request against `main` that says what the change does and why, and links the issue.

Commit messages are plain sentences that say what the commit does, e.g. "Show the last watering on the plant card". No prefixes needed.

By contributing, you agree that your contribution is licensed under the [AGPL-3.0](LICENSE), like the rest of the code.

## Running it locally

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/Stikling.Web
```

The app works fully like this, with everything stored in the browser. You only need the sync API if you're working on signing in or sync.

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

The API only accepts sign-ins from the origins in `AppOrigins` in `src/Stikling.Api/appsettings.Development.json`. Locally that is any port on localhost:

```bash
dotnet run --project src/Stikling.Web --urls http://localhost:5170
```

To try the signed-in screens without a Clerk account, use "Sign in as a test person" on the sign-in page. It only shows in Development, and only the local API accepts it. Each name is its own person.

## Where things go

- `src/Stikling.Core`: models, rules, and the services the pages call. No browser or UI code. Anything worth testing belongs here.
- `src/Stikling.Web`: pages, components, and the IndexedDB side. Each repository interface in Core has its implementation in `Services/IndexedDbRepositories.cs`, which goes through the JavaScript modules in `wwwroot/js`.
- `src/Stikling.Api`: the ASP.NET Core API for accounts and sync.
- `tests/`: xUnit tests. The Core services are tested against the in-memory repositories in `tests/Stikling.Core.Tests/Fakes.cs`.
- `tools/plant-names`: builds the plant names the app suggests. Don't edit `plant-names.json` by hand; its README says how to add names.

## Rules for data

The app keeps everything on the device and syncs it between devices when someone signs in, so data has to survive being merged from two places.

- Ids are made on the device, and deletes are soft (`DeletedAt`). Nothing is removed outright.
- Enums are stored as text, so backups stay readable and reordering values can't change old data.
- Records point at each other by id, never by name or by position in a list.
- All writes go through the repositories, so `UpdatedAt` is always set.
- When two devices can change the same thing, prefer adding a record, like a log entry, over changing a total or a list inside another record.
- Anything the app creates on its own, like starter data, needs a fixed id, or every device makes its own copy.
- Code that only works in a browser stays behind a service like `PhotoService`, never in a page.

A new kind of record touches more places than the model and its page, and missing one quietly leaves the records out of backups or sync:

- the model in `Core/Models` and a repository interface next to its service
- a new `if (event.oldVersion < N)` block in `wwwroot/js/db.js` with `DB_VERSION` raised (never change an old block)
- the store name in `Stores` in `Services/IndexedDb.cs`, the implementation in `IndexedDbRepositories.cs`, and the registration in `Program.cs`
- the store name in `SyncKinds` in `Core/Sync` (a test checks this list against `BackupData`)
- `BackupData` and its counts, export and restore in `BackupService`, and the restore summary in `Pages/Settings.razor`
- a fake in `tests/Stikling.Core.Tests/Fakes.cs`, and `BackupTests`

## The interface

- Stikling is used on a phone first, often with one hand, but it runs in any browser on a computer too. Check changes at phone width, and that they still look right on a wider screen.
- The look is described in [DESIGN.md](DESIGN.md), and who the app is for in [PRODUCT.md](PRODUCT.md). The app should stay simple on top: the common path is short, and extra fields and settings stay out of the way until someone asks for them.
- Write for someone who doesn't know the plant words. "Propagation" alone isn't enough without an explanation.
- The Help page (`Pages/Help.razor`) has to describe the app as it is. If you add or change a feature, update the answers it affects.
