# Guides

The guides are short how-tos built into the app, at `/guides`. They work offline and say the same thing to everyone. This file is the text of each guide and where every part of it comes from. A change to a guide in the app is a change here too.

Part (a): taking a cutting, moving a plant to semi-hydro, watering through a wick, mixing soil. Part (b): checking for pests, and a guide for each pest a case can be for. Guides by plant group come later. The structure is in `Stikling.Core/Guides/GuideBook.cs`, the English and Danish in `Stikling.Web/Resources/Guides.resx` and `Guides.da.resx`, and the guides are at `/guides`.

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
- **Left out on purpose**: ticks that save which steps are done, any "best" method, advice about a particular plant. Part (a) has no pictures. The pest guides have photos, since pests are hard to tell apart from words alone (see Photos below).

## Pest guides (shape confirmed Nov 11)

- **Which guides**: one for each pest a case can be for (thrips, spider mites, fungus gnats, mealybugs, scale, aphids, whitefly), and Checking for pests, which a case for Other opens. Listed under a Pests heading on `/guides`, Checking for pests first, then thrips, spider mites, fungus gnats, mealybugs, scale, aphids and whitefly.
- **Every pest guide** has the same parts: what you see, with photos; what it is mistaken for; why it comes back (the life cycle); what to do first; the ways to treat it, as methods side by side; when it is over; In Stikling.
- **Treatments are kinds, not products**: rinsing and wiping off, insecticidal soap and horticultural oil, natural enemies, and "a product sold for houseplants, used as its label says". No active ingredients or brand names, since what may be sold differs between countries. Natural enemies are named by species, since that is what is bought.
- **Links only**: the guides explain cases, treatment plans and traps and link to them, and start nothing.
- **Opens from**: a pest case's page (its pest's guide, or Checking for pests for Other), the new case form once a pest is picked (in a new tab, like the other edit form links), the Pests page and its empty state, the symptom check's Pests cause, and a plant's quarantine card. The back link names the case or the plant.

## Photos

Photos are in `wwwroot/img/guides/`, WebP, 800px on the long side, and listed here. Every one is from Wikimedia Commons under a licence that allows it in the app with credit. Under each photo the app shows its caption and a credit line, "Photo: author, licence", linking to the file's page. The licences ask for changes to be marked, so a cropped photo says "cropped" after the licence. Resizing alone is not marked.

| File | Guide | Caption | Author | Licence | Source |
|---|---|---|---|---|---|
| thrips-adult.webp | Thrips | A western flower thrips on a flower. The bar is 1 mm. | Jesse Rorabaugh | CC0 | https://commons.wikimedia.org/wiki/File:Frankliniella_occidentalis_14827630.jpg |
| spider-mite.webp | Spider mites | A spider mite in its red form, with strands of silk. | Gilles San Martin | CC BY-SA 2.0 | https://commons.wikimedia.org/wiki/File:Tetranychus_urticae_with_silk_threads.jpg |
| spider-mite-webbing.webp | Spider mites | Webbing in a bad case, here on a tomato. | Clemson University, USDA Cooperative Extension Slide Series, Bugwood.org | CC BY 3.0 US | https://commons.wikimedia.org/wiki/File:Solanum_lycopersicum_-_spider_mite_-Clemson_University_-_USDA.jpg |
| fungus-gnat-trap.webp | Fungus gnats | A yellow sticky trap in a houseplant's pot, full of fungus gnats. | Ich (Wikimedia Commons user) | CC BY-SA 4.0 | https://commons.wikimedia.org/wiki/File:Gelbsticker_mit_Trauerm%C3%BCcken.jpg |
| fungus-gnat.webp | Fungus gnats | An adult fungus gnat. | AfroBrazilian | CC BY-SA 3.0 | https://commons.wikimedia.org/wiki/File:Bradysia_sp._01.JPG |
| mealybug-colony.webp | Mealybugs | Mealybugs on a clivia leaf. | Sinikka Halme | CC BY-SA 4.0 | https://commons.wikimedia.org/wiki/File:Citrus_mealybug_Planococcus_citrus_on_Clivia_miniata_02.jpg |
| mealybug-long-tailed.webp | Mealybugs | A long-tailed mealybug on an orchid bud. | Gianni Del Bufalo | CC BY 2.0 | https://commons.wikimedia.org/wiki/File:Cocciniglia_-_Pseudococcus_longispinus_-_Mealybug_-Gianni_Del_Bufalo_bygdb.jpg |
| scale-ficus.webp | Scale | Brown soft scale on a ficus leaf, shiny with honeydew. | Whitney Cranshaw, Colorado State University, Bugwood.org | CC BY 3.0 US | https://commons.wikimedia.org/wiki/File:Coccus_hesperidum01.jpg |
| scale-single.webp | Scale | One brown soft scale beside a leaf's vein. | AfroBrazilian | CC BY-SA 3.0 | https://commons.wikimedia.org/wiki/File:Coccus_hesperidum_01.JPG |
| aphids.webp | Aphids | Aphids on a stem. | Jules Verne Times Two | CC BY-SA 4.0 | https://commons.wikimedia.org/wiki/File:Green_peach_aphid_(Myzus_persicae)_on_a_stem,_Lisbon,_Portugal_(approx._GPS_location)_julesvernex2.jpg |
| whitefly-adults.webp | Whitefly | Adult whiteflies on a squash leaf. | Pablo Oliveri (Pro Huerta) | CC BY-SA 2.5 | https://commons.wikimedia.org/wiki/File:Pro_Huerta_-_Cucurbita_maxima_var._zapallito_-_Trialeurodes_vaporariorum_01_adults.jpg |
| whitefly-young.webp | Whitefly | Young whiteflies and a ring of eggs on a leaf. | Tashkoskim | CC BY-SA 4.0 | https://commons.wikimedia.org/wiki/File:%D0%9B%D0%B0%D1%80%D0%B2%D0%B8_%D0%B8_%D1%98%D0%B0%D1%98%D1%86%D0%B0_%D0%BE%D0%B4_%D0%B1%D0%B5%D0%BB%D0%BE%D0%BA%D1%80%D0%B8%D0%BB%D0%BA%D0%B8.jpg |

