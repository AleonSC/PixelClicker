# Pixel Clicker - game design document

> **Every click leaves something behind.**

Living document (started 2026-10-16, reorganised the same day). It describes the game as a whole: what it is, how the pieces fit and
where it is going. Detail lives in the system docs (`docs/systems/*.md`); the full technical reference is `docs/GameReference.txt`.

**How to use it:** the open questions are first, each with a short suggestion. When one is answered, it moves to "Decided at a glance"
and into its section. Sections point back to their questions as **(Q1)**, **(Q2)**...

Legend: **Built** = in the game now · **Agreed** = decided, not built · **Idea** = suggested, not agreed.

---

## Open questions

**Big direction**

| # | Question | Suggestion |
|---|---|---|
| Q1 | Which cosmetics, locked behind which goals? | One per big milestone: a skill cape for the cube at max level, a full Collection Log page, each prestige, rare achievements. A Wardrobe tab in Pocket. |
| Q2 | Prestige: what resets and what is kept? | Resets currencies, shop unlocks, devices and the floor. Keeps skill levels, the Collection Log, pets, gear and cosmetics. Each payment gives a permanent "Cubie's Goodwill" bonus and unlocks something new. |
| Q3 | Prestige: how does Cubie's bill grow? | A ledger: interest piles up over play time and Cubie inflates it every time he mentions it. Paying later earns more goodwill. |
| Q4 | Is there an ending at all? | No hard ending. The long tail is the end game: max every skill, finish the log, collect cosmetics, prestige again. |
| Q5 | Exactly what is in 1.0? | The six gathering skills that exist, polished (every perk working), the core devices, the collectibles, Cubie's prestige. Tester machines and dev-only Firemaking stay out. Write a 1.0 checklist. |

**Identity**

| # | Question | Suggestion |
|---|---|---|
| Q6 | The game's name ("Pixel Clicker" is a working title?) | Decide before any public build; something with "husk", "cube" or "extract" fits the hook. |
| Q7 | Which pillar wins when two clash (idle vs active)? | Active play is always *better*, idle play is always *possible*: machines catch up while you're away but never beat a player who is there. |

**Systems**

| # | Question | Suggestion |
|---|---|---|
| Q8 | Which systems get cut or merged? | Run the pruning test (`ConnectionMap.md`) once the tree rework and gear are designed. |
| Q9 | The long-term currency sink? | Prestige, the alien's face sockets and relics, machine repairs. |
| Q10 | What runs during offline progress? | Machines yes (at a reduced rate); minigames and hazards no. Today only the auto clicker counts. |
| Q11 | How many skills at release, which come next? | Answered together with Q5. |
| Q12 | Should every minigame feed a skill? | Yes, the way the Evil Tree feeds Woodcutting (e.g. Bomb Defusal -> Mining, Snake -> Foraging). |
| Q13 | What does a player learn in the first 5 minutes? | Click, husks fall, Cubie arrives. Everything else is taught the first time it matters. |

**Before any public build**

| # | Question | Suggestion |
|---|---|---|
| Q14 | Homage renaming (the Golden Snitch is a trademarked name) | Rename every homage before release, as with the Prime Cubes; keep a list of the real names here so none slips through. |
| Q15 | Save versioning and the first public build date | Pick the date, so one save version and its upgrade steps (on the to-do) land before it. |

Feature-level questions: gear, first person, the alien and legendary gear in `GearDesign.md`; combat in `CombatDesign.md`; Woodcutting in
`WoodcuttingProgression.md`; husk rules in `PixelRules.md`; cross-system gaps in `ConnectionMap.md` section 8.

---

## Decided at a glance

All decided 2026-10-16 unless noted.

- **Hook:** *"Every click leaves something behind."*
- **Audience:** people who love numbers going up and collecting things; RuneScape-type players first, idle / clicker fans too.
- **Platform and price:** Steam, about $10, one-time purchase.
- **Concept:** no story. The game is about our real world - monotonous grinding for money - with a deliberately **simulated** feel:
  everything is nothing in itself, and only people give it meaning. Cubie is just another guy in the world.
- **Many ways to play:** manual clicking, auto clicking, machines, consumables and minigames are all real ways to progress.
- **Late game:** explore every option you have and grind skills up; cosmetics as rewards are being considered (Q1).
- **Prestige:** paying Cubie back for his small gift and the Recycler - a joke, not a theme. Interest has piled up and he keeps
  inflating the bill.
- **Length:** open-ended, no set playthrough time.
- **1.0:** flesh out the systems, ship a handful of skills, add more skills as updates (Q5).
- **Art direction:** what the game already is - smooth, clean menus with retro / 8-bit / cube visuals; no fixed rulebook.
- **Music:** the developer's own Suno tracks (paid plan, no attribution needed).

---

## 1. The pitch

A 3D incremental clicker about a cube. You click the cube; each click **extracts** a block's colour into your counter and drops its
drained **husk** to the floor. Husks are material: you sort, store, carry and process them with machines, train skills with them, and
slowly turn a single clicking cube into a small, busy world of skills, devices, visitors, minigames and impossibly rare collectibles.

