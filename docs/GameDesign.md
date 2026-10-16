# Pixel Clicker - game design document

Status: **living document, started 2026-10-16.** It describes the game as a whole: what it is, how the pieces fit together and where it is
going. Detail lives in the system docs it links to (`docs/systems/*.md`); the full technical reference is `docs/GameReference.txt`.

Everything here is written from what is **built** or **agreed in chat**. Where nothing has been decided, the section has a
**Blank spot** box: the question, what I'd suggest (**Suggestion**), and why it matters. The last section collects them all in one list.

Legend: **Built** = in the game now · **Agreed** = decided, not built · **Idea** = suggested, not agreed · **Blank** = undecided.

---

## 1. The pitch

A 3D incremental clicker about a cube. You click the cube; each click **extracts** a block's colour into your counter and drops its
drained **husk** to the floor. Husks are material: you sort, store, carry and process them with machines, train skills with them, and
slowly turn a single clicking cube into a small, busy world of skills, devices, visitors, minigames and impossibly rare collectibles.

**In one line:** *Cookie Clicker's numbers, RuneScape's skills, a dash of factory building, and a cube with a lot of personality.*

> **Blank spot - the hook in one sentence.** What does a stranger read on the store page? The game has many systems; it needs one
> sentence that says why it is different from other clickers. **Suggestion:** lead with the husks - *"Every click leaves something
> behind."* - because that is the idea no other clicker has.

## 2. Pillars

These are the rules every feature is measured against (they come from decisions already made in chat and in `PixelRules.md`):

1. **Every click leaves something behind.** A click gives the block's value AND a physical husk. Husks are material, never currency by
   themselves. (Built: `PixelRules.md`.)
2. **Each system answers a pressure.** Despawn, space, hazards, one skill at a time, the random rotation - every device, skill and
   consumable exists because it solves one of these (`ConnectionMap.md`, section 4).
3. **Skills feed each other.** A skill is simple on its own and gets better through other skills (the Whetstone from Mining for
   Woodcutting, Pesticide from Farming...). (Partly built, partly agreed.)
4. **Surprise inside structure.** Random when, designed what: you never know when a tree, a visitor or a rare drop comes, but what's
   inside is authored (`WoodcuttingProgression.md`).
5. **Played over days.** An idle game with numbers that keep climbing; sessions are short and frequent, and the game keeps working
   while you are away. (Agreed in the balance direction, `docs/roadmap.md`.)
6. **Charm everywhere.** Cubie, the vendors, the cats, the code-made sounds, the homages. The cube has a personality.

> **Blank spot - which pillar wins when two clash?** E.g. "played over days" (idle) vs "every click leaves something behind" (active
> handling of husks). **Suggestion:** active play is always *better*, idle play is always *possible* - machines catch up while away, but
> never beat a player who is there.

## 3. The player

> **Blank spot - who is this for?** Not written down anywhere. It changes how hard the early game is, how much text there is, and how
> long a session is. Candidates: idle-game fans (Cookie Clicker, Melvor Idle), RuneScape players, factory-game players taking a break.
> **Suggestion:** Melvor Idle / RuneScape players first - the skills, the rare drops and the Collection Log speak directly to them.

> **Blank spot - platform and price.** PC only (Steam / itch.io)? Mobile later? Free or paid? Ads or none? Nothing decided. It matters
> for controls (the game uses right click, keys, first person) and for how the economy is tuned. **Suggestion:** PC, paid, no ads,
> no microtransactions; mobile only if ever, and much later.

## 4. The world and the tone

- **Built:** a cube floating over a floor in a void, with sky styles, floors, day / night and filters. Everything is cubes: the
  vendors, the pets, the collectibles, the alien (agreed) - even the gear joke ("one slot, you're a cube").
- **Cubie**, the suspicious cube shopkeeper, is the guide and the voice of the game.
- **Extraction:** the in-world reason a click gives both value and a husk - the clicker pulls the block's essence out; the husk is what
  is left (`PixelRules.md`).
