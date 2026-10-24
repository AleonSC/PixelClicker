#!/usr/bin/env python3
"""Builds the Husk Codex page (tools/husk-codex/husk_codex.html): every pixel type, what a click does, what its husk does,
how the husk falls (bounce / grip / gravity / special behaviour) and which husks, sprouts and machines it touches.
Everything is read from the code and the Resources texts, except the interactions (tools/husk-codex/interactions.txt, hand-written).
Run from anywhere: python3 tools/husk-codex/build.py   (then republish the page, see README.md)"""
import glob, json, os, re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
R = os.path.join(ROOT, 'Assets', 'Scripts') + os.sep
TEXTS = os.path.join(ROOT, 'Assets', 'Resources', 'PixelTexts.txt')
OUT = os.path.join(HERE, 'husk_codex.html')
src = lambda f: open(R + f, encoding='utf-8', errors='ignore').read()

# ================= 1. raw data from the scripts =================

def split_top(s):
    """split by commas at depth 0 (parens/braces/brackets), respecting strings"""
    out=[];d=0;cur='';i=0;ins=False
    while i<len(s):
        c=s[i]
        if ins:
            cur+=c
            if c=='\\': cur+=s[i+1]; i+=2; continue
            if c=='"': ins=False
        elif c=='"': ins=True; cur+=c
        elif c in '([{': d+=1; cur+=c
        elif c in ')]}': d-=1; cur+=c
        elif c==',' and d==0: out.append(cur); cur=''
        else: cur+=c
        i+=1
    if cur.strip(): out.append(cur)
    return [o.strip() for o in out]

def matching(s, start):
    """s[start]=='(' -> index of matching ')'"""
    d=0;ins=False;i=start
    while i<len(s):
        c=s[i]
        if ins:
            if c=='\\': i+=2; continue
            if c=='"': ins=False
        elif c=='"': ins=True
        elif c in '([{': d+=1
        elif c in ')]}':
            d-=1
            if d==0: return i
        i+=1

def cs_string(expr):
    # "a" + t.x + "b"  -> a{x}b
    parts=re.split(r'\s*\+\s*(?=(?:[^"]*"[^"]*")*[^"]*$)', expr.strip())
    out=''
    for p in parts:
        p=p.strip()
        if p.startswith('"') and p.endswith('"'): out+=p[1:-1].replace('\\"','"')
        else:
            m=re.match(r't\.(\w+)',p)
            out+='{'+(m.group(1) if m else p)+'}'
    return out

# ---- catalog ----
cat=src('PixelBlockCatalog.cs')
cards={}
for m in re.finditer(r'\bB\(PixelType\.', cat):
    st=m.start()+1; en=matching(cat,st)
    args=split_top(cat[st+1:en])
    t=args[0].split('.')[1]
    card={'type':t,'tags':[x.split('.')[1] for x in re.findall(r'PixelTag\.\w+',args[1]) if x!='PixelTag.None']}
    for a in args[2:]:
        k,v=a.split(':',1); k=k.strip(); v=v.strip()
        if k=='guide': card['guide']=cs_string(v.split('=>',1)[1])
        elif k=='oldPixel': card['husk']=cs_string(v)
        elif k in('soundAs','fxAs'): card[k]=v.split('.')[-1]
    cards[t]=card

# ---- tiers ----
def color(expr):
    expr=expr.strip()
    m=re.match(r'new Color\(([^)]*)\)',expr)
    if m:
        v=[float(x.strip().rstrip('f')) for x in m.group(1).split(',')]
        return v[:3]
    named={'white':[1,1,1],'black':[0,0,0],'red':[1,0,0],'green':[0,1,0],'blue':[0,0,1],'yellow':[1,0.92,0.016],'cyan':[0,1,1],'magenta':[1,0,1],'gray':[.5,.5,.5],'grey':[.5,.5,.5]}
    m=re.match(r'Color\.(\w+)',expr)
    if m: return named.get(m.group(1))
    return None