Thrips has one photo. The only damage photo found, `Basilikum Thripsbefall-Schlaghecken.jpg`, is titled thrips damage but described as leafhopper damage, so it is left out. Checking for pests has none. The captions are checked against each file's description on Commons. The whitefly photos are on squash and pepper and the spider mite webbing on tomato, since no houseplant photos as clear were found. The species are the same ones found indoors.

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
| ucipm | *Pest Notes: Houseplant Problems*, UC ANR Publication 74172, University of California, updated 2020. The only one of the UC notes written for houseplants. https://ipm.ucanr.edu/home-and-landscape/houseplant-problems/ |
| ucipm-thrips | Bethke, Dreistadt and Varela, *Pest Notes: Thrips*, UC ANR Publication 7429, 2014. https://ipm.ucanr.edu/PMG/PESTNOTES/pn7429.html |
| ucipm-mites | Godfrey, *Pest Notes: Spider Mites*, UC ANR Publication 7405, 2011. https://ipm.ucanr.edu/PMG/PESTNOTES/pn7405.html |
| ucipm-gnats | Bethke and Dreistadt, *Pest Notes: Fungus Gnats*, UC ANR Publication 7448, 2013. https://ipm.ucanr.edu/PMG/PESTNOTES/pn7448.html |
| ucipm-mealybugs | Flint, *Pest Notes: Mealybugs*, UC ANR Publication 74174, 2016. https://ipm.ucanr.edu/PMG/PESTNOTES/pn74174.html |
| ucipm-scales | Kabashima and Dreistadt, *Pest Notes: Scales*, UC ANR Publication 7408, 2014. https://ipm.ucanr.edu/PMG/PESTNOTES/pn7408.html |
| ucipm-aphids | Flint, *Pest Notes: Aphids*, UC ANR Publication 7404, 2013. https://ipm.ucanr.edu/PMG/PESTNOTES/pn7404.html |
| ucipm-whiteflies | Flint, *Pest Notes: Whiteflies*, UC ANR Publication 7401, 2015. https://ipm.ucanr.edu/PMG/PESTNOTES/pn7401.html |
| rhs-sap | *Indoor plants: sap feeders*, Royal Horticultural Society. https://www.rhs.org.uk/biodiversity/indoor-plants-sap-feeding-insects |
| rhs-houseplants | *How to grow houseplants*, Royal Horticultural Society. https://www.rhs.org.uk/plants/types/houseplants/growing-guide |
| rhs-bio | *Biological control in the home garden*, Royal Horticultural Society. https://www.rhs.org.uk/prevention-protection/biological-control-garden |
| rhs-thrips | *Thrips*, Royal Horticultural Society. https://www.rhs.org.uk/biodiversity/thrips |
| rhs-mite | *Glasshouse red spider mite*, Royal Horticultural Society. https://www.rhs.org.uk/biodiversity/glasshouse-red-spider-mite |
| rhs-gnats | *Fungus gnats (sciarid flies)*, Royal Horticultural Society. https://www.rhs.org.uk/biodiversity/fungus-gnats |
| rhs-mealybug | *Mealybug (glasshouse)*, Royal Horticultural Society. https://www.rhs.org.uk/biodiversity/mealybug |
| rhs-scale | *Scale insects*, Royal Horticultural Society. https://www.rhs.org.uk/biodiversity/scale-insects |
| rhs-aphids | *Aphids*, Royal Horticultural Society. https://www.rhs.org.uk/biodiversity/aphids |
| rhs-whitefly | *Glasshouse whitefly*, Royal Horticultural Society. https://www.rhs.org.uk/biodiversity/glasshouse-whitefly |

