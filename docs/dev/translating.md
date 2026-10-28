# Translating Stikling

All the text people see in the app lives in `.resx` files, one set per part of the app. English is the source. A translation sits next to it with the language in the name: `Settings.resx` is English and `Settings.da.resx` is Danish.

The text that is still in the Razor pages and C# files is being moved over one area at a time. Until it is, a translated app shows a mix of the two languages. A language is only offered to everyone once most of the text is translated, see "When a language goes live".

## For translators

### Where the files are

- `src/Stikling.Web/Resources` has the text for the pages and components.
- `src/Stikling.Core/Resources` has the text that comes from the services in Core. It is added as those services are moved over.

Each area has its own file: `Common` (words used on many screens), `Labels` (names of statuses, kinds and dates), `Layout` (the menu and header) and `Settings`. Translate the ones you can. A language doesn't have to have every file to work, text with no translation shows in English.

### Keys, placeholders and comments

Each piece of text is a key with a value. The key says what the text is, like `EverydayNames` or `ThemeDark`. Never change a key, only the value.

- **Placeholders** like `{0}` and `{1}` are filled in by the app with a name, a number or a date. Keep them in your translation, in whatever order the sentence needs. A test fails if one is missing or new.
- **Counts** come in pairs: `Plant_One` is used for exactly one and `Plant_Other` for any other number, including none. `{0}` is the number. Translate both, even if your language uses the same word.
- **Comments** on a key say where the text shows and what the placeholders are. Read them before translating.
- **Date patterns** like `DateFormat` are not text. They are .NET date patterns: `d` is the day, `MMM` the short month name and `yyyy` the year. Change the order and the punctuation to how dates are written in your language, and leave the letters alone.
- A few pieces of text have HTML in them, like `<i>`. Keep the tags where they are.
- The name of the app, Stikling, and botanical names stay as they are.

A translation that is the same as the English counts as translated. Some words are the same in your language.

### Your name in the app

`Settings` has a text called `Translators`. Put your name there, or the names of everyone who translated your language, separated by commas. The app then shows "Translated by" with the names under the choice of language. Left as it is in English, no names show.

### When a language goes live

A language is offered in Settings to everyone once at least 90 percent of the English text is translated, counted over all the files. Before that it only shows in development builds. `tests/Stikling.Core.Tests/TranslationFileTests.cs` checks this, so the language has to be listed as ready in `src/Stikling.Core/Settings/AppLanguages.cs` at the same moment it reaches 90 percent.

### Adding a language

New languages are added on Weblate. Start a translation there and it opens a pull request with the new files. Then add a line for the language to `AppLanguages.All` (code, its name in its own language, and `Ready: false`). Set `Ready: true` when it has enough translated.

### Danish

Write in plain everyday Danish, with "du". Keep the text as short as the English, with a capital only at the start of a sentence.

| English | Danish |
| --- | --- |
| plant | plante |
| cutting | stikling |
| propagation | formering (the Propagations page is Formeringer) |
| Today | I dag |
| water plants | vand planter |
| care log | plejelog |
| repot | ompot |
| pot up | potte op |
| rooted | har slået rødder |
| room | rum |
| spot | plads |
| collection | samling |
| backup | sikkerhedskopi |
| restore | gendan |
| settings | indstillinger |
| pest | skadedyr |
| quarantine | karantæne |
| wishlist | ønskeliste |
| swap | bytte |
| fertiliser, feed | gødning (verb: gøde) |
| soil mix | jordblanding |
| sign in | log ind |
| sync | synkronisere |
| theme | tema |
| light, dark | lyst, mørkt |
| everyday names | daglige navne |
| outer pot | yderpotte |
| nursery pot | planteskolepotte |
| tag | tag (plural tags) |
| label (printed, the Labels page) | etiket (plural etiketter) |
| node | knude (plural knuder) |
| mother plant | moderplante |
| example plant | eksempelplante |
| round (the daily round) | runde |
| Do it later | Gør det senere |
| put off | udskudt |
| away, quiet time | ude, pause |
| plant sitter | plantesitter |
| kind of care | plejetype |
| care interval | plejeinterval |
| moisture meter, reading | fugtmåler, måling |
| growing season | vækstsæson |
| rests, dormant | hviler |
| grow light | plantelampe |
| pest case | sag |
| treatment, treatment plan | behandling, behandlingsplan |
| sticky trap | limfælde |
| problem | problem |
| flowering | blomstring |
| bud | knop |
| seller | sælger |
| product | produkt |
| wish | ønske |
| pass on (a cutting) | give videre |
| timeline | tidslinje |
| note | note |
| photo | billede |
| share | del |
| search | søg |
| import | importere |
| spreadsheet | regneark |
| account | konto |
| sign out | log ud |
| own fields | egne felter |