tiers={}
def num(v):
    try: return float(v.strip().rstrip('fdD'))
    except: return None
allsrc={f.split('/')[-1]:open(f).read() for f in glob.glob(R+'*.cs')}
for fn,s in allsrc.items():
    for m in re.finditer(r'\b(?:Block|Ore)\((?:PixelClicker\.)?PixelType\.', s):
        st=s.index('(',m.start()); en=matching(s,st)
        args=split_top(s[st+1:en])
        if len(args)<6 or not args[1].startswith('"'): continue
        t=args[0].split('.')[-1]
        tiers.setdefault(t,{}).update({'name':args[1].strip('"'),'color':color(args[2]),'hits':num(args[3]),'pay':num(args[4]),'weight':num(args[5]),'src':fn})
    for m in re.finditer(r'new (?:PixelClicker\.)?PixelTier\s*\{', s):
        st=s.index('{',m.start()); en=matching(s,st)
        body=s[st+1:en]
        tm=re.search(r'type\s*=\s*(?:PixelClicker\.)?PixelType\.(\w+)',body)
        if not tm: continue
        t=tm.group(1); d=tiers.setdefault(t,{})
        for a in split_top(body):
            if '=' not in a: continue
            k,v=a.split('=',1); k=k.strip(); v=v.strip()
            if k=='displayName' and 'name' not in d: d['name']=v.strip('"')
            elif k=='color' and 'color' not in d: d['color']=color(v)
            elif k=='amountPerClick' and 'pay' not in d: d['pay']=num(v)
            elif k=='clicksToCollect' and 'hits' not in d: d['hits']=num(v)
            elif k=='spawnWeight' and 'weight' not in d: d['weight']=num(v)
            elif k in('payoutMin','payoutMax','randomClicksMin','randomClicksMax'): d.setdefault(k,num(v))
            elif k=='flyAway' and v=='true': d['flyAway']=True
            elif k=='glow' and v=='true': d['glow']=True

# ---- looks ----
lk=src('PixelLooks.cs')
looks={}
FLAGS=['customPhysics','flutter','splash','shatter','floatAway','gravityWell','radiation','burn','sunFx','wobble','jagged','blob','shine','cometTail','forceTranslucent','vacuumContainer']
for m in re.finditer(r'new PixelLook\s*\{', lk):
    st=lk.index('{',m.start()); en=matching(lk,st); body=lk[st+1:en]
    tm=re.search(r'type\s*=\s*PixelClicker\.PixelType\.(\w+)',body)
    if not tm: continue
    d={}
    for a in split_top(body):
        if '=' not in a: continue
        k,v=a.split('=',1); k=k.strip(); v=v.strip()
        if k in FLAGS and v=='true': d[k]=True
        elif k in('bounce','friction','gravityMultiplier','emission'): d[k]=num(v)
        elif k=='color' : d['lookColor']=color(v)
        elif k=='useColor' and v=='true': d['useColor']=True
        elif k=='blockSurface': d['surface']=v.split('.')[-1]
    looks.setdefault(tm.group(1),d)

# Rare drops are made by loops / helpers in PixelClickerTiers.cs (EnsureDragonCubes, EnsureGodCubes) and PixelInfinity.cs, not by
# Block(...) lines, so their names / colours are copied here. Update them if those change.
for i in range(1,8): tiers.setdefault('DragonCube%d'%i,{}).update({'name':'Dragon Cube (%d)'%i,'color':[1,0.62,0.12],'pay':1})
for t,n,c in [('GodSlifer','Slifer the Sky Dragon',[0.92,0.12,0.1]),('GodRa','Ra the Winged Dragon',[1,0.82,0.15]),('GodObelisk','Obelisk the Tormentor',[0.2,0.38,0.95])]:
    tiers.setdefault(t,{}).update({'name':n,'color':c,'pay':1})