The pest sources were read on Nov 11. The UC notes other than ucipm are written for gardens, landscapes and greenhouses, and the RHS pest pages mostly for gardens and glasshouses with a pointer to rhs-sap for indoor plants. The pest guides take only what holds indoors from them: what the pest looks like, its life cycle, and how sprays have to be used. Where a garden source and an indoor one differ, the guide says which says what. Temperatures in the UC notes are in Fahrenheit and are given here in Celsius (90°F as 32°C, 75°F as 24°C, 60 to 90°F as 16 to 32°C).

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
- Some let the reservoir run empty for a few days before filling it again, so the roots get air. LECHUZA's instructions do the same, with a dry phase of about 1 to 2 weeks. Others top it up so the level stays the same. [lechuza] *growers*

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

---

## Checking for pests

Opens from: a pest case for Other, the Pests page and its empty state, the symptom check's Pests cause, a plant's quarantine card, Guides.
The switch: none.

**Intro.** Most pests come into a home on a new plant, since many of them barely move on their own. Finding them early, while there are few, makes them much easier to deal with. [rhs-mealybug, ucipm-mealybugs, ucipm]

**New plants**

1. Look a plant over before you buy it or bring it home: the undersides of the leaves, where leaves meet the stem, the base, any flowers, and the roots, by sliding it out of its pot. [ucipm, rhs-thrips]
2. Keep it away from your other plants for a while, at least a month where you can, so eggs have time to hatch and show. [rhs-mealybug, rhs-whitefly, rhs-sap]
3. Look it over every week for the first several weeks. If nothing shows, once a month is enough. [ucipm]
4. Look again at a plant that comes back in from outdoors. [ucipm]

**How to look**

- Turn the leaves over. Most pests sit on the undersides and where leaves meet the stem. [ucipm, rhs-sap]
- A magnifier that enlarges 10 times shows mites and eggs you can't see otherwise. [ucipm, rhs-mite]
- Shake a shoot over a sheet of white paper. Specks that walk are mites or thrips. [rhs-thrips, ucipm-mites]
- Sticky leaves, or a black film on them, mean something is sucking the sap above. The film is sooty mould growing on the sugary droppings, and wipes off with a damp cloth. [rhs-sap, rhs-aphids, ucipm]

**Sticky traps.** Yellow traps catch fungus gnats and whitefly, and blue ones catch thrips. They show what is there and whether the numbers go up or down, but they don't clear a plant, and they catch harmless insects too. [rhs-sap, rhs-thrips, rhs-gnats, ucipm-whiteflies]

**Which pest is it?** (each name links to its guide)

| Pest | What gives it away |
|---|---|
| Thrips | Silvery patches dotted with tiny black droppings. [rhs-thrips] |
| Spider mites | Fine pale speckles, and fine webbing in a bad case. [rhs-mite] |
| Fungus gnats | Small dark flies around the pots. [rhs-houseplants] |
| Mealybugs | White cottony fluff in leaf joints and crevices. [rhs-sap] |
| Scale | Small brown bumps along the veins and stems that don't move. [rhs-houseplants] |
| Aphids | Clusters on new shoots and buds, and white cast skins. [rhs-sap] |
| Whitefly | Tiny white flies that rise from the leaves when the plant is touched. [rhs-whitefly] |

**Found something**

1. Move the plant away from the others. Wash your hands after handling it. [ucipm, rhs-sap]
2. Look over the plants that stood near it. [rhs-sap]
3. Remove fallen and dead leaves, which can carry pests and eggs. [rhs-mealybug]
4. A plant badly overrun, above all by mealybugs or scale, can be easier to replace than to clear. [rhs-sap, ucipm]

**Before you spray anything**

- Use only a product sold for houseplants, for that pest and that kind of plant, and follow its label. [ucipm]
- Spray outdoors if you can, and cover the undersides of the leaves and the stems. [ucipm]
- Try it on a few leaves a day or two first, to see that it doesn't mark them. [ucipm, ucipm-mites]
- Not on a plant that is dry and thirsty, or above 32°C. [ucipm-mites, ucipm-aphids]
- Soap and oil only kill what they touch, so it takes more than one round. [ucipm-aphids, ucipm-whiteflies]

