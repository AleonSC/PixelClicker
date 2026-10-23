#!/usr/bin/env python3
"""Builds the interface map from tools/ui-map/ui_map.txt:
  docs/UIMap.md            - the ID index Claude reads to find the code behind an ID ("W4.3" -> Shop, Consumables tab, PixelShopUI.cs)
  tools/ui-map/ui_map.html - the interactive map page (published as an Artifact for the user)
Run: python3 tools/ui-map/build.py
"""
import html
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))

GROUPS = [
    ("H", "HUD", "Always-on screen pieces: the bars, their buttons, meters"),
    ("W", "Main windows", "Windows opened from the bars or keys"),
    ("N", "Popups & notices", "Tips, popups, tooltips, tutorials, shows"),
    ("M", "Menus", "Title screen, pause menu, settings, save / load"),
    ("O", "World UI", "Cards, panels and labels on objects in the world"),
    ("D", "Dev Hub", "F9 dev tools: tabs and Main-tab widgets"),
]

# Where a piece sits on the 16:9 screen sketch (x, y, w, h in % of the screen) and which sketch: 'hud' or 'win'.
SPOTS = {
    "H2": ("hud", 1, 0.9, 7.5, 4.2), "H3": ("hud", 9.2, 0.9, 6, 4.2), "H4": ("hud", 15.8, 0.9, 3, 4.2),
    "H6": ("hud", 82, 0.9, 8.5, 4.2), "H5": ("hud", 91, 0.9, 8, 4.2),
    "H7": ("hud", 1, 94.9, 7, 4.2), "H8": ("hud", 8.6, 94.9, 7, 4.2),
    "H11": ("hud", 78.6, 94.9, 8.4, 4.2), "H10": ("hud", 87.5, 94.9, 3, 4.2), "H9": ("hud", 91, 94.9, 8, 4.2),
    "H13": ("hud", 40, 7, 20, 4), "H15": ("hud", 38, 12.2, 24, 3.6), "H32": ("hud", 42, 16.6, 16, 3.6),
    "H16": ("hud", 1, 34, 17, 36), "H21": ("hud", 1, 8, 9, 22), "H22": ("hud", 84, 7, 15, 3.4),
    "H17": ("hud", 43, 86.5, 14, 5), "H19": ("hud", 80, 81, 18, 9), "H23": ("hud", 74, 84, 5, 7),
    "H31": ("hud", 44, 40, 12, 22),
    "N1": ("hud", 33, 21, 34, 10), "N4": ("hud", 83, 12, 16, 13), "N5": ("hud", 82, 44, 17, 9),
    "N7": ("hud", 57, 46, 8, 4), "N20": ("hud", 72, 60, 27, 21),
    "W1": ("win", 1, 7, 21, 42), "W5": ("win", 15, 12, 11, 62), "W4": ("win", 27, 12, 46, 72),
    "W7": ("win", 1, 52, 23, 41), "W9": ("win", 1, 18, 24, 62), "W10": ("win", 25.5, 18, 18, 62),
    "W12": ("win", 76, 18, 23, 62), "W14": ("win", 83, 30, 16, 38), "W15": ("win", 1, 26, 19, 48),
    "W16": ("win", 46, 20, 28, 56), "W19": ("win", 80, 47, 18, 34), "W13": ("win", 52, 54, 22, 30),
    "D1": ("win", 30, 8, 40, 30),
}


def load():
    items = []
    with open(os.path.join(HERE, "ui_map.txt"), encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            p = [x.strip() for x in line.split(" | ")]
            if len(p) != 8:
                raise SystemExit("bad line (needs 8 fields): " + line)
            parts = [x.strip() for x in p[7].split(";") if x.strip()]
            items.append({"id": p[0], "group": p[0][0], "name": p[2], "kind": p[3], "where": p[4],
                          "opened": p[5], "code": p[6], "parts": parts, "spot": SPOTS.get(p[0])})
    ids = [i["id"] for i in items]
    if len(ids) != len(set(ids)):
        raise SystemExit("duplicate IDs")
    return items


def write_md(items):
    out = ["# Interface map (UI IDs)", "",
           "Every piece of the game's interface has a short ID the user can quote (\"make W4.3 wider\"). "
           "A letter for the group, a number for the piece, and `.n` for its n-th part (the Parts column, in order).",
           "Generated from `tools/ui-map/ui_map.txt` by `python3 tools/ui-map/build.py` - edit the .txt, never this file. "
           "IDs never change: new pieces go at the end of their group, new parts at the end of their line. "
           "Line numbers drift as code changes; search the named builder method if a line is off.", ""]
    for key, title, desc in GROUPS:
        out += [f"## {key} - {title}", "", desc, "", "| ID | Piece | Kind | Where | Opened by | Code | Parts |", "|---|---|---|---|---|---|---|"]
        for i in items:
            if i["group"] != key:
                continue
            parts = "<br>".join(f"**{i['id']}.{n}** {p}" for n, p in enumerate(i["parts"], 1))
            cells = [i["id"], i["name"], i["kind"], i["where"], i["opened"], "`" + i["code"] + "`", parts]
            out.append("| " + " | ".join(c.replace("|", "/") for c in cells) + " |")
        out.append("")
    with open(os.path.join(ROOT, "docs", "UIMap.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(out))


def write_html(items):
    with open(os.path.join(HERE, "page_template.html"), encoding="utf-8") as f:
        page = f.read()
    data = json.dumps({"groups": [{"key": k, "title": t, "desc": d} for k, t, d in GROUPS], "items": items}, ensure_ascii=False)
    page = page.replace("/*DATA*/null", data.replace("</", "<\\/"))
    page = page.replace("{{COUNT}}", html.escape(str(len(items))))
    with open(os.path.join(HERE, "ui_map.html"), "w", encoding="utf-8") as f:
        f.write(page)


if __name__ == "__main__":
    items = load()
    write_md(items)
    write_html(items)
    print(f"{len(items)} pieces -> docs/UIMap.md, tools/ui-map/ui_map.html")
