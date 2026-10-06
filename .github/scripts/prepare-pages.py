"""Prepare a published Blazor WebAssembly app for GitHub Pages.

The <base href> in index.html has to match where the site is served from: "/" on its own
domain, or the repository's subfolder on https://<user>.github.io/<repo>/. Changing index.html
also changes its SHA-256 hash, and the PWA service worker refuses to cache files whose
hash doesn't match service-worker-assets.js, so the hash is updated there too.

It also writes the pages search engines and AI tools read without running the app: help.html and privacy.html
from the folder tools/static-pages/render.cs renders them to, deletion.html, and llms.txt.

Usage: python prepare-pages.py <wwwroot dir> <base path, e.g. / or /stikling/> <rendered pages dir>
"""
import base64
import hashlib
import pathlib
import re
import shutil
import sys
from html.parser import HTMLParser

root = pathlib.Path(sys.argv[1])
base_path = sys.argv[2]
rendered = pathlib.Path(sys.argv[3])
if not (base_path.startswith("/") and base_path.endswith("/")):
    sys.exit(f"Base path must start and end with '/': {base_path}")

index = root / "index.html"
html = index.read_text(encoding="utf-8")
html, count = re.subn(r'<base href="[^"]*"\s*/>', f'<base href="{base_path}" />', html)
if count != 1:
    sys.exit("Expected exactly one <base href> tag in index.html")
index.write_text(html, encoding="utf-8", newline="\n")

# Keep the service worker's integrity check happy
digest = base64.b64encode(hashlib.sha256(index.read_bytes()).digest()).decode()
manifest = root / "service-worker-assets.js"
text = manifest.read_text(encoding="utf-8")
text, count = re.subn(
    r'("hash":\s*")sha256-[^"]+(",\s*"url":\s*"index\.html")',
    rf"\g<1>sha256-{digest}\g<2>",
    text,
)
if count != 1:
    sys.exit("Expected exactly one index.html entry in service-worker-assets.js")
manifest.write_text(text, encoding="utf-8", newline="\n")

# The pre-compressed copies would still contain the old base href; Pages doesn't need them
for stale in ("index.html.gz", "index.html.br"):
    (root / stale).unlink(missing_ok=True)

# GitHub Pages serves 404.html for unknown paths, which lets deep links like /plants load the app. It answers
# with a 404, so search engines leave those paths out, and the pages below that should be found get their own file.
shutil.copyfile(index, root / "404.html")


# Visitors from a search land on the static pages, so a link back to Settings means nothing to them yet
def strip_back_link(text):
    return re.sub(r'\s*<a class="back-link"[^>]*>.*?</a>', "", text, count=1, flags=re.S)


# A page the app also has, as plain HTML with its text in place of the front page. Pages serves name.html for
# /name with a 200, and search engines, AI tools and Meta's check for the deletion page read it without running
# the app. The app replaces it with the same page once it has started.
def write_static_page(name, title, description, text):
    text = strip_back_link(text)
    page, count = re.subn(r"\s*<!-- intro:.*?<!-- /intro -->", "", html, flags=re.S)
    if count != 1:
        sys.exit("Expected exactly one <!-- intro: --> ... <!-- /intro --> block in index.html")
    page, count = re.subn(
        r'(<div id="app">).*?(</div>\s*<div id="blazor-error-ui">)',
        lambda m: f'{m.group(1)}\n<main class="static-page">\n{text}</main>\n{m.group(2)}',
        page,
        flags=re.S,
    )
    if count != 1:
        sys.exit("Expected exactly one <div id=\"app\"> in index.html")
    page, count = re.subn(r"<title>.*?</title>", f"<title>{title} · Stikling</title>", page, count=1)
    if count != 1:
        sys.exit("Expected a <title> in index.html")
    # Its own address, title and description, also for link previews, which go by og:url
    for pattern, value in (
        (r'<link rel="canonical" href="[^"]*" />', f'<link rel="canonical" href="https://stikling.app/{name}" />'),
        (r'<meta property="og:url" content="[^"]*" />', f'<meta property="og:url" content="https://stikling.app/{name}" />'),
        (r'<meta property="og:title" content="[^"]*" />', f'<meta property="og:title" content="{title} · Stikling" />'),
        (r'<meta name="description" content="[^"]*" />', f'<meta name="description" content="{description}" />'),
        (r'<meta property="og:description" content="[^"]*" />', f'<meta property="og:description" content="{description}" />'),
    ):
        page, count = re.subn(pattern, lambda m: value, page, count=1)
        if count != 1:
            sys.exit(f"Expected a tag matching {pattern} in index.html")
    (root / f"{name}.html").write_text(page, encoding="utf-8", newline="\n")


