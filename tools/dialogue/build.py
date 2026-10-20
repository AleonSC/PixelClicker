#!/usr/bin/env python3
"""Builds Assets/Resources/Dialogue.txt from the game's BUILT-IN texts (tips, intro boxes, tutorials, the Woodcutting guide, perk popups,
Cubie, every visitor / vendor, the Black Hole message), in the exact sections / fields PixelDialogue reads. Run it only to start a fresh file:
it overwrites the file (pass --check to only print what it found). The game itself also appends missing texts on the first play in the editor."""
import re, sys, os
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
S = os.path.join(ROOT, "Assets", "Scripts")
def src(name): return open(os.path.join(S, name), encoding="utf-8").read()
LIT = r'"(?:[^"\\]|\\.)*"'

def string_expr(text, pos):
    """Reads a C# expression of string literals joined by '+' starting at pos; returns its value (escapes kept as written, \\" -> ")."""
    out, i = [], pos
    while True:
        m = re.compile(r'\s*(' + LIT + r')').match(text, i)
        if not m: break
        out.append(m.group(1)[1:-1].replace('\\"', '"'))
        i = m.end()
        p = re.compile(r'\s*\+').match(text, i)
        if not p: break
        i = p.end()
    return "".join(out) if out else None

def field_default(code, field):
    m = re.search(r'\b' + field + r'\s*=\s*(?=")', code)
    return string_expr(code, m.end()) if m else None

def list_literals(block):
    return [string_expr(block, m.start()) for m in re.finditer(LIT + r'(?:\s*\+\s*' + LIT + r')*\s*,?\s*\n', block)]

sections = []   # (name, [(field, value)])
def add(name, fields): sections.append((name, fields))

# --- first-time tips ---
h = src("PixelHints.cs")
shorts = dict(re.findall(r'case "([^"]+)": return (' + LIT + r');', h[h.index("private static string ShortFor"):]))
shorts = {k: v[1:-1].replace('\\"', '"') for k, v in shorts.items()}
for m in re.finditer(r'\bH\("([^"]+)",\s*', h):
    add("Tip: " + m.group(1), [("text", string_expr(h, m.end())), ("overlay line", shorts.get(m.group(1), ""))])
add("Intro tips", [("log", field_default(h, "introLogMessage")), ("inventory", field_default(h, "introInventoryText")),
                   ("earn", field_default(h, "introEarnText")), ("right click", field_default(h, "introRightClickText"))])

# --- tutorials (only the text of each step) ---
def steps(file, start):
    c = src(file); i = c.index(start); body = c[i:c.index("};", i)]
    return [string_expr(body, m.end()) for m in re.finditer(r'\btext\s*=\s*(?=")', body)]
for title, file, start in [("Shop", "PixelShopTutorial.cs", "static List<Step> DefaultSteps"), ("Toggles", "PixelTogglesTutorial.cs", "static List<Step> DefaultSteps"),
                           ("Skills", "PixelSkillsTutorial.cs", "List<TutorialStep> DefaultTutorialSteps")]:
    add("Tutorial: " + title, [("step %d" % (n + 1), t) for n, t in enumerate(steps(file, start))])

# --- Woodcutting guide ---
g = src("PixelSkillsGuide.cs"); i = g.index("DefaultWoodGuide() => new"); body = g[i:g.index("};", i)]
add("Guide: Woodcutting", [("question", field_default(g, "woodGuideQuestion")), ("later", field_default(g, "woodGuideLaterText"))]
    + [("step %d" % (n + 1), t) for n, t in enumerate(list_literals(body[body.index("{"):]))])

# --- perk popups ---
consts = {}
for f in ("PixelSkills.cs", "PixelSkillsStandard.cs"):
    c = src(f)
    for m in re.finditer(r'const string (\w+Notice)\s*=\s*', c): consts[m.group(1)] = string_expr(c, m.end())
perks = [("standard", 2, "StandardLevel2Notice"), ("standard", 3, "StandardLevel3Notice"), ("woodcutting", 2, "WoodChipperNotice"),
         ("woodcutting", 3, "WoodLifespanNotice"), ("woodcutting", 5, "WoodGuestNotice"), ("electrician", 5, "ConveyorNotice"),
         ("electrician", 10, "ConveyorWideNotice"), ("electrician", 20, "ConveyorWidestNotice")]
