# Woodcutting progression (design, not built yet)

Status: **agreed direction, build not started.** The "Open questions" section lists what is still a proposal.
Today's Woodcutting (blocks, perks, Termite Hive, filler family) is described in `SkillBlocks.md`; this doc is where it is going.

## Goals

- **The intro skill.** Quick to start and to understand; the first ten levels take about an hour.
- **A rhythm, not random noise.** Each click should feel random, but the session should have a shape: a beginning, a build-up and a payoff that come round again and again.
- **Hazards you can plan around.** A Termite Hive should be a known risk, not bad luck.
- **Unlocks you can see.** Every early level adds one new thing; later levels make your trees visibly richer.
- **Better through other skills.** Woodcutting stays simple on its own; other skills supply its upgrades (the Whetstone comes from Mining, and so on).

## The core idea: the tree bag

The cube no longer rolls a fresh random block on every click while Woodcutting is in play. Instead:

1. **A tree grows.** The game picks a tree **species** (oak, pine...), fills a *bag* from that species' recipe (see "Species and undergrowth") and shuffles it.
2. **Every click draws the next block from the bag.** The order is random, so it still feels random click to click.
3. **When the bag is empty, the tree falls: "Timber!"** A short felling effect and sound, a small reward (see "Timber reward").
4. **Undergrowth.** Before the next tree grows, a short run of 0-3 loose blocks comes up (twigs, moss, sap...): a breather between trees, drawn from a separate undergrowth pool.

Why it works:

- **Random but fair.** Every tree has the same kind of mix. No long dry streaks, no clusters of bad luck.
- **Predictable hazards.** A tree holds **at most one** Termite Hive. Once it has shown up you know the rest of the tree is safe.
- **Something to look at.** A small **tree bar** shows the species and how much of it is left (e.g. `Pine 7 / 18`), so there is always a short goal in sight.
- **Easy to expand.** A species is just a recipe (data). A new tree type is a new entry in a list, not new code - and the same system works for other skills (see "The bag system for other skills").
- **Levels you can see.** Levelling changes what goes *into* the bag (bigger trees, more wood and heartwood, less canopy junk) instead of changing hidden percentages.

### How the existing mechanics fit in

| Mechanic | With the bag |
|---|---|
| **Next Up window** | Shows the next draw from the bag (as now with the pre-rolled block). |
| **Block Hold** | Holding pulls the live block out; letting go puts it back as the *next* draw (it already works like this through `nextInLine`). It does not go back into the bag, so holding never changes the tree's mix. |
| **Shuffle bonus** | A right tap on a filler swaps it with a random block *later in the bag* (instead of a fresh roll). The combo still grows when the block it hid behind is broken. |
| **Hazard defuse / Pesticide** | Unchanged: right clicks (10 for the Hive) or a held Pesticide clear the Hive; it then counts as drawn. |
| **Great Oak** | Acorns lying on the floor raise the chance that the **next tree** is a Great Oak tree (a special bag, see below) instead of replacing a single block. |
| **Auto clicker** | Draws from the bag like a manual click. It still never touches a Termite Hive (it waits for you), as today. |
| **Focus potion** | Lumberjack's Focus: new trees get no Hive while it lasts (instead of removing the Hive's spawn share). |

## Lookahead grows with the skill

How far ahead you can see into the bag is itself a reward: early on you react, later you plan.

| Source | What you see |
|---|---|
| Level 1 | The next block (the Next Up window, as now) and the tree bar (`Oak 7 / 18`). |
| Level 7 | **Keen eye** (perk): the next **two** blocks. |
| Level 20 | **Woodsman's eye** (perk): the tree bar shows a small Hive icon while this tree still has a Hive in it. |
| Skill tree: **Read the grain** | The next **three** blocks. |
| Level 30 | **Master forester** (perk): the tree bar lists what is left in the tree as counts (e.g. `3 wood, 1 sap, 1 heartwood`). |

