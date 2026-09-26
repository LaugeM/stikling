---
name: Stikling
description: A plant and propagation tracker built for one hand at the potting bench.
colors:
  potting-green: "#2f7d4f"
  deep-moss: "#1f5a37"
  leaf-tint: "#e6f2ea"
  clay: "#b85c38"
  clay-soft: "#f6e6dc"
  alert: "#b3261e"
  alert-soft: "#fbe3e0"
  paper: "#f6f7f4"
  surface: "#ffffff"
  border: "#dde3dc"
  ink: "#1d2520"
  muted-ink: "#5f6b63"
typography:
  headline:
    fontFamily: "'Bricolage Grotesque', system-ui, sans-serif"
    fontSize: "2rem"
    fontWeight: 700
    lineHeight: 1.1
    letterSpacing: "-0.02em"
  count:
    fontFamily: "'Bricolage Grotesque', system-ui, sans-serif"
    fontSize: "1.4rem"
    fontWeight: 700
    lineHeight: 1
    letterSpacing: "-0.02em"
  title:
    fontFamily: "'Bricolage Grotesque', system-ui, sans-serif"
    fontSize: "1.125rem"
    fontWeight: 600
    lineHeight: 1.3
  name:
    fontFamily: "'Bricolage Grotesque', system-ui, sans-serif"
    fontSize: "1.0625rem"
    fontWeight: 600
    lineHeight: 1.4
  body:
    fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.5
  label:
    fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif"
    fontSize: "0.85rem"
    fontWeight: 600
    letterSpacing: "0.03em"
  caption:
    fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.4
rounded:
  control: "0.6rem"
  sm: "0.5rem"
  md: "0.6rem"
  lg: "0.75rem"
  xl: "0.9rem"
  full: "50%"
  pill: "999px"
spacing:
  xs: "0.3rem"
  sm: "0.5rem"
  md: "0.75rem"
  lg: "1rem"
  xl: "1.25rem"
  "2xl": "1.75rem"
components:
  button-primary:
    backgroundColor: "{colors.potting-green}"
    textColor: "#ffffff"
    rounded: "{rounded.control}"
    padding: "0.375rem 0.75rem"
  button-primary-hover:
    backgroundColor: "{colors.deep-moss}"
    textColor: "#ffffff"
  button-outline:
    backgroundColor: "transparent"
    textColor: "{colors.deep-moss}"
    rounded: "{rounded.control}"
    padding: "0.375rem 0.75rem"
  item-card:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.lg}"
    padding: "0.7rem"
  item-thumb:
    backgroundColor: "{colors.leaf-tint}"
    textColor: "{colors.potting-green}"
    rounded: "{rounded.md}"
    size: "3.5rem"
  item-thumb-plant:
    backgroundColor: "{colors.clay-soft}"
    textColor: "{colors.clay}"
    rounded: "{rounded.md}"
    size: "3.5rem"
  day-thumb:
    backgroundColor: "{colors.leaf-tint}"
    textColor: "{colors.deep-moss}"
    typography: "{typography.count}"
    rounded: "{rounded.md}"
    size: "3.5rem"
  fab:
    backgroundColor: "{colors.potting-green}"
    textColor: "#ffffff"
    rounded: "{rounded.full}"
    size: "3.5rem"
  stage-badge:
    backgroundColor: "{colors.leaf-tint}"
    textColor: "{colors.deep-moss}"
    typography: "{typography.caption}"
    padding: "0.35em 0.65em"
  detail-card:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.lg}"
    padding: "1rem"
  empty-state:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.muted-ink}"
    rounded: "{rounded.lg}"
    padding: "2.5rem 1rem"
  empty-state-first-run:
    backgroundColor: "{colors.leaf-tint}"
    textColor: "{colors.ink}"
    rounded: "{rounded.lg}"
    padding: "2rem 1.25rem 2.25rem"
  nav-link-active:
    backgroundColor: "transparent"
    textColor: "{colors.deep-moss}"
    height: "3.5rem"
  nav-icon-active:
    backgroundColor: "{colors.leaf-tint}"
    rounded: "{rounded.pill}"
---

# Design System: Stikling

## Overview

**Creative North Star: "The Potting Bench"**

A potting bench is a working surface. Things are within reach, nothing is precious, and the person using it knows exactly what they are doing. The expertise shows in the labels and the dates, not in the decoration. Stikling should feel like that bench: warm, hands-on and unbothered, run by someone with soil under their fingernails who still knows precisely how many days that corm has been on the riser.

