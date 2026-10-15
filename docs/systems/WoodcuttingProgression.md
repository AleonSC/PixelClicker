# Woodcutting progression (design, not built yet)

Status: **agreed direction, build not started.** The "Open questions" section lists what is still a proposal.
Today's Woodcutting (blocks, perks, Termite Hive, filler family) is described in `SkillBlocks.md`; this doc is where it is going.

## Goals

- **The intro skill.** Quick to start and to understand; the first ten levels take about an hour.
- **A rhythm, not random noise.** Each click should feel random, but the session should have a shape: a beginning, a build-up and a payoff that come round again and again.
- **Hazards you can plan around.** A Termite Hive should be a known risk, not bad luck.
- **Unlocks you can see.** Every early level adds one new thing; later levels make your trees visibly richer.
- **Better through other skills.** Woodcutting stays simple on its own; other skills supply its upgrades (the Whetstone comes from Mining, and so on).

## The core idea: random trees, designed shapes

While Woodcutting is in play the cube alternates between two modes:

1. **Filler rotation (random).** The cube rolls randomly among the basic blocks: **Twig, Leaf and plain Wood** (later Moss and Sap too). Quick, cheap, the "waiting" part.
2. **A tree arrives (random timing).** After a random stretch of filler (see "When a tree arrives") a tree of a random **species** starts.
3. **The tree plays out in a fixed shape (designed).** Each species is an ordered list of **slots**: the blocks come in that order, with a few controlled random parts (see "Tree shapes"). The **Termite Hive** can only appear in a fixed slot, and an infested tree is **warned** from its first block.
4. **"Timber!"** When the last slot is chopped the tree falls: a felling show, sound and a small reward (see "Timber reward"). The cube goes back to the filler rotation.

So: **when** a tree comes is random, **what is inside** it is designed.

Why it works:

- **Keeps the clicker's surprise.** You never know when the next tree comes or which species it is.
- **An authored rhythm.** Inside a tree every click has a place: canopy, trunk, core, payoff. Each species can be tuned like a little level.
- **Hazards you can plan around.** The Hive only ever sits in the tree's last slot, and an infested tree shows it from the start (see "Infested trees"), so the player can prepare instead of being ambushed.
- **The Next Up window becomes a tool.** Once you know a species, you know what is coming; the window confirms it.
- **Easy to expand.** A species is data (a slot list). A new tree is a new list entry, not new code - and the same system fits other skills (see "The same system for other skills").

### How the existing mechanics fit in