**Natural enemies.** Predatory mites and insects can be bought by mail for most pests. They breed best from about 21°C and in good light, need the pest there to feed on, and most sprays kill them. The RHS says to leave a day after soap or oil before letting them out. [rhs-bio]

**In Stikling.** Settings can start quarantine when you add a bought, swapped or gifted plant. A check with nothing found can be logged from a plant's menu. When you find something, open a pest case for the plants it covers, and Today keeps track of the treatments.

---

## Thrips

Opens from: a thrips case, the new case form, Checking for pests, Guides.
The switch starts on: Rinse and cut off.

**Intro.** Thrips are slender insects up to about 2 mm long that feed on leaves and flowers. They are hard to see, so the damage is often what shows first. [rhs-thrips, ucipm]

**What you see**

- Leaves that turn dull, then silvery on top, dotted with tiny black droppings. [rhs-thrips, rhs-sap, ucipm]
- Pale flecks and scars, and new leaves and buds that grow twisted. [rhs-thrips, ucipm]
- Narrow insects, more often under the leaves: dark adults with fringed wings folded along the back, and paler young without wings. [rhs-thrips, rhs-sap]
- To find them, shake a shoot over white paper and look with a magnifier. [rhs-thrips, ucipm-thrips]

Photo: thrips-adult.webp

**Mistaken for.** Spider mites speckle leaves too, but leave fine webbing and no black droppings. [ucipm-mites, rhs-thrips]

**Why it comes back.** A female lays up to about 100 eggs, one or two a day, in young leaves and buds. The young feed, then rest in the mix or in sheltered spots on the plant before they grow wings, where sprays don't reach them. A generation takes about 24 to 35 days, and indoors they breed all year. [rhs-thrips]

**First**

1. Move the plant away from the others. [ucipm]
2. Cut off the worst leaves and any flowers with thrips in them. [ucipm, ucipm-thrips]
3. Hang a blue sticky trap above or among the plants, to see how many there are and whether that falls. [rhs-thrips, rhs-sap]

**Treat it with** (switch)

*Rinse and cut off*
- How: rinse the plant with water, the undersides too, and keep cutting off what is badly hit. [ucipm]
- Watch for: young growing again from the mix and buds. Rinsing reaches only what is on the leaves. [rhs-thrips]

*Soap or oil*
- How: insecticidal soap or horticultural oil, sprayed so it covers the buds, the shoot tips and the undersides of the leaves. [ucipm, ucipm-thrips]
- It only kills what it touches, so it takes more than one round. [ucipm-thrips]
- Watch for: marked leaves. Try it on a few leaves first. [ucipm]

*Natural enemies*
- What: the RHS lists predatory mites (Amblyseius, Hypoaspis and Macrocheles species) and the predatory bug Orius laevigatus, for under glass. [rhs-thrips, rhs-bio]
- UC IPM, writing about gardens, finds that bought predators rarely clear thrips on their own. [ucipm-thrips]
- Watch for: they need warmth, light and thrips to eat. See Checking for pests. [rhs-bio]

*A product for houseplants*
- How: a product sold for houseplants and for thrips, used as its label says. [ucipm]

**When it's over.** Leaves silvered by thrips stay silver after the thrips are gone, so look at the new leaves. [rhs-sap] Clean new growth and an empty trap for some weeks mean it is over. *growers*

**In Stikling.** A thrips case starts at 5 days between treatments, which you can change. Count the trap on the case to see whether the catch falls week by week, and once you stop treating, the case is watched until it has been clean long enough.

---

## Spider mites

Opens from: a spider mite case, the new case form, Checking for pests, Guides.
The switch starts on: Rinse and wipe.

**Intro.** Spider mites are tiny relatives of spiders, about half a millimetre long, that feed from the undersides of leaves. They do most harm in warm, dry rooms. [ucipm, rhs-mite]

**What you see**

- Fine pale speckles on the top of the leaves, which can turn yellow or bronze before the leaves dry and drop. [rhs-mite, ucipm-mites]
- Fine silk webbing between leaves and stems in a bad case. [rhs-mite, ucipm]
- Under the leaves, tiny oval mites, yellowish green with two dark spots, or orange red in winter and when crowded, with round eggs. A magnifier that enlarges 10 times helps. [rhs-mite, ucipm]
- Shake a shoot over white paper. Specks that walk are mites. [ucipm-mites]

Photo: spider-mite.webp
Photo: spider-mite-webbing.webp

**Mistaken for.** Thrips speckle leaves too, but leave tiny black droppings and no webbing. [ucipm-mites, rhs-thrips]

