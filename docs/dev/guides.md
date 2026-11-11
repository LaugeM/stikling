# Guides

The guides are short how-tos built into the app, at `/guides`. They work offline and say the same thing to everyone. This file is the text of each guide and where every part of it comes from. A change to a guide in the app is a change here too.

Part (a): taking a cutting, moving a plant to semi-hydro, watering through a wick, mixing soil. Part (b): checking for pests, and a guide for each pest a case can be for. Part (c): a guide for each of nine plant groups (aroids, begonias, bromeliads and air plants, cacti and succulents, carnivorous plants, ferns, hoyas, orchids, prayer plants). The structure is in `Stikling.Core/Guides/GuideBook.cs`, the English and Danish in `Stikling.Web/Resources/Guides.resx` and `Guides.da.resx`, and the guides are at `/guides`.

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

## Plant group guides (shape confirmed Nov 11)

- **Which guides**: aroids, begonias, bromeliads and air plants, cacti and succulents, carnivorous plants, ferns, hoyas, orchids and prayer plants, listed under a By plant group heading on `/guides` in that order (alphabetical in English; the order is the same in Danish).
- **What's special only**: the plant page already shows the light, water and humidity a plant usually likes (`tools/plant-names/care.txt`), so a group guide only says what differs from an ordinary houseplant. Every one has the same parts, leaving out any a group has nothing special for: the lead (what the group is), What to look at (signs to read instead of a schedule), Mix and pot, the group's own topic (climbing, flowers, rest, water, cups and pups and so on), Making new plants, Problems (linking the pest guides), In Stikling.
- **What to look at, never how often**: like the other guides, these never say how often to water or feed. Where a source gives a schedule ("water weekly"), the guide takes only the sign it is checked by.
- **Which plants a guide is for** is decided by the plant's genus, through its family in `plant-regions.json` where the dictionary has one, and a list of genera where it doesn't. The lists are under each guide below. A genus the dictionary only knows by an old name is looked up by the name it goes by now.
- **Opens from**: a plant's page whose genus is in a group (a small guide link under What it likes), the dictionary's genus page for a genus in a group, and Guides. The back link names the plant or the genus.
- **Photos** only where words fall short: a hoya's peduncle, an orchid keiki and the spore cases under a fern. The rest have none (see Photos below).
- **The growers lines** were checked against threads in r/hoyas, r/orchids, r/Monstera, r/alocasia, r/succulents, r/SavageGarden, r/tillandsia, r/calatheas, r/ferns and r/begonias and the r/succulents wiki, read Nov 11. They are not listed as sources in the app, like all growers lines.

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
| hoya-peduncle.webp | Hoyas | The old flower peduncle of a Hoya carnosa, still on the stem. | MPF (Wikimedia Commons user) | CC BY 2.5 | https://commons.wikimedia.org/wiki/File:Hoya_carnosa1.jpg |
| orchid-keiki.webp | Orchids | A Phalaenopsis keiki, joined to the mother plant by the flower stem. | Dominique Lyon | CC0 | https://commons.wikimedia.org/wiki/File:Phalaenopsis_keiki.jpg |
| fern-sori.webp | Ferns | The spore cases (sori) on the underside of the fronds of Nephrolepis cordifolia. | Ixitixel | CC BY-SA 3.0 | https://commons.wikimedia.org/wiki/File:Nephrolepis_cordifolia_(Sori).jpg |

Thrips has one photo. The only damage photo found, `Basilikum Thripsbefall-Schlaghecken.jpg`, is titled thrips damage but described as leafhopper damage, so it is left out. Checking for pests has none. The captions are checked against each file's description on Commons. The whitefly photos are on squash and pepper and the spider mite webbing on tomato, since no houseplant photos as clear were found. The species are the same ones found indoors.

