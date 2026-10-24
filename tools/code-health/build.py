#!/usr/bin/env python3
"""Builds tools/code-health/CodeHealth.html: a health and optimization report for every script in Assets/Scripts.

Run it any time:  python3 tools/code-health/build.py      (add --no-history to leave history.json alone)
It reads the code only (no Unity): every finding is a text pattern with a file and line, so it can be wrong - treat it as a list of places
to look at, not proof. The checks and their weights are the CHECKS table below; areas come from tools/code-map/build.py (one GROUPS table).

Scores: each script starts at 100 and loses points per finding (capped per check). The overall score and the four category scores
(Performance, Size and structure, Project rules, Docs) are averages weighted by lines of code, so big scripts count more.
"""
import datetime, importlib.util, json, os, re, sys, collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SCRIPTS = os.path.join(ROOT, 'Assets', 'Scripts')
OUT = os.path.join(HERE, 'CodeHealth.html')
TEMPLATE = os.path.join(HERE, 'template.html')
HISTORY = os.path.join(HERE, 'history.json')

# The code map's helpers: comment / string stripping, the area table, the docs/Index.md descriptions.
spec = importlib.util.spec_from_file_location('codemap', os.path.join(ROOT, 'tools', 'code-map', 'build.py'))
codemap = importlib.util.module_from_spec(spec); spec.loader.exec_module(codemap)

HOT_METHODS = ('Update', 'LateUpdate', 'FixedUpdate', 'OnGUI')