The tension that defines this system is warm and precise at the same time. The data is genuinely experimental, with mediums compared and days counted, but nothing about the interface should feel clinical or sterile. Numbers are rendered exactly and never decorated. Everything around them is soft, green and welcoming.

The first build was quieter than this target: white cards, thin grey borders, small muted labels, and one green used sparingly. The design refresh closed most of that gap with two changes the owner approved. Headings, names, counts and buttons now use a display typeface, Bricolage Grotesque, whose slightly handmade shapes and narrow widths recall the print on a nursery plant label. And clay, the colour of a terracotta pot, joined the green as a second material: green is what grows and what you can tap, clay is the pot a plant lives in. The one confirmed rejection still stands: no neon gradients over black, no glow shadows, no frosted glass.

**Key Characteristics:**
- Phone first, one hand, content centred in a 48rem column that never gets wider.
- Two materials: green for growth and actions, clay for plants in pots, both against warm neutrals.
- A display face for anything read at a glance: titles, names, day counts, buttons.
- The days a propagation has been going are the biggest number on its row and its page.
- Flat surfaces separated by tone and a hairline, not by shadow.
- Stroked line icons throughout, never filled.
- A full dark theme that is a real second skin, not a filter.
- Exact values shown plainly: counts, days, stages, measurements.

## Colors

Green carries the system, in three strengths. Clay is the second material, in two strengths, and it has one job. Both sit on warm off-white neutrals that keep the app from reading as clinical.

### Primary
- **Potting Green** (`#2f7d4f`): the working accent. Primary buttons, the add button, the icon inside a thumbnail, the active tab underline, and the border a card takes on when hovered or focused. In dark mode it lightens to `#5fb883` so it still carries against a near-black page.
- **Deep Moss** (`#1f5a37`): the reading green. Links, active navigation labels, the app title, and any number that deserves emphasis such as a rooted count or a days-to-root figure. It exists because Potting Green does not have the contrast to be read as small text. In dark mode it swaps role and becomes the lighter `#86d1a4`.
- **Leaf Tint** (`#e6f2ea`): the tinted surface. Photo placeholders, thumbnail backgrounds, stage and moisture badges, the reminder strip on Today, and the selected state of a plant card. This is the token that does the warming, and it is currently under-used. In dark mode it becomes the deep `#1f3327`.

### Secondary
- **Clay** (`#b85c38`): the terracotta pot. The icon and placeholder colour for anything that is a plant, meaning the plant list thumbnails and the plant page tile, so plants and propagations tell apart at a glance even without photos. Dark mode `#e08a64`.
- **Clay Soft** (`#f6e6dc`): the tinted clay surface behind a plant's placeholder icon. Dark mode `#36241b`.

### Alert
- **Alert** (`#b3261e`) and **Alert Soft** (`#fbe3e0`): pests and anything overdue. Deliberately redder than clay, so an outbreak never reads as just another plant. Used for pest badges, the due date on a treatment, the tile of an open pest case, and every delete button and error message: Bootstrap's danger colour points at Alert, so the app has one red. Dark mode `#ff978b` and `#3d1c1a`.

### Neutral
- **Paper** (`#f6f7f4`): the page. Warm off-white, never pure white, so cards can sit on top of it without a shadow. Dark mode `#121814`.
- **Surface** (`#ffffff`): cards, the header, the bottom navigation, form field backgrounds. Dark mode `#1a221d`.
- **Border** (`#dde3dc`): every hairline. Card edges, dividers between list rows, the line under the tab strip. Dark mode `#2c3830`.
- **Ink** (`#1d2520`): body text. Nearly black, with green in it. Dark mode `#e3e9e4`.
- **Muted Ink** (`#5f6b63`): the supporting voice. Species lines, timestamps, section labels, inactive navigation, placeholder text, empty-state copy. Dark mode `#9aa89f`.

### Named Rules

**The Two Materials Rule.** Green means growing and acting: buttons, links, selection, stages, rooted counts. Clay means a plant in a pot, and nothing else. It never fills a button and never marks a state. Red means pests and danger. A fourth hue has no job to do.

**The Warm Neutral Rule.** No neutral in this system is a pure grey. Every one of them carries green. A `#f5f5f5` or a `#888` anywhere in this app is a bug, because it is the fastest way to turn the bench back into a clinic.

