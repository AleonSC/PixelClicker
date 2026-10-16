# Combat (design, not built yet)

> An enemy is a tree that fights back.

Status: **idea, written up 2026-10-16. Nothing built.** **Parked for AFTER 1.0, maybe (2026-10-16)** - see `docs/GameDesign.md` section 9b. It is built on Woodcutting's **tree mode** (`PixelTreeMode.cs`, the "bag" of
blocks played out in a designed order), which the developer has tested in Unity and likes (2026-10-16; only timings and numbers left to tune), so the base is proven.
Overall game design: `docs/GameDesign.md`. Gear (weapons are gear): `GearDesign.md`.

**How to use it:** open questions first (C1, C2...), each with a suggestion. Answered ones move into the design below.

---

## Open questions

**Shape**

| # | Question | Suggestion |
|---|---|---|
| C1 | Is Combat its own skill pack (chosen in the Skills window), or does it happen alongside every skill? | Its own pack, "Combat", like the gathering skills: it matches the existing structure and keeps each skill's rotation clean. Occasional "intruders" (C9) can visit other skills later. |
| C2 | One Combat skill, or several (RuneScape's Attack / Strength / Defence)? | One skill. Splitting it triples the grind without adding new actions; the depth comes from gear and enemies instead. |
| C3 | What does "getting hit" cost, with no player health? | Like hazards today: the enemy steals a little currency, scatters your floor husks, or drains the Block Hold meter, plus a short XP penalty. It hurts; nothing dies. |
| C4 | Is there player health at all (and food to heal)? | No health bar. Food is a **buff** you eat before or during a fight (less damage, faster attacks), so Fishing / Farming still feed Combat the RuneScape way without a fail state. |
| C5 | What does the auto clicker do in combat? | It fights weak enemies on its own; elites and bosses wait for your own clicks, so "many ways to play" holds and active play stays better. |

**Content**

| # | Question | Suggestion |
|---|---|---|
| C6 | What are the enemies - fantasy monsters or everyday nuisances? | Everyday nuisances, to fit the real-world, mundane concept: pests, stray machines, rival vendors, tax inspectors - and Cubie's **debt collectors** as his bill grows (prestige). |
| C7 | What do enemies drop? | Husks of materials (scrap, bones, hides, paperwork...), combat XP, a rare drop per enemy (a trophy for the Collection Log), and cosmetics from bosses. |
| C8 | Are there bosses? | Yes, rare: a long slot list with phases (armour, then weak spots, then a frenzy of attack blocks), announced like an infested tree, with a cosmetic drop. |
| C9 | Do enemies ever turn up outside Combat? | Later, optionally: an "intruder" can wander into another skill's rotation now and then (like a visitor), so other skills get a taste of it. |
| C10 | Where do weapons come from? | Crafted from other skills' materials (Mining metals, Woodcutting handles), bought from a vendor, or dropped by bosses. |

**Fit**

| # | Question | Suggestion |
|---|---|---|
| C11 | Does it clash with the "no story, mundane grind" concept? | Not if the enemies stay ordinary (C6). Combat becomes one more job in the grind - pest control, debt collection - not a heroic quest. |
| C12 | Is combat in 1.0 or an update? | An update after 1.0, once tree mode, gear and the skill polish are done. It is a big new skill, the kind the 1.0 plan says comes later. |

---

## The idea in one paragraph

While Combat is in play the cube alternates between a **wandering rotation** (weak critters and scraps, like Woodcutting's filler) and
an **enemy** arriving. Each enemy is a designed **slot list** played out on the cube, exactly like a tree species: armour blocks,
weak-spot blocks, flesh blocks and **attack blocks**. A health bar counts it down; breaking the last block defeats it and drops loot.
Attack blocks are the enemy's turn: clear them in time or they hit you.

## How it reuses tree mode

Tree mode already has every piece combat needs (`PixelTreeMode.cs` header):

| Tree mode today | Combat version |
|---|---|
| Filler rotation (Twig, Leaf, Wood...), a tree arrives after `minFiller`..`maxFiller` blocks | Wandering rotation (critters, scraps), an enemy arrives the same way |
| A species picked by weight among the unlocked ones | An enemy picked by weight among the unlocked ones (level-gated) |
| Slot kinds: Fixed, Run, Wildcard, Optional, Hazard | Fixed = set body parts, Run = several armour plates, Wildcard = random body part, Optional = a weak spot that may or may not show, Hazard = an attack block |
| Infested trees, warned from the first block | **Elite** enemies, warned on the health bar from the first block |
| Tree bar: "Oak 3 / 9" (a `PixelStatusLines` line) | Health bar: "Rat King 3 / 9" |
| "Timber!" + bonus XP when the last block leaves | "Defeated!" + loot + bonus combat XP |
| Pick tags (first / last block) | the same tags: the first block triggers the entrance, the last the defeat |

So a new enemy is **data, not code**: one more entry in a list of slot lists, like a new tree species. The shared part should be
generalised once ("encounter mode") so Woodcutting, Combat and later Mining veins / Fishing spots all use it (the Woodcutting doc
already plans this: "The same system for other skills").

## The blocks of a fight

| Block | What it does |
|---|---|
| **Flesh** | a normal block; one hit, a little damage |
| **Armour** | a tough block (several hits, like tough pixels today); a better weapon breaks it faster |
| **Weak spot** | a rare, high-damage block; can start a Weak Spots round for a critical hit |
| **Attack** | the enemy winds up (a visible charge, like a hazard's tell); clear it in time or take a hit (C3) |
| **Finisher** | the last slot; breaking it defeats the enemy |

## Player actions (all existing mechanics)

- **Left click:** hit, as always.
- **Right-click clear:** the existing hazard clearing works on attack blocks.
- **Block Hold as a parry:** holding an attack block blocks it (no hit) and stores a counter-attack - a new use for Block Hold.
- **Weak Spots minigame:** crits on weak-spot blocks, like catching fish.
- **Consumables:** food buffs (C4), potions; a "smoke bomb" to skip an enemy.
- **Gear:** the weapon in your one gear slot (below).

## Gear and other skills

- **Weapons are gear.** With one slot ("you're a cube"), wearing a sword means not wearing the Tractor Glove or the Prime Gauntlet:
  exactly the choice the one-slot rule is for. Weapon types change the fight, not just the numbers: a hammer smashes armour, a dagger
  hits weak spots more often, a shield-like item parries for free.
- **Skills feed Combat (pillar 3):** Mining metals and Woodcutting handles -> crafted weapons (C10); Fishing / Farming -> food buffs (C4);
  Electrician -> powered weapons later.
- **Combat feeds skills:** monster husks (scrap, bones, hides) become crafting materials and machine-repair parts; trophies fill the
  Collection Log; bosses drop cosmetics (the late-game rewards in `GameDesign.md`).

## Tone

Enemies are ordinary nuisances, not fantasy monsters (C6), so combat fits the game's concept - the real-world grind with a simulated
feel. Combat is just another job: pest control, clearing out broken machines, and seeing off Cubie's debt collectors when his bill gets
silly. No heroes, no quest; the humour is how mundane it all is.

## Build order (when it is time)

1. ~~Prove tree mode in Unity (Woodcutting).~~ Done 2026-10-16: it feels good; tuning remains.
2. Generalise it into a shared "encounter mode" (data lists of slot lists, used by Woodcutting first).
3. Build the gear slot (`GearDesign.md`), so weapons have somewhere to go.
4. Combat pack: the wandering rotation, 3-4 enemies, attack blocks with the "getting hit" cost (C3), the health bar, loot.
5. Then: elites, a boss, weapons from crafting, food buffs, the Block Hold parry.

## Fits the game / could be better

- **Reuses:** tree mode (the whole encounter loop), hazards (attack blocks and their clearing), tough blocks (armour), the Weak Spots
  minigame (crits), Block Hold (parry), gear (weapons), `PixelStatusLines` (health bar), the Collection Log and crafting.
- **New pieces:** the Combat skill pack, the "getting hit" penalty, loot tables per enemy, weapons as gear items, food buffs.
- **Do first:** generalise tree mode into encounter mode before writing combat, so the two never become two copies of the same system.