# id: (category, title, why it matters, points per hit, max points, where: 'hot' = only inside Update / LateUpdate / FixedUpdate / OnGUI, 'file' = anywhere)
CHECKS = {
    'hot-find':    ('Performance', 'Scene search every frame', 'FindObjectOfType / PixelFind.First / GameObject.Find search the whole scene; in Update that is every frame. Use PixelFind.Cached or keep the reference.', 12, 36, 'hot', r'\bFind(?:First)?Objects?(?:OfType|ByType)\b|PixelFind\.First\b|PixelFind\.All\b|GameObject\.Find(?:WithTag|GameObjectsWithTag)?\b'),
    'hot-find-lazy': ('Performance', 'Scene search in Update while missing', '"if (x == null) x = PixelFind.First..." in Update: cheap once found, but searches every frame while the object does not exist. PixelFind.Cached is safer.', 2, 8, 'hot', None),
    'hot-getcomp': ('Performance', 'GetComponent every frame', 'GetComponent in Update costs a lookup each frame; fetch it once in Awake / Start.', 4, 16, 'hot', r'\bGetComponents?(?:InChildren|InParent)?\s*<'),
    'hot-alloc':   ('Performance', 'New collection every frame', 'new List / Dictionary / array or ToArray / ToList in Update makes garbage each frame (GC spikes). Reuse one field.', 5, 20, 'hot', r'\bnew\s+(?:List|Dictionary|HashSet|Queue|Stack)\s*<|\.ToArray\s*\(|\.ToList\s*\(|\bnew\s+\w+\s*\[\s*\w'),
    'hot-linq':    ('Performance', 'LINQ every frame', 'LINQ (Where / Select / Any / OrderBy...) allocates; in Update it runs every frame. Use a plain loop. (Only checked in files with using System.Linq.)', 5, 20, 'hot', r'(?<!Mathf)(?<!Math)(?<!PixelStats)\.(?:Where|Select|Any|OrderBy|OrderByDescending|FirstOrDefault|Sum|Distinct|GroupBy)\s*\('),
    'hot-camera':  ('Performance', 'Camera.main every frame', 'Camera.main searches by tag on older Unity; cache the camera.', 3, 9, 'hot', r'\bCamera\.main\b'),
    'hot-spawn':   ('Performance', 'Spawning every frame', 'Instantiate / new GameObject in Update: make sure it is not every frame (pool it).', 3, 12, 'hot', r'\bInstantiate\s*\(|\bnew\s+GameObject\s*\('),
    'hot-string':  ('Performance', 'Text built every frame', 'string.Format / concatenation / ToString in Update makes garbage; only rebuild text when the value changes.', 2, 10, 'hot', r'string\.Format\s*\(|\$"|"\s*\+|\+\s*"|\.ToString\s*\('),
    'big-file':    ('Size and structure', 'Very long file', 'Over 1,500 lines: hard to read and to change safely. Consider splitting by job (a partial file or a helper).', 0, 0, 'size', None),
    'long-method': ('Size and structure', 'Very long method', 'A method over 150 lines does several jobs; split it into named steps.', 6, 24, 'method', None),
    'fan-out':     ('Size and structure', 'Uses many systems', 'Uses 30+ other scripts: changes elsewhere break it easily. A sign it does too much.', 0, 0, 'coupling', None),
    'prefs':       ('Project rules', 'PlayerPrefs used directly', 'Settings go through PixelPrefs (CLAUDE.md), so per-save settings and resets work.', 8, 24, 'file', r'\bPlayerPrefs\.'),
    'debuglog':    ('Project rules', 'Debug.Log outside PixelDebug', 'Debug.Log runs in release builds too and allocates; use PixelDebug.Info (editor / dev builds only) or a warning that matters.', 1, 10, 'file', r'\bDebug\.Log\s*\('),
    'statics':     ('Project rules', 'Static state without a restart reset', 'Static fields outlive Restart (scene reload); clear them with a SubsystemRegistration ResetStatics / PixelStatics.ResetOnRestart.', 10, 10, 'statics', None),
    'todo':        ('Project rules', 'TODO / FIXME / HACK left in code', 'Open notes in code get forgotten; move them to Todo.txt or fix them.', 2, 10, 'raw', r'\b(?:TODO|FIXME|HACK|XXX)\b'),
    'no-header':   ('Docs', 'No header notes', 'Every script explains itself at the top: a "System notes" header, a /// summary on the class, or a docs/systems/<Name>.md page (CLAUDE.md).', 15, 15, 'docs', None),
    'duplicate':   ('Size and structure', 'Copied code', 'The same 8+ lines of code also sit in another place: a change has to be made in every copy, and they drift apart. Move it into one shared helper and call that.', 3, 12, 'dup', None),
    'restart-reset': ('Project rules', 'Restart reset not reviewed', 'PixelStatics.ResetOnRestart clears static state on Restart. Progress belongs in the GameState (a new game resets it by itself); a reset of unsaved run state says so on its line with \'// restart-reset-not-progress: <what>\'.', 4, 4, 'custom', None),
    'one-place':   ('Project rules', 'Bypasses its one helper', 'A version-split API used outside the one script that hides it (physics materials: PixelPhysicsMaterials, scene searches: PixelFind, object ids: PixelObjectId). The split then lives in two places, and a Unity upgrade breaks the copy.', 4, 12, 'custom', None),
    'unused-field': ('Project rules', 'Unused field', 'A field nothing reads (only declared, or only ever assigned): dead settings confuse the Inspector and whoever reads the code. Remove it, or use it.', 3, 9, 'custom', None),
    'no-tooltip':  ('Project rules', 'Inspector field without a tooltip', 'Every Inspector field explains itself with a [Tooltip] (CLAUDE.md: every variable editable and explained).', 1, 5, 'custom', None),
    'complex-method': ('Size and structure', 'Very branchy method', 'Over 40 branches (if / case / loops / && / ||) in one method: hard to follow even when short. Split the decisions into named steps. (A plain switch table, mostly case labels, is not counted.)', 3, 9, 'custom', None),
    'doc-drift':   ('Docs', 'Docs name missing code', 'The script\'s docs (its docs/systems page or Index line) name a field / method / type in `backticks` that no longer exists anywhere in the code: notes that point at renamed or removed code send the next reader the wrong way. Rename or remove the stale name.', 2, 10, 'custom', None),
    'no-index':    ('Docs', 'Not in docs/Index.md', 'Every script / system gets one line in docs/Index.md so it can be found.', 8, 8, 'docs', None),
}
CATEGORIES = ['Performance', 'Size and structure', 'Project rules', 'Docs']
CAT_CAP = {'Performance': 45, 'Size and structure': 30, 'Project rules': 25, 'Docs': 20}
# 'one-place': the pattern and the one script allowed to use it.
ONE_PLACE = [
    (re.compile(r'new\s+Physic(?:s)?Material\s*\('), 'PixelPhysicsMaterials.cs', 'physics material made by hand'),
    (re.compile(r'\b(?:FindObjectOfType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsOfType|FindObjectsByType)\s*[<(]'), 'PixelFind.cs', 'Unity scene search called directly'),
    (re.compile(r'\b(?:GetInstanceID|GetEntityId)\s*\('), 'PixelObjectId.cs', 'object id read directly'),
]
COMPLEX_BRANCHES = 40   # branch points in one method before 'complex-method'
CHURN_DAYS = 30   # 'hot spots' list: how often a script changed in this many days
DUP_WINDOW = 8   # lines of real code (not braces / blank) that must match for a 'duplicate' finding
EXEMPT = {'prefs': {'PixelPrefs.cs'}, 'debuglog': {'PixelDebug.cs', 'PixelCrashLog.cs', 'PixelDevConsole.cs'},
          'fan-out': {'PixelSystems.cs'}}   # (PixelSystems IS the list of every system: naming them all is its job)

