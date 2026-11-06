# Guides

The guides are short how-tos built into the app, at `/guides`. They work offline and say the same thing to everyone. This file is the text of each guide and where every part of it comes from. A change to a guide in the app is a change here too.

Part (a): taking a cutting, moving a plant to semi-hydro, watering through a wick, mixing soil. Pests and guides by plant group come later. The structure is in `Stikling.Core/Guides/GuideBook.cs`, the English and Danish in `Stikling.Web/Resources/Guides.resx` and `Guides.da.resx`, and the guides are at `/guides`.

In the app a line marked *growers* says "many growers" or "some growers" in the words of the line itself, and a line from a maker says who, like "COMPO says". The sources of a guide are listed at its end.

## The page (shape confirmed Nov 6)

- **Guides list** at `/guides`: Settings-style rows in bordered blocks under Label headings (Propagating, Growing media, later Pests and By plant group), in the 48rem column. Linked from Help (an answer and a link at the top), Supplies (a row next to Soil mixes) and Search (guide titles match).
- **A guide** at `/guides/{name}`: back link, Headline title, a lead paragraph, "You'll need" when there is one, shared steps numbered (number in Bricolage, Deep Moss; step in body text).
- **Methods** where growers disagree: a Label heading ("Root it in") over the existing segmented control on a phone, each method with why people pick it, its steps and what to watch for. From 64rem the page takes WideLayout and the methods sit side by side in columns at least 20rem wide, with no switch. Fixed order, nothing marked best.
- **Sources** at the end under "Written from": title and publisher, each a link that opens in the browser. Offline the names still show. *growers* lines read "many growers" or "some growers". A guide whose sources are thin has a "Where this comes from" line under the intro.
- **Opened from somewhere** (`?from=`): the back link names it ("Back to Monstera cutting"), falling back to Guides when the record is gone, and the switch starts on the matching method. Nothing else changes; the text is the same for everyone.
- **Semi-hydro and tracking**: opened from a plant in soil or water that isn't moving, the guide ends with "Track the move for Monstera", which opens the plant's existing start-tracking sheet. Already moving: says since when, with a link. From the list: a sentence that tracking starts from a plant's page.
- **Entry points**: quiet text links with a stroked open-book icon, at the places listed under each guide.
- **Built as**: text in `Resources/Guides.resx` and `Guides.da.resx`; each guide's structure and source ids in Core, with tests that every guide has sources and every key has Danish.
- **Left out on purpose**: pictures, ticks that save which steps are done, any "best" method, advice about a particular plant.

## Rules for the text

- Every step and every claim comes from a source below. The id in brackets after a line says which. A line marked *growers* comes from how growers describe their own practice and has no page behind it. Those lines say "many growers" or "some growers" in the app, never "you should".
- Where growers disagree on a method, the guide shows the common ones side by side and never calls one best. The methods are listed in the order given here, which is not a ranking.
- A guide never says when to water, feed or repot a particular plant. It says what to look at.
- Measurements are metric in both languages.

## Sources

