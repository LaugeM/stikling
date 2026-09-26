# Notes for working on Stikling

Blazor WebAssembly PWA. Everything is stored on the device in IndexedDB, and there is no backend.

## Where code goes

- `src/Stikling.Core`: models, rules, and the services pages call (`PlantService`, `CareService` and so on). No browser or UI code. Anything worth testing belongs here.
- `src/Stikling.Web`: pages, components, and the IndexedDB side. Each Core repository interface (`IPlantRepository` and so on) has its implementation in `Services/IndexedDbRepositories.cs`, which goes through the JavaScript modules in `wwwroot/js`.
- `tests/Stikling.Core.Tests`: xUnit, run with `dotnet test Stikling.slnx`. The services are tested against the in-memory repositories in `Fakes.cs`.

Photos are resized, stored and read entirely in JavaScript. The image data only crosses into C# when a backup is written or restored.

UI work follows `DESIGN.md`. `PRODUCT.md` has who the app is for and the principles behind it.

## Adding a new kind of record

A new IndexedDB store touches more places than the model and its page, and missing one of them quietly leaves the records out of backups. Use the feeds commit (`3e255f3`) as the example:

- the model in `Core/Models` and a repository interface next to its service
- a new `if (event.oldVersion < N)` block in `wwwroot/js/db.js` with `DB_VERSION` raised. Never change an old block.
- the store name in `Stores` in `Services/IndexedDb.cs`, the implementation in `IndexedDbRepositories.cs`, and the registration in `Program.cs`
- `BackupData` and its `Counts`, export and restore in `BackupService`, and the restore summary in `Pages/Settings.razor`
- a fake in `tests/Stikling.Core.Tests/Fakes.cs`, and `BackupTests`

## Data conventions

- Ids are made on the device, and deletes are soft (`DeletedAt`). A restore can then tell the difference between something deleted and something never seen.
- Enums are stored as text, so backups stay readable and reordering the values can't change what old data means.
- Restoring a backup merges instead of replacing. A newer version wins, deletions in the backup carry over, and anything deleted here comes back if the backup still has it.

## The feature list

`docs/FEATURES.md` is the record of what is built, not only a roadmap. The first column on every row is the status: ✅ is shipped and in the app, ◐ is partly built with the row saying what is missing, and ☐ is not started. Check it before proposing a feature, because the list is long and a lot of it already exists.

## One trap

The .NET gitignore template ignores `Backup*/`, which hid `src/Stikling.Core/Backup` until the build failed in CI on missing types. There is an exception for that folder at the end of `.gitignore`. If a build passes locally but fails in CI, check `git status --ignored` first.

## Before opening a pull request

Build, run the tests, and check any UI change in a browser at phone width. The dev server is `stikling-web` in `.claude/launch.json` (port 5170).

Clear the test data afterwards. The app has no button for it, so delete the database from the page with `indexedDB.deleteDatabase("stikling")` and reload.
