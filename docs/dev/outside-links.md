# Pages on other sites

The app links to a short list of pages on other sites, for what the guides don't cover. They are in `Stikling.Core/Guides/OutsidePages.cs`, the title and line for each in `Stikling.Web/Resources/OutsidePages.resx` and `OutsidePages.da.resx` (`{Id}_Title` and `{Id}_Line`), and `Services/OutsideText.cs` and `Components/OutsideLinks.razor` show them.

Unlike the guides, these need a connection. The guides stay the main answer and the links go beside them.

## Where they show

- **Guides** (`/guides`): after the built-in guides, under Pests, By plant group, Propagating and Growing media, in the order of the guides.
- **A pest case**: under the link to the pest's guide, one page for its pest (a case for Other gets a general page on houseplant pests).
- **A plant and the dictionary's genus page**: under the link to the plant group guide. The pages for the genus come first (air plants for Tillandsia, the Venus flytrap for Dionaea, moth orchids for Phalaenopsis), then the page for the whole group. A plant is matched the way `GuideBook.ForPlant` matches it, so a plant with no guide has no pages.

Every link opens in a new tab with `rel="noopener noreferrer"`, has the arrow that `SettingsRow` uses for outside links, names the site, and says "in English" when the app is in Danish and the page isn't. It is a plain link, so the Content Security Policy doesn't touch it, and nothing is fetched. The page is only reached when someone taps it.

## Language

Each page has an English address. It may also have a Danish one, which is shown instead in Danish and never in English. A Danish page is added only when it is a good page about the same thing, written by someone who isn't selling anything. When there is none the English page is shown, labelled as English. Leave the Danish out rather than link a weak page.

## How a page is picked

A page goes on the list only when all of these hold. Open it and read it first.

- It says what the link promises. The title and the line describe the page, and the page is about that thing, for houseplants.
- It comes from an extension service, the RHS, a botanic garden, a university, a plant society, or a Danish equivalent like Bolius (part of the Realdania foundation).
- It isn't a shop or a page that mainly sells, a page from another plant app, an AI-written content farm, or behind a login or a cookie wall that stops reading.
- It is current and unlikely to move. Prefer a page from a section the site keeps (the RHS advice pages, UC IPM pest notes) over a blog post.
- The list stays short. A topic with no good page gets no link, and a second page for a topic needs a reason the first can't do.

The repository never names or compares itself to other apps, so a page from one doesn't qualify either.

### Topics without a page

- **Semi-hydro and LECA**: the searches found only shops, blogs and growers' sites. The RHS page on compost mixes covers clay pebbles in a paragraph, which isn't enough to promise a page about semi-hydro, so the Semi-hydro guide stands alone. This matches the note in `guides.md` that no society or university has written about moving a houseplant to semi-hydro.
- **Danish pages** for most pests, plant groups and soil: the Danish results were shops and garden centres. The ones used are Bolius pages for pests in general, scale and cuttings. Bolius's page on spider mites is mostly about greenhouses and was left out. The Bolius page on growing in water is about plain water culture, not LECA.

## The pages

| Id | Topic | Page | Danish |
|---|---|---|---|
| thrips | Thrips | RHS, *Thrips* | none |
| spider-mites | Spider mites | RHS, *Glasshouse red spider mite* | none |
| fungus-gnats | Fungus gnats | RHS, *Fungus gnats* | none |
| mealybugs | Mealybugs | RHS, *Mealybug (glasshouse)* | none |
| scale | Scale | RHS, *Scale insects* | Bolius, *Slip af med skjoldlus* |
| aphids | Aphids | RHS, *Aphids* | none |
| whitefly | Whitefly | RHS, *Glasshouse whitefly* | none |
| houseplant-pests | Pest case for Other | UC IPM, *Houseplant problems* | Bolius, *Skadedyr og insekter i stueplanter* |
| aroids | Aroids | Iowa State University Extension, *A is for Aroids!* | none |
| begonias | Begonias | RHS, *Begonias as houseplants* | none |
| bromeliads | Bromeliads | RHS, *How to grow bromeliads* | none |
| air-plants | Tillandsia | RHS, *Air plants* | none |
| cacti-succulents | Cacti and succulents | RHS, *How to grow cacti and succulents* | none |
| carnivorous | Carnivorous plants | RHS, *How to grow carnivorous plants* | none |
| venus-flytrap | Dionaea | ICPS, *Growing Dionaea muscipula* | none |
| ferns | Ferns | RHS, *Tender ferns* | none |
| hoyas | Hoyas | RHS, *How to grow hoya* | none |
| orchids | Orchids | RHS, *Tips for growing orchids at home* | none |
| moth-orchids | Phalaenopsis | RHS, *How to grow Phalaenopsis* | none |
| prayer-plants | Prayer plants | RHS, *How to grow calatheas* | none |
| cuttings | Taking cuttings | RHS, *How to take cuttings from houseplants* | Bolius, *Sådan laver du stiklinger* |
| compost-mixes | Potting mixes | RHS, *A guide to compost mixes for houseplants* | none |

The addresses are in `OutsidePages.cs`. All of them were opened and read on 19 November 2026. The ICPS page on Nepenthes is about growing from seed and was left out. Ids are fixed: a page that is replaced gets a new id, and the old one is not reused.

## Adding or changing a page

1. Find the page, open it, and read it against the list above.
2. Add it to `OutsidePages.All` in the order of the list on the Guides page (pests as in the pest guides, groups as in the group guides).
3. Add `{Id}_Title` and `{Id}_Line` to `OutsidePages.resx` and `OutsidePages.da.resx`. The title says what the page is about, the line says what the page offers, in a sentence that doesn't claim more than the page has.
4. Add the row to the table above.
5. Run the tests. They check that every address is https, that every id has a title and a line in both languages, that every pest and every plant group has a page, that a plant in no group gets none, and that a Danish address is only used in Danish.

## Checking them again

Pages move and change, so go through the list now and then, at least every six months and before a release that touches it. Open each address, in the Danish one too:

1. It still loads (no 404, no redirect to a front page, no login or cookie wall that stops reading).
2. It still says what the title and the line promise, and is still about houseplants.
3. It is still current: the site hasn't taken it over for selling, or replaced it with something thinner.

Use `scrap.py fetch` from the `scrapling-local` skill if a site blocks a plain request. When a page is gone, find its replacement the same way as a new page, or remove the entry and its text, and say so in the pull request. A page that is removed leaves nothing stored on anyone's device, since the list is only code and text.