| Id | Source |
|---|---|
| rhs-cuttings | Hunt, *How to take cuttings from houseplants*, Royal Horticultural Society. https://www.rhs.org.uk/plants/types/houseplants/how-to-take-cuttings-from-houseplants |
| rhs-media | RHS Editorial Team, *A guide to compost mixes for houseplants*, Royal Horticultural Society. https://www.rhs.org.uk/plants/types/houseplants/growing-media-houseplants |
| rhs-repot | *How to repot a plant*, Royal Horticultural Society. https://www.rhs.org.uk/container-gardening/how-to-repot-a-plant |
| rhs-cacti | *How to grow cacti and succulents*, Royal Horticultural Society. https://www.rhs.org.uk/plants/types/cacti-succulents/houseplants/growing-guide |
| illinois | *Cuttings*, Houseplants, University of Illinois Extension. https://extension.illinois.edu/houseplants/cuttings |
| unr | Robinson and Roberts, *Propagating Houseplants*, University of Nevada, Reno Extension, FS 98-92, 1998. https://extension.unr.edu/small-business-education/pub.aspx?PubID=3384 |
| uf-mix | *Homemade Potting Mix*, UF/IFAS Extension. https://sfyl.ifas.ufl.edu/lawn-and-garden/homemade-potting-mix/ |
| okstate | *Soilless Growing Mediums*, Oklahoma State University Extension, HLA-6728. https://extension.okstate.edu/fact-sheets/soilless-growing-mediums |
| lechuza | LECHUZA planting and watering instructions (the maker of PON). https://getpotted.com/upload/Lechuza%20Instructions.pdf |
| compo | COMPO, *Hydrokultur für Zimmerpflanzen?* (a maker of hydroculture fertiliser and substrate). https://www.compo.de/ratgeber/pflanzenpflege/grundlagen/erde-kompost/hydrokultur |
| cornell | Fischer and Fox, *Propagation of House Plants*, Cornell University, 1969, reprinted by Cornell Cooperative Extension Suffolk County, 2025. https://s3.amazonaws.com/assets.cce.cornell.edu/attachments/75540/Propagation_Of_Houseplants.pdf |
| pavlis | Pavlis, *Wick Watering: How It Works & Common Myths*, Garden Myths. Written by a gardener with a science background, drawing on substrate research in HortScience 49(6). https://www.gardenmyths.com/wick-watering-how-it-works-common-myths/ |
| costa | Costa Farms, *Wick & Grow* (the maker of a wick pot). https://help.costafarms.com/en/knowledge/wick-grow |
| uf-sphagnum | Hildinger, *Rooting Spider Plants in Sphagnum Moss*, Therapeutic Horticulture Activity Database, UF/IFAS, 2024. https://hos.ifas.ufl.edu/media/hosifasufledu/therapeutic-horticulture-activities-database/pdfs/Rooting-Spider-Plants-in-Sphagnum.pdf |

okstate and lechuza were read in full on Nov 6, before the first PR. okstate is a hydroponics fact sheet: it says expanded clay gives off almost no nutrients, drains and dries fast, and can be washed and reused. It says nothing about a plant drinking from a reservoir or about moving a houseplant, so it only backs the first. lechuza describes its own sub-irrigation planter: a layer of PON under potting soil, the plant set in the soil and watered from above for 12 weeks ("three months"), then drinking from the reservoir. The reservoir is refilled only once the indicator reads min, after a dry phase of about 1 week for small plants and 2 for larger ones, and it says to add liquid fertiliser with every second watering. It does not say that PON has fertiliser in it, and it is not a move to pure PON. The semi-hydro guide was changed to match (see its notes).

---

## Taking a cutting

Opens from: a propagation's stage button and its page, Propagations when empty, Guides.
The switch starts on: the propagation's medium (Water, Soil or Perlite as Cutting mix, Sphagnum).

**Intro.** A cutting is a piece of stem that grows its own roots and becomes a new plant. Most climbing and trailing houseplants, like pothos, philodendron and tradescantia, root easily this way. [rhs-cuttings]

**When.** Late spring and early summer, while the plant is growing, roots fastest, but cuttings root at most times of year. [rhs-cuttings, illinois] Rooting is slower in a cool room. *growers*

**You'll need.** Clean, sharp scissors or a knife, and a glass, pot or box for what it roots in. [rhs-cuttings]

**Steps**

1. Pick a healthy stem the plant can spare, with no pests on it. [rhs-cuttings]
2. Find the nodes: the joints where a leaf meets the stem, often with a bump or a small brown root. Roots grow from the nodes. [illinois, rhs-cuttings]
3. Cut just below a node, so the piece is about 8 to 10 cm long with two or more nodes. A tip cutting usually roots fastest. [rhs-cuttings, illinois, unr]
4. Take off the lower leaves and any flowers, and keep 2 or 3 leaves at the top. Leaves that would sit in water or compost rot there. [rhs-cuttings, illinois]
5. For succulents, cacti, pelargoniums and rubber plants, leave the cut end to dry and seal for a day or so first. [illinois]
6. Rooting powder is optional. Some sources dip the end in it, others don't use it, and easy plants like pothos root well without. [illinois, unr, rhs-cuttings]