**In one line:** *Cookie Clicker's numbers, RuneScape's skills, a dash of factory building, and a cube with a lot of personality.*

**Store hook:** *"Every click leaves something behind."* - the husks are the idea no other clicker has.

## 2. Pillars

Every feature is measured against these:

1. **Every click leaves something behind.** A click gives the block's value AND a physical husk. Husks are material, never currency by
   themselves. (Built: `PixelRules.md`.)
2. **Each system answers a pressure.** Despawn, space, hazards, one skill at a time, the random rotation: every device, skill and
   consumable exists because it solves one of these (`ConnectionMap.md`, section 4).
3. **Skills feed each other.** A skill is simple on its own and gets better through other skills (the Whetstone from Mining for
   Woodcutting, Pesticide from Farming...). (Partly built, partly agreed.)
4. **Surprise inside structure.** Random *when*, designed *what*: you never know when a tree, a visitor or a rare drop comes, but
   what's inside is authored (`WoodcuttingProgression.md`).
5. **Played over days.** Numbers keep climbing; sessions are short and frequent, and the game keeps working while you're away.
6. **Many ways to play.** Manual, auto, machines, consumables, minigames: lean on whichever you enjoy. No approach is the only good one.
7. **Charm everywhere.** The vendors, the cats, the code-made sounds, the homages. The cube has a personality.

When two pillars pull against each other, see **(Q7)**.

## 3. The player

- **Who:** people who love numbers going up and collecting things. **RuneScape-type players** first (skills to grind, rare drops, a
  Collection Log), with appeal to idle / clicker fans because the game supports several ways of playing.
- **Where and how much:** Steam, about $10, one-time purchase. No ads, no microtransactions.
- **What this means for design:** skills and collections are the heart of the game; long-term goals (max levels, full logs, cosmetics)
  are the motivation, not a plot; the game must respect players who idle and players who click.

## 4. Concept, world and tone

- **The concept:** our real world - monotonous grinding for money. The game feels **simulated** on purpose: the pixels, the currency
  and the numbers mean nothing in themselves; only the player gives them meaning.
- **No story.** No ultimate plot, no hidden mystery, no big reveal.
- **Cubie is just another guy in the world**, like the other vendors: a suspicious cube shopkeeper with his own small concerns (mostly
  getting his money back, with interest).
- **Writing:** characters are ordinary people, not keys to a plot. The humour comes from how mundane and transactional everything is.
- **Extraction:** the in-world reason a click gives both value and a husk - the clicker pulls the block's essence out; the husk is
  what's left (`PixelRules.md`). It fits the concept: the value is in the counter, the thing itself is just leftovers.
- **The world (Built):** a cube floating over a floor in a void, with sky styles, floors, day / night and filters. Everything is
  cubes: the vendors, the pets, the collectibles, the alien (Agreed) - even the gear joke ("one slot, you're a cube").
- **Homages:** a running thread of pop-culture nods (Dragon Cubes, Egyptian God Cubes, Prime Cubes, the Golden Snitch; more ideas on
  the to-do). Renaming before release: **(Q14)**.
- **The name:** **(Q6)**.

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

