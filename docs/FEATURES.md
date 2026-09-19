# Stikling feature list

A working list of what Stikling could do, grouped by area and tagged with a priority and a target milestone. Tick items off as they ship, and change priorities as the app gets used.

**Priority**
- **Must**: the app isn't useful without it.
- **Should**: clearly worth building.
- **Could**: nice to have, depending on time and interest.
- **Later**: needs accounts/sync (v2) or a native app (v3).

**Milestones:** M1 scaffold · M2 plants · M3 photos & timeline · M4 propagations · M5 backup & Today · then v1.x releases.

---

## 1. Plants

| ✓ | Feature | Priority | When |
|---|---|---|---|
| ✅ | Add, edit and delete plants: nickname, genus, species, cultivar, location, origin, date, source, medium, pot, notes | Must | M2 |
| ✅ | Status: in collection, died, given away, sold. Gone plants are hidden but keep their history | Must | M2 |
| ✅ | Search on all name fields; filter by status and room | Must | M2 |
| ✅ | Parent plant and offspring, "Add offspring" | Must | M2 |
| ✅ | Cover photo on cards and the plant page | Must | M3 |
| ✅ | Tabs on the plant page: **Info**, **History** (timeline), **Props** (propagations taken from it) | Must | M3–M4 |
| ☐ | Select several plants at once: change status, move to a room, add a note to all | Should | M3 |
| ☐ | Favourites / pinned plants at the top of the list | Could | v1.x |
| ☐ | Tags (e.g. "variegated", "rare", "quarantine", "for swap") and a tag filter | Should | v1.x |
| ☐ | Sort options: name, newest, room, last activity | Could | v1.x |
| ☐ | Grid view with large photos as an alternative to the list | Could | v1.x |
| ☐ | Species autocomplete beyond genus (a bundled list of common houseplant species and cultivars) | Should | v1.x |
| ☐ | **Plant dictionary**: a large built-in list of plants and their varieties (genus, species, cultivars, common names) to search and pick from when adding a plant or propagation. Works offline | Should | v1.x |
| ☐ | Duplicate a plant (e.g. a second basil pot) | Could | v1.x |
| ☐ | Wishlist: plants you want, with notes on where to find them | Could | v1.x |
| ☐ | **Quick add**: paste or type a list of names ("Alocasia zebrina, Monstera deliciosa, …") to add many plants at once | Should | v1.x |
| ☐ | **Dormancy status**: e.g. alocasias dropping leaves in winter. Pauses "check on it" reminders and keeps the plant from looking dead | Should | v1.x |
| ☐ | **Soil to semi-hydro transition tracker**: mark a plant as transitioning, with a checklist (roots washed, first new water root, first new leaf) and a warning if nothing happens after a set number of weeks | Should | v1.x |
| ☐ | **Leaf log for variegated plants**: each new leaf with a photo and a variegation rating (low / medium / high / reverted), so reverting plants are spotted early | Should | v1.x |
| ☐ | **Problem log**: yellow leaves, root rot, sunburn, crispy edges. What you saw, what you did, and whether it helped | Should | v1.x |
| ☐ | Purchase price per plant | Could | v1.x |

## 2. Photos & timeline

Every plant and propagation gets its own history. Entries are only ever added, never overwritten, so the full history is kept.

| ✓ | Feature | Priority | When |
|---|---|---|---|
| ✅ | Take a photo with the camera or pick from the gallery. Photos are resized and compressed on the phone (≈1600 px) with a thumbnail | Must | M3 |
| ✅ | Photo timeline per plant and per propagation, newest first, tap to view full screen | Must | M3 |
| ✅ | Notes on the timeline ("new leaf unfurling", "roots 2 cm") | Must | M3 |
| ✅ | Automatic timeline entries for changes: status, medium/pot ("moved to semi-hydro"), room, stage | Must | M3 |
| ✅ | Choose the cover photo | Must | M3 |
| ☐ | Swipe through photos side by side to compare growth | Should | v1.x |
| ☐ | Correct or hide a timeline entry by adding a new entry, keeping the original | Should | M3 |
| ☐ | Measurements on an entry: number of leaves, height, root length | Could | v1.x |
| ☐ | Time-lapse / before-and-after image from a plant's photos | Could | v1.x |
| ☐ | **Import existing photos with their dates**: pick old photos from the gallery and each is placed on the timeline by the date it was taken (EXIF), so a plant's history starts from day one | Should | v1.x |
| ☐ | **Ghost overlay** when taking a photo: a faint copy of the previous photo to line up the same angle every time | Could | v1.x |
| ☐ | **Share into Stikling**: the installed app appears in Android's share menu, so a photo from the gallery can go straight to a plant (Web Share Target) | Could | v1.x |
| ☐ | **Voice notes**: a microphone button on note fields using the phone's speech-to-text | Should | v1.x |

## 3. Propagations

This is the core of the app: cuttings, corms, seeds and divisions, linked to their parent.

