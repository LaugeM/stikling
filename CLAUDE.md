# Notes for working on Stikling

Blazor WebAssembly PWA. Everything is stored on the device in IndexedDB. When someone signs in, the app syncs with the API, which runs on Azure. The app is at https://stikling.app.

## Where code goes

- `src/Stikling.Core`: models, rules, and the services pages call (`PlantService`, `CareService` and so on). No browser or UI code. Anything worth testing belongs here.
- `src/Stikling.Web`: pages, components, and the IndexedDB side. Each Core repository interface (`IPlantRepository` and so on) has its implementation in `Services/IndexedDbRepositories.cs`, which goes through the JavaScript modules in `wwwroot/js`.
- `src/Stikling.Api`: the ASP.NET Core API for accounts and sync, with EF Core on SQL Server. See "The API" below.
- `tests/Stikling.Core.Tests`: xUnit, run with `dotnet test Stikling.slnx`. The services are tested against the in-memory repositories in `Fakes.cs`.
- `tests/Stikling.Api.Tests`: the API's endpoints, run against a real SQL Server and Azurite that Testcontainers starts in Docker. Docker Desktop has to be running for `dotnet test Stikling.slnx`.
- `tools/plant-names`: the script that builds `wwwroot/data/plant-names.json`, the names the genus, species and cultivar fields suggest. Its README says where the names come from and how to add more. Never edit the JSON by hand.
- `infra`: the Azure resources for the hosted API, in Bicep. `docs/ops/hosting.md` says what they are and how to deploy them. The workflow only deploys new versions of the API, and changes to the Bicep are deployed by hand.

Photos are resized, stored, read, and sent to and fetched from the API entirely in JavaScript. The image data only crosses into C# when a backup is written or restored.

UI work follows `DESIGN.md`. `PRODUCT.md` has who the app is for and the principles behind it.

The impeccable skill is for real design work, not a check on every change, since each run reads a lot of instructions and screenshots. For a new screen or a redesign, run `/impeccable shape` before building and `/impeccable critique` or `polish` on the result. A small change that reuses existing patterns only needs `DESIGN.md`, the plugin's hook that scans each edited file, and the reviewer. Now and then, an `/impeccable audit` across several screens catches what drifts.

## Adding a new kind of record

A new IndexedDB store touches more places than the model and its page, and missing one of them quietly leaves the records out of backups. Use the feeds commit (`3e255f3`) as the example:

- the model in `Core/Models` and a repository interface next to its service
- a new `if (event.oldVersion < N)` block in `wwwroot/js/db.js` with `DB_VERSION` raised. Never change an old block.
- the store name in `Stores` in `Services/IndexedDb.cs`, the implementation in `IndexedDbRepositories.cs`, and the registration in `Program.cs`
- the store name in `SyncKinds` in `Core/Sync`, or it won't sync. The API only takes the kinds listed there, and a test checks the list against `BackupData`.
- `BackupData` and its `Counts`, export and restore in `BackupService`, and the restore summary in `Pages/Settings.razor`
- a fake in `tests/Stikling.Core.Tests/Fakes.cs`, and `BackupTests`

## Data conventions

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

## The API

