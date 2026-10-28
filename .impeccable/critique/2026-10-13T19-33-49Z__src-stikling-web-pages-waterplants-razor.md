---
target: "Water plants screen, PR #143"
total_score: 26
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 2
target_identity: "file:src/Stikling.Web/Pages/WaterPlants.razor"
target_fingerprint: "sha256:f7a57af038f913803269c7a0e8b13641952e914c8ccbe989c75b4136ff7699f1"
target_path: "src/Stikling.Web/Pages/WaterPlants.razor"
timestamp: 2026-10-13T19-33-49Z
slug: src-stikling-web-pages-waterplants-razor
---
Method: dual-agent (A: design review · B: detector + browser)

Target: Water plants screen, PR #143 (src/Stikling.Web/Pages/WaterPlants.razor, /water)

## Design Health Score

| # | Heuristic | Score | Key Issue |
|---|-----------|-------|-----------|
| 1 | Visibility of System Status | 2 | No step indicator or "3 of 4 read"; a saved reading only tints one chip |
| 2 | Match System / Real World | 3 | Walk order fits real watering; "dry by your reading (3)" is jargon, scale ends unlabeled |
| 3 | User Control and Freedom | 3 | Back and Change rooms work; a reading can't be cleared, a Log can't be undone here |
| 4 | Consistency and Standards | 3 | Back link always says Today; only step 2 has a heading; success uses the dashed placeholder style |
| 5 | Error Prevention | 2 | Rows start ticked and one tap on Log saves them all; each reading tap saves at once |
| 6 | Recognition Rather Than Recall | 3 | Last reading on the row, today's reading repeated in step 2 |
| 7 | Flexibility and Efficiency | 3 | Remembered room order and All help; 0 or 1 room still goes through the room step |
| 8 | Aesthetic and Minimalist Design | 3 | Clean, but 11 chips per plant makes a long dense page |
| 9 | Error Recovery | 2 | Raw ex.Message in errors (WaterPlants.razor:326, :376) |
| 10 | Help and Documentation | 2 | Nothing for someone without a meter; Today's soil-check tips not carried over |
| **Total** | | **26/40** | **Acceptable** |

## Design Specificity Verdict

LLM: Mostly authored for this product. Rooms in walk order, remembered order, reading first then ticking, and "dry by your reading" come from how a collector actually waters. Reuses chips, answer grid, Label heads, Leaf Tint well. Generic at both ends: a bare chip row plus Start, and a dashed placeholder box saying "Logged 3 plants."

Deterministic scan: impeccable detect found 0 issues in WaterPlants.razor, WaterPlants.razor.css, TodayRow.razor, Home.razor. Browser overlay on /water (empty state and room step) and Today also reported no anti-patterns. The reading and tick steps were not overlay-scanned. The detector's silence matches the review: the problems are flow and state design, not visual anti-patterns.

## What's Working

1. State is derived from the care log (WateringPass.cs), so leaving and coming back the same day resumes correctly.
2. Follows the care-advice rule: rows pre-tick only after a reading at or below the plant's own level (WateringPass.cs:128, :139); unread plants say "no reading today".
3. Touch targets are generous, checkboxes are labelled by their whole row, dark mode holds.

## Priority Issues

1. [P1] No path without a meter. Every top-watered plant gets the 0-10 grid and "Tap the meter reading" (WaterPlants.razor:67, :95-104). Casual owners invent numbers or tick blindly. Fix: Dry / Still damp chips (or a "No meter?" option) that pre-tick like a low reading, and label the grid ends 0 dry, 10 wet. Command: /impeccable clarify, then build.
2. [P1] Pre-ticked rows plus one-tap Log can record waterings that didn't happen (WaterPlants.razor:332, :172). Fix: lead says "Ticked because they read dry. Untick any you skipped."; Undo on the done state. Command: /impeccable harden.
3. [P2] Room step is friction for most and confusing on first run (lone "No room" chip, Start disabled; :26-60, WateringPass.cs:52). Fix: skip it for 0 or 1 room; "All rooms" as the main action, not a chip that looks like a room. Command: /impeccable distill.
4. [P2] No progress on the readings step. Fix: sticky bottom bar "3 of 4 read · Next" (Thumb Rule, Anchored bar); quiet done state on read rows. Command: /impeccable layout.
5. [P2] Done state uses the empty-state dashed look (:177-186). Fix: solid Leaf Tint panel listing the plants and feeds logged, Back to Today primary, Undo as text link, drop the Plants button. Command: /impeccable polish.

## Persona Red Flags

Casey: Start and step links at the top; Next and Log at the end of a long scroll; a stray tap while scrolling saves a reading; `busy` disables every grid while one saves (:103, :315) so quick taps drop.
Jordan: "Tap the meter reading" assumes a meter; "dry by your reading (3)" unexplained; first run offers only "No room"; title "Water plants" over a list of every plant can read as "water all of these".
Sam: step changes don't move focus or announce (no aria-live except done); order badge is a span aria-label inside the button (:45), often ignored; unticked checkbox border about 1.3:1 and pure grey (likely app-wide .form-check-input).

## Minor Observations

- Back link hard-coded to Today (:18) though the screen is also reached from Plants.
- Uneven headings: only step 2 has an h2 (:132).
- Desktop answer grid wraps 11 values into 6+5; one row would read as a scale.
- Picked room chip goes bold and shifts the chips after it.
- Disabled "Log " with trailing space when nothing is ticked (:173); say "All done for today" instead.
- Plant name links on the readings step navigate away mid-pass and lose ticks.

## Questions to Consider

- Should the screen open on what's due, with rooms as a sort order rather than a gate?
- People often water right after reading. Would one combined step (read, then a Watered toggle on dry rows) be fewer taps?
- If the reading already says dry, is the tick the real confirmation, or a second chance to say no?