SIG = re.compile(r'^(\s{4}|\t)(?:(?:public|private|protected|internal|static|override|virtual|abstract|sealed|async|new|unsafe|extern)\s+)*'
                 r'(?:[\w<>\[\],.?]+\s+)?(\w+)\s*\([^;{]*\)\s*(?:where[^{]*)?(\{)?\s*$')

def methods(code_lines):
    """(name, first line index, last line index) of each top-level class member method (brace matched; approximate)."""
    out = []
    i = 0
    n = len(code_lines)
    while i < n:
        m = SIG.match(code_lines[i])
        if m and m.group(2) not in ('if', 'for', 'foreach', 'while', 'switch', 'using', 'lock', 'catch', 'return', 'new'):
            j = i if m.group(3) else i + 1
            if j < n and '{' in code_lines[j] and (m.group(3) or code_lines[j].strip().startswith('{')):
                depth = 0; k = j; started = False
                while k < n:
                    depth += code_lines[k].count('{') - code_lines[k].count('}')
                    if '{' in code_lines[k]: started = True
                    if started and depth <= 0: break
                    k += 1
                out.append((m.group(2), i, k))
                i = k + 1
                continue
        i += 1
    return out

STATIC_FIELD = re.compile(r'^\s+(?:(?:public|private|protected|internal)\s+)?static\s+(?!readonly|class|void|event\b|bool\s+\w+\s*\(|extern)(?:[\w<>\[\],.?]+\s+)+(\w+)\s*(?:=[^;]*)?;')

