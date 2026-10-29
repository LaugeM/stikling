"""Prepare a published Blazor WebAssembly app for GitHub Pages.

The <base href> in index.html has to match where the site is served from: "/" on its own
domain, or the repository's subfolder on https://<user>.github.io/<repo>/. Changing index.html
also changes its SHA-256 hash, and the PWA service worker refuses to cache files whose
hash doesn't match service-worker-assets.js, so the hash is updated there too (and for sitemap.xml).

It also writes the pages search engines and AI tools read without running the app. For each ready language
(English at the root, the others under /<code>/, like /da/) that is the front page and help.html, privacy.html
and deletion.html, made from the folder tools/static-pages/render.cs renders them to. Every page links to the
same page in the other languages, the sitemap lists them all, and llms.txt is built from the English Help.

Usage: python prepare-pages.py <wwwroot dir> <base path, e.g. / or /stikling/> <rendered pages dir>
"""
import base64
import hashlib
import html as htmllib
import json
import pathlib
import re
import shutil
import sys
from html.parser import HTMLParser

SITE = "https://stikling.app/"
PAGES = ("help", "privacy", "deletion")

root = pathlib.Path(sys.argv[1])
base_path = sys.argv[2]
rendered = pathlib.Path(sys.argv[3])
if not (base_path.startswith("/") and base_path.endswith("/")):
    sys.exit(f"Base path must start and end with '/': {base_path}")

index = root / "index.html"
template = index.read_text(encoding="utf-8")
template, count = re.subn(r'<base href="[^"]*"\s*/>', f'<base href="{base_path}" />', template)
if count != 1:
    sys.exit("Expected exactly one <base href> tag in index.html")

# The languages that are ready, in the order the app lists them. English is the one at the root.
languages = json.loads((rendered / "languages.json").read_text(encoding="utf-8"))
codes = [language["code"] for language in languages]
if "en" not in codes:
    sys.exit("languages.json has no English")
texts = {code: json.loads((rendered / code / "text.json").read_text(encoding="utf-8")) for code in codes}


def prefix(code):
    return "" if code == "en" else f"{code}/"


# The address of a page, relative to the base href. name is "" for the front page, else help, privacy or deletion.
def local_href(code, name):
    return (prefix(code) + name) or "."


def full_url(code, name):
    return SITE + prefix(code) + name


# The HTML Blazor renders writes every character outside ASCII as &#xE5;. Plain characters are easier to read
# in the source of the page, and in the text llms.txt is made from.
def plain_characters(text):
    return re.sub(
        r"&#x([0-9A-Fa-f]+);",
        lambda m: chr(int(m.group(1), 16)) if int(m.group(1), 16) > 0xA0 else m.group(0),
        text,
    )


# Links to the app's own pages stay in the language of the page: help, privacy and deletion, and the front page.
# Anything else, like the link to Settings, goes to the app, which picks its language itself.
def keep_in_language(text, code):
    if code == "en":
        return text
    text = re.sub(
        r'href="(help|privacy|deletion)((?:#[^"]*)?)"',
        lambda m: f'href="{prefix(code)}{m.group(1)}{m.group(2)}"',
        text,
    )
    return re.sub(r'href="(?:\.|\./)?"', f'href="{prefix(code)}"', text)


# Visitors from a search land on the static pages, so a link back to Settings means nothing to them yet
def strip_back_link(text):
    return re.sub(r'\s*<a class="back-link"[^>]*>.*?</a>', "", text, count=1, flags=re.S)


# Puts value between the two groups of the pattern, once
def fill(pattern, value, page, flags=0):
    page, count = re.subn(pattern, lambda m: m.group(1) + value + m.group(2), page, count=1, flags=flags)
    if count != 1:
        sys.exit(f"Expected exactly one match for {pattern} in index.html")
    return page


def attribute(value):
    return htmllib.escape(value, quote=True)


