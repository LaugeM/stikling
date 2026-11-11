# Adding a new kind of record

A new IndexedDB store touches more places than the model and its page, and missing one of them quietly leaves the records out of backups. Use the feeds commit (`3e255f3`) as the example:

- the model in `Core/Models` and a repository interface next to its service
- a field in `RecordKinds` in `Core/Sync/RecordKinds.cs`, and its line in `RecordKinds.All`, added at the end of the collection's kinds (before `Settings`). The order matters: a device keeps the list it last synced with and fetches everything again when it changes, so a kind in the middle makes every device start over. The name is the store's name on every device and the server, so it never changes. Two flags to set: `isContent: false` for a kind a fresh device can have before anything has been added (settings, what's put off on Today, sitter sheets), so it doesn't count as something a restore could overwrite. `RecordOwner.Person` for a kind that belongs to the person and not the collection, which only `Settings` is.
- a new `if (event.oldVersion < N)` block in `wwwroot/js/db.js` with `DB_VERSION` raised. Never change an old block. A store that doesn't hold records, like `photoBlobs`, goes in `NOT_RECORDS` in `db.js` and in `Stores` in `Services/IndexedDb.cs` instead of in `RecordKinds`.
- a property on `BackupData` and its line in `BackupData.Lists`, in the same position. The property name is the backup's format, so it is written out and never changes.
- a number in `BackupCounts` and its line in `BackupData.Counts`
- the implementation in `IndexedDbRepositories.cs`, which passes its `RecordKinds` field to `IndexedDbEntityRepository`, and the registration in `Program.cs`
- a fake in `tests/Stikling.Core.Tests/Fakes.cs`

Nothing else is needed for sync, export or restore. The sync kinds, the change list, the backup's export and restore and the check for something to save first all go through `RecordKinds` and `BackupData.Lists`.

## What the tests catch

`RecordKindTests` fails when a step is missed:

- an `Entity` without a kind in `RecordKinds`, or two kinds for the same one
- a kind missing from `BackupData.Lists`, or the lists in another order than the properties on `BackupData`
- a list that reads or writes another list's property, which the export and restore round trip also shows
- a kind left out of `BackupData.Counts`, since `BackupCounts` has to count every list
- a kind without a store in `db.js`, or a store there that is neither a kind nor in `NOT_RECORDS`
- a changed sync order (`SyncKinds.All` is pinned) and a changed backup format (`BackupTests` pins the property names)

Adding a kind makes the pinned lists fail on purpose. Add the new name to them in the same place it has in the code.