def analyse(f, src, code, node_desc, in_index):
    lines = src.split('\n')
    code_lines = code.split('\n')   # (same line count: strip_code keeps newlines)
    findings = []
    def add(check, line, text):
        findings.append({'check': check, 'line': line, 'text': text.strip()[:140]})
    meths = methods(code_lines)
    hot_ranges = [(a, b) for (name, a, b) in meths if name in HOT_METHODS]
    def in_hot(i): return any(a <= i <= b for a, b in hot_ranges)
    def indent(l): return len(l) - len(l.lstrip())
    def guarded(i):
        # One-time setup inside Update: the line, or an 'if (... == null ...)' block it sits in, only runs while something is missing.
        if '== null' in code_lines[i] or 'is null' in code_lines[i]: return True
        me = indent(code_lines[i])
        for j in range(i - 1, max(-1, i - 25), -1):
            l = code_lines[j]
            if not l.strip(): continue
            if indent(l) < me:
                if re.search(r'\bif\s*\(.*(?:==\s*null|is null|!\w+\.activeSelf|!built|!ready|\bbuilt\b\s*==\s*false)', l): return True
                if l.strip() == '{' and j > 0 and re.search(r'\bif\s*\(.*==\s*null', code_lines[j - 1]): return True
                me = indent(l)
                if re.match(r'\s*(?:(?:public|private|protected|internal|static|override|virtual)\s+)*[\w<>\[\]]+\s+\w+\s*\(', l): return False
        return False
    for cid, (cat, title, why, per, cap, where, rx) in CHECKS.items():
        if rx is None or f in EXEMPT.get(cid, ()): continue
        if cid == 'hot-linq' and 'using System.Linq' not in src: continue
        pat = re.compile(rx)
        source = lines if where == 'raw' else code_lines
        for i, l in enumerate(source):
            if where == 'hot' and not in_hot(i): continue
            if pat.search(l):
                if where == 'hot' and guarded(i):
                    if cid == 'hot-find': add('hot-find-lazy', i + 1, lines[i])
                    continue   # (one-time setup: not a per-frame cost)
                if cid == 'hot-find' and re.search(r'==\s*null\)?\s*\w+\s*=\s*PixelFind\.First', l): add('hot-find-lazy', i + 1, lines[i])   # (a lazy fallback)
                else: add(cid, i + 1, lines[i])
    # Restart resets: each needs the not-progress marker (progress lives in the GameState).
    if f != 'PixelStatics.cs':
        for i, l in enumerate(code_lines):
            if 'PixelStatics.ResetOnRestart(' in l and 'restart-reset-not-progress:' not in lines[i]:
                add('restart-reset', i + 1, lines[i])
    # Version-split APIs outside their one helper.
    for rx, home, what in ONE_PLACE:
        if f == home: continue
        for i, l in enumerate(code_lines):
            if rx.search(l): add('one-place', i + 1, what + ' (use ' + home[:-3] + '): ' + lines[i].strip())
    n = len(lines)
    if n > 1500: add('big-file', 1, f'{n:,} lines')
    for name, a, b in meths:
        if b - a + 1 > 150: add('long-method', a + 1, f'{name}(): {b - a + 1} lines')
    has_reset = 'RuntimeInitializeOnLoadMethod' in code or 'ResetOnRestart' in code
    if not has_reset:
        for i, l in enumerate(code_lines):
            m = STATIC_FIELD.match(l)
            if m and ' const ' not in l and '=>' not in l and '(' not in l.split('=')[0]:
                name = m.group(1)
                if re.search(r'\b' + name + r'\s*=\s*null\b', code) or re.search(r'(?i)(buffer|cache|scratch|pool|tmp|temp)', name): continue   # (cleared in OnDestroy, or a reused buffer)
                if re.search(r'static\s+(?:readonly\s+)?(?:Mesh|Sprite|Material|Texture2D|Texture|AnimationCurve|Shader|Font|TMP_FontAsset|Gradient)\b', l): continue   # (a shared asset made once: fine to keep)
                add('statics', i + 1, lines[i]); break
    base = f[:-3]
    head = '\n'.join(lines[:40])
    has_notes = 'System notes' in head or re.search(r'^\s*///\s*<summary>', head, re.M) or lines[0].lstrip().startswith('//') \
                or os.path.exists(os.path.join(ROOT, 'docs', 'systems', base + '.md')) or os.path.exists(os.path.join(ROOT, 'docs', 'systems', base.replace('Pixel', '', 1) + '.md'))
    if not has_notes: add('no-header', 1, 'no System notes header, /// summary or docs/systems page')
    if not in_index: add('no-index', 1, 'no line in docs/Index.md')
    return findings, meths, n

