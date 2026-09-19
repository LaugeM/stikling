# Stikling feature list

A working list of what Stikling could do, grouped by area. Each row has a priority from 1 to 10, a rough effort, and the milestone it shipped in or is aimed at. Tick items off as they ship, and change the numbers as the app gets used.

**Priority: 1 is next, 10 is furthest away.**

| | |
|---|---|
| **1–2** | The app is missing something it needs. Build these first. |
| **3–4** | Clearly worth building, and the payoff comes soon. |
| **5–6** | Worth building, but nothing is waiting on it. |
| **7–8** | Nice to have if there is time and interest. |
| **9** | Needs accounts and a backend (v2). |
| **10** | Needs a native app (v3). |

**Effort** is a guess at how much work each one is on top of what is already built.

| | |
|---|---|
| **S** | A field or two, or a calculation on data the app already stores. An evening. |
| **M** | A new screen, or a new kind of record with its own IndexedDB store. |
| **L** | A new area of the app, a bundled data set, or something that needs a library. |

**Milestones:** M1 scaffold · M2 plants · M3 photos & timeline · M4 propagations · M5 backup & Today · then v1.x releases.

---

## Next up

In the order I would build them. The care log is the only whole area that is still empty, and pests come next because thrips are the problem right now.

1. **Quick-log care** and logging several plants at once. The care log is the last unbuilt part of the daily routine.
2. **"Last watered / fertilised / flushed"** on each plant. This is what makes the care log worth keeping.
3. **Pest cases and a treatment log**, with the interval showing up on Today.
4. **Quarantine flag and tags.** Both are small, and tags cover "variegated", "for swap" and "rare" at the same time.
5. **Success rate and days to root per medium.** The data is already stored, so this is only a calculation and a small page.
6. **Import old photos with their dates**, so a plant's history can start before the app did.
7. **Pot suggestions** in the pot field, the same way rooms are already suggested.
8. **Correct or hide a timeline entry.** Left over from M3.

### Cheap ones to slot in whenever

These need no new storage and no new screens worth the name: counts on the Today screen, sort options and favourites on the plant list, purchase price, duplicating a plant, quick add from a pasted list of names, home screen shortcuts, an app version and a "what's new" page, and empty screens that say what to do next.

---

## 1. Plants

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Add, edit and delete plants: nickname, genus, species, cultivar, location, origin, date, source, medium, pot, notes | 1 | M | M2 |
| ✅ | Status: in collection, died, given away, sold. Gone plants are hidden but keep their history | 1 | S | M2 |
| ✅ | Search on all name fields; filter by status and room | 1 | S | M2 |
| ✅ | Parent plant and offspring, "Add offspring" | 1 | M | M2 |
| ✅ | Cover photo on cards and the plant page | 1 | S | M3 |
| ✅ | Add a photo while adding the plant, not only afterwards. The first one becomes the cover | 2 | S | M4 |
| ✅ | Tabs on the plant page: **Info**, **History** (timeline), **Props** (propagations taken from it) | 1 | M | M3–M4 |
| ☐ | Tags (e.g. "variegated", "rare", "quarantine", "for swap") and a tag filter | 3 | S | v1.x |
| ☐ | Select several plants at once: change status, move to a room, add a note to all | 4 | M | v1.x |
| ☐ | **Dormancy status**: e.g. alocasias dropping leaves in winter. Pauses "check on it" reminders and keeps the plant from looking dead | 4 | S | v1.x |
| ☐ | Sort options: name, newest, room, last activity | 5 | S | v1.x |
| ☐ | **Quick add**: paste or type a list of names ("Alocasia zebrina, Monstera deliciosa, …") to add many plants at once | 5 | S | v1.x |
| ☐ | Species autocomplete beyond genus (a bundled list of common houseplant species and cultivars) | 5 | M | v1.x |
| ☐ | **Soil to semi-hydro transition tracker**: mark a plant as transitioning, with a checklist (roots washed, first new water root, first new leaf) and a warning if nothing happens after a set number of weeks | 5 | M | v1.x |
| ☐ | **Problem log**: yellow leaves, root rot, sunburn, crispy edges. What you saw, what you did, and whether it helped | 5 | M | v1.x |
| ☐ | Favourites / pinned plants at the top of the list | 6 | S | v1.x |
| ☐ | Grid view with large photos as an alternative to the list | 6 | S | v1.x |
| ☐ | **Plant dictionary**: a large built-in list of plants and their varieties (genus, species, cultivars, common names) to search and pick from when adding a plant or propagation. Works offline | 6 | L | v1.x |
| ☐ | **Name autocomplete**: suggestions as you type in the genus, species and cultivar fields, from the dictionary, including hybrids and named varieties. Anything not on the list can still be typed | 6 | M | v1.x |
| ☐ | **Leaf log for variegated plants**: each new leaf with a photo and a variegation rating (low / medium / high / reverted), so reverting plants are spotted early | 6 | M | v1.x |
| ☐ | Duplicate a plant (e.g. a second basil pot) | 7 | S | v1.x |
| ☐ | Purchase price per plant | 7 | S | v1.x |
| ☐ | Wishlist: plants you want, with notes on where to find them | 8 | M | v1.x |

