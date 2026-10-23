#!/usr/bin/env python3
"""Builds the dialogue map from Assets/Resources/Dialogue.txt (the file the game reads and the user edits):
  docs/DialogueMap.md              - the ID index ("T12.1" -> which section and line of Dialogue.txt)
  tools/dialogue-map/dialogue_map.html - the interactive page (published as an Artifact for the user)
IDs are kept in tools/dialogue-map/ids.txt so they never change: a section new to Dialogue.txt gets the next free number of its group.
A field's number (.n) is its place inside its section. Run: python3 tools/dialogue-map/build.py
"""
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
DIALOGUE = os.path.join(ROOT, "Assets", "Resources", "Dialogue.txt")
IDS = os.path.join(HERE, "ids.txt")

GROUPS = [
    ("T", "Tips", "'Got it' boxes the first time something happens (text = the box, overlay line = the event log line)"),
    ("G", "Tutorials & guides", "Intro tips, the guided tutorials (one box per step) and the Woodcutting guide"),
    ("P", "Perk popups", "The box shown when a skill reaches a level with a new perk"),
    ("C", "Characters", "Cubie's pages and every visitor's lines, buttons and log messages"),
]


def group_of(section):
    if section.startswith(("Tip:", "Minigame:")):
        return "T"
    if section.startswith(("Intro tips", "Tutorial:", "Guide:")):
        return "G"
    if section.startswith("Perk popup:"):
        return "P"
    return "C"


def nice_title(section):
    """A readable name for a section header ('Tip: device_Fan' -> 'Fan placed for the first time')."""
    kind, _, key = section.partition(":")
    key = key.strip()
    if kind == "Tip":
        words = key.replace("_", " ")
        for prefix, fmt in (("minigame_", "{} minigame starts"), ("device_", "{} device placed / got"), ("pixel_", "{} pixel first seen"),
                            ("upgrade_", "Upgrade bought: {}"), ("bank_", "Pixel Bank: {}"), ("timestop_", "Time Stop: {}"), ("node_", "Skill node: {}")):
            if key.startswith(prefix):
                name = re.sub(r"(?<=[a-z])(?=[A-Z])", " ", key[len(prefix):]).replace("_", " ")
                return fmt.format(name[:1].upper() + name[1:])
        return words[:1].upper() + words[1:]
    if kind == "Minigame":
        return key + " minigame hints"
    if kind == "Tutorial":
        return key + " tutorial"
    if kind == "Guide":
        return key + " guide"
    if kind == "Perk popup":
        return key[:1].upper() + key[1:]
    if kind == "Visitor":
        return key[:1].upper() + key[1:] + " (visitor)"
    return section


def parse():
    sections, cur = [], None
    with open(DIALOGUE, encoding="utf-8") as f:
        for n, raw in enumerate(f, 1):
            line = raw.rstrip("\n")
            m = re.match(r"^\[(.+)\]\s*$", line)
            if m:
                cur = {"section": m.group(1), "line": n, "fields": []}
                sections.append(cur)
                continue
            if cur is None or not line.strip() or line.lstrip().startswith("#"):
                continue
            key, sep, text = line.partition(":")
            if not sep:
                continue
            cur["fields"].append({"key": key.strip(), "text": text.strip(), "line": n})
    return sections


def load_ids():
    ids = {}
    if os.path.exists(IDS):
        with open(IDS, encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith("#"):
                    continue
                i, _, section = line.partition(" | ")
                ids[section.strip()] = i.strip()
    return ids


def assign_ids(sections):
    ids = load_ids()
    used = {}
    for i in ids.values():
        used[i[0]] = max(used.get(i[0], 0), int(i[1:]))
    added = False
    for s in sections:
        if s["section"] not in ids:
            g = group_of(s["section"])
            used[g] = used.get(g, 0) + 1
            ids[s["section"]] = f"{g}{used[g]}"
            added = True
        s["id"] = ids[s["section"]]
        s["group"] = s["id"][0]
        s["title"] = nice_title(s["section"])
    if added or not os.path.exists(IDS):
        with open(IDS, "w", encoding="utf-8") as f:
            f.write("# Dialogue map IDs: one per [section] of Assets/Resources/Dialogue.txt. Never renumber or reuse an ID (the user quotes them).\n")
            f.write("# New sections get the next free number of their group when build.py runs.\n")
            order = sorted(ids.items(), key=lambda kv: (kv[1][0], int(kv[1][1:])))
            for section, i in order:
                f.write(f"{i} | {section}\n")


def write_md(sections):
    out = ["# Dialogue map (dialogue IDs)", "",
           "Every 'Got it' box and every character's lines, with a short ID the user can quote (\"reword T12.1\", \"make C1 page 3 shorter\"). "
           "A letter for the group, a number for the section of `Assets/Resources/Dialogue.txt`, and `.n` for its n-th line in that section. "
           "Edit the text in Dialogue.txt (line numbers below); this file is generated by `python3 tools/dialogue-map/build.py`. "
           "IDs live in `tools/dialogue-map/ids.txt` and never change.", ""]
    for key, title, desc in GROUPS:
        out += [f"## {key} - {title}", "", desc, "", "| ID | Section | Dialogue.txt line | Lines |", "|---|---|---|---|"]
        for s in sections:
            if s["group"] != key:
                continue
            lines = "<br>".join(f"**{s['id']}.{n}** {f['key']}: {(f['text'][:90] + '...') if len(f['text']) > 90 else f['text']}"
                                for n, f in enumerate(s["fields"], 1))
            cells = [s["id"], f"{s['title']} `[{s['section']}]`", str(s["line"]), lines]
            out.append("| " + " | ".join(c.replace("|", "/") for c in cells) + " |")
        out.append("")
    with open(os.path.join(ROOT, "docs", "DialogueMap.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(out))


def write_html(sections):
    with open(os.path.join(ROOT, "tools", "ui-map", "page_template.html"), encoding="utf-8") as f:
        ui = f.read()
    style = ui[ui.index("<style>"):ui.index("</style>") + len("</style>")]   # (one look for both maps: the interface map's styles)
    with open(os.path.join(HERE, "page_template.html"), encoding="utf-8") as f:
        page = f.read()
    data = json.dumps({"groups": [{"key": k, "title": t, "desc": d} for k, t, d in GROUPS], "sections": sections}, ensure_ascii=False)
    page = page.replace("<!--SHARED STYLE-->", style)
    page = page.replace("/*DATA*/null", data.replace("</", "<\\/"))
    page = page.replace("{{COUNT}}", str(sum(len(s["fields"]) for s in sections)))
    with open(os.path.join(HERE, "dialogue_map.html"), "w", encoding="utf-8") as f:
        f.write(page)


if __name__ == "__main__":
    secs = parse()
    assign_ids(secs)
    write_md(secs)
    write_html(secs)
    print(f"{len(secs)} sections, {sum(len(s['fields']) for s in secs)} lines -> docs/DialogueMap.md, tools/dialogue-map/dialogue_map.html")