def element_text(value):
    return htmllib.escape(value, quote=False)


# The links to the same page in the other languages, like <a href="da/help" hreflang="da" lang="da">Dansk</a>
def language_links(code, name):
    links = "".join(
        f'<a href="{local_href(other["code"], name)}" hreflang="{other["code"]}" lang="{other["code"]}">{htmllib.escape(other["name"])}</a>'
        for other in languages
        if other["code"] != code
    )
    return f'<p class="language-links">{links}</p>' if links else ""


# One page of the site: the template with a language's head tags, shell text and hreflang links. name is "" for
# the front page. title and description are the page's own, in that language, and body is its rendered HTML.
def make_page(code, name, title, description, body):
    text = texts[code]
    url = full_url(code, name)
    page = template.replace('<html lang="en">', f'<html lang="{code}">', 1)
    page = fill(r"(<title>).*?(</title>)", element_text(title), page, flags=re.S)

    # Its own address, title and description, also for link previews, which go by og:url
    page = fill(r'(<link rel="canonical" href=")[^"]*(" />)', url, page)
    alternates = "".join(f'\n    <link rel="alternate" hreflang="{c}" href="{full_url(c, name)}" />' for c in codes)
    alternates += f'\n    <link rel="alternate" hreflang="x-default" href="{full_url("en", name)}" />'
    canonical = f'<link rel="canonical" href="{url}" />'
    page = page.replace(canonical, canonical + alternates, 1)
    page = fill(r'(<meta property="og:url" content=")[^"]*(" />)', url, page)
    page = fill(r'(<meta property="og:title" content=")[^"]*(" />)', attribute(title if name else "Stikling"), page)
    page = fill(r'(<meta name="description" content=")[^"]*(" />)', attribute(description), page)
    page = fill(r'(<meta property="og:description" content=")[^"]*(" />)', attribute(description), page)
    page = fill(r'(<meta property="og:image:alt" content=")[^"]*(" />)', attribute(text["ogImageAlt"]), page)
    # The description in the structured data, written as JSON
    page = fill(r'("@type": "WebApplication",.*?"description": ")[^"]*(")',
                json.dumps(text["description"], ensure_ascii=False)[1:-1], page, flags=re.S)

    # The text around the app: the icon while it starts, and the bar when something fails
    page = fill(r'(<div class="intro-quiet"><img [^>]*?alt=")[^"]*(")', attribute(text["shellStarting"]), page)
    page = fill(r'(<p class="intro-quiet-text">)[^<]*(</p>)', element_text(text["shellOpening"]), page)
    page = fill(r'(<div class="error-ui-body">\s*<span>)[^<]*(</span>)', element_text(text["shellError"]), page)
    page = fill(r'(class="reload btn btn-primary btn-sm">)[^<]*(</a>)', element_text(text["shellReload"]), page)
    page = fill(r'(class="dismiss btn btn-link btn-sm">)[^<]*(</button>)', element_text(text["shellClose"]), page)

    body = keep_in_language(plain_characters(body), code)
    if name == "":
        # What a first visit sees while the app downloads, with the other languages in the footer
        body = body.replace("</footer>", language_links(code, name) + "</footer>", 1)
        page, count = re.subn(r"<!-- intro:.*?<!-- /intro -->", lambda m: body, page, count=1, flags=re.S)
        if count != 1:
            sys.exit("Expected exactly one <!-- intro: --> ... <!-- /intro --> block in index.html")
        # The shell text of every language, so the icon and the error bar follow the language the person uses
        shell = {
            c: {key[len("shell"):].lower(): value for key, value in texts[c].items() if key.startswith("shell")}
            for c in codes
        }
        page = fill(r'(<script type="application/json" id="shell-text">)[^<]*(</script>)',
                    json.dumps(shell, ensure_ascii=False).replace("</", "<\\/"), page)
    else:
        # A page the app also has, as plain HTML with its text in place of the front page. Pages serves name.html
        # for /name with a 200, and search engines, AI tools and Meta's check for the deletion page read it
        # without running the app. The app replaces it with the same page once it has started.
        page, count = re.subn(r"\s*<!-- intro:.*?<!-- /intro -->", "", page, flags=re.S)
        if count != 1:
            sys.exit("Expected exactly one <!-- intro: --> ... <!-- /intro --> block in index.html")
        main = f'\n<main class="static-page">\n{language_links(code, name)}\n{strip_back_link(body)}</main>\n'
        page, count = re.subn(
            r'(<div id="app">).*?(</div>\s*<div id="blazor-error-ui"[^>]*>)',
            lambda m: m.group(1) + main + m.group(2),
            page,
            count=1,
            flags=re.S,
        )
        if count != 1:
            sys.exit('Expected exactly one <div id="app"> in index.html')
    return page