- **Homages:** a running thread of pop-culture nods, renamed so they are nods and not the real thing (Dragon Cubes, Egyptian God Cubes,
  Prime Cubes, the Golden Snitch; more on the to-do).

> **Blank spot - is there a story?** Who is Cubie, where is this void, why are we extracting pixels, what is the cube? Today there is
> flavour but no story. **Suggestion:** a light mystery told through Cubie and the visitors (what is the cube for? who is buying all this
> colour?), revealed bit by bit at milestones and finished by the end game (section 9). No cutscenes; lines in speech boxes.

> **Blank spot - the name.** "Pixel Clicker" is a working title? It is generic and hard to search for. **Suggestion:** decide before any
> public build; something with "husk", "cube" or "extract" in it would match the hook.

> **Blank spot - homage rules.** How close may a homage get? The Golden Snitch keeps a trademarked name. **Suggestion:** rename every
> homage before release (as with "Prime Cubes"); keep a list of real names in this doc so nothing slips through.

## 5. The core loop (Built)

```
   click the cube ──> currency of that block (its value)
        │
        └──> a husk drops to the floor (material)
                 │
                 ├─ despawns if nobody uses it (pressure)
                 ├─ machines / hands turn it into XP, charges, storage, a little currency (Recycler)
                 └─ skills: husk XP trains the skill it belongs to

   currency ──> shop (packs, devices, upgrades, minigames) ──> more and better blocks, faster clicking
```

- **The rotation:** the next block is a weighted random pick among unlocked types. **Block Hold** (right mouse) pulls a block out of
  the rotation and lets you stake it. Potions force one type for a while.
- **Tough blocks** take several hits; **hazards** pay nothing and must be cleared; **specials** are rare replacements.
- Detail: `docs/GameReference.txt` section 1, `PixelRules.md`, `PixelBlockRules.md`.

## 6. The progression arc

This is the shape of a full playthrough. Early and mid game mostly exist; late and end game are mostly blank.