### Early game - the first hour (Built, being tuned)
White -> Gray -> Black pixels by thresholds; Cubie's first visit after 50 White clicks (gift: a Recycler - the bill he'll come back
for); the shop opens; the RGB pack; the Auto Clicker; the Toggles window; Standard level 2 opens the Skills window; the first skill
pack (Lumberjack's Pack). Teaching order: **(Q13)**.

### Mid game - skills and machines (Built, growing)
Gathering skills (Woodcutting, Foraging, Fishing, Mining, Farming, Electrician), each with filler, hazards, specials, perks and a
tree; devices to handle husks; minigames bought as packs; visitors and vendors; pets; crafting; the Pixel Bank.
**Agreed next:** Woodcutting's tree mode, first person as a mid-game unlock (the Cube Visor), the gear system (`GearDesign.md`).

### Late game - explore and grind (Decided direction)
By now the player has every tool (machines, consumables, minigames, vendors, first person, gear) and mixes them their own way while
grinding skills towards the cap. **Cosmetics** locked behind long goals give the late game things worth chasing - the game already has
places for them: pet outfits, floor / sky / bar styles, cube looks, gear models. Which ones and for what: **(Q1)**.

### Prestige - paying Cubie back (Decided theme)
Cubie gave you a small gift and a Recycler at the start. Prestige is paying him back, and he means it: he talks about suing you,
*with interest*. **Keep it a joke:** the "debt" is only that small gift and a second-hand bin, but after all this time the interest has
piled up, and Cubie inflates the bill further every time he brings it up. It is one petty guy and his absurd invoice, never a "pay your
debts" theme. Each prestige settles the bill and resets the run - and the next bill is somehow even bigger.
Details: what resets **(Q2)**, how the bill grows **(Q3)**.

### End game
Open: **(Q4)**. Whatever it becomes, no story payoff - at most Cubie reacts to being paid, the way anyone would.

### Length (Decided)
Open-ended, no set playthrough time. Pacing is tuned per skill and per goal (how long a level, a log page, a prestige should take),
not towards a finish line.

## 7. Systems map

Links between systems: `ConnectionMap.md`. The short tour:

| System | Role in the game | State | Doc |
|---|---|---|---|
| Pixels / blocks | each type is its own currency; tags decide what a block is | Built | `PixelBlockCatalog.cs` header |
| Husks | the material every click leaves behind | Built | `PixelRules.md` |
| Skills | what spawns, hazards, perks, a tree; they feed each other | Built (6 gathering + Standard + Offline) | `PixelSkills.md`, `SkillBlocks.md` |
| Devices / machines | turn husks into XP, storage, charges, currency (Recycler) | Built; factory machines are tester features | `PixelConsumables.md`, `PixelFactory.md` |
| Shop | packs, upgrades, Value levels, consumables, minigames | Built | `PixelShop.md` |
| Vendors / visitors | trades, buy skill currency, sell tools; Cubie | Built | `PixelVisitorMinigame.md` |
| Minigames | events that pay currency and raise the XP multiplier | Built (14+); skill links **(Q12)** | per-script headers |
| Collectibles | Dragon / God / Prime Cubes, the Golden Snitch, Collection Log, gallery | Built | `PixelGodCubes.md`, `PixelDragonWish.md` |
| Pets | one per block type, perks, outfits | Built | `PixelPets.md` |
| First person | walk the floor, handle husks, repair machines (Agreed) | Built; unlock + relics Agreed | `PixelFirstPerson.md`, `GearDesign.md` |
| Gear | one slot (you're a cube), relics, legendary gear | Agreed | `GearDesign.md` |
| Combat | enemies as designed slot lists on the cube (built on tree mode), weapons as gear | Idea | `CombatDesign.md` |
| Prestige | paying Cubie back | Theme decided; details **(Q2, Q3)** | this doc |
| Cosmetics | late-game rewards | Being considered **(Q1)** | this doc |
| Offline progress | earnings while away | Built (auto clicker only) **(Q10)** | `PixelOfflineProgress.cs` header |

Cutting and merging: **(Q8)** - several minigames only pay currency, several shop devices have no skill home, and Charcoal / Wood
Chips / Magic Wood have no use yet.

## 8. Economy

- **Built:** each pixel type is its own currency; Pure pixels (the six basic types) are the shop currency; Value upgrades scale
  payouts; potions are priced from expected output; vendors buy skill currency for Pure pixels at a rarity-based rate; long number
  suffixes up to ~1e306. Rarer pixels are deliberately slower to upgrade.
- **Agreed direction:** numbers keep growing over days; prices re-laddered for the Value economy (`docs/roadmap.md`).
- **Tools:** the Dev Hub's Balance report (per-skill XP, minutes to level), `PixelBalanceReport.cs`.
- Open: the long-term sink **(Q9)**, offline progress **(Q10)**.

## 9. Content and version 1.0

**1.0 direction (Decided):** flesh out the game's systems and ship **a handful of skills**; more skills arrive as **updates** after
release. Exact scope: **(Q5)**, **(Q11)**.

| Kind | Built | Agreed / on the to-do |
|---|---|---|
| Gathering skills | 6 (Woodcutting, Foraging, Fishing, Mining, Farming, Electrician) + Firemaking (dev-only) | Woodcutting trees |
| Minigames | 14+ | Early Cube Dash |
| Vendors / visitors | Cubie, Prospector, Lumberjack, Tinkerer, Wizard, Entrepreneur, Farmer, 2 cats | the alien |
| Devices | ~20 | Pesticide Sprayer, factory machines as real items |
| Collectibles | 7 Dragon + 3 God + 6 Prime Cubes, the Cubelette, the Golden Snitch | legendary gear |
| Homages | 5 families | ~9 ideas on the to-do |

## 10. Onboarding and teaching

- **Built:** Cubie's visits, the shop tutorial (from its "?" button), the toggles tutorial, Standard level 2 opening Skills, first-time
  tips (`PixelHints`), Did You Know tips, the Game Guide (Controls, How To Play, Database).
- **Agreed:** meet one idea at a time (the to-do's first-hour notes); first person and gear arrive mid-game as reveals.
- Open: what the first 5 minutes teach **(Q13)**.

## 11. Look and sound

- **Art direction (Decided):** what the game already is - smooth, clean menus with retro / 8-bit / cube visuals. No single fixed style
  on purpose: new looks follow the developer's feel for what fits, so match the existing look rather than a rulebook.
- **Built:** cubes everywhere, per-type looks (`PixelLooks.md`), code-made sounds and synths, a music player, sky / floor / filter /
  bar styles, the fancy pause menu.
- **Music (Decided):** the developer's own Suno tracks.

## 12. Technical direction

- Unity 6, everything built in code, one scene. Claude writes and maintains all the code; the developer tests in Unity (CLAUDE.md).
- Recent cleanups: `PixelCalm`, `PixelStatics`, `PixelPrefs`, `PixelFind.Cached`, the `PixelClicker` split, `PixelSystems` (systems in
  the scene for editing). Further cleanups and the save-version plan are on the to-do.
- Saves before release: **(Q15)**.