- The Next Up window grows a slot per extra block (the next block on top, then the ones after it, smaller), so the same window just gets taller.
- Blocks bumped back by Block Hold show first, then the bag's next draws, so the preview is always the real order.
- Cross-skill angle (later): a Mining or Electrician item could give a temporary "see the whole tree" boost.

## Blocks and their roles

| Block | Part of the tree | Hits | Role |
|---|---|---|---|
| Leaf | canopy | 1 | Fast filler-ish block, floats down. |
| Twig / Moss | undergrowth | 1 | The filler family, now mostly BETWEEN trees: quick, nearly worthless breathers. |
| Pinecone | pine tree | 1 | Part of the Pine tree's recipe (no longer a plain filler). |
| Wood | trunk | 2 | The bread and butter. |
| Bark | trunk | 1 | Common, cheap trunk block. |
| Sap | undergrowth, maple | 1 | Sticky old pixel, pays a bit more. Mostly undergrowth; the Maple tree is full of it. |
| Acorn | oak tree | 1 | Part of the Oak recipe; its old pixel on the floor raises the Great Oak chance. |
| Heartwood | core | 4 | Rare payoff. |
| Termite Hive | hazard | 4 left / 10 right | At most one per tree. |
| Great Oak | special tree | - | A whole special tree, not a single block (see below). |
| **Whetstone** | - | - | **Leaves Woodcutting:** comes from Mining (see "Cross-skill upgrades"). |
| **Pesticide** | - | - | Proposed to leave Woodcutting too and come from Farming or Foraging (open question). |

## Level gating