**Root it in** (switch)

*Water*
- Why people pick it: you can see the roots come, and it needs nothing but a glass. [rhs-cuttings] *growers*
- Steps: stand the cutting in water with a node under the surface and the leaves above it. Keep it in a bright spot out of direct sun. Change the water every few days so it stays clean. [rhs-cuttings, unr]
- How long: about 10 days for the easiest, 3 to 4 weeks for most. [unr, rhs-cuttings]
- In winter, when cuttings root slowly, they may rot in water before they root, and a mix does better. [cornell]
- Watch for: cloudy or smelly water and a soft brown stem end, which is rot. Cut above it and start again in fresh water. *growers* Roots grown in water are used to water, so the move to compost is the risky part. See Potting it up. [unr]

*Cutting mix*
- Why people pick it: the roots grow in what the plant will live in, so there is no move from water to compost. *growers*
- Steps: fill a small pot with half peat-free multipurpose compost and half perlite, or equal parts peat and perlite, vermiculite or sand. Make a hole with a pencil, set the cutting in so a node is buried and the leaves sit just above the surface, and water well. [rhs-cuttings, unr, illinois, cornell]
- Keep the mix moist but not soggy. A clear bag over the pot, or a clear box or jar, keeps the air around it damp. Keep it out of direct sun, which heats it up. [rhs-cuttings, illinois, unr, cornell] Some growers open it now and then for air. *growers*
- How long: 1 to 2 weeks for soft stems, about 3 to 4 weeks for most. [illinois, rhs-cuttings] A gentle tug that meets resistance means roots. *growers*
- Watch for: mould on the surface or a blackened stem, from too wet or too little air. *growers*

*Sphagnum moss*
- Why people pick it: it holds water and air at the same time, and is popular for aroids and hoyas. *growers*
- Steps: wet the moss and put it in a clear cup, enough to cover the base of the cutting. Push the cutting gently in and cover the cup with plastic that doesn't touch the leaves. Keep it in bright light out of direct sun, and add water if the moss dries. [uf-sphagnum] Many growers squeeze the moss until it no longer drips. *growers*
- How long: about a month. [uf-sphagnum]
- Wear gloves: a rare skin infection has been linked to sphagnum moss, and a mask is advised when handling it dry. [uf-sphagnum]
- Watch for: moss kept dripping wet, which rots the stem. *growers*

**Potting it up**

Sources differ on how long to wait, and both ways are common:

- *Early*: pot it up once the roots are about 2.5 cm long. Short roots settle into compost more easily. [illinois] Many growers say the same. *growers*
- *Later*: wait for a good set of roots and new growth before potting up. [rhs-cuttings, unr]

Pot each one into a small pot, 8 to 10 cm, of peat-free multipurpose compost. [rhs-cuttings] Some growers keep the compost a little moister than usual for the first week or two while the roots get used to it, then water as normal. Others add a spoonful of compost to the water over a few days before the move. *growers*

**In Stikling.** Mark the propagation Rooting when the first root shows and Rooted when it is ready, and Pot up when it goes into a pot. The days it took are then counted for you.

---

## Moving a plant to semi-hydro

