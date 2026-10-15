# Connection map

What every skill, machine, consumable, minigame and special pixel **gives**, **needs** and **answers**, so it is easy to see where the game already links up and where it does not yet.
Rules this map follows: `PixelRules.md` (a click gives currency and drops a husk; husks are material, never currency by themselves; holding a utility block gives charges).

**State legend:** **Built** = in the game now · **Designed** = agreed in a design doc, not built · **Idea** = suggested, not agreed · **?** = intent not written down yet (fill it in).

Keep this map current: when a system gets a new link (or loses one), change its row here. The pruning test (end of this page) uses it.

## 1. The big picture

```
            click (currency + husk)
                   |
   skills ---------+--------- machines / consumables
 (what spawns,     |          (turn husks into XP, charges,
  hazards, perks,  |           storage, a little currency)
  unlocks for      |
  other skills)    v
             Pure pixels -> shop (machines, consumables, skill packs)
```

- **Clicking** is the staple source of currency.
- **Skills** decide what spawns, bring their hazard, and unlock blocks / tools for *other* skills.
- **Machines and consumables** turn husks into something useful; they answer the game's pressures (despawn, space, hazards, one skill at a time, the rotation).
- **Vendors** buy skill currency for Pure pixels; the **shop** sells machines, consumables and skill packs for Pure pixels.

## 2. Skills

| Skill | Core blocks | Hazard | Gives others | Needs from others | Machines / tools that belong to it | State |
|---|---|---|---|---|---|---|
| **Standard** | the six basic pixels (+ Glass, Luminescent) | - | Pure pixels (the shop currency) | - | the shop | Built |
| **Woodcutting** | Wood, Leaf, Twig, Sap, Bark, Acorn, Heartwood, Pinecone, Moss, Pesticide; Great Oak | Termite Hive | wood husks (Chipper XP, Kiln Charcoal, crafting); hives -> Focus potions | Whetstone (Mining, Designed); Pesticide charges (Farming, Designed) | Wood Chipper (Built), Pesticide Sprayer (Designed), Lumberjack (Built) | Built; tree rework Designed (`WoodcuttingProgression.md`) |
| **Mining** | Rock (filler), Copper, Tin, Iron, Silver, Gold, Obsidian, Bedrock | Gas Pocket | Whetstone -> Woodcutting (Designed); metals -> Electrician? (Idea) | ? | Prospector (Built) | Built (perks mostly text) |
| **Farming** | Dirt (filler), Seed, Water, Fertilizer, Pollen, Wheat, Weevil, Golden Fruit | Pest | Pesticide charges -> Woodcutting (Designed); Saplings -> better trees (Idea) | water / sunlight: Solar daylight speeds growth (Built); Fishing water? (Idea) | Sprinkler (Built), Seeds (Built), Farmer visitor (Built) | Built |
| **Foraging** | Weeds (filler), Berry, Mushroom, Herb, Truffle, Treasure | Mold | ? | ? | ? | Built (perks text only) |
| **Fishing** | Water (filler), Fish, Seaweed, Shell, Pearl, Bait, Bottle, Whale | Jellyfish | ? (water / driftwood -> Farming? Idea) | ? | Bait Bucket (Built), Weak Spots minigame (Built) | Built |
| **Electrician** | Electric, Wire, Battery, Capacitor, Resistor, Transistor, Fuse, Bulb | - | power for machines (Built: Charge Booster, Lightning Rod, device links); the conveyor belt at level 5 (Built), wider belts at 10 / 20 (Built); a chainsaw for Woodcutting (Idea); Recycler speed (Designed) | metals from Mining? (Idea) | Charge Booster, Lightning Rod, Conveyor Belt (Built); the factory machines below belong here (?) | Built |
| **Offline Progression** | the assigned pack's blocks | - | earnings while away | - | - | Built |

**Gaps:** Foraging gives nothing to other skills and needs nothing yet; Fishing has no outgoing link; Mining has no incoming link.

## 3. Machines and consumables

### Placed devices and consumables (the shop / vendors)