def find_duplicates(files, code, src_by_file):
    """Blocks of DUP_WINDOW real code lines (whitespace ignored, braces / blank / one-word lines skipped) that appear in two or more places.
    Returns {file: [finding, ...]}: one finding per copied run (the first line of the run), naming where the other copy is."""
    trivial = re.compile(r'^(?:[{}();,\s]*|else|break;|return;|continue;|default:|try|finally|base\.\w+\(\);)$')
    # Declarations are not 'code that does something': fields, one-line properties / overrides (=>), usings, attributes, signatures.
    decl = re.compile(r'^(?:using\s|\[|(?:(?:public|private|protected|internal|static|readonly|override|virtual|const|abstract|sealed|new)\s+)+[\w<>\[\],.?]+\s+\w+\s*(?:[;=]|=>|\(|\{\s*get))')
    comment = re.compile(r'//(?=(?:[^"]*"[^"]*")*[^"]*$).*')   # (a // comment that is not inside a string)
    seq = {}
    for f in files:
        rows = []
        raw = src_by_file[f].split('\n')
        stripped = code[f].split('\n')
        for i, l in enumerate(raw):
            if i < len(stripped) and not stripped[i].strip(): continue   # (inside a block comment)
            t = re.sub(r'\s+', ' ', comment.sub('', l)).strip()   # (string literals KEPT: two lists that differ only in their text are not copies)
            if len(t) < 12 or trivial.match(t) or decl.match(t): continue
            rows.append((i + 1, t))
        seq[f] = rows
    where = collections.defaultdict(list)
    for f, rows in seq.items():
        for k in range(len(rows) - DUP_WINDOW + 1):
            key = hash('\n'.join(t for _, t in rows[k:k + DUP_WINDOW]))
            where[key].append((f, k))
    hit = collections.defaultdict(dict)   # file -> {window index: (other file, other line)}
    for key, places in where.items():
        if len(places) < 2: continue
        for f, k in places:
            other = next(((g, j) for g, j in places if (g, j) != (f, k) and not (g == f and abs(j - k) < DUP_WINDOW)), None)
            if other: hit[f][k] = other
    out = {}
    for f, ks in hit.items():
        rows = seq[f]
        last = -10
        for k in sorted(ks):
            if k - last > 1:   # (the start of a new copied run)
                g, j = ks[k]
                out.setdefault(f, []).append({'check': 'duplicate', 'line': rows[k][0], 'text': f'{DUP_WINDOW}+ lines also in {g}:{seq[g][j][0]}'})
            last = k
    return out

def score(findings, lines, fan_out):
    by = collections.Counter(x['check'] for x in findings)
    lost = collections.Counter()
    for cid, cnt in by.items():
        cat, title, why, per, cap, where, rx = CHECKS[cid]
        if cid == 'big-file': pts = min(30, (lines - 1500) / 100)   # 1 point per 100 lines over 1,500
        else: pts = min(cap, per * cnt)
        lost[cat] += pts
    if fan_out >= 30: lost['Size and structure'] += min(15, (fan_out - 25) * 0.6)
    cats = {c: max(0.0, 100.0 * (1 - min(lost[c], CAT_CAP[c]) / CAT_CAP[c])) for c in CATEGORIES}
    total = max(0.0, 100.0 - sum(min(lost[c], CAT_CAP[c]) for c in CATEGORIES))
    return round(total, 1), {c: round(v, 1) for c, v in cats.items()}

BRANCH = re.compile(r'\b(?:if|for|foreach|while|case|catch)\b|&&|\|\||\?(?![?.\[])')
FIELD_DECL = re.compile(r'^\s*(?:\[[^\]]*\]\s*)*(?:private|protected)\s+(?:static\s+)?(?:readonly\s+)?(?:volatile\s+)?[\w<>\[\],.?]+\s+(\w+)\s*(?:=[^=>]|;)')
DOC_IDENT = re.compile(r'`([A-Za-z_][\w.]*?)(?:\(\))?`')

def complex_methods(code_lines, meths):
    """Methods with more than COMPLEX_BRANCHES branch points; a switch table (mostly case labels) is skipped."""
    out = []
    for name, a, b in meths:
        body = '\n'.join(code_lines[a:b + 1])
        hits = BRANCH.findall(body)
        n = len(hits)
        if n <= COMPLEX_BRANCHES: continue
        cases = sum(1 for h in hits if h == 'case')
        if cases > n * 0.5: continue   # (a lookup table written as a switch: long, not hard)
        out.append((a + 1, f'{name}(): {n} branches'))
    return out

def unused_fields(code_lines, word_count, assign_count):
    """Fields whose name appears nowhere but its own declaration / assignments in the whole codebase (Editor scripts included)."""
    out = []
    for i, l in enumerate(code_lines):
        m = FIELD_DECL.match(l)
        if not m or ' const ' in l or '=>' in l: continue
        name = m.group(1)
        if len(name) < 3: continue
        total, assigns = word_count.get(name, 0), assign_count.get(name, 0)
        if total <= 1 or total == assigns: out.append((i + 1, f'{name} is never read'))
    return out

