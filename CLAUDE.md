# Notes for working on Stikling

Blazor WebAssembly PWA. Everything is stored on the device in IndexedDB, and there is no backend.

## Where code goes

- `src/Stikling.Core`: models and rules, no browser or UI code. Anything worth testing belongs here.
- `src/Stikling.Web`: pages, components, and the services that reach IndexedDB through the JavaScript modules in `wwwroot/js`.
- `tests/Stikling.Core.Tests`: xUnit, run with `dotnet test Stikling.slnx`.

Photos are resized, stored and read entirely in JavaScript. The image data only crosses into C# when a backup is written or restored.

## Data conventions

- Ids are made on the device, and deletes are soft (`DeletedAt`). A restore can then tell the difference between something deleted and something never seen.
- Enums are stored as text, so backups stay readable and reordering the values can't change what old data means.
- Restoring a backup merges instead of replacing. A newer version wins, deletions in the backup carry over, and anything deleted here comes back if the backup still has it.

## The feature list

`docs/FEATURES.md` is the record of what is built, not only a roadmap. The first column on every row is the status: ✅ is shipped and in the app, ◐ is partly built with the row saying what is missing, and ☐ is not started. Check it before proposing a feature, because the list is long and a lot of it already exists.

## One trap

The .NET gitignore template ignores `Backup*/`, which hid `src/Stikling.Core/Backup` until the build failed in CI on missing types. There is an exception for that folder at the end of `.gitignore`. If a build passes locally but fails in CI, check `git status --ignored` first.

## Before opening a pull request

Build, run the tests, and check any UI change in a browser at phone width. Clear the test data afterwards.