def write_page(code, name, page):
    folder = root / prefix(code).rstrip("/")
    folder.mkdir(exist_ok=True)
    (folder / ("index.html" if name == "" else f"{name}.html")).write_text(page, encoding="utf-8", newline="\n")


# The front page and the three static pages, in each language. Help and Privacy are rendered from the app's own
# pages by tools/static-pages/render.cs, and so is the page on deleting your data, which Facebook's data deletion
# setting links to. Privacy is linked from Google's, Facebook's and Discord's sign-in setup.
for code in codes:
    text = texts[code]
    front = (rendered / code / "front.html").read_text(encoding="utf-8")
    write_page(code, "", make_page(code, "", text["title"], text["description"], front))
    for name in PAGES:
        body = (rendered / code / f"{name}.html").read_text(encoding="utf-8")
        write_page(code, name, make_page(code, name, f'{text[name + "Title"]} · Stikling', text[name + "Description"], body))

# Every page of the site, in every language
sitemap = (
    '<?xml version="1.0" encoding="UTF-8"?>\n'
    "<!-- The pages that are served as plain HTML (see .github/scripts/prepare-pages.py), in every language that is ready. "
    "Every other path loads the app. -->\n"
    '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n'
    + "".join(f"  <url><loc>{full_url(code, name)}</loc></url>\n" for code in codes for name in ("", *PAGES))
    + "</urlset>\n"
)
(root / "sitemap.xml").write_text(sitemap, encoding="utf-8", newline="\n")

# Keep the service worker's integrity check happy for the files changed above
manifest = root / "service-worker-assets.js"
manifest_text = manifest.read_text(encoding="utf-8")
for name in ("index.html", "sitemap.xml"):
    digest = base64.b64encode(hashlib.sha256((root / name).read_bytes()).digest()).decode()
    manifest_text, count = re.subn(
        r'("hash":\s*")sha256-[^"]+(",\s*"url":\s*"' + re.escape(name) + '")',
        lambda m: f"{m.group(1)}sha256-{digest}{m.group(2)}",
        manifest_text,
    )
    if count != 1:
        sys.exit(f"Expected exactly one {name} entry in service-worker-assets.js")
manifest.write_text(manifest_text, encoding="utf-8", newline="\n")

# The pre-compressed copies would still contain the old files; Pages doesn't need them
for name in ("index.html", "sitemap.xml"):
    for stale in (f"{name}.gz", f"{name}.br"):
        (root / stale).unlink(missing_ok=True)

# GitHub Pages serves 404.html for unknown paths, which lets deep links like /plants load the app. It answers
# with a 404, so search engines leave those paths out, and the pages above that should be found get their own file.
shutil.copyfile(index, root / "404.html")


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


# What Stikling is, for AI tools that read llms.txt (llmstxt.org). It stays in English. The answers come from the
# English Help page, so they stay in step with the app.
parser = Markdown()
parser.feed(strip_back_link((rendered / "en" / "help.html").read_text(encoding="utf-8")))
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