**The Two Greens Rule.** Potting Green fills, Deep Moss reads. Never set small text in Potting Green, and never fill a large area with Deep Moss.

## Typography

**Display Font:** Bricolage Grotesque, self-hosted from `wwwroot/fonts` as one variable woff2 (latin, weights 200 to 800, widths 75% to 100%) so it works offline. It is under the SIL Open Font License, which sits next to it.
**Body Font:** system-ui (with -apple-system, 'Segoe UI', Roboto, sans-serif)

**Character:** the display face does the talking and the system font does the reading. Bricolage is used for what you glance at: page titles, plant and propagation names, card headings, day counts, buttons, tabs and the navigation labels. Everything read as prose, every form label and every meta line stays in the system font, which keeps longer sessions calm and keeps the app feeling native. Bricolage has no italic, so anything that has to be italic (species lines, notes) stays in the system font.

### Hierarchy
- **Headline** (Bricolage 700, 2rem, 92% width, -0.02em): the page title, one per screen. On a detail page the name is 1.75rem and wraps rather than truncating, because a cultivar name is allowed to be long.
- **Count** (Bricolage 700, 1.4rem, 80% width, tabular figures): the day count in a propagation row's tile, and 2.25rem in the tile on a propagation page. Narrow so three digits still fit.
- **Title** (Bricolage 600, 1.125rem): card headings and form fieldset legends.
- **Name** (Bricolage 600, 1.0625rem): the name on a list row.
- **Body** (400, 1rem, 1.5 line-height): everything read as prose. Notes preserve their line breaks.
- **Label** (600, 0.85rem, uppercase, 0.03em tracking, Muted Ink): the section marker. Groupings on Today, propagation stage groups, subheadings inside a detail card, and the date heading on a timeline day. It is the most distinctive type decision in the app and it is applied consistently.
- **Caption** (400, 0.875rem): supporting lines. The species line under a plant name is set in italic here, which is correct botanical practice and the one piece of real typographic manners in the app.

### Named Rules

**The Quiet Label Rule.** Section headings are small, uppercase and muted. They mark structure without competing with the content beneath them. They are never coloured, never large, and never heavier than 600.

**The Italic Species Rule.** Genus and species are set in italic wherever they appear. Cultivar names are not.

**The Glance Rule.** Bricolage is for things read at a glance, the system font for things read line by line. If a new element is a sentence, it is system font.

## Layout

A single column, phone first. The content column is capped at 48rem and centred, with 1rem of side padding, so the desktop view is a comfortable reading measure rather than a stretched phone. The header, the bottom navigation and the add button pad themselves to the same column, so on a wide screen the wordmark, the tabs and the button line up with the page instead of sitting at the window edges. The app shell fills `100dvh` and splits into a sticky header, a flexible content area, and a fixed bottom navigation.

Safe-area insets are respected everywhere it matters: the header pads for the notch, and the bottom navigation and every element anchored above it pad for the home indicator. This is a real strength of the built system and should not be dropped when new fixed elements are added.

Vertical rhythm runs on a rem scale of 0.3, 0.5, 0.75, 1, 1.25 and 1.75. Sections separate at 1.75rem, cards at 1rem, list rows at 0.6rem.

There is exactly one breakpoint, 48rem, and it currently does one thing: the detail hero stops bleeding to the screen edges and gains a radius. Everything else is fluid. Lists reserve 5rem of trailing padding so the add button never covers the last row.

### Named Rules

**The Thumb Rule.** Anything tapped often lives in the bottom third. The navigation, the add button and the action bars are all anchored there. The top of the screen is for reading, not for reaching.

**The Safe Area Rule.** Any element fixed to the bottom adds `env(safe-area-inset-bottom)` to its own padding, and anything stacked above it offsets by the height of what it sits on. Nothing is allowed to hide under a home indicator.

## Elevation & Depth

This system is flat and separates by tone. A card is visible because Surface sits on Paper and a hairline Border traces its edge, not because it casts a shadow. Depth is built from steps of colour, which is the only approach that behaves the same in both themes: a shadow that reads convincingly on warm off-white all but disappears on a near-black page, and this app ships both.

Shadows are reserved for genuine floating: elements that overlap the page rather than sit in it.

