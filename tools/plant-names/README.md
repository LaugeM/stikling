# Plant names

The names suggested in the genus, species and cultivar fields come from `src/Stikling.Web/wwwroot/data/plant-names.json`. The app ships the file and the service worker caches it, so suggestions work offline. The file is built by `build.cs` in this folder and is never edited by hand.

To rebuild it, from the repository root:

```bash
dotnet run tools/plant-names/build.cs
```

It takes about a minute and needs an internet connection. Commit the new `plant-names.json` along with whatever changed here.

## Where the names come from

| What | Source | Licence |
|---|---|---|
| Genera, species, subspecies, varieties and old names | [Catalogue of Life](https://www.catalogueoflife.org), which takes its plants from Kew's World Checklist of Vascular Plants, through the [ChecklistBank API](https://api.checklistbank.org) | CC BY 4.0, credited in Settings |
| Cultivars | [Wikidata](https://www.wikidata.org), items that are a cultivar of something in the included families | CC0 |
| Cultivars and trade names | `cultivars.txt` in this folder, kept by hand | |

Old names are kept so that typing one offers the name it goes by now, for example Scindapsus aureus gives Epipremnum aureum. When an old name is spelled the same as an accepted one (a homonym by another author), the accepted one wins.

## Adding more

- **A family or genus with all its species:** add it to `include` at the top of `build.cs`. Araceae is the only one so far. Each family adds roughly 50 to 100 KB.
- **A genus by name only:** add it to `genusOnly`. These are the common houseplants outside the included families. Once their family is in `include`, take them out of `genusOnly`.
- **A cultivar:** add a line to `cultivars.txt`. The build warns about a genus that isn't included and a species that isn't an accepted name, which catches most typos. A cultivar added to Wikidata is picked up too, the next time the file is built.