for t,n,c in zip(['StoneSpace','StoneMind','StoneReality','StonePower','StoneTime','StoneSoul'],["Space Cube","Mind Cube","Reality Cube","Power Cube","Time Cube","Soul Cube"],[[0.2,0.45,1],[1,0.85,0.15],[0.95,0.1,0.1],[0.68,0.22,0.95],[0.2,0.9,0.35],[1,0.55,0.1]]):
    tiers.setdefault(t,{}).update({'name':n,'color':c,'pay':1})

# ================= 2. one record per pixel type =================
# enum numbers straight from PixelClickerTypes.cs
_types = src('PixelClickerTypes.cs')
_body = _types[_types.index('enum PixelType'):]
_body = _body[_body.index('{') + 1:_body.index('}')]
enum = {m.group(1): int(m.group(2)) for m in re.finditer(r'(\w+)\s*=\s*(\d+)', _body)}
# Lead / Zinc / Nickel come from PixelShopDefaults.ProspectorOre (conditional expressions the parser can't read): copied here.
for t,c,h,p,w in [('Lead',[0.42,0.44,0.5],3,5,0.7),('Zinc',[0.7,0.74,0.76],3,6,0.65),('Nickel',[0.6,0.62,0.52],4,8,0.55)]:
    tiers[t]={'name':t+' Ore','color':c,'hits':h,'pay':p,'weight':w}
# The player-facing texts: Assets/Resources/PixelTexts.txt wins over the catalog's defaults (as in the game).
texts={}; cur=None
for line in open(TEXTS, encoding='utf-8'):
    line=line.rstrip('\n')
    if line.startswith('#'): continue
    m=re.match(r'\[(\w+)\]',line)
    if m: cur=texts.setdefault(m.group(1),{}); continue
    m=re.match(r'(name|guide|husk):\s?(.*)',line)
    if m and cur is not None and m.group(2).strip(): cur[m.group(1)]=m.group(2).strip().replace('\\n',' ')

# Which family a block is listed under: mostly its tags; these blocks have no skill tag (or the wrong one for the page), so they are placed by hand.
FAM_OVERRIDE={'Rock':'mining','Obsidian':'mining','GasPocket':'mining','Electric':'electric','Pest':'farming','Weevil':'farming','Seed':'farming','Water':'farming',
 'HornetNest':'wood','Pesticide':'wood','Acorn':'wood','Whetstone':'wood','GreatOak':'wood','WoodChips':'wood','Mold':'forage','Toadstool':'forage',
 'Jellyfish':'fish','Wildfire':'fire','Charcoal':'fire','Fire':'fire'}
def family(t,tags):
    if t in FAM_OVERRIDE: return FAM_OVERRIDE[t]
    if t in ('White','Gray','Black','Red','Green','Blue'): return 'core'
    if t.startswith('DragonCube') or t.startswith('God') or t.startswith('Stone') or t in('InfinityCubelette','GoldenSnitch'): return 'rare'
    for tag,f in [('Wood','wood'),('Forage','forage'),('Fish','fish'),('Farm','farming'),('Ore','mining'),('Electrical','electric'),('Firemaking','fire')]:
        if tag in tags: return f
    return 'special'

def fill(s,t,tier):
    if not s: return s
    rep={'clicks':tier.get('hits'),'clicksToCollect':tier.get('hits'),'clicksMin':tier.get('randomClicksMin'),'randomClicksMin':tier.get('randomClicksMin'),
         'clicksMax':tier.get('randomClicksMax'),'randomClicksMax':tier.get('randomClicksMax'),'payMin':tier.get('payoutMin'),'payoutMin':tier.get('payoutMin'),
         'payMax':tier.get('payoutMax'),'payoutMax':tier.get('payoutMax'),'pellets':6,'percent':35,'seconds':60}   # (Bait: the game's coded defaults, PixelGuideTexts.BaitNumbers)
    def r(m):
        v=rep.get(m.group(1)); 
        if v is None: return 'several'
        return str(int(v)) if float(v).is_integer() else str(v)
    return re.sub(r'\{(\w+)\}',r,s)