Opens from: a plant's medium set to LECA or PON, the "Moving to LECA" card on a plant page, Guides.
The switch starts on: Straight in. (It was planned to start on the medium it is moving to, but the only PON method is LECHUZA's soil over PON, which is not a move to PON.)

**Intro.** In semi-hydro a plant grows in clay pebbles (LECA) or a mix of mineral grains like PON instead of soil, and drinks from a reservoir of water at the bottom of the pot that the pebbles soak up. [rhs-media, okstate] The pebbles hold almost no nutrients, so the water carries the feed. [okstate] The move from soil can be tricky, and rotting roots are the main risk. [rhs-media]

**Where this comes from** (shown in the app under the intro). No horticultural society or university has written about moving a houseplant from soil to semi-hydro. This guide is how growers and the makers of PON and hydroculture fertiliser describe it, and each part says which.

**Which plants.** Young plants and cuttings make the move most easily. An old plant that has grown in soil for years finds it harder. [compo] Robust plants like pothos, spider plant and peace lily are the easiest to start with. [compo] Plants that like to dry out between waterings may not do well with water always at their roots. *growers*

**You'll need.**
- LECA or PON. [rhs-media, lechuza]
- A pot with holes inside one without, or a self-watering pot, so the water stands below the roots. *growers*
- A fertiliser made for hydroculture, since the pebbles give off almost no nutrients. [okstate, compo] (A line saying LECHUZA's PON has fertiliser in it was removed: lechuza says to add liquid fertiliser every second watering.)
- An opaque pot, since light on the water grows algae. *growers*

**Get the medium ready.** Rinse LECA until the water runs clear, to wash off the clay dust. Many growers soak it for a few hours too. Some growers rinse PON, others use it straight from the bag. *growers*

**Steps**

1. Take the plant out of its pot. [rhs-repot]
2. Get all the soil off the roots. [compo] Soaking the root ball in a bowl of water first loosens it, then work it loose with your fingers and rinse. Soil left between the pebbles packs down and rots. Some fine roots break off. *growers*
3. Trim any soft, brown or rotten roots. [rhs-repot]
4. Set the plant in the new pot and fill around the roots with the pebbles, at the depth it stood before. *growers*

**Two ways to set up the pot.** The pot of pebbles can stand in the water, or stand above it with a wick drawing the water up. Both are common. The wick way has its own guide, Watering through a wick. *growers*

**How to move it** (switch)

*Straight in*
- Why people pick it: one step, and the plant keeps its roots. *growers*
- How: move it straight into the pebbles after washing. Keep the water low at first, just touching the bottom of the pebbles, and change it often. *growers*
- Watch for: the old soil roots dying back over the first weeks while new white water roots grow. Some leaves may yellow. *growers*

*Water roots first*
- Why people pick it: the plant grows roots made for water before it goes into the pebbles, so less dies back. *growers*
- How: after washing, stand the roots in water, change it every few days, and move the plant into the pebbles once new white roots have grown. Mostly used for small plants and cuttings. *growers*
- Watch for: the same rot signs as in a glass: cloudy water and soft brown roots. *growers*

*Soil over PON (LECHUZA's way)*
- Why people pick it: it is how LECHUZA, the maker of PON, tells you to start a plant in its planters. [lechuza]
- How: LECHUZA's planters hold a layer of PON under potting soil, and the plant is set in the soil. For the first 3 months it is watered from above while the roots grow down, and only then drinks from the reservoir. This is not a move to PON alone. [lechuza]
- Watch for: refill the reservoir only once the indicator reads min, after a dry phase of about 1 week for small plants and 2 for larger ones. [lechuza]
- The switch opens on Straight in whatever the medium, since this method is not a move to pure PON.

**The water level**

Growers keep it at different heights, and all of these are common:

- About a third of the pot's height. *growers*
- Just touching the bottom of the pebbles, so they wick the water up. *growers*
- Some let the reservoir run empty for a few days before filling it again, so the roots get air. LECHUZA's instructions do the same, with a dry phase of about 1 to 2 weeks. Others keep it topped up all the time. [lechuza] *growers*

Never so high that the water reaches where the stem meets the roots, and the plant shouldn't stand in water all the time. [compo] *growers*

**While it settles**

- Change the water and flush the pebbles with fresh water often, rather than only topping up, so the roots get oxygen and salts don't build up. Once new roots are growing, every week or two. [compo] *growers*
- Feed: COMPO says a weak hydro feed each week while the plant is growing. Some growers start straight away, others wait until new roots show. [compo] *growers*
- It takes weeks to months before new roots and a new leaf show. *growers*
- Watch for: a sour smell, soft brown roots, or a white crust of salts on the pebbles. *growers*

**In Stikling.** Moving to semi-hydro is in the More menu on a plant's page. The tracking card then asks for Roots washed, First new water root and First new leaf, and Today asks for a look if nothing happens for a while. Opened from a plant, the guide ends with *Track the move for Monstera*, which opens the plant's tracking sheet.

COMPO's page is about hydroculture in general and also covers a mixed semi-hydro with some soil, which this guide leaves out. The one claim worth a second look is COMPO's weekly feed, since it sells the fertiliser.

---

## Watering through a wick

Opens from: a plant whose pot is a self-watering pot with a wick, the Pots page when adding or editing a wick pot, the semi-hydro guide, Guides.
The switch starts on: Semi-hydro when the plant is in LECA or PON, Soil otherwise.

**Intro.** In a wick pot the plant's pot stands above a reservoir of water, and a cord runs from the water up into the mix. The water climbs the cord the way it climbs a strip of kitchen paper, and the mix takes up what it can hold. [pavlis] It works with soil and with semi-hydro, and is used to water less often or while you are away. [costa, cornell] *growers*

**How wet it gets.** The wick can't make the mix wetter than the mix itself holds. A mix that holds a lot of water stays too wet in a wick pot, which is why the mix matters more than the wick. [pavlis] A thicker wick only brings the water faster, so a big pot needs a thicker wick or more than one. [pavlis]

**You'll need.**
- A pot with a hole in the bottom, and a container for the water that the pot can stand on or in without touching the water. [costa] Some self-watering pots come with both. [costa]
- A wick of synthetic cord, like nylon or acrylic yarn, or a strip of nylon stocking. Cotton and wool rot in the wet. [pavlis] *growers*

**Steps**

1. Thread the wick through the hole in the bottom of the pot, so a few centimetres lie along the bottom inside and the rest hangs below. [pavlis] *growers*
2. Wet the wick through before you start, so the water starts climbing at once. *growers*
3. Fill the pot with the mix and the plant, with the wick lying at the bottom. A wick pushed up into the middle makes the middle wetter. [pavlis]
4. Fill the container with water and stand the pot over it, so the wick hangs in the water but the bottom of the pot never touches it. [costa]
5. Water once from the top to settle the mix around the roots. *growers*

**What it grows in** (switch)

*Soil*
- Why people pick it: the plant stays in soil and simply gets its water from below. [costa]
- Mix: an ordinary potting mix stays too wet. Wick growers open it up with a lot of perlite, often half the mix or more. [pavlis]
- Watch for: the top of the mix staying dry on a plant with few roots. Water small plants and new cuttings from the top for a few weeks until the roots reach down. [pavlis]

*Semi-hydro*
- Why people pick it: the pebbles never stand in the water, so the roots always have air, and a slow drinker is less likely to rot. *growers*
- Mix: LECA alone wicks poorly, since the round pebbles barely touch. Many growers use PON, pumice or perlite, or mix some into the LECA. *growers*
- Watch for: a dry top layer, and roots that grow out through the hole into the water, which is fine. *growers*

**Looking after it**

- Fill the container before it runs dry. A dry wick stops drawing and has to be wetted again. [costa]
- Liquid feed can go in the water. [costa] Use it weaker than on the bottle, since it goes in every time. *growers*
- Water from the top now and then, which flushes out salts that build up when water only comes from below. [pavlis] *growers*
- Keep light off the water, or it grows algae. The colour of the wick doesn't matter. [pavlis]
- Change the wick if it goes slimy or stops drawing. *growers*

**Where growers disagree**

- *Wick or no wick in semi-hydro*: some growers wick every pot, others take the wicks out and let the pebbles stand in a shallow layer of water instead. *growers*
- *How thick*: some split a yarn into single strands for small pots. pavlis says thickness only changes how fast the water comes, not how wet the mix is. [pavlis] *growers*

**In Stikling.** A pot can be marked as a self-watering pot with a wick on the Pots page, and the watering reminder then learns how long the water lasts instead of how long the soil takes to dry.

(Shown in the app as the "Where this comes from" line: no RHS or extension page covers wick pots for houseplants beyond a mention in cornell. pavlis is the most careful page found, and it leans on research for how wet a mix stays. costa is a pot maker. The rest is grower practice.)

---

## Mixing soil

Opens from: Soil mixes (the intro, and the empty state), Guides.
The switch starts on: nothing; it opens on the first group.

**Intro.** A potting mix is a base that holds water with something added for air. Roots need both, and how much of each depends on the plant. [rhs-media, uf-mix] Compost also breaks down and packs tighter over time, which is why plants are moved into fresh mix every couple of years. [rhs-repot]

A mix also has to suit how you water: someone who waters often does better with a mix that drains fast, and someone who forgets with one that holds more. *growers*

**What goes in**

| Ingredient | What it does |
|---|---|
| Peat-free multipurpose compost | The usual base. [rhs-cuttings, rhs-repot] |
| Peat | Holds a lot of water and is acidic. Many people avoid it, since digging it harms peat bogs, and the RHS stopped selling it in 2019. [uf-mix, rhs-repot] |
| Coir | Coconut husk, used instead of peat. Holds water but lets the rest drain, and is close to neutral. [rhs-media, uf-mix] |
| Loam-based compost (John Innes No 2 in the UK) | Heavier, so tall plants don't tip over. [rhs-repot] |
| Bark | Air and drainage. The base for orchids. Use bark that has been aged. [rhs-media, uf-mix] |
| Perlite | Light white grains that add air and drainage. [rhs-media, uf-mix] |
| Pumice | Adds air and holds a little water. Good for thick roots. [rhs-media] |
| Grit or coarse sand | Drainage and weight. Fine sand makes the mix dense. [rhs-repot, uf-mix] |
| Vermiculite | Holds several times its weight in water. Packs down if pressed hard. [rhs-media, uf-mix] |
| Worm castings | A gentle feed that also holds water. [rhs-media] |
| Charcoal | Drainage. [rhs-media] |
| Sphagnum moss | Holds a lot of water. Used on top for plants that like it damp. [rhs-media] |

**Starting points** (switch)

Each is a starting point to change to suit the plant and how you water, not a fixed recipe. [uf-mix]

*Most foliage plants*
- 2 parts peat or coir, 1 part perlite, 1 part coarse sand. [uf-mix]
- Or 1 part peat or coir, 1 part bark, 1 part coarse sand. [uf-mix]

*Aroids* (monstera, philodendron, anthurium)
- A chunky mix for thick roots: coir with perlite, bark and pumice, and some worm castings and charcoal. [rhs-media]
- Many growers use a simpler one: about half multipurpose compost, with bark and perlite making up the rest. *growers*

*Cacti and succulents*
- Loam-based compost with up to 30 percent grit or fine gravel, or a ready-made cactus compost. [rhs-cacti]
- Or 2 parts soil, 1 part peat or coir, 1 part perlite, 1 part coarse sand. [uf-mix]

*Orchids*
- Mostly bark. Specialist orchid mixes are sold ready to use. [rhs-media]

**Steps**

1. Wet coir or peat before mixing. Dry peat is hard to wet again. *growers*
2. Measure by parts with the same scoop or cup each time, so the mix can be made again. *growers*
3. Mix in a tub or a bag until it looks even. *growers*
4. It should feel moist but not wet, and fall apart when squeezed. *growers*
5. Garden soil can go in a mix, but it may carry pests, disease and weed seeds unless it is sterilised first. [uf-mix]

**In Stikling.** Save a mix you like on Soil mixes, in parts, so you can pick it when you repot.

**Needs a source** for steps 1 to 4. They are common practice, but none of the pages above says them, so they are left out of the app. Only step 5, about garden soil, is in the guide, under What goes in. Add steps 1 to 4 when a page says them.
