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
| **9** | Needs more of the accounts and server side built first (v2). |
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

In the order I would build them, with the ones that belong together built together. The first one is for reaching new people: what someone sees when they find the app from a post on TikTok, Instagram or Reddit. Danish Facebook groups wait until the Danish translation is in.

The people on Reddit who track their plants at all are mostly collectors with 50 plants or more, kept in a spreadsheet. They don't want fixed watering schedules, and they worry about getting their data back out. The order after the demo plant is picked for them.

1. **A demo plant and propagation**, so a first visit shows the app with plants in it. It is built in a draft pull request that is waiting on photos of a coleus at each stage. The posts on Reddit come once it is in.

### Cheap ones to slot in whenever

Small ones with no new screen worth the name:

- Small info buttons next to the less obvious things.
- **A note for next year**, shown next to A year ago today.
- **Empty the outer pot** after watering a plant that stands in one.
- **A parent that isn't yours**, for cuttings from a friend's plant.
- **How much bigger the pot is** on Pot up.

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
| ✅ | Plant and propagation pages as a profile of the plant: they open on the photos and notes, newest first, with one line under the name for where it is, what it grows in, when it was last watered and where it came from. The rest is behind **Details**, and **Edit** is a small link | 1 | M | M3–M4 |
| ✅ | Buttons at the bottom of a plant or propagation page within reach of a thumb: **Photo** goes straight to the camera or gallery, **Note** and **Care** open a panel from the bottom, and a propagation's stage button holds the stage, its dates, Pot up (**It's a plant now** when it is already in soil, bark, LECA or PON) and Mark failed | 2 | M | v1.x |
| ✅ | Say a plant died, was given away or sold, went dormant or into quarantine, or flag it, from its page instead of the edit form, and bring it back into the collection the same way | 3 | S | v1.x |
| ✅ | **Recent changes line**: under the plant's name, "Repotted 6 days ago, moved to the hall 3 days ago" for a while after a pot up, a room change or arriving. How many days is a setting. It only states what happened and never says what it means | 4 | S | v1.x |
| ✅ | Short add and edit forms: name, where it came from, the date and notes on top, and the rest under **More details** with a line saying what is set. Save stays pinned at the bottom | 2 | S | v1.x |
| ✅ | **About it** in place of Notes on the plant and propagation forms and pages, with a line saying it's for what stays true about the plant and that dated notes go on its history. It sits under **More details** on the add forms. The field and what is already stored in it stay the same, and the spreadsheet export still calls the column Notes | 5 | S | v1.x |
| ✅ | A loose date for when you got a plant: a whole day, just the month, or just the year, so a date you only half remember doesn't have to be guessed at | 3 | S | v1.x |
| ✅ | Tags (e.g. "variegated", "rare", "for swap") and a tag filter. Picking several tags narrows the list to plants that have all of them, the search finds tags too, and a tag on a plant's page opens the list filtered by it | 3 | S | v1.x |
| ✅ | Rename or merge a tag everywhere at once, the way rooms can be | 6 | S | v1.x |
| ✅ | Adding or removing a tag is written on the plant's history ("Tagged flowering", "No longer tagged flowering"), so a tag used for something a plant goes through still says when it began and ended. Renaming or merging a tag isn't written on the history | 4 | S | v1.x |
| ✅ | Add rooms, spots and tags from the Rooms and tags page, so they can be set up before any plant uses them. An empty room or spot can be removed there again | 4 | S | v1.x |
| ✅ | Select several plants at once: change status, log care, add a note or tags to all, or move them all to a room | 4 | M | v1.x |
| ✅ | **Dormancy status** for plants and propagations, e.g. an alocasia dropping its leaves in winter or a corm sitting still. A badge and the day it went dormant show it's resting rather than dead, the plant list has a "Dormant" filter, and going dormant and waking up are recorded on the history. A dormant propagation doesn't come up on Today to be checked on. Dormancy ends by itself when a plant dies or leaves, or a propagation finishes | 4 | S | v1.x |
| ✅ | **Growing season**: the season is set under What it likes, as grows all year or rests in winter or in summer. When its usual resting time comes round (from November for a plant that rests in winter, May in the southern hemisphere, and later from around the day it went dormant before) Today asks "Gone dormant?". Yes marks it dormant like the button on the plant page, and Not this year stops asking until next season. It only asks about plants where a season was set, since most houseplants are tropical and don't need a rest | 5 | S | v1.x |
| ✅ | Sort options: name, newest, room, last activity | 5 | S | v1.x |
| ✅ | The plant list remembers the sort you picked, on the device, the way the Pots page remembers its kind | 4 | S | v1.x |
| ✅ | **Search everything**: a Search page at /search, opened from a magnifier in the phone header, Search in the side menu on a computer, and the `/` key. One box looks through plants, propagations, wishes, pots, mixes, feeds, products, treatment recipes, rooms and spots, tags, and the notes on every history, care log, problem and pest case, ignoring case and accents, with every word needing to match. Results are grouped (the first 5 of each, then Show all), notes show the matched words in bold and come newest first, and a result opens the page it is on and marks the note or item. The last 5 searches are kept on the device. Searching notes starts from two letters. The searches on the Plants, Propagations and Wishlist pages stay as they are. Nothing new is stored | 4 | M | v1.x |
| ✅ | **Quick add**: paste or type a list of names ("Alocasia zebrina, Monstera deliciosa, …") to add many plants at once. Each name is matched against the plant dictionary, and anything else on the line goes into the notes | 5 | S | v1.x |
| ✅ | **Soil to semi-hydro transition tracker**: a checklist on the plant page (roots washed, first new water root, first new leaf), started from the edit page when the medium changes or from More, and a check on Today after 6 weeks (or the weeks chosen) with nothing new | 5 | M | v1.x |
| ✅ | **Problem log**: Something's wrong on a plant's More menu, for yellow leaves, root rot, sunburn, brown tips and the rest. Pick what you see, then add updates with what you did and whether it looks better, the same or worse, with photos, and close it with how it ended. Each step goes on the history, and the problem page lists the repots and moves made meanwhile. Opening one shows what you did the last times a plant of yours had the same and how it ended, never advice. Look again puts it on Today on a day you pick. Open problems have a badge, a filter on Plants, and a list on the Pests and problems page. Only plants for now | 4 | M | v1.x |
| ✅ | **Already have it?** Adding a plant with the same name as one you have says so and links to it, so a second one is added on purpose | 5 | S | v1.x |
| ✅ | Favourites / pinned plants at the top of the list | 6 | S | v1.x |
| ✅ | Grid view with large photos as an alternative to the list | 4 | S | v1.x |
| ✅ | **Table view**: a third view on the plant list next to List and Grid, with a column per detail (name, cultivar, last watered, fed and repotted, genus, species, everyday name, room, when and where you got it, origin, how it was raised, price, medium, moisture (left empty once the plant has been watered since the reading), status, tags, when it was added, and a column for each of your own fields). Tap a heading to sort by it and again to reverse it. The Columns picker chooses what shows, lets you drag the columns (or use arrows) into your own order, and each device remembers it, along with the view and the sort. On a computer it uses the whole width of the screen, and on a phone it scrolls sideways with the name column pinned. For someone used to keeping their plants in a spreadsheet | 4 | M | v1.x |
| ✅ | Group the plant list by genus, with an A to Z jump for long lists. Plants has a By genus sort, and By genus and By room both show a heading for each group with its count. From 30 plants sorted A to Z or by genus, a letter strip down the side jumps to a letter, and a finger can slide along it | 5 | S | v1.x |
| ✅ | **The collection at a glance**: a Collection page, from Plants, with how many plants, species and genera there are, the plant you have had longest, and how many there are of each genus and in each room as bars. Each count opens Plants filtered to it. Propagations rooting now and rooted this year are counted too. Worked out from what is stored | 5 | S | v1.x |
| ✅ | **Collection numbers**: an optional short number per plant, e.g. 117, set on the plant form with the next free one suggested. Propagations get 117a, 117b and so on (117c-a from a lettered parent), and a cutting potted up keeps its code (117c1, 117c2 for several). Settings can number the plants you have, oldest first. The number shows on cards, tiles, pages, the plant table (No.), shared lists, the sitter sheet, the spreadsheet import and the CSV export, and the search finds it. Two with the same number are pointed out, never blocked or changed | 4 | M | v1.x |
| ✅ | **Sellers**: where plants were bought as a list instead of typed out each time, with what was paid, the state the plant arrived in, and how the plants from each seller have done since | 4 | M | v1.x |
| ✅ | **How it was raised**: nursery grown, tissue culture, from seed, from a cutting or division, or wild collected, picked under **Where it came from** for a bought, gifted, swapped or unknown plant. A plant grown from seed or propagated reads as from seed or from a cutting. A locality or collector's number goes in **Locality or code** beside the name. It shows on the plant's page after how you got it, has a column (Raised) in the spreadsheet export, and the import reads it | 4 | S | v1.x |
| ☐ | **Orders**: a purchase or swap that brought several plants at once, with the seller, the date, the shipping and the total. The plants that came in it point to the order, so what each one cost can include its share of the shipping. Builds on Sellers | 7 | M | v1.x |
| ✅ | **Unsure names**: mark a name as not certain yet, the way collectors write it (cf., aff., or sp. for a plant with no species name), with an optional locality or collector code, e.g. "Hoya sp. Sumatra". The name fields and the search work as before, and nothing extra shows until it's used. It also covers a plant with no name at all, so a lost tag leaves a note instead of a blank | 4 | S | v1.x |
| ☐ | **A page for each genus and species**: tap the name on a plant to see every plant and propagation of it you have or had, how its propagations went in each medium, and a note of your own that shows on all of them, e.g. "mine like it tight" | 8 | M | v1.x |
| ☐ | **A note and photos on a room or spot**, e.g. "south window, cold draught in winter", shown on the Rooms and tags page and on the plants that stand there | 5 | M | v1.x |
| ☐ | **What has lived in a spot**: a room or spot lists the plants that have stood there, for how long, and how each did: still there, moved on, or died there. It comes from the room changes the history already keeps, so nothing new is stored. It only says what happened and never gives the spot a score | 6 | S | v1.x |
| ☐ | **Moving house**: say you have moved, and pick which new room the plants from each old room went to, or move them later. Every plant gets one "Moved house" entry on its history, and the watering reminder learns again from the move on, since the light and warmth are different. The waterings before the move stay in the history. The entry is a change with a field saying what happened, like pot-ups, so older versions still read it | 6 | M | v1.x |
| ☐ | **Attach a file to a plant**: a receipt, a plant tag or a care card, as a photo or a PDF. Only images and PDFs, with a size limit per file and in total, and never shown on a share link, so it can't be used to host files for others | 8 | M | v1.x |
| ✅ | **Why it died**: an optional note on the cause when a plant is marked as died. It shows on the plant and on its card under Gone, where a Died choice lists only the plants that died and why. Adding a plant with the name of one that died shows what went wrong with it | 6 | S | v1.x |
| ◐ | **Plant dictionary**: a large built-in list of plants and their varieties (genus, species, cultivars, everyday names) to search and pick from when adding a plant or propagation. Works offline. Has every genus and species from Kew's checklist for the aroids (Araceae), prayer plants, begonias, carnivorous plants and the genera of most common houseplants, the species with an everyday name from big genera like Ficus and Euphorbia, the old names (Sansevieria for Dracaena, Plectranthus for Coleus), and a starter list of cultivars. Family and native region, and a page to browse it, aren't built. See `tools/plant-names` | 6 | L | v1.x |
| ✅ | **The common houseplants in the dictionary**, with the names people call them. A "What plant is it?" box on the plant and propagation forms finds a plant by its everyday or botanical name, so typing "snake plant" finds Dracaena trifasciata, and so does Quick add. A plant with no nickname shows its everyday name under the botanical one, and the list searches find it. Names come from Wikidata (CC0) and lists kept by hand. English and Danish names, picked in Settings or from the browser's language. The Danish file is only downloaded on a device that uses it | 2 | L | v1.x |
| ✅ | **Name autocomplete**: suggestions as you type in the genus, species and cultivar fields, from the dictionary and the names already on your plants and propagations, including hybrids and named varieties. An old name offers the one it goes by now, and a cultivar fills in its genus and species. Anything not on the list can still be typed | 6 | M | v1.x |
| ☐ | **Leaf log for variegated plants**: each new leaf with a photo and a variegation rating (low / medium / high / reverted), so reverting plants are spotted early | 6 | M | v1.x |
| ✅ | **Wishlist**: plants you want, opened from the Plants page at /wishlist. A wish has the same name fields and "What plant is it?" box as a plant, or a name of your own, plus where to find it, the most you would pay and notes. It says when you already have the plant ("You have 2", or "You have another Monstera deliciosa"), and an old name counts as the one it goes by now. Got it opens Add a plant or Add a propagation filled in from the wish, and once saved the wish moves to a Got section that links to what it became and can be put back. The add forms offer to mark a matching wish got. Searching the Plants page shows the matching wishes under the plants, and the search on the Wishlist page shows the plants and cuttings you already have. Share as text sends the open wishes, with switches for notes, the most you would pay and a "Kept in Stikling, stikling.app" line. Wishes sync and are in backups | 4 | M | v1.x |
| ✅ | **A photo on a wish**: add a picture to a wish, e.g. of the plant at a shop or in a seller's listing, which moves to the plant's photos when it is got | 4 | S | v1.x |
| ✅ | Duplicate a plant (e.g. a second basil pot): "Add another like this" opens the add form filled in from it | 7 | S | v1.x |
| ✅ | **More than one plant in a pot**: a count on a plant (How many in the pot), e.g. five aloe pups in one pot, and **Some of them** in the More menu for when some died, were given away or were potted up on their own as new plants. **Pot together** puts a plant into another plant's pot: it is marked as potted with it, leaves the lists and is counted in the other plant, keeps its history and lineage, and **Take it out again** undoes it. Potting up a propagation can put the cuttings all in one pot or in with a plant you have | 5 | M | v1.x |
| ✅ | **Price paid** per plant: a number with no currency, under **Where it came from** when the plant was purchased. The plant's page says "Bought from Plantorama for 150" or "Paid 150", "Add another like this" copies it, and the spreadsheet export has a column for it | 7 | S | v1.x |
| ✅ | What a plant was sold or traded for: the price, or the plant you got back | 7 | S | v1.x |
| ✅ | **Your own fields**: add a field the app doesn't have, e.g. clone name, awards or humidity, as text, a number, a date or a choice from a list you set. Add and change them under Settings, **Your fields**, or from **More details** on the plant form. They show on the plant's page once filled in, the search finds text and choices, and the spreadsheet export has a column for each. Removing a field hides it and keeps the values, which come back if a field with that name is added again. Nothing extra shows until you add one | 4 | M | v1.x |
| ☐ | **Home map**: a simple plan of each room with the plants placed where they stand, to see what is where and to go round them in order | 7 | L | v1.x |
| ✅ | **Toxic to pets?** Whether a plant is toxic to cats and dogs, from the ASPCA, and to people, from NC State Extension, under Details on plant and propagation pages with the source linked. A plant with nothing known says "Nothing known about pets" rather than looking safe | 5 | L | v1.x |
| ✅ | **What it likes**: the light a plant likes (apart from the light where it stands), how it likes its water, how much humidity, and its growing season with how it rests (kept cooler, kept drier, both or dies back). The plant page shows it in one row, with a note when the plant stands somewhere darker or brighter than it likes. The plant list can be filtered by it, the table has columns for it, and changing it on one plant offers to set the same on the other plants of the same species. A plant potted up from a propagation starts with what its parent likes | 4 | S | v1.x |
| ✅ | **Care for common plants filled in**: for about 70 common houseplants, the plant form offers the light, water and humidity they usually like, and a growing season when a source says they go dormant, as "Usually likes ... · from <source>" with a Use button that only fills what is empty. A plant page with nothing set under What it likes offers the same. From a care file kept by hand in `tools/plant-names`, where every value names its source, and nothing is filled in without one | 4 | L | v1.x |
| ✅ | **Your own fields in the plant list**: every field, and How it was raised, can be a column in the table and sorted by (numbers as numbers, dates as dates, choices in the order of their list). Choice fields and How it was raised filter the list from the Fields menu, several at once, with Not set as a choice. In Select mode, **Set a field** fills one in on several plants at once | 4 | S | v1.x |
| ☐ | **Symptom check**: tick what you see on a plant (yellow leaves, brown tips, drooping, spots), how wet the soil is, and anything that changed lately, and get the few likely causes with what to check next and the source each one comes from. The app can't see the plant, so it only ever says what to look at, never to water or treat, and says so when the answers don't point anywhere. What you see can go straight into the problem log | 6 | L | v1.x |

