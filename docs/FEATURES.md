# Stikling feature list

A working list of what Stikling could do, grouped by area. Each row has a priority from 1 to 10, a rough effort, and the milestone it shipped in or is aimed at. Tick items off as they ship, and change the numbers as the app gets used.

**Status** is the first column on every row.

| | |
|---|---|
| ✅ | Built and in the app. |
| ◐ | Partly built. Some of what the row describes already works, and the row says what is still missing. |
| ☐ | Not started. |

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

In the order I would build them.

1. **Checking plants outside a case**: an inspection logged on a single plant and the neighbours to check when a case opens. That finishes the pest section apart from the biological control log.
2. **Growing season**, so the app asks about dormancy when a plant's resting time comes round. It builds on dormancy, which I just finished.
3. **Problem log**: what you saw on a plant, what you did, and whether it helped.
4. **Soil to semi-hydro transition tracker**, with its checklist and the warning when nothing happens.
5. **Supplies stock and the shopping list**, built together since the list comes from marking a supply as running low.
6. **Import from a spreadsheet**, for someone who already keeps a list of their plants somewhere else.
7. **Comparing photos side by side, and voice notes.**
8. **A side menu on wide screens**, since on a computer the app sits in a narrow column with a lot of empty space around it.

### Cheap ones to slot in whenever

These need no new storage and no new screens worth the name: purchase price, a "Log care" home screen shortcut, and an app version and a "What's new" page in Settings.

---

