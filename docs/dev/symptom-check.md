# The symptom check

What could it be? on Something's wrong lists the few likely causes for what someone sees on a plant, with what to check for each and where the cause comes from. This file is the list of causes and the rules that rank them. The code in `Core/Problems/SymptomCheck.cs` follows it, and a change to one is a change to the other.

## What it may say

The app never sees the plant, so it never says to water, feed, repot or treat. Each cause says what to look at, and the person decides. When the answers don't point anywhere, it says so and lists the general checks (soil, roots, light) instead of guessing.

A cause is only listed for a sign when a source below names it for that sign. What the app knows about the plant can move a cause up or down, but never adds one that no source gives for the signs picked.

## Sources

| Id | Source |
|---|---|
| ucipm | Pittenger and Hodel, *Houseplant Problems*, UC IPM Pest Notes 74172, University of California, 2020. Table 1. https://ipm.ucanr.edu/home-and-landscape/houseplant-problems/ |
| rhs | Royal Horticultural Society, *Leaf damage on houseplants*. https://www.rhs.org.uk/prevention-protection/leaf-damage-on-houseplants |
| caes | Douglas, *Diagnosis of Common Houseplant Problems*, Connecticut Agricultural Experiment Station, 2003. https://portal.ct.gov/caes |
| isu | Iowa State University Extension, *What causes leaves on my houseplant to turn yellow or brown and drop off?* https://yardandgarden.extension.iastate.edu/faq/what-causes-leaves-my-houseplant-turn-yellow-or-brown-and-drop |

## The questions

The signs come from Something's wrong. Three questions follow, each with Not sure:

- **Which leaves?** Older and lower ones, the newest ones, or all over. (ucipm splits yellowing this way.)
- **How fast?** Over a few days, or slowly over weeks. (caes splits leaf drop this way.)
- **How is the soil now?** Wet, damp, dry all the way down. A meter reading from the last two days fills this in.

## What the app knows

Shown as lines the person can untick if they're wrong. Each one only moves causes up or down.

| Fact | Comes from |
|---|---|
| Arrived, repotted or moved in the last 4 weeks, and the day | `RecentChanges` with 28 days |
| Stands darker or brighter than it likes | `CareLikes.Fit` |
| Likes to dry out, or likes high humidity | What it likes |
| Last meter reading and its day | Care log |
| Days since last watered, against its usual gap | `WateringGuess`, only once it has learned the plant |
| Last fed, and how often in the last 3 months | Care log |
| Last repotted more than 2 years ago, or never | Care log and the acquired date |
| An open pest case on it, or on a plant in the same room | Pest cases |
| Rests in this season, or is marked dormant | Growing season and dormancy |
| In semi-hydro, water or a self-watering pot | How it gets water |

## The causes

Each cause: the signs a source gives it for, what moves it up or down, and what to check. Signs are the ones on Something's wrong, with the new ones marked *new*.

### Too wet at the roots

- **Signs:** yellow leaves (ucipm, rhs, isu), drooping (ucipm, caes), leaf drop (ucipm, caes), root rot (ucipm), mushy stem (ucipm: "soft stem bases are usually indicative of overwatering"), edema (rhs), spots (caes, ucipm), brown tips (ucipm, rhs).
- **Up:** soil wet now, a meter reading in the wet part, likes to dry out, rests in this season, repotted lately into a larger pot (rhs: overpotting).
- **Down:** soil dry all the way down, in semi-hydro or water (then the check is the roots and the water level, not the soil).
- **Check:** how wet the soil is a few centimetres down, with a finger or a meter. Whether the pot drains and isn't standing in water. Slide the plant out and look at the roots: healthy ones are firm and light, rotting ones are brown or black, soft, and may smell.

### Too dry

- **Signs:** drooping (ucipm: "wilting is usually caused by underwatering"), brown tips (ucipm, rhs), leaf drop (ucipm, caes), yellow leaves (rhs, isu), new growth small and pale *new* (caes).
- **Up:** soil dry now, a meter reading in the dry part, longer since watering than its usual gap, roots fill the pot (dries out fast).
- **Down:** soil wet now, watered in the last 2 days.
- **Check:** whether the soil is dry all the way down and the pot feels light. Whether water runs straight through without wetting the soil, which happens when it has dried hard.

### Too little light

- **Signs:** leggy (ucipm: "usually caused by inadequate light"), yellow leaves on older leaves (ucipm, rhs), leaf drop (ucipm, isu), lost variegation *new* (rhs), new growth small and pale *new* (caes).
- **Up:** stands darker than it likes, moved lately, the darker half of the year.
- **Down:** stands brighter than it likes.
- **Check:** how far it is from the window and which way the window faces. Whether new growth leans towards the light or has longer gaps between leaves than the old.

### Too much sun

