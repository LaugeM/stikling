---
target: Pollen, traits, Today card and compare (PR 317)
total_score: 28
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 2
target_identity: "file:src/Stikling.Web/Pages/CrossCompare.razor"
target_fingerprint: "sha256:256b0c6b6a34af07066828db3415101fd7acc8382e70c4832bb1c19b6202e560"
target_path: "src/Stikling.Web/Pages/CrossCompare.razor"
timestamp: 2026-11-20T01-54-04Z
slug: src-stikling-web-pages-crosscompare-razor
---
Method: dual-agent (A: design review sub-agent, source only · B: detector sub-agent)

## Pollen, traits, the Today card and comparing seedlings (PR #317)

Design health: about 28 of 40 (70%, Good). Detector: 0 findings on all 7 files; the browser overlay pass was skipped because the pane was unreliable, so there is no rendered evidence.

Verdict: restrained and built from the app's own parts, which is right for Operate mode. Not yet design-led: Compare lists facts without showing where the seedlings differ.

Strengths:
- The Today row reuses TodayRow and the put-off vocabulary, with a precise Not this flowering.
- The no-advice rule holds: the pollen line is a record (collected, where), nothing about how long it lasts.
- Traits fold away with a summary, and the cross page hides the Traits card when neither parent has any.

Priority issues:
- P1 On a phone Compare stacked cards, so two could not be seen together. Fixed: the cards sit in a row you swipe, the next one peeking in. Left: highlighting where traits differ.
- P1 The check icon on the Today row meant done everywhere else but opened a form here. Fixed: a labelled Record a cross button replaces it.
- P2 The pollen form had seven fields at once. Fixed: amount, used up and note fold under More about this pollen.
- P2 Genus is free text, so a typo silently means no match on Today. Left: suggestions from the plant name data need a shared component and a decision.
- P3 Pollen list rows have no leading tile. Left: it would need a new icon and the row follows the cross rows.

Also fixed: the compare photo link is out of the tab order (the name links there), the Compare toggle says whether it is pressed, and the leaf line uses a middle dot.