The plant group guides have three photos: a hoya's old peduncle, an orchid keiki and the spore cases of a fern. Left out, since no photo under CC0, CC BY or CC BY-SA was found on Commons that shows the subject: bromeliad pups, the scales on an air plant and a stretched succulent. A begonia photo (`Begonia_acetosa_propagation.jpg`) was left out too: it shows whole leaves rooting with their stalks, while the guide's leaf cuttings have the stalk cut off and the veins cut. The hoya photo names no author on Commons beyond its uploader, MPF, and its description says "Persistent flower peduncle". The fern is Nephrolepis cordifolia, and the keiki photo is of a plant out of its pot on paving, since that was the clearest licensed one found.

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
| rhs-monstera | *How to grow Swiss cheese plants*, Royal Horticultural Society. https://www.rhs.org.uk/plants/swiss-cheese-plants/how-to-grow-swiss-cheese-plants |
| rhs-philodendron | *How to grow philodendrons*, Royal Horticultural Society. https://www.rhs.org.uk/plants/philodendron/growing-guide |
| rhs-alocasia | *How to grow alocasias*, Royal Horticultural Society. https://www.rhs.org.uk/plants/alocasia/growing-guide |
| iastate-aroids | Haynes, *A is for Aroids!*, Iowa State University Extension and Outreach, 2021. https://yardandgarden.extension.iastate.edu/article/2021/01/aroids |
| iastate-philodendron | Steil, *Growing Philodendrons at Home*, Iowa State University Extension and Outreach, reviewed 2025. https://yardandgarden.extension.iastate.edu/how-to/growing-philodendrons-home |
| rhs-hoya | *How to grow hoya*, Royal Horticultural Society. https://www.rhs.org.uk/plants/hoya/how-to-grow |
| iastate-hoya | Steil, *All About Hoyas*, Iowa State University Extension and Outreach, reviewed 2026. https://yardandgarden.extension.iastate.edu/how-to/all-about-hoyas |
| psu-hoya | Smith, *Hoyas as Houseplants*, Penn State Extension, 2026. https://extension.psu.edu/hoyas-as-houseplants |
| rhs-phal | *How to grow Phalaenopsis (moth orchids)*, Royal Horticultural Society. https://www.rhs.org.uk/plants/phalaenopsis/growing-guide |
| rhs-orchids | Richards, *Top tips for growing orchids at home*, Royal Horticultural Society. https://www.rhs.org.uk/plants/types/houseplants/top-tips-for-growing-orchids-at-home |
| aos-phal | Sullivan, *Novice Culture Sheet: Phalaenopsis*, American Orchid Society, 2012. https://secure.aos.org/media/Content-Images/PDFs/Phalaenopsis_novice.pdf |
| iastate-succulents | Steil, *Growing Succulents Indoors*, Iowa State University Extension and Outreach, reviewed 2024. https://yardandgarden.extension.iastate.edu/how-to/growing-succulents-indoors |
| illinois-succulents | Anderson, *Propagating Succulents and Cacti*, University of Illinois Extension, 2015. https://extension.illinois.edu/blogs/hort-home-landscape/2015-06-04-propagating-succulents-and-cacti |
| rhs-carnivorous | *How to grow carnivorous plants*, Royal Horticultural Society. https://www.rhs.org.uk/plants/types/carnivorous/growing-guide |
| rhs-flytrap | *Venus fly trap*, Royal Horticultural Society. https://www.rhs.org.uk/plants/venus-fly-trap |
| icps-flytrap | Brittnacher, *Growing Dionaea muscipula*, International Carnivorous Plant Society, updated 2019. https://www.carnivorousplants.org/grow/guides/Dionaea |
| icps-nepenthes | Brittnacher, *Growing Nepenthes*, International Carnivorous Plant Society. https://www.carnivorousplants.org/grow/guides/Nepenthes |
| rhs-bromeliads | *How to grow bromeliads*, Royal Horticultural Society. https://www.rhs.org.uk/plants/bromeliads/growing-guide |
| rhs-airplants | *Air plants*, Royal Horticultural Society. https://www.rhs.org.uk/plants/types/houseplants/air-plants |
| uf-bromeliads | *Bromeliads*, UF/IFAS Gardening Solutions. https://gardeningsolutions.ifas.ufl.edu/plants/ornamentals/bromeliads/ |
| rhs-calathea | *How to grow calatheas*, Royal Horticultural Society. https://www.rhs.org.uk/plants/calathea/growing-guide |
| illinois-prayer | *Prayer Plant*, Houseplants, University of Illinois Extension. https://extension.illinois.edu/houseplants/prayer-plant |
| umd-prayer | *Prayer plant leaves turning yellow/brown*, an answer from the Home and Garden Information Center, University of Maryland Extension, on Ask Extension, 2023. https://ask.extension.org/kb/faq.php?id=852330 |
| rhs-ferns | *How to grow epiphytic ferns*, Royal Horticultural Society. https://www.rhs.org.uk/plants/epiphytic-ferns/how-to-grow-epiphytic-ferns |
| rhs-tender-ferns | *Tender ferns*, Royal Horticultural Society. https://www.rhs.org.uk/plants/types/ferns/tender-ferns-houseplants |
| clemson-ferns | Russ, Pertuit and Smith, *Indoor Ferns*, HGIC 1505, Clemson Cooperative Extension, revised 2015. https://hgic.clemson.edu/factsheet/indoor-ferns/ |
| rhs-begonias | *Begonias: houseplants*, Royal Horticultural Society. https://www.rhs.org.uk/plants/begonias/houseplants |
| iastate-begonias | Haynes, *Growing Begonias in Iowa*, Iowa State University Extension and Outreach, reviewed 2024. https://yardandgarden.extension.iastate.edu/how-to/growing-begonias-iowa |
| iastate-leafvein | Steil, *How to Propagate Houseplants by Leaf Vein Cuttings*, Iowa State University Extension and Outreach, reviewed 2025. https://yardandgarden.extension.iastate.edu/how-to/how-propagate-houseplants-leaf-vein-cuttings |

The plant group sources were read on Nov 11. The RHS growing guides carry no author or date. The ICPS Nepenthes page is about growing from seed, and the guide takes only its line on which ones make houseplants. umd-prayer is one extension answer to one grower's question; the guide takes only its general points. Temperatures in Fahrenheit are given in Celsius.

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

---

## Aroids

For: the family Araceae (monstera, philodendron, alocasia, anthurium, pothos, syngonium, aglaonema, ZZ plant, peace lily and the rest).
Opens from: the plant's page, the genus page, Guides.
The switch starts on: Moss pole.

**Intro.** Aroids are the arum family, more than 100 genera, with monstera, philodendron, alocasia, anthurium, pothos and peace lily among them. Most have a flower made of a spike inside a hood, but only a few grown indoors have showy ones. [iastate-aroids] Many climb trees in the wild and root onto them as they go. [rhs-philodendron]

**What to look at**

