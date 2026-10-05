# Plant names

The names the app suggests come from three files in `src/Stikling.Web/wwwroot/data`, all built by `build.cs` in this folder and never edited by hand:

- `plant-names.json`: the botanical names, for the genus, species and cultivar fields and the "What plant is it?" box.
- `everyday-names.en.json` and `everyday-names.da.json`: what people call the plants, like "Snake plant" for Dracaena trifasciata. The box finds plants by them, and a plant with no nickname shows one under its botanical name.

The service worker caches the botanical names and the English everyday names with the rest of the app, so they work offline. Another language is only downloaded on a device that uses it, and cached then.

To rebuild them, from the repository root:

```bash
dotnet run tools/plant-names/build.cs
```

It takes a few minutes and needs an internet connection. Commit the new files along with whatever changed here.

## Where the names come from

| What | Source | Licence |
|---|---|---|
| Genera, species, subspecies, varieties and old names | [Catalogue of Life](https://www.catalogueoflife.org), which takes its plants from Kew's World Checklist of Vascular Plants, through the [ChecklistBank API](https://api.checklistbank.org) | CC BY 4.0, credited in Settings |
| Cultivars | [Wikidata](https://www.wikidata.org), items that are a cultivar of something included | CC0 |
| Everyday names | Wikidata's common names (P1843) in English and Danish, and the Danish labels | CC0 |
| Cultivars and trade names | `cultivars.txt` in this folder, kept by hand | |
| Everyday names | `everyday-names.en.txt` and `everyday-names.da.txt` in this folder, kept by hand | |

Old names are kept so that typing one offers the name it goes by now, for example Scindapsus aureus gives Epipremnum aureum. When an old name is spelled the same as an accepted one (a homonym by another author), the accepted one wins.

Wikidata has an everyday name for many species, but often not the one on the label in a shop: snake plant is only "viper's bowstring hemp" there, and pothos has no English name at all. So the lists kept by hand come first, and their first name for a plant is the one the app shows. The plants on those lists also come first in the search box. From Wikidata, the build leaves out names that are only a botanical name again, Danish labels that name a genus ("Vedbend-slægten"), and a name given to three or more species, like "Fig" or "Orchid".

## What is included

- **Families and genera with all their species**, in `include` at the top of `build.cs`: the aroids (Araceae), prayer plants (Marantaceae), begonias, the carnivorous plant families, bananas and birds of paradise, and the genera of most common houseplants. Each family adds roughly 50 to 100 KB.
- **Big genera with only their named species**, in `namedOnly`: genera like Ficus, Euphorbia and Dendrobium, with hundreds of species of which only a few are grown indoors. A species is kept when it has an everyday name or a cultivar.
- **Genera by name only**, in `genusOnly`: the herbs.

`plant-names.json` is around 1 MB, about 270 KB once compressed, which is what a phone downloads. It is written one genus to a line, which is a third smaller than indenting every name and still shows in a pull request which genera changed.

## Adding more

- **A family or genus with all its species:** add it to `include`. A genus with hundreds of species nobody grows goes in `namedOnly` instead.
- **A cultivar:** add a line to `cultivars.txt`. The build warns about a genus that isn't included and a species that isn't an accepted name, which catches most typos. A cultivar added to Wikidata is picked up too, the next time the files are built.
- **An everyday name:** add it to `everyday-names.en.txt` or `everyday-names.da.txt`, as `Genus species: Name, Name`. The first name is the one shown. The build warns about a genus or species it doesn't know.
- **A wrong name from Wikidata:** add a line starting with `-` to the same file, like `- Sarracenia: Jeg er dansk`, and the build leaves it out.
- **Another language:** add it to `languages` in `build.cs` with a file of names kept by hand, and to the language choice in Settings.