# Facebook's data deletion setting links to /deletion. Its text is in wwwroot/pages, which the app shows too
write_static_page("deletion", "Deleting your data",
                  "How to delete what Stikling keeps about you, with or without an account.",
                  (root / "pages" / "deletion.html").read_text(encoding="utf-8"))

# Help and Privacy are rendered from the app's own pages by tools/static-pages/render.cs. Privacy is linked from
# Google's, Facebook's and Discord's sign-in setup.
help_html = (rendered / "help.html").read_text(encoding="utf-8")
write_static_page("help", "Help", "Short answers about Stikling: how it keeps your plants on your device, backups, accounts, and how plants, propagations, care and pests work.", help_html)
write_static_page("privacy", "Privacy", "What happens to what you put into Stikling, with and without an account.",
                  (rendered / "privacy.html").read_text(encoding="utf-8"))


class Markdown(HTMLParser):
    """The Help page as Markdown for llms.txt: topics as headings, each question in bold over its answer."""

    def __init__(self):
        super().__init__()
        self.out, self.href, self.skip = [], None, 0

    # Left out: the page title, the line under it and the links to each topic
    def skipped(self, tag, attrs):
        return tag in ("h1", "nav") or (tag == "p" and attrs.get("class") == "help-intro")

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if self.skip or self.skipped(tag, attrs):
            self.skip += tag not in ("br", "img")
            return
        if tag == "h2":
            self.out.append("\n\n### ")
        elif tag == "summary":
            self.out.append("\n\n**")
        elif tag == "p":
            self.out.append("\n\n")
        elif tag == "li":
            self.out.append("\n- ")
        elif tag == "em":
            self.out.append("*")
        elif tag == "a":
            self.href = attrs.get("href", "")
            self.out.append("[")

    def handle_endtag(self, tag):
        if self.skip:
            self.skip -= 1
        elif tag == "summary":
            self.out.append("**")
        elif tag == "em":
            self.out.append("*")
        elif tag == "a" and self.href is not None:
            href = self.href if "://" in self.href or self.href.startswith("mailto:") else f"https://stikling.app/{self.href}"
            self.out.append(f"]({href})")
            self.href = None

    def handle_data(self, data):
        if not self.skip:
            self.out.append(re.sub(r"\s+", " ", data))

    def text(self):
        text = "".join(self.out)
        text = re.sub(r"[ \t]+\n", "\n", text)
        text = re.sub(r"\n[ \t]+", "\n", text)
        return re.sub(r"\n{3,}", "\n\n", text).strip() + "\n"


# What Stikling is, for AI tools that read llms.txt (llmstxt.org). The answers come from the Help page, so they
# stay in step with the app.
parser = Markdown()
parser.feed(strip_back_link(help_html))
(root / "llms.txt").write_text(
    "# Stikling\n\n"
    "> A free app for keeping track of houseplants and the cuttings grown from them, with a history of photos "
    "and notes for each one. It runs in the browser on a phone or a computer, works offline, and needs no "
    "account. Signing in is optional and keeps the same plants on every device. It is open source.\n\n"
    "Stikling is Danish for a cutting. A cutting, or anything else grown into a new plant from one you have "
    "(a corm, an offset, a leaf, a division, seeds), gets its own record, linked to the plant it came from, "
    "with what it is rooting in, its stage and how long it took. Once potted up it becomes a plant of its own "
    "and keeps the link to its parent. The app also keeps a care log, pest cases with treatments, experiments "
    "comparing rooting mediums, and the pots, soil mixes and fertilisers used. There are no ads, no analytics "
    "and no tracking.\n\n"
    "## Links\n\n"
    "- [Stikling](https://stikling.app/): the app\n"
    "- [Help](https://stikling.app/help): answers to common questions, the same as below\n"
    "- [Privacy](https://stikling.app/privacy): what happens to what people put into the app\n"
    "- [Source code](https://github.com/LaugeM/stikling): the code on GitHub\n\n"
    "## Help\n\n" + parser.text(),
    encoding="utf-8",
    newline="\n",
)

# Stop Jekyll from hiding folders that start with an underscore (_framework)
(root / ".nojekyll").touch()

print(f"Prepared {root} for base path {base_path}")