def missing_tooltips(lines):
    """[SerializeField] private / protected fields (not hidden) with no [Tooltip] on them or the attribute lines just above."""
    out = []
    for i, l in enumerate(lines):
        if '[SerializeField' not in l or 'HideInInspector' in l or not re.search(r'\b(?:private|protected)\b', l): continue
        ok, k = 'Tooltip' in l, i - 1
        # walk up over the attribute lines (also a long tooltip wrapped over several lines) until the previous statement / blank line
        while not ok and k >= 0 and lines[k].strip() and not re.search(r'[;{}]\s*(?://.*)?$', lines[k]):
            ok = 'Tooltip' in lines[k]
            k -= 1
        if not ok: out.append((i + 1, l.strip()[:120]))
    return out

def doc_drift(base, idx_text, raw_all):
    """Names in backticks in the script's docs/systems page and its docs/Index.md lines that no code (strings included) contains."""
    texts = []
    for name in (base, base.replace('Pixel', '', 1)):
        p = os.path.join(ROOT, 'docs', 'systems', name + '.md')
        if os.path.exists(p): texts.append(open(p, encoding='utf-8').read()); break
    texts += [l for l in idx_text if l.startswith('| `' + base)]
    missing = []
    for t in texts:
        for ident in DOC_IDENT.findall(t):
            if '.' in ident and ident.rsplit('.', 1)[-1] in ('cs', 'md', 'txt', 'json', 'html', 'py', 'sh'): continue
            if '_' in ident and ident.islower(): continue   # (sound ids, pref keys)
            parts = [p for p in ident.split('.') if len(p) >= 4]
            if not parts or ident in missing: continue
            if any(not re.search(r'\b' + re.escape(p) + r'\b', raw_all) for p in parts): missing.append(ident)
    return missing

def churn(days):
    """{script file: commits touching it in the last 'days' days} from git (empty if git is not there)."""
    import subprocess
    try:
        out = subprocess.run(['git', '-C', ROOT, 'log', f'--since={days} days ago', '--name-only', '--pretty=format:'],
                             capture_output=True, text=True, timeout=60).stdout
    except Exception:
        return {}
    c = collections.Counter(os.path.basename(l) for l in out.split('\n') if l.startswith('Assets/Scripts/') and l.endswith('.cs'))
    return dict(c)

