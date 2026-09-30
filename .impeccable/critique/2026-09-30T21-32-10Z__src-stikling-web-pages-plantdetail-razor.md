---
target: plant and propagation pages and their add and edit forms
total_score: 23
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 3
target_identity: "file:src\\Stikling.Web\\Pages\\PlantDetail.razor"
target_fingerprint: "sha256:e87f652d1f5466263d661e94eff65bca24ff6c40bc7141401da51d4788721293"
target_path: "src\\Stikling.Web\\Pages\\PlantDetail.razor"
timestamp: 2026-09-30T21-32-10Z
slug: src-stikling-web-pages-plantdetail-razor
---
Method: dual-agent (A: design review · B: detector and browser evidence)

Target: plant and propagation detail pages and their add/edit forms (PlantDetail, PropagationDetail, PlantEdit, PropagationEdit and shared components), phone width, light and dark.

## Design health score

| # | Heuristic | Score | Key issue |
|---|---|---|---|
| 1 | Visibility of system status | 2 | Form errors render ~830px above the viewport; a finished propagation keeps counting "day 121" |
| 2 | Match system / real world | 3 | Mostly plain; add-propagation form never says what a propagation is; LECA/PON unexplained |
| 3 | User control and freedom | 2 | Mark failed and pot up can't be undone; typed note dropped when the sheet is dismissed |
| 4 | Consistency and standards | 2 | Bootstrap blue link hover, #dc3545 outline danger, cool grey status badge; "Save" vs verbs in sheets |
| 5 | Error prevention | 2 | Stage segment saves on tap; Mark failed equal weight next to Pot up with no explanation |
| 6 | Recognition rather than recall | 3 | Experiment name not on the propagation page; milestone dates three levels deep |
| 7 | Flexibility and efficiency | 3 | Add another like this, Quick add, Saw a new leaf today |
| 8 | Aesthetic and minimalist design | 2 | Finished propagation says its outcome four times and "day N" twice |
| 9 | Error recovery | 1 | Validation alert off screen, focus stays on Save, no field marked |
| 10 | Help and documentation | 3 | Good inline form-text; delete confirm points to softer options |
| Total | | 23/40 | Acceptable |

## Design specificity verdict

LLM: mostly authored for this product. Day-count tile, stage ticks and path picker, clay for plants, situational sheet copy ("It leaves your collection, its history stays"), loose acquired date. Generic layer on top: the header is a stack of small muted lines plus two text links, Bootstrap colours leak through, and the finished propagation reuses the active layout instead of reading as a result.

Deterministic scan: CLI detect over 10 files, 0 findings. Browser detect.js: 1 finding per detail page, skipped-heading (h1 then Timeline day h3, Timeline.razor:24). Forms clean. No contrast failures, no horizontal overflow in either scheme. Tap targets under DESIGN.md's 44px rule: inline links (family line, parent link, "2 plants potted up from it", Timeline "Open propagation", Quick add) at 19-21px tall.

## Priority issues

1. [P1] Validation errors render off screen on both forms. PlantEdit.razor 31-39 / 476-484, PropagationEdit.razor 23-31 / 377-383. Fix: scroll the alert into view and focus it on every failed save.
2. [P1] Finished propagation header repeats the outcome and keeps counting days (known issue, confirmed). PropagationDetail.razor 67, 76, 95-100, 140; Labels.Age ignores FinishedOn. Fix: one line "Finished 15 Sep 2026 after 106 days · 2 potted up, 1 failed", "2 potted up" linking to the plants.
3. [P1] Mark failed is irreversible, one tap from Pot up, with no word on what it does. PropagationDetail.razor 356-361, 404-430. Fix now: explain it and fix "Mark 1 as failed" when one is left. Undo is a product decision.
4. [P2] Off-palette Bootstrap colours: link hover blue (no --bs-link-hover-color), .btn-outline-danger #dc3545 about 3.6:1 on dark Surface, .text-bg-secondary cool grey badge.
5. [P2] Pot up sheet shows Soil mix for any medium and defaults a water cutting to Water; nothing says what pot up does. PropagationDetail.razor 390-392, 747.

## Persona red flags

- Jordan (first-timer): empty-name error invisible; "Start propagation" with no explanation; plant facts line reduces to a lone "Soil".
- Casey (one hand): stage tap saves instantly; Mark failed beside Pot up at equal size; note text lost on backdrop tap; entry delete ✕ top right and always visible.
- Sam (accessibility): stage dock button named only "Rooting"; favourite button inside the h1; skipped heading level; errors not moved into focus.
- Corm experimenter: experiment name missing from the propagation page; finished batch reports "day 121"; pot-ups and failures show as generic "Updated"; failures can't be dated.

## Minor observations

- Italic species rule broken in the h1 when there is no nickname.
- Seeds show "First root: Not yet" and "from Seeds from the garden centre".
- Potted-up plant says "from Alocasia 'Frydek'" and skips the propagation it came from.
- Plant status sheet button is a bare "Save".
- "(optional)" styled two ways.
- Finished-propagation list row meta wraps to two lines.
- Typed ✕ glyphs in Timeline, EntryComposer and PlantEdit instead of stroked SVG.
- Focused inputs switch to the Paper background.

## Questions to consider

- Should a mistaken pot up or mark failed be undoable?
- Should tapping a stage save at once, or wait for a confirm?
- Propagate sits behind More while Care has a dock slot; is that the right way round for this app?
- Should dismissing the note sheet keep the typed text?