GENERAL='falls beside the cube and lies there for a while before it despawns.'
out=[]
for t,c in cards.items():
    tier=tiers.get(t,{}); lo=looks.get(t,{}); tx=texts.get(t,{})
    if t in('Bait',): pass
    name=tx.get('name') or tier.get('name') or t
    col=lo.get('lookColor') if lo.get('useColor') and lo.get('lookColor') and t not in('White','Gray','Black') else None
    col=tier.get('color') or col or [0.6,0.6,0.6]
    guide=fill(tx.get('guide') or c.get('guide') or '',t,tier)
    husk=fill(tx.get('husk') or c.get('husk') or '',t,tier)
    # physics: global defaults bounce 0.08 friction 0.65 gravity 1
    phys={'bounce':0.08,'friction':0.65,'gravity':1.0}
    if lo.get('customPhysics'):
        phys={'bounce':lo.get('bounce',0.85),'friction':lo.get('friction',0.05),'gravity':lo.get('gravityMultiplier',0.25)}
    beh=[k for k in ['flutter','splash','shatter','floatAway','gravityWell','radiation','burn','sunFx','wobble','jagged','blob','shine','cometTail'] if lo.get(k)]
    if tier.get('flyAway') or t=='Meteor': beh.append('flyAway')
    if tier.get('glow'): beh.append('glow')
    hits=tier.get('hits') or 1
    out.append({'id':t,'n':enum.get(t),'name':name,'family':family(t,c['tags']),'tags':c['tags'],'hazard':'Hazard' in c['tags'],
        'color':[round(x,3) for x in col],'pay':tier.get('pay'),'hits':int(hits),'rmin':tier.get('randomClicksMin'),'rmax':tier.get('randomClicksMax'),
        'pmin':tier.get('payoutMin'),'pmax':tier.get('payoutMax'),'weight':tier.get('weight'),
        'guide':guide,'husk':husk or GENERAL,'huskDefault':not husk,'phys':phys,'beh':beh,'surface':lo.get('surface'),
        'soundAs':c.get('soundAs'),'fxAs':c.get('fxAs')})
out.sort(key=lambda x:(x['n'] if x['n'] is not None else 999))
# rare weights
for o in out:
    if o['family']=='rare' and o['weight'] is None:
        o['weight']=0.0001 if o['id'].startswith('Dragon') else 0.000001


# ================= 3. interactions (hand-written file) =================
SYS = {}
LINKS = []
known = {o['id'] for o in out}
for n, line in enumerate(open(os.path.join(HERE, 'interactions.txt'), encoding='utf-8'), 1):
    line = line.strip()
    if not line or line.startswith('#'): continue
    if line.startswith('@'):
        k, v = line[1:].split('=', 1); SYS[k.strip()] = v.strip(); continue
    parts = [x.strip() for x in line.split('|', 3)]
    if len(parts) != 4: raise SystemExit(f'interactions.txt line {n}: needs  from | to | kind | text')
    a, b, kind, text = parts
    for x in (a, b):
        if x not in known and x not in SYS: raise SystemExit(f'interactions.txt line {n}: unknown pixel type or thing "{x}"')
    if kind not in ('boost','harm','feeds','creates','hold','link','reads','converts','consume'):
        raise SystemExit(f'interactions.txt line {n}: unknown kind "{kind}"')
    LINKS.append({'from': a, 'to': b, 'kind': kind, 'text': text})

# ================= 4. the page =================
data = json.dumps({'pixels': out, 'systems': SYS, 'links': LINKS}, separators=(',', ':')).replace('</', '<\\/')
page = open(os.path.join(HERE, 'page_template.html'), encoding='utf-8').read().replace('__DATA__', data)
open(OUT, 'w', encoding='utf-8').write(page)
print(f'wrote {OUT}: {len(out)} pixel types, {len(LINKS)} interactions')