### Early game - the first hour (Built, being tuned)
White -> Gray -> Black pixels by thresholds; Cubie's first visit after 50 White clicks (gift: a Recycler); the shop opens; the RGB
pack; the Auto Clicker; the Toggles window; Standard level 2 opens the Skills window; the first skill pack (Lumberjack's Pack).

### Mid game - skills and machines (Built, growing)
Gathering skills (Woodcutting, Foraging, Fishing, Mining, Farming, Electrician), each with filler, hazards, specials, perks and a
tree; devices to handle husks; minigames bought as packs; visitors and vendors; pets; crafting; the Pixel Bank.
**Agreed next:** Woodcutting's tree mode, first person as a mid-game unlock (the Cube Visor), the gear system (`GearDesign.md`).

### Late game (Blank)
> **Blank spot - what does the late game look like?** Mid-game systems are rich, but nothing says what a player does at hour 30.
> Candidates already on the table: the factory (belts, sorters, machines that break and need repairs - `PixelRules.md`), the alien
> vendor and relics in first person, face sockets, legendary gear. **Suggestion:** the late game is *building*: a factory on the floor
> that processes husks from several skills at once while you focus on one, with the alien's relics making first person the place you
> maintain it.

### End game and prestige (Blank)
> **Blank spot - is there an end, a prestige, or both?** Prestige is "wanted" (`docs/roadmap.md`) but undesigned: what resets, what is
> kept, what the reward is. And separately: is there an ending (a story finale, a final collectible)?
> **Suggestion:** both. A finale tied to the story (e.g. completing the Collection Log or forging the Cubelette reveals what the cube is
> for), then prestige as "start a new cube": currencies, unlocks and the floor reset; skills' levels, the Collection Log, pets and gear
> are kept; each prestige adds a permanent bonus and a new twist (a new skill, a harder rotation, a new vendor).

> **Blank spot - how long is a full playthrough?** One week? One month? Endless? It decides every number in the shop.
> **Suggestion:** the story finale around 2-4 weeks of normal play; prestige makes it endless.

## 7. Systems map (one paragraph each)

The full links between systems are in `ConnectionMap.md`; this is the short tour.

| System | Role in the game | State | Doc |
|---|---|---|---|
| Pixels / blocks | each type is its own currency; tags decide what a block is | Built | `PixelBlockCatalog.cs` header |
| Husks | the material every click leaves behind | Built | `PixelRules.md` |
| Skills | what spawns, hazards, perks, a tree; feed each other | Built (6 gathering + Standard + Offline) | `PixelSkills.md`, `SkillBlocks.md` |
| Devices / machines | turn husks into XP, storage, charges, currency (Recycler) | Built; factory machines are tester features | `PixelConsumables.md`, `PixelFactory.md` |
| Shop | packs, upgrades, Value levels, consumables, minigames | Built | `PixelShop.md` |
| Vendors / visitors | trades, buy skill currency, sell tools; Cubie guides | Built | `PixelVisitorMinigame.md` |
| Minigames | events that pay currency and raise the XP multiplier | Built (14+) | per-script headers |
| Collectibles | Dragon / God / Prime Cubes, the Golden Snitch, Collection Log, gallery | Built | `PixelGodCubes.md`, `PixelDragonWish.md` |
| Pets | one per block type, perks, outfits | Built | `PixelPets.md` |
| First person | walk the floor, handle husks, repair machines (agreed) | Built; unlock + relics Agreed | `PixelFirstPerson.md`, `GearDesign.md` |
| Gear | one slot, relics, legendary gear | Agreed | `GearDesign.md` |
| Offline progress | earnings while away | Built (auto clicker only) | `PixelOfflineProgress.cs` header |

> **Blank spot - which systems get cut?** `ConnectionMap.md` has a pruning test (a feature must pass 2 of: unique action, connected,
> answers a pressure). Nobody has run it over the whole game yet. Several minigames only pay currency, several shop devices have no
> skill home, Charcoal / Wood Chips / Magic Wood have no use. **Suggestion:** run the test once the tree rework and gear are designed,
> and merge or cut whatever fails.

## 8. Economy

- **Built:** each pixel type is its own currency; Pure pixels (the six basic types) are the shop currency; Value upgrades scale payouts;
  potions are priced from expected output; vendors buy skill currency for Pure pixels at a rarity-based rate; long number suffixes up to
  ~1e306. Rarer pixels are deliberately slower to upgrade.
- **Agreed direction:** idle game over days, numbers keep growing, prices re-laddered for the Value economy (`docs/roadmap.md`).
- Tools: the Dev Hub's Balance report (per-skill XP / minutes to level), `PixelBalanceReport.cs`.

> **Blank spot - what is the long-term sink?** Currencies pile up once the shop is bought out. **Suggestion:** prestige, the alien's
> face sockets and relics, and machine upkeep / repairs.

> **Blank spot - how should offline progress work?** Today only the auto clicker counts while away (`docs/roadmap.md`, "Known gaps").
> Should machines, minigames and skills run offline too? **Suggestion:** machines yes (at a reduced rate), minigames and hazards no.

## 9. Content plan

What exists and what is planned, by kind. Numbers are rough.

| Kind | Built | Agreed / on the to-do | Blank |
|---|---|---|---|
| Gathering skills | 6 (Woodcutting, Foraging, Fishing, Mining, Farming, Electrician) + Firemaking (dev-only) | Woodcutting trees | How many skills at release? Which are next? |
| Minigames | 14+ | Early Cube Dash | Should each feed a skill? (`ConnectionMap.md` gap 6) |
| Vendors / visitors | Cubie, Prospector, Lumberjack, Tinkerer, Wizard, Entrepreneur, Farmer, 2 cats | the alien | One vendor per skill? |
| Devices | ~20 | Pesticide Sprayer, factory machines as real items | Which tester machines become real? |
| Collectibles | 7 + 3 + 6 + Cubelette + Snitch | legendary gear | More families? |
| Homages | 5 families | ~9 ideas on the to-do | Which make the cut? |

> **Blank spot - what is in version 1.0?** There is far more content than one release needs, and lots is half-finished (tester
> machines, dev-only Firemaking, text-only perks). **Suggestion:** pick a 1.0 scope: the early game, 4-6 polished skills, the core
> devices, the collectibles, Cubie's story through to the finale; everything else is "after 1.0".

## 10. Onboarding and teaching

- **Built:** Cubie's visits, the shop tutorial (only from its "?" button now), the toggles tutorial, Standard level 2 opening Skills,
  first-time tips (`PixelHints`), Did You Know tips, the Game Guide (Controls, How To Play, Database).
- **Agreed:** meet one idea at a time (the to-do's first-hour notes); first person and gear arrive mid-game as reveals.

> **Blank spot - what does a player need to know in the first 5 minutes, and what can wait?** There is a lot to teach (husks, Block
> Hold, stakes, the Recycler, the shop). **Suggestion:** first 5 minutes = click, husks fall, Cubie arrives; everything else is taught
> the first time it matters, never up front.

## 11. Look and sound

- **Built:** cubes everywhere, per-type looks (`PixelLooks.md`), code-made sounds and synths, music player with tracks, sky / floor /
  filter / bar styles, the fancy pause menu.
- **Art direction (decided 2026-10-16): what the game already is** - smooth, clean menus with retro / 8-bit / cube visuals. There is
  no single fixed style on purpose: new looks follow the developer's feel for what fits, so match the existing look rather than a rulebook.
- **Music (decided 2026-10-16):** the soundtrack is generated with Suno by the developer.

## 12. Technical direction (short)

- Unity 6, everything built in code, one scene. Claude writes and maintains all code; the user tests in Unity (CLAUDE.md).
- Recent cleanups: `PixelCalm`, `PixelStatics`, `PixelPrefs`, `PixelFind.Cached`, the `PixelClicker` split, `PixelSystems` (systems in
  the scene for editing). Next ones and the save-version plan are on the to-do.

> **Blank spot - saves at release.** Save compatibility is ignored during development (CLAUDE.md). Before the first public build there
> must be one save version and upgrade steps (on the to-do). **Suggestion:** decide the date of the "first public build" so this lands
> before it.

---

## 13. All blank spots in one list

Grouped by how much they block other decisions. Answer them in any order; each answer gets written into its section above.

**Big direction (decide first - they shape everything else)**
1. Who is the game for? (section 3)
2. Platform and price. (section 3)
3. Late game: what does a player do at hour 30? (section 6)
4. End game: a finale, prestige, or both - and what prestige keeps. (section 6)
5. How long is a full playthrough? (section 6)
6. What is in version 1.0? (section 9)

**Identity**
7. The hook in one sentence. (section 1)
8. Is there a story, and who is Cubie really? (section 4)
9. The name. (section 4)
10. Which pillar wins when two clash? (section 2)

**Systems**
11. Which systems get cut or merged (run the pruning test). (section 7)
12. The long-term currency sink. (section 8)
13. What counts for offline progress. (section 8)
14. How many skills at release, and which are next. (section 9)
15. Should every minigame feed a skill? (section 9)
16. What to teach in the first 5 minutes. (section 10)

**Before any public build**
17. Homage rules / renaming. (section 4)
18. Save versioning and the first public build date. (section 12)

Feature-level blank spots (gear, first person, the alien, legendary gear) are in `GearDesign.md`; Woodcutting's in
`WoodcuttingProgression.md`; the husk rules' in `PixelRules.md`; the cross-system gaps in `ConnectionMap.md` section 8.
