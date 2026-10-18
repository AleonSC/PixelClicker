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
- **After 1.0 (maybe):** gear, the alien vendor and its relics, combat, and first person as an unlock are parked for updates (section 9b); first person stays simple in 1.0.
- **Art direction:** what the game already is - smooth, clean menus with retro / 8-bit / cube visuals; no fixed rulebook.
- **Music:** the developer's own Suno tracks (paid plan, no attribution needed).
- **Early game path:** RGB is Cubie's unlock at 100 Black, not a purchase; the Auto Clicker is the shop's first buy; a skill is nudged as the first branch (section 6).
- **Early game length (2026-10-17):** about an hour, ending when the player first chooses between skills; the unique mechanics land in the first 10 minutes and the rest of the hour deepens them (section 6).
- **Standard (2026-10-17):** the longest-levelling skill; it gives abilities (tree) and unlocks the general husk-handling devices in the shop (level perks); gathering skills give content and their own tools (section 6).

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

### Early game - the first hour (Decided 2026-10-16, built, being tuned)
The agreed path (times for a normal player at about 4 actions a second, clicks + husk crushes). **Times re-set 2026-10-17 from the
6:24 session report and KEPT on purpose:** crushing takes about a quarter of the player's actions, which slowed the unlocks by ~2.5 min,
and the developer likes that pace - it leaves room for the tutorials. Do not lower the unlock costs to speed it back up.