| Level | What changes |
|---|---|
| 1 | **Oak** trees only, with just **Wood and Leaf**; undergrowth is Twig and Moss. Small trees. |
| 2 | **Sap** joins the undergrowth; trees can be **infested** (a Termite Hive inside). Perk: Wood Chipper (as now). |
| 3 | Oaks drop **Acorns**; **Great Oak** trees can grow. Perk: wood pixels last longer (as now). |
| 4 | **Pine** trees (log, needles, pinecones). Bark joins the oak's trunk. |
| 5 | Perk: Wood can spawn in any skill (as now). |
| 6 | **Heartwood** cores can be in a tree. |
| 7 | Perk: Keen eye - see the next two blocks. |
| 8 | **Birch** trees (light, bark-rich, quick). First skill point (see "Skill tree"). |
| 10 | Evil Tree minigame can appear (it needs the Woodcutter's Pack today; gate it at 10). |
| 12 | **Maple** trees (sap-rich). Lumberjack sells Lumberjack's Focus. |
| 15, 20, 25, 30 | Mastery perks (see "Perks"). |

## Species and undergrowth

### A species is a recipe

Each tree species is one entry in a list (Inspector data). An entry says:

| Field | Meaning |
|---|---|
| name, unlock level | e.g. "Pine", level 4 |
| weight | how often it is picked among the unlocked species |
| blocks | each block with a **min-max count** (e.g. Wood 3-6): the counts are rolled when the tree grows, so trees of one species come in different sizes |
| growth per level | how much the counts grow with the skill level (e.g. +1 max wood every 5 levels), so trees get bigger and richer as you level |
| core | an optional block that always goes in (e.g. heartwood for the Great Oak) |
| infested chance | the chance that this tree hides a Termite Hive (never the first or last block) |
| Timber reward | what felling it gives (XP bonus, a drop) |

The log count is what mainly sets a tree's size, so "a big oak" and "a small oak" are just different rolls of the same recipe.

### Proposed species

| Species | Unlock | Recipe (counts at its unlock level) | Character |
|---|---|---|---|
| **Oak** | 1 | Wood 3-6, Leaf 3-5; Acorn 0-1 from level 3; Bark 0-2 from level 4; Heartwood 0-1 (20%) from level 6 | The all-rounder; feeds the Great Oak through acorns. |
| **Pine** | 4 | Wood 4-8, Leaf (needles) 2-4, Pinecone 2-4 | Tall, more logs, cones instead of acorns. |
| **Birch** | 8 | Wood 2-4, Bark 3-5, Leaf 2-3 | Short and quick: many Timbers in a row. |
| **Maple** | 12 | Wood 3-6, Sap 3-5, Leaf 2-4; Heartwood 0-1 (25%) | Sticky and well paid. |
| **Great Oak** | 3, rare | Wood 8-12, Bark 2-4, Leaf 3-5, core: Heartwood | Special: 1% per tree, up to 25% with acorns lying on the floor; never infested. |

Infested chance: about 25% at level 2, rising slowly with the level (to about 45% at 20), per tree; the Focus potion sets it to 0 while it lasts.

### Undergrowth between trees

After a tree falls, 0-3 loose blocks come up before the next one grows (rolled from an undergrowth pool): **Twig, Moss**, and from level 2 **Sap**. This is where the filler family now lives - a short, cheap breather between trees instead of most of every tree. The Shuffle bonus works on undergrowth fillers as before.

As you level, trees grow (more logs, more cores) while the undergrowth stays short, so the share of junk falls naturally - replacing today's filler fade (75% -> 25% over 20 levels).

### The bag system for other skills

The same three pieces - species recipes, the bag, undergrowth - fit the other skills without new spawn code:

| Skill | "Species" | Undergrowth |
|---|---|---|
| Mining | ore veins (copper vein, iron vein...), a rich vein as the special | stone, gravel |
| Foraging | bushes (berry bush, mushroom ring, herb patch), a truffle spot as the special | weeds |
| Fishing | schools / spots (shallows, reef), the whale as the special | water |
| Farming | crop rows | dirt |

## Special trees

- **Great Oak.** A species (see the table above) with a guaranteed heartwood core; its blocks stall the Block Hold meter (as the Great Oak block does today) and felling it gives a bigger Timber reward.
- **Later special trees** (the to-do's "special-block tier" for Woodcutting): ideas to pick from - a *Beehive tree* (honey that sticks pixels together), a *Hollow Log* (a creature hides in it), a *Lightning-struck tree* (a quick-click event). Fishing's bottle -> map -> treasure is the model.

## Timber reward

When a tree falls:

- A felling show: the cube "falls over" (a quick tilt and drop), chips burst out, a deep `timber` sound.
- **Bonus XP** of about 20% of the tree's own XP.
- A chance of an **Acorn** old pixel dropping (feeds the Great Oak chance).
- Counted in stats (`woodcutting.trees_felled`), which later achievements and the Lumberjack can use ("Fell 100 trees").

## XP and pacing

Today: 60 XP for level 2, each level costs 1.45x the one before, 1 XP per block. That is about 1 hour to level 10 but about 43 hours to 20 and 1,770 hours to 30, so the curve hits a wall around level 15.

Proposed: **a curve that flattens** - 1.45x per level up to 10, 1.25x from 10 to 20, 1.15x after 20 (new fields on `SkillPack`, e.g. `xpGrowthAfter10` / `xpGrowthAfter20`, or a small list of level bands).

At about one block per second of manual chopping:

| Level | Total XP | Time |
|---|---|---|
| 5 | 456 | ~8 min |
| 10 | 3,645 | ~1 h |
| 15 | 15,673 | ~4.5 h |
| 20 | 52,379 | ~15 h |
| 30 | 307,337 | ~85 h |

Also proposed: **XP per block = its hits** (Wood 2, Heartwood 4) instead of 1 per block, so tough blocks are worth their clicks; the Timber bonus comes on top.

## Perks

| Level | Perk |
|---|---|
| 2 | Wood Chipper (built) |
| 3 | Seasoned wood: wood pixels last longer (built) |
| 5 | Wood can spawn in any skill (built) |
| 7 | Keen eye: see the next two blocks (see "Lookahead") |
| 15 | Clean cut: Wood sometimes breaks in one hit |
| 20 | Woodsman's eye: the tree bar shows whether this tree still has a Hive |
| 25 | Old growth: Great Oak chance +5% |
| 30 | Master forester: every tree has a heartwood core, and the tree bar lists what is left in the tree |

The current placeholder perks at 4 ("Sap collector") and 6 ("Heart of the forest") are replaced by the gating unlocks above.

## Skill tree

Points: 1 at level 8, then one every 4 levels (12, 16, 20...). Proposed nodes:

| Node | Effect |
|---|---|
| Axe | The first person axe (the existing, not yet built node). |
| Sharp edge | Wood takes 1 hit. |
| Tapper | More sap in each trunk. |
| Acorn hoard | Acorns count double for the Great Oak chance. |
| Big timber | Trees are 3 blocks bigger (more XP per Timber). |
| Read the grain | See the next three blocks. |
| Chipper tune | The Wood Chipper gives more XP per wood pixel. |

## Cross-skill upgrades

Woodcutting stays the simple intro; other skills make it better:

| From | Upgrade |
|---|---|
| **Mining** | **Whetstone** - moves to Mining as a block / item. Used in Woodcutting it gives the rapid clicks it gives today (Block Hold), or becomes a permanent "sharpened axe". |
| Farming / Foraging | **Pesticide** (proposed): clears a Termite Hive instantly; also Saplings that grow better trees (from the existing to-do "Cross-skill links"). |
| Electrician | **Chainsaw** (idea): a powered tool for the trunk part of a tree. |
| Fishing | Idea: driftwood or a "log raft" that adds a bonus tree. |

## Economy and sinks

Wood goods today only sell to the Lumberjack. Proposed sinks, to decide later: Magic Wood (Evil Tree) and heartwood spent on permanent upgrades (a better first person axe, a stronger chipper), and wood as a crafting material.

## Saves and migrations

- **Bag state** (the species, the tree's remaining blocks and its size) is saved with the skill state so a reload does not reroll a half-chopped tree; an old save simply starts a fresh tree.
- **Whetstone** leaves the Woodcutter's Pack: a `packDataVersion` bump removes it from saved packs (the tier stays unlocked so counts are not lost) and adds it to Mining's side.
- The Hive's per-pack shares (`hazardBaseShare` / `hazardPerLevelShare`) and the filler fade (`fillerStartShare` / `fillerEndShare`) are no longer used while the bag mode is on; they stay for the other skills.

## Build plan (in steps)

1. **Bag spawn mode (shared system).** Data: a list of species recipes (blocks with min-max counts, unlock level, weight, growth per level, core, infested chance) and an undergrowth pool per skill pack. `PixelClicker.RollTier` draws from the bag when the pack in play has species. Built generic so Mining (veins), Foraging (bushes) and Fishing (spots) can use it later.
2. **Tree bar + Timber + lookahead.** The tree bar (`Pine 7 / 18`, near the Next Up window), the felling show, sound, Timber reward and stats; the Next Up window learns to show 1-3 blocks ahead (`PixelClicker.PeekNextTier` becomes a peek of N).
3. **Woodcutting species + gating.** Oak, Pine, Birch, Maple, Great Oak and the undergrowth pool; remove the per-block filler / hazard shares for Woodcutting.
4. **Hook the existing mechanics into the bag.** Shuffle swaps inside the bag; Great Oak becomes a tree; Focus potion affects new trees; Hive never first / last.
5. **XP curve bands + XP per hit.** Then retune with the Balance report (which will need a "per tree" view).
6. **Perks and skill tree nodes.**
7. **Cross-skill moves.** Whetstone to Mining (migration); Pesticide decision.
8. **Special trees** beyond the Great Oak.

## Open questions

- Species recipes and counts, unlock levels and the infested chance are proposals.
- Should Pine bring its own block types later (e.g. Pine Log, Resin) or keep sharing Wood / Leaf?
- Lookahead steps: the levels above (7 / 20 / 30 and the tree node) are a proposal.
- Pesticide: leave it in Woodcutting, or move it to Farming / Foraging?
- XP per hit vs XP per block.
- Should the auto clicker be able to fell whole trees, or stop at the core so heartwood is always hand-chopped?