- Most do well when the compost dries a little between waterings. The RHS waits until the top has dried for philodendrons, until it is almost dry for monsteras, and until the top 5 cm is dry for alocasias. [iastate-aroids, rhs-philodendron, rhs-monstera, rhs-alocasia]
- Yellowing leaves and a plant that collapses point to roots rotting in wet compost. [rhs-philodendron, rhs-monstera]
- Brown leaf edges point to dry air. [rhs-monstera, rhs-alocasia]
- Long stems with fewer, smaller leaves mean too little light. A monstera in low light makes leaves with fewer holes. [rhs-philodendron, rhs-monstera]
- Direct sun can scorch the leaves. [rhs-philodendron, rhs-monstera, rhs-alocasia]

**Mix and pot**

- An open mix that drains well. The RHS mixes 2 parts orchid compost with 1 part peat-free ericaceous compost for monsteras and philodendrons, and 3 parts peat-free compost with 1 part grit for alocasias. Iowa State uses an ordinary potting mix for philodendrons. See Mixing soil. [rhs-monstera, rhs-philodendron, rhs-alocasia, iastate-philodendron]
- A pot only a few centimetres bigger than the root ball, since a much bigger one stays wet and rots the roots. They do well slightly pot-bound. [rhs-philodendron, rhs-monstera, iastate-aroids]
- A heavy pot for a big climber or a large alocasia, which get top-heavy. [rhs-monstera, rhs-alocasia]

**Give it something to climb** (switch)

Bushy kinds need no support. [rhs-philodendron] For climbers, all of these are common:

*Moss pole*
- Why people pick it: the stems can root into the moss, and a plant that climbs often makes bigger leaves, with more holes on a monstera. [rhs-monstera, rhs-philodendron]
- How: tie the stems to the pole and keep the moss moist, or the roots won't grow into it. [rhs-monstera, rhs-philodendron]
- Watch for: a plant that outgrows the pole. Many growers add a piece on top before it does. *growers*

*Stake or trellis*
- Why people pick it: it holds the plant up without a pole to keep moist. [iastate-philodendron]
- How: tie the stems on, and guide the aerial roots down into the pot, where they root and help hold the plant. [iastate-philodendron] Many growers say a plant with its aerial roots in the pot does fine without a moss pole. *growers*

*Let it trail*
- Why people pick it: climbing kinds like heartleaf philodendron and pothos hang well from a basket or a shelf. [iastate-philodendron]
- Watch for: trailing stems usually keep smaller leaves than climbing ones. [rhs-monstera]

**Aerial roots.** The roots that grow from the stems are how the plant climbs in the wild. They can be left, guided onto a pole or into the pot, or cut off close to the stem, which doesn't harm the plant. [iastate-philodendron, rhs-monstera]

**Winter rest for alocasias.** Alocasias rest in winter. Kept cooler and drier, but above 10°C, they may lose some of their leaves, which is normal, and grow new ones in spring. Wet compost while they rest rots the roots. [rhs-alocasia] Many growers find some lose every leaf and go back to a bare corm or rhizome, which can sit still for months before it grows again. *growers*

**Making new plants**

- Stem cuttings with a node, rooted in water or a mix. See Taking a cutting. [rhs-philodendron, rhs-monstera, iastate-philodendron]
- Offsets, the small plants at the base of a grown alocasia or a bushy philodendron, cut off with some root and potted on their own. [rhs-alocasia, iastate-philodendron]
- Pieces of an alocasia's rhizome, in late spring or early summer. [rhs-alocasia]
- The small corms found around an alocasia's roots when it is repotted. Many growers start them in damp moss or perlite in a closed box somewhere warm. *growers*

**Gloves.** The sap irritates skin, and the plants are poisonous if eaten. Wear gloves when cutting them, and keep them away from children and pets. [rhs-philodendron, rhs-alocasia, iastate-philodendron]

**Problems.** Mealybugs, spider mites and scale. Spider mites like dry air, which alocasias often get. [rhs-philodendron, rhs-alocasia, rhs-monstera] (Each pest links to its guide.)

**In Stikling.** When an alocasia drops its leaves for winter, mark it Dormant on its page, so it shows as resting rather than dead. A corm, an offset or a cutting can be tracked as a propagation from its parent.

---

## Begonias

For: the family Begoniaceae.
Opens from: the plant's page, the genus page, Guides.
The switch starts on: Leaf cuttings.

**Intro.** Begonias grown indoors come in a few kinds that are looked after differently: cane begonias with tall, bamboo-like stems, often called angel wings; rhizomatous ones like rex begonias, which grow from a creeping stem and are kept for their leaves; fibrous-rooted ones with upright stems; and semi-tuberous ones sold in flower. [rhs-begonias, iastate-begonias] Cane begonias are the easiest indoors. [iastate-begonias]

**What to look at**

- Brown leaf edges: dry air, or too warm. [rhs-begonias]
- Rotting or dropping leaves: air that is too damp, too much water or too little light. [rhs-begonias]
- Flower buds that drop: dry air or too little water. [rhs-begonias]
- The RHS says most begonias brought to it were overwatered or kept in air that was too dry. Roots rot quickly in compost that stays wet. [rhs-begonias, iastate-begonias] Many growers let a rex begonia dry until the pot feels light. *growers*

**Keep the leaves dry.** Don't wet the leaves or the middle of a rex or other rhizomatous begonia, and don't mist the fibrous kinds. Air that is damp and still brings powdery mildew and grey mould, so give them air as well as humidity. [rhs-begonias]