## 2. Photos & timeline

Every plant and propagation gets its own history. Entries are only ever added, and a corrected note or photo keeps the version it replaced, so the full history is kept.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Take a photo with the camera or pick from the gallery. Photos are resized and compressed on the phone (≈1600 px, WebP, or JPEG where the browser can't make WebP) with a thumbnail | 1 | L | M3 |
| ✅ | Photo timeline per plant and per propagation, newest first, tap to view full screen | 1 | M | M3 |
| ✅ | Notes on the timeline ("new leaf unfurling", "roots 2 cm") | 1 | S | M3 |
| ✅ | Automatic timeline entries for changes: status, medium/pot ("moved to semi-hydro"), room, stage | 1 | M | M3 |
| ✅ | Choose the cover photo | 1 | S | M3 |
| ✅ | Tap the photo at the top of a plant or propagation page to open it full screen, where **Set as cover** is. The cover used to be chosen only from a photo opened in the history, which was easy to miss | 2 | S | v1.x |
| ✅ | **Frame a photo**: drag and zoom a photo (pinch, the slider or the mouse wheel) so the plant sits well where it's cropped, on cards, at the top of its page and in the history. Only the position and zoom are saved, the photo itself isn't changed, and photos never framed stay centred | 3 | M | v1.x |
| ✅ | Correct a timeline entry: fix the text or the date of a note or a photo, and its photos move with it. The earlier versions are kept and shown under "edited". Automatic entries are fixed where they come from, and any entry can be deleted | 3 | S | v1.x |
| ✅ | **Fix or delete a care entry**: tap it in a plant's Care history to change the date, reading, water, EC, pH or note, or delete it | 2 | S | v1.x |
| ✅ | **Import existing photos with their dates**: pick old photos from the gallery and each lands on the day it was taken, so a plant's history starts from day one. The date comes from the photo itself (EXIF), then from a date in the file name, then from the file. Photos from the same day share an entry, a note keeps the photos picked with it together, and the app says how many photos had no date of their own so they can be checked. Works on a plant's page and when adding a plant, where the day it was got moves back to the oldest photo's, so the photos come after it in the history | 3 | M | v1.x |
| ✅ | **Pot-ups and failures in the history** get their own title and icon instead of "Updated". A new optional field on the entry says what happened, and its kind stays a change, so older app versions still read the entry and show "Updated" as before. A value in the field that a version doesn't know reads as empty. Entries from before are recognised by the text the app wrote ("Potted up 2: …", "1 failed: rotted") when they are shown, so nothing stored is rewritten. A new value in `TimelineKind` would instead stop older versions from loading any history | 4 | S | v1.x |
| ☐ | Swipe through photos side by side to compare growth | 5 | M | v1.x |
| ✅ | **Reference photo**: a photo that isn't of your own plant, like the seller's listing, the parents of a hybrid or the plant fully grown. It sits apart from the history and never becomes the cover or day one, and it is a record of what was bought if a label goes missing | 4 | S | v1.x |
| ✅ | **Voice notes**: covered by the keyboard's own microphone key, which works in every field and every browser. A button of the app's own was left out, since Firefox on Android has no speech-to-text for web pages and Chrome sends the audio to Google. Help says where the key is | 4 | S | v1.x |
| ☐ | Measurements on an entry: number of leaves, height, root length | 6 | S | v1.x |
| ☐ | **Growth curve**: a small chart of a plant's or propagation's measurements over time, once there are a few to draw | 7 | S | v1.x |
| ☐ | **Share into Stikling**: the installed app appears in Android's share menu, so a photo from the gallery can go straight to a plant (Web Share Target) | 7 | M | v1.x |
| ✅ | **Before and after**: in the Share sheet, pick two photos of a plant or propagation, starting on the first and the newest, and get one image of them side by side (stacked for 9:16) with the dates and how far apart they are, ready to post. Shared through the phone's share menu, or saved | 3 | M | v1.x |
| ✅ | **Time-lapse video**: in the Share sheet, with three or more photos, make an MP4 of a plant's or propagation's photos in the order they were taken. Each shows for a moment, Slow, Normal or Fast, with its date and day count in the same layout as the share card, and fades into the next. The last one stays up a little longer, and there is no sound. Square, 4:5 or 9:16, with the size and speed remembered, and a tap on a photo leaves it out (at least two stay in, and up to 60 photos, so a longer history starts with 60 spread over it). The preview plays on a loop, and waits for a Play button with reduced motion. It is made on the phone with the browser's own H.264 encoder, or with a WebAssembly one at a lower frame rate where there is none, like Firefox on Android, and loaded only when someone makes a video. Shared through the phone's share menu, or saved as an MP4. Which formats TikTok and Instagram take still needs trying on a phone | 4 | L | v1.x |
| ☐ | **Ghost overlay** when taking a photo: a faint copy of the previous photo to line up the same angle every time | 8 | L | v1.x |
| ✅ | **Day count on photos**: each photo of a propagation shows the day it was taken on, e.g. "day 24", in its history, the photo viewer and Before and after. Photos of a plant count from the day you got it, when that is a full date. The time-lapse has it too | 3 | S | v1.x |
| ☐ | **Mark up a photo**: draw a circle or an arrow on a photo and add a short label, like "new node" or "mealybugs here". The marks are saved on their own, so the photo itself is never changed | 5 | M | v1.x |
| ☐ | **Link to another plant in a note**: type @ and pick a plant, propagation or pot, and the note links to it. The other one's page shows the notes that mention it | 6 | M | v1.x |
| ☐ | **A photo of a shelf or spot with the plants marked on it**: tap a plant in the photo to open it. The photo also shows on the history of each plant marked in it | 5 | L | v1.x |

## 3. Propagations

This is the core of the app: cuttings, corms, seeds and divisions, linked to their parent.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | One-tap **Propagate** on a plant page, which creates a propagation linked to that plant | 1 | M | M4 |
| ✅ | Type: cutting, corm, offset/pup, seed, division, air layer, leaf | 1 | S | M4 |
| ✅ | Medium: water, perlite, sphagnum, LECA, PON, soil, corm riser, other | 1 | S | M4 |
| ✅ | Stages: started, rooting, rooted and done (or failed). The date of each stage change is saved | 1 | M | M4 |
| ✅ | Batches with a count ("3 corms in LECA"); mark some as failed | 1 | M | M4 |
| ✅ | **Gave some away**: units of a propagation still going can be given away, with who got them, without potting them up first. They don't count for or against the success rate | 4 | S | v2 |
| ✅ | Correct how many were potted up or failed under Edit, when one was marked by mistake. What's left goes back to growing at the stage it had reached | 2 | S | v1.x |
| ✅ | **Promote to plant**: turn 1..n units of a batch into plants and keep the lineage | 1 | M | M4 |
| ✅ | Propagations list grouped by stage, with cards like "3× corm · LECA · day 24" | 1 | M | M4 |
| ✅ | Propagations without a parent plant (bought seeds, e.g. the bay laurel) | 1 | S | M4 |
| ✅ | "Check on it" reminder in the app: a propagation not looked at for X days shows on Today | 2 | S | M5 |
| ✅ | Success rate and average days-to-root per medium and per type, on a results page linked from Propagations. Success is counted in units, and a unit has made it once it's potted up or its batch has rooted | 3 | S | v1.x |
| ✅ | **Experiments**: group propagations started together to compare mediums (the corm test: perlite vs sphagnum vs LECA vs corm riser) | 4 | M | v1.x |
| ✅ | **On time or slow**: a propagation that is still rooting says whether it is on time or slower than usual, compared with your own average days to root for the same type and medium. Only shown once there are at least 3 rooted batches to compare with, and the list cards only mention it when it's slow | 4 | S | v1.x |
| ☐ | **Is it a fair comparison?**: on the results page and in an experiment, a line saying so when the batches being compared differ in more than one thing, e.g. a different medium and a different month, so a difference isn't read as the medium's doing | 6 | S | v1.x |
| ☐ | **Ready by**: give the date a rooted cutting should be ready, e.g. for a swap on 1 June, and the app says when to start it, from your own days to root for that type and medium. Only once there are at least 3 rooted batches to go by, like On time or slow. It answers when asked and puts nothing on Today | 6 | S | v1.x |
| ✅ | **Start like last time**: Start propagation can fill in the type, medium, container and rooting aids from your last propagation of the same plant or type, and anything can still be changed | 4 | S | v1.x |
| ✅ | Seed germination: sown count, germinated count and dates | 5 | S | v1.x |
| ✅ | **Rooting aids**: what was used to help a propagation root, e.g. rooting powder or gel, cinnamon, or a bag or dome for humidity. The results page compares each one with the batches that went without it | 5 | S | v1.x |
| ✅ | **Let the cut end dry first, as a rooting aid**: another choice under Rooting aids, with how long it dried. The results page compares those batches with the ones that went straight in, so conflicting advice is settled by your own results | 4 | S | v1.x |
| ◐ | **Changing the water**: log "changed water" in one tap on a propagation in water, with an optional number of days between changes that is off until set and then comes up on Today. The results page compares batches by how often the water was changed. Logging is built (**Care** on a propagation in water); the reminder interval and the comparison on the results page are not | 6 | S | v1.x |
| ✅ | **Care on a propagation**: a **Care** button next to Photo and Note logs a watering and a moisture reading (0 to 10) for one in soil, bark, LECA, PON, perlite or sphagnum, and a water change for one in water or on a corm riser, on today or another day. The newest shows under its name and in Details, logging care counts as looking at it for Worth a look on Today, and potting up carries the last watering over to the new plants. Kept in its own kind of record, so plants' care log is untouched | 5 | M | v1.x |
| ✅ | **Rooted pups as plants**: Start propagation says that a pup or division with its own roots is a plant and links to Add plant with the parent picked. A propagation already in soil, bark, LECA or PON has **It's a plant now** in place of Pot up, which keeps the medium it is in | 4 | S | v1.x |
| ✅ | **Milestones** on a propagation: first root, first leaf, corm size. Gives the medium experiments concrete dates to compare. Moving a propagation to rooting on its page notes the first root as that day. First root, first leaf and corm size are built and show in experiments | 5 | S | v1.x |
| ✅ | **Photo on Start propagation**: add photos with Camera or Gallery while starting a propagation, like Add plant has, so its history starts with how the cutting looked on day one. The newest becomes the cover, and the start day moves back to the oldest photo unless you set it yourself | 3 | S | v1.x |
| ✅ | **A date on Mark failed**, today unless changed, like Pot up has. The history entry and the finished date use it, so a rotted corm found a week late is recorded on the right day. Pot up uses its date for the finished date as well | 3 | S | v1.x |
| ✅ | **The result of a finished propagation**: in place of the "Finished … after … days" line, a short summary of how long it ran, the days to first root, how many were potted up and failed, and the plants it became with the room each one is in and whether it's still in the collection. All from data the app already has, and the share card uses it | 3 | S | v1.x |
| ✅ | **Say what the less obvious choices are**: a line under **Grows in** and **Type** describing the one picked, for LECA, PON, sphagnum, corm riser and air layer, on the plant and propagation forms (not on the pot up sheet). The same explanations are in one Help answer | 5 | S | v1.x |
| ✅ | Lineage tree view (family tree across generations). It opens on one plant with three generations of where it came from, its siblings (other cuttings from the same mother) and what has come from it. Tapping another plant moves the tree to that one, so a big family never turns into one huge chart | 4 | M | v1.x |
| ✅ | **What a plant has given**: on a plant's page, how many cuttings it has given, how many rooted, how many were given away, and how many plants have come from those in turn | 4 | S | v1.x |
| ☐ | **Generation**: a plant or propagation says how many generations it is from the oldest ancestor you have, e.g. "3rd generation from the kitchen Monstera". Worked out from the parent links, with nothing to fill in | 8 | S | v1.x |
| ✅ | **Why a propagation failed**: an optional reason on Mark failed (rotted, dried out, pests, mould, don't know). The results page shows what each medium loses cuttings to, and at which stage | 4 | S | v1.x |
| ☐ | **How it differed from the ones that rooted**: a failed propagation lists what was different from your rooted ones of the same type: the medium, the rooting aids, the month it was started and how often the water was changed. Only facts from your own records, and it says so when nothing stands out | 6 | S | v1.x |
| ✅ | **Given away / swapped**: record who got a cutting, so the family tree extends to friends' plants | 7 | S | v1.x |
| ☐ | **A parent that isn't yours**: a plant that lives with someone else or is gone, like grandma's pothos, added only so its cuttings have somewhere to come from. It shows in the family tree and on its offspring, never in the plant list or on Today, and can say whose it is. A field of its own on the plant rather than a new status, so older versions still read it | 5 | S | v1.x |
| ☐ | **Unsure parent**: mark where a plant came from as a guess, e.g. "probably a cutting of the kitchen Monstera", shown with a dotted line in the family tree | 7 | S | v1.x |
| ✅ | **Labels for a label maker**: a page at /labels, reached from Select on Plants and Propagations (Labels in the action bar), from More on a plant or propagation (Print a label), after starting a propagation and after potting one up. It makes a label for each plant or cutting picked, with the collection number large on the left and the name and botanical name beside it, and the date if ticked; a label can be left out for this print. Three ways out: a phone label maker's app (one PNG per label at 203 dpi for 12×40, 14×30, 40×30, 50×30 mm or a custom size, shared through the share menu in one go, or saved where the browser can't share files), a tape printer installed on a computer (tape 9 to 24 mm, label length 30 to 60 mm, one label per page), and a sheet on A4 or Letter (pot labels of 50×25 mm with cut lines, or 15×120 mm plant stakes with a pointed end and the number at the top). Tape 12 mm and under is one line. The size and what to show are kept on the device. No QR code needed | 4 | S | v1.x |
| ☐ | Printable QR labels for jars and pots; scanning one opens the plant or propagation | 6 | L | v1.x |
| ✅ | **"Available for swap" list**: a page at /swap, linked from Propagations and from the label on a plant or propagation. A plant is marked as having cuttings to spare or as up for swap itself, and a propagation says how many cuttings it can spare, never more than what is left, with giving some away taking them off first. Marks are set from More on the plant or propagation page, in the edit form, and for several plants in Select on the Plants page. The page lists cuttings with their count and stage, cuttings on request and plants, offers to mark plants tagged "For swap", and shares the list as pictures or as text from Share | 4 | M | v1.x |
| ✅ | **The swap list as an image**: Share on the swap page makes pictures like the share card, with the list's photos in a grid and the name and count in a strip under each one. A tile without a photo shows the leaf, a list that doesn't fit is split over several numbered pictures ("1 of 3"), and they come in square, 4:5 and 9:16. They are shared together through the share menu or saved, and the nicknames switch applies to both the pictures and the text | 4 | M | v1.x |
| ☐ | **An experiment's result as an image**: the mediums side by side with how many rooted and how long it took on average, e.g. "LECA: 4 of 4, 21 days", to post in a group | 6 | S | v1.x |

## 3b. Crossbreeding

For people who pollinate their own plants and raise the seedlings. A cross is two parents, a date and a method, and then a long wait, so most of the value is in writing it down on the day it happens. A plant only has one parent in the app today, so holding on to both is the real work in this section.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ☐ | **Crosses**: record a pollination with the pod parent, the pollen parent and the date, shown as "pod parent × pollen parent". A parent that isn't in the app can be typed as a name | 7 | M | v1.x |
| ☐ | How it was done: brush or by hand, fresh or stored pollen, whether the flower was bagged, and a note for anything else | 7 | S | v1.x |
| ☐ | Outcome: whether it took, when the seed was ripe, how much there was, and when it was sown | 7 | S | v1.x |
| ☐ | Seedlings from a cross keep both parents, so the family tree shows where a hybrid came from | 7 | M | v1.x |
| ☐ | A working name for the cross, used on the seedlings until they earn a real one | 7 | S | v1.x |
| ☐ | Cross log: every cross in one list with what came of it, so the ones worth repeating stand out | 8 | S | v1.x |
| ☐ | Traits on parents and seedlings, e.g. dark new leaves or silver splash, to see which parent passes what on | 8 | S | v1.x |
| ☐ | **Pollen store**: what is in the freezer, from which plant and when it was collected, since pollen doesn't keep forever | 8 | M | v1.x |
| ☐ | **Seed lots**: seeds you have before sowing, with where they came from, when they were collected or bought, a sow-by date and an amount that can be rough, e.g. about 100 or 1 g. Sowing starts a seed propagation linked back to the lot, and a cross's pod becomes a lot | 8 | M | v1.x |
| ☐ | A plant coming into flower shows on Today together with the pollen you have stored for it | 8 | M | v1.x |
| ☐ | Compare the seedlings from one cross side by side, to pick the keepers | 8 | M | v1.x |

## 4. Care log

Built around your routine: moisture meter instead of a watering schedule, semi-hydro reservoirs, and different fertilisers for different plants. The basics are in. Only the notable kinds (repotted, flushed, pruned) go on a plant's history, so watering doesn't bury the photos and notes.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Quick-log care: watered, fertilised, reservoir topped up, flushed, repotted, pruned, rotated, cleaned leaves, harvested | 1 | L | v1.x |
| ✅ | Log several plants at once ("fertilised all semi-hydro plants") | 2 | M | v1.x |
| ✅ | "Last watered / fertilised / flushed" summary on each plant | 2 | S | v1.x |
| ☐ | **Empty the outer pot**: after watering a plant that stands in an outer pot, Today reminds you to tip out the water left in it, since a pot left standing in water for days is a common cause of root rot. How long to wait is set in Settings, an hour unless changed, and the reminder can be turned off. Plants watered together share one reminder. A notification for it can follow, now that **Notifications** is built | 4 | S | v1.x |
| ✅ | **Moisture meter reading** (0–10) as a log type, with the latest reading shown on the card | 3 | S | v1.x |
| ✅ | **Products**: your fertilisers (the hydro one, the general one, the herb one), each with a usual dose in ml, g or drops. The dose is typed the way the bottle puts it, e.g. 5 ml per 4 L or 1 ml per 500 ml, and is optional | 4 | M | v1.x |
| ✅ | **Nutrients on a product**: N-P-K for a fertiliser and any other nutrient (Ca, Mg, S, Si, Fe and trace elements) for any product, copied from the label as printed, shown on the Products list and when picking products for a feed | 4 | S | v1.x |
| ✅ | Which products and doses went in on a feed or a top-up, e.g. "Hydro fertiliser, 2 ml/L". The dose starts at the product's usual one and can be changed for that time, and the entry keeps its own copy, so editing or deleting a product doesn't change what was logged | 4 | S | v1.x |
| ✅ | **A watering reminder that learns each plant**: from a plant's own history the app learns how long it usually goes between waterings, and from its meter readings how long it takes to dry out to the reading you water at. That is 3 by default, the top of the red "dry" part on a 0 to 10 meter, and can be set per plant, higher for one kept moister, like a peace lily. It says nothing about a plant until it has seen a few waterings, and never goes by a care guide or a fixed interval. When a plant is due, Today asks you to check it in words that fit what it knows: "probably dry by now, check with the meter" when there are readings, or "usually watered about every 9 days, it's been 9 days" when there aren't. The tick logs the watering. "Still wet" takes a meter reading from 0 to 10, or "No meter", and the plant comes back after a while. The guess leans later when unsure. Semi-hydro, corm risers and self-watering pots count how long the reservoir lasts, and plants in water how often they are topped up. A change in how a plant gets water starts the learning over. Dormant stretches are learned apart from the rest. When the days get shorter, a plant without a grow light leans later, from earlier years or from how the other plants shift. The plant page says roughly how long it goes and how many waterings that is learned from. It can be switched off per plant, and it's off until turned on in Settings | 3 | M | v1.x |
| ☐ | **How long it goes, by season**: next to how long a plant usually goes between waterings, the range in the brighter and the darker half of the year, from its own history, e.g. "9 to 12 days in summer, 15 to 24 in winter". Only once there are a few waterings in each | 6 | S | v1.x |
| ✅ | **Feed with the watering**: a plant can be set to get its feed with every watering, or every second or third, so ticking Watered logs the feed and its dose as well. Off unless set | 4 | S | v1.x |
| ✅ | **Water plants**: one pass over the plants. Pick rooms (order remembered on the device, skipped with one room), choose Meter or Feel the soil, answer how dry each plant is, and tap Watered on the dry ones. It saves on the tap with Undo, and a Still damp answer puts the plant off on Today. The feed with the watering is offered when due, reservoirs are listed to top up, and a bar shows how many are checked. Each row has the plant's photo, Watered can be tapped without a check, Feed this round gives one feed with every watering (a plant's own due feed wins), and active cuttings in soil or bark are listed after their room's plants | 3 | M | v1.x |
| ✅ | **Your own kinds of care**: add a kind of care the app doesn't have, e.g. misted, bottom watered, wired or refilled the humidifier, from the care sheet or under Settings, Kinds of care. It is logged like the others, also on several plants at once. Each kind counts as a watering, a feed or neither, so a bottom watering reaches the watering reminder and last watered, and can go on the plant's history. Stored in fields of its own on the entry rather than as a new kind in the list, so older versions still read it (as Rotated) | 4 | M | v1.x |
| ☐ | Water type: tap, demineralised, rain | 6 | S | v1.x |
| ☐ | Semi-hydro details: reservoir level, flush interval, last flush | 6 | M | v1.x |
| ✅ | **Care intervals**: care that comes back by itself, like fertilise every 2 weeks from April to October, repot every 2 years, or an own kind of care every 8 weeks. Set in days, weeks, months or years, for the whole collection, a room, a tag or picked plants, and limited to part of the year if wanted. Today lists what is due grouped by kind, with one tap to log it for a plant or all. Feeding stops while a plant is dormant, round days gather it, and watering is never on an interval because the watering reminder learns it | 4 | M | v1.x |
| ✅ | **Nutrient dose calculator**: fill in the water on a feed or a top-up, in litres or millilitres, and each product shows how much to add | 7 | S | v1.x |
| ☐ | Harvest log for herbs (basil, parsley): date and amount | 7 | S | v1.x |
| ✅ | **Flowering log** (orchid, peace lily, hoya): under More on a plant's page, mark spike or buds, in flower and done, on any day up to today. Each goes on the history, with the chance to add a photo. A flowering from the past can be added with a month or a year. A badge shows in the lists while a plant is in bud or in flower, and drops after four months without a mark, when the page asks "Still in flower?". The page lists the flowerings by year and, after two years, says when the plant usually starts, e.g. by April, and says so if nothing has started yet this year. A flowering calendar, from a link on the Plants page, shows what is in flower now, what usually flowers next month and the whole year by month, from your own records. It stays off Today and only says when, never what to do about it | 3 | M | v1.x |
| ✅ | **Care calendar on a plant**: a year of waterings, feeds and meter readings as a grid of days, so the plant's rhythm and any gaps show at a glance | 5 | S | v1.x |

