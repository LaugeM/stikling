# Notes for working on Stikling

Blazor WebAssembly PWA. Everything is stored on the device in IndexedDB, and there is no backend.

## Where code goes

- `src/Stikling.Core`: models, rules, and the services pages call (`PlantService`, `CareService` and so on). No browser or UI code. Anything worth testing belongs here.
- `src/Stikling.Web`: pages, components, and the IndexedDB side. Each Core repository interface (`IPlantRepository` and so on) has its implementation in `Services/IndexedDbRepositories.cs`, which goes through the JavaScript modules in `wwwroot/js`.
- `tests/Stikling.Core.Tests`: xUnit, run with `dotnet test Stikling.slnx`. The services are tested against the in-memory repositories in `Fakes.cs`.
- `tools/plant-names`: the script that builds `wwwroot/data/plant-names.json`, the names the genus, species and cultivar fields suggest. Its README says where the names come from and how to add more. Never edit the JSON by hand.

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

## Keep it ready for sync

The app will move to hosting at some point, with accounts and sync between devices, so people don't have to move backups around by hand. Nothing for that is built yet, but new code should be written so it won't have to be redone:

- Anything that belongs to the person is an `Entity` in its own store behind a repository. That includes settings they would expect on every device. localStorage (`DeviceFiles`) is only for things that really belong to one device, like the theme.
- All writes go through the repositories, so `UpdatedAt` is always set. Nothing is ever hard deleted.
- Records point at each other by id, never by name or position in a list.
- When two devices can change the same thing, prefer adding a record (a log entry) over changing a total or a list inside another record. When the newest version of a record wins, one of the two edits is lost.
- Expect what a merge can produce: two pots with the same name, a record whose parent was deleted on another device, records arriving in any order. Checking uniqueness when saving isn't enough.
- Anything the app creates on its own, like defaults or starter data, needs a fixed id. Otherwise every device makes its own copy.
- Code that only works in a browser stays behind a service like `DeviceFiles` or `PhotoService`, never in a page.

## The Help page

`Pages/Help.razor` answers the questions someone new asks, and has a few answers for each part of the app. It has to describe the app as it is. When a change adds or changes a feature, update the answers it affects in the same pull request, and add one when the feature raises a question of its own. Check the empty states on the screens involved too, since they tell a new user what to do next and go stale the same way.

## The feature list

`docs/FEATURES.md` is the record of what is built, not only a roadmap. The first column on every row is the status: ✅ is shipped and in the app, ◐ is partly built with the row saying what is missing, and ☐ is not started. Check it before proposing a feature, because the list is long and a lot of it already exists.

## One trap

The .NET gitignore template ignores `Backup*/`, which hid `src/Stikling.Core/Backup` until the build failed in CI on missing types. There is an exception for that folder at the end of `.gitignore`. If a build passes locally but fails in CI, check `git status --ignored` first.

## Before opening a pull request

Build, run the tests, and check any UI change in a browser at phone width. The dev server is `stikling-web` in `.claude/launch.json`. It runs on port 5170, or on another free port when a session running at the same time already has 5170.

Clear the test data afterwards. The app has no button for it, so delete the database from the page with `indexedDB.deleteDatabase("stikling")` and reload.

Then run the `reviewer` agent (`.claude/agents/reviewer.md`) on the branch, with a sentence on what the change is for. It reads the diff with a fresh context and checks it against this file and `DESIGN.md`. Fix what it finds that holds up, and say in the chat what it found and what was left alone and why.
