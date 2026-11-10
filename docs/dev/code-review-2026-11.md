# Code review, November 2026

A review of the whole codebase at `d22efd5`, with a plan for what to change. Nothing here changes how a feature works. Every package is a refactor that keeps behaviour, stored data and the look of the app as they are.

The baseline when the review was done: 3628 Core tests pass in about a second, the Web project builds with 1 warning, and the API tests were not run (they need Docker).

## What is in good shape

These were looked at and need nothing:

- **The rules in Core.** The static rule classes (`CareIntervals`, `WateringGuess`, `PlantLineage`, `SpotHistory`, `TodaySections` and the rest) are small, pure and well tested. This is the best part of the codebase and the pattern to move more code towards.
- **The API.** Small, consistent, and tested against a real SQL Server. The push handler in `RecordEndpoints.cs` is one long lambda, but it reads top to bottom and the tests cover it.
- **Sync and data safety.** `db.js`, `SyncService`, `BackupMerge` and the migrations follow the rules in CLAUDE.md.
- **The edit forms.** `PlantForm`, `PlantForms`, `PropagationForm` and `PotUpForm` already keep form state and loading in Core, which leaves `PlantEdit.razor` with 10 injections. The detail pages should get the same treatment (package 8).
- **The models.** `Copy()` with `MemberwiseClone` is fine as it is.
- **Test data helpers.** Each test class has its own small `APlant(...)`. They differ on purpose, so they stay.
- **CSS.** It follows DESIGN.md. The raw colours in scoped files are the camera, viewer and print surfaces that DESIGN.md describes. One nit: the phone breakpoint is written `63.98rem`, `63.99rem` and `63.999rem` in `app.css` and `Plants.razor.css`. Pick `63.98rem` when a package touches those lines.
- **The `busy = true; try ... finally` blocks** (124 of them). A wrapper would save lines but hide the flow behind a lambda in 60 files. Left alone.

## Findings

Each finding has a letter. The packages further down say which findings they fix.

### A. A new kind of record is registered by hand in about ten places

`docs/dev/new-record.md` lists them. The same list of 33 stores is written out in:

- `wwwroot/js/db.js` (the upgrade blocks)
- `Stores` in `Services/IndexedDb.cs`
- `SyncKinds.Collection` in Core, as string literals
- `BackupData`: the 33 properties, the 33 lines of `Counts`, and the 33 parameters of `BackupCounts`
- `BackupService`: the list in `HasAnythingToSaveAsync`, 33 lines in `ExportAsync`, 33 lines in `ImportAsync`
- `IndexedDbRepositories.cs`, `Program.cs` and `Fakes.cs`

A store has been added in most weeks (`DB_VERSION` is 27), and a missed line leaves records out of backups without any error. One test checks `SyncKinds` against `BackupData`. Nothing checks the other lists.

### B. Thirty repository interfaces and thirty fakes that are the same

Every interface in Core repeats `GetAllAsync`, `GetAsync`, `SaveAsync` and `DeleteAsync`. `Fakes.cs` is 1406 lines, about 900 of them one fake per interface. The fakes have drifted apart: some validate and some don't, `FakeCareLogRepository` keeps a list and the others a dictionary, and none of them sets `CreatedAt` or `UpdatedAt`.

The real rules for saving (validate, set the timestamps, soft delete) are in `IndexedDbEntityRepository` in the Web project, which has no tests. So the code every write goes through is the one part of the save path the tests never run.

### C. Pages read whole stores many times for one screen

Every repository read is `db.GetAllAsync<T>(store)`: the whole store through JavaScript interop and JSON, then filtered in C#.

- `TodayService.LoadAsync` reads the whole timeline at least four times (`GetLatestPerSubjectAsync`, `GetLastActivityBeforeAsync`, `GetComingBackAsync`, `GetPlantChangesAsync`), and once more per day back while away.
- `PlantDetail.LoadAsync` reads all plants twice, the plant changes twice, the feeds twice, the settings three times, and the plant's timeline once in the page, once per leaf subject and twice more in the `Timeline` component.
- The Razor files have 164 `GetAllAsync()` calls in total.

With the current test collection (22 plants, 112 timeline entries) this costs nothing. The timeline and the care logs grow with every action, so a collection of a few hundred plants kept for a few years has well over ten thousand entries in each. The Web project runs interpreted (no AOT), where reading JSON is slow. This is the one finding that could become something people notice, and it has not been measured.

### D. The detail pages are very large and hold logic that can't be tested

