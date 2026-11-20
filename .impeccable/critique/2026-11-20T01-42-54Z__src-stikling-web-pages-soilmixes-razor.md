---
target: PR 316 batch and stock screens
total_score: 26
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 2
target_identity: "file:C://Users//lauge//Desktop//Privat//Fritidsprojekter//Claude//stikling//.claude//worktrees//awesome-goldwasser-76ab3e//src//Stikling.Web//Pages//SoilMixes.razor"
target_fingerprint: "sha256:1513c2cf5d8cfa5c1d2dc17ec4a7643b843c721d0656c46dec3ad44d4f96f146"
target_path: "C://Users//lauge//Desktop//Privat//Fritidsprojekter//Claude//stikling//.claude//worktrees//awesome-goldwasser-76ab3e//src//Stikling.Web//Pages//SoilMixes.razor"
timestamp: 2026-11-20T01-42-54Z
slug: src-stikling-web-pages-soilmixes-razor
---
Method: dual-agent (A: design review, source only · B: detector)

Targets: batch form, batch list and Link to stock panel (SoilMixes.razor), check sheet (TakeoffSheet.razor), Mixed a can (Feeds.razor).

Score: 26/40 (Acceptable). Visibility 3, Match 3, Control 3, Consistency 2, Prevention 3, Recognition 3, Flexibility 3, Minimalist 2, Recovery 2, Help 3.

Detector: no findings in the three pages or their CSS.

Priority issues
- [P1] "Mixed a can" labels both the opener and the save button on Feeds. Fixed: the save button says Save.
- [P1] The check sheet does not say what it is for. Fixed: a subtitle with the day and size (and the feed name for a can).
- [P2] Seven actions in one row on a mix, Delete next to everyday ones. Left alone: the row already wraps with 44px targets and Delete is the same on every other list.
- [P2] The "can't be worked out" note was not announced. Fixed: role=status.
- [P3] Remove links in the mixing and can lists had no distinguishing name. Fixed: aria-label with the date.

Minor
- Batch form shows the date picker for the common today case. Left alone.
- Focus does not move to the volume field when the batch form opens. Left alone.
- .mix-batch-head is declared twice in the CSS. Left alone.
