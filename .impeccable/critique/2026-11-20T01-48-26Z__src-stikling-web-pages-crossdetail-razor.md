---
target: Crosses screens (PR 314)
total_score: 25
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 2
target_identity: "file:src/Stikling.Web/Pages/CrossDetail.razor"
target_fingerprint: "sha256:fa4731e4e10c3179e81cc14a5bf30c7968c484928d649d1d6bb6b3e926b22cea"
target_path: "src/Stikling.Web/Pages/CrossDetail.razor"
timestamp: 2026-11-20T01-48-26Z
slug: src-stikling-web-pages-crossdetail-razor
---
Method: dual-agent (A: design review sub-agent, source only · B: detector sub-agent)

## Crosses and seed lots (PR #314)

Design health: about 2.5 of 4 on average (25/40). Detector: 0 findings on all 10 files; the browser overlay pass did not complete (pane timeouts), so there is no rendered evidence.

Verdict: consistent with the system and calm, but generic. Nothing in the UI shows time passing, which is what crossing is about.

Strengths:
- The Crosses section stays hidden until used, with a quiet start line.
- Lineage is carried through: working name on seedlings, typed-name fallback, a deleted plant keeps a readable name.
- The cross page follows the real order: parents, outcome, lots, seedlings, and every empty block says what comes next.

Priority issues:
- P1 Recording the outcome needed Edit and the whole form. Fixed: It took / It didn't take buttons on a cross with no outcome.
- P1 A failed check showed its alert far above Save on a phone. Fixed: the alert takes focus and scrolls into view on the cross and seed lot forms.
- P2 The new cross form was heavy (about 14 controls, the outcome shown before anything happened). Fixed: outcome only when editing, how-details folded, one line saying what a cross is. Used up on is only on an existing lot.
- P2 Time is invisible and the first cross is hard to find. Partly fixed: a waiting cross shows how many days ago it was pollinated. Start line reworded. Left: showing the start line higher up on Propagations.
- P3 Small links under 44 px. Fixed: 2.75rem tall.

Left alone: pod ripening on Today (a new feature, needs a decision), search in the parent picker (native select like the rest of the app), a thumbnail on cross rows, the Firefox Android datalist check on the seller field (needs a device).
