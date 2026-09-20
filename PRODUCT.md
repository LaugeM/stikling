# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

The primary user is the developer, tracking his own houseplants and the cuttings taken from them. The app is built around that collection first, and nothing is made worse to suit someone else.

Two further audiences are real:

- **Houseplant hobbyists.** The app is public on GitHub Pages so anyone can use it. Some of them are casual plant owners who will never take a cutting. Features aimed at them, mainly watering and care logging, are built on purpose even though the developer does not use them.
- **Employers and other developers.** The project is meant for a CV. The code gets read and the live app gets opened. An app with real users is worth more here than an app that earns money.

Danish speakers may be the easier early audience, because the subject is niche and the app has a Danish name.

## Product Purpose

Keep track of plants, the propagations taken from them, and how both develop, with a photo timeline for each one. It exists because a propagation is a thing with a history worth recording: where it came from, what it was rooted in, how long it took, and whether it worked.

Success is the developer using it for a full season on his own collection, and other people finding it and coming back.

## Positioning

The propagation is a record in its own right, not a note attached to a plant. It is linked to the plant it came from, carries its own type, medium, stage, count and timeline, and can be promoted to a plant while the lineage survives. The family tree holds across generations.

Everything is stored on the device. No account, no server, no sign-up. The app opens from a link, installs to the home screen and works with no signal.

## Operating Context

Phone first. The common moment is standing at the plant with one hand free, taking a photo or moving a propagation to the next stage. Sessions are short and happen in ordinary indoor light.

Longer sessions happen sitting down: going through the collection, reading timelines, writing notes.

Desktop is currently used for development rather than real use, but the desktop layout still has to be good to look at, not merely unbroken.

## Capabilities and Constraints

Built and working:

- Plants, propagations with lineage and promotion, photo timelines, the Today screen, backup and restore, a care log, and medium experiments.
- Blazor WebAssembly standalone PWA on .NET 10, Bootstrap 5, IndexedDB through a small JavaScript interop layer. No backend.
- Photos are resized and stored entirely in JavaScript. Image data only crosses into C# for backup and restore.
- Ids are made on the device, deletes are soft, and enums are stored as text so backups stay readable.
- Backup and restore is a ZIP the user exports and imports. Restores merge rather than replace.

Constraints that bind future work:

- All data stays on the device, for the foreseeable future, including after any move to a native app.
- Free, no ads, no upsell.
- Jargon is a real barrier. Terminology has to work for someone who does not know what propagation means.

Explicitly undecided:

- **Sync between devices.** It needs hosting and probably some income to cover it, and no decision has been made. Until one is, everything stays on the device.
- **Language switching.** The interface is English today. Danish is intended and belongs on the list. Until it is built, avoid choices that make translating painful: no strings baked into fixed-width layout, and room for longer words.
- **Native apps.** An Android app is intended later, reusing the Razor components through .NET MAUI Blazor Hybrid. iOS only if Android goes anywhere, because releasing there costs more. This record stays `web` until that work actually starts.
- **Sharing a plant's progress outward**, for example turning a timeline into something postable, is wanted but not built. It is the intended way the app reaches people.
- **Accessibility.** No product-specific requirement has been established. Sensible defaults apply and nothing is committed.

## Brand Commitments

- The name is Stikling, Danish for "cutting". The Danish name stays even in an English interface.
- Existing assets: the leaf icon and the web manifest in `src/Stikling.Web/wwwroot`.
- Voice is plain and direct, with no marketing language. The README and `docs/FEATURES.md` are the reference for it.

## Evidence on Hand

- A working v1 deployed to GitHub Pages, covering everything listed under Capabilities.
- `docs/FEATURES.md`: a prioritised feature list with effort estimates and the milestone each item shipped in or is aimed at.
- Real use by the developer: an ongoing experiment comparing corm mediums (perlite, sphagnum, LECA and a corm riser), and a current thrips problem that drives the next feature.
- One first-run observation from showing the app to someone new: they asked what propagation means.
- There are no other users yet, no metrics, no testimonials and no press. Future work must not invent any.

## Product Principles

1. **The cutting is the unit.** Plants exist so cuttings have somewhere to come from. Anything that obscures where a propagation came from is wrong.
2. **First run decides it.** On the web nobody has invested anything. A screen that does not explain itself loses the user in seconds, so empty states and the first few taps carry real weight.
3. **Say it without the jargon.** Someone who has never heard the word propagation has to get through anyway.
4. **The device is the whole system.** No account, no signal and no sync. Features are designed against the data already on hand.
5. **Sharing is the growth plan.** Making a plant's progress easy to show other people is how this reaches anyone at all.
