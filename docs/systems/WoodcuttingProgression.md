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

1. **A tree grows.** The game fills a *bag* with a set mix of blocks for this tree (see "Bag contents") and shuffles it.
2. **Every click draws the next block from the bag.** The order is random, so it still feels random click to click.
3. **When the bag is empty, the tree falls: "Timber!"** A short felling effect and sound, a small reward (see "Timber reward"), and the next tree's bag is filled.

Why it works:

- **Random but fair.** Every tree has the same kind of mix. No long dry streaks, no clusters of bad luck.
- **Predictable hazards.** A tree holds **at most one** Termite Hive. Once it has shown up you know the rest of the tree is safe.
- **Something to look at.** A small **tree bar** shows how much of the tree is left (e.g. `Tree 7 / 18`), so there is always a short goal in sight.
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

## Blocks and their roles

| Block | Part of the tree | Hits | Role |
|---|---|---|---|
| Leaf | canopy | 1 | Fast filler-ish block, floats down. |
| Twig / Pinecone / Moss | canopy | 1 | The filler family: quick, nearly worthless, the "speed" part of a tree. |
| Wood | trunk | 2 | The bread and butter. |
| Bark | trunk | 1 | Common, cheap trunk block. |
| Sap | trunk | 1 | Sticky old pixel, pays a bit more. |
| Acorn | canopy | 1 | Its old pixel on the floor raises the Great Oak chance. |
| Heartwood | core | 4 | Rare payoff. |
| Termite Hive | hazard | 4 left / 10 right | At most one per tree. |
| Great Oak | special tree | - | A whole special tree, not a single block (see below). |
| **Whetstone** | - | - | **Leaves Woodcutting:** comes from Mining (see "Cross-skill upgrades"). |
| **Pesticide** | - | - | Proposed to leave Woodcutting too and come from Farming or Foraging (open question). |

## Level gating

| Level | What changes |
|---|---|
| 1 | Bag holds only **Leaf, Twig (+ Pinecone, Moss) and Wood**. Small trees. |
| 2 | **Sap** and the **Termite Hive** can be in the bag. Perk: Wood Chipper (as now). |
| 3 | **Acorn** can be in the bag; **Great Oak** trees can grow. Perk: wood pixels last longer (as now). |
| 4 | Bark joins the trunk. |
| 5 | Perk: Wood can spawn in any skill (as now). |
| 6 | **Heartwood** can be in the bag (the tree core). |
| 8 | First skill point for the Woodcutting tree (see "Skill tree"). |
| 10 | Evil Tree minigame can appear (it needs the Woodcutter's Pack today; gate it at 10). |
| 12 | Lumberjack sells Lumberjack's Focus. |
| 15, 20, 25, 30 | Mastery perks (see "Perks"). |

## Bag contents

Every tree is built from a **recipe** that depends on the level. Proposed starting numbers (all of them Inspector data, tuned later with the Balance report):

| | Level 1 | Level 5 | Level 10 | Level 20 |
|---|---|---|---|---|
| Tree size (blocks) | 12 | 15 | 18 | 24 |
| Canopy (leaf, filler family, acorn from 3) | 8 | 8 | 7 | 7 |
| Trunk (wood, bark from 4, sap from 2) | 4 | 6 | 9 | 13 |
| Heartwood (from 6) | 0 | 0 | 0-1 (35%) | 1, sometimes 2 |
| Termite Hive (from 2) | 0 | 0-1 (30%) | 0-1 (40%) | 0-1 (55%) |

- The canopy share falls from about 65% to about 30%, replacing today's filler fade (75% -> 25% over 20 levels) with something you can see in the tree.
- Inside each part the exact block is still a weighted random pick (e.g. canopy = leaf / twig / pinecone / moss / acorn), so trees never look identical.
- The Hive, when a tree has one, is never the first or last block of the tree.

## Special trees

- **Great Oak tree.** Chance per tree: 1% base, up to 25% with acorns lying on the floor (the current numbers, moved from "per block" to "per tree"). A big bag (about 1.5x a normal tree), mostly trunk, a **guaranteed heartwood core**, no Hive, and its blocks stall the Block Hold meter (as the Great Oak block does today). Felling it gives a bigger Timber reward.
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
| 15 | Clean cut: Wood sometimes breaks in one hit |
| 20 | Woodsman's eye: the tree bar shows whether this tree has a Hive |
| 25 | Old growth: Great Oak chance +5% |
| 30 | Master forester: every tree has a heartwood core |

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

- **Bag state** (the tree's remaining blocks and its size) is saved with the skill state so a reload does not reroll a half-chopped tree; an old save simply starts a fresh tree.
- **Whetstone** leaves the Woodcutter's Pack: a `packDataVersion` bump removes it from saved packs (the tier stays unlocked so counts are not lost) and adds it to Mining's side.
- The Hive's per-pack shares (`hazardBaseShare` / `hazardPerLevelShare`) and the filler fade (`fillerStartShare` / `fillerEndShare`) are no longer used while the bag mode is on; they stay for the other skills.

## Build plan (in steps)

1. **Bag spawn mode (shared system).** A `SkillPack.bagMode` with level-based recipes (data, Inspector-editable); `PixelClicker.RollTier` draws from the bag when the pack in play has one. Built generic so Mining (a rock vein) or Foraging (a bush) can use it later.
2. **Tree bar + Timber.** The `Tree 7 / 18` bar (near the Next Up window), the felling show, sound, Timber reward and stats.
3. **Woodcutting recipes + gating.** The level table above; remove the per-block filler / hazard shares for Woodcutting.
4. **Hook the existing mechanics into the bag.** Shuffle swaps inside the bag; Great Oak becomes a tree; Focus potion affects new trees; Hive never first / last.
5. **XP curve bands + XP per hit.** Then retune with the Balance report (which will need a "per tree" view).
6. **Perks and skill tree nodes.**
7. **Cross-skill moves.** Whetstone to Mining (migration); Pesticide decision.
8. **Special trees** beyond the Great Oak.

## Open questions

- Tree sizes: the 12 -> 24 growth above is a proposal.
- Lookahead: Next Up shows one block ahead today. Show two or three? Or only the tree bar plus the level 20 "Woodsman's eye"?
- Hive chance per tree: the 30% -> 55% ramp above is a proposal.
- Pesticide: leave it in Woodcutting, or move it to Farming / Foraging?
- XP per hit vs XP per block.
- Should the auto clicker be able to fell whole trees, or stop at the core so heartwood is always hand-chopped?
