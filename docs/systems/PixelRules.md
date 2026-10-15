# Pixel rules: value, body and use (design, not built yet)

Status: **agreed rule, not built.** This is a game-wide rule; every skill, machine and block should follow it. Woodcutting's design (`WoodcuttingProgression.md`) is the first place it is applied.

## The problem

Today one block can pay twice: the click gives currency, and the old pixel it drops can be turned into currency again (the Vacuum and Vacuum Device pay out its stored value, the Pixel Bank stores it, a hovering Solar old pixel pays a trickle). It is not clear which of the two is the "real" reward, and it does not make sense in the game's world: you should get the money **or** the thing, not both.

## The rule

**A click splits a block into its VALUE and its BODY.**

| What you get | What it is | What it is for |
|---|---|---|
| **Currency** (from the click) | the block's value | spending in the shop, selling to vendors |
| **Old pixel** (the body) | the drained, physical leftover - material | machines, crafting, charges, XP; **never currency by itself** |
| **Charges** (from holding a utility block) | the block's use | Pesticide, Whetstone and other tools |

### Why it makes sense in the world: extraction

The clicker does not simply "break" blocks: **a click extracts the block's essence** (its colour, its value) into your counter, and what drops out is the **drained husk**. That is why you get both: the currency *is* the essence, and the old pixel is what is left once the essence is gone. The husk is still a real thing - wood is still wood - so it is useful as material, but it has no value of its own to cash in.

Idea for the look: old pixels could be slightly duller than the block on the cube (a little desaturated, a faint "drained" shimmer as they drop), so the player can see that the value went into the counter and the husk is what is left.

### The three ways to take a block

| Action | You get | You give up |
|---|---|---|
| **Left click** (break) | its currency + its husk (old pixel) | nothing - this is the staple |
| **Hold** (Block Hold) on a utility block | its **charges** (e.g. Pesticide charges) + its husk | its currency |
| **Right click** on a hazard (clear) | the hazard as currency + its husk | the time the right clicks take |

- Clicking is the reliable way to earn currency, at every stage of the game; going back to farm currency is a real choice.
- Holding a plain block keeps today's Block Hold behaviour (pull it out of the rotation, the stake); holding a **utility** block converts it to charges.

## What husks are for

Machines exist to turn husks into something useful; despawning hurts because unused **material** is lost, not money.

| Machine / system | Turns husks into |
|---|---|
| Wood Chipper | skill XP (and Wood Chips) |
| Kiln | Charcoal (fuel / material) |
| Storage Crate | stored material for later |
| Pesticide Sprayer (planned) | used up to clear hives automatically |
| Crafting (planned) | recipe ingredients |
| Conveyor belts, sorters, connectors | moving husks to the right machine |

## Systems that need a new job under this rule

These pay old pixels out as currency today and must change (each to be designed when it is touched):

| System | Today | Direction |
|---|---|---|
| Vacuum pixel / Vacuum Device | pays the stored value of every old pixel it sucks up | collects husks as material or XP, or hands them to machines / a crate |
| Pixel Bank (hose) | stores old pixels as banked currency | stores husks as material (a big storage), or becomes a currency-only bank fed by clicks |
| Solar old pixel | hovers and pays a small currency trickle | its trickle becomes something else (daylight, XP, or powering devices) |
| `PixelClicker.ReleaseOldPixel(body, credit: true)` | re-adds the old pixel's reward | callers should not credit currency any more |

Until each is reworked the old behaviour stays, so nothing breaks in the meantime.

## Open questions

- Should vendors buy only currency counts (as now), or also husks (e.g. the Lumberjack buys a pile of wood husks for a little)?
- The "drained" look for old pixels: worth doing, and how subtle?
- Which blocks become utility blocks with charges, besides Pesticide (and the Whetstone once it moves to Mining)?
