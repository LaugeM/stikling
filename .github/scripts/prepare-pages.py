"""Prepare a published Blazor WebAssembly app for GitHub Pages.

The <base href> in index.html has to match where the site is served from: "/" on its own
domain, or the repository's subfolder on https://<user>.github.io/<repo>/. Changing index.html
also changes its SHA-256 hash, and the PWA service worker refuses to cache files whose
hash doesn't match service-worker-assets.js, so the hash is updated there too.

Usage: python prepare-pages.py <wwwroot dir> <base path, e.g. / or /stikling/>
"""
import base64
import hashlib
import pathlib
import re
import shutil
import sys

root = pathlib.Path(sys.argv[1])
base_path = sys.argv[2]
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

# GitHub Pages serves 404.html for unknown paths, which lets deep links like /plants load the app
shutil.copyfile(index, root / "404.html")

# The privacy page is linked from Google's, Facebook's and Discord's sign-in setup, so it answers 200 rather than the 404
# the other deep links get. Pages serves privacy.html for /privacy.
shutil.copyfile(index, root / "privacy.html")

# Facebook's data deletion setting links to /deletion, and Meta's check reads the page without running
# the app, so the page's text from wwwroot/pages goes into the HTML in place of the loading spinner.
# The app replaces it with the same text once it has started.
def write_static_page(name, title):
    text = (root / "pages" / f"{name}.html").read_text(encoding="utf-8")
    page, count = re.subn(
        r'(<div id="app">).*?(</div>\s*<div id="blazor-error-ui">)',
        lambda m: f'{m.group(1)}\n<main class="static-page">\n{text}</main>\n{m.group(2)}',
        html,
        flags=re.S,
    )
    if count != 1:
        sys.exit("Expected exactly one <div id=\"app\"> in index.html")
    page = page.replace("<title>Stikling</title>", f"<title>{title} · Stikling</title>", 1)
    (root / f"{name}.html").write_text(page, encoding="utf-8", newline="\n")


write_static_page("deletion", "Deleting your data")

# Stop Jekyll from hiding folders that start with an underscore (_framework)
(root / ".nojekyll").touch()

print(f"Prepared {root} for base path {base_path}")
