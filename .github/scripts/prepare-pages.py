"""Prepare a published Blazor WebAssembly app for GitHub Pages.

GitHub Pages serves a project site from a subfolder (https://<user>.github.io/<repo>/),
so the <base href> in index.html has to point at that subfolder. Changing index.html
also changes its SHA-256 hash, and the PWA service worker refuses to cache files whose
hash doesn't match service-worker-assets.js, so the hash is updated there too.

Usage: python prepare-pages.py <wwwroot dir> <base path, e.g. /stikling/>
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

# Stop Jekyll from hiding folders that start with an underscore (_framework)
(root / ".nojekyll").touch()

print(f"Prepared {root} for base path {base_path}")