**Humidity.** The RHS gives rex and rhizomatous begonias high humidity in summer and stands cane and fibrous ones on a tray of damp gravel. [rhs-begonias] Many growers find cane begonias manage in ordinary room air, and keep the rex begonias for a humid spot. *growers*

**Mix and pot.** Peat-free multipurpose compost for cane and fibrous begonias. [rhs-begonias] Many growers keep them in a pot on the small side. *growers*

**Making new plants** (switch)

*Leaf cuttings*
- For: rex and other rhizomatous begonias. [rhs-begonias, iastate-leafvein]
- How: take a firm, newly grown leaf and cut off its stalk. Cut through the main veins on the underside, about 2.5 cm apart, and lay the leaf flat on a sandy cutting mix with the cuts touching it, held down with pins or small stones. Water it, cover it with a bag or dome that doesn't seal tight, and keep it warm and bright, out of direct sun. [rhs-begonias, iastate-leafvein]
- How long: small plants appear at the cuts in 6 to 8 weeks. Pot them up once they have two or three leaves. [iastate-leafvein, rhs-begonias]

*Stem cuttings*
- For: cane and fibrous begonias. [rhs-begonias]
- How: a piece 7 to 10 cm long cut just below a node, rooted in a sandy cutting mix, or in water. [rhs-begonias, iastate-begonias]

*Division*
- For: rhizomatous begonias. [rhs-begonias]
- How: in spring, divide the rhizome so each piece has one or more growing points. [rhs-begonias]

**Rest.** Semi-tuberous begonias rest for about six weeks after flowering, cooler and with less water, then are cut back. Rex and cane begonias only get less water in winter. [rhs-begonias]

**Problems.** Aphids and spider mites, and powdery mildew and grey mould. [rhs-begonias] (Each pest links to its guide.)

**In Stikling.** A leaf cutting can be tracked as a propagation of the kind Leaf, and a bag or dome as a rooting aid, so the results page shows whether it helped.


---

## Bromeliads and air plants

For: the family Bromeliaceae.
Opens from: the plant's page, the genus page, Guides.
The switch starts on: Soaking.

**Intro.** Bromeliads grow as a rosette of leaves, and many hold water in a cup in the middle. Air plants (Tillandsia) are bromeliads too, and most live with no compost at all. [rhs-bromeliads, rhs-airplants] They take up water and food mainly through their leaves and the cup, and the roots mostly hold the plant in place. [uf-bromeliads]

**What to look at**

- Long, thin, greener leaves mean too little light, and shorter, thicker, paler ones a lot. Bleached or brown patches are scorch from strong sun. [uf-bromeliads, rhs-bromeliads]
- Brown leaf tips and rotting roots: too much water, or a pot standing wet. [rhs-bromeliads]
- An air plant whose middle or leaves turn brown or black is rotting, mostly from water left in the middle with too little air around it. [rhs-airplants]
- An air plant that dries out was watered too seldom, too warm or in too dry air. An overnight soak can sometimes bring it back. [rhs-airplants] Many growers say the leaves curl more when it is dry. *growers*

**The cup.** Keep the cup in the middle filled, with rainwater or distilled water if you can, and empty it and fill it fresh every month or two so it doesn't go stale. Keep the compost damp but not wet. [rhs-bromeliads] Fresh water in the cup also keeps mosquito larvae out. [uf-bromeliads]

**Mix and pot.** A loose mix, like equal parts fine bark, grit and coir, or peat, bark and coarse sand. Most need only a small pot, 7.5 to 12.5 cm. [rhs-bromeliads, uf-bromeliads]

**Air plants**

- No compost. Fix them to wood, shells or stone with wire, a tie or glue. [rhs-airplants]
- Rainwater. Hard tap water leaves a crust on the small scales that take up water. [rhs-airplants]
- Let them dry after watering, turned upside down or shaken, so no water sits in the middle. [rhs-airplants] Many growers make sure they are dry within a few hours. *growers*
- Not below 12°C. [rhs-airplants]

**Watering an air plant** (switch)

*Soaking*
- Why people pick it: the whole plant gets wet at once. The RHS waters this way. [rhs-airplants]
- How: put the plant under rainwater for a while, then let it dry. [rhs-airplants] Many growers soak for 20 to 30 minutes. *growers*
- Watch for: water left in the middle. Some growers find the thick, silvery kinds, like xerographica, rot from soaking. *growers*

*Misting*
- Why people pick it: less water stays in the middle. Some growers mist the silvery kinds rather than soak them. *growers*
- How: mist the whole plant. The RHS mists only between soaks, in warm weather, and says misting alone is not enough. [rhs-airplants]

**Flowers and pups.** The colourful part is a bract that lasts for months. Most bromeliads flower only once and then slowly die, after making pups at the base. [rhs-bromeliads, rhs-airplants] A grown bromeliad that won't flower can be put in a large clear bag with a few ripe apples or bananas for up to two weeks. [rhs-bromeliads]

**Making new plants.** Cut a pup off once it is about a third the size of its parent, in spring, and pot it up or treat it as a grown air plant. With only one pup, the old rosette can be cut away after flowering instead. [rhs-bromeliads, rhs-airplants]

**Problems.** Mealybugs and scale. [rhs-bromeliads] (Each pest links to its guide.)

**In Stikling.** A pup with its own roots is a plant: add it with its parent picked. Before that it can be tracked as a propagation of the kind Offset. The flowering log under More on the plant's page keeps when it flowered.


---

## Cacti and succulents