- **Signs:** sunburn, spots (rhs: pale spots; ucipm: "excess light (sunburn) associated with a recent move"), brown tips (ucipm: "too much sun or heat through a window"), yellow all over (ucipm), drooping (ucipm).
- **Up:** stands brighter than it likes, moved lately.
- **Down:** stands darker than it likes.
- **Check:** whether the marks are on the leaves facing the window and on the top of the leaves. Whether they appeared within days of a move or a sunny spell.

### A recent change

- **Signs:** leaf drop (ucipm: "sudden change in light, temperature, or relative humidity"; caes: "changes in location"), drooping (ucipm), yellow leaves (isu).
- **Up:** arrived, repotted or moved in the last 4 weeks, and the signs came over a few days.
- **Only listed when** the app knows of a change, or the person says it came over a few days.
- **Check:** whether it started after the change. Whether the newest leaves and the growing point still look healthy, which often means it is settling in.

### Cold or a draught

- **Signs:** leaf drop (caes, isu), yellow leaves (rhs, isu), spots (ucipm: chilling below 10°C), new growth blackened (caes), drooping (caes).
- **Up:** the signs came over a few days, the colder half of the year.
- **Check:** whether it stands by a door, a window that is opened, cold glass at night, or a heater or vent that blows on it.

### Dry air

- **Signs:** brown tips (ucipm, rhs, caes, isu).
- **Up:** likes high humidity, the half of the year with the heating on.
- **Check:** whether the brown is only at the tips and edges and the rest of the leaf is fine. Whether it stands near a radiator or a vent.

### Salts from feed or water

- **Signs:** brown tips (ucipm: "excessive fertilizer or other soluble salts in the soil or water"; caes: chlorine and fluoride), new growth blackened (caes: too much fertilizer), drooping (caes: roots burned).
- **Up:** fed more than 3 times in the last 3 months, not repotted in over 2 years.
- **Check:** whether there is a white crust on the soil or the rim of the pot. What water it gets and how often it has been fed.

### Hungry

- **Signs:** yellow leaves on older leaves (ucipm: "insufficient fertilizer, especially nitrogen"), new growth small and pale *new* (caes), pale all over *new* (caes).
- **Up:** not fed in the last 3 months while growing, not repotted in over 2 years.
- **Down:** fed in the last month, rests in this season.
- **Check:** when it was last fed or given fresh soil. Whether the yellowing is even across the older leaves rather than in spots.

### Roots fill the pot

- **Signs:** yellow leaves on older leaves (ucipm: "pot-bound growth, compacted soil"), leaf drop (ucipm), drooping (ucipm).
- **Up:** not repotted in over 2 years or never, dries out quickly.
- **Check:** whether roots come out of the holes at the bottom or circle the inside of the pot. Whether water runs straight through.

### Pests

- **Signs:** yellow leaves with tiny speckles *new* (ucipm: spider mites), sticky leaves *new* (ucipm: scale, mealybug, aphids), curled or twisted leaves *new* (caes: aphids or mites), yellow leaves (ucipm), leaf drop (ucipm).
- **Up:** an open pest case on it or in the same room, arrived lately (ucipm: check new plants weekly).
- **Check:** the undersides of the leaves and where they meet the stem, with a magnifier if you have one. Fine webbing, a sticky shine, white fluff or small bumps that scrape off. Opens a pest case when something is found.

### Leaf spot disease

- **Signs:** spots (ucipm, rhs, caes).
- **Up:** arrived lately or was outside (ucipm: "rare unless plants have recently come from outdoors or greenhouses"), soil wet now.
- **Check:** whether the spots are dark with a yellow ring, or look wet, and whether they spread to new leaves over a week.

### Natural ageing

- **Signs:** yellow leaves (rhs: "the odd lower leaf" is normal; isu), leaf drop.
- **Only listed when** the person says older and lower leaves.
- **Check:** whether it is one or two of the oldest leaves at a time and the new growth is healthy. Then it is usually the plant letting go of old leaves.

### Going into rest

- **Signs:** yellow leaves, leaf drop, drooping.
- **Only listed when** the plant has a growing season set and rests in this season, or is marked dormant.
- **Check:** whether it does this around the same time each year. For a corm, tuber or rhizome, whether it is still firm.

## How they are ranked

Each sign a cause explains gives it 2 points. Each fact or answer that moves it up gives 1, and each that moves it down takes 1. Causes with 3 points or more are listed, the most first, at most 3, with the facts that moved each one up shown under it ("Moved 5 days ago"). Ties keep the order above.

When no cause reaches 3, or more than 3 tie at the top, it says the answers don't point to one cause and shows the general checks: soil a few centimetres down, the roots, the light where it stands, and the undersides of the leaves.

The numbers are a starting point and are kept in one place in the code so they can be tuned.
