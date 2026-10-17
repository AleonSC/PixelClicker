#!/usr/bin/env python3
"""Builds docs/GameReference.txt: one plain-text reference of the whole game for AI readers.
Section 1 = tools/game-reference/overview.txt (hand-written). The rest is generated from the code and the existing notes.
Run from anywhere: python3 tools/game-reference/build.py"""
import glob, os, re, datetime

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SCRIPTS = os.path.join(ROOT, 'Assets', 'Scripts')
OUT = os.path.join(ROOT, 'docs', 'GameReference.txt')

def read(path):
    with open(path, encoding='utf-8', errors='ignore') as f:
        return f.read()

def plain(text):
    """Markdown -> plain text: drop bold / code marks and heading hashes, collapse blank runs."""
    text = text.replace('**', '').replace('`', '')
    text = re.sub(r'^#+\s*', '', text, flags=re.M)
    text = re.sub(r'\n{3,}', '\n\n', text)
    return text.strip()

def script_files():
    return sorted(glob.glob(os.path.join(SCRIPTS, '**', '*.cs'), recursive=True))

out = []
def section(title):
    out.append('\n=== ' + title + ' ===')

# 1. Overview
out.append(read(os.path.join(os.path.dirname(__file__), 'overview.txt')).strip())
out.append('GENERATED: ' + datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%d %H:%M UTC'))

# 2. Data model (CLAUDE.md)
claude = read(os.path.join(ROOT, 'CLAUDE.md'))
m = re.search(r'## Data model\n(.*?)\n## ', claude, re.S)
if m:
    section('2. DATA MODEL (from CLAUDE.md)')
    out.append(plain(m.group(1)))

# 3. Enums
section('3. ENUMS (name = value; values are only ever appended)')
wanted = ['PixelType', 'PixelTag', 'DeviceKind', 'UpgradeEffect', 'ShopTab', 'RequirementKind', 'TierUnlockMode', 'SpawnSwitch',
          'PixelAction', 'ItemKind', 'ResultKind', 'BlockSurface', 'PixelViewKind', 'CueTrigger', 'SlotKind', 'PixelOperationStat', 'Kind']
for f in script_files():
    src = read(f)
    for em in re.finditer(r'enum\s+(\w+)\s*(?::\s*\w+\s*)?\{(.*?)\}', src, re.S):
        name, body = em.group(1), em.group(2)
        if name not in wanted:
            continue
        body = re.sub(r'//[^\n]*', '', body)
        body = re.sub(r'/\*.*?\*/', '', body, flags=re.S)
        body = re.sub(r'\[[^\]]*\]', '', body)
        items = [re.sub(r'\s+', ' ', x).strip() for x in body.split(',') if x.strip()]
        out.append('%s (%s): %s' % (name, os.path.basename(f), ', '.join(items)))

# 4. Block catalog
section('4. BLOCK CATALOG (PixelBlockCatalog.cs: type | tags | Pixel Guide text)')
cat = read(os.path.join(SCRIPTS, 'PixelBlockCatalog.cs'))
parts = re.split(r'\n\s*B\(PixelType\.', cat)
for p in parts[1:]:
    head = re.match(r'(\w+)\s*,\s*([^,\n]*)', p)
    if not head:
        continue
    typ, tags = head.group(1), head.group(2).replace('PixelTag.', '').replace(' | ', '+').strip()
    g = re.search(r'guide:\s*t\s*=>\s*"((?:[^"\\]|\\.)*)"', p)
    guide = g.group(1).replace('\\n', ' ') if g else ''
    out.append('%s | %s | %s' % (typ, tags, guide))

# 5. Skills
section('5. SKILLS (PixelSkills ids; full rules in the PixelSkills / SkillBlocks notes below)')
for f in sorted(glob.glob(os.path.join(SCRIPTS, 'PixelSkills*.cs'))):
    for sm in re.finditer(r'public const string (\w+Id) = "(\w+)"', read(f)):
        out.append('%s = "%s"' % sm.groups())

# 6. Shop packs
section('6. SHOP PACKS (PixelShopDefaults.cs Create...Pack: method | first displayName | hidden in shop?)')
defaults = read(os.path.join(SCRIPTS, 'PixelShopDefaults.cs'))
for pm in re.finditer(r'(?:private|public|internal)\s+static\s+ShopPack\s+(Create\w+)\s*\(.*?\)\s*\{(.*?)\n    \}', defaults, re.S):
    name, body = pm.group(1), pm.group(2)
    dn = re.search(r'displayName = "((?:[^"\\]|\\.)*)"', body)
    desc = re.search(r'description = "((?:[^"\\]|\\.)*)"', body)
    hidden = 'hidden' if re.search(r'hideInShop = true', body) else 'shown'
    out.append('%s | %s | %s | %s' % (name, dn.group(1) if dn else '?', hidden, desc.group(1) if desc else ''))

# 7. Devices
section('7. DEVICES (PixelConsumables.cs CreateDefault...: method | name | description)')
cons = read(os.path.join(SCRIPTS, 'PixelConsumables.cs'))
for dm in re.finditer(r'private\s+(?:static\s+)?Device\s+(CreateDefault\w+)\s*\(.*?\)\s*\{(.*?)\n    \}', cons, re.S):
    name, body = dm.group(1), dm.group(2)
    dn = re.search(r'displayName = "((?:[^"\\]|\\.)*)"', body)
    desc = re.search(r'description = "((?:[^"\\]|\\.)*)"', body)
    out.append('%s | %s | %s' % (name, dn.group(1) if dn else '(set at runtime)', desc.group(1) if desc else ''))

# 8. System index (docs/Index.md table)
section('8. SYSTEM INDEX (script / system | what it is | where the details are)')
index_md = read(os.path.join(ROOT, 'docs', 'Index.md'))
for line in index_md.splitlines():
    if line.startswith('| ') and not line.startswith('| Script') and not line.startswith('|---'):
        cells = [c.strip().replace('`', '') for c in line.strip('|').split('|')]
        if len(cells) >= 3:
            out.append(' | '.join(cells[:3]))

# 9. Script notes (header comments)
section('9. SCRIPT NOTES (the header comment of every script that has one)')
for f in script_files():
    src = read(f)
    hm = re.match(r'\s*// ---- System notes.*?\n(.*?)\n// -{5,}', src, re.S)
    if not hm:
        continue
    lines = [re.sub(r'^//\s?', '', l).rstrip() for l in hm.group(1).splitlines()]
    text = re.sub(r'\s+', ' ', ' '.join(l.strip() for l in lines)).strip()
    out.append('--- ' + os.path.relpath(f, SCRIPTS) + ' ---\n' + text)

# 10. System docs
section('10. SYSTEM DOCS (docs/systems/*.md)')
for f in sorted(glob.glob(os.path.join(ROOT, 'docs', 'systems', '*.md'))):
    out.append('--- docs/systems/' + os.path.basename(f) + ' ---\n' + plain(read(f)))

# 11. Ideas + open to-dos (the older roadmap notes are docs/systems/OlderNotes.md, in section 10)
ideas = os.path.join(ROOT, 'Assets', 'Resources', 'Ideas.txt')
if os.path.exists(ideas):
    section('11. IDEAS - MAYBE SOMEDAY (Assets/Resources/Ideas.txt)')
    out.append(plain(read(ideas)))
todo = os.path.join(ROOT, 'Assets', 'Resources', 'Todo.txt')
if os.path.exists(todo):
    section('12. OPEN TO-DO ITEMS (Assets/Resources/Todo.txt, unchecked only)')
    out.extend(l.strip() for l in read(todo).splitlines() if l.strip().startswith('[ ]'))

text = '\n'.join(out) + '\n'
with open(OUT, 'w', encoding='utf-8') as f:
    f.write(text)
print('wrote', OUT, len(text), 'chars,', text.count('\n'), 'lines')