- `PlantDetail.razor` is 2731 lines with 37 injections, 12 of them repositories. Its `More` enum has 22 states for the sheets it owns, and one `LoadAsync` reloads everything after every action.
- `PropagationDetail.razor` is 1691 lines. About 60 lines of its code are a copy of PlantDetail's: `RefreshGhostAsync`, `AddedAsync`, `CameraClosed`, `CameraTakenAsync`, `PickFromGoogleAsync`, the leaves (`LeafPhoto`, `ReloadLeavesAsync`, `OpenLeaf`, `LeafSavedAsync`), `SetCoverAsync`, and the markup that goes with them.
- Across the Web project, 57 Razor files inject repositories (159 injections) and 29 call `SaveAsync` or `DeleteAsync` on a repository directly. CLAUDE.md says pages call services.
- Rules that belong to Core sit in pages. Two examples: a status change has to be followed by `CarryStatusToPottedWithAsync`, and `PlantDetail` does that pair by hand in two places. The headline care kinds for a plant are worked out inside `ReloadCareAsync`.

`TodayService` with `TodayData`, and `PlantForms`, already show the fix: a Core class that loads what a screen needs into one record.

### E. The same small helpers are written again in many files

- `private static string F(string format, ...)` is defined in 15 files, and `string.Format(CultureInfo.CurrentCulture, ...)` is written out 566 times. `H`, `Link`, `Html` and `Around` are defined in 2 or 3 pages each.
- `private static string? Clean(string? s)` is defined in 16 files, and the same expression is written inline 57 more times.
- 264 inline `<svg>` icons in Razor files. 22 drawings appear three times or more: the camera 24 times, the close cross 23, the plus 15, the potted plant 12. Some copies differ only in how a number is written (`7.500` and `7.5`), so the real duplication is higher.
- 21 services repeat the same lazy `import` of a JavaScript module and the same `DisposeAsync`.
- 10 pages import `./js/page.js` themselves to scroll or focus. CLAUDE.md says browser code stays behind a service.
- Lookups like `pots.FirstOrDefault(p => p.Id == id)` appear 63 times in Razor files. `Places` and `SellerList` show the better shape.

### F. The card modules in JavaScript share most of their code

`swapcard.js`, `experimentcard.js` and `yearcards.js` each have their own copy of the draw shell (the token check, fonts, canvas size, the `kept` map), `readPhotos`, `closeAll`, `release`, `fileOf`, `canShareFiles`, `share` and `save`. The copies are the same line for line. The C# services beside them (`SwapCardService`, `ExperimentCardService`, `YearCardService`) repeat each other too.

### G. Enum labels are handed from pages into Core

`HistoryText` saves an English line with each history entry, and the English words for enum values are in the Web project's `Labels.resx`. So 79 method signatures in Core take a `Func<Enum, string> label`, and every page call passes `Labels.For`, `Labels.For(propagation)` or `Labels.English`. Three things follow:

- Pages pass `Labels.For` in some places and `Labels.English` in others for the same job. Both work, because Core switches the culture around the call, but nothing says which is right.
- `AddChangeAsync` falls back to `e => e.ToString()` when no label is given, so a forgotten argument would save `BrightIndirect` in a history line.
- There are three helpers for "run this in English": `SavedText.InEnglish`, `HistoryText.InCulture` and `Labels.English`.

### H. The library pages repeat one skeleton, with tidy-up rules in the page

`Feeds`, `Products`, `SoilMixes`, `TreatmentRecipes` and `TreatmentPlans` each have the same `StartNew`, `StartEdit`, `Cancel`, `SaveAsync` and `DeleteAsync`, and the same six state fields. The rules for what is saved (trim the name, turn empty notes into null, drop rows never filled in) are in each page's `SaveAsync`, where no test reaches them. `FeedService`, `ProductService` and `SoilMixService` exist in Core but the pages save through the repository.

### I. Logic in Web services that no test can reach

The Web project has no test project. Some of what is in `Services/` is not browser code:

- `BackupService`: what is exported and how a restore runs. `BackupTests` covers the merge of one list, not a whole restore.
- `Labels.cs`, `WateringText.cs`, `PlantGivenText.cs`: text rules.
- `SyncRunner` (481 lines, 15 constructor arguments): when to sync, the status, fetching single photos, signing out, deleting the account and downloading the account's data.

## The plan

Twelve packages in four phases. Each package is one pull request that builds, passes `dotnet test Stikling.slnx`, and reverts cleanly unless it says otherwise. None raises `DB_VERSION` or changes a stored format unless it says so.

