#!/usr/bin/env python3
"""Builds tools/code-map/CodeMap.html: an interactive map of every script in Assets/Scripts.

Run it after adding / renaming scripts:  python3 tools/code-map/build.py
- A node = one class. Partial classes spread over several files (PixelShop + PixelShopUI + ...) are ONE node listing its files.
- A link A -> B = A's code (comments and strings stripped) names a type declared in B.
- Groups come from GROUPS below (first matching rule wins); the one-line description from docs/Index.md or the script's header comment.
"""
import json, os, re, collections

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPTS = os.path.join(ROOT, 'Assets', 'Scripts')
OUT = os.path.join(ROOT, 'tools', 'code-map', 'CodeMap.html')
TEMPLATE = os.path.join(ROOT, 'tools', 'code-map', 'template.html')

# (group, regex on the class name without 'Pixel'). Order matters: first match wins.
GROUPS = [
    ('Dev tools', r'^(Dev|FeatureTester|BalanceReport|SessionRecorder|CrashLog|Debug|LookStudio|ModelEditor|ModelPicture|BoundsSnapshot|TimeControl|Probe)'),
    ('Save and settings', r'^(Save|Autosaves|Prefs|SettingsSync|DisplaySettings|CustomKeys|Keys|MenuKeys|Input|Statics|ObjectId)'),
    ('Core loop', r'^(Clicker|BlockCatalog|BlockRules|BlockTags|BlockTextures|BlockHold|Looks|LookSource|ClickerAura|Combo|AutoClicker|Hold|PowerClick|ShuffleBonus|NextUp|HazardTell|TimeStop|Overcharge|Infinity|FirePixel|NuclearPixel|ChargeBooster|LightningRod|Stats|Achievements|OfflineProgress|Goals|Bank|Focus)'),
    ('Husks', r'^(Husk|Grab|CarryHandles|Body|Hold$|Snap|PathCarrier|VacuumContents|Mold|SapField)'),
    ('Devices and factory', r'(Device|Conveyor|Belt|Coupling|Socket|Recycler|SortingHopper|Sweeper|StorageCrate|Funnel|Kiln|Elevator|JunctionBox|ContainerHose|Tube|TrashCan|VacuumCleaner|ControlLever|BiomassGenerator|PowerBank|SolarPanel|FactoryKit|CraftingCube|WoodChipper|BaitBucket|Bait$|AimLaser|RobotWorker|RobotCockpit|RobotSaver|ElectricLinks|Operation|Seed|DeviceCard|DeviceInfo|Consumables|ConsumableGallery|InstantItems|Attachment)'),
    ('Shop and crafting', r'^(Shop|Crafting|Cubie)'),
    ('Skills', r'^(Skill|FishingSpawns|GreatOak|TreeMode|PestSwarm|TermiteSwarm|FarmFx|Prospector$|WhaleFight|ToadstoolDaze)'),
    ('Characters and visitors', r'(Vendor|Visitor|Wizard|Tinkerer|Entrepreneur|Farmer|Egg|Bean|Pets|GuideVendor|CubiePeek|Talk|Dialogue)'),
    ('Minigames', r'(Minigame|Meteor|Treasure)'),
    ('Rares and shows', r'^(Dragon|God|GoldenSnitch|Collection|Ultra|Prime)'),
    ('World and camera', r'^(Floor|Skybox|DayNight|Daylight|Filters|Camera|OrbitCamera|FreeCam|FirstPerson|OrthoView|Zoom|ViewBounds|ViewWindow|Shaders|Noise|RockMesh|RingMesh|WorldRing)'),
    ('Sound', r'(Audio|Synth|TitleMusic|ScrollSound)'),
    ('Tutorials and tips', r'^(Hints|HintBox|Notice|TipQueue|DidYouKnow|Tutorial|ShopTutorial|TogglesTutorial|SkillsTutorial|GuideTexts|PixelGuide|ShopNudge|StatusBox|StatusLines)'),
    ('Foundations', r'^(Systems|Find|Calm|PointerClaim|RightClick|RightTap|TextFile|TextOverrides|TierPicture|Highlight|HoverMarker|Windows|MyWindow)'),
    ('Effects', r'(Fx$|Bolts|ArcGraphic|Glow|BurstPop|ButtonPulse|Feedback|ElectricFx|RemoveTint)'),
    ('UI', r'.'),
]
COLORS = {
    'Core loop': '#e0a422', 'Husks': '#b07a4f', 'Devices and factory': '#3f9e7a', 'Shop and crafting': '#d0603d',
    'Skills': '#6f9e3a', 'Characters and visitors': '#2fa6a6', 'Minigames': '#c2457a', 'Rares and shows': '#9a63d6',
    'World and camera': '#4f7fd6', 'Sound': '#d68a3c', 'Tutorials and tips': '#8b8f3a', 'Foundations': '#7d8796',
    'Save and settings': '#5d6fa8', 'Dev tools': '#a05656', 'Effects': '#c9a0dc', 'UI': '#5b9bc4',
}

