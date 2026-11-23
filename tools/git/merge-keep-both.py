"""Git merge driver for the files every branch adds to at the same spot.

Two branches that each add a text key to a .resx file, or a line to the
current month in WhatsNew.razor, conflict even though both changes belong in
the result. This driver keeps both. Anything that is a real conflict, like
both branches changing the same key in different ways, is left to git as
usual, with conflict markers.

.resx files are merged key by key: a key added, changed or removed on one
side is taken from that side, and keys added on the other branch go after the
last key. WhatsNew.razor is merged by git, and a conflict is settled only when
every line in it on both sides is a Translated(Strings.WhatsNew.X) line. This
branch's lines go first, so the newest stays on top.

Git runs it as: merge-keep-both.py %O %A %B %P
(base, ours, theirs, path). The result is written to %A. Exit 0 means merged.
docs/dev/merging.md says how to turn it on.
"""
import re
import subprocess
import sys
import xml.dom.minidom

DATA = re.compile(r'^[ \t]*<data name="([^"]+)"[^>]*>.*?</data>[ \t]*\r?\n', re.M | re.S)
WHATS_NEW_LINE = re.compile(r"^\s*Translated\(Strings\.WhatsNew\.\w+\),\s*$")


def read(path):
    with open(path, encoding="utf-8", newline="") as f:
        return f.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def git_merge_file(base, ours, theirs):
    """Plain git merge into ours. Returns True when it merged without conflicts."""
    result = subprocess.run(
        ["git", "merge-file", "-L", "this branch", "-L", "base", "-L", "other branch", ours, base, theirs]
    )
    return result.returncode == 0


def split(text):
    """Text pieces and data entries in order: [text, (name, block), text, ...]."""
    pieces, at = [], 0
    for m in DATA.finditer(text):
        pieces.append(text[at:m.start()])
        pieces.append((m.group(1), m.group(0)))
        at = m.end()
    pieces.append(text[at:])
    return pieces


def entries(pieces):
    found = {}
    for p in pieces:
        if isinstance(p, tuple):
            if p[0] in found:
                raise ValueError(f"key {p[0]} is in the file twice")
            found[p[0]] = p[1]
    return found


def skeleton(pieces):
    """All the text outside the entries."""
    return "".join(p for p in pieces if not isinstance(p, tuple)).replace("\r\n", "\n")


def merge_resx(base_text, ours_text, theirs_text):
    """Key by key merge. Returns the merged text, or None for a real conflict."""
    b, o, t = split(base_text), split(ours_text), split(theirs_text)
    try:
        be, oe, te = entries(b), entries(o), entries(t)
    except ValueError:
        return None
    # Text outside the entries (header, schema) must not have changed on the other side.
    if skeleton(t) != skeleton(b) and skeleton(t) != skeleton(o):
        return None
    nl = "\r\n" if "\r\n" in ours_text else "\n"

    def same(x, y):
        return x is not None and y is not None and x.replace("\r\n", "\n") == y.replace("\r\n", "\n")

    def as_ours(block):
        return block.replace("\r\n", "\n").replace("\n", nl)

    merged = {}
    for name in set(be) | set(oe) | set(te):
        bv, ov, tv = be.get(name), oe.get(name), te.get(name)
        if ov is None and tv is None:
            continue
        if bv is None:  # added on one side or both
            if ov is not None and tv is not None and not same(ov, tv):
                return None
            merged[name] = ov if ov is not None else as_ours(tv)
        elif ov is None:  # removed here
            if not same(tv, bv):
                return None
        elif tv is None:  # removed on the other branch
            if not same(ov, bv):
                return None
        elif same(ov, bv):
            merged[name] = as_ours(tv)
        elif same(tv, bv) or same(tv, ov):
            merged[name] = ov
        else:
            return None

    out, last = [], -1
    for p in o:
        if isinstance(p, tuple):
            if p[0] in merged:
                out.append(merged[p[0]])
            last = len(out)
        else:
            out.append(p)
    added = [merged[name] for name in te if name in merged and name not in oe]
    if last < 0:  # no entries here yet: put them before </root>
        text = "".join(out)
        at = text.rfind("</root>")
        if at < 0:
            return None
        text = text[:at] + "".join(added) + text[at:]
    else:
        text = "".join(out[:last] + added + out[last:])
    try:
        xml.dom.minidom.parseString(text.encode("utf-8"))
    except Exception:
        return None
    return text


def settle_whats_new(text):
    """Settle conflicts made only of WhatsNew lines. Returns None if any other conflict is left."""
    nl = "\r\n" if "\r\n" in text else "\n"
    lines, out, i = text.split(nl), [], 0
    while i < len(lines):
        if not lines[i].startswith("<<<<<<<"):
            out.append(lines[i])
            i += 1
            continue
        ours, theirs, part = [], [], None
        i += 1
        while i < len(lines) and not lines[i].startswith(">>>>>>>"):
            if lines[i].startswith("|||||||"):
                part = "base"
            elif lines[i].startswith("======="):
                part = "theirs"
            elif part is None:
                ours.append(lines[i])
            elif part == "theirs":
                theirs.append(lines[i])
            i += 1
        i += 1
        both = ours + theirs
        if not all(WHATS_NEW_LINE.match(line) for line in both):
            return None
        seen = set()
        for line in both:
            if line.strip() not in seen:
                seen.add(line.strip())
                out.append(line)
    return nl.join(out)


def main():
    base, ours, theirs, path = sys.argv[1:5]
    if path.endswith(".resx"):
        merged = merge_resx(read(base), read(ours), read(theirs))
        if merged is not None:
            write(ours, merged)
            return 0
        return 0 if git_merge_file(base, ours, theirs) else 1
    if git_merge_file(base, ours, theirs):
        return 0
    if path.endswith("WhatsNew.razor"):
        settled = settle_whats_new(read(ours))
        if settled is not None:
            write(ours, settled)
            return 0
    return 1


if __name__ == "__main__":
    sys.exit(main())