**Why it comes back.** From egg to adult takes about 12 days at 21°C and about 55 at 10°C, so a warm room speeds them up. Dust and a plant short of water make it worse. The damage often shows after the mites have built up. [rhs-mite, ucipm, ucipm-mites]

**First**

1. Move the plant away from the others, and look over the plants that stood near it. [ucipm, rhs-sap]
2. Cut off the worst leaves. [ucipm]

**Treat it with** (switch)

*Rinse and wipe*
- How: rinse the leaves often, above all the undersides, and wipe off dust. UC IPM, writing about gardens, sprays a plant with mites at least once a day. [ucipm, ucipm-mites]
- Misting and moist air help keep them down, but don't clear them on their own. [rhs-mite]

*Soap or oil*
- How: insecticidal soap or horticultural oil, reaching the undersides of the leaves. UC IPM's houseplant note keeps this for when rinsing hasn't worked. [ucipm, ucipm-mites]
- It only kills what it touches, so it takes more than one round. [ucipm-mites]
- Watch for: marked leaves. Try it on a few leaves first, and not on a dry plant or above 32°C. [ucipm-mites]

*Natural enemies*
- What: Phytoseiulus persimilis is the predatory mite most used, and eats every stage. Amblyseius species, often A. californicus, do less against a heavy attack but can be let out early and last longer. [rhs-mite, rhs-bio]
- Watch for: they need mites to eat and die out without them. [ucipm-mites]

*A product for houseplants*
- How: a product sold for houseplants and for mites, used as its label says. [ucipm]

**If it doesn't stop.** If they keep coming back or get worse, UC IPM says to throw the plant out, and the RHS says the same of a plant badly infested in late summer. [ucipm, rhs-sap]

**When it's over.** No new speckles on new leaves, and nothing walking on the paper for some weeks. *growers*

**In Stikling.** A spider mite case starts at 4 days between treatments, which you can change. Mites don't fly into sticky traps, so the trap is behind a small link on the case.

---

## Fungus gnats

Opens from: a fungus gnat case, the new case form, Checking for pests, Guides.
The switch starts on: Sticky traps.

**Intro.** Fungus gnats are small dark flies, 3 to 4 mm long, that run over the mix and fly slowly around the pots. The adults don't harm plants. Their larvae live in the mix and mostly eat dead roots, rotting bits and fungi. [rhs-gnats, ucipm-gnats]

**What you see**

- Small dark flies on the mix, on the plant and at the windows. [rhs-gnats, ucipm-gnats]
- In the top of the mix, slender white larvae up to 6 mm long with shiny black heads, and slime trails in very wet pots. [rhs-gnats, ucipm-gnats]
- To check for larvae, lay a piece of raw potato on the mix, cut side down, and look under it after a few days. [ucipm-gnats]

Photo: fungus-gnat-trap.webp
Photo: fungus-gnat.webp

**Does it harm the plant?** Seedlings and soft cuttings can be harmed. A plant with a few years of roots rarely is. [rhs-gnats] Many larvae in a wet mix rich in organic matter can stunt young plants, but a wilting houseplant is more often down to watering or root rot than to gnats. [ucipm-gnats]

**Mistaken for.** Shore flies look much the same but have short, bristly antennae. [ucipm-gnats]

**Why it comes back.** Eggs laid on the mix hatch in a few days, and the larvae turn into adults in the mix. From egg to adult takes about 17 days at 24°C, and indoors they breed all year. [ucipm-gnats, rhs-gnats]

**First**

1. They breed in mix that stays wet. Letting the top of the mix dry between waterings, where the plant can take it, and emptying water left in saucers, cuts their numbers. [ucipm, ucipm-gnats, rhs-gnats]
2. Clear dead leaves and rotting bits off the pots. [ucipm, ucipm-gnats]
3. When you repot, use fresh houseplant compost rather than old, which can hold eggs and larvae, and keep open bags closed. [rhs-gnats, ucipm-gnats]
4. A mix with perlite or sand in it drains faster and stays wet for less time. See Mixing soil. [ucipm-gnats]

**Treat it with** (switch)

*Sticky traps*
- How: yellow sticky traps, cut into squares and stuck in the pots, catch the adults. [ucipm-gnats, rhs-gnats]
- Watch for: they cut the numbers but don't end it while larvae are in the mix, and they catch helpful insects too. [ucipm-gnats, rhs-gnats]

*Bacteria in the water*
- How: Bti (Bacillus thuringiensis israelensis), watered into the mix, kills the larvae. [ucipm-gnats, ucipm]
- It doesn't last indoors, so it is repeated about every 5 days. [ucipm-gnats]