| Item | Takes in | Gives out | Pressure it answers | Skill home | State |
|---|---|---|---|---|---|
| Vacuum Device | loose old pixels | currency today; husks to a Recycler / storage under the pixel rules | despawn | ? | Built (needs a new job, `PixelRules.md`) |
| Fan | - | moves old pixels | space | ? | Built |
| Sorter (ring) | collected pixels | streams them out of a pipe | space | ? | Built |
| Magnet | loose old pixels | holds them in a heap | despawn, space | ? | Built |
| Magnetized Sorter (crafted) | old pixels | chosen types out of three pipes | space | Crafting | Built |
| Quantum Organizer | bound types | stores them in its box | despawn, space | ? | Built |
| Storage Crate | old pixels | stores them, click to take out | despawn | ? | Built |
| Funnel | falling old pixels | a queue down a tube to a belt (with backpressure) | space, despawn | Electrician? | Built |
| Wood Chipper | wood husks | Woodcutting XP + Wood Chips | despawn | Woodcutting | Built |
| Sprinkler | - | waters sprouts | - | Farming | Built |
| Charge Booster | Electric pixels | a super charge | - | Electrician | Built |
| Lightning Rod | old pixels around it | turns them into Electric pixels | - | Electrician | Built |
| Bait Bucket | Fish / Water husks | more fish spawn | - | Fishing | Built |
| Seeds | - | grow a pixel type | - | Farming | Built |
| Potions (per pixel type) | Glass + pixels (crafting) | that type's effect for a while | the rotation | Crafting | Built |
| Combo Fuel | - | keeps the combo | - | Combo | Built |
| Ghost Bait / Pet Treat | - | ghosts sooner / hyper pets | - | Ghost minigame / Pets | Built |
| Gauntlet | the six Prime Cubes | the Prime Cubelette | - | collectibles (Wizard) | Built |
| Lumberjack's Focus | Termite Hives (Designed) | trees without hives for a while | hazards | Woodcutting | Built as a dev widget; hive recipe Designed |
| **Recycler** | husks | currency at 15% of the click value | despawn | Electrician (speed) | Designed (`PixelRules.md`) |
| **Pesticide Sprayer** | Pesticide husks | clears hives automatically | hazards | Woodcutting / Farming | Designed |

### Factory machines (Dev Hub tester features today)

| Machine | Takes in | Gives out | Pressure | Skill home | State |
|---|---|---|---|---|---|
| Conveyor Belt (+ corner joints) | husks | moves them | space | Electrician (level 5) | Built (a real item at Electrician 5) |
| Splitter | a belt's husks | shares them over three outputs | space | Electrician? | Built (tester) |
| Conveyor Elevator | a belt's husks | lifts them to a higher belt | space | Electrician? | Built (tester) |
| Conveyor Connector / Hose Adapter | husks in its box / from a Funnel | onto the next belt or machine | space | Electrician? | Built (tester) |
| Belt Sorter | a belt's husks + one filter block | that type off the side | space, the rotation | Electrician? | Built (tester) |
| Kiln | husks | Charcoal husks | despawn | ? | Built (tester) |
| Trash Can | loose husks | deletes them | space | ? | Built (tester) |
| Tiny Robot | loose husks | puts them on a belt | space | Electrician (planned consumable) | Built (tester) |

### Hands (first person)