DECL = re.compile(r'^(?:public |internal |sealed |static |abstract |partial )*(?:class|struct|enum|interface)\s+(\w+)', re.M)

def strip_code(src):
    src = re.sub(r'/\*.*?\*/', ' ', src, flags=re.S)
    src = re.sub(r'@"(?:[^"]|"")*"', '""', src)
    src = re.sub(r'"(?:\\.|[^"\\\n])*"', '""', src)
    src = re.sub(r"'(?:\\.|[^'\\\n])'", "''", src)
    src = re.sub(r'//[^\n]*', ' ', src)
    return src

def header_summary(src):
    lines = []
    for l in src.split('\n')[:12]:
        l = l.strip()
        if not l.startswith('//'): break
        t = l.lstrip('/').strip()
        if not t or t.startswith('----') or t.endswith('.cs'): continue
        lines.append(t)
    s = ' '.join(lines)
    return s[:420]

def index_lines():
    d = {}
    p = os.path.join(ROOT, 'docs', 'Index.md')
    for l in open(p, encoding='utf-8'):
        if not l.startswith('| `'): continue
        cols = [c.strip() for c in l.strip().strip('|').split('|')]
        if len(cols) < 2: continue
        for name in re.findall(r'`(Pixel\w+)\.cs`', cols[0]):
            d.setdefault(name, (cols[1], cols[2] if len(cols) > 2 else ''))
    return d

def main():
    files = sorted(f for f in os.listdir(SCRIPTS) if f.endswith('.cs'))
    src = {}
    code = {}
    for f in files:
        s = open(os.path.join(SCRIPTS, f), encoding='utf-8', errors='replace').read()
        src[f] = s
        code[f] = strip_code(s)
    # Top-level types per file (column 0), and which node each type belongs to.
    # One node per file, named after the file; a file that only adds to a partial class of another file
    # (PixelShopUI.cs: 'partial class PixelShop') joins that file's node.
    node_of_file = {}
    for f in files:
        base = f[:-3]
        node_of_file[f] = base
        for t in DECL.findall(code[f]):
            if t != base and t + '.cs' in src and re.search(r'partial\s+class\s+' + t + r'\b', code[f]):
                node_of_file[f] = t
    # Which node each declared type belongs to (the file named after the type wins).
    type_node = {}
    for f in files:
        for t in DECL.findall(code[f]):
            if t not in type_node or t + '.cs' == f: type_node[t] = node_of_file[f]
    nodes = collections.OrderedDict()
    idx = index_lines()
    for f in files:
        n = node_of_file[f]
        node = nodes.setdefault(n, {'id': n, 'files': [], 'lines': 0, 'desc': '', 'doc': ''})
        node['files'].append(f)
        node['lines'] += src[f].count('\n') + 1
    for n, node in nodes.items():
        mainf = n + '.cs' if n + '.cs' in node['files'] else node['files'][0]
        if n in idx: node['desc'], node['doc'] = idx[n]
        if not node['desc']: node['desc'] = header_summary(src[mainf])
        node['desc'] = re.sub(r'\s+', ' ', node['desc'])[:420]
        short = n[5:] if n.startswith('Pixel') else n
        for g, rx in GROUPS:
            if re.search(rx, short): node['group'] = g; break
    # links
    word = re.compile(r'\b([A-Z]\w+)\b')
    links = collections.Counter()
    for f in files:
        a = node_of_file[f]
        seen = set()
        for m in word.finditer(code[f]):
            t = m.group(1)
            b = type_node.get(t)
            if b and b != a and b in nodes: seen.add(b)
        for b in seen: links[(a, b)] += 1
    data = {
        'nodes': list(nodes.values()),
        'links': [{'source': a, 'target': b} for (a, b) in links],
        'colors': COLORS,
        'groups': [g for g, _ in GROUPS],
    }
    html = open(TEMPLATE, encoding='utf-8').read().replace('/*__DATA__*/null', json.dumps(data, separators=(',', ':')))
    open(OUT, 'w', encoding='utf-8').write(html)
    print('nodes', len(nodes), 'links', len(links), '->', OUT)
    c = collections.Counter(n['group'] for n in nodes.values())
    print(dict(c))

if __name__ == '__main__':
    main()
