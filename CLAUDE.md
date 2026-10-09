# Notes for working on Stikling

Blazor WebAssembly PWA. Everything is stored on the device in IndexedDB. When someone signs in, the app syncs with the API, which runs on Azure. The app is at https://stikling.app.

## Where code goes

- `src/Stikling.Core`: models, rules, and the services pages call (`PlantService`, `CareService` and so on). No browser or UI code. Anything worth testing belongs here.
- `src/Stikling.Web`: pages, components, and the IndexedDB side. Each Core repository interface (`IPlantRepository` and so on) has its implementation in `Services/IndexedDbRepositories.cs`, which goes through the JavaScript modules in `wwwroot/js`.
- `src/Stikling.Api`: the ASP.NET Core API for accounts and sync, with EF Core on SQL Server.
- `tests/Stikling.Core.Tests`: xUnit, run with `dotnet test Stikling.slnx`. The services are tested against the in-memory repositories in `Fakes.cs`.
- `tests/Stikling.Api.Tests`: the API's endpoints, run against a real SQL Server and Azurite that Testcontainers starts in Docker. Docker Desktop has to be running for `dotnet test Stikling.slnx`.
- `tools/plant-names`: the script that builds `wwwroot/data/plant-names.json`, the names the genus, species and cultivar fields suggest. Its README says where the names come from and how to add more. Never edit the JSON by hand.
- `infra`: the Azure resources for the hosted API, in Bicep. `docs/ops/hosting.md` says what they are and how to deploy them. The workflow only deploys new versions of the API, and changes to the Bicep are deployed by hand.

Photos are resized, stored, read, and sent to and fetched from the API entirely in JavaScript. The image data only crosses into C# when a backup is written or restored.

UI work follows `DESIGN.md`. `PRODUCT.md` has who the app is for and the principles behind it.

The impeccable skill is for real design work, not a check on every change, since each run reads a lot of instructions and screenshots. For a new screen or a redesign, run `/impeccable shape` before building and `/impeccable critique` or `polish` on the result. A small change that reuses existing patterns only needs `DESIGN.md`, the plugin's hook that scans each edited file, and the reviewer. Now and then, an `/impeccable audit` across several screens catches what drifts.

## Docs for some tasks

- **New kind of record** (a new IndexedDB store): follow every step in `docs/dev/new-record.md`. A missed step quietly leaves the records out of backups or sync.
- **API or sync code** (`src/Stikling.Api`, `Core/Sync`, `SyncRunner`, how `db.js` saves records, or settings that differ between local and hosted): read `docs/dev/api.md` first. It has the auth rules every endpoint follows, how records and photos are stored, and how a sync runs.
- **Sign-in, or testing while signed in** (Clerk, `account.js`, `AccountService`, `StiklingApi`, or checking a screen or a sync with an account): read `docs/dev/sign-in.md`.

## Data conventions

People keep their real plants in the app, so their data has to survive every change:

- Data already stored has to keep working after a change: records on the device, older backup files, and records already synced to the server. When a change alters how something is stored, like renaming an enum value, changing what a field means or reshaping a model, it migrates the old data (a new block in `db.js`, a converter, or a server migration) so nobody has to enter anything again.
- Design new data so it won't need deleting later. Add fields and records rather than giving old ones a new meaning, and think a step ahead about how it might change.
- Nothing deletes or overwrites someone's data unless they asked for it in the app.
- A more destructive solution is sometimes the better one. Then offer it next to the safe one, say what data would be lost or changed, and let me decide. The safe one is the default.
- A pull request that changes how data is stored says so, and says how old data is handled.

- Ids are made on the device, and deletes are soft (`DeletedAt`). A restore can then tell the difference between something deleted and something never seen.
- Enums are stored as text, so backups stay readable and reordering the values can't change what old data means.
- Restoring a backup merges instead of replacing. A newer version wins, deletions in the backup carry over, and anything deleted here comes back if the backup still has it.

## Keep it ready for sync

Accounts and sync between devices are being added in steps, so people don't have to move backups around by hand. Sign-in goes through Clerk, and an ASP.NET Core API does the sync. The app stays fully usable without an account. New code should be written so it won't have to be redone:

- Data belongs to a collection, not to a person, since people can be added to a collection. That covers plants, pots, propagations, rooms and spots, care logs, feeds, pest cases, photos, products, soil mixes, treatment recipes and what is put off on Today.
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

## The Help page

`Pages/Help.razor` answers the questions someone new asks, and has a few answers for each part of the app. It has to describe the app as it is. When a change adds or changes a feature, update the answers it affects in the same pull request, and add one when the feature raises a question of its own. Check the empty states on the screens involved too, since they tell a new user what to do next and go stale the same way.

The deploy also renders the page to plain HTML for search engines, and into `llms.txt` for AI tools (`tools/static-pages/render.cs`), so it has to render without a browser: anything that calls JavaScript goes in `OnAfterRenderAsync`. The front page in `wwwroot/index.html` answers a few of the same questions, so check those too.

When a change is something people using the app would notice, add a line for it at the top of the current month in `Pages/WhatsNew.razor`, in the same pull request.

## The feature list

`docs/FEATURES.md` is the record of what is built, not only a roadmap. The first column on every row is the status: ✅ is shipped and in the app, ◐ is partly built with the row saying what is missing, and ☐ is not started. Check it before proposing a feature, because the list is long and a lot of it already exists.

## One trap

The .NET gitignore template ignores `Backup*/`, which hid `src/Stikling.Core/Backup` until the build failed in CI on missing types. There is an exception for that folder at the end of `.gitignore`. If a build passes locally but fails in CI, check `git status --ignored` first.

## Choosing how to build

For a batch of small features that touch different parts of the app, suggest `/coordinate`, which runs Sonnet workers in parallel. For one feature that needs design decisions, plan it on Opus and build it on Sonnet 5.5 once the design is settled: switch the session's model, or hand off to a new Sonnet session when the planning conversation is long. A part that needs judgment, like a data migration, stays on Opus. The skill has the details on when workers pay off.

## Before opening a pull request

Build, run the tests, and check any UI change in a browser at phone width. The dev server is `stikling-web` in `.claude/launch.json`. It runs on port 5170, or on another free port when a session running at the same time already has 5170.

Hand the browser check to the `browser-check` agent (`.claude/agents/browser-check.md`) instead of driving the browser in this session. Every turn in a long session re-reads its whole context, so a check done here costs several times more than one done by an agent that starts small. Tell it what changed, which screens to open and how to get to them. It checks phone, desktop and dark mode, reports in a few lines and leaves one screenshot in the pane. Check in the main session only when the judgment about how it looks is part of the design work.

Clear the test data afterwards. The app has no button for it, so delete the database from the page with `indexedDB.deleteDatabase("stikling")` and reload. The `browser-check` agent does this itself for data it added.

Then run the `reviewer` agent (`.claude/agents/reviewer.md`) on the branch, with a sentence on what the change is for. It reads the diff with a fresh context and checks it against this file and `DESIGN.md`. Fix what it finds that holds up, and say in the chat what it found and what was left alone and why.

In the pull request text, say how the change was checked (which tests ran, what was looked at in the browser) and whether reverting it would undo it. Most changes revert cleanly. Some can't, because devices or the server keep what the new version wrote: raising `DB_VERSION`, migrating stored data, a server migration, or anything that deletes. Say that near the top, along with what it affects.