## 4b. Light and climate

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Light level per plant: low, medium, bright indirect, some direct sun | 5 | S | v1.x |
| ☐ | **Light readings**: type in a reading from a light meter for a room or spot, with the date and which window. The latest shows on the spot and next to the light level of the plants that stand there. Only readings typed in, since light apps on phones disagree with each other | 6 | M | v1.x |
| ☐ | **Room climate**: temperature and humidity readings for a room or spot, e.g. from a hygrometer in a cabinet, so a plant shows the conditions it lives in | 6 | M | v1.x |
| ◐ | **Grow lights**: your lamps with a name, wattage and hours on per day, and which plants are under each one. A room or spot can be marked as having a grow light, which the watering reminder uses to leave the seasons out of its guess. Names, wattage and hours on per day are still missing | 6 | M | v1.x |
| ☐ | Moving a plant under or away from a grow light is recorded in its history, like a room change | 6 | S | v1.x |
| ☐ | Window direction for each room (north, east, south, west), so plants in a room show what light they get | 7 | M | v1.x |
| ☐ | Filter by light, e.g. "under grow light" or "low light", to find a spot for a new plant | 7 | S | v1.x |
| ☐ | **Plant sensors and Home Assistant**: soil moisture, light and temperature from sensors you already have, read from your own Home Assistant and shown on the plant and its spot. The watering reminder can count a sensor reading like a meter reading, and still only ever asks for a check. How it connects, straight from the browser or through Stikling's API, needs working out first. Nothing changes for anyone who doesn't set it up | 8 | L | v2 |
| ☐ | Winter note: flag plants that may need a grow light when daylight gets short | 6 | M | v1.x |
| ☐ | **Light check**: a few questions about a spot (hours of direct sun, how far it is from the window, whether anything is in the way) that suggest one of the light levels above for a plant standing there. It says it's a rough guess from your answers, and the level can still be picked by hand | 7 | S | v1.x |

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
| ✅ | **Clean for how long**: a case being watched counts the days since its last treatment, towards 4 weeks unless changed in Settings. The case shows "Clean 12 of 28 days", and finding a sign starts the count again. It is ready to resolve once the count is reached and a check was logged in the last week. Today shows it, and it never resolves by itself | 3 | S | v1.x |
| ✅ | Quarantine flag with the day it started, a "Quarantine" filter and a badge on the card. Going in and coming out are recorded on the plant's history | 3 | S | v1.x |
| ✅ | A length for quarantine, e.g. two weeks, with the default set under Pests in Settings. When it is up, Today says it is time to check the plant and let it out | 4 | S | v1.x |
| ✅ | **Start quarantine when adding a plant**: a switch under Pests in Settings, off by default. When it is on, the add form starts with In quarantine ticked for a bought, swapped or gift plant, using the length from Settings, and it can be unticked there | 3 | S | v1.x |
| ✅ | **Back from outside**: mark a room or spot as outdoors, like a balcony. A spot in an outdoor room counts too. Moving a plant from there to an indoor room, on its edit form or with several plants at once on the Plants list, offers to start quarantine and a check for pests. It only asks when a plant moves, never on a date | 4 | S | v1.x |
| ✅ | **Treatment recipes**, e.g. "Alcohol spray: isopropyl alcohol and a drop of dish soap, topped up with water". The ingredients are typed, each with an optional amount like 250 ml per 1 L, and there's a note on how to use it. "Mix a batch" works out the amounts for any size of bottle. Logging a treatment offers your recipes to pick from, and the treatment saves the recipe as it was that day, so changing the recipe doesn't rewrite history. Recipes are under Supplies | 4 | M | v1.x |
| ✅ | Pest overview: all active cases in one list | 4 | S | v1.x |
| ✅ | **Sticky trap counts** on a pest case: count everything on a (blue/yellow) sticky trap and tick when a new one goes up, and the app works out how many were caught since last time. A small chart of the catch per week shows whether treatment is working. A count never moves the next treatment, but on a case being watched it counts as the weekly check, and new ones on the trap ask whether to start treating again. The trap shows straight away for pests that fly into one (thrips, fungus gnats, whitefly), and sits behind a small link for the rest | 4 | M | v1.x |
| ✅ | Inspection log: "checked, no signs", so you can see how long a plant has been clean. Checks on a single plant are logged from its menu, and the plant page shows when it was last checked, counting checks on cases | 5 | S | v1.x |
| ✅ | **Check the neighbours**: a case on picked plants lists the other plants in their rooms while it is being treated, with when each was last checked, and Clean and Add to case | 5 | S | v1.x |
| ☐ | **Biological control log**: release dates for predatory mites and similar | 6 | S | v1.x |
| ✅ | **A treatment plan with steps**: steps with the days between them, e.g. spray, spray again after 3 days, then release predatory mites after 10, saved under Supplies and started on a case (or when opening one). Today shows the step that is due, and a step can be logged, skipped or moved. After the last step Today asks whether to start watching or keep treating. A case with a plain interval works as it does now | 4 | M | v1.x |
| ✅ | Pests only has a tab in the bottom bar while a case is open. The rest of the time it's under Supplies, and a plant's menu can start a case, so someone who never has pests doesn't have a tab for them | 3 | S | v1.x |