## 1. Plants

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Add, edit and delete plants: nickname, genus, species, cultivar, location, origin, date, source, medium, pot, notes | 1 | M | M2 |
| ✅ | Status: in collection, died, given away, sold. Gone plants are hidden but keep their history | 1 | S | M2 |
| ✅ | Search on all name fields; filter by status and room | 1 | S | M2 |
| ✅ | Pick a room from the ones already used instead of typing it again, so the same room never gets two names | 1 | S | v1.x |
| ✅ | Spots inside a room, e.g. "Living room / On top of the PC". Filtering by the room includes its spots | 2 | M | v1.x |
| ✅ | Parent plant and offspring, "Add offspring" | 1 | M | M2 |
| ✅ | Cover photo on cards and the plant page | 1 | S | M3 |
| ✅ | Add a photo while adding the plant, not only afterwards. The newest one becomes the cover | 2 | S | M4 |
| ✅ | Tabs on the plant page: **Info**, **History** (timeline), **Props** (propagations taken from it) | 1 | M | M3–M4 |
| ✅ | A loose date for when you got a plant: a whole day, just the month, or just the year, so a date you only half remember doesn't have to be guessed at | 3 | S | v1.x |
| ✅ | Tags (e.g. "variegated", "rare", "for swap") and a tag filter. Picking several tags narrows the list to plants that have all of them, the search finds tags too, and a tag on a plant's page opens the list filtered by it | 3 | S | v1.x |
| ✅ | Rename or merge a tag everywhere at once, the way rooms can be | 6 | S | v1.x |
| ✅ | Add rooms, spots and tags from the Rooms and tags page, so they can be set up before any plant uses them. An empty room or spot can be removed there again | 4 | S | v1.x |
| ✅ | Select several plants at once: change status, log care, add a note or tags to all, or move them all to a room | 4 | M | v1.x |
| ✅ | **Dormancy status** for plants and propagations, e.g. an alocasia dropping its leaves in winter or a corm sitting still. A badge and the day it went dormant show it's resting rather than dead, the plant list has a "Dormant" filter, and going dormant and waking up are recorded on the history. A dormant propagation doesn't come up on Today to be checked on. Dormancy ends by itself when a plant dies or leaves, or a propagation finishes | 4 | S | v1.x |
| ☐ | **Growing season**: mark a plant as growing in summer or in winter. When its usual resting time comes round, the app asks whether it has gone dormant | 5 | S | v1.x |
| ✅ | Sort options: name, newest, room, last activity | 5 | S | v1.x |
| ✅ | **Quick add**: paste or type a list of names ("Alocasia zebrina, Monstera deliciosa, …") to add many plants at once. Each name is matched against the plant dictionary, and anything else on the line goes into the notes | 5 | S | v1.x |
| ☐ | **Soil to semi-hydro transition tracker**: mark a plant as transitioning, with a checklist (roots washed, first new water root, first new leaf) and a warning if nothing happens after a set number of weeks | 5 | M | v1.x |
| ☐ | **Problem log**: yellow leaves, root rot, sunburn, crispy edges. What you saw, what you did, and whether it helped | 5 | M | v1.x |
| ✅ | **Already have it?** Adding a plant with the same name as one you have says so and links to it, so a second one is added on purpose | 5 | S | v1.x |
| ✅ | Favourites / pinned plants at the top of the list | 6 | S | v1.x |
| ☐ | Grid view with large photos as an alternative to the list | 6 | S | v1.x |
| ☐ | Group the plant list by genus, with an A to Z jump for long lists | 6 | S | v1.x |
| ☐ | **Collection numbers**: an optional short number per plant, e.g. 117, to write on the pot. Its propagations get 117a, 117b and so on, so a label on a jar says where the cutting came from | 6 | M | v1.x |
| ☐ | **Sellers**: where plants were bought as a list instead of typed out each time, with what was paid, the state the plant arrived in, and how the plants from each seller have done since | 6 | M | v1.x |
| ◐ | **Why it died**: an optional note on the cause when a plant is marked as died, and a list of the plants that died and why, to look over before buying the same plant again. The note is built and shows on the plant and on its card under Gone; a list of only the plants that died is missing | 6 | S | v1.x |
| ◐ | **Plant dictionary**: a large built-in list of plants and their varieties (genus, species, cultivars, common names) to search and pick from when adding a plant or propagation. Works offline. Built for the aroids (Araceae): every genus and species from Kew's checklist, the old names, and a starter list of cultivars. Other families, common names, family and native region, and a page to browse it aren't built. See `tools/plant-names` | 6 | L | v1.x |
| ✅ | **Name autocomplete**: suggestions as you type in the genus, species and cultivar fields, from the dictionary and the names already on your plants and propagations, including hybrids and named varieties. An old name offers the one it goes by now, and a cultivar fills in its genus and species. Anything not on the list can still be typed | 6 | M | v1.x |
| ☐ | **Leaf log for variegated plants**: each new leaf with a photo and a variegation rating (low / medium / high / reverted), so reverting plants are spotted early | 6 | M | v1.x |
| ☐ | **Wishlist**: plants you want, with notes on where to find them what you liked about them, and the most you would pay. One search covers both the wishlist and your plants, so in a shop or at a swap it's quick to see whether you already have something or have been looking for it | 6 | M | v1.x |
| ✅ | Duplicate a plant (e.g. a second basil pot): "Add another like this" opens the add form filled in from it | 7 | S | v1.x |
| ☐ | Purchase price per plant | 7 | S | v1.x |
| ✅ | What a plant was sold or traded for: the price, or the plant you got back | 7 | S | v1.x |
| ☐ | **Your own fields**: add a field the app doesn't have, e.g. clone name, awards or humidity, and fill it in on any plant. Nothing extra shows until you add one | 7 | M | v1.x |
| ☐ | **Home map**: a simple plan of each room with the plants placed where they stand, to see what is where and to go round them in order | 7 | L | v1.x |
| ☐ | **Toxic to pets?** Whether a plant is harmful to cats, dogs and people, shown on the plant from the plant dictionary. Only taken from a source that can be named on the plant, and a plant with nothing known says that rather than looking safe | 7 | L | v1.x |
| ☐ | Filter and sort the plant list by your own fields, and give a field a fixed set of choices so it can be set on several plants at once | 8 | S | v1.x |
| ☐ | **Symptom check**: tick what you see on a plant (yellow leaves, brown tips, drooping, spots), how wet the soil is, and anything that changed lately, and get the few likely causes with what to check next and the source each one comes from. The app can't see the plant, so it only ever says what to look at, never to water or treat, and says so when the answers don't point anywhere. What you see can go straight into the problem log | 8 | L | v1.x |

## 2. Photos & timeline