| ✓ | Feature | Priority | When |
|---|---|---|---|
| ✅ | One-tap **Propagate** on a plant page, which creates a propagation linked to that plant | Must | M4 |
| ✅ | Type: cutting, corm, offset/pup, seed, division, air layer, leaf | Must | M4 |
| ✅ | Medium: water, perlite, sphagnum, LECA, PON, soil, "humidity dome", other | Must | M4 |
| ✅ | Stages: started, rooting, rooted and done (or failed). The date of each stage change is saved | Must | M4 |
| ✅ | Batches with a count ("3 corms in LECA"); mark some as failed | Must | M4 |
| ✅ | **Promote to plant**: turn 1..n units of a batch into plants and keep the lineage | Must | M4 |
| ✅ | Propagations list grouped by stage, with cards like "3× corm · LECA · day 24" | Must | M4 |
| ✅ | Propagations without a parent plant (bought seeds, e.g. the bay laurel) | Must | M4 |
| ☐ | "Check on it" reminder in the app: a propagation not looked at for X days shows on Today | Should | M5 |
| ☐ | **Experiments**: group propagations started together to compare mediums (your corm test: perlite vs sphagnum vs LECA vs humidity dome) | Should | v1.x |
| ☐ | Success rate and average days-to-root per medium and per type | Should | v1.x |
| ☐ | Seed germination: sown count, germinated count and dates | Should | v1.x |
| ☐ | Lineage tree view (family tree across generations) | Could | v1.x |
| ☐ | Printable QR labels for jars and pots; scanning one opens the plant or propagation | Could | v1.x |
| ☐ | **Milestones** on a propagation: first root, first leaf, corm size. Gives the medium experiments concrete dates to compare | Could | v1.x |
| ☐ | **Given away / swapped**: record who got a cutting, so the family tree extends to friends' plants | Could | v1.x |
| ☐ | **"Available for swap" list**: mark propagations as available and share a simple list or image before a plant swap | Could | v1.x |

## 4. Care log

Built around your routine: moisture meter instead of a watering schedule, semi-hydro reservoirs, and different fertilisers for different plants.

| ✓ | Feature | Priority | When |
|---|---|---|---|
| ☐ | Quick-log care: watered, fertilised, reservoir topped up, flushed, repotted, pruned, rotated, cleaned leaves, harvested | Must | v1.x |
| ☐ | Log several plants at once ("fertilised all semi-hydro plants") | Must | v1.x |
| ☐ | **Moisture meter reading** (1–10) as a log type, with the latest reading shown on the card | Should | v1.x |
| ☐ | **Products**: your fertilisers (the hydro one, the general one, the herb one), each with a default dose | Should | v1.x |
| ☐ | Which product and dose was used, e.g. "Hydro fertiliser, 2 ml/L" | Should | v1.x |
| ☐ | Water type: tap, demineralised, rain | Could | v1.x |
| ☐ | Semi-hydro details: reservoir level, flush interval, last flush | Could | v1.x |
| ☐ | **Nutrient dose calculator**: reservoir size × the product's dose per litre = ml to add | Could | v1.x |
| ☐ | "Last watered / fertilised / flushed" summary on each plant | Should | v1.x |
| ☐ | Harvest log for herbs (basil, parsley): date and amount | Could | v1.x |
| ☐ | Flowering log (orchid, peace lily): spike started, blooming, done | Could | v1.x |
| ☐ | Optional care intervals per plant ("fertilise every 2 weeks") that show up on Today | Could | v1.x |

## 4b. Light

| ✓ | Feature | Priority | When |
|---|---|---|---|
| ☐ | Light level per plant: low, medium, bright indirect, some direct sun | Should | v1.x |
| ☐ | Window direction for each room (north, east, south, west), so plants in a room show what light they get | Could | v1.x |
| ☐ | **Grow lights**: your lamps with a name, wattage and hours on per day, and which plants are under each one | Should | v1.x |
| ☐ | Moving a plant under or away from a grow light is recorded in its history, like a room change | Should | v1.x |
| ☐ | Filter by light, e.g. "under grow light" or "low light", to find a spot for a new plant | Could | v1.x |
| ☐ | Winter note: flag plants that may need a grow light when daylight gets short | Could | v1.x |

## 5. Pests & treatments

| ✓ | Feature | Priority | When |
|---|---|---|---|
| ☐ | **Pest cases** per plant: pest (thrips, spider mites, fungus gnats, mealybugs, scale, other), start date, status (active / monitoring / resolved) | Should | v1.x |
| ☐ | Treatment log on a case: what was used and when | Should | v1.x |
| ☐ | **Treatment recipes**, e.g. "Thrips spray: alcohol + demineralised water + a drop of dish soap". Each log entry saves the recipe as it was that day, so changing the recipe doesn't rewrite history | Should | v1.x |
| ☐ | Interval ("every 3–5 days") with "next treatment due" shown on Today | Should | v1.x |
| ☐ | Quarantine flag and a "Quarantine" filter; badge on the card | Should | v1.x |
| ☐ | Inspection log: "checked, no signs", so you can see how long a plant has been clean | Could | v1.x |
| ☐ | Pest overview: all active cases in one list | Could | v1.x |
| ☐ | **Sticky trap counts**: log how many pests a (blue/yellow) sticky trap caught each week; a small chart shows whether treatment is working | Should | v1.x |
| ☐ | **Check the neighbours**: opening a pest case lists other plants in the same room to inspect, with "last inspected" dates | Should | v1.x |
| ☐ | **Biological control log**: release dates for predatory mites and similar | Could | v1.x |