## 5b. Supplies & shopping

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | **Pot suggestions**: a plant's pot is picked from your pot library, on the plant form and when potting up. A propagation's container is picked from the containers for propagating, kept as their own list in the pot library with how many are in use | 3 | S | v1.x |
| ☐ | **How much bigger the pot is**: on Pot up, the sheet shows the old and new pot widths and says when the new one is much wider, since that keeps the soil wet longer. It only says so above a width that can be changed in Settings, and any pot can still be picked | 5 | S | v1.x |
| ☐ | **Supplies stock**: LECA, PON, perlite, sphagnum, fertilisers, sticky traps, alcohol, pots | 5 | M | v1.x |
| ☐ | **Shopping list**: mark a supply as running low and it lands on a list to open in the shop | 5 | S | v1.x |
| ✅ | **Your pots**: the pots you own, by kind (nursery, outer, stands on its own), what they're made of, their measurements, whether they self-water, and how many you have. A plant points at a pot and, when it has one, an outer pot | 6 | M | v1.x |
| ✅ | See which of your pots are in use and which are free, so it's clear what's available before repotting. A plant that died frees its pots, and giving a plant away or selling it asks which of its pots went with it | 7 | S | v1.x |
| ✅ | **Photos of a pot**: one or more photos on a pot, shown on the Pots page, on the chips when picking a pot and on the plant that uses it, so an outer pot can be told apart from the others. Photos belong to the pot, not the plant, so they stay when a pot moves to another plant. They go through backup and sync like any other photo, and deleting a pot deletes them | 6 | M | v1.x |
| ✅ | The Pots page lists one kind at a time (nursery, outer, on its own), since they are used so differently, and remembers on the device which one was open | 6 | S | v1.x |
| ☐ | **Does it fit?**: given a nursery pot, which of your free outer pots it would go in. Compares the tops, allows for a rim only having to clear the opening, and says when the bottom is the tighter measurement | 7 | M | v1.x |
| ✅ | **Orchid bark** as a medium next to soil, LECA and the others, for orchids potted in bark with little or no soil | 4 | S | v1.x |
| ✅ | Default medium per pot kind: picking a self-watering pot for a new plant or when potting up suggests LECA, so potting up is one tap less | 8 | S | v1.x |
| ☐ | Money spent on plants and supplies, per year, to see what the hobby costs. On a plant's page, **What a plant has given** also shows what the plant cost and what its cuttings have brought in when any were sold. It only shows when there is a price on either side | 6 | S | v1.x |

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
| ✅ | Lately only shows the last 30 days, so a plant added with a date a year back doesn't show up as something recent, and the list says there's nothing new instead | 2 | S | v1.x |
| ✅ | Pest treatments due or overdue, checks due on cases being watched, and a new case until its first treatment is logged | 3 | S | v1.x |
| ✅ | Counts: plants, active propagations, success rate this month | 4 | S | v1.x |
| ✅ | **Needs attention**: flag a plant or propagation with a reason ("repot soon", "look closer", "change the water"), typed or picked from a few suggestions, and it stays on Today until it's marked done there or on its own page. Flags aren't written on the history, and one goes away by itself when the plant leaves or the propagation finishes | 4 | S | v1.x |
| ✅ | Put something on Today off until tomorrow, for 3 days or for a week, without logging anything: a propagation waiting to be looked at, a flag, or the photo reminder, which can also be skipped for the rest of the month. What's put off is remembered on the device, not in backups. Pest treatments stay until they're logged | 5 | S | v1.x |
| ✅ | **Photo reminder**: once a month, Today lists the plants and propagations that haven't had a photo taken that month, room by room, with a camera button on each that puts the photo straight on its history. Dormant ones are left out. Off until turned on in Settings | 5 | S | v1.x |
| ✅ | Plants due a watering or a check today, and the ones due in the next couple of days under Coming up, when the watering reminder is turned on | 3 | S | v1.x |
| ✅ | **How to check**: where Today says a plant is due a check, one line on how to tell whether it needs water: a finger in the soil, a wooden skewer, lifting the pot, or the meter | 4 | S | v1.x |
| ✅ | **A daily round**: Today suggests the room that has gone longest without anything logged, working round the home a room a day. It can be put off to show another room or skipped for the day. Off until turned on in Settings | 6 | S | v1.x |
| ✅ | **Going round**: open a room or spot and go through its plants one card at a time, then its propagations: a meter reading, watered, looked over, a photo, or skip. Opening it again the same day starts at the first one with nothing logged. Looked over goes on the history. Started from Today's round, Go round these in Water, the Plants list filtered to a room, or Rooms and tags | 4 | M | v1.x |
| ☐ | **Your own walking order** for a round, set per room by dragging the cards | 6 | S | v1.x |
| ✅ | **Away or quiet time**: set the dates you're away, or a quiet stretch of a week, two weeks, a month or a date with no reason asked, under Away and quiet time in Settings. Today and the photo reminder stop asking during it, a bar shows how far in you are, and I'm back ends it early. A week before an away stretch, Today mentions it. The watering reminder leaves a gap across a stretch out of what it learns unless something was watered in it, so a two-week trip isn't learned as how long a plant goes. On the way back, Today opens with a short list of the plants to look at first, as things to check and never to water. It never says overdue, and 3, 5, 10 or all can be set. Water and Worth a look come back when the list is done or after three days | 3 | M | v1.x |
| ✅ | **Do it later**: a flag on a plant or propagation can be set to start from a month or a day, or the next round day. It stays off Today until then, shows greyed out on the page with Change and Remove, and works like the flag for needing attention from that day. One flag per plant, and it doesn't repeat | 4 | S | v1.x |
| ☐ | **A note for next year**: a note on a plant that comes back on Today on the same date next year, or on a date you pick, e.g. "thrips showed up in May" or "repot before it starts growing". It shows next to A year ago today | 4 | S | v1.x |
| ✅ | **Round days**: pick the weekdays you go round your plants under Round days in Settings. A watering check comes up on the round day before it is due, after Still wet it waits for the next round day, and a plant that dries out faster than the gap keeps its own days. Propagations waiting for a look are gathered too. On other days Today says how many wait for the next round, with Go round now. Pests, flags and quarantine keep their own days, and the daily round only shows on round days | 3 | M | v1.x |
| ✅ | **A year ago today**: Today shows a plant's photo from this day a year ago, or six months ago, next to its newest one, with Before and after a tap away | 3 | S | v1.x |
| ☐ | **Anniversaries and small celebrations**: Today says when you have had a plant for a year, and rooting, a first new leaf or a cutting promoted to a plant gets a short moment of its own | 6 | S | v1.x |
| ✅ | **How long you've had a plant** instead of "day 430" on its photos: "1 yr 2 mo", counted from when you got it | 3 | S | v1.x |
| ✅ | **Put A year ago today above Worth a look** on the days it shows, since it is the part people share | 3 | S | v1.x |
| ✅ | **Move What a plant has given under the plant's name** instead of behind Details, so cuttings taken, rooted and passed on show first | 3 | S | v1.x |
| ☐ | **Today in your calendar**: a private link to add to any calendar app, which then shows what is due. It works on every phone and in every browser without notifications, and turning it off makes the old link stop working | 8 | L | v2 |
| ✅ | Care intervals due (if set) | 4 | S | v1.x |
| ☐ | **The week ahead**: what is due over the next 7 days as well as today, grouped by day, with a way to mark a whole day as done. Most useful once care intervals exist | 7 | S | v1.x |