def main():
    files = sorted(x for x in os.listdir(SCRIPTS) if x.endswith('.cs'))
    idx = codemap.index_lines()
    src, code = {}, {}
    for f in files:
        s = open(os.path.join(SCRIPTS, f), encoding='utf-8', errors='replace').read()
        src[f] = s
        c = codemap.strip_code(s)
        # strip_code may drop newlines inside block comments: rebuild so line numbers match the source
        if c.count('\n') != s.count('\n'):
            c = re.sub(r'/\*.*?\*/', lambda m: '\n' * m.group(0).count('\n'), s, flags=re.S)
            c = re.sub(r'@"(?:[^"]|"")*"', lambda m: '""' + '\n' * m.group(0).count('\n'), c)
            c = re.sub(r'"(?:\\.|[^"\\\n])*"', '""', c)
            c = re.sub(r'//[^\n]*', ' ', c)
        code[f] = c
    # fan-out per file: other scripts' types it names (same idea as the code map)
    types = {}
    for f in files:
        for t in codemap.DECL.findall(code[f]): types.setdefault(t, f)
    word = re.compile(r'\b([A-Z]\w+)\b')
    fan_out = {}
    for f in files:
        fan_out[f] = len({types[m.group(1)] for m in word.finditer(code[f]) if m.group(1) in types and types[m.group(1)] != f})
    dups = find_duplicates(files, code, src)
    # Codebase-wide word counts for 'unused-field' (Editor scripts too: they read fields as well)
    editor_dir = os.path.join(SCRIPTS, 'Editor')
    extra_code = [codemap.strip_code(open(os.path.join(editor_dir, x), encoding='utf-8', errors='replace').read())
                  for x in (os.listdir(editor_dir) if os.path.isdir(editor_dir) else []) if x.endswith('.cs')]
    all_code = '\n'.join(list(code.values()) + extra_code)
    word_count = collections.Counter(re.findall(r'\b[A-Za-z_]\w*\b', all_code))
    assign_count = collections.Counter(re.findall(r'\b([A-Za-z_]\w*)\s*=(?![=>])', all_code))
    raw_all = '\n'.join(src.values())
    idx_text = open(os.path.join(ROOT, 'docs', 'Index.md'), encoding='utf-8').read().split('\n')
    changes = churn(CHURN_DAYS)
    scripts = []
    for f in files:
        base = f[:-3]
        in_index = base in idx or any(base in k for k in idx) or re.search(r'\b' + re.escape(base) + r'\b', open(os.path.join(ROOT, 'docs', 'Index.md'), encoding='utf-8').read()) is not None
        findings, meths, n = analyse(f, src[f], code[f], '', in_index)
        fan = 0 if f in EXEMPT['fan-out'] else fan_out[f]
        if fan >= 30: findings.append({'check': 'fan-out', 'line': 1, 'text': f'uses {fan} other scripts'})
        findings.extend(dups.get(f, []))
        code_lines = code[f].split('\n')
        for line, text in complex_methods(code_lines, meths): findings.append({'check': 'complex-method', 'line': line, 'text': text})
        for line, text in unused_fields(code_lines, word_count, assign_count): findings.append({'check': 'unused-field', 'line': line, 'text': text})
        for line, text in missing_tooltips(src[f].split('\n')): findings.append({'check': 'no-tooltip', 'line': line, 'text': text})
        for ident in doc_drift(base, idx_text, raw_all): findings.append({'check': 'doc-drift', 'line': 1, 'text': f'`{ident}` is named in its docs but not found in the code'})
        total, cats = score(findings, n, fan)
        short = base[5:] if base.startswith('Pixel') else base
        group = next((g for g, rx in codemap.GROUPS if re.search(rx, short)), 'UI')
        desc = idx.get(base, ('', ''))[0] or codemap.header_summary(src[f])
        hot = [name for name, a, b in meths if name in HOT_METHODS]
        scripts.append({'file': f, 'group': group, 'lines': n, 'score': total, 'cats': cats, 'findings': findings,
                        'methods': len(meths), 'longest': max((b - a + 1 for _, a, b in meths), default=0), 'hot': hot,
                        'fanOut': fan_out[f], 'churn': changes.get(f, 0), 'desc': re.sub(r'\s+', ' ', desc)[:300]})
    total_lines = sum(s['lines'] for s in scripts)
    overall = round(sum(s['score'] * s['lines'] for s in scripts) / total_lines, 1)
    cat_scores = {c: round(sum(s['cats'][c] * s['lines'] for s in scripts) / total_lines, 1) for c in CATEGORIES}
    counts = collections.Counter(x['check'] for s in scripts for x in s['findings'])
    per_frame = sum(1 for s in scripts if s['hot'])
    today = datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%d %H:%M')
    history = []
    if os.path.exists(HISTORY):
        try: history = json.load(open(HISTORY, encoding='utf-8'))
        except Exception: history = []
    entry = {'date': today, 'score': overall, 'cats': cat_scores, 'scripts': len(scripts), 'lines': total_lines}
    if '--no-history' not in sys.argv and (not history or history[-1]['score'] != overall or history[-1]['lines'] != total_lines):
        history.append(entry)
        json.dump(history, open(HISTORY, 'w', encoding='utf-8'), indent=1)
    data = {
        'built': today, 'overall': overall, 'cats': cat_scores, 'categories': CATEGORIES, 'scripts': scripts,
        'checks': {k: {'cat': v[0], 'title': v[1], 'why': v[2]} for k, v in CHECKS.items()},
        'counts': counts, 'colors': codemap.COLORS, 'history': history or [entry],
        'totals': {'scripts': len(scripts), 'lines': total_lines, 'perFrame': per_frame}, 'churnDays': CHURN_DAYS,
    }
    html = open(TEMPLATE, encoding='utf-8').read().replace('/*__DATA__*/null', json.dumps(data, separators=(',', ':')))
    open(OUT, 'w', encoding='utf-8').write(html)
    print(f'overall {overall}  ' + '  '.join(f'{c}: {v}' for c, v in cat_scores.items()))
    print('findings:', dict(counts.most_common()))
    print('->', OUT)

if __name__ == '__main__':
    main()