## 2. Photos & timeline

Every plant and propagation gets its own history. Entries are only ever added, never overwritten, so the full history is kept.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Take a photo with the camera or pick from the gallery. Photos are resized and compressed on the phone (≈1600 px) with a thumbnail | 1 | L | M3 |
| ✅ | Photo timeline per plant and per propagation, newest first, tap to view full screen | 1 | M | M3 |
| ✅ | Notes on the timeline ("new leaf unfurling", "roots 2 cm") | 1 | S | M3 |
| ✅ | Automatic timeline entries for changes: status, medium/pot ("moved to semi-hydro"), room, stage | 1 | M | M3 |
| ✅ | Choose the cover photo | 1 | S | M3 |
| ☐ | Correct or hide a timeline entry by adding a new entry, keeping the original | 3 | S | v1.x |
| ☐ | **Import existing photos with their dates**: pick old photos from the gallery and each is placed on the timeline by the date it was taken (EXIF), so a plant's history starts from day one | 3 | M | v1.x |
| ☐ | Swipe through photos side by side to compare growth | 5 | M | v1.x |
| ☐ | **Voice notes**: a microphone button on note fields using the phone's speech-to-text | 5 | S | v1.x |
| ☐ | Measurements on an entry: number of leaves, height, root length | 6 | S | v1.x |
| ☐ | **Share into Stikling**: the installed app appears in Android's share menu, so a photo from the gallery can go straight to a plant (Web Share Target) | 7 | M | v1.x |
| ☐ | Time-lapse / before-and-after image from a plant's photos | 8 | L | v1.x |
| ☐ | **Ghost overlay** when taking a photo: a faint copy of the previous photo to line up the same angle every time | 8 | L | v1.x |

## 3. Propagations

This is the core of the app: cuttings, corms, seeds and divisions, linked to their parent.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | One-tap **Propagate** on a plant page, which creates a propagation linked to that plant | 1 | M | M4 |
| ✅ | Type: cutting, corm, offset/pup, seed, division, air layer, leaf | 1 | S | M4 |
| ✅ | Medium: water, perlite, sphagnum, LECA, PON, soil, "humidity dome", other | 1 | S | M4 |
| ✅ | Stages: started, rooting, rooted and done (or failed). The date of each stage change is saved | 1 | M | M4 |
| ✅ | Batches with a count ("3 corms in LECA"); mark some as failed | 1 | M | M4 |
| ✅ | **Promote to plant**: turn 1..n units of a batch into plants and keep the lineage | 1 | M | M4 |
| ✅ | Propagations list grouped by stage, with cards like "3× corm · LECA · day 24" | 1 | M | M4 |
| ✅ | Propagations without a parent plant (bought seeds, e.g. the bay laurel) | 1 | S | M4 |
| ✅ | "Check on it" reminder in the app: a propagation not looked at for X days shows on Today | 2 | S | M5 |
| ☐ | Success rate and average days-to-root per medium and per type | 3 | S | v1.x |
| ☐ | **Experiments**: group propagations started together to compare mediums (your corm test: perlite vs sphagnum vs LECA vs humidity dome) | 4 | M | v1.x |
| ☐ | Seed germination: sown count, germinated count and dates | 5 | S | v1.x |
| ☐ | **Milestones** on a propagation: first root, first leaf, corm size. Gives the medium experiments concrete dates to compare | 5 | S | v1.x |
| ☐ | Lineage tree view (family tree across generations) | 6 | M | v1.x |
| ☐ | **Given away / swapped**: record who got a cutting, so the family tree extends to friends' plants | 7 | S | v1.x |
| ☐ | Printable QR labels for jars and pots; scanning one opens the plant or propagation | 8 | L | v1.x |
| ☐ | **"Available for swap" list**: mark propagations as available and share a simple list or image before a plant swap | 8 | M | v1.x |

## 4. Care log