## 7. Data, backup & settings

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Light / dark / match-system theme | 1 | S | M1 |
| ✅ | All data on the device (IndexedDB), works offline, installable | 1 | L | M1–M2 |
| ✅ | Export everything to a ZIP (data + photos) and import it again | 1 | L | M5 |
| ✅ | **A backup before every restore**: restoring a backup first saves one of what is on the device now, so anything the restore removed can be brought back by restoring that one | 4 | S | v1.x |
| ☐ | **Tidy up**: a page in Settings listing what is worth a look, like plants with no genus, photo or room, a propagation whose parent was deleted on another device, two plants that may be the same, and tags, products or mixes nothing uses. Out of the way until it's needed | 7 | M | v1.x |
| ✅ | Ask the browser for persistent storage; show storage used | 1 | S | M5 |
| ✅ | Backup reminder ("last backup 30 days ago") | 2 | S | M5 |
| ✅ | Rooms and tags page in Settings: rename a room or a spot everywhere at once, and merge two names for the same room | 2 | M | v1.x |
| ✅ | **Settings behind a gear** at the top, as a short list: the account, the theme and photo reminder, then pages for rooms and tags, backup and storage, and the about pages. The last tab is **Supplies**, with the pots, soil mixes, products, feeds and treatment recipes, each saying what it holds or what it's for | 2 | M | v2 |
| ✅ | **"New version available" banner** with a Reload button, so the installed app never stays stuck on an old version | 1 | S | M5 |
| ✅ | "What's new" page and app version in Settings. The deploy sets the version to the publish date and the commit, and the page shows both. Entries are written by hand in plain language for the people using the app, grouped by month, not generated from commit or pull request titles | 5 | S | v1.x |
| ✅ | A link to the source code on GitHub under About in Settings | 5 | S | v1.x |
| ✅ | A title, description and picture for links to the app shared on Facebook and other sites | 5 | S | v1.x |
| ✅ | A picture of the app itself for shared links in place of the icon: a cutting and the plant it came from, in the size Facebook, Reddit and messaging apps show | 2 | S | v1.x |
| ✅ | **The front page says what the app does**: below the first screen, the life of one cutting from the first snip to a plant of its own, what else the app keeps, and answers to the first questions people have. Search engines and AI tools read it too. When the app has downloaded, it opens straight away for someone still at the top, and waits for a tap on Open for someone reading further down | 2 | S | v1.x |
| ✅ | **Found by search engines and AI tools**: Help and Privacy are served as plain pages they can read without running the app, rendered from the app's own pages on every deploy, with a sitemap, a robots.txt that lets every crawler in, an llms.txt made from the Help answers, a site icon Google accepts, and a description of the app as a free web app | 2 | S | v1.x |
| ✅ | Screenshots in the web manifest, so Android and Chrome show them when offering to install the app | 4 | S | v1.x |
| ☐ | A page in the app with what's built and what's coming, written for the people using it rather than copied from this list | 7 | S | v1.x |
| ✅ | **Import from a spreadsheet**: a CSV file, or cells pasted from Excel or Google Sheets. Each column is matched to a field from its heading, in English or Danish, and every match can be changed. A column that fits no field becomes a field of its own, named after the heading with the kind guessed (or goes into About it, or is left out), missing rooms are made, and last watered, fed and repotted dates go into the care log. Rows that look like a plant you have are left unticked, the app's own CSV export reads back in, and the result screen can undo the whole import, the new fields too | 5 | M | v1.x |
| ☐ | Import propagations from a spreadsheet, with the parent plant found by name | 7 | S | v1.x |
| ✅ | **Side menu on wide screens**: on a computer or a tablet held sideways, the tabs move to a menu down the side, with Help and Settings at the bottom of it. Plant and propagation pages show the details next to the history, the Plants and Propagations lists flow into columns, Today shows what needs doing next to the rest, Water plants flows its plants into columns, and sheets open as a dialog in the middle. Phones keep the tabs at the bottom | 5 | M | v1.x |
| ☐ | **Pest cases and problems in two columns on a wide screen**, like plant pages: the case and its facts on the left, the updates beside them | 6 | S | v1.x |
| ☐ | **Supplies as grids on a wide screen**: Pots, Products, Soil mixes, Feeds and Sellers flow into columns the way the Plants list does, instead of one long list in the middle | 6 | S | v1.x |
| ☐ | **Drop photos onto a plant on a computer**: drag one or more photo files onto a plant or propagation page, or onto its row in a list, to add them, the same as picking them with Photo | 5 | S | v1.x |
| ☐ | **Keyboard shortcuts**: N for a new plant, W to water, Esc to close a sheet, arrow keys through lists and `?` for a sheet that lists them all. Only `/` for Search exists so far | 6 | S | v1.x |
| ☐ | **Shift-click to select a range** of plants or propagations in Select and in the table, instead of picking them one at a time | 6 | S | v1.x |
| ☐ | **The list and a plant side by side**: on a wide screen, clicking a plant opens it in a pane next to the list, so you can go through plants without going back each time. Has to fit with the plant page's own two columns | 7 | M | v1.x |
| ☐ | **Settings in two panes on a wide screen**: the sections down the left and the open one on the right | 7 | S | v1.x |
| ☐ | **Contents down the side of the Help page** on a wide screen, so you can jump to a part of the app | 7 | S | v1.x |
| ✅ | CSV export of plants and propagations for spreadsheets | 6 | S | v1.x |
| ✅ | **Your photos in folders**: a download of every photo with one folder per plant and propagation, each file named by the date it was taken, so the photos are useful without the app. Separate from the backup, which is made for the app to read back | 4 | S | v1.x |
| ☐ | Units and date format settings | 7 | M | v1.x |
| ☐ | Danish translation | 7 | L | v1.x |