For: the families Cactaceae, Crassulaceae and Aizoaceae, and the genera Aloe, Haworthia, Haworthiopsis, Gasteria, Agave and Curio.
Opens from: the plant's page, the genus page, Guides.
The switch starts on: Offsets.

**Intro.** Cacti and succulents store water in thick leaves or stems. They need a lot of light, a mix that drains fast and compost that dries between waterings. [rhs-cacti, iastate-succulents]

**What to look at**

- Wrinkled or drying lower leaves, or a shrivelling plant: too dry. [iastate-succulents, rhs-cacti]
- Yellowing leaves, soft stems, blisters or rot: too wet. [iastate-succulents, rhs-cacti]
- Pale, stretched growth that leans to the window: too little light. Turning the pot keeps it even. [iastate-succulents, rhs-cacti]
- Wait until the compost is dry before watering, then water so it runs out of the bottom, and never leave the pot standing in water. [iastate-succulents, rhs-cacti]
- A white, chalky crust on the leaves comes from hard tap water. The RHS uses rainwater. [rhs-cacti]

**Forest cacti are different.** Christmas cactus, orchid cactus (Epiphyllum) and Rhipsalis like semi-shade, an east or west window, and more water than other cacti. A Christmas cactus flowers in winter and rests in summer. [rhs-cacti]

**Mix and pot**

- A gritty mix: a ready-made cactus compost, or loam-based compost with up to 30% grit. Iowa State uses 1 part potting soil, bark or coir to 2 parts perlite, sand, pumice or gravel. See Mixing soil. [rhs-cacti, iastate-succulents]
- A pot with a hole in the bottom, only just big enough. Clay dries faster. [iastate-succulents, rhs-cacti]
- Hold a spiny plant with a band of folded newspaper or thick gloves. [rhs-cacti, iastate-succulents]

**Winter rest.** Most rest from November to early March. The RHS keeps them at 8 to 10°C at night and gives them only enough water to stop them shrivelling, and a cool, dry, bright rest helps them flower. [rhs-cacti]

**Stretched plants.** A plant that has stretched stays stretched. Many growers cut off the top, let it dry, root it as a cutting, and leave the stem, which often grows new heads. *growers*

**Making new plants** (switch)

*Offsets*
- For: kinds that grow in clumps or rosettes, like aloe. [rhs-cacti, illinois-succulents]
- How: cut off an offset once it has roots of its own, and pot it up. [rhs-cacti, illinois-succulents] Many growers wait until it is about a third the size of the plant, and let the cut dry for a few days first. *growers*

*Stem cuttings*
- For: branching plants and columnar cacti. [rhs-cacti]
- How: cut a piece of stem, leave it to dry and seal for a day to a week, then set it in a gritty mix kept only just moist. [rhs-cacti, illinois-succulents]
- How long: roots in a few weeks to a month. A gentle tug that meets resistance means roots. [illinois-succulents]

*Leaves*
- For: many succulents, like echeveria and crassula. [rhs-cacti] Many growers find that aloe, haworthia and the string-of plants rarely or never grow from a single leaf. *growers*
- How: let a leaf dry like a cutting and lay it on the mix. [illinois-succulents] Many growers twist it off whole, so it breaks cleanly where it met the stem. *growers*
- How long: a small plant forms at the base in one to two months, and the old leaf shrivels away. [illinois-succulents]

*Seed*
- How: sow in spring on a gritty mix, cover small seeds only with sand, keep it at about 21°C, bright but out of direct sun, under glass or a clear bag. Seedlings can take up to a year to be big enough to pot. [rhs-cacti]

**Problems.** Mealybugs, spider mites and scale. Cold causes dead or discoloured patches. [rhs-cacti] (Each pest links to its guide.)

**In Stikling.** Set the growing season to Rests in winter under What it likes, and Today asks "Gone dormant?" when its rest comes round. Leaves, cuttings, offsets and seed can each be tracked as a propagation.

---

## Carnivorous plants

For: the families Droseraceae, Sarraceniaceae, Nepenthaceae, Lentibulariaceae and Cephalotaceae, and the genera Dionaea, Drosera, Sarracenia, Nepenthes, Pinguicula, Utricularia, Darlingtonia, Heliamphora and Cephalotus.
Opens from: the plant's page, the genus page, Guides.
The switch starts on: A cool rest.

**Intro.** Carnivorous plants grow in bogs and other places too poor for most plants, and catch insects for what the soil lacks. The easiest indoors are the Cape sundew, the Venus flytrap and the purple pitcher plant. Tropical pitcher plants (Nepenthes) need warmth and humid air. [rhs-carnivorous]

**Water.** Rainwater, distilled water or reverse osmosis water. Tap water can kill them, above all hard water. [rhs-carnivorous, rhs-flytrap, icps-flytrap] The ICPS uses tap water only when it has under 90 ppm of dissolved salts, and says an ordinary water filter doesn't take them out. [icps-flytrap]

**Standing in water.** Most stand in a saucer of water while they grow. The RHS keeps up to 2.5 cm in it, and the ICPS keeps the water at least 5 cm below the surface of the mix. [rhs-carnivorous, icps-flytrap] In winter the RHS waters from below and tips the rest away. [rhs-carnivorous]

**Mix and pot**

- A mix with almost no food in it, never potting compost or garden soil, and no fertiliser. [rhs-carnivorous, icps-flytrap]
- The ICPS uses peat and sand 1 to 1, or pure sphagnum, and no perlite. The RHS avoids peat and uses sphagnum with fine pine bark. [icps-flytrap, rhs-carnivorous]
- Keep a new plant in its pot and mix for the first year. [rhs-carnivorous]