Sizes are rough: S is one worker run, M is a planning step and one or two worker runs, L is several of each.

Rules for every package:

- No change to what the app does or shows. If a refactor turns up a bug, write it down and fix it in its own pull request.
- Saved text, store names, backup property names and resource keys stay byte for byte the same.
- A package that touches Razor markup gets the `browser-check` agent on the screens named in it, at 412 x 907, on desktop and in dark mode.
- Run the `reviewer` agent before the pull request.

### Phase 1: shared helpers

Mechanical, low risk. Packages 1 and 2 touch most Razor files, so they run one at a time and merge before anything else starts. Packages 3 to 5 can run side by side after that.

**Package 1. One format helper (finding E).** Sonnet 5.5, medium. Size M.

- Add `Stikling.Core.Text.Format` with `F(string format, params object?[] args)`, `F(int count, string one, string other, params object?[] args)` and `Clean(string? text)`. `F` uses `CultureInfo.CurrentCulture`, as every copy does today.
- Add `Stikling.Web.Services.Markup` with `H`, `Link`, `Html` and `Around`, taken from `PlantDetail.razor`.
- Import both with `@using static` in `_Imports.razor`. Delete the 15 local `F`, the 16 local `Clean` and the local `H`, `Link`, `Html`, `Around`, `WithLink`.
- Replace `string.Format(CultureInfo.CurrentCulture, ...)` with `F(...)` and the inline `IsNullOrWhiteSpace(x) ? null : x.Trim()` with `Clean(x)`, file by file.
- Check each local `F` before deleting it. Two take `params object[]` instead of `object?[]`, which is fine. Any that used another culture stays.
- Done when no file defines its own `F` or `Clean`, and the tests pass. No browser check needed beyond opening Today, a plant and Settings once in Danish to see the text still formats.

**Package 2. One place for the icons (finding E).** Sonnet 5.5, medium. Size M.

- Follow `ProblemIcons`: a static `Icons` class in `Web/Services` with one `MarkupString` for the inside of each drawing used more than once. The `<svg viewBox="0 0 24 24" aria-hidden="true">` wrapper stays in the page, so scoped CSS that styles `svg` keeps working. Don't make a component that renders the `<svg>` itself, since about 40 lines in scoped stylesheets select `svg` and would stop matching.
- Treat drawings that differ only in number formatting (`7.500` and `7.5`) as one.
- Leave drawings used once where they are.
- Done when the 22 repeated drawings are each written once. Browser check: Today, Plants, a plant page, a propagation page and Settings, since every icon swap is a chance for a typo.

**Package 3. One base for JavaScript modules, and page.js behind a service (finding E).** Sonnet 5.5, medium. Size S.

- Add `JsModule` in `Web/Services`: it takes `IJSRuntime` and a path, loads the module once, and disposes it. Move the 21 services with the repeated `Module` property onto it. `ReleaseAsync`-style calls that only run when the module has loaded need a `IfLoaded` method.
- Add `PageScripts` (scroll to an id, focus an element: whatever `page.js` exports) and use it in the 10 pages that import `page.js` themselves. Those pages then drop `@inject IJSRuntime` where nothing else needs it.
- Components that own a module for their own element (`Sheet`, `VirtualGrid`, `LetterJump` and the like) stay as they are.

**Package 4. One kit for the picture cards (finding F).** Sonnet 5.5, medium. Size M.

- Add `wwwroot/js/cardkit.js` with what the three modules share: the draw shell (takes the canvas, the spec, the list of photo references and a paint function), `readPhotos`, `closeAll`, the kept pictures with `release`, and `share`, `save`, `shareAll`, `saveEach`.
- `swapcard.js`, `experimentcard.js` and `yearcards.js` keep only their layout and painting. Their exported names don't change, so the C# services don't either.
- Before starting, save one picture from each card as it is now (the swap list, an experiment card, each kind of year card). After, draw the same ones and compare. They must be identical.
- Browser check: Swap, Results on Propagations, and Your year, with Share and Save on each.

**Package 5. One generic fake (finding B, first half).** Sonnet 5.5, medium. Size S.

- Add `IRepository<T> where T : Entity` in Core with the four shared methods. Each existing interface inherits it and keeps only what is its own (`IsDeletedAsync`, `GetContainersAsync`, `GetStoredAsync` and so on). No call site changes.
- In `Fakes.cs`, add `FakeRepository<T>` and make each fake a short subclass. Keep each fake's public members (`Plants`, `Saves`, `Logs`) and its current behaviour, including which ones validate and the save order `FakeCareLogRepository` keeps, so no test changes.
- Done when `Fakes.cs` has no repeated `GetAllAsync`/`GetAsync`/`DeleteAsync` bodies and all 3628 tests pass untouched.