## 7b. Phone conveniences

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | **Home-screen shortcuts**: long-press the app icon for Add plant, New propagation, Water plants and Pest cases | 5 | S | v1.x |
| ☐ | **Quick actions**: long-press a plant card to log watering or a note without opening it | 6 | M | v1.x |
| ✅ | **Badge on the app icon** with the number of things due today | 7 | S | v1.x |
| ✅ | Pick photos from Google Photos, including ones only in the cloud, through Google's Photos Picker. Turned on in Settings. Photo on a plant or propagation page then asks phone or Google Photos, and the add and edit pages get a Google Photos button. The photos go straight from Google to the device. The phone's own chooser only reaches photos stored on the phone, which is why it needs Google's picker | 4 | S | v1.x |
| ☐ | **Copy a plant's link** for an NFC sticker or a label. Write it to a sticker with any NFC app, and tapping the phone on the pot opens the plant | 8 | S | v1.x |

## 7c. Help & first visit

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | **Help page** with short questions and answers: how the app stores everything on your device, what happens if you clear the browser, how to back up, how propagation and lineage work, how to install it on the phone, and a few answers for each part of the app. Each question folds open, the topics at the top jump to their section, and a search box leaves only the answers with every word typed, opening them when there are five or fewer. Opened from Settings and from the first screens someone new sees | 4 | S | v1.x |
| ✅ | Empty screens that say what to do next instead of just "nothing here": the first screen on Today says what the app is for and offers a plant or a propagation, a list with nothing matching offers to clear the search and filters, and the ones that use a word like feed or propagation say what it means | 4 | S | v1.x |
| ✅ | **The quickest ways to start on the first screen**: under "Add a plant" and "Start a propagation", a line for someone who has plants already, with links to Quick add and to starting a plant from old photos. That opens the add plant form with the photos first, so the plant's history begins on the first day | 2 | S | v1.x |
| ✅ | **A page that shows while the app loads**: what Stikling is in two sentences, two screens from the app and how far the download has got, in the page itself rather than the app, so the first visit shows something straight away and search engines can read it. The first visit takes about 26 seconds on slow mobile data and 7 on fast, and the page shows after 1.4 and 0.6. Someone coming back only sees the icon, and only if the app takes a moment to start. Links to the app show a picture of it in their preview | 2 | M | v1.x |
| ☐ | **Guided tour on the first visit**: a short walk through the app (plants, propagations, photos, backup) that can be skipped | 6 | M | v1.x |
| ☐ | Start the tour again from Settings whenever you want | 6 | S | v1.x |
| ☐ | Small info buttons next to the less obvious things, e.g. what "Pot up" does and what the stages mean | 4 | S | v1.x |
| ☐ | A demo plant and propagation that can be loaded and removed again, so the app can be tried out without adding real plants. They need fixed ids so a device that syncs doesn't get a second copy, and removing them removes their photos too | 3 | M | v1.x |