Keep "Stikling" as the name of the app.

## For developers

### Adding text

Text people see never goes in a Razor page or a C# file. Add it to the file for its area, in English and in Danish, and use it through the typed class:

```razor
@using Strings = Stikling.Web.Resources
<h2>@Strings.Settings.SectionPests</h2>
```

`_Imports.razor` already has the alias. In a C# file, add `using Strings = Stikling.Web.Resources;`.

- Name the key for what the text is, in PascalCase, not for the English words of a long sentence. Add a `<comment>` in the English file when a translator needs to know where it shows or what `{0}` is.
- Text with values uses placeholders and `string.Format(CultureInfo.CurrentCulture, Strings.X.Y, a, b)`. Never build a sentence out of pieces that are translated separately.
- For a count, use two keys, `Thing_One` and `Thing_Other`, and `Plural.Of(count, Strings.X.Thing_One, Strings.X.Thing_Other)`.
- Text made in `Stikling.Core`, like a care suggestion or a line of the plant-sitter sheet, goes in `src/Stikling.Core/Resources` the same way and is used through `using Strings = Stikling.Core.Resources;`. Counts use `Plural.Of` from `Stikling.Core.Text`. The Core tests run with English text, so a test can still check the English wording.
- Dates go through `Labels.Date` and the other date methods in `Labels`, which pick the patterns from `Labels.resx`. English keeps its own look ("19 Sep 2026"), any other language uses its culture's names.
- When a sentence has a link or an element inside it, put `{0}` where the element goes and fill it in on the page. Only text from the resource files may be shown as markup, never anything a person typed.
- The app language is picked once, in `Program.cs`, before the app starts, so changing it restarts the app. A language that was chosen is saved in `UserSettings.Language` and `wwwroot/js/language.js` keeps a copy on the device for that first start.

### Adding an area file

1. Add `Name.resx` and `Name.da.resx` to the `Resources` folder of the project. The typed class `Name` is made from the English file when the project is built.
2. Keep the keys in the same order in both files.
3. Use the text through `Strings.Name.Key`.

Parallel pull requests should use their own area file so they don't edit the same one.

## Setting up Weblate

This is done once, by the owner of the repository.

1. Apply for the free Libre plan on Hosted Weblate (hosted.weblate.org) for an open source project, and create the project Stikling.
2. Connect the GitHub repository. Weblate needs access to push branches and to open pull requests.
3. Use component discovery, so every English file becomes its own component. The file mask is written the way Weblate's discovery documents it, with the groups `component` and `language`, and has to match `src/<project>/Resources/<Area>.<language>.resx`, for example `src/Stikling.Web/Resources/Settings.da.resx`. The English file is `<Area>.resx` in the same folder. Run it once for each project that has a `Resources` folder (`Stikling.Web`, later `Stikling.Core`).
4. For each component, set English as the source language and the file format to ".NET resource file".
5. Set it to push its changes as pull requests, not straight to `main`.
6. Check that a new translation of one component opens a pull request, and that the tests pass on it.