**Food**

- Most catch enough on their own. [rhs-carnivorous] Indoors, where there is little to catch, the ICPS feeds flytraps live insects or soaked dried bloodworms. [icps-flytrap]
- A trap only digests something that moves, so a dead insect needs the trap gently pressed after it closes. [rhs-flytrap, icps-flytrap]
- A flytrap's trap closes only a few times before it dies, so don't set them off for fun. [rhs-flytrap]

**Light.** The RHS keeps them bright but out of direct sun behind glass, which can scorch them. A flytrap in poor light makes weak traps, and in strong light they turn red. [rhs-carnivorous, rhs-flytrap] The ICPS finds a windowsill too dark for a flytrap and grows them under lights. [icps-flytrap] Pitcher plants with thick, waxy leaves do well in a window with some direct sun. [icps-nepenthes]

**Winter for a flytrap** (switch)

*A cool rest*
- Why people pick it: the RHS gives flytraps and many other carnivorous plants a rest in winter. [rhs-carnivorous, rhs-flytrap]
- How: a cooler spot in winter, not a warm room, but above 10°C indoors, and less water, given only when the mix starts to dry. Some lose leaves. [rhs-carnivorous, rhs-flytrap]

*Growing on*
- Why people pick it: the ICPS has grown flytraps indoors under lights for years without a rest, and says one is only needed for plants outdoors that freeze. [icps-flytrap] Many growers do the same, and others still give a rest. *growers*
- How: under lights on a timer, kept the same all year. [icps-flytrap]
- Watch for: a rest in the fridge for a plant indoors does nothing, the ICPS says. [icps-flytrap]

Many growers say tropical pitcher plants don't rest at all. *growers*

**Flowers.** The RHS cuts off flowers once they fade. [rhs-carnivorous] Many growers let flytraps flower and say it doesn't kill them. *growers*

**Making new plants**

- Flytraps: divide a large clump with four or more crowns. [rhs-carnivorous]
- Sundews and butterworts: leaf cuttings, division or seed. [rhs-carnivorous]
- Tropical pitcher plants: stem cuttings. [rhs-carnivorous]
- Trumpet pitchers: divide large clumps, or rhizome cuttings. [rhs-carnivorous]

**Problems.** Aphids and mealybugs on new shoots, and grey mould indoors. Most problems go away with the right water and mix. A few sundews among other plants catch fungus gnats. [rhs-carnivorous] (Each pest links to its guide.)

**In Stikling.** For a flytrap given a rest, set the growing season to Rests in winter, kept cooler, and Today asks "Gone dormant?" when it comes round.

---

## Ferns

For: the families Aspleniaceae, Davalliaceae, Nephrolepidaceae, Polypodiaceae and Pteridaceae, and the genera Adiantum, Asplenium, Blechnum, Cyrtomium, Davallia, Microsorum, Nephrolepis, Pellaea, Phlebodium, Platycerium, Polypodium and Pteris.
Opens from: the plant's page, the genus page, Guides.
The switch: none.

**Intro.** The ferns kept indoors are ones that can't take temperatures below 10°C. [rhs-tender-ferns] Some grow in the ground, like Boston and maidenhair ferns. Others grow on trees in the wild, like bird's nest, hare's foot and staghorn ferns, and need less water than ground ferns. [rhs-ferns, clemson-ferns] Tough, leathery ferns manage indoor air better than fine, feathery ones. [clemson-ferns]

**What to look at**

- Brown fronds can come from too much water, hard tap water, direct sun, dry air, a cold draught or a radiator. Check the compost first: brown often means less water, not more. [rhs-ferns]
- Brown leaflets on fine fronds, like a maidenhair's, mean dry air. [clemson-ferns]
- Ground ferns should never dry out completely. The tree-growing kinds are watered once the compost starts to feel dry. [clemson-ferns, rhs-ferns]
- Rows of brown dots under the fronds are spore cases, part of the fern. Scale insects can look like them. [ucipm]

**Humid air.** A tray of damp gravel under the pot, plants grouped together, a bright bathroom or a glass case. Clemson stands the pot inside a bigger one lined with damp sphagnum. [rhs-ferns, clemson-ferns] Indoor air is driest in winter. [clemson-ferns]

**Water.** Rainwater or filtered water where you can, at room temperature. Hard tap water undoes the slightly acid mix they like. [rhs-ferns]

**Mix and pot**

- An open, slightly acid mix: the RHS uses 2 parts peat-free ericaceous compost, 1 part perlite and 1 part orchid bark, or orchid compost alone for many tree-growing kinds. [rhs-ferns]
- Repot in spring once the roots are crowded, into a pot just bigger than the roots. Don't bury the furry surface stems of a hare's foot fern. [rhs-ferns, clemson-ferns]
- A staghorn fern grows on a board or bark, not in soil, held on by the round, flat fronds at its base. It needs very humid air. [clemson-ferns]

**Making new plants**

- Divide a clump so each part has plenty of fronds and roots, cutting a tight root ball if needed. [rhs-ferns, clemson-ferns]
- Ferns with creeping stems, like hare's foot ferns, can be divided along the stem. [rhs-ferns]
- From spores, which is possible but hard. [rhs-ferns]