## 7d. Guides & references

Questions come up that the app can't answer from your own data: how long a cutting should take to root, what thrips damage looks like, how much perlite goes in a mix. There are two ways to handle it. Point at a good page somewhere else, or keep a short guide inside the app. Linking is much less work and there is nothing to maintain, so that comes first. Anything written into the app has to be kept correct afterwards.

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ☐ | **Reference links**: a short, checked list of outside pages (care by genus, pest identification, semi-hydro, soil), opened in the browser | 6 | S | v1.x |
| ☐ | Links where they apply: a pest case links to a page about that pest, a plant links to a care page for its genus | 7 | M | v1.x |
| ☐ | **Built-in guides** that work offline: taking a cutting, converting to semi-hydro, treating thrips, mixing soil. Short, part of the app, no backend and no network | 8 | L | v1.x |
| ☐ | A guide opens from the place it is about, e.g. the stages on a propagation link to the cutting guide | 8 | S | v1.x |

## 8. Sharing & later versions

| ✓ | Feature | Priority | Effort | When |
|---|---|---|---|---|
| ✅ | Share your plant list or wishlist as text, e.g. to send to a friend or take to a swap. The plant list shares what is shown (the search and filters, or the selected plants) with the botanical name and the everyday name in brackets, the same plants counted once, sorted by name, nicknames off unless switched on, and the spare mark. Never rooms, prices, where it came from or tags. The swap list shares the same way, and both end with a switchable "Kept in Stikling, stikling.app" line. The wishlist shares as text from its own page, with the same last line | 4 | S | v1.x |
| ✅ | **Share card**: a plant or propagation as one image, with its photo, its name and a plain line about it, like "Cutting from my Monstera, rooted in 18 days in LECA", and stikling.app small in a corner. Square, 4:5 or 9:16, to fit Instagram, TikTok and Reddit. Shared through the phone's share menu, or saved. The line is written from what the app keeps and can be changed before sharing, and nothing is saved. With no photo it is a card built around the big number, like days to root | 2 | M | v1.x |
| ✅ | **Plant-sitter sheet**: a page at /sitter-sheet to print, save as a PDF or share as text for whoever waters your plants while you're away, so nothing is left online. It is made for the Away stretch that is on or the next one, with the visits you pick (every day, every 2 or 3 days, twice or once a week). Plants and propagations are in the order you walk past them, room by room, with a photo, where it is, how it is watered, how often and a note for each. It says when to check a plant and never when to water it: the box at the top says to read the moisture meter, or push a finger into the soil and leave the plant alone unless it is dry all the way down, and only then gives the steps for watering from the bottom, from the top, soaking, or in your own words. A plant with a learned pace is checked on the visits near the days it usually needs water, and the rest on every visit. Three layouts: plant labels, visits first and a chart for the fridge. You choose what to show, leave plants off, and set how each plant is watered and a note for it on its edit form | 4 | M | v1.x |
| ☐ | Weigh a pot when it's dry and just after watering, so you or a plant-sitter can check by lifting it | 6 | S | v1.x |
| ☐ | **A plant's story to print**: one page with a plant's photos by date, its notes, where it came from and what has come from it, printed or saved as a PDF from the browser. For giving a plant away with its story, or for keeping. You choose what goes on it | 6 | M | v1.x |
| ✅ | **Your year with your plants**: a page at /year with the year's pictures as a deck to flick through: plants added and where they came from, how the propagations did, the quickest one to root, the medium that worked best, the plant photographed most, one plant's first and last photo of the year side by side, and a collage whose photos can be swapped. Each card comes in square, 4:5 and 9:16 and follows the share card, and any card can be left out before sharing one at a time or all at once. Earlier years are in a picker. Plants that died that year are listed under the cards and never put on one. In December, until the middle of January, Today shows the year with its first card, and a link at the bottom of Today and on the Results page opens it the rest of the time | 4 | M | v1.x |
| ☐ | **Your year as a video**: the year's cards played one after another as an MP4 for Reels and TikTok, made with the time-lapse encoder | 5 | S | v1.x |
| ✅ | Accounts and sync between devices (ASP.NET Core Web API, EF Core, sign-in through Clerk). The API checks Clerk sign-ins and has people, collections and members with roles, and the app can sign in and out from its Account page in Settings. Records, settings and photos sync between devices when signed in. Every device keeps all the thumbnails and fetches a full-size photo when it's opened, and each account has 1 GB for photos on the server. The Account page shows when it last synced and, once it's nearly full, how much photo space is used, a dot on the settings gear shows a sync problem, and signing out asks whether to keep the data on the device. Delete account, at the bottom of the Account page, erases everything on the server and the sign-in, and Download your data gives a ZIP of everything the server and Clerk hold about the person. When something is deleted, the server keeps only its id and dates. The app is at stikling.app, with the API on Azure Container Apps, Azure SQL and Blob Storage, and a privacy page | 9 | L | v2 |
| ☐ | **Add people to a collection**: screens for inviting someone and choosing their role. The first role is "can view", so someone can look at a collection without being able to change it. The API already has members and roles and checks them on every request, so this is the app side | 8 | L | v2 |
| ☐ | **Helper role: can log care**: someone in a collection who can log watering and notes but not edit or delete anything, for a plant-sitter or someone in the same household. Builds on the members and roles the API already has | 8 | M | v2 |
| ☐ | Someone leaving a shared collection, including when they are the only editor. The collection needs an editor left, so the app has to ask them to hand it over or to say what happens to it first, and nothing is deleted without them asking | 9 | M | v2 |
| ✅ | **Share link for one plant or propagation**: in the Share sheet, **Link** turns on a page for that plant only, at a link on share.stikling.app that can't be guessed. Anyone can open it without an account. The API serves it as a plain page rather than the app, so it opens quickly, works in the in-app browsers of TikTok and Instagram, and the link preview shows the cover photo. It tells the story oldest first: photos with their dates and day counts, milestones like first root and potted up, and the notes unless **Show my notes** is off. Photos can be left out with a tap, and the name and the line can be changed for the page. It follows the plant as it grows and never shows the room, prices, the shop or person it came from, who it went to, tags, pots or care. A plant potted up from a propagation starts its page with the rooting it came from, up to the day it was potted up, and marking a plant or a cutting as given away or sold offers to send the link right away, and once it is marked given away or sold, the page offers the friend a copy of it. Turning it off stops the page for good, and turning it on again makes a new link. Only an editor who is signed in can make one, a collection can have 50 at a time and a person can make 20 a day, the page asks search engines not to list it, and it ends with a link to start your own and a way to report it. The note box says when a note will be public, and Details on the plant's page links to the page | 3 | L | v2 |
| ✅ | **Pass a cutting on with its history**: giving a plant or cutting away offers the link for the friend. Once it is marked given away or sold, the share page has Keep its history in Stikling, which opens the app, and the friend needs no account. The app shows a preview and asks for a nickname, who gave it, the room and, for cuttings, how many. The photos, milestones and the notes the page shows are copied once to the device and kept under Before you got it on its page, read-only apart from framing the photos and picking the cover. The friend's own history starts at Arrived, above it. Deleting it and opening the link again offers Bring it back, and the friend's own share link shows that history first, so it carries on to whoever it is passed to | 4 | L | v2 |
| ☐ | **Compare lists with a friend**: open a friend's shared list and see which of their plants are on your wishlist, and which of yours are on theirs | 8 | M | v2 |
| ☐ | Public read-only collection page | 9 | L | v2 |
| ☐ | **Plant facts card** from Trefle: a short card with what is known about the species, shown on the plant's page. The data is under the ODbL, so the app has to credit it, and the Trefle API token has to stay on the server, so the app asks the API and never Trefle directly. It is never pushed on anyone: a small "About this species" link on the plant page opens it | 9 | L | v2 |
| ✅ | **Error tracking with Sentry**: errors in the app and the API are sent to Sentry, so they are seen without anyone having to report them. Reports are anonymous (no account id, IP address or anything typed in), the app's can be turned off with Send error reports in Settings, and the privacy page says what a report holds | 4 | M | v1.x |
| ☐ | Plant identification from a photo via an identification API, only with a free one. Pl@ntNet's API is the free one: up to 500 identifications a day for non-commercial use, and it doesn't keep the photos. plant.id only gives 100 free identifications before it charges, and iNaturalist doesn't open its full identification model to other apps. Two ways in: through Stikling's own API, which keeps the key secret and shares the daily allowance between everyone signed in, or with a free key of your own added in Settings, kept on the device and used without an account, if Pl@ntNet allows calls straight from the browser. The result is a suggestion for the name fields, never filled in on its own | 9 | L | v2 |
| ✅ | **Notifications** from the web app, now that there is a server: on Android, and on an iPhone once the app is on the home screen. Off until turned on, and only for what Today would show, like a propagation to check on or a treatment due. Everything due comes as one list a day, at a time you choose, never one notification per plant. Never a reminder to water | 4 | L | v2 |
| ☐ | Push notifications, widgets and a Google Play release (MAUI Blazor Hybrid) | 10 | L | v3 |
| ☐ | **Android APK** released through IzzyOnDroid first, and then F-Droid, so it can be installed without a paid store account. Both take open source apps, and each has its own rules for how a release is built and signed | 10 | L | v3 |

## 9. Quality (not features, but part of each milestone)

- Unit tests for every Core rule (promote/split counts, stage changes, lineage, filters).
- Accessibility: labels on every input, visible focus, enough contrast in both themes, tap targets ≥ 44 px.
- Performance: the list stays quick with 200+ plants and 1000+ photos (thumbnails, lazy loading).
- Database migrations: schema changes upgrade existing data and never wipe it.
- Every PR builds and tests in CI; `main` always deploys.
- Component tests for Razor components with bUnit.
- End-to-end tests with Playwright in CI (add a plant, promote a propagation, export/import).
- Lighthouse audit in CI (PWA, accessibility, performance) with a badge in the README.