*Nematodes*
- How: Steinernema feltiae, tiny worms watered into the mix, which kill the larvae. [ucipm-gnats, rhs-gnats, rhs-bio]
- They need a moist mix and 16 to 32°C, can breed in the mix and last, and are sold by mail and keep only a short time. [ucipm-gnats, rhs-bio]

*Predatory mites*
- How: the RHS lists the mites Hypoaspis miles and Macrocheles robustulus and the rove beetle Atheta coriaria, put in the mix to eat eggs, larvae and pupae. [rhs-gnats, rhs-bio]
- UC IPM finds Hypoaspis mites do poorly in a single houseplant and doesn't suggest them for homes. [ucipm-gnats]

No spray is listed: UC IPM says other insecticides are rarely needed against fungus gnats in a home. [ucipm, ucipm-gnats]

**When it's over.** No new flies on the trap for some weeks, and no larvae under the potato. *growers*

**In Stikling.** A fungus gnat case starts at 7 days between treatments, which you can change. Count the trap on the case to see whether the catch falls week by week.

---

## Mealybugs

Opens from: a mealybug case, the new case form, Checking for pests, Guides.
The switch starts on: Wipe off.

**Intro.** Mealybugs are soft, oval insects up to 4 mm long, hidden under white wax, that suck sap in tight spots. Females hardly move, so they nearly always come in on a new plant. [rhs-mealybug, ucipm-mealybugs]

**What you see**

- White cottony fluff in leaf joints, leaf sheaths and other crevices, with insects or orange pink eggs under it. [rhs-sap, rhs-mealybug]
- Sticky leaves, and black sooty mould on them. [rhs-mealybug]
- A plant that grows slowly, yellows and drops leaves. [rhs-mealybug, ucipm]
- Long-tailed mealybugs have two tail threads longer than the body. [ucipm-mealybugs]
- Root mealybugs live on the roots, with white wax on them, and are usually only found when repotting. [rhs-mealybug, ucipm-mealybugs]

Photo: mealybug-colony.webp
Photo: mealybug-long-tailed.webp

**Mistaken for.** Some scale insects lay eggs under white wax, and some aphids have a white waxy coat. [rhs-scale, rhs-aphids] The larvae of the mealybug ladybird, sold to eat them, look like large mealybugs. [rhs-mealybug]

**Why it comes back.** Females lay 100 to 200 eggs over 10 to 20 days in a cottony sac, and the young grow up in about a month in summer, all year indoors. The wax keeps sprays off, and they hide where sprays don't reach, so no single treatment gets them all. [ucipm-mealybugs, rhs-mealybug] The RHS says it is often impossible to get rid of them completely. [rhs-sap]

**First**

1. Move the plant away from the others. [ucipm, rhs-sap]
2. Look for egg sacs on the pot, stakes and saucer, and remove dead leaves, which can carry eggs. [ucipm-mealybugs, rhs-mealybug]

**Treat it with** (switch)

*Wipe off*
- How: pick them off, or dab each one with a cotton bud dipped in rubbing alcohol (isopropyl, 70% or weaker). On a sturdy plant, a strong spray of water every few days washes off those in the open. [ucipm, ucipm-mealybugs]
- Watch for: try the alcohol on a small part of the plant a day or two before, since it can burn leaves, and keep it away from flames. [ucipm, ucipm-mealybugs]

*Spray*
- How: insecticidal soap, horticultural oil or neem oil, sprayed onto the insects. They work best on the young, which have less wax. [ucipm, ucipm-mealybugs]
- For more than a few, UC IPM gives a spray of 10 to 25% rubbing alcohol in water, repeated weekly until they are gone. [ucipm-mealybugs]
- Watch for: try it on a few leaves first. [ucipm-mealybugs]

*Natural enemies*
- What: the mealybug ladybird Cryptolaemus montrouzieri, and parasitic wasps such as Leptomastix, which only attacks citrus mealybug. [rhs-mealybug, ucipm-mealybugs, rhs-bio]
- Watch for: they need warmth and do best from May to September. The ladybird needs egg sacs to breed, so it is let out when there are many. [rhs-mealybug, ucipm-mealybugs]

*A product for houseplants*
- How: a product sold for houseplants and for mealybugs, used as its label says. Products that go in the soil work less well against mealybugs than against other sap suckers. [ucipm, ucipm-mealybugs]

**If it doesn't stop.** A badly infested plant is often better thrown out than treated again and again. [ucipm-mealybugs, rhs-sap]

**When it's over.** No new fluff for some weeks, looking in the crevices and at the roots when you repot. *growers*

**In Stikling.** A mealybug case starts at 7 days between treatments, which you can change.

---

## Scale