Built around your routine: moisture meter instead of a watering schedule, semi-hydro reservoirs, and different fertilisers for different plants. Nothing here is built yet, and it is the last part of the daily routine the app can't hold.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ☐ | Quick-log care: watered, fertilised, reservoir topped up, flushed, repotted, pruned, rotated, cleaned leaves, harvested | 1 | L | v1.x |
| ☐ | Log several plants at once ("fertilised all semi-hydro plants") | 2 | M | v1.x |
| ☐ | "Last watered / fertilised / flushed" summary on each plant | 2 | S | v1.x |
| ☐ | **Moisture meter reading** (1–10) as a log type, with the latest reading shown on the card | 3 | S | v1.x |
| ☐ | **Products**: your fertilisers (the hydro one, the general one, the herb one), each with a default dose | 4 | M | v1.x |
| ☐ | Which product and dose was used, e.g. "Hydro fertiliser, 2 ml/L" | 4 | S | v1.x |
| ☐ | Water type: tap, demineralised, rain | 6 | S | v1.x |
| ☐ | Semi-hydro details: reservoir level, flush interval, last flush | 6 | M | v1.x |
| ☐ | Optional care intervals per plant ("fertilise every 2 weeks") that show up on Today | 6 | M | v1.x |
| ☐ | **Nutrient dose calculator**: reservoir size × the product's dose per litre = ml to add | 7 | S | v1.x |
| ☐ | Harvest log for herbs (basil, parsley): date and amount | 7 | S | v1.x |
| ☐ | Flowering log (orchid, peace lily): spike started, blooming, done | 7 | S | v1.x |

## 4b. Light

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ☐ | Light level per plant: low, medium, bright indirect, some direct sun | 5 | S | v1.x |
| ☐ | **Grow lights**: your lamps with a name, wattage and hours on per day, and which plants are under each one | 6 | M | v1.x |
| ☐ | Moving a plant under or away from a grow light is recorded in its history, like a room change | 6 | S | v1.x |
| ☐ | Window direction for each room (north, east, south, west), so plants in a room show what light they get | 7 | M | v1.x |
| ☐ | Filter by light, e.g. "under grow light" or "low light", to find a spot for a new plant | 7 | S | v1.x |
| ☐ | Winter note: flag plants that may need a grow light when daylight gets short | 8 | M | v1.x |

## 5. Pests & treatments

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ☐ | **Pest cases** per plant: pest (thrips, spider mites, fungus gnats, mealybugs, scale, other), start date, status (active / monitoring / resolved) | 2 | M | v1.x |
| ☐ | Treatment log on a case: what was used and when | 2 | S | v1.x |
| ☐ | Interval ("every 3–5 days") with "next treatment due" shown on Today | 3 | S | v1.x |
| ☐ | Quarantine flag and a "Quarantine" filter; badge on the card | 3 | S | v1.x |
| ☐ | **Treatment recipes**, e.g. "Thrips spray: alcohol + demineralised water + a drop of dish soap". Each log entry saves the recipe as it was that day, so changing the recipe doesn't rewrite history | 4 | M | v1.x |
| ☐ | Pest overview: all active cases in one list | 4 | S | v1.x |
| ☐ | **Sticky trap counts**: log how many pests a (blue/yellow) sticky trap caught each week; a small chart shows whether treatment is working | 4 | M | v1.x |
| ☐ | Inspection log: "checked, no signs", so you can see how long a plant has been clean | 5 | S | v1.x |
| ☐ | **Check the neighbours**: opening a pest case lists other plants in the same room to inspect, with "last inspected" dates | 5 | S | v1.x |
| ☐ | **Biological control log**: release dates for predatory mites and similar | 8 | S | v1.x |

## 5b. Supplies & shopping

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ☐ | **Pot suggestions**: the pot field suggests pots you've already used, so the same one isn't typed differently every time. The same for a propagation's container | 3 | S | v1.x |
| ☐ | **Supplies stock**: LECA, PON, perlite, sphagnum, fertilisers, sticky traps, alcohol, pots | 5 | M | v1.x |
| ☐ | **Shopping list**: mark a supply as running low and it lands on a list to open in the shop | 5 | S | v1.x |
| ☐ | **Your pots**: register the pots you own by kind (nursery pot, self-watering, terracotta, glass jar, net pot, humidity box) and size, then pick one from a list when adding a plant or potting up | 6 | M | v1.x |
| ☐ | See which of your pots are in use and which are free, so it's clear what's available before repotting | 7 | S | v1.x |
| ☐ | Default medium per pot kind, e.g. a net pot suggests LECA, so potting up is one tap less | 8 | S | v1.x |
| ☐ | Money spent on supplies, to see what the hobby costs | 8 | S | v1.x |

## 5c. Soil mixes