## 5b. Supplies & shopping

| ✓ | Feature | Priority | When |
|---|---|---|---|
| ☐ | **Supplies stock**: LECA, PON, perlite, sphagnum, fertilisers, sticky traps, alcohol, pots | Should | v1.x |
| ☐ | **Shopping list**: mark a supply as running low and it lands on a list to open in the shop | Should | v1.x |
| ☐ | Money spent on supplies, to see what the hobby costs | Could | v1.x |

## 6. Today screen

| ✓ | Feature | Priority | When |
|---|---|---|---|
| ☐ | Propagations that haven't been checked recently | Should | M5 |
| ☐ | Pest treatments due or overdue | Should | v1.x |
| ☐ | Care intervals due (if set) | Could | v1.x |
| ☐ | Recent activity feed | Could | M5 |
| ☐ | Counts: plants, active propagations, success rate this month | Could | v1.x |

## 7. Data, backup & settings

| ✓ | Feature | Priority | When |
|---|---|---|---|
| ✅ | Light / dark / match-system theme | Must | M1 |
| ✅ | All data on the device (IndexedDB), works offline, installable | Must | M1–M2 |
| ☐ | Export everything to a ZIP (data + photos) and import it again | Must | M5 |
| ☐ | Ask the browser for persistent storage; show storage used | Must | M5 |
| ☐ | Backup reminder ("last backup 30 days ago") | Should | M5 |
| ☐ | CSV export of plants and propagations for spreadsheets | Could | v1.x |
| ☐ | Units and date format settings | Could | v1.x |
| ☐ | Danish translation | Could | v1.x |
| ☐ | **"New version available" banner** with a Reload button, so the installed app never stays stuck on an old version | Must | M5 |
| ☐ | "What's new" page and app version in Settings | Could | v1.x |
| ☐ | **Species autocomplete from GBIF** (free botanical database, no API key): correct spelling for genus/species, plus family and native region | Could | v1.x |

## 7b. Phone conveniences

| ✓ | Feature | Priority | When |
|---|---|---|---|
| ☐ | **Home-screen shortcuts**: long-press the app icon for "Add plant", "Log care", "New propagation" | Could | v1.x |
| ☐ | **Badge on the app icon** with the number of things due today | Could | v1.x |
| ☐ | **Quick actions**: long-press a plant card to log watering or a note without opening it | Could | v1.x |

## 8. Sharing & later versions

| ✓ | Feature | Priority | When |
|---|---|---|---|
| ☐ | Share a plant card as an image (photo + name + "propagated from…") | Could | v1.x |
| ☐ | Plant-sitter sheet: a printable/shareable care list for someone watering while you're away | Could | v1.x |
| ☐ | Accounts and sync between devices (ASP.NET Core Web API, EF Core, Identity) | Later | v2 |
| ☐ | Public read-only collection page | Later | v2 |
| ☐ | Plant identification from a photo via an identification API (needs a backend to keep the API key secret) | Later | v2 |
| ☐ | Push notifications, widgets and a Google Play release (MAUI Blazor Hybrid) | Later | v3 |

## 9. Quality (not features, but part of each milestone)

- Unit tests for every Core rule (promote/split counts, stage changes, lineage, filters).
- Accessibility: labels on every input, visible focus, enough contrast in both themes, tap targets ≥ 44 px.
- Performance: the list stays quick with 200+ plants and 1000+ photos (thumbnails, lazy loading).
- Database migrations: schema changes upgrade existing data and never wipe it.
- Every PR builds and tests in CI; `main` always deploys.
- Component tests for Razor components with bUnit.
- End-to-end tests with Playwright in CI (add a plant, promote a propagation, export/import).
- Lighthouse audit in CI (PWA, accessibility, performance) with a badge in the README.

---

## Top picks from the second round of ideas

1. "New version available" banner: small, and needed for a PWA.
2. Import existing photos with their dates.
3. Sticky trap counts: useful for the thrips right now.
4. Supplies stock and shopping list.
5. Soil to semi-hydro transition tracker.

---

## Questions for you

1. **Care log vs pests: which first after M5?** I'd do pests first, since you're fighting thrips right now.
2. **Moisture meter:** what scale does yours use (1–10, or dry/moist/wet)?
3. **Semi-hydro:** do you want to track reservoir top-ups and flushes, or is "watered/fertilised" enough?
4. **Experiments:** would comparing your corm mediums side by side (success rate, days to root) be useful, or is that overkill?
5. **Anything missing?** Things you do with your plants that aren't on this list.