Opens from: a scale case, the new case form, Checking for pests, Guides.
The switch starts on: Scrape off.

**Intro.** Scale insects settle on a leaf or stem, stop moving and grow a waxy or hard shell. They look like small bumps, which makes them easy to miss. [ucipm-scales, rhs-scale]

**What you see**

- Small, light brown bumps shaped like limpets on the stems and along the veins under the leaves. Brown soft scale is common on houseplants. [rhs-houseplants, rhs-sap, ucipm]
- Sticky, shiny leaves and black sooty mould on them. [rhs-sap, ucipm]
- Yellowing leaves and poor growth. [ucipm, rhs-scale]
- Tiny yellow or orange young that walk, before they settle. [ucipm-scales]
- On ferns, scale can look like the spore clusters under the fronds. [ucipm]

Photo: scale-ficus.webp
Photo: scale-single.webp

**Soft and armoured.** Soft scale is rounded, its shell is its body, and it makes sticky honeydew. Armoured scale is flat, its cover lifts off and leaves the insect behind, it makes no honeydew, and sprays reach it less well. [ucipm-scales]

**Alive or dead?** Dead scales stay stuck to the plant. Squash a few: live ones leave a wet smear, dead ones don't. Clean new growth is the sign a treatment worked. [ucipm-scales, rhs-scale, rhs-sap]

**Why it comes back.** The young spread and settle within a day or two, and indoors scale breeds all year, so every stage is there at once. Sprays mostly kill the young. [ucipm-scales, rhs-sap]

**First**

1. Move the plant away from the others. [ucipm]
2. Cut off stems and leaves that are covered. [ucipm, ucipm-scales]

**Treat it with** (switch)

*Scrape off*
- How: scrape or wipe the adults off, and wash the young off with water. [ucipm, rhs-scale]
- Watch for: on a big plant this may not keep up. [rhs-scale]

*Soap or oil*
- How: insecticidal soap or horticultural oil, which work best on the small young. Cover the plant well. [ucipm, ucipm-scales]
- Since there are young all year indoors, it takes more than one round. [rhs-sap]

*Natural enemies*
- What: for soft scale, parasitic wasps such as Metaphycus helvolus, and the nematode Steinernema feltiae. For armoured scale indoors, the ladybirds Chilocorus nigritus and Rhyzobius lophanthae. [rhs-bio, rhs-scale]

*A product for houseplants*
- How: a product sold for houseplants and for scale, used as its label says. One that goes in the soil works against some soft scales. [ucipm]

**A few is fine.** A healthy plant copes with a few scale insects, so not every one has to go. [rhs-scale] One badly overrun can be easier to replace. [rhs-sap]

**When it's over.** New leaves and stems that stay clean for some weeks. [rhs-sap]

**In Stikling.** A scale case starts at 7 days between treatments, which you can change.

---

## Aphids

Opens from: an aphid case, the new case form, Checking for pests, Guides.
The switch starts on: Squash and rinse.

**Intro.** Aphids are soft, pear-shaped insects, 1 to 7 mm long, green, black, yellow, pink or white, that live in colonies on new growth. Unlike most pests, they are easy to see. [rhs-aphids, ucipm-aphids]

**What you see**

- Clusters on shoot tips, buds and under young leaves. [rhs-aphids, rhs-sap]
- White cast skins on the tops of the leaves. [rhs-sap, rhs-aphids]
- Sticky leaves, and black sooty mould on them. [rhs-aphids, ucipm]
- New leaves that curl and twist. [rhs-aphids, ucipm]
- Two small tubes sticking out from the back end, which no other pest has. [ucipm-aphids]

Photo: aphids.webp

**Mistaken for.** White specks on top of the leaves are cast aphid skins, not whitefly. Aphids with a white waxy coat can look like mealybugs. [rhs-whitefly, rhs-aphids]

**Why it comes back.** Indoors they are mostly females that give birth to live young without mating, up to a dozen a day, and in warmth a newborn is an adult in about a week. Winged ones appear when a colony is crowded and fly to other plants. [ucipm-aphids, rhs-aphids] Even so, heavy damage to houseplants is uncommon. [rhs-aphids]

**First**

1. Move the plant away from the others. [ucipm]
2. Look over the shoot tips of the plants near it. [rhs-aphids]

**Treat it with** (switch)

*Squash and rinse*
- How: squash colonies with your fingers, rinse them off with water, and cut off tips that are covered. [rhs-aphids, rhs-sap, ucipm]
- Sooty mould wipes off with a damp cloth. [rhs-aphids]

*Soap or oil*
- How: insecticidal soap, horticultural oil or neem oil. [ucipm, ucipm-aphids]
- They only kill the aphids they touch, so it takes more than one round. [ucipm-aphids]
- Watch for: try it on a few leaves first, and not on a dry plant or above 32°C. [ucipm-aphids]

