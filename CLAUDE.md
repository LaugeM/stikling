# Notes for working on Stikling

Blazor WebAssembly PWA. Everything is stored on the device in IndexedDB. When someone signs in, the app syncs with the API, which runs on Azure. The app is at https://stikling.app.

## Where code goes

- `src/Stikling.Core`: models, rules, and the services pages call (`PlantService`, `CareService` and so on). No browser or UI code. Anything worth testing belongs here.
- `src/Stikling.Web`: pages, components, and the IndexedDB side. Each Core repository interface (`IPlantRepository` and so on) has its implementation in `Services/IndexedDbRepositories.cs`, which goes through the JavaScript modules in `wwwroot/js`.
- `src/Stikling.Api`: the ASP.NET Core API for accounts and sync, with EF Core on SQL Server.
- `tests/Stikling.Core.Tests`: xUnit, run with `dotnet test Stikling.slnx`. The services are tested against the in-memory repositories in `Fakes.cs`.
- `tests/Stikling.Api.Tests`: the API's endpoints, run against a real SQL Server and Azurite that Testcontainers starts in Docker. Docker Desktop has to be running for `dotnet test Stikling.slnx`.
- `tools/plant-names`: builds `wwwroot/data/plant-names.json`, the names the genus, species and cultivar fields suggest. Its README says how to add names. Never edit the JSON by hand.
- `infra`: the hosted API's Azure resources, in Bicep, deployed by hand (the workflow only deploys new versions of the API). `docs/ops/hosting.md` has the details.

Photos are resized, stored, read, and sent to and fetched from the API entirely in JavaScript. The image data only crosses into C# when a backup is written or restored.

UI work follows `DESIGN.md`. `PRODUCT.md` has who the app is for and the principles behind it.

The impeccable skill is for real design work, not a check on every change, since each run reads a lot of instructions and screenshots. For a new screen or a redesign, run `/impeccable shape` before building and `/impeccable critique` or `polish` on the result. A small change that reuses existing patterns only needs `DESIGN.md`, the plugin's hook that scans each edited file, and the reviewer. Now and then, an `/impeccable audit` across several screens catches what drifts.

## Docs for some tasks

- **New kind of record** (a new IndexedDB store): follow every step in `docs/dev/new-record.md`. A missed step quietly leaves the records out of backups or sync.
- **API or sync code** (`src/Stikling.Api`, `Core/Sync`, `SyncRunner`, how `db.js` saves records, or settings that differ between local and hosted): read `docs/dev/api.md` first. It has the auth rules every endpoint follows, how records and photos are stored, and how a sync runs.
- **Sign-in, or testing while signed in** (Clerk, `account.js`, `AccountService`, `StiklingApi`, or checking a screen or a sync with an account): read `docs/dev/sign-in.md`.
- **Help page or front page** (adding or changing an answer, also for a feature change, `Help.razor`, `FrontPage.razor`): read `docs/dev/help-page.md`. They are also rendered without a browser.
- **Testing with the real collection** (a change to stored data or restore, or a screen that needs a full collection to judge): read `docs/dev/test-collection.md` first.
- **Build passes locally but fails in CI**: run `git status --ignored` first. The .NET gitignore template ignores `Backup*/`, and the exception for `src/Stikling.Core/Backup` is at the end of `.gitignore`.

## Data conventions

People keep their real plants in the app, so their data has to survive every change:

- Data already stored has to keep working after a change: records on the device, older backup files, and records already synced to the server. When a change alters how something is stored, like renaming an enum value, changing what a field means or reshaping a model, it migrates the old data (a new block in `db.js`, a converter, or a server migration) so nobody has to enter anything again.
- Design new data so it won't need deleting later. Add fields and records rather than giving old ones a new meaning, and think a step ahead about how it might change.
- Nothing deletes or overwrites someone's data unless they asked for it in the app.
- A more destructive solution is sometimes the better one. Then offer it next to the safe one, say what data would be lost or changed, and let me decide. The safe one is the default.
- A pull request that changes how data is stored says so, and says how old data is handled.

- Ids are made on the device, and deletes are soft (`DeletedAt`). A restore can then tell the difference between something deleted and something never seen.
- Enums are stored as text, so backups stay readable and reordering the values can't change what old data means.
- A new value in an existing enum stops older versions of the app reading every record that uses it, since they fail on a name they don't know. Give the new meaning a field of its own, as own kinds of care do on `CareLog`, or read an optional enum with `TolerantNullableEnumConverter`.
- Restoring a backup merges instead of replacing. A newer version wins, deletions in the backup carry over, and anything deleted here comes back if the backup still has it.

## Keep it ready for sync

Signed-in devices sync through the API, and the app stays fully usable without an account. New code is written for sync from the start:

- Data belongs to a collection, not to a person, since people can be added to a collection. That covers every record the app keeps, down to what is put off on Today.
- Settings the person expects on every device belong to the person, in `UserSettings`. The theme is one of them. `theme.js` keeps a copy on the device only so the first paint has the right colours.
- Every one of these is an `Entity` in its own store behind a repository. localStorage (`DeviceFiles`) is only for things that really belong to one device, like when it last took a backup.
- All writes go through the repositories, so `UpdatedAt` is always set. Records are soft deleted. The one exception is deleting an account, which really erases the person's data and photos from the server.
- Records point at each other by id, never by name or position in a list.
- When two devices can change the same thing, prefer adding a record (a log entry) over changing a total or a list inside another record. When the newest version of a record wins, one of the two edits is lost.
- Expect what a merge can produce: two pots with the same name, a record whose parent was deleted on another device, records arriving in any order. Checking uniqueness when saving isn't enough.
- Anything the app creates on its own, like defaults or starter data, needs a fixed id. Otherwise every device makes its own copy.
- Code that only works in a browser stays behind a service like `DeviceFiles` or `PhotoService`, never in a page.
- The server strips a record that arrives deleted to its id and dates (`SyncRules.AsStored`), so the app must never need other fields of a deleted record. Places merged into another are the one exception, and are kept whole.
- Lists and detail pages have `<ReloadOnSync Reload="..." />`, which reloads their data when changes arrive from another device. Edit pages don't, so nothing changes under someone filling in a form.

## Text people see

All text people see goes in `Resources/*.resx`, English in `Name.resx` and Danish in `Name.da.resx`, and is used as `Strings.Name.Key`. Never write it inline in a Razor page or a C# file, and add the Danish with the English. Dates and counts have helpers for it. `docs/dev/translating.md` has the rules, the Danish word list and how to add an area file.

## The Help page

`Pages/Help.razor` answers the questions someone new asks, and has to describe the app as it is. When a change adds or changes a feature, update the answers it affects in the same pull request, and add one when the feature raises a question of its own. Check the empty states on the screens involved too, since they tell a new user what to do next and go stale the same way.

When a change is something people using the app would notice, add a line for it at the top of the current month in `Pages/WhatsNew.razor`, in the same pull request. The line is a key in `Resources/WhatsNew.resx` with its Danish in `WhatsNew.da.resx`. The older lines stay English.

## The feature list

`docs/FEATURES.md` is the record of what is built, not only a roadmap. The first column on every row is the status: ✅ is shipped and in the app, ◐ is partly built with the row saying what is missing, and ☐ is not started. Check it before proposing a feature, because the list is long and a lot of it already exists.

## Choosing how to build

Plan on Opus. Once I approve a plan, don't ask me to switch models. Write a brief and hand the build to the `worker` agent (`.claude/agents/worker.md`), which runs on Sonnet 5.5, then check what it did. The `coordinate` skill has the steps, including how to write a brief the worker can finish without coming back with questions. A worker is cheaper than switching this session's model, which writes the whole planning conversation to the cache again.

Build in this session instead when the change is one or two files, or when the work is mostly judgment, like a data migration or tuning a screen together with me. For a batch of small features that touch different parts of the app, suggest `/coordinate`, which runs workers in parallel.

When starting or proposing another session, always set both its model and its effort. Use Sonnet 5.5 at medium for building and fixes, and low for docs and mechanical work. Left out, the effort is copied from this session, so a Sonnet session started from Opus on high also runs on high. A session can't be started above this session's own effort.

## Before opening a pull request

Build, run the tests, and check any UI change in a browser at phone width. The dev server is `stikling-web` in `.claude/launch.json`.

Hand the browser check to the `browser-check` agent (`.claude/agents/browser-check.md`) instead of driving the browser in this session, where a check costs several times more. Tell it what changed, which screens to open and how to get to them. It checks phone, desktop and dark mode, reports in a few lines and leaves one screenshot in the pane. Check in the main session only when the judgment about how it looks is part of the design work.

Clear the test data afterwards. The app has no button for it, so delete the database from the page with `indexedDB.deleteDatabase("stikling")` and reload. The `browser-check` agent does this itself for data it added.

Then run the `reviewer` agent (`.claude/agents/reviewer.md`) on the branch, with a sentence on what the change is for. Fix what it finds that holds up, and say in the chat what it found and what was left alone and why.

In the pull request text, say how the change was checked (which tests ran, what was looked at in the browser) and whether reverting it would undo it. Most changes revert cleanly. Some can't, because devices or the server keep what the new version wrote: raising `DB_VERSION`, migrating stored data, a server migration, or anything that deletes. Say that near the top, along with what it affects.

