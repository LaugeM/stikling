---
target: People section and Join page
total_score: 24
max_score: 40
na_heuristics: 
p0_count: 1
p1_count: 2
target_identity: "file:C:\\Users\\lauge\\Desktop\\Code\\claude\\projects\\stikling\\.claude\\worktrees\\notes-and-tidying-5fed24\\src\\Stikling.Web\\Components\\PeopleSection.razor"
target_fingerprint: "sha256:450529a096bcdacd883280c6b6cc2787431972d5027f28853ef97f854cd3c3e0"
target_path: "C:\\Users\\lauge\\Desktop\\Code\\claude\\projects\\stikling\\.claude\\worktrees\\notes-and-tidying-5fed24\\src\\Stikling.Web\\Components\\PeopleSection.razor"
timestamp: 2026-11-18T08-56-55Z
slug: src-stikling-web-components-peoplesection-razor
---
Method: dual-agent (A: design review · B: detector and browser)

| # | Heuristic | Score | Key issue |
|---|---|---|---|
| 1 | Visibility of system status | 2 | Opening /join cold while signed in says "Sign in to see this invite"; switching collections lands on Today with no word |
| 2 | Match system / real world | 3 | Plain words; "collection" never explained on Join |
| 3 | User control and freedom | 3 | Role select saves on change; invite link gone after Done |
| 4 | Consistency and standards | 2 | Bare radios, Bootstrap grey Leave, headings off the Quiet Label rule, rows not in Settings Row blocks |
| 5 | Error prevention | 3 | Good confirms; one mis-tap grants "Can edit and add people" |
| 6 | Recognition rather than recall | 2 | Roles have no meaning where chosen; open invites look alike |
| 7 | Flexibility and efficiency | 2 | No way to reshow a link; switching is three levels deep |
| 8 | Aesthetic and minimalist design | 2 | One flat stack, name shown twice, equal-weight buttons |
| 9 | Error recovery | 3 | Plain errors with a next step |
| 10 | Help and documentation | 2 | Help answers exist but nothing points to them |
| Total | | 24/40 | Acceptable |

Priority issues:
- [P0] Cold-loaded /join shows the signed-out state to a signed-in person (Join.razor checks sign-in before the account is loaded). Both assessments hit it.
- [P1] Join page gives a newcomer nothing to decide with: no inviter or role when signed out, no word on what the role allows or that their own plants stay as they are.
- [P1] Roles chosen without their meaning, in 24px bare radios; member select commits on change with no confirm for raising to "edit and add people".
- [P2] People section has no structure: heading levels look the same, collection name shown twice, Hand over as loud as Invite, "Download your data" runs on.
- [P2] An invite link can't be shown again after Done, and open invites with the same role look identical.
- [P3] Leave button is Bootstrap grey (about 3.8:1 in dark), reads as disabled.

Detector: 3 design-system-color findings, all false positives (line 0, browser default black on raw Razor). Overlay: one straggler on YourNameHint in Danish at 375px; the line-length finding is on the existing sync hint, outside this change.
