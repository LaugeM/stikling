# Adding a new kind of record

A new IndexedDB store touches more places than the model and its page, and missing one of them quietly leaves the records out of backups. Use the feeds commit (`3e255f3`) as the example:

- the model in `Core/Models` and a repository interface next to its service
- a new `if (event.oldVersion < N)` block in `wwwroot/js/db.js` with `DB_VERSION` raised. Never change an old block.
- the store name in `Stores` in `Services/IndexedDb.cs`, the implementation in `IndexedDbRepositories.cs`, and the registration in `Program.cs`
- nothing for the sync change list, which takes every store on its own. A store that doesn't hold records, like `photoBlobs`, goes in `NOT_RECORDS` in `db.js` instead.
- the store name in `SyncKinds` in `Core/Sync`, or it won't sync. The API only takes the kinds listed there, and a test checks the list against `BackupData`.
- `BackupData` and its `Counts`, export and restore in `BackupService`, and the restore summary in `Pages/Settings.razor`
- a fake in `tests/Stikling.Core.Tests/Fakes.cs`, and `BackupTests`