- Clerk's session tokens are checked by ASP.NET Core's JWT bearer authentication in `Auth/ClerkAuthentication.cs`. Clerk stays behind that file and the `ClerkUserId` column on `Person`. Everything else points at our own `Person.Id`, so the sign-in service can be replaced.
- Never take the caller's identity from the request body or the URL. `CurrentPerson` finds them from the token.
- Anything under `/collections/{collectionId}` needs `CollectionPolicies.View` or `CollectionPolicies.Edit`. The policy checks the caller's membership on every request, so a viewer can't change data even with a modified app.
- `docker compose up --build` runs the API on port 5180 with SQL Server, and Azurite in place of Blob Storage. It applies its migrations on start, locally and hosted. A new migration is made with `dotnet tool restore`, then `dotnet ef migrations add <Name> --project src/Stikling.Api --output-dir Data/Migrations`.
- Records from the app are kept as their JSON in one `Records` table, keyed by collection, kind and id. The server only reads the id, `updatedAt` and `deletedAt`, so a new field in a model needs no migration. The newest `updatedAt` wins, and each accepted change gets the collection's next change number, which is what devices fetch by. The person's `UserSettings` are kept the same way in `PersonSettings`. A record the server can't keep, like one dated more than a day ahead of its clock, is refused on its own, and the rest of the upload is kept.
- Photo images don't travel with the records. After the records, `PhotoSyncService` in `Core/Sync` sends the images from the `photoUploads` list in `db.js` and fetches thumbnails for the `photoDownloads` list. `db.js` keeps both lists in the same transaction as the images. Full sizes are only fetched when a photo is opened. The API keeps each image as a blob, with a `PhotoImages` row so a collection's photo space (1 GB, `Photos:MaxBytesPerCollection`) can be added up, and deletes the images when a photo's record arrives deleted.
- The sync logic on the device side is `SyncService` in `Core/Sync`, tested against `FakeSyncStore` and `FakeSyncServer`. `SyncRecord`, `PushRequest` and the other request formats there are shared with the API.
- In the app, `SyncRunner` decides when to sync, and `IndexedDbSyncStore` is the device side of it. Every write in `db.js` also puts the record on the change list, the `changes` store, in the same transaction. That covers every store except the ones in `NOT_RECORDS`, so a new store is on the list without anything else to add. A store that doesn't hold records, like `photoBlobs`, goes in `NOT_RECORDS`.
- `db.js` keeps the fields of a stored record that the app didn't send when saving it, so a device on an older version can't erase a field that a newer version added. Only fields at the top of the record are kept this way.
- Lists and detail pages have `<ReloadOnSync Reload="..." />`, which reloads their data when changes arrive from another device. Edit pages don't, so nothing changes under someone filling in a form.
- Settings that differ between local and hosted (the Clerk instance, the app's origins, the connection string) are in `appsettings.Development.json` locally, and in the container's environment in `infra/resources.bicep` when hosted. Secrets never go in the repo, and the hosted API has none: it reaches the database and storage with its managed identity.

## Signing in from the app

- Clerk has no Blazor library, so it is behind `wwwroot/js/account.js`, and `AccountService` is the only class that calls that file. Calls to the API go through `StiklingApi`, which adds the session token to each request.
- Clerk's scripts are only loaded when they are needed: on the sign-in page, or for syncing on a device where someone is signed in. Syncing starts once the app is on screen, never before, so it still opens offline, and a device where nobody signed in never loads Clerk.
- Signing in is only offered when `wwwroot/appsettings.{Environment}.json` has the Clerk instance and the API address. Locally that is `appsettings.Development.json`, which is kept out of the published site. The live site uses `appsettings.Production.json`, with Clerk's production instance and the hosted API.
- The API only accepts tokens from the origins in its `AppOrigins`. The `stikling-web` dev server runs in Development on port 5170 for that reason. If it falls back to another port, the API turns the sign-in away.

## The Help page

`Pages/Help.razor` answers the questions someone new asks, and has a few answers for each part of the app. It has to describe the app as it is. When a change adds or changes a feature, update the answers it affects in the same pull request, and add one when the feature raises a question of its own. Check the empty states on the screens involved too, since they tell a new user what to do next and go stale the same way.

## The feature list

`docs/FEATURES.md` is the record of what is built, not only a roadmap. The first column on every row is the status: ✅ is shipped and in the app, ◐ is partly built with the row saying what is missing, and ☐ is not started. Check it before proposing a feature, because the list is long and a lot of it already exists.

## One trap

The .NET gitignore template ignores `Backup*/`, which hid `src/Stikling.Core/Backup` until the build failed in CI on missing types. There is an exception for that folder at the end of `.gitignore`. If a build passes locally but fails in CI, check `git status --ignored` first.

## Choosing how to build

For a batch of small features that touch different parts of the app, suggest `/coordinate`, which runs Sonnet workers in parallel. For one feature that needs design decisions, plan and build it in the session. The skill has the details on when workers pay off.

## Before opening a pull request

Build, run the tests, and check any UI change in a browser at phone width. The dev server is `stikling-web` in `.claude/launch.json`. It runs on port 5170, or on another free port when a session running at the same time already has 5170.

Clear the test data afterwards. The app has no button for it, so delete the database from the page with `indexedDB.deleteDatabase("stikling")` and reload.

Then run the `reviewer` agent (`.claude/agents/reviewer.md`) on the branch, with a sentence on what the change is for. It reads the diff with a fresh context and checks it against this file and `DESIGN.md`. Fix what it finds that holds up, and say in the chat what it found and what was left alone and why.