### Phase 2: the storage layer

Sequential, since each builds on the one before. Opus 5.5 plans each and reviews the result. These touch backup and sync, so each pull request restores the newest and the oldest backup in `stikling-testdata` (see `docs/dev/test-collection.md`) and checks the counts on the restore summary against the same restore on main.

**Package 6. One list of record kinds (finding A).** Opus 5.5 plans, Sonnet 5.5 builds, medium. Size M.

- Add `RecordKinds` in `Core/Sync`: one entry per store with its name, its type, whether it belongs to the collection or the person, and whether it counts as content for `HasAnythingToSaveAsync` (settings, put-offs and sitter sheets don't today).
- `SyncKinds.Collection` and `SyncKinds.All` are built from it. `Stores` in the Web project keeps only the stores that aren't records (`photoBlobs`, `changes`, `syncState`) and points at `RecordKinds` for the rest.
- `BackupData` keeps its 33 properties, because their names are the backup format. Add one table that pairs each record kind with its list on `BackupData`. `ExportAsync`, `ImportAsync`, `HasAnythingToSaveAsync` and `Counts` loop over the table. Photos keep their extra steps.
- `BackupCounts` stays a record with named numbers, since `Settings.razor` reads them by name.
- Add a test that every `Entity` subclass has a record kind and every record kind has its list in `BackupData`. With `db.js` the test can't see the stores, so add one that reads `db.js` as text and checks every record kind is created there.
- Rewrite `docs/dev/new-record.md` for the shorter list of steps.
- Data: nothing stored changes. The exported `data.json` must have the same properties in the same order as before. Add a test that pins the property names.

**Package 7. The save rules move into Core (findings B and I).** Opus 5.5 plans and builds the core of it, Sonnet 5.5 moves the repositories. Size L.

- Add `IRecordStore` in Core: `GetAllAsync<T>(kind)`, `GetAsync<T>(kind, id)`, `PutAsync<T>(kind, value)`. `IndexedDb` implements it.
- Move `IndexedDbEntityRepository<T>` into Core as `EntityRepository<T>` over `IRecordStore`, and move the per-entity repositories with it, each next to its interface. The timeline and photo repositories move the same way. The Web project keeps `IndexedDb` and the registration in `Program.cs`.
- Add `InMemoryRecordStore` to the test project. It keeps each record as JSON and hands out a fresh copy on every read, like IndexedDB does.
- Add tests for `EntityRepository<T>`: validation refuses, `CreatedAt` is set once, `UpdatedAt` on every save, delete is soft and sets both dates, deleted records are left out of `GetAllAsync`.
- Then move `BackupService`'s export and restore of records into Core as a class over `IRecordStore`, leaving the ZIP and the photos in the Web service. `BackupTests` gains whole-restore tests: restore onto a device with newer records, with deleted records, twice in a row.
- Moving the test fakes onto the real repositories is a separate, optional step at the end. Tests that rely on getting the same object back from a fake would need changing, so do it one fake at a time and stop if it stops paying off.

### Phase 3: reading, and the big pages

**Package 8. Measure, then read each store once per screen (finding C).** Opus 5.5, high for the plan. Size M.

- First add a generator under `tools/` that writes a backup ZIP of a made-up large collection: 300 plants, 60 propagations, three years of care (about 15,000 care logs and 12,000 timeline entries), 2,000 photo records with no image files. Made-up names only.
- Restore it in the browser and time Today, Plants, a plant page and Water plants, on desktop and with the CPU slowed 4x. Write the numbers in the pull request.
- If nothing is slow, stop there and keep the generator. It is useful for layout checks too.
- If it is slow, in this order:
  1. Give `ITimelineRepository` one `GetSnapshotAsync()` that reads the store once and answers the derived questions (`LatestPerSubject`, `LastActivityBefore`, `ComingBack`, `PlantChanges`, `Since`) from memory. `TodayService` uses it. No caching between loads, so nothing can go stale.
  2. The loaders from package 9 pass lists they have already read to the parts that need them.
  3. Only if a plant page is still slow: indexes on `subjectId` for `timeline` and `photos` and on `plantId` for `careLogs`, so a detail page reads only its own entries. This raises `DB_VERSION`, which can't be reverted on devices that have run it. It adds indexes and changes no record. Ask before doing this one.
- Don't add a cache of whole stores in C#. Pages change the objects they load before saving them, so a shared cache would hand one page another page's half-made edits.

**Package 9. A loader in Core for each detail page (finding D).** Opus 5.5 plans, Sonnet 5.5 builds. Size L.

- Add `PlantProfileService` in Core with `LoadAsync(Guid id)` returning a `PlantProfile` record: the plant, its family, its care summary and headline, what comes back, flowering, leaves, problems, pests, pots, mix, feed and the watering lines. It replaces `LoadAsync` and the `Reload...Async` methods in `PlantDetail.razor`, and reads each store once.
- Text stays in the page or in `Labels`. The profile carries values, not sentences, except where Core already makes the text (`FloweringText`, `ProblemText`).
- Move the rules found on the way into Core with tests: the headline care kinds, `ComesBack`, and a `PlantService.ChangeStatusAsync` that does the update and `CarryStatusToPottedWithAsync` together.
- Do the same for `PropagationDetail` with `PropagationProfileService`.
- Tests for both loaders over the in-memory store from package 7.
- Browser check: a plant with everything on it (flowering, a problem, a pest case, potted with another, moving to semi-hydro) and a propagation at each stage, against main side by side.

**Package 10. Split the detail pages into components (finding D).** Opus 5.5 decides the cut, Sonnet 5.5 builds one component per worker. Size L.

- From `PlantDetail.razor`, each with its own sheet state so the `More` enum shrinks: the flowering card and its sheets, the semi-hydro card and its sheets, the problem cards, the potted-together sheets (pot together, some of them, take out), the status and pass-on sheets, the pest check sheet.
- Shared by both detail pages: a `PhotoBar` component (the `SubjectPhotos` name is taken by a service) for the photo input, camera, same-angle ghost, Google Photos pick and the composer, and a `LeavesCard`.
- Markup and class names move unchanged. No visual change is the test: screenshots before and after must match.
- One component per pull request, or two or three small ones together. Not all at once.
- Target: `PlantDetail.razor` under 1000 lines, `PropagationDetail.razor` under 800.

### Phase 4: the rest

Independent of each other. Do them when there is room.

**Package 11. Enum labels move into Core (finding G).** Opus 5.5, high. Size L.

- First add a test that pins the English label of every value of every enum `HistoryText` can hold, as they are now. It must pass unchanged at the end. `HistoryText.Parse` reads old entries by their English text, so one changed word breaks old history.
- Move those labels (English and Danish) to `Core/Resources/Labels.resx` and add `EnumLabels.For(Enum)` and the stage names by propagation type to Core. `Labels.For(...)` in the Web project calls through for them.
- `HistoryText.Set` and the services stop taking `label`. Remove the parameter from the 79 signatures and every call.
- Keep one helper for "in English" (`SavedText.InEnglish`) and remove the other two.
- Data: the text saved from now on must be identical to before for the same input. Nothing already stored changes.

**Package 12. The library pages, and SyncRunner (findings H and I).** Sonnet 5.5, medium. Size M.

- Move the tidy-up before save into Core: a `Tidy()` on `Feed`, `Product`, `SoilMix`, `TreatmentRecipe` and `TreatmentPlan`, or a `SaveAsync` on their services that tidies, validates and returns the errors. Add tests. The five pages call it.
- Leave the pages' `StartNew`/`StartEdit`/`Cancel` skeleton alone unless a shared `Draft<T>` class clearly reads better after the first step. It may not.
- From `SyncRunner`, move fetching single photos (`FetchPhotoAsync`, the `fetching` and `notOnServer` state) into a `PhotoFetcher`, and signing out, deleting the account and downloading the account's data into an `AccountSession`. `SyncRunner` keeps deciding when to sync and reporting status. Read `docs/dev/api.md` and `docs/dev/sign-in.md` first, and test signed in as they describe.

## Order and what can run together

1. Package 1, alone. Merge.
2. Package 2, alone. Merge.
3. Packages 3, 4 and 5 together with `/coordinate`.
4. Packages 6, then 7.
5. Package 8 (the measuring half can run any time after step 2).
6. Package 9, then 10.
7. Packages 11 and 12 whenever.

Packages 1 to 5 are worth doing whatever happens to the rest. Packages 6 and 7 pay back every time a store is added. Package 8 decides how urgent 9 is: if the large collection loads fine, 9 and 10 are about keeping the pages workable, not about speed.

## After each phase

- Update `CLAUDE.md` where it describes the code: where repositories live after package 7, the loader pattern after package 9, the icon and format helpers after phase 1.
- Update `docs/dev/new-record.md` after packages 6 and 7.