| Mechanic | With trees |
|---|---|
| **Next Up window** | Shows the next block: from the tree's slot list while a tree is up, otherwise the pre-rolled filler. |
| **Block Hold** | Holding pulls the live block out; letting go puts it back as the *next* block (it already works like this through `nextInLine`). Inside a tree the held block slots back in before the rest of the tree, so the tree's order is kept. |
| **Shuffle bonus** | Works on filler blocks during the filler rotation, as now. Inside a tree it is off (a tree's order is the point). |
| **Hazard defuse / Pesticide** | The Hive in an infested tree's last slot: right clicks (10) or a held Pesticide clear it, as now. |
| **Great Oak** | A rare species (see "Species"); acorns lying on the floor raise its chance when a tree arrives. |
| **Auto clicker** | Chops filler and trees like a manual click. It never touches a Hive (it waits for you), as now. |
| **Focus potion** | Lumberjack's Focus: no tree is infested while it lasts. |

## When a tree arrives

- During the filler rotation each click has a growing chance to start a tree: a few filler blocks always come first (minimum 3), then the chance climbs until a tree is guaranteed (by about 10). Inspector data: `minFiller`, `maxFiller` (or a chance curve).
- Levels shorten the filler stretch a little (more trees per hour), so the share of junk falls as you level - replacing today's filler fade (75% -> 25% over 20 levels).
- The species is a weighted pick among the unlocked ones; acorns on the floor add weight to the Great Oak.

## Tree shapes

A species is an ordered **slot list**. A slot is one of:

| Slot kind | Example | Meaning |
|---|---|---|
| **Fixed** | `Leaf` | Always that block. |
| **Run** | `Wood x3-6` | That block a random number of times: this is what makes trees different sizes. The range can grow with the level. |
| **Wildcard** | `[Sap 30% / Wood]` | A small weighted pick, so details change while the shape stays. |
| **Optional** | `Acorn?` (50%) | Sometimes there, sometimes skipped. |
| **Hazard** | `(Hive)` | Only filled when the tree is infested; always the last slot before Timber. |

### Proposed species

| Species | Unlock | Shape | Character |
|---|---|---|---|
| **Oak** | 1 | `Leaf, Leaf, Wood x3-6, [Sap 30% / Wood], Acorn?, [Heartwood 20% / Wood], (Hive)` (Sap from level 2, Acorn from 3, Heartwood from 6; before that those slots are Wood) | The all-rounder; feeds the Great Oak through acorns. |
| **Pine** | 4 | `Leaf, Pinecone, Wood x4-8, Pinecone, Wood x1-2, (Hive)` | Tall: a long log run with cones. |
| **Birch** | 8 | `Leaf, Bark, Wood x2-3, Bark, (Hive)` | Short and quick: Timber after Timber. |
| **Maple** | 12 | `Leaf, Wood x2-4, Sap, Sap, Wood x1-3, [Heartwood 25% / Sap], (Hive)` | Sticky and well paid. |
| **Great Oak** | 3, rare | `Leaf, Leaf, Wood x6-10, Bark, Wood x2-4, Heartwood` | Long, guaranteed heartwood core, never infested; its blocks stall the Block Hold meter. 1% when a tree arrives, up to 25% with acorns lying on the floor. |

### Infested trees

- Chance per tree: about 25% at level 2 (no Hives before level 2), rising slowly to about 45% at level 20; 0 while the Focus potion lasts; the Great Oak is never infested.
- **Warned from the first block:** the tree bar shows a Hive icon, the tree's blocks get a darker, bored-through bark look, and a faint termite skitter plays. The player can then:
  - **prepare:** have a Pesticide ready to hold, or plan a Block Hold;
  - **accept it:** chop through and clear the Hive with right clicks at the end;
  - **skip it** (idea): spend something (Pure pixels? a consumable?) to fell the tree early and lose the rest of it.
- The Hive keeps today's rules: 4 left clicks set the termites loose, 10 right clicks clear it safely (and collect it).

### The same system for other skills

Filler rotation + randomly arriving shapes fit the other skills without new spawn code:

| Skill | Filler | "Trees" (shapes) |
|---|---|---|
| Mining | stone, gravel | ore veins (copper, iron...), a rich vein as the special; a Gas Pocket in a fixed slot |
| Foraging | weeds | bushes (berry bush, mushroom ring, herb patch), a truffle spot as the special; Mold in a fixed slot |
| Fishing | water | spots (shallows, reef), the whale as the special; a Jellyfish in a fixed slot |
| Farming | dirt | crop rows |

## Lookahead grows with the skill

How far ahead you can see is itself a reward: early on you react, later you plan.

| Source | What you see |
|---|---|
| Level 1 | The next block (the Next Up window, as now) and, during a tree, the tree bar (`Oak 7 / 12`, with the Hive icon if infested). |
| Level 7 | **Keen eye** (perk): the next **two** blocks. |
| Level 20 | **Woodsman's eye** (perk): the tree bar shows what the tree's wildcard slots rolled (e.g. whether a heartwood is coming). |
| Skill tree: **Read the grain** | The next **three** blocks. |
| Level 30 | **Master forester** (perk): during the filler rotation a small hint shows the species of the next tree. |

- The Next Up window grows a slot per extra block (the next block on top, then the ones after it, smaller).
- Blocks bumped back by Block Hold show first, then the real next blocks, so the preview is always the true order.

## Blocks and their roles

| Block | Where | Hits | Role |
|---|---|---|---|
| Twig | filler | 1 | Basic filler. |
| Leaf | filler, tree tops | 1 | Basic filler and the start of most trees; floats down. |
| Wood | filler, tree trunks | 2 | Plain wood in the filler rotation; the log runs of every tree. |
| Moss | filler | 1 | Joins the filler later (level 2+). |
| Sap | filler (2+), oak / maple slots | 1 | Sticky old pixel, pays a bit more. |
| Pinecone | pine trees | 1 | Part of the Pine's shape. |
| Acorn | oak trees (3+) | 1 | Its old pixel on the floor raises the Great Oak chance. |
| Bark | birch, oak, Great Oak | 1 | Cheap trunk block. |
| Heartwood | cores (6+) | 4 | Rare payoff. |
| Termite Hive | last slot of an infested tree | 4 left / 10 right | The hazard. |
| **Whetstone** | - | - | **Leaves Woodcutting:** comes from Mining (see "Cross-skill upgrades"). |
| **Pesticide** | - | - | Proposed to come from Farming or Foraging (open question). |

## Level gating

| Level | What changes |
|---|---|
| 1 | Filler: **Twig, Leaf, Wood**. Trees: **Oak** only, with plain wood in its special slots. Small trees. |
| 2 | **Sap** (filler and oak slot), **Moss** in the filler; trees can be **infested**. Perk: Wood Chipper (as now). |
| 3 | Oaks drop **Acorns**; the **Great Oak** can arrive. Perk: wood pixels last longer (as now). |
| 4 | **Pine** trees. |
| 5 | Perk: Wood can spawn in any skill (as now). |
| 6 | **Heartwood** cores can roll. |
| 7 | Perk: Keen eye - see the next two blocks. |
| 8 | **Birch** trees. First skill point (see "Skill tree"). |
| 10 | Evil Tree minigame can appear (it needs the Woodcutter's Pack today; gate it at 10). |
| 12 | **Maple** trees. Lumberjack sells Lumberjack's Focus. |
| 15, 20, 25, 30 | Mastery perks (see "Perks"). |

## Special trees

- **Great Oak.** A rare species (above) with a guaranteed heartwood core and a bigger Timber reward.
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
| 20 | Woodsman's eye: the tree bar shows what the tree's wildcard slots rolled |
| 25 | Old growth: Great Oak chance +5% |
| 30 | Master forester: every tree has a heartwood core, and a hint shows the species of the next tree |

The current placeholder perks at 4 ("Sap collector") and 6 ("Heart of the forest") are replaced by the gating unlocks above.

## Skill tree

Points: 1 at level 8, then one every 4 levels (12, 16, 20...). Proposed nodes:

| Node | Effect |
|---|---|
| Axe | The first person axe (the existing, not yet built node). |
| Sharp edge | Wood takes 1 hit. |
| Tapper | Sap wildcards roll more often. |
| Acorn hoard | Acorns count double for the Great Oak chance. |
| Big timber | Log runs are 2 blocks longer (more XP per Timber). |
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

- **Tree state** (the species, the position in its slot list and what its runs / wildcards rolled, whether it is infested) is saved with the skill state so a reload does not reroll a half-chopped tree; an old save simply starts in the filler rotation.
- **Whetstone** leaves the Woodcutter's Pack: a `packDataVersion` bump removes it from saved packs (the tier stays unlocked so counts are not lost) and adds it to Mining's side.
- The Hive's per-pack shares (`hazardBaseShare` / `hazardPerLevelShare`) and the filler fade (`fillerStartShare` / `fillerEndShare`) are no longer used while tree mode is on; they stay for the other skills until they move over.

## Build plan (in steps)

1. **Tree mode (shared system).** Data per skill pack: a filler pool, the tree-arrival settings (`minFiller` / `maxFiller`) and a list of species (unlock level, weight, slot list with Fixed / Run / Wildcard / Optional / Hazard slots, infested chance). `PixelClicker.RollTier` serves the filler rotation or the current tree's next slot. Built generic so Mining (veins), Foraging (bushes) and Fishing (spots) can use it later.
2. **Tree bar + Timber + infested warning + lookahead.** The tree bar (`Oak 7 / 12`, Hive icon, near the Next Up window), the felling show, sound, Timber reward and stats; the infested look and sound; the Next Up window learns to show 1-3 blocks ahead (`PixelClicker.PeekNextTier` becomes a peek of N).
3. **Woodcutting species + gating.** Oak, Pine, Birch, Maple, Great Oak and the filler pool; remove the per-block filler / hazard shares for Woodcutting.
4. **Hook the existing mechanics in.** Block Hold keeps the tree's order; Shuffle only on filler; Great Oak becomes a species; Focus potion stops infestation.
5. **XP curve bands + XP per hit.** Then retune with the Balance report (which will need a "per tree" view).
6. **Perks and skill tree nodes.**
7. **Cross-skill moves.** Whetstone to Mining (migration); Pesticide decision.
8. **Special trees** beyond the Great Oak.

## Open questions

- Species shapes, run lengths, unlock levels and the infested chance are proposals.
- Filler stretch between trees (3-10 blocks proposed) and how much levels shorten it.
- "Skip an infested tree": worth adding, and what does it cost?
- Should Pine bring its own block types later (e.g. Pine Log, Resin) or keep sharing Wood / Leaf?
- Lookahead steps: the levels above (7 / 20 / 30 and the tree node) are a proposal.
- Pesticide: leave it in Woodcutting, or move it to Farming / Foraging?
- XP per hit vs XP per block.
- Should the auto clicker be able to fell whole trees, or stop at the core so heartwood is always hand-chopped?
