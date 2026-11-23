# Merging branches

Most branches add text keys at the end of a `.resx` file and a line at the top of the current month in `Pages/WhatsNew.razor`. Two such branches conflict even though both changes belong in the result, and resolving that by hand has gone wrong before: a merge that took one side left a `<data>` entry without its closing tag, and every translation test failed in CI.

`tools/git/merge-keep-both.py` is a git merge driver that keeps both. `.gitattributes` uses it for every `.resx` file and for `WhatsNew.razor`.

## What it does

- **`.resx` files** are merged key by key, not line by line. A key added, changed or removed on one branch is taken from that branch. Keys the other branch added go after the last key. The result has to parse as XML.
- **`WhatsNew.razor`** is merged by git as usual. A conflict is settled only when every line in it, on both sides, is a `Translated(Strings.WhatsNew.X),` line. The current branch's lines go first.

Anything else is a real conflict and is left to you with the usual conflict markers: both branches changing the same key in different ways, one changing a key the other removed, a change to the file's header, or any other code in `WhatsNew.razor`.

GitHub's own merge doesn't run the driver. A pull request that GitHub says has conflicts is fixed by merging `main` into the branch locally.

## Turning it on

The project's SessionStart hook in `.claude/settings.json` sets it up in every Claude Code session. In a clone without it, run this once (worktrees share the setting):

```
git config merge.keep-both.name "Keep both sides of added resx keys and WhatsNew lines"
git config merge.keep-both.driver "python tools/git/merge-keep-both.py %O %A %B %P"
```

Without the setting git ignores the attribute and merges these files the normal way.

## After a merge

Build and run `dotnet test Stikling.slnx` as for any change. `TranslationFileTests` reads every `.resx` file, so a broken merge fails there. Look over the What's new list for the month too, since the order of the lines from the two branches is a choice you may want to change.
