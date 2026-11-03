# Plant names

The names the app suggests come from three files in `src/Stikling.Web/wwwroot/data`, all built by `build.cs` in this folder and never edited by hand:

- `plant-names.json`: the botanical names, for the genus, species and cultivar fields and the "What plant is it?" box.
- `everyday-names.en.json` and `everyday-names.da.json`: what people call the plants, like "Snake plant" for Dracaena trifasciata. The box finds plants by them, and a plant with no nickname shows one under its botanical name.
- `plant-regions.json`: the family of each genus and where each species grows wild, which a plant's page shows. Only the plant page reads it, so it is its own file and the page doesn't need the bigger file of names.

The service worker caches the botanical names and the English everyday names with the rest of the app, so they work offline. Another language is only downloaded on a device that uses it, and cached then.

To rebuild them, from the repository root:

```bash
dotnet run tools/plant-names/build.cs
```

It takes a few minutes and needs an internet connection. Commit the new files along with whatever changed here.

## Where the names come from

| What | Source | Licence |
|---|---|---|
| Genera, species, subspecies, varieties, old names and families | [Catalogue of Life](https://www.catalogueoflife.org), which takes its plants from Kew's World Checklist of Vascular Plants, through the [ChecklistBank API](https://api.checklistbank.org) | CC BY 4.0, credited in Settings |
| Where a species grows wild | Catalogue of Life's distributions, which also come from Kew's checklist | CC BY 4.0, credited in Settings |
| Area names and codes | Kew's [World Geographical Scheme for Recording Plant Distributions](https://github.com/tdwg/wgsrpd), with the place names in `areas.txt` added by hand | |
| Cultivars | [Wikidata](https://www.wikidata.org), items that are a cultivar of something included | CC0 |
| Everyday names | Wikidata's common names (P1843) in English and Danish, and the Danish labels | CC0 |
| Cultivars and trade names | `cultivars.txt` in this folder, kept by hand | |
| Everyday names | `everyday-names.en.txt` and `everyday-names.da.txt` in this folder, kept by hand | |

Old names are kept so that typing one offers the name it goes by now, for example Scindapsus aureus gives Epipremnum aureum. When an old name is spelled the same as an accepted one (a homonym by another author), the accepted one wins.

Wikidata has an everyday name for many species, but often not the one on the label in a shop: snake plant is only "viper's bowstring hemp" there, and pothos has no English name at all. So the lists kept by hand come first, and their first name for a plant is the one the app shows. The plants on those lists also come first in the search box. From Wikidata, the build leaves out names that are only a botanical name again, Danish labels that name a genus ("Vedbend-slægten"), and a name given to three or more species, like "Fig" or "Orchid".

## What is included

- **Families and genera with all their species**, in `include` at the top of `build.cs`: the aroids (Araceae), prayer plants (Marantaceae), begonias, the carnivorous plant families, bananas and birds of paradise, the cacti (Cactaceae), the succulent families Crassulaceae and Aizoaceae, and the genera of most common houseplants. Each family adds roughly 50 to 100 KB.
- **Big genera with only their named species**, in `namedOnly`: genera like Ficus, Euphorbia and Dendrobium, with hundreds of species of which only a few are grown indoors. A species is kept when it has an everyday name or a cultivar.
- **Genera by name only**, in `genusOnly`: the herbs.

`plant-names.json` is around 1.8 MB, about 450 KB once compressed, which is what a phone downloads. The cacti and succulents are more than a third of that, most of it their many old names. `plant-regions.json` is about 700 KB, 210 KB compressed, and is only downloaded by the plant page. It is written one genus to a line, which is a third smaller than indenting every name and still shows in a pull request which genera changed.

## Where plants grow wild

`plant-regions.json` has, for each species, the areas it is native to, as Kew's level 3 areas from the World Geographical Scheme for Recording Plant Distributions: mostly countries, with the biggest countries split into states or regions and the islands on their own. Places a plant has been brought to and gone wild in are left out. A subspecies or variety is only listed when its areas differ from its species'. The file also has each genus's family and the name an old genus goes by now.

For many species, among them most cacti, succulents, begonias and peperomias, the Catalogue of Life gives the distribution as text, like "Mexico (Oaxaca, Chiapas); Cuba [I]", instead of area codes. `areas.txt` in this folder says which area each place name in that text is in. It started from Kew's level 3 and level 4 area names, with the spellings the text uses added by hand. A place marked [I] was brought there and is left out. A country split into several areas, like Mexico or Brazil, needs the states it names, and is left out when the text names none. The build lists the place names it couldn't place, so a common one can be added to `areas.txt`.

The names of the areas are in `src/Stikling.Web/Resources/Regions.resx` and `Regions.da.resx`, keyed by the area's code, so they are translated like the rest of the app. The build warns about a code that isn't there.

## Adding more

- **A family or genus with all its species:** add it to `include`. A genus with hundreds of species nobody grows goes in `namedOnly` instead.
- **A cultivar:** add a line to `cultivars.txt`. The build warns about a genus that isn't included and a species that isn't an accepted name, which catches most typos. A cultivar added to Wikidata is picked up too, the next time the files are built.
- **An everyday name:** add it to `everyday-names.en.txt` or `everyday-names.da.txt`, as `Genus species: Name, Name`. The first name is the one shown. The build warns about a genus or species it doesn't know.
- **A wrong name from Wikidata:** add a line starting with `-` to the same file, like `- Sarracenia: Jeg er dansk`, and the build leaves it out.
- **Another language:** add it to `languages` in `build.cs` with a file of names kept by hand, and to the language choice in Settings.

## Care for common plants

`care.txt` is a list kept by hand of what about 70 common houseplants usually like and whether they are toxic. `care.cs` turns it into `src/Stikling.Web/wwwroot/data/plant-care.json`, which the plant form and plant page offer from. Like the names, the JSON is never edited by hand.

```
dotnet run tools/plant-names/care.cs
```

It works offline and takes a few seconds. Run it after changing `care.txt` and commit both files. It doesn't touch `plant-names.json`.

| What | Source |
|---|---|
| Light, water, humidity and growing season | [NC State Extension Plant Toolbox](https://plants.ces.ncsu.edu), the [Missouri Botanical Garden Plant Finder](https://www.missouribotanicalgarden.org/PlantFinder) or the [RHS](https://www.rhs.org.uk), only what they say about growing the plant indoors |
| Toxic to cats and dogs | [ASPCA Toxic and Non-Toxic Plants](https://www.aspca.org/pet-care/aspca-poison-control/toxic-and-non-toxic-plants) |
| Toxic to people | NC State Extension Plant Toolbox, only its poison section about eating the leaves or stems |

How a source's words are read is in the header of `care.txt`. A new plant needs a source for every value it sets, and `?` for anything no source says. The build stops with the line number when a value is unknown, a web address has no `source` line, or a value has no source.

The build also adds the old names of each plant, so a plant entered as Sansevieria trifasciata finds the line for Dracaena trifasciata. It warns, without stopping, about a name that isn't an accepted genus or species in `plant-names.json`, and says what an old name goes by now.