| Action | Takes in | Gives out | Pressure | Later replaced / kept by | State |
|---|---|---|---|---|---|
| Break husks by hand | loose husks | skill XP (a fraction of the click's XP) | despawn | machines (Chipper, Recycler) | Built |
| Pick up and carry husks | loose husks | husks fed to a machine by hand | despawn, space | belts, Funnel, connectors, Tiny Robot | Built |
| **Repair a broken machine** | husks from other skills (Mining metals, Electrician Wire) | the machine runs again | machine breakdowns | stays a hands-only job | Designed (`PixelRules.md`) |

Machines can **break** from the wrong input (main cause) or gentle wear (optional); a broken machine stops and keeps its contents until it is repaired in first person (`PixelRules.md`, "Machine breakdowns and repair").

**Gaps:** Charcoal (Kiln) and Wood Chips (Chipper) have no use yet; most factory machines have no decided skill home (Electrician is the natural one); several shop devices have no skill home (fine for early-game tools, but say so).

## 4. Pressures and what answers them

| Pressure | Answered by |
|---|---|
| **Despawn** (husks vanish) | first person by hand (the first answer), Wood Chipper, Storage Crate, Magnet, Quantum Organizer, Funnel, Recycler (Designed), Kiln |
| **Machine breakdowns** (wrong input, wear) | sorting before the machine (Belt Sorter, Splitter), repairs in first person (Designed) |
| **Space** (the floor circle is small) | belts, Splitter, Elevator, Connector, Fan, Sorter, Trash Can, free cam; a bigger floor as a reward (Idea, the dev toggle exists) |
| **Hazards** | right-click clearing, Pesticide charges / Sprayer (Designed), Focus potions, Infestation dial (open), sap bait (Designed), Belt Sorter |
| **One skill at a time** | auto clicker + machines working while you focus elsewhere; Offline Progression |
| **The rotation** (cannot choose the next block) | Block Hold, Shuffle, potions, lookahead perks (Designed) |

## 5. Minigames and visitors

| Minigame / visitor | Gives | Connects to | State |
|---|---|---|---|
| All skill-affecting minigames | currency / XP; the minigame XP multiplier boosts skill XP (`PixelSkillsMinigames`) | skills | Built |
| Evil Tree | Magic Wood + Woodcutting XP | Woodcutting (Magic Wood has **no use yet**) | Built |
| Weak Spots | catching fish | Fishing | Built |
| Stardust | stardust while the Meteor pixel is unlocked | ? | Built |
| Black Hole, Ghost, Meteor | currency, ghost / meteor events | ? | Built |
| Snake, Sorting Race, Cube Dash, Red Light Green Light, Cube Evasion, Cube Blink, Breakout, Bomb Defusal, Ultra Holes | currency (RGB-pack minigames) | ? | Built |
| Cubie | guide, shop, tutorials | the whole early game | Built |
| Tinkerer, Wizard, Entrepreneur, Farmer | trades; Wizard -> Gauntlet; Farmer -> Dragon Seeds | collectibles, Farming | Built |
| Prospector, Lumberjack | buy skill currency for Pure pixels; sell skill tools | Mining, Woodcutting | Built |
| Egg, Bean (cats) | rare visits | ? | Built |

**Gaps:** most minigames only pay currency; they could each feed one skill (e.g. Bomb Defusal -> Mining, Snake -> Foraging) the way the Evil Tree feeds Woodcutting.

## 6. Special pixels

| Pixel | What it does | Connects to | State |
|---|---|---|---|
| Glass | potion and Pixel Bank material | Crafting, Bank | Built |
| Luminescent | glowing trail | ? | Built |
| Vacuum | sucks up old pixels and pays them | husks (needs a new job under the pixel rules) | Built |
| Obsidian | a tough Mining ore | Mining | Built |
| Singularity | gravity well holding old pixels | despawn, space | Built |
| Ghost | drifting old pixel, Ghost minigame | Ghost minigame | Built |
| Meteor | flying old pixel, Stardust | Stardust minigame | Built |
| Solar | daylight that speeds growth; hovering old pixel pays a trickle | Farming (growth); the trickle needs a new job | Built |
| Electric | Overcharge, device power | Electrician, auto clicker | Built |
| Mirror | repeats the last payout | the rotation | Built |
| Fire | ring of flames | ? (Kiln fuel? Idea) | Built |
| Treasure | random-hit chest, pours out pixels | ? | Built |
| Nuclear | ages nearby old pixels fast | a hazard-like pressure | Built |

## 7. Collectibles and meta systems

| System | Connects to | State |
|---|---|---|
| Dragon Cubes (wish), God Cubes, Prime Cubes + Gauntlet | Collection Log, gallery, Wizard | Built |
| Pets | pixel types (a pet per type), Pet Treats | Built |
| Crafting | potions, attuned devices, Magnetized Sorter | Built |
| Pixel Bank | husk storage for later use (e.g. Electric husks before Electrician), one shared capacity; stored husks carry no payout (Designed) | Built; storage-only rework Designed |
| Achievements, Collection Log | everything (goals) | Built |
| Combo meter | clicking | Built |
| Time Stop | clicking, devices | Built |

## 8. Open gaps to decide (from the tables above)

1. **Foraging:** what does it give other skills, and what does it need? (e.g. Herbs -> potions, Mushrooms -> Farming fertilizer)
2. **Fishing:** an outgoing link (e.g. Water -> Farming, Pearls -> crafting).
3. **Mining:** an incoming link (e.g. Electrician powers a drill; Charcoal fuels a smelter). Its metal husks as machine repair material (Designed) give it an outgoing link.
4. **Electrician as the factory skill:** confirm it is the home of belts, splitters, elevators, connectors, sorters and the Tiny Robot.
5. **Dead-end outputs:** Charcoal, Wood Chips and Magic Wood need a use.
6. **Minigames:** give each one a skill it feeds.
7. **Pixel rules rework:** the Vacuum and the Solar trickle need their new jobs (the Pixel Bank's is decided: husk storage, no payout).
8. **Shop relics:** most shop pixels and devices are left over from early development. Decide which consumables stay shop-only from the start and which move to unlocks later (and from which skill).
9. **Breakdowns:** which machines can break, which inputs break each one, whether wear exists, and which husks repair which machines.

## The pruning test

A skill, block, machine or feature earns its place by passing at least **two** of these:

1. **Unique action:** it asks the player to do or decide something new.
2. **Connected:** it gives to, or needs from, at least two other things on this map.
3. **Pressure:** it creates or answers one of the pressures in section 4.

Anything that fails two of the three: merge it into something else, or cut it.