*Natural enemies*
- What: lacewing larvae, the predatory midge Aphidoletes aphidimyza and parasitic wasps such as Aphidius. [rhs-bio, rhs-sap]

*A product for houseplants*
- How: a product sold for houseplants and for aphids, used as its label says. UC IPM's houseplant note keeps one that goes in the soil for last. [ucipm]

**When it's over.** No new colonies on the shoot tips for some weeks. *growers*

**In Stikling.** An aphid case starts at 5 days between treatments, which you can change.

---

## Whitefly

Opens from: a whitefly case, the new case form, Checking for pests, Guides.
The switch starts on: Sticky traps.

**Intro.** Whiteflies are tiny white winged insects, about 1.5 mm long, that rise in a cloud when the plant is touched. Their young are flat and sit still under the leaves. [rhs-whitefly, ucipm-whiteflies]

**What you see**

- Small white flies that rise from the leaves. [rhs-whitefly, ucipm-whiteflies]
- Under the leaves, flat, oval, creamy young that look like scale, and grey white eggs, often in a ring. [rhs-whitefly, ucipm-whiteflies]
- Sticky leaves, and black sooty mould on them. [rhs-whitefly, ucipm]
- Leaves that yellow and drop. Poinsettias often have them. [ucipm]
- Black young are dead, killed by a parasitic wasp. [rhs-whitefly]

Photo: whitefly-adults.webp
Photo: whitefly-young.webp

**Mistaken for.** White specks on top of the leaves are more likely cast aphid skins. [rhs-whitefly]

**Why it comes back.** Each female lays over 200 eggs, without needing a male, and from egg to adult takes about three weeks at 21°C. Indoors they breed all year. They quickly stop dying from most sprays, apart from soap and oil. [rhs-whitefly, ucipm-whiteflies]

**First**

1. Move the plant away from the others. [ucipm]
2. Remove the worst leaves. [ucipm, ucipm-whiteflies]

**Treat it with** (switch)

*Sticky traps*
- How: a yellow sticky trap in each pot catches adults. [ucipm]
- Watch for: they show how many there are and cut the numbers somewhat, but don't clear a plant. [ucipm-whiteflies, rhs-whitefly]

*Rinse*
- How: wash the eggs and young off the undersides of the leaves with water. [ucipm, ucipm-whiteflies]

*Soap or oil*
- How: insecticidal soap or horticultural oil, covering the undersides of the leaves well, repeated, and best in the early evening. [ucipm, ucipm-whiteflies]
- Watch for: not on a dry plant or above 32°C. [ucipm-whiteflies]

*Natural enemies*
- What: the parasitic wasp Encarsia formosa, let out before there are many, since it doesn't work at once. The young it kills turn black, so you can see it working. The predatory mite Amblyseius andersoni eats the eggs. [rhs-whitefly, rhs-bio]

**If it doesn't stop.** UC IPM says a plant with a large infestation may be best thrown out. [ucipm]

**When it's over.** No adults rising and nothing new on the trap for some weeks. *growers*

**In Stikling.** A whitefly case starts at 5 days between treatments, which you can change. Count the trap on the case to see whether the catch falls week by week.

---

## Notes on the pest guides

- **Intervals.** The days between treatments a case starts with (`PestTraits.UsualIntervalDays`) are not from these sources, and the guides don't claim they are. None of the sources contradicts them. The only intervals the sources give are Bti about every 5 days (ucipm-gnats), an alcohol spray weekly for mealybugs (ucipm-mealybugs) and a water spray at least daily for mites in gardens (ucipm-mites). The guides say those, and leave the app's numbers as starting points.
- **Kinds, not products.** UC IPM names active ingredients and brands (for thrips, mealybugs, scale, aphids and whitefly), and the RHS names some, but both say to try other ways first. The guides name none. Rubbing alcohol, neem oil, Bti and the natural enemies are named as kinds of treatment.
- **Fungus gnats and watering.** "Let the top of the mix dry between waterings, where the plant can take it" is the one line in these guides about watering. It tells people to wait longer, never to water sooner, and both sources say it. The RHS adds "as long as plant health isn't affected", which is where "where the plant can take it" comes from.
- **The "growers" lines** are only the "When it's over" lines, since no source says when a pest is gone for good. They say what to look for, not a number of weeks, and the case's Clean for how long count (4 weeks unless changed) is what the app uses.
- **Left out.** Broad and cyclamen mites (in ucipm, too small to see and diagnosed from damage), ants, fumigants and anything sprayed into the air indoors.