### Shadow Vocabulary
- **Floating action** (`box-shadow: 0 4px 12px rgb(0 0 0 / 0.25)`): the round add button, which hovers over scrolling content.
- **Anchored bar** (`box-shadow: 0 -4px 12px rgb(0 0 0 / 0.08)`): bars pinned above the bottom navigation, meaning the update banner and the bulk action bar. The shadow points upward because the light is above and the bar sits below the content it covers.
- **Focus ring** (`box-shadow: 0 0 0 0.2rem rgb(var(--bs-primary-rgb) / 0.25)`): every focused input and control. This is a state rather than elevation, but it shares the mechanism and must never be removed.

### Named Rules

**The Flat-By-Default Rule.** Surfaces are flat at rest. If a new component wants a shadow, the honest questions are whether it overlaps other content and whether it would still read in dark mode. If either answer is no, use tone and a hairline instead.

**The Tonal Depth Rule.** Warmth and depth are added by moving between Paper, Surface and Leaf Tint, not by adding shadows. This is the intended route out of the current restraint.

## Shapes

Soft and consistent, with radius carrying the softness the palette has not yet been allowed to.

- **Cards and panels** use 0.75rem: list rows, detail cards, form fieldsets, the composer, the empty state.
- **Small tiles** use 0.6rem for thumbnails and timeline entries, and 0.5rem for photo tiles in a grid.
- **Large surfaces** use 0.9rem: the detail icon, and the detail hero once it is no longer edge to edge.
- **Controls** use 0.6rem, set through Bootstrap's `--bs-border-radius`, so buttons and fields match the thumbnails.
- **Pills** (999px) are for chips, the experiment bars and the tile behind the active navigation icon.
- **Circles** are reserved for icon-only buttons: the add button, the theme toggle, the delete button on a timeline entry, and the remove button on a photo.

Borders are always exactly 1px and always Border, with two deliberate exceptions: a panel awaiting a decision is outlined in Potting Green, and an empty state uses a dashed border so it reads as a placeholder rather than as content.

Arrows are icons too: the back links and the photo viewer use a stroked chevron, never a "‹" typed as text. Icons are inline SVG, stroked and never filled, at stroke widths from 1.5 to 2.25 with round caps and joins. There are no icon fonts and no icon library.

### Named Rules

**The Stroked Icon Rule.** Every icon is a stroked outline with round caps, drawn inline as SVG and coloured with `currentColor`. No filled glyphs, no icon fonts, no two-tone icons.

**The Hairline Rule.** Borders are 1px. A thicker border has to mean something specific, and right now exactly one does: the 2px underline on an active tab.

## Components

### Buttons
- **Shape:** rounded at 0.6rem, the same as a thumbnail.
- **Label:** Bricolage at weight 600.
- **Primary:** Potting Green fill with white text, deepening to Deep Moss on hover and active. In dark mode the label flips to a near-black ink (`#0f1a13`) because the lighter green cannot carry white text.
- **Outline:** Deep Moss text on a Potting Green hairline, filling with Potting Green on hover.
- **Focus:** the focus ring above, on every control, always.

### Cards and Containers
- **Corner Style:** 0.75rem.
- **Background:** Surface on Paper.
- **Shadow Strategy:** none. See Elevation & Depth.
- **Border:** 1px Border, switching to Potting Green when a card is hovered, focused or selected. A selected card also fills with Leaf Tint.
- **Internal Padding:** 1rem for detail cards and form fieldsets, 0.7rem for list rows.

### List Row
The signature component, shared by plants and propagations. A 3.5rem tinted thumbnail, then a name at weight 600, a species line in italic Muted Ink, and a meta line of small facts. All three text lines truncate to one line with an ellipsis, so a row never changes height and a long list stays scannable.

The thumbnail holds either a photo cropped to fill or, when there is no photo, a stroked icon: a sprout in Clay on Clay Soft for a plant, a glass in Potting Green on Leaf Tint for a finished propagation (one still going shows its day count instead), and a bug in Alert on Alert Soft for an open pest case. Rows separate by 0.6rem of space, and a row scales to 98.5% while pressed.

A propagation that is still going shows how many days it has been going in its tile, on Today and on the Propagations list. With no photo, the tile holds the number in Count type and Deep Moss with "days" under it in small Muted Ink, in place of the glass icon. With a photo, the photo stays and a small "14d" label sits in its bottom left corner on a solid Leaf Tint patch, so any photo keeps it readable. It is the one number worth reading from arm's length, and keeping it inside the tile leaves the full width for the name and one line of meta on a phone. The meta line does not repeat it: on Today, a propagation with nothing noted since it started says "no notes yet" instead of "last seen" with the same number.

