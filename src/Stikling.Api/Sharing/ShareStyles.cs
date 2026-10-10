namespace Stikling.Api.Sharing;

/// <summary>
/// The page's stylesheet, inline so the page needs nothing but its images. The colours are the
/// app's (DESIGN.md), light and dark. Bricolage Grotesque is fetched from the app's own site, which
/// the content security policy allows. Without it the page falls back to the system font.
/// </summary>
public static class ShareStyles
{
    public const string AppUrl = "https://stikling.app/";

    public const string Css = """
        @font-face {
          font-family: "Bricolage Grotesque";
          font-style: normal;
          font-weight: 200 800;
          font-stretch: 75% 100%;
          font-display: swap;
          src: url("https://stikling.app/fonts/bricolage-grotesque-latin.woff2") format("woff2");
        }
        :root {
          --paper: #f6f7f4; --surface: #ffffff; --border: #dde3dc; --ink: #1d2520; --muted: #5f6b63;
          --moss: #1f5a37; --green: #2f7d4f; --tint: #e6f2ea; --on-green: #ffffff;
          --display: "Bricolage Grotesque", system-ui, sans-serif;
          --body: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
          color-scheme: light dark;
        }
        @media (prefers-color-scheme: dark) {
          :root {
            --paper: #121814; --surface: #1a221d; --border: #2c3830; --ink: #e3e9e4; --muted: #9aa89f;
            --moss: #86d1a4; --green: #5fb883; --tint: #1f3327; --on-green: #0f1a13;
          }
        }
        * { box-sizing: border-box; }
        body { margin: 0; background: var(--paper); color: var(--ink); font: 1rem/1.5 var(--body); -webkit-text-size-adjust: 100%; }
        a { color: var(--moss); }
        .page { max-width: 40rem; margin: 0 auto; padding: 0 0 2.5rem; }
        .cover { display: block; width: 100%; height: auto; aspect-ratio: 4 / 3; object-fit: cover; background: var(--tint); }
        .head { padding: 1.25rem 1rem 0.5rem; }
        h1 { margin: 0; font: 700 1.75rem/1.1 var(--display); letter-spacing: -0.02em; font-stretch: 92%; overflow-wrap: anywhere; }
        .latin { margin: 0.3rem 0 0; font-size: 0.875rem; line-height: 1.4; color: var(--muted); }
        .latin i { font-style: italic; }
        .line { margin: 0.75rem 0 0; }
        .counts { margin: 0.5rem 0 0; font-size: 0.8rem; line-height: 1.4; color: var(--muted); }
        .history { list-style: none; margin: 1.5rem 0 0; padding: 0 1rem; }
        .history > li { margin: 0 0 1.25rem; }
        .when { margin: 0 0 0.3rem; font: 600 0.85rem/1.4 var(--body); letter-spacing: 0.03em; text-transform: uppercase; color: var(--muted); }
        .milestone { display: flex; flex-wrap: wrap; gap: 0.3rem 0.75rem; align-items: baseline; padding: 0.5rem 0.75rem; background: var(--tint); border-radius: 0.6rem; }
        .milestone .when { margin: 0; }
        .milestone .what { font-family: var(--display); font-weight: 600; color: var(--moss); }
        .note { margin: 0 0 0.5rem; white-space: pre-wrap; overflow-wrap: anywhere; }
        .photos { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(3, 1fr); gap: 0.3rem; }
        .photos li { margin: 0; }
        .photos a { display: block; }
        .photos img { display: block; width: 100%; aspect-ratio: 1; object-fit: cover; border-radius: 0.5rem; background: var(--tint); }
        .photos.single { grid-template-columns: 1fr; }
        .photos.single img { aspect-ratio: 4 / 3; }
        .foot { margin: 2rem 1rem 0; padding: 1rem; background: var(--surface); border: 1px solid var(--border); border-radius: 0.75rem; font-size: 0.875rem; line-height: 1.4; }
        .foot p { margin: 0 0 0.5rem; }
        .start { display: inline-block; padding: 0.5rem 0.9rem; background: var(--green); color: var(--on-green); font: 600 0.95rem var(--display); text-decoration: none; border-radius: 0.6rem; }
        .meta { margin-top: 0.75rem; font-size: 0.8rem; color: var(--muted); }
        .meta a { color: var(--muted); }
        .gone { max-width: 28rem; margin: 0 auto; padding: 3rem 1rem; }
        @media (min-width: 40rem) {
          .page { padding-top: 1.5rem; }
          .cover { border-radius: 0.9rem; }
          .photos { grid-template-columns: repeat(4, 1fr); }
          .photos.single { grid-template-columns: 1fr; }
        }
        """;
}