**Problems.** Scale insects and mites are the most common, and Boston ferns also get mealybugs. Many sprays harm ferns, so try one on a small part first. Pests can often be picked off or rinsed off with water. [clemson-ferns] (Each pest links to its guide.)

**In Stikling.** A division with its own roots can be added straight away as a plant with its parent picked.

Photo: fern-sori.webp

---

## Hoyas

For: the genus Hoya.
Opens from: the plant's page, the genus page, Guides.
The switch starts on: Water.

**Intro.** Hoyas, or wax plants, are climbing and trailing plants with thick leaves, from tropical Asia and Australia. Many grow on trees in the wild. They flower in clusters of waxy, often scented stars. [iastate-hoya]

**What to look at**

- Thick leaves hold a lot of water. Collectors wait until the leaves pucker slightly before watering. [psu-hoya] Wrinkled or soft leaves mean dry, but soft leaves in wet compost can mean damaged roots. [iastate-hoya]
- Yellow leaves in compost that stays wet: too much water. [iastate-hoya]
- Thin, stretched growth and no flowers: too little light. Leaves that redden or scorch: too much. [iastate-hoya]
- Leaves that drop or blacken, and stems that die back, come from waterlogged compost or a cold winter. A plant with rotting roots seldom recovers, so take cuttings as soon as you see it. [rhs-hoya]

**Mix and pot**

- A chunky mix that drains at once, like equal parts orchid bark, peat-free compost and coarse perlite, or potting soil with bark and perlite or pumice. [rhs-hoya, iastate-hoya] Many growers use a mostly bark mix in a clear pot. *growers*
- A snug pot. Hoyas flower better slightly pot-bound, and a big pot stays wet. Move up only one size, when the roots fill the pot. [iastate-hoya, rhs-hoya, psu-hoya]
- A wire hoop, a trellis or a hanging pot. [rhs-hoya, iastate-hoya]

**Flowers**

- Hoyas flower from short spurs called peduncles, and many flower from the same one again and again. Don't cut them off with the old flowers. [iastate-hoya, rhs-hoya]
- Once buds show, don't move or turn the plant, since a change of light or water drops them. [rhs-hoya, psu-hoya, iastate-hoya]
- Young plants often take several years to flower. A plant that grows well but doesn't flower usually needs more light. [iastate-hoya, rhs-hoya]

**Rest.** From October to February they grow little. The RHS keeps them a bit cooler and drier then, but never dry right through. [rhs-hoya]

**Pruning.** Light pruning only, in late winter or early spring. Hoyas don't take hard pruning. [rhs-hoya]

**Root a cutting in** (switch)

Take a piece with one or two nodes and take off the lowest leaves. A single leaf roots, but never grows into a vine without a node at its base, which is how sweetheart hoyas are often sold. [iastate-hoya]

*Water*
- How: stand it with the node in water. [rhs-hoya, iastate-hoya]
- How long: roots in two or three weeks. [rhs-hoya]

*Sphagnum moss*
- How: the node in damp sphagnum. [iastate-hoya] Some growers wrap moss round each node, lay the cuttings on a damp mix and close them in a clear bag. *growers*

*A light mix*
- How: a light propagation mix. [iastate-hoya] Many growers use perlite, which makes the move to a pot easy. *growers*

**Layering.** A low shoot pegged down into a pot of cutting compost beside the plant, and cut free once it has rooted. [rhs-hoya]

**Gloves.** The milky sap irritates skin. [rhs-hoya]

**Problems.** Mealybugs, often hidden where the leaves meet the stem, scale, and spider mites, which come with very dry air. [iastate-hoya, rhs-hoya] (Each pest links to its guide.)

**In Stikling.** The flowering log under More on the plant's page keeps when it budded and flowered, and after two years says when it usually starts.

Photo: hoya-peduncle.webp

---

## Orchids

For: the family Orchidaceae.
Opens from: the plant's page, the genus page, Guides.
The switch starts on: Running water through.

**Intro.** This guide is about moth orchids (Phalaenopsis), the easiest to grow at home and the ones sold most. [rhs-orchids, aos-phal] In the wild their roots cling to trees in the open air. [rhs-phal] Other orchids, like slipper orchids, are looked after differently. [rhs-orchids]

**What to look at**

- The roots. Healthy roots are plump, firm and silvery white. Grey, shrivelled ones are too dry, and brown, mushy or hollow ones are dead. [rhs-phal, rhs-orchids] Many growers water once the roots turn from green to silvery. *growers*
- A clear pot shows the roots and the moisture inside. [rhs-phal]
- The weight of the pot: after a few waterings you can tell by lifting it. If in doubt, wait a day. [aos-phal]
- The leaves: olive green is right. Darker means too little light, a red tinge too much. [aos-phal]

**Water**

- Never leave it standing in water, and blot any water left where the leaves meet in the middle, or it rots there. [rhs-phal, aos-phal]
- Use water at room temperature. The RHS uses rainwater or boiled water, since they don't like the chlorine in tap water. The American Orchid Society says not to use softened or distilled water. [rhs-phal, rhs-orchids, aos-phal]
- Bark holds less water than moss, so an orchid in moss needs water less often. [aos-phal]
- Plain water now and then washes out the salts from feeding. [rhs-phal, aos-phal] Some growers water before they feed, since dry roots soak up the feed too strong. *growers*

**How to water** (switch)

*Running water through*
- How: in the sink, let tepid water run through the pot for a minute, then let it drain completely. [aos-phal]