### Experiment Group
Batches in an experiment close up into one bordered block divided by hairlines, so mediums can be compared line by line. Each row carries a bar on a shared scale, where the longest batch fills the width: solid Potting Green for the days it took to root, a dashed outline up to today for a batch still going, and no bar for a batch that finished without rooting. The bars grow in once on load, and not at all with reduced motion.

### Stage Badge
A propagation's stage in a Leaf Tint badge, led by three small ticks filled up to the stage: one for Started, two for Rooting, three for Rooted, and none for Failed. On a propagation page the stage picker reads as a path the same way: stages already passed keep a Leaf Tint fill, the current one is filled green. When a propagation has no photo, its page tile shows the day count instead of an icon.

### Inputs and Fields
- **Style:** Surface background on a Border hairline. The background is deliberately the card colour rather than the page colour, so fields do not look disabled.
- **Focus:** border shifts to Potting Green, plus the focus ring.
- **Placeholder:** Muted Ink at 0.8 opacity.
- Radio groups appear as a segmented control of equal-width buttons, used for filters and for propagation stage.

### Navigation
Five fixed destinations in a bottom bar: a stroked icon over a 0.75rem Bricolage label, Muted Ink at rest, Deep Moss and weight 600 when active. The active icon also sits on a Leaf Tint pill, so where you are shows before you read the label.

The header is sticky and holds the app icon at 28px beside the wordmark, set in Bricolage 800 at 1.375rem and 88% width in Deep Moss. The wordmark links to Today. The theme toggle sits on the right. Within a detail page, tabs are a simple underline strip in Bricolage: Muted Ink at rest, Deep Moss with a 2px Potting Green underline when active.

### Timeline Entry
One record on a plant or propagation history. Each entry opens with its kind set in Label, with a small stroked icon beside it: a sprout for Added, a pencil for Note, a camera for Photo, swap arrows for Updated, scissors for Propagated. Entries someone wrote take Deep Moss and entries the app recorded itself stay Muted Ink, so a long history can be scanned for the human notes without reading every line. Entries group under a date heading set in Label, and photos attach as a grid of square tiles at 0.5rem radius.

This replaced a 3px coloured bar down the left edge. The bar was flagged as a generic interface tell, and it was also redundant: the label already named the entry in words, and the bar had two colours for five kinds. The icon carries the same at-a-glance signal at a fraction of the visual weight, and it uses the icon system the rest of the app already follows.

### Empty State
Centred Muted Ink text on Surface inside a dashed Border at 0.75rem radius, with 2.5rem of vertical padding. Used for "nothing matches" and for empty sections.

The first-run version, on Today, Plants and Propagations when there is nothing at all yet, is louder on purpose: Leaf Tint with a green dashed edge, a stroked drawing at 5.5rem (a sprout in a clay pot, or a cutting rooting in a glass), a Bricolage heading in Deep Moss, and the sentence and button under it. The Propagations one also says in plain words what a propagation is, because that is the first question a new person asked.

## Do's and Don'ts

### Do:
- **Do** use green for growth and actions, clay for plants in pots, and red for pests. Nothing else.
- **Do** keep neutrals warm. Every grey in this app has green in it.
- **Do** reach for Leaf Tint when a screen feels cold. Tonal surface is the intended route to warmth.
- **Do** define both themes at once. A new colour without a dark counterpart is unfinished.
- **Do** pad fixed bottom elements with `env(safe-area-inset-bottom)` and offset anything stacked above them.
- **Do** truncate to one line in list rows so row height never varies.
- **Do** set genus and species in italic.
- **Do** use Bricolage for what is read at a glance and the system font for sentences.
- **Do** draw icons as inline stroked SVG using `currentColor`.
- **Do** show counts, days and stages as exact values.

### Don't:
- **Don't** use neon gradients over black, glow shadows or frosted glass. This is the one confirmed rejection.
- **Don't** add a shadow to a resting surface. Use tone and a hairline.
- **Don't** introduce a third font family or a fourth hue.
- **Don't** fill a button or mark a state with clay.
- **Don't** set small text in Potting Green, or fill a large area with Deep Moss.
- **Don't** use a pure grey anywhere.
- **Don't** let a fixed element cover the last row of a list; lists reserve 5rem at the end.
- **Don't** use filled icons or an icon font.
- **Don't** load Bricolage from a CDN. It is self-hosted so the app works with no signal.
