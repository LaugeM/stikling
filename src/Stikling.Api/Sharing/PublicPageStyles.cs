namespace Stikling.Api.Sharing;

/// <summary>
/// The stylesheet of a collection's public page: <see cref="ShareStyles"/> (its colours, light and dark,
/// and its font) and what the gallery adds. Phone first, and from 40rem it uses the width. Clay is
/// for plants and green for cuttings, as in the app (DESIGN.md).
/// </summary>
public static class PublicPageStyles
{
    public const string Css = ShareStyles.Css + """

        :root { --clay: #b85c38; --clay-soft: #f6e6dc; }
        @media (prefers-color-scheme: dark) { :root { --clay: #e08a64; --clay-soft: #36241b; } }
        .pp { max-width: 90rem; margin: 0 auto; padding: 0 0 2.5rem; }
        .pp-head { padding: 1.5rem 1rem 0.5rem; }
        .pp-head h1 { font-size: 2rem; }
        .pp-head .line { max-width: 40rem; }
        .pp-back { display: inline-flex; align-items: center; min-height: 2.75rem; padding: 0 1rem; font-size: 0.95rem; }
        .pp-bar { display: flex; flex-wrap: wrap; gap: 0.5rem; margin: 0.75rem 1rem 0; padding: 0; list-style: none; }
        .pp-bar a { display: inline-flex; align-items: center; gap: 0.4rem; min-height: 2.75rem; padding: 0 0.9rem; border: 1px solid var(--border); border-radius: 0.6rem; background: var(--surface); color: var(--ink); text-decoration: none; font: 600 0.95rem var(--display); }
        .pp-bar a[aria-current="page"] { background: var(--tint); border-color: var(--green); color: var(--moss); }
        .pp-bar .n { font-weight: 400; color: var(--muted); font-family: var(--body); }
        .pp-part { margin: 1.75rem 0 0; }
        .pp-part-head { display: flex; align-items: baseline; justify-content: space-between; gap: 1rem; padding: 0 1rem; }
        .pp-part-head h2 { margin: 0; font: 700 1.25rem/1.2 var(--display); letter-spacing: -0.01em; }
        .pp-part-head a { display: inline-flex; align-items: center; min-height: 2.75rem; }
        .pp-empty { margin: 1rem; color: var(--muted); }

        .pp-tile { margin: 0; min-width: 0; }
        .pp-tile-link { display: block; color: inherit; text-decoration: none; }
        .pp-photo { position: relative; display: block; aspect-ratio: 1; overflow: hidden; border-radius: 0.75rem; background: var(--clay-soft); }
        .pp-tile.cutting .pp-photo { background: var(--tint); }
        .pp-photo img { display: block; width: 100%; height: 100%; object-fit: cover; }
        .pp-bare { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; padding: 0.5rem; text-align: center; font: 700 1rem/1.15 var(--display); color: var(--clay); overflow-wrap: anywhere; }
        .pp-tile.cutting .pp-bare { color: var(--moss); }
        .pp-text { display: block; padding: 0.4rem 0.1rem 0; }
        .pp-name { display: block; font: 700 0.95rem/1.2 var(--display); overflow-wrap: anywhere; }
        .pp-latin { display: block; margin-top: 0.1rem; font-size: 0.8rem; line-height: 1.3; color: var(--muted); overflow-wrap: anywhere; }
        .pp-latin i { font-style: italic; }
        .pp-caption { display: block; margin-top: 0.15rem; font-size: 0.8rem; line-height: 1.3; color: var(--moss); }
        a.pp-tile-link:hover .pp-name { text-decoration: underline; }

        .pp-shelf { display: flex; gap: 0.75rem; margin: 0.6rem 0 0; padding: 0 1rem 0.5rem; list-style: none; overflow-x: auto; scroll-snap-type: x mandatory; scroll-padding: 0 1rem; }
        .pp-shelf .pp-tile { flex: 0 0 8rem; scroll-snap-align: start; }
        .pp-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 1rem 0.75rem; margin: 0.6rem 0 0; padding: 0 1rem; list-style: none; }
        .pp-lines { margin: 0.6rem 1rem 0; padding: 0; list-style: none; border: 1px solid var(--border); border-radius: 0.75rem; background: var(--surface); }
        .pp-lines li { padding: 0.65rem 0.9rem; border-top: 1px solid var(--border); overflow-wrap: anywhere; }
        .pp-lines li:first-child { border-top: 0; }
        .pp-more { margin: 0.5rem 1rem 0; }
        .pp-more a { display: inline-flex; align-items: center; min-height: 2.75rem; font-weight: 600; }

        .pp-wall { display: grid; grid-template-columns: repeat(3, 1fr); gap: 2px; margin: 1rem 0 0; padding: 0; list-style: none; }
        .pp-wall .pp-photo { border-radius: 0; }
        .pp-wall .pp-text { position: absolute; left: 0; right: 0; bottom: 0; padding: 1.5rem 0.4rem 0.35rem; background: linear-gradient(to top, rgba(0, 0, 0, 0.7), rgba(0, 0, 0, 0)); color: #fff; }
        .pp-wall .pp-name { font-size: 0.8rem; }
        .pp-wall .pp-latin, .pp-wall .pp-caption { display: none; color: #fff; }
        .pp-wall .pp-tile { position: relative; }
        .pp-wall .pp-tile.bare .pp-text { display: none; }
        .pp-wall .pp-bare { font-size: 0.8rem; }

        .pp-pager { display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin: 1.5rem 1rem 0; }
        .pp-pager a, .pp-pager span.off { display: inline-flex; align-items: center; min-height: 2.75rem; padding: 0 0.9rem; border-radius: 0.6rem; font-weight: 600; }
        .pp-pager a { border: 1px solid var(--border); background: var(--surface); text-decoration: none; }
        .pp-pager span.off { visibility: hidden; }
        .pp-pager .of { color: var(--muted); font-size: 0.9rem; }
        .pp .foot { max-width: 40rem; }

        @media (min-width: 40rem) {
          .pp-head { padding: 2rem 1.5rem 0.5rem; }
          .pp-head h1 { font-size: 2.5rem; }
          .pp-part-head, .pp-more, .pp-pager { padding-left: 0.5rem; padding-right: 0.5rem; }
          .pp-part-head { padding: 0 1.5rem; }
          .pp-bar, .pp-more, .pp-pager { margin-left: 1.5rem; margin-right: 1.5rem; }
          .pp-lines { margin-left: 1.5rem; margin-right: 1.5rem; max-width: 40rem; }
          .pp-shelf { padding: 0 1.5rem 0.5rem; scroll-padding: 0 1.5rem; gap: 1rem; }
          .pp-shelf .pp-tile { flex-basis: 11rem; }
          .pp-grid { grid-template-columns: repeat(auto-fill, minmax(10rem, 1fr)); gap: 1.25rem 1rem; padding: 0 1.5rem; }
          .pp-wall { grid-template-columns: repeat(auto-fill, minmax(12rem, 1fr)); }
          .pp-wall .pp-tile:first-child { grid-column: span 2; grid-row: span 2; }
          .pp-wall .pp-name { font-size: 1rem; }
          .pp-wall .pp-latin { display: block; }
          .pp-wall .pp-bare { font-size: 1.1rem; }
          .pp .foot { margin-left: 1.5rem; }
        }
        @media (min-width: 90rem) {
          .pp-shelf .pp-tile { flex-basis: 12rem; }
        }
        """;
}
