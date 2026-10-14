---
target: "Import plants page, PR #151"
total_score: 23
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 2
target_identity: "file:src/Stikling.Web/Pages/ImportPlants.razor"
target_fingerprint: "sha256:51fe6950a4a6de5933dbd2d548fe1587bcc2c5ecbc0027e6958ebb89bf9d0e9a"
target_path: "src/Stikling.Web/Pages/ImportPlants.razor"
timestamp: 2026-10-14T17-04-51Z
slug: src-stikling-web-pages-importplants-razor
---
Method: dual-agent (A: design review · B: detector + browser)

Target: Import plants page, PR #151 (src/Stikling.Web/Pages/ImportPlants.razor, /plants/import). Checked at 412 x 907, 1024 x 800, 1600 x 1000 and in dark mode.

## Design Health Score

| # | Heuristic | Score | Key Issue |
|---|-----------|-------|-----------|
| 1 | Visibility of System Status | 2 | No step marker; on a phone the Plants step loses the file name and Start again; a failed add showed a raw stack trace |
| 2 | Match System / Real World | 2 | "Snake plant" shows as Dracaena trifasciata with no trace of what was typed; "Add to About it" next to "About it" |
| 3 | User Control and Freedom | 3 | Undo, Start again and Back to columns exist; Undo is gone once you leave the result screen |
| 4 | Consistency and Standards | 3 | Follows DESIGN.md; unticked checkboxes use Bootstrap's cool grey border |
| 5 | Error Prevention | 2 | "Tick the plants to add" is an enabled primary button that does nothing; Use these cells enabled when empty |
| 6 | Recognition Rather Than Recall | 2 | What goes into About it never shows in the preview |
| 7 | Flexibility and Efficiency | 3 | Tick all / Untick all; on a wide screen a column change updates the plants beside it at once |
| 8 | Aesthetic and Minimalist Design | 3 | Calm, but ticked and unticked rows carry the same weight |
| 9 | Error Recovery | 1 | A failed add showed ex.Message (IndexedDB text) on a result screen with only Go to Plants; the mapping was lost |
| 10 | Help and Documentation | 2 | Good intro line; nothing says what About it is |
| **Total** | | **23/40** | **Acceptable** |

## Design Specificity Verdict

**LLM assessment**: Partly authored. It speaks the app's language: hairline blocks like Settings, Bricolage names, Deep Moss for "Looks like one you have", the slim progress bar, and problem copy like "Couldn't read "31/02/2024" as a date, kept in About it". The structure is a stock CSV import wizard, and the result screen ("6 plants added" and a count line) could belong to any app.

**Deterministic scan**: `impeccable detect` on ImportPlants.razor returned 0 findings. The in-page detector reported no anti-patterns on the Choose step, the Columns step at 412px, and the Plants step at 412px and 1024px. No false positives to discount. The scan did not cover the Columns step at desktop width.

**Visual overlays**: injection worked in the assessment tab and reported clean; that tab was closed afterwards, so no overlay is left open.

## Overall Impression

The flow is sound and the copy for problems is the best part. What's weak is trust at the moment of a bulk write to someone's real collection: the preview hides what will be written, a failure dead-ends, and the result screen doesn't show what arrived.

## What's Working

- Problem copy is plain and specific, and every unreadable value is kept in About it with a sentence saying so.
- Duplicates start unticked and are marked calmly; Undo is instant and says what it removed.
- The date order question shows what each choice means ("03/04 is 3 April") and appears only when the dates really are ambiguous.

## Priority Issues

- **[P1] A failed add dead-ends.** ImportPlants.razor:501 put `ex.Message` on screen (raw IndexedDB text, overflowing at 412px), moved to a "No plants added" result with only Go to Plants, and dropped the column choices. **Fix**: a plain sentence, and when nothing was added stay on Plants with the ticks so Add can be tried again. Same for Undo's error. **Command**: /impeccable harden
- **[P1] The preview hides what will be written.** Plant rows never show the About it content, and a name read as a botanical name loses what was typed. **Fix**: a muted "From “Snake plant”" line when the shown name differs, and a muted "About it: Pot size: 17 cm · …" line. **Command**: /impeccable clarify
- **[P2] The column picker is a flat list of 19 with twin labels.** **Fix**: optgroups (Keep or leave out / Name / Where and when / Care / More about it), and "A line in About it" vs "About it, the notes". **Command**: /impeccable distill
- **[P2] Ticked and unticked rows look the same, and the checkbox border is about 1.3:1 on white.** **Fix**: Muted Ink checkbox border on the import rows, and unticked rows' names in Muted Ink. **Command**: /impeccable polish
- **[P3] Dead primary button and an empty result.** **Fix**: disable Add while nothing is ticked; list the added plants on the result screen. **Command**: /impeccable polish

## Persona Red Flags

**Jordan (first-timer)**: doesn't know what About it is, why "Row 2" leads each line, or why the Snake plant now has a Latin name. No "1 of 3".

**Casey (phone, one hand)**: result actions sit at the top on a short screen; the update banner covered "Next: check 6 plants" until dismissed.

**Sam (keyboard / screen reader)**: after Next or Back to columns, focus stays on a button that is now hidden. Each plant label reads the whole row. The count line is announced after every match change.

## Minor Observations

- "6 looks like one you have" should be "6 look like ones you have".
- At 1024px the 24rem columns pane leaves the plants one narrow column; the columns should give up room first.
- "Where" isn't recognised as Room.
- The paste error shows above the Choose button, away from the paste box.
- The undone screen says "Import another file" after pasted cells.
- The h1 stays "Import plants" on the result screen.

## Questions to Consider

- Should the result screen be the new plants themselves, Plants filtered to this import with an Undo bar?
- If a duplicate can't name the plant it matched, is "Looks like one you have" something the person can check?
- Does the picker need all 19 targets up front, or the five common ones with the rest under More?