*Soaking*
- How: stand the pot in a bowl of water, then let it drain completely. [rhs-phal] Many growers soak for 15 to 30 minutes. *growers*

Ice cubes, sometimes sold with orchids, are not in any of these sources, and many growers advise against them. *growers*

**Mix and pot**

- Bark orchid compost. Never multipurpose or loam-based compost, which kills them. [rhs-phal]
- Repot every one to three years, when the bark breaks down, while the roots have green tips. Cut off dead roots, set the plant at the same height and don't bury the roots growing in the air. [rhs-phal, aos-phal]

**Aerial roots.** Roots growing above the pot are normal. Don't bury or cut them. [rhs-phal]

**After it flowers.** Both are common:

- Cut the stem just above a node lower down, one of the small brown rings, and a side shoot often flowers again within 8 to 12 weeks. [rhs-phal, aos-phal, rhs-orchids]
- Cut it down to the leaves. The plant flowers again within a year, with larger flowers on a stronger stem. [aos-phal] If the stem turns pale and dry, cut it off at the base. [rhs-phal]

**Flowering again.** A healthy plant that hasn't flowered for a while can be moved somewhere about 5°C cooler for a month, or given more light. [rhs-phal, aos-phal] Cold draughts and changing temperatures make buds drop. [rhs-orchids, aos-phal]

**Keikis.** A keiki is a small plant that sometimes grows from a node on the flower stem. Once it has several good roots it can be cut off and potted in orchid compost. [rhs-phal]

**Problems.** Mealybugs. Water on the flowers spots them. [rhs-phal, rhs-orchids] (Each pest links to its guide.)

**In Stikling.** The flowering log under More on the plant's page keeps spikes, flowers and when it's done, and after two years says when it usually starts. A keiki with its own roots is a plant: add it with its parent picked.

Photo: orchid-keiki.webp

---

## Prayer plants

For: the family Marantaceae (Maranta, Calathea and Goeppertia, Ctenanthe, Stromanthe).
Opens from: the plant's page, the genus page, Guides.
The switch: none.

**Intro.** Prayer plants are the arrowroot family: Maranta, and calatheas, most of which are called Goeppertia now. Their leaves fold up at night. [rhs-calathea, illinois-prayer]

**Where this comes from** (shown under the intro). The RHS's calathea guide is the one full source. The rest is a short University of Illinois page, one extension answer and what growers say, and each part says which.

**What to look at**

- Brown leaf tips and edges: dry air. [rhs-calathea] Water with a lot of minerals or additives can cause them too. [umd-prayer]
- Patches that look soaked with water, mostly in winter: oedema, from compost kept too wet. [rhs-calathea]
- Weak growth: too little light. [rhs-calathea]
- Yellowing on the oldest, outermost leaves can be normal. [umd-prayer]
- Don't let water stand where the stems meet, since they rot easily. [illinois-prayer]

**Water.** Rainwater is best. Tap water left to stand for a day will do, and distilled water from a dehumidifier or tumble dryer works too. [rhs-calathea] They react to hard water and to what is added to tap water. [umd-prayer] Many growers water with distilled or rainwater and say the brown edges stopped. *growers* Keep the compost evenly moist while they grow, and let the top dry a little in winter, but never dry through. [rhs-calathea]

**Humid air.** A tray of damp gravel with the water below the top of the gravel, plants grouped together, or a bathroom. Sources differ on misting: the RHS mists the leaves regularly, and Maryland's answer says to stop, since wet leaves invite disease. [rhs-calathea, umd-prayer]

**Mix and pot.** A peat-free compost that holds water and drains, or multipurpose compost with about 10% grit. Repot every few years, one or two sizes up. [rhs-calathea] Many growers check the root ball of a new plant for a mesh plug from the nursery. *growers*

**Making new plants**

- Division in spring: cut the root ball into pieces with several leaves each, pot them on their own, and keep them warm, humid and moist until they grow. [rhs-calathea]
- Branching kinds like Ctenanthe: cut just below where a stem branches and root it in a covered pot. [rhs-calathea]

**Problems.** Spider mites, mealybugs and soft scale, and fungus gnats. [rhs-calathea] (Each pest links to its guide.)

**In Stikling.** For the common ones, the plant form offers the light, water and humidity they usually like.

---

## Notes on the plant group guides

- **Nothing about how often.** The RHS guides give watering and feeding by the week and month, the AOS sheet "once a week" for orchids in bark, and Iowa State "every 2 to 3 weeks" for succulents. The guides take only the sign each is checked by (compost dry, roots silvery, pot light) and leave the schedules out, like the rest of the guides. No feeding schedule is given anywhere.
- **Where sources disagree** the guide says who says what: tap water for carnivorous plants (RHS against ICPS), peat (ICPS uses it, the RHS avoids it), a flytrap's winter rest (a switch), misting prayer plants, distilled water for orchids (the RHS uses rain or boiled water, the AOS says not distilled).
- **Families** come from `plant-regions.json`. The genus lists cover genera the dictionary has no family for (Pinguicula, Cephalotus, Agave, Phlebodium and others) and keep the matching working for a genus the dictionary doesn't know.
- **The "In Stikling" lines** name features that are built: dormancy status, growing season, the flowering log, propagation kinds (cutting, corm, offset, seed, division, leaf), rooting aids and adding a rooted pup as a plant.
- **Left out.** Feeding amounts, keiki paste, Bti for bromeliad cups (outdoors in Florida), and anything about growing outdoors.