Every plant and propagation gets its own history. Entries are only ever added, and a corrected note or photo keeps the version it replaced, so the full history is kept.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Take a photo with the camera or pick from the gallery. Photos are resized and compressed on the phone (≈1600 px, WebP, or JPEG where the browser can't make WebP) with a thumbnail | 1 | L | M3 |
| ✅ | Photo timeline per plant and per propagation, newest first, tap to view full screen | 1 | M | M3 |
| ✅ | Notes on the timeline ("new leaf unfurling", "roots 2 cm") | 1 | S | M3 |
| ✅ | Automatic timeline entries for changes: status, medium/pot ("moved to semi-hydro"), room, stage | 1 | M | M3 |
| ✅ | Choose the cover photo | 1 | S | M3 |
| ✅ | Correct a timeline entry: fix the text or the date of a note or a photo, and its photos move with it. The earlier versions are kept and shown under "edited". Automatic entries are fixed where they come from, and any entry can be deleted | 3 | S | v1.x |
| ✅ | **Import existing photos with their dates**: pick old photos from the gallery and each lands on the day it was taken, so a plant's history starts from day one. The date comes from the photo itself (EXIF), then from a date in the file name, then from the file. Photos from the same day share an entry, a note keeps the photos picked with it together, and the app says how many photos had no date of their own so they can be checked | 3 | M | v1.x |
| ☐ | Swipe through photos side by side to compare growth | 5 | M | v1.x |
| ☐ | **Voice notes**: a microphone button on note fields using the phone's speech-to-text | 5 | S | v1.x |
| ☐ | Measurements on an entry: number of leaves, height, root length | 6 | S | v1.x |
| ☐ | **Growth curve**: a small chart of a plant's or propagation's measurements over time, once there are a few to draw | 7 | S | v1.x |
| ☐ | **Share into Stikling**: the installed app appears in Android's share menu, so a photo from the gallery can go straight to a plant (Web Share Target) | 7 | M | v1.x |
| ☐ | Time-lapse / before-and-after image from a plant's photos | 8 | L | v1.x |
| ☐ | **Ghost overlay** when taking a photo: a faint copy of the previous photo to line up the same angle every time | 8 | L | v1.x |

## 3. Propagations

This is the core of the app: cuttings, corms, seeds and divisions, linked to their parent.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | One-tap **Propagate** on a plant page, which creates a propagation linked to that plant | 1 | M | M4 |
| ✅ | Type: cutting, corm, offset/pup, seed, division, air layer, leaf | 1 | S | M4 |
| ✅ | Medium: water, perlite, sphagnum, LECA, PON, soil, corm riser, other | 1 | S | M4 |
| ✅ | Stages: started, rooting, rooted and done (or failed). The date of each stage change is saved | 1 | M | M4 |
| ✅ | Batches with a count ("3 corms in LECA"); mark some as failed | 1 | M | M4 |
| ✅ | **Promote to plant**: turn 1..n units of a batch into plants and keep the lineage | 1 | M | M4 |
| ✅ | Propagations list grouped by stage, with cards like "3× corm · LECA · day 24" | 1 | M | M4 |
| ✅ | Propagations without a parent plant (bought seeds, e.g. the bay laurel) | 1 | S | M4 |
| ✅ | "Check on it" reminder in the app: a propagation not looked at for X days shows on Today | 2 | S | M5 |
| ✅ | Success rate and average days-to-root per medium and per type, on a results page linked from Propagations. Success is counted in units, and a unit has made it once it's potted up or its batch has rooted | 3 | S | v1.x |
| ✅ | **Experiments**: group propagations started together to compare mediums (the corm test: perlite vs sphagnum vs LECA vs corm riser) | 4 | M | v1.x |
| ✅ | **On time or slow**: a propagation that is still rooting says whether it is on time or slower than usual, compared with your own average days to root for the same type and medium. Only shown once there are at least 3 rooted batches to compare with, and the list cards only mention it when it's slow | 4 | S | v1.x |
| ✅ | Seed germination: sown count, germinated count and dates | 5 | S | v1.x |
| ✅ | **Rooting aids**: what was used to help a propagation root, e.g. rooting powder or gel, cinnamon, or a bag or dome for humidity. The results page compares each one with the batches that went without it | 5 | S | v1.x |
| ✅ | **Milestones** on a propagation: first root, first leaf, corm size. Gives the medium experiments concrete dates to compare. First root, first leaf and corm size are built and show in experiments | 5 | S | v1.x |
| ☐ | Lineage tree view (family tree across generations) | 6 | M | v1.x |
| ✅ | **Given away / swapped**: record who got a cutting, so the family tree extends to friends' plants | 7 | S | v1.x |
| ☐ | Printable QR labels for jars and pots; scanning one opens the plant or propagation | 8 | L | v1.x |
| ☐ | **"Available for swap" list**: mark propagations as available and share a simple list or image before a plant swap | 8 | M | v1.x |

## 3b. Crossbreeding

For people who pollinate their own plants and raise the seedlings. A cross is two parents, a date and a method, and then a long wait, so most of the value is in writing it down on the day it happens. A plant only has one parent in the app today, so holding on to both is the real work in this section.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ☐ | **Crosses**: record a pollination with the pod parent, the pollen parent and the date | 7 | M | v1.x |
| ☐ | How it was done: brush or by hand, fresh or stored pollen, whether the flower was bagged, and a note for anything else | 7 | S | v1.x |
| ☐ | Outcome: whether it took, when the seed was ripe, how much there was, and when it was sown | 7 | S | v1.x |
| ☐ | Seedlings from a cross keep both parents, so the family tree shows where a hybrid came from | 7 | M | v1.x |
| ☐ | A working name for the cross, used on the seedlings until they earn a real one | 7 | S | v1.x |
| ☐ | Cross log: every cross in one list with what came of it, so the ones worth repeating stand out | 8 | S | v1.x |
| ☐ | Traits on parents and seedlings, e.g. dark new leaves or silver splash, to see which parent passes what on | 8 | S | v1.x |
| ☐ | **Pollen store**: what is in the freezer, from which plant and when it was collected, since pollen doesn't keep forever | 8 | M | v1.x |
| ☐ | A plant coming into flower shows on Today together with the pollen you have stored for it | 8 | M | v1.x |
| ☐ | Compare the seedlings from one cross side by side, to pick the keepers | 8 | M | v1.x |

## 4. Care log

Built around your routine: moisture meter instead of a watering schedule, semi-hydro reservoirs, and different fertilisers for different plants. The basics are in. Only the notable kinds (repotted, flushed, pruned) go on a plant's history, so watering doesn't bury the photos and notes.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Quick-log care: watered, fertilised, reservoir topped up, flushed, repotted, pruned, rotated, cleaned leaves, harvested | 1 | L | v1.x |
| ✅ | Log several plants at once ("fertilised all semi-hydro plants") | 2 | M | v1.x |
| ✅ | "Last watered / fertilised / flushed" summary on each plant | 2 | S | v1.x |
| ✅ | **Moisture meter reading** (1–10) as a log type, with the latest reading shown on the card | 3 | S | v1.x |
| ✅ | **Products**: your fertilisers (the hydro one, the general one, the herb one), each with a usual dose in ml, g or drops. The dose is typed the way the bottle puts it, e.g. 5 ml per 4 L or 1 ml per 500 ml, and is optional | 4 | M | v1.x |
| ✅ | Which products and doses went in on a feed or a top-up, e.g. "Hydro fertiliser, 2 ml/L". The dose starts at the product's usual one and can be changed for that time, and the entry keeps its own copy, so editing or deleting a product doesn't change what was logged | 4 | S | v1.x |
| ☐ | **Due a check with the meter**: from a plant's own readings, the app learns how many days it takes to dry out after watering and when it should be down at the reading you water at. That is 3 by default, the top of the red "dry" part on a 1 to 10 meter, and can be set higher for a plant kept moister, like a peace lily. It then asks you to check with the meter, never to water, and a reading that says it's still wet moves the next check back. Without readings it goes by the usual gap between your waterings. Plants in semi-hydro are left out. The plant page says roughly how long it usually takes to dry out, and Today lists the plants due a check, off until turned on in Settings | 5 | M | v1.x |
| ☐ | Water type: tap, demineralised, rain | 6 | S | v1.x |
| ☐ | Semi-hydro details: reservoir level, flush interval, last flush | 6 | M | v1.x |
| ☐ | Optional care intervals per plant ("fertilise every 2 weeks") that show up on Today | 6 | M | v1.x |
| ✅ | **Nutrient dose calculator**: fill in the water on a feed or a top-up, in litres or millilitres, and each product shows how much to add | 7 | S | v1.x |
| ☐ | Harvest log for herbs (basil, parsley): date and amount | 7 | S | v1.x |
| ☐ | Flowering log (orchid, peace lily): spike started, blooming, done | 7 | S | v1.x |

## 4b. Light and climate

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Light level per plant: low, medium, bright indirect, some direct sun | 5 | S | v1.x |
| ☐ | **Room climate**: temperature and humidity readings for a room or spot, e.g. from a hygrometer in a cabinet, so a plant shows the conditions it lives in | 6 | M | v1.x |
| ☐ | **Grow lights**: your lamps with a name, wattage and hours on per day, and which plants are under each one | 6 | M | v1.x |
| ☐ | Moving a plant under or away from a grow light is recorded in its history, like a room change | 6 | S | v1.x |
| ☐ | Window direction for each room (north, east, south, west), so plants in a room show what light they get | 7 | M | v1.x |
| ☐ | Filter by light, e.g. "under grow light" or "low light", to find a spot for a new plant | 7 | S | v1.x |
| ☐ | Winter note: flag plants that may need a grow light when daylight gets short | 8 | M | v1.x |
| ☐ | **Light check**: a few questions about a spot (hours of direct sun, how far it is from the window, whether anything is in the way) that suggest one of the light levels above for a plant standing there. It says it's a rough guess from your answers, and the level can still be picked by hand | 8 | S | v1.x |

## 5. Pests & treatments

A case covers a group of plants rather than one, because that is how treating works: you see mites
on one plant and spray the whole room. A case is either everywhere, one room, or a set of plants you
pick, and everywhere and a room are worked out from where the plants are now, so a plant moved in
while a case is open is covered too. Treatments are logged on the case, so one spray is one entry.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | **Pest cases** covering a group of plants: pest (spider mites, thrips, fungus gnats, mealybugs, scale, aphids, whitefly, other), start date, status (treating / watching / resolved) | 2 | M | v1.x |
| ✅ | Treatment log on a case: what was used and when | 2 | S | v1.x |
| ✅ | Interval ("every 3–5 days") with "next treatment due" shown on Today | 3 | S | v1.x |
| ✅ | A case being watched is due a check for pests once a week instead of a treatment. Checks are logged on the case, and "Found some" puts it back to treating | 3 | S | v1.x |
| ✅ | Quarantine flag with the day it started, a "Quarantine" filter and a badge on the card. Going in and coming out are recorded on the plant's history | 3 | S | v1.x |
| ✅ | A length for quarantine, e.g. two weeks. When it is up, Today says it is time to check the plant and let it out | 4 | S | v1.x |
| ✅ | **Treatment recipes**, e.g. "Alcohol spray: isopropyl alcohol and a drop of dish soap, topped up with water". The ingredients are typed, each with an optional amount like 250 ml per 1 L, and there's a note on how to use it. "Mix a batch" works out the amounts for any size of bottle. Logging a treatment offers your recipes to pick from, and the treatment saves the recipe as it was that day, so changing the recipe doesn't rewrite history. Recipes are under Supplies | 4 | M | v1.x |
| ✅ | Pest overview: all active cases in one list | 4 | S | v1.x |
| ✅ | **Sticky trap counts** on a pest case: count everything on a (blue/yellow) sticky trap and tick when a new one goes up, and the app works out how many were caught since last time. A small chart of the catch per week shows whether treatment is working. A count never moves the next treatment, but on a case being watched it counts as the weekly check, and new ones on the trap ask whether to start treating again | 4 | M | v1.x |
| ◐ | Inspection log: "checked, no signs", so you can see how long a plant has been clean. Checks are logged on a case being watched, but not on a single plant outside a case | 5 | S | v1.x |
| ☐ | **Check the neighbours**: opening a pest case lists other plants in the same room to inspect, with "last inspected" dates | 5 | S | v1.x |
| ☐ | **Biological control log**: release dates for predatory mites and similar | 8 | S | v1.x |

## 5b. Supplies & shopping

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | **Pot suggestions**: a plant's pot is picked from your pot library, on the plant form and when potting up. A propagation's container is typed by hand, because a jar or a humidity box isn't a pot | 3 | S | v1.x |
| ☐ | **Supplies stock**: LECA, PON, perlite, sphagnum, fertilisers, sticky traps, alcohol, pots | 5 | M | v1.x |
| ☐ | **Shopping list**: mark a supply as running low and it lands on a list to open in the shop | 5 | S | v1.x |
| ✅ | **Your pots**: the pots you own, by kind (nursery, outer, stands on its own), what they're made of, their measurements, whether they self-water, and how many you have. A plant points at a pot and, when it has one, an outer pot | 6 | M | v1.x |
| ✅ | See which of your pots are in use and which are free, so it's clear what's available before repotting. A plant that died frees its pots, and giving a plant away or selling it asks which of its pots went with it | 7 | S | v1.x |
| ☐ | **Does it fit?**: given a nursery pot, which of your free outer pots it would go in. Compares the tops, allows for a rim only having to clear the opening, and says when the bottom is the tighter measurement | 7 | M | v1.x |
| ✅ | Default medium per pot kind: picking a self-watering pot for a new plant or when potting up suggests LECA, so potting up is one tap less | 8 | S | v1.x |
| ☐ | Money spent on plants and supplies, per year, to see what the hobby costs | 8 | S | v1.x |

## 5c. Soil mixes

Most people mix their own soil instead of using it straight from the bag. This is for keeping track of what is actually in it, including the mixes you never measured.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | **Your mixes**: save a mix under a name ("aroid mix", "seedling mix", "cactus mix") | 5 | M | v1.x |
| ✅ | What a mix is made of, in the order you mix it, most of first. Amounts are optional and can be parts or percent, and percentages are added up with a note when they don't reach 100% | 5 | S | v1.x |
| ✅ | Add your own ingredient if something isn't on the list | 5 | S | v1.x |
| ✅ | Pick a mix when adding a plant or potting up, instead of typing the medium by hand. The plant page shows which mix it's in, and what's in it | 5 | M | v1.x |
| ◐ | Editing a mix that plants are already in says so first, and offers to save the edit as a new mix instead, dated so the versions read in order. A plant still points at the mix rather than keeping its own copy of the recipe, so a mix can also be marked as no longer mixed to keep it off the picker | 5 | M | v1.x |
| ✅ | The mixes list says how many plants are in each mix, and tapping the count shows those plants | 7 | S | v1.x |
| ✅ | Use a mix on a propagation too, for the ones that go straight into soil | 7 | S | v1.x |
| ✅ | Batch calculator: pick a mix and a volume, and get how much of each ingredient to measure out | 7 | S | v1.x |
| ☐ | **Batches**: mix a dated batch from a recipe and top it up as it runs low, so a plant can point at the batch it was actually potted in rather than the recipe | 8 | L | v1.x |
| ☐ | Mixing a batch takes the ingredients off the supplies stock | 8 | M | v1.x |

## 5d. Fertiliser mixes

Most feeds are more than one bottle. A fertiliser, then a rooting or growth stimulant, and often silica for the exotics so the leaf tips don't go brown. This is for saving what goes in the water and in what order, and builds on the products in the care log.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | **Your feeds**: save a mix of products under a name ("weekly aroid feed", "rooting water"), each product with its own dose, e.g. 5 ml per 4 L. The dose starts at the product's usual one | 5 | M | v1.x |
| ✅ | Several products in one feed. A product says what it is: fertiliser, rooting or growth stimulant, silica, cal-mag, pH up or down, or something else | 5 | S | v1.x |
| ✅ | The mix keeps the order the products go in the water, since silica has to go in first and be stirred before anything else. A note says so when silica isn't first or pH isn't last, without stopping the save | 5 | S | v1.x |
| ✅ | Log a feed by picking the mix and the amount of water. The care entry saves each product and dose as it was that day, so changing the mix doesn't rewrite history. The water is optional, and the doses can still be changed for that one time | 5 | M | v1.x |
| ✅ | Batch calculator: pick a mix and a water volume, and get how much of each product to measure out. On the feeds page, and on the care entry once the water is filled in | 6 | S | v1.x |
| ✅ | See which plants got a given feed and when, so a mix that isn't working shows up. The feeds list says how many plants have had each feed and when it was last given, and opens to the plants and their dates | 7 | S | v1.x |
| ☐ | A different mix for propagations, e.g. quarter strength with a rooting stimulant | 7 | S | v1.x |
| ✅ | EC and pH reading on a feed, for the semi-hydro reservoirs | 8 | S | v1.x |
| ☐ | Mixing a feed takes the products off the supplies stock | 8 | M | v1.x |

## 6. Today screen

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Propagations that haven't been checked recently | 2 | M | M5 |
| ✅ | Recent activity feed | 5 | S | M5 |
| ✅ | Pest treatments due or overdue, and checks due on cases being watched | 3 | S | v1.x |
| ✅ | Counts: plants, active propagations, success rate this month | 4 | S | v1.x |
| ✅ | **Needs attention**: flag a plant or propagation with a reason ("repot soon", "look closer", "change the water"), typed or picked from a few suggestions, and it stays on Today until it's marked done there or on its own page. Flags aren't written on the history, and one goes away by itself when the plant leaves or the propagation finishes | 4 | S | v1.x |
| ✅ | Put something on Today off until tomorrow, for 3 days or for a week, without logging anything: a propagation waiting to be looked at, a flag, or the photo reminder, which can also be skipped for the rest of the month. What's put off is remembered on the device, not in backups. Pest treatments stay until they're logged | 5 | S | v1.x |
| ✅ | **Photo reminder**: once a month, Today lists the plants and propagations that haven't had a photo taken that month, room by room, with a camera button on each that puts the photo straight on its history. Dormant ones are left out. Off until turned on in Settings | 5 | S | v1.x |
| ☐ | Plants due a check with the meter today, and the ones due in the next couple of days, if turned on | 5 | S | v1.x |
| ☐ | **A daily round**: Today suggests one room or spot to go through each day, working round the home over the week instead of everything at once. Off until turned on | 6 | S | v1.x |
| ☐ | Care intervals due (if set) | 6 | S | v1.x |
| ☐ | **The week ahead**: what is due over the next 7 days as well as today, grouped by day, with a way to mark a whole day as done. Most useful once care intervals exist | 7 | S | v1.x |

## 7. Data, backup & settings

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Light / dark / match-system theme | 1 | S | M1 |
| ✅ | All data on the device (IndexedDB), works offline, installable | 1 | L | M1–M2 |
| ✅ | Export everything to a ZIP (data + photos) and import it again | 1 | L | M5 |
| ✅ | Ask the browser for persistent storage; show storage used | 1 | S | M5 |
| ✅ | Backup reminder ("last backup 30 days ago") | 2 | S | M5 |
| ✅ | Rooms and tags page in Settings: rename a room or a spot everywhere at once, and merge two names for the same room | 2 | M | v1.x |
| ✅ | **Settings behind a gear** at the top, as a short list: the account, the theme and photo reminder, then pages for rooms and tags, backup and storage, and the about pages. The fifth tab is **Supplies**, with the pots, soil mixes, products, feeds and treatment recipes, each saying what it holds or what it's for | 2 | M | v2 |
| ✅ | **"New version available" banner** with a Reload button, so the installed app never stays stuck on an old version | 1 | S | M5 |
| ☐ | "What's new" page and app version in Settings. Entries are written by hand in plain language for the people using the app, not generated from commit or pull request titles | 5 | S | v1.x |
| ☐ | **Import from a spreadsheet**: a CSV of plants from Excel or Google Sheets, with each column matched to a field, so a list kept somewhere else doesn't have to be typed in again | 5 | M | v1.x |
| ☐ | **Side menu on wide screens**: on a computer or a tablet held sideways, the tabs move to a menu down the side and the pages use the width instead of keeping the phone's narrow column. Phones keep the tabs at the bottom | 5 | M | v1.x |
| ✅ | CSV export of plants and propagations for spreadsheets | 6 | S | v1.x |
| ☐ | Units and date format settings | 7 | M | v1.x |
| ☐ | Danish translation | 7 | L | v1.x |

## 7b. Phone conveniences

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ◐ | **Home-screen shortcuts**: long-press the app icon for "Add plant", "Log care", "New propagation". Add plant, New propagation and Pest cases are there; Log care is missing since there is no page for it yet | 5 | S | v1.x |
| ☐ | **Quick actions**: long-press a plant card to log watering or a note without opening it | 6 | M | v1.x |
| ✅ | **Badge on the app icon** with the number of things due today | 7 | S | v1.x |

## 7c. Help & first visit

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | **Help page** with short questions and answers: how the app stores everything on your device, what happens if you clear the browser, how to back up, how propagation and lineage work, how to install it on the phone, and a few answers for each part of the app. Each question folds open, and the topics at the top jump to their section. Opened from Settings and from the first screens someone new sees | 4 | S | v1.x |
| ✅ | Empty screens that say what to do next instead of just "nothing here": the first screen on Today says what the app is for and offers a plant or a propagation, a list with nothing matching offers to clear the search and filters, and the ones that use a word like feed or propagation say what it means | 4 | S | v1.x |
| ☐ | **Guided tour on the first visit**: a short walk through the app (plants, propagations, photos, backup) that can be skipped | 6 | M | v1.x |
| ☐ | Start the tour again from Settings whenever you want | 6 | S | v1.x |
| ☐ | Small info buttons next to the less obvious things, e.g. what "Pot up" does and what the stages mean | 6 | S | v1.x |
| ☐ | A demo plant and propagation that can be loaded and removed again, so the app can be tried out without adding real plants | 7 | M | v1.x |

## 7d. Guides & references

Questions come up that the app can't answer from your own data: how long a cutting should take to root, what thrips damage looks like, how much perlite goes in a mix. There are two ways to handle it. Point at a good page somewhere else, or keep a short guide inside the app. Linking is much less work and there is nothing to maintain, so that comes first. Anything written into the app has to be kept correct afterwards.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ☐ | **Reference links**: a short, checked list of outside pages (care by genus, pest identification, semi-hydro, soil), opened in the browser | 7 | S | v1.x |
| ☐ | Links where they apply: a pest case links to a page about that pest, a plant links to a care page for its genus | 7 | M | v1.x |
| ☐ | **Built-in guides** that work offline: taking a cutting, converting to semi-hydro, treating thrips, mixing soil. Short, part of the app, no backend and no network | 8 | L | v1.x |
| ☐ | A guide opens from the place it is about, e.g. the stages on a propagation link to the cutting guide | 8 | S | v1.x |

## 8. Sharing & later versions

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ☐ | Share your plant list or wishlist as text, e.g. to send to a friend or take to a swap | 6 | S | v1.x |
| ☐ | Share a plant card as an image (photo + name + "propagated from…") | 7 | M | v1.x |
| ☐ | Plant-sitter sheet: a printable/shareable care list for someone watering while you're away | 7 | M | v1.x |
| ✅ | Accounts and sync between devices (ASP.NET Core Web API, EF Core, sign-in through Clerk). The API checks Clerk sign-ins and has people, collections and members with roles, and the app can sign in and out from its Account page in Settings. Records, settings and photos sync between devices when signed in. Every device keeps all the thumbnails and fetches a full-size photo when it's opened, and each account has 1 GB for photos on the server. The Account page shows when it last synced and, once it's nearly full, how much photo space is used, a dot on the settings gear shows a sync problem, and signing out asks whether to keep the data on the device. Delete account, at the bottom of the Account page, erases everything on the server and the sign-in, and Download your data gives a ZIP of everything the server and Clerk hold about the person. When something is deleted, the server keeps only its id and dates. The app is at stikling.app, with the API on Azure Container Apps, Azure SQL and Blob Storage, and a privacy page | 9 | L | v2 |
| ☐ | Public read-only collection page | 9 | L | v2 |
| ☐ | Plant identification from a photo via an identification API (needs a backend to keep the API key secret). Pl@ntNet's API is the free one: up to 500 identifications a day for non-commercial use, and it doesn't keep the photos. plant.id only gives 100 free identifications before it charges, and iNaturalist doesn't open its full identification model to other apps | 9 | L | v2 |
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