for pack, lvl, name in perks:
    if consts.get(name): add("Perk popup: %s level %d" % (pack, lvl), [("text", consts[name])])

# --- Cubie ---
c = src("PixelGuideVendor.cs")
def pages(start):
    i = c.index(start); body = c[i:c.index("};", i)]
    return list_literals(body[body.index("{"):])
add("Cubie", [("name", field_default(c, "characterName")), ("next button", field_default(c, "nextText")), ("last button", field_default(c, "lastText")),
              ("recycler gift", field_default(c, "recyclerGiftText"))]
    + [("visit 1 page %d" % (n + 1), t) for n, t in enumerate(pages("private string[] DefaultOne"))]
    + [("visit 2 page %d" % (n + 1), t) for n, t in enumerate(pages("private string[] DefaultTwo"))])

# --- visitors / vendors ---
base = src("PixelVisitorMinigame.cs")
common = [("accept button", field_default(base, "acceptText")), ("reject button", field_default(base, "rejectText")), ("done button", field_default(base, "doneText")),
          ("buy button", field_default(base, "buyText")), ("full label", field_default(base, "fullText")),
          ("peek bubble", field_default(base, "peekText")), ("missed log", field_default(base, "missedLog"))]
def prop(code, name):
    m = re.search(r'override string ' + name + r'\s*=>\s*', code)
    return string_expr(code, m.end()) if m else None
visitors = [("PixelTinkererMinigame.cs", []), ("PixelWizardMinigame.cs", []), ("PixelEntrepreneurMinigame.cs", []),
            ("PixelFarmerMinigame.cs", [("dragon aside", "dragonAsideText")]), ("PixelProspectorVendor.cs", []), ("PixelLumberjackVendor.cs", []),
            ("PixelEggMinigame.cs", [("farewell", "farewellText"), ("gift farewell", "giftFarewellText")]),
            ("PixelBeanMinigame.cs", [("farewell", "farewellText"), ("gift farewell", "giftFarewellText")])]
for file, extras in visitors:
    v = src(file)
    vid = re.search(r'override string Id => "([^"]+)"', v).group(1)
    greet = prop(v, "DefaultGreeting") or ""   # (Egg / Bean have two greetings: empty keeps both)
    fields = [("name", prop(v, "DefaultName")), ("greeting", greet), ("wares title", prop(v, "DefaultWaresTitle")),
              ("arrival log", prop(v, "DefaultArrivedLog") or ((prop(v, "DefaultName") or vid) + " is here!"))] + common
    fields += [(f, field_default(v, var)) for f, var in extras]
    if vid == "wizard": fields.append(("gauntlet aside", field_default(src("PixelInfinity.cs"), "wizardAside")))
    add("Visitor: " + vid, fields)

add("Minigame: Black Hole", [("first hole", field_default(src("PixelBlackholeMinigame.cs"), "firstHoleMessage"))])

# --- write ---
header = re.search(r'new PixelTextFile\("Dialogue",\s*((?:' + LIT + r'\s*\+?\s*)+)\)', src("PixelDialogue.cs")).group(1)
header_text = string_expr(header, 0).replace("\\\\", "\x00").replace("\\n", "\n").replace("\x00", "\\")   # (C# escapes: \\ = a backslash, \n = a new line)
out = [header_text.rstrip("\n"), "# Egg and Bean: their greeting is left empty here on purpose (it keeps their two built-in greetings: first visit / later).", ""]
missing = []
for name, fields in sections:
    out.append("[" + name + "]")
    for f, val in fields:
        if val is None: missing.append(name + " / " + f); val = ""
        out.append(f + ": " + val)
    out.append("")
if "--check" in sys.argv:
    print("\n".join(out)); print("MISSING:", missing, file=sys.stderr); sys.exit(0)
open(os.path.join(ROOT, "Assets", "Resources", "Dialogue.txt"), "w", encoding="utf-8").write("\n".join(out))
print("wrote %d sections; missing: %s" % (len(sections), missing))
