---
target: People section, Join page and collection switcher
total_score: 28
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 2
target_identity: "file:src/Stikling.Web/Components/PeopleSection.razor"
target_fingerprint: "sha256:020ddaad47feb7a29dfae7ece5e1109a33266ab1d3aee74a89e6863ea537ad69"
target_path: "src/Stikling.Web/Components/PeopleSection.razor"
timestamp: 2026-11-18T09-35-42Z
slug: src-stikling-web-components-peoplesection-razor
---
Method: dual-agent (A: design review · B: detector and browser)

| # | Heuristic | Score | Key issue |
|---|---|---|---|
| 1 | Visibility of system status | 3 | Focus drops to body after Make link, Cancel on the raise and Withdraw |
| 2 | Match system / real world | 3 | "Your own plants stay as they are" shown to someone with none; "Sign in to join" to someone with no account |
| 3 | User control and freedom | 3 | Withdraw is instant, on rows that look alike |
| 4 | Consistency and standards | 3 | Switcher line uses Bootstrap grey and the browser focus outline; "samling" on the line, "planter" in the rows |
| 5 | Error prevention | 3 | Identical invite rows invite a wrong withdraw |
| 6 | Recognition rather than recall | 2 | Open invites named only by role; three Withdraw buttons share one accessible name; switchable sheet rows have no cue |
| 7 | Flexibility and efficiency | 2 | Keyboard focus lost after the main actions |
| 8 | Aesthetic and minimalist design | 3 | h2 "People" reads quieter than the Labels under it; Join is a small card on a large desktop canvas |
| 9 | Error recovery | 3 | Join problem state only offers Close |
| 10 | Help and documentation | 3 | Neither People nor Join links to the Help answers |
| Total | | 28/40 | Good |

Design specificity: People and the switcher are built from the house parts (Settings Row blocks, Label markers, confirm cards, the picked-chip look on role rows) and the house voice. The Join page is the exception: a Leaf Tint card with a sentence and a button that would fit any app, with nothing of the plants on it.

Detector: 4 design-system-color findings, all false positives (raw Razor rendered without the stylesheet, default black at line 0). Browser overlay on Account, Join and Today at 412 and desktop: no anti-patterns.

Strengths: plain-language roles with their meaning at the decision and again on Join; guards match the risk (raise asks, Leave syncs first and offers a backup, owner hands over first); the switcher line only exists while a shared collection is open.

Priority issues:
- [P1] Join doesn't carry the first run: no plants, counts or photos of what you join; own-plants line and "Sign in to join" don't fit a newcomer; Not now exits to an empty app. Fix: preview counts and a few covers, show the own-plants line only to signed-in people with plants, lead with "Continue with a free account". /impeccable onboard
- [P1] Open invites can't be told apart. Fix: optional "Who is it for?" on the invite as a new server field, used as the row name; aria-labels with role and date on Withdraw and Show link. /impeccable clarify
- [P2] Switcher line off-system: Bootstrap grey, browser focus outline, static-looking switchable rows. Fix: --pa-muted, the green focus ring, a chevron or "Open" on switchable rows. /impeccable polish
- [P2] Focus lost after Make link, Cancel and Withdraw. Fix: focus the link field, the member's select, the next row. /impeccable harden
- [P3] "Change name" shortened to "Skift" in Danish; "Yours" line says nothing; "Owner. Can edit…" reads as fragments; h2 weaker than Labels. /impeccable clarify

Persona red flags: newcomer from a link meets a sign-in button and an own-plants line that doesn't apply; screen reader user meets three identical "Withdraw" names and lost focus; stress tester signing in as Bo on a device whose main database held Alma's collection saw "Alma's plants: Open now" with no switcher and no Leave (worth checking in CollectionsService); plant-sitter has to make an account and sees nothing of the plants first; owner can't see who hasn't accepted yet.

Minor: owner opening their own link sees "Join a collection" over "You're already in", and "Open it" for the open collection; role-row label in system font while names are Bricolage; .switcher-manage at 0.9rem is off-step; made-link card names the role only in its hint.