Most people mix their own soil instead of using it straight from the bag. This is for keeping track of what is actually in it. The first five rows are really one feature and should be built together.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ☐ | **Your mixes**: save a mix under a name ("aroid mix", "seedling mix", "cactus mix") | 5 | M | v1.x |
| ☐ | What a mix is made of, as percentages: potting soil, perlite, LECA, bark, pumice, coco coir, sphagnum, worm castings, sand, charcoal. The app adds them up and says when they don't reach 100% | 5 | S | v1.x |
| ☐ | Add your own ingredient if something isn't on the list | 5 | S | v1.x |
| ☐ | Pick a mix when adding a plant or potting up, instead of typing the medium by hand. The plant page shows which mix it's in | 5 | M | v1.x |
| ☐ | Editing a mix doesn't rewrite history: a plant keeps the recipe as it was on the day it was potted | 5 | M | v1.x |
| ☐ | See which plants are in a given mix, so a mix that isn't working shows up | 7 | S | v1.x |
| ☐ | Use a mix on a propagation too, for the ones that go straight into soil | 7 | S | v1.x |
| ☐ | Batch calculator: pick a mix and a volume, and get how much of each ingredient to measure out | 7 | S | v1.x |
| ☐ | Mixing a batch takes the ingredients off the supplies stock | 8 | M | v1.x |

## 6. Today screen

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Propagations that haven't been checked recently | 2 | M | M5 |
| ✅ | Recent activity feed | 5 | S | M5 |
| ☐ | Pest treatments due or overdue | 3 | S | v1.x |
| ☐ | Counts: plants, active propagations, success rate this month | 4 | S | v1.x |
| ☐ | Care intervals due (if set) | 6 | S | v1.x |

## 7. Data, backup & settings

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Light / dark / match-system theme | 1 | S | M1 |
| ✅ | All data on the device (IndexedDB), works offline, installable | 1 | L | M1–M2 |
| ✅ | Export everything to a ZIP (data + photos) and import it again | 1 | L | M5 |
| ✅ | Ask the browser for persistent storage; show storage used | 1 | S | M5 |
| ✅ | Backup reminder ("last backup 30 days ago") | 2 | S | M5 |
| ✅ | **"New version available" banner** with a Reload button, so the installed app never stays stuck on an old version | 1 | S | M5 |
| ☐ | "What's new" page and app version in Settings | 5 | S | v1.x |
| ☐ | CSV export of plants and propagations for spreadsheets | 6 | S | v1.x |
| ☐ | Units and date format settings | 7 | M | v1.x |
| ☐ | Danish translation | 7 | L | v1.x |
| ☐ | **Species autocomplete from GBIF** (free botanical database, no API key): correct spelling for genus/species, plus family and native region | 7 | M | v1.x |

## 7b. Phone conveniences

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ☐ | **Home-screen shortcuts**: long-press the app icon for "Add plant", "Log care", "New propagation" | 5 | S | v1.x |
| ☐ | **Quick actions**: long-press a plant card to log watering or a note without opening it | 6 | M | v1.x |
| ☐ | **Badge on the app icon** with the number of things due today | 7 | S | v1.x |

## 7c. Help & first visit

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ☐ | **FAQ page**: how the app stores everything on your device, what happens if you clear the browser, how to back up, how propagation and lineage work, how to install it on the phone | 4 | S | v1.x |
| ☐ | Empty screens that say what to do next instead of just "nothing here" | 4 | S | v1.x |
| ☐ | **Guided tour on the first visit**: a short walk through the app (plants, propagations, photos, backup) that can be skipped | 6 | M | v1.x |
| ☐ | Start the tour again from Settings whenever you want | 6 | S | v1.x |
| ☐ | Small info buttons next to the less obvious things, e.g. what "Pot up" does and what the stages mean | 6 | S | v1.x |
| ☐ | A demo plant and propagation that can be loaded and removed again, so the app can be tried out without adding real plants | 7 | M | v1.x |

## 8. Sharing & later versions

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ☐ | Share a plant card as an image (photo + name + "propagated from…") | 7 | M | v1.x |
| ☐ | Plant-sitter sheet: a printable/shareable care list for someone watering while you're away | 7 | M | v1.x |
| ☐ | Accounts and sync between devices (ASP.NET Core Web API, EF Core, Identity) | 9 | L | v2 |
| ☐ | Public read-only collection page | 9 | L | v2 |
| ☐ | Plant identification from a photo via an identification API (needs a backend to keep the API key secret) | 9 | L | v2 |
| ☐ | Push notifications, widgets and a Google Play release (MAUI Blazor Hybrid) | 10 | L | v3 |

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

## Questions for you

1. **Moisture meter:** what scale does yours use (1–10, or dry/moist/wet)?
2. **Semi-hydro:** do you want to track reservoir top-ups and flushes, or is "watered/fertilised" enough?
3. **Experiments:** would comparing your corm mediums side by side (success rate, days to root) be useful, or is that overkill? The success rate table at priority 3 gets part of the way there on its own.
4. **Anything missing?** Things you do with your plants that aren't on this list.