| Time | Step | What it teaches |
|---|---|---|
| 0:00-~2:00 | **White** (Gray now unlocks by the 'Crush 10 husks' goal, Black by 'Recycle 20' - see 'Early game feel'). Cubie's first visit after the 'Click 50' goal: the Recycler. | Clicking, husks, edge-click aiming. |
| ~2:00-4:20 | **Gray** (200 to unlock Black). Standard level 2 (~5:00): the first skill point - Block Hold or Power Click. | The first choice. |
| ~4:20-6:10 | **Black**: earn 100. | The last plain grind. |
| ~6:10 | **Cubie's second visit:** RGB is UNLOCKED (not bought - he gives the RGB Pack), a taste of Red / Green / Blue, the shop opens. "The rest of the pure currency: grind it and you're ready to branch off." | Six currencies. |
| ~6-10 min | **The RGB phase.** First buy: the **Auto Clicker** (150 of each pure pixel). Value upgrades on the side. | Automation, the Toggles window. |
| ~10-17 min | **The branch.** All six pure currencies in hand. A **skill comes first** (nudged: the Lumberjack's Pack is the cheapest next buy, Cubie names Big Tim, the Toggles tutorial and the Log goal point at it); special pixels (Glass...) and minigames follow. | Skills are the heart of the game. |

The RGB Pack stays in the data (everything that "needs the RGB pack" keeps working) but is never sold.

### Early game, minutes 15-60 - the first skill (Agreed 2026-10-17, numbers to tune)
**Early game lasts about an hour and ends when the player first has to choose between skills** (the second skill pack becomes
affordable / worth buying). By then every core idea has been met once: clicking and husks, the Recycler, the Auto Clicker, a skill
with its filler and hazards, a first tree point, a first husk machine.

**The hooks land early, the hour goes deep.** Everything that makes the game stand out (husks, Block Hold, Power Click) arrives in
the first 10 minutes; the rest of the hour adds TWISTS to those moves (Standard's tree branches) and new CONTENT (the first gathering
skill), never long gaps. Aim: something new every 5-8 minutes, one idea at a time.

Minutes 15-60, roughly: level the first skill until its filler thins out, spend its first tree point, meet its first hazard (right
click), get the first husk machine (the to-do's Standard-XP deposit box), the skill's vendor visits (Big Tim), maybe one forgiving
minigame (the to-do's Early Cube Dash).

### Early game feel: click, pile, crush (Agreed 2026-10-18; ALL BUILT 2026-10-18 - point 1 see docs/systems/PixelRules.md 'Husks wait for you'; point 2 `PixelGoals.cs` goal table; point 3 the mystery bar via `PixelSkills.ShownName`)
The developer's playtests: clicks and husks felt like they COMPETE - husks fade in ~15 s so they demand attention at the same moment as
the cube, and the early goals ('click N', 'earn 300 White') all say "click", so crushing feels off-task. The fix is a RHYTHM: click a
bunch, build a pile, then get told to crush it.

1. **Husks wait for you:** base husk lifetime ~35 s early (`PixelClicker.oldPixelLifetime`, was 15 s; Standard's Lingering perk still adds
   on top). **The floor limit rises with it** (~120, was 50) - at ~3.5 blocks a second 50 husks fill the floor in ~14 s and the oldest are
   culled, so a longer lifetime alone does nothing. Resting husks sleep / settle so a big pile stays cheap (see the husk heaps prompt);
   re-check performance with ~120 husks.
2. **Goals alternate clicking and husks, and the early GOALS ARE THE UNLOCKS** (the Log's Goals tab; decided 2026-10-18). Gray and Black
   no longer unlock by thresholds of the previous colour ('earn 300 White'); each early goal's REWARD is the next step, so every new
   colour rewards learning one new action (one idea at a time). The colours stay (monochrome still pays for machines in the shop).
   Husk goals appear at the moment they make sense; rewards come from the goal, never from a husk.

| Goal | Reward | Teaches |
|---|---|---|
| Click 50 blocks | Cubie's 1st visit: the Recycler | the core click |
| Crush 10 husks (shows once ~20 husks lie on the floor) | **Gray unlocks** | crushing |
| Recycle 20 husks | **Black unlocks** | the money route |
| Get a crush streak of 5 | a chunk of Standard XP (the "???" bar jumps; level 2 is close) | bursts and streaks |
| Earn 100 Black | Cubie's 2nd visit: RGB + the shop | earning still matters |

   **Built 2026-10-18** (`PixelGoals` rows + `TierUnlockMode.ByGoal` + `PixelGuideVendor.RequestVisit`; the Black goal is **Earn 120 Black**,
   sized with the others for the shop at ~6 min - pace notes in the `PixelGoals.cs` header).
   Size the goals to keep the pace the developer likes (RGB and the shop at about 6 minutes). Build it on the goal table that now exists
   (`PixelGoals`, built 2026-10-18): add reward types 'unlock a tier' and 'start a Cubie visit', and a new tier unlock mode 'by goal'
   (appended to `TierUnlockMode`), so the early progression lives in one table.

3. **The skill reveal: a mystery bar from minute 1, revealed at Standard 2 (~5 min).** Standard's XP bar shows from the first click but
   labelled "???" (hover: "Something is growing..."), XP popups read "+1 ???", and crushing visibly fills it faster. At level 2 it flips
   to "Standard" with a short fanfare and the first-move choice - the existing level-2 popup ("Surprise: you have been training a skill
   all along") becomes the payoff of a 5-minute mystery instead of coming out of nowhere. No new system: the existing bar, hidden label.

Test: while crushing, do you feel "I should be clicking"? While clicking, "I'm wasting husks"? If not - it reads as "click for a bit,
then clear the pile" - it works.

### Standard - the ability skill (Agreed 2026-10-17, numbers to tune)
Standard is the skill that levels the LONGEST: it trains all the time (1 flat XP per block, whatever skill is trained), just at its
own pace. **Two tracks with different jobs:** Standard gives ABILITIES (new ways to click) and GENERAL husk tools; the gathering skills
give CONTENT (blocks, hazards, vendors) and their own tools.

**XP curve:** front-loaded - the first 5-6 levels come about every 8-10 minutes (the early-game rewards), then the curve slows sharply
(the long grind). Standard XP also counts auto clicks, so the minutes must be tuned with the Auto Clicker running.

**Abilities (skill tree, one point per level):**

| ~Minute | Standard level | Tree unlock |
|---|---|---|
| ~5 | 2 | Block Hold *or* Power Click (the new move) |
| ~8-10 | 3 | The other one, or Aimed Clicks (edge-click aiming, see 'Husk routing rework') |
| ~20 | 4 | First twist: Lucky stake, Quick charge or Chain Crush |
| ~30-35 | 5 | What's Next (needs both moves) |
| ~45 | 6 | Second twist: High roller, Aftershock or Clean Break |
| ~60 | 7 | Shuffle Combo - the end of the early game |

**Husk Crush (Agreed 2026-10-17 in the Design Discussion chat, built 2026-10-17 - `PixelHuskCrush.cs`):** husks get a use from the very first
minute. **Base crush is FREE** (not a tree node): RIGHT-CLICK a husk lying on the floor and it breaks apart for a little skill XP.
- **XP, never currency.** A crush gives about **25%** of the XP a click on that block gave (Inspector value), to the husk's own skill
  (`PixelSkills.AddHuskXp`: pure husks -> Standard, wood husks -> Woodcutting...). It never pays currency (pixel rules: husks are never
  currency; only the Recycler turns them into currency, at a loss).
- **Two routes from the start:** with Cubie's Recycler (visit 1) the player chooses per husk: crush = instant small XP, recycle = currency +
  XP + streak.
- **Taught** by a first-time tip once husks lie about: "Right-click a husk to crush it for XP".
- **Right-click priority is decided in ONE place** (`PixelRightClick.cs`): menus / UI / the intro block it; the Bank hose and the conveyor kit
  (and device placing) keep the right button while active; a pet under the pointer opens its menu; a device part that claimed the pointer
  keeps it; the live cube = Block Hold / clearing hazards (it wins even with a husk in front of it); otherwise the husk under the pointer is
  crushed. Husks held in machines (Recycler, Storage Crate, Sorting Hopper, Bait Bucket - `PixelHuskHolder`) or riding a belt can't be crushed.
- **Feedback:** the husk bursts into small chunks of itself (the glass shatter's shards) with a crunch, and the usual skill XP popup (small
  amounts show with decimals).
- **Tree upgrades (Standard), next to the twists so those points become real picks:** **Chain Crush** (Standard 4, ~20 min): crushing a husk
  also crushes the husks of the SAME type touching it (`PixelHuskQuery.Near`); **Clean Break** (Standard 6, ~45 min, 2 points): each crush
  gives the husks around it a few seconds of their lifespan back; **Hard Hands** (Standard 9, later, 2 points): crushes give more XP (x1.6 =
  40% of a click), still well below a click. The tree's `minLevel` holds a node back until the skill reaches that level.
- **Charge Crush (Agreed 2026-10-17, built 2026-10-17 - `PixelHuskCrush.cs`; tree row 4, level 7, 2 points, cooldown 8 s, reach 1.5 -> 8 husk
  widths over 1.5 s) - the step above Chain Crush:** CLICK = Chain Crush, the quick cleanup (the husk + touching
  same-type husks). HOLD right on a husk = charge: a ring grows from it the longer you hold (capped reach, Inspector values); letting
  go sends an arc to every same-type husk inside the ring, touching or not - a slower but more thorough cleanup. It mirrors Power
  Click (hold left on the cube to charge). Needs Chain Crush; about Standard 7-8 (~60 min). Short cooldown after a charged crush so it
  never out-earns clicking. Right-click priority is unchanged: holding on the cube = Block Hold, holding on a husk = the charge (the
  target decides, `PixelRightClick`). Maybe later: a **Wide Arc** node lets the arc jump to any husk type.
- **Crush bonuses (Agreed 2026-10-17 from the 10:50 session report, built 2026-10-17 - `PixelHuskCrush.CrushXp`; Standard retuned with them:
  baseXp 380 -> 600 and the Recycler gives pure husks half its XP, see `PixelSkillsStandard.cs`):** the report showed why crushing felt flat - a crush
  paid 0.25 XP while a recycled husk paid ~2.6 (Recycler streaks), and HALF of all husks were wasted (340 faded, 823 removed by the
  floor cap, of 2,357). Crushing's job: use the husks the Recycler misses. Numbers (C = a click's XP on that block):
  plain crush **0.5 C** (was 0.25); **air crush** (husk crushed before it lands) **x2 = 1 C**; **last-gasp** (its last ~2 s, while it
  flickers) **x1.5 = 0.75 C**; **crush streak** (crushes within 1 s of each other) **+10% each, capped at x1.5**. **HARD CAP: a crush never
  pays more than a click (1 C), whatever stacks** - no reward for swapping clicks for crushes (decided 2026-10-17: no 'swap bonus').
  - **No new UI:** a bonus REPLACES the usual XP popup with a short text - "Air crush bonus!", "Last-gasp bonus!", "Crush streak x1.4!".
  - **Checks:** alternating click + crush = the same XP as clicking only, for half the currency, so swapping never pays; crushing's
    value is the husks the Recycler misses and the time the Auto Clicker runs (crushing what it drops + its clicks ~50% of clicking by
    hand - active clicking stays best). The Recycler (~2.6 C + currency per husk) stays the best home for a husk.
  - **Knock-on:** in the same report Standard levelled ~2x faster than planned (level 4 at 8:21, plan ~20 min) because the Recycler's
    streak XP (2,620) out-earned clicking (2,357); better crushing adds more. Retune Standard's curve and/or how much Standard XP the
    Recycler gives together with this.
- **Crush streaks recharge the first move (Agreed 2026-10-17, not built):** the early-game payoff for crushing, from Standard level 2
  (~5 min), before the Auto Clicker / Combo Meter exist. Each crush in a streak takes **1 s off Power Click's cooldown** (45 s) and adds
  a little to the **Block Hold meter** (like its 'every 5 clicks +1%' energy). Small and capped - about one extra Power Click a minute at
  most, never a money source - so it is not a swap bonus. Why: both session reports show the first move is barely used (1 and 4 power
  clicks, 0 block holds); this gives crushing and the move a reason to exist together. No new UI: a short "Power Click ready!" text
  when a streak finishes the cooldown (reuse the existing ready signal / popup).
- **Tuning note:** pure husks give Standard XP, so crushing speeds up early Standard levels. Keep the base small; the Standard XP curve
  targets above (levels 2-7 at about 3 / 8-10 / 20 / 30-35 / 45 / 60 min) must be tuned with crushing AND the Auto Clicker in the mix.

**Shop unlocks (level perks, not tree points):** Standard levels unlock the GENERAL husk-handling devices in the shop. Rules:
1. Standard gates tools that work on any husk; a skill's own tools stay with that skill and its vendor (Wood Chipper -> Woodcutting,
   Bait Bucket -> Fishing, Sprinkler -> Farming).
2. Unlocks are level PERKS, never tree points - the player never picks between an ability and a shop item.
3. Standard unlocks ACCESS; currency still buys the item. The shop shows it locked with "Standard N" so the player sees what's coming.
4. The first tool must land inside the first hour, or husks pile up with nothing to do with them.

| Standard level | Unlocks in the shop |
|---|---|
| 3-4 | Sorting Hopper (the Recycler's partner, below) |
| 4-5 | Storage Crate (just holding husks) |
| 5 | Magnet, Fan (moving husks) |
| 5-6 | Sweeper (rescues stray husks, below) |
| 6-7 | Funnel (catching husks automatically) |
| 8-10 | Sorter, then Magnetized Sorter |
| 12+ | Quantum Organizer; later the factory pieces as real items |

Build it as ONE shared rule (a small "needs skill X at level N" table on shop items) read by the Shop, Toggles and Game Database,
not a check per device; the same table later covers skill-gated minigames and the gathering skills' own tools (to-do).

### The early husk setup: Recycler + Sorting Hopper (Agreed 2026-10-17, built 2026-10-17 - `PixelSortingHopper.cs`)
The Recycler (Cubie's gift) pays a STREAK bonus for the same husk type in a row, but husks land mixed and fade in about 15 s, so
streaks rarely happen. The **Sorting Hopper** is its partner: a choice, a little help, and still a loss - right for the early game.

- **Snaps to the Recycler's back.** Placed near a Recycler it snaps onto the back wall; its exit lines up with the bin's back hatch.
  It is a wide tray, so it catches husks the bin alone would miss.
- **A filter toggle picks ONE husk type** (shown as a coloured flag on the hopper; click / scroll to change it). Husks of that type
  slide through the exit into the Recycler one after another - a steady same-type stream, so the streak builds.
- **Every other husk stays in the hopper and despawns like normal** (no keep-alive). They take room, so a hopper full of the wrong
  colours catches less until they fade - that is the loss, and choosing the type is the decision.
- **Without a Recycler** the exit stays shut and it is just a tray. When a Recycler's time runs out the hopper stays placed and snaps
  onto the next one.
- **Unlock:** Standard level 3-4 (the first general husk tool, see the table above); bought for pure currency.
- **As built:** no separate capacity - the tray's physical room is the limit; it latches onto a free Recycler within 6 world units
  (the Device's Reach), on placing and later; 150 White, on sale from Standard 3 (vendorOnly + shopSkillId, so it is simply
  hidden before then - the "shown locked with Standard N" part waits for the shared skill-requirement to-do).
- **Changed after the first look (2026-10-17):** the flag only steps through the types lying in the tray (the first husk in sets it); an
  orange EJECT button throws everything in the tray out over the back; no floating text - hovering the flag shows a small card.
- **Open:** whether the filter can also be set to "any pure", and the price after testing.

### The Sweeper - an early stray-husk helper (Agreed 2026-10-17, built 2026-10-17 - `PixelSweeper.cs`)
A little wind-up clockwork cube with a broom: early automation for stray husks that is NOT tied to Electrician (no power needed).

- **Only rescues strays:** it fetches husks that are close to fading (their last few seconds) and leaves the rest to the player, so it
  saves waste without playing for you. It moves husks, it never processes them.
- **Puts them in the NEAREST CONTAINER with room** - any `PixelHuskHolder` (Storage Crate, Sorting Hopper, Recycler). Placement is the
  player's choice: a crate nearest = strays are stored safe; the hopper nearest = strays go straight to recycling. Later the crate's
  output coupling + a belt links stored strays on to a Recycler (the socket / coupling plan) - the first factory chain.
- **Wind-up:** runs about a minute, then slows to a stop until clicked to wind it again (a light active touch: active is better, idle
  is possible).
- **Unlock:** Standard 5-6 (~30-45 min), in the shop for pure currency - after the player has felt husks fading even with Crush, the
  Recycler and the hopper.
- **Upgrades without needing them:** Electrician fits a motor (no more winding - an early use of power sockets); Standard upgrades
  range and speed.
- **Built from the Tiny Robot** (`PixelTinyRobot.cs`, tester: fetches loose husks onto a belt): same fetching code, new target (a
  container instead of a belt), wind-up instead of electric - not a fourth separate husk fetcher. Its own job next to the Magnet
  (gathers a heap) and the UFO Helper (late-game cleanup into the Bank).
- **As built:** Standard 5, 400 White; range 12 husk widths from its pad, 'close to fading' = 4 s or less left, 60 s per wind (slows over the
  last 12 s); the nearest container is measured from where the husk was picked up; no carry handles (it roams from its pad: remove and place it
  again to move it). Open: the Standard range / speed upgrades and the Electrician motor (`motorFitted` is the hook).

### Husk routing rework: aiming becomes a skill, the Recycler pays currency (Agreed 2026-10-17; BUILT 2026-10-17 - point 1 Aimed Clicks: node `aimed`, Standard 3, row 1 col 0; the Recycler part: see point 7)
Why: the 10:50 session report showed the early husk setup was too automatic - edge-click aiming (free from the start) sent husks
straight into the Recycler (1,017 recycled, an 89-husk streak), and the Recycler paid MORE Standard XP (2,620) than clicking (2,357)
while also paying currency, so Standard levelled ~2x too fast and Husk Crush had no job.

1. **Edge-click aiming is no longer free.** Without it, a click scatters its husk at random round the cube (the existing burst).
   Aiming (click near the cube's edge to throw the husk out of that side, `PixelBurstPop.edgeAiming`, the aim cursor `PixelAimCursor`)
   becomes a **Standard tree node, "Aimed Clicks"** (~Standard 3-5; a new way to click, like Block Hold / Power Click). The aim
   cursor only shows once it is owned.
2. **Recyclers catch husks more easily: a modest SUCTION ring** round the rim pulls nearby loose husks in. Modest on purpose - one bin
   catches SOME of the random scatter, not all of it, so placement matters. Grown by the Operation tab (Bin Size, or a new Suction row).
3. **More Recyclers are a real buy:** several can stand at once, each extra one costs more (a rising price ladder) - an early currency
   sink (Q9) and a placement puzzle (ring the cube with bins).
4. **Role split - every husk is a choice of money OR XP:**

| Route | Gives | Not |
|---|---|---|
| Recycler | currency at a loss (50%); the same-type STREAK boosts the CURRENCY | XP (none, or very little) |
| Husk Crush | XP (with the capped crush bonuses) | currency |

   The Sorting Hopper stays the streak tool (a single-colour stream = more currency). The Sweeper still feeds the nearest container.
5. **One owner per husk:** suction, the Magnet, the Sweeper and the hopper can reach for the same husk - whoever captures it first owns
   it, through `PixelHuskHolder`, never a special case per device.
6. **Re-check after building:** Standard's pace against the curve (levels 2-7 at about 3 / 8-10 / 20 / 30-35 / 45 / 60 min) with a new
   session report - removing the Recycler's XP should bring it close to clicking-only.
7. **As built (Recycler part, 2026-10-17):** 50% currency x the streak (+0.1 a same-type husk, cap 1.6 = 80% of a click; Streak Power +0.06 a
   level), hard ceiling 95% of a click; no XP (xpPerHusk 0). Suction ring 2 husk widths past the rim + the new Operation row **Suction** (+0.75 a
   level) - a separate row rather than folded into Bin Size, so the player chooses between a bigger bin (more lands in by itself) and a longer
   reach (draws more in from the floor), and Bin Size keeps meaning one thing. Price ladder x1.8 per Recycler held or standing. One owner per husk
   = PixelHuskHolder.Takeable (Recycler, Magnet, Sweeper, hopper). Note for the pace check: Standard's baseXp was raised 380 -> 600 (Skills
   session) partly to absorb the Recycler's XP, which is now gone - the next report says whether to lower it again.
8. **Changed 2026-10-18 (Devices session):** auto-recycling REMOVED - the player presses the button again (it flashes as soon as one husk is in);
   the suction stays. The streak is now built per button press, in the order the husks went in. Default bin 2x wide and deep (4x footprint).
   Operation rows for the Recycler and Sorting Hopper are now just **Speed** and **Size** each (Run Time, Streak Power and Suction rows removed).
9. **Changed again 2026-10-18 (same day):** AUTO RECYCLING IS BACK (the button is hidden, not deleted: `showButton`); Speed now shortens the
   recycle delay. Default bin 10% smaller (1.8). The Sorting Hopper became a Minecraft-style FUNNEL behind the bin that rises above it and HOLDS
   up to 20 husks; its filter (picked in its new device window) goes through a feed pipe into the bin, every other type drops out of a side tube
   (no more fading tray as the loss - the loss is now that rejects end up loose on the floor behind the bin).

### Mid game - skills and machines (Built, growing)
Gathering skills (Woodcutting, Foraging, Fishing, Mining, Farming, Electrician), each with filler, hazards, specials, perks and a
tree; devices to handle husks; minigames bought as packs; visitors and vendors; pets; crafting; the Pixel Bank.
**Tested and liked (2026-10-16):** Woodcutting's tree mode (numbers still to tune). First person as a mid-game unlock and the gear system are parked for after 1.0 (section 9b).

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
| First person | walk the floor, handle husks | Built; kept simple in 1.0 (unlock + relics after 1.0, maybe) | `PixelFirstPerson.md` |
| Gear | one slot (you're a cube), relics, legendary gear | After 1.0, maybe (9b) | `GearDesign.md` |
| Combat | enemies as designed slot lists on the cube (built on tree mode), weapons as gear | After 1.0, maybe (9b) | `CombatDesign.md` |
| Prestige | paying Cubie back | Theme decided; details **(Q2, Q3)** | this doc |
| Cosmetics | late-game rewards | Being considered **(Q1)** | this doc |
| Offline progress | earnings while away | Built (auto clicker only) **(Q10)** | `PixelOfflineProgress.cs` header |

Cutting and merging: **(Q8)** - several minigames only pay currency, several shop devices have no skill home, and Charcoal / Wood
Chips / Magic Wood have no use yet.

## 8. Economy

- **Built:** each pixel type is its own currency; Pure pixels (the six basic types) are the shop currency; Value upgrades scale
  payouts; potions are priced from expected output; vendors buy skill currency for Pure pixels at a rarity-based rate; long number
  suffixes up to ~1e306. Rarer pixels are deliberately slower to upgrade.
- **Agreed direction:** numbers keep growing over days; prices re-laddered for the Value economy (`docs/systems/OlderNotes.md`).
- **Tools:** the Dev Hub's Balance report (per-skill XP, minutes to level), `PixelBalanceReport.cs`.
- Open: the long-term sink **(Q9)**, offline progress **(Q10)**.

### Shop consumable pricing (Agreed 2026-10-17, built 2026-10-17)
Why: the early devices cost White (or Gray) only, but the shop opens after all six pure colours are unlocked (the 10:50 report: RGB at
3:59, shop at 4:29), so White drains while the other five pile up. Income in that report (per minute, session average): White 113,
Gray 85, Black 80, Red 135, Green 128, Blue 160.

1. **Every shop item costs 2-3 of the pure colours**, so all six are spent.
2. **Colours have meaning:** monochrome (White / Gray / Black) = machines and structure (Recycler, Sorting Hopper, Storage Crate,
   Sweeper, couplings, Funnel...); RGB = boosts and timed things (Combo Fuel, potions, Pet Treat, Ghost Bait...). A machine may add one
   RGB 'accent' cost.
3. **Price in minutes of income, not raw numbers:** a cheap tool ~1 minute of the colours it costs, a milestone tool (Sweeper, Funnel)
   ~3-5 minutes; set from session reports, since income grows with Value upgrades.
4. **Repeat buys climb** (each extra Recycler costs more - 'Husk routing rework').
5. **The main shop and Cubie take pure currency only;** skill items (Wood Chipper, Pesticide Spray, Bait Bucket...) stay priced in
   their skill's blocks.

Starting examples (tune from the next report):

| Item | Was | New | ~Minutes of income |
|---|---|---|---|
| Coupling | 30 White | 40 Gray + 40 Black | ~0.5 |
| Extra Recycler (2nd) | - | 150 White + 100 Green, then x2 each | ~1.5, rising |
| Sorting Hopper (Standard 3) | 150 White | 200 Gray + 150 Red | ~2 |
| Storage Crate (Standard 4-5) | 300 Gray | 300 Black + 200 Blue | ~3 |
| Sweeper (Standard 5) | 400 White | 400 White + 300 Gray + 200 Red | ~4 |

Shop prices live in the scene's serialized Inspector data, so new coded defaults need a one-time reset step (like the shop's
`shopBalanceVersion` / ResetBuiltInPackPrices) to reach an existing scene.

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

## 9b. After 1.0 (maybe)

Content parked for **after 1.0** (decided 2026-10-16). These are ideas the developer will **probably** add in updates after release,
but **might not**. Nothing here is built, and none of it blocks 1.0. Their to-do lines are tagged `[POST-1.0, MAYBE]`.

| Idea | What it is | Doc |
|---|---|---|
| **Gear** | One gear slot ("you're a cube"), free swapping, face sockets for more slots later; legendary gear from super rares (the Prime Gauntlet's stone powers and the Snap, God Cube relics, the Dragon Radar, the Seeker's Charm) | `GearDesign.md` |
| **The alien vendor** | A 3D alien whose UFO lands at the floor's edge, reached in first person; sells first-person relics (Grav Boots, Tractor Glove, Xeno Lens, Blink Core, Probe Drone) and face sockets | `GearDesign.md` |
| **Combat** | Enemies as tree-mode slot lists on the cube (armour, weak spots, attack blocks, a health bar, loot); weapons as gear; food buffs | `CombatDesign.md` |
| **First person as a mid-game unlock** | The Cube Visor gift and a camera-dive reveal. Parked with gear | `GearDesign.md` |

**First person in 1.0: keep it simple.** First person stays what it is now - walk the floor, handle husks, break them by hand - without
relics, an unlock reveal or extra systems on top. (Whether it stays available from the start or unlocks later is still open.)

The Gear and Combat docs keep their open questions; they only need answering if and when that content is picked up.

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
