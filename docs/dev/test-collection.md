# Testing with a real collection

A `stikling-testdata` folder next to the stikling checkout holds backups of my real collection, named `stikling-backup-<date>.zip`. It is outside the repository on purpose. Older ones stay when a new one is added, so they double as backups from older versions of the app.

Use it when a change touches stored data or restore (a migration, raising `DB_VERSION`, a converter), when a screen needs a full collection to judge (long lists, the desktop layout, plants with a long history), or to check that older backups still restore. For anything else, a few records added by hand are quicker.

- `stikling-testdata` in `.claude/launch.json` serves the folder on localhost, so a page can restore from it without a file picker. It runs on Node. `.claude/agents/browser-check.md` has the snippet that does it.
- Never sign in while it's restored. A sync would upload the collection to that account.
- Never copy its plant names, notes, places or photos into commits, pull requests, the Help page or anything else in the repository.
- Clear the database afterwards with `indexedDB.deleteDatabase("stikling")` from the page, then reload.
