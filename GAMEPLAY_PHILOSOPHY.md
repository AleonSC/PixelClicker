# Pixel Clicker - Gameplay Philosophy

Claude's reading of what the game is meant to be, built from the owner's own statements in conversation (2026-10-10) and the existing design. **It is a draft for the owner to correct.** Where a statement is the owner's, it is marked *(owner)*. Where it is Claude's inference, it is marked *(inferred)*. Change anything that is wrong; this file is meant to become the authority on design questions, the way `CLAUDE.md` is for code.

---

## 1. One-sentence definition

A **sandbox** game with an **idle clicker at its core**, where the thing that sets it apart is that clicked pixels become **physical old pixels the player can manage, route and automate** - and where progression is a wide, dense web of unlocks in the style of RuneScape, mixing AFK play with simple, engaging play. *(owner)*

## 2. Pillars

### 2.1 The clicker must feel good on its own *(owner)*
- A player who only clicks the cube, or only runs the auto clicker, or leaves the game and uses offline progress, is **playing the game correctly**. Nothing may require engagement beyond that for the game to make progress.
- Click feel (animation, sound, combo, pop-out of the old pixel) is a protected quality. Features must not make the basic click slower, noisier or more cluttered.
- Everything beyond the click is **optional depth**, never a gate on the basic loop. *(inferred from "if they so wish")*

### 2.2 Old pixels are the playground *(owner)*
- Every collected pixel leaves behind a physical **old pixel**. This is the game's central differentiator: idle games do not normally give you something to *do* with the output.
- Devices, tools and minigames all act on old pixels (vacuum, fan, sorter, black hole, grabbing, hose/bank, pad, time stop). Keeping them acting on the *same object* is what lets systems combine. New systems should act on old pixels, or on pixel types, rather than adding a self-contained mechanic.

### 2.3 Builds and synergies, not a power ladder *(owner)*
- The model is **Risk of Rain 2**: many items, where having everything at once is chaotic, but specific combinations are strong and satisfying.
- Consequences for design:
  - Items should have **distinct roles** and interact in ways the player can reason about, not just stack a number.
  - Choice should matter: some combinations should be better than others, and not everything should be best at once. *(inferred - needs limits such as slots, upkeep or opportunity cost; see section 8.)*
  - "Messing around" is a goal in itself. The sandbox should reward experimenting.

### 2.4 Automation is something the player builds *(owner)*
- The end goal of the engaged layer is **automated pixel farms** that are not just the auto clicker: the player assembles a chain of devices and pixel effects that produces pixels (or materials) without their input.
- Chains are built from **small single-purpose pieces the player must discover**. The owner's example: electric pixels extend the timers of consumable machines; those pair with the sorter; the sorter pairs with water pixels to feed the seed pet. *(owner; these pixels/pet are planned content, not yet in code as of this writing)*
- **Pixel types are the synergy language.** A pixel type that changes how a device behaves is preferred over a new menu or a new currency. *(inferred from the example)*
- Discovery is part of the fun, so recipes and links are **found, not listed**. The game must still **confirm** that a link worked (visual or log feedback), so players can tell success from failure. *(inferred)*

### 2.5 Density and long progression, RuneScape-style *(owner)*
- Favourite game: RuneScape. The model is a **dense** game with lots of progression and a **mixture of AFK and simple engaged activity**.
- A large amount of content is therefore *intended*. Size is not a defect; **legibility** is the thing to protect (see 6).
- Different activities can have their own progression and their own rewards. *(inferred; no skill-style levels exist yet)*

### 2.6 Pixels are the identity *(owner)*
- The large number of pixel types - each with its own colour and "vibe" - is **core identity** and is to be kept and expanded.
- A pixel type should *look* and *feel* different, not just have different numbers. Each should have a reason to exist (a role in a build, a cost, or a source).

## 3. The three loops

1. **Click loop (always on):** click the cube, collect a pixel, spend on shop upgrades. Passive version: auto clicker and offline progress.
2. **Old-pixel loop (engaged, optional):** manage the old pixels - collect, route, suck up, sort, burn through with devices - to earn more, or to feed other systems.
3. **Discovery loop (long-term):** find combinations of pixels, devices and potions that unlock a new effect or an automated chain; unlock new pixel types through minigames; build farms.

Each loop must work without the one above it being used heavily. The upper loops make the lower ones more efficient and more interesting; they do not replace them.

## 4. Minigames and materials

*(All of this section is the owner's stated intent.)*

- Minigames are the **active** way to earn **materials** and to **unlock** new pixel types.
- **Two stages for a pixel type made by a minigame:**
  1. **Unlock:** do the minigame enough times (a lifetime "earned" count that never goes down). This makes the pixel visible in the shop.
  2. **Buy:** spend **held materials** from that minigame (a separate spendable count) to purchase it.
- **A pixel's price is what it is made of.** Meteor pixels cost meteor chunks, bomb parts pay for bomb-related upgrades, and so on. The shop should read as a set of recipes.
- Materials are a **byproduct of play**, meant to be obtained passively as the player engages with the minigames. The owner will later add **slower, less active ways** to get them. The active route must stay clearly faster, so the minigames stay worth playing.
- The "earned" count (progress) and "held" count (currency) must stay separate everywhere, so spending never re-locks something.

## 5. Economy principles

- **Cost matches theme.** Pay in the thing that makes sense for the thing you are buying (section 4).
- **Active beats passive, but passive is always viable.** Offline and idle income should be meaningful, never worthless, and never better than engaging.
- **Numbers live in the Inspector.** Rates, costs and goals are tuning values, not code. The owner is still balancing; do not hard-code them.
- **Shop pacing:** each new item should be reachable from the one before it, so the player always has a next step. *(inferred from how packs are gated today)*

## 6. Legibility rules (what "dense but not cluttered" means)

Density is the goal, so the rules protect it rather than cap it.

- A new player should always be able to tell **what to do next** and **what a thing is for** (hint system, shop wording, log).
- A currency or counter should say whether it **unlocks something, is spent, or both**, so the player does not have to guess.
- The player should be able to **turn things off** (the Toggles window) so a build or a quiet session is possible.
- A discovered synergy should **show itself working**.
- Controls should stay consistent: a key or click should not mean unrelated things depending on mode more than necessary. *(inferred)*

## 7. Anti-goals

*(Inferred from the above. Owner to confirm.)*

- Not a game that requires constant attention, or punishes leaving.
- Not a game where the optimum is "buy everything"; choices should matter.
- Not a collection of unrelated minigames. Each should feed pixels, materials or unlocks into the shared economy.
- Not a hidden-information game without feedback. Discoverable is good; opaque is not.
- Not a game that loses its basic click feel under added systems.

## 8. Open questions for the owner

These shape design but have no answer yet. Answer any of them and move the answer into the sections above.

1. **Limits on builds.** RoR2 builds work because of scarcity (one run, random items). What creates scarcity here: device slots, upkeep cost, per-device pixel fuel, or something else?
2. **Persistent vs timed devices.** Placed devices currently expire and offline progress covers only the auto clicker. Should farms keep running when the player is away, and if so, at what rate compared with active play?
3. **Skills.** Should activities (bank, sorter, crafting, minigames) get their own levels, RuneScape-style, or is progression pixel-and-shop only?
4. **Prestige.** Does the game want a reset layer, or is the sandbox meant to be continuous?
5. **End state.** Is there a final goal, or is it open-ended like a sandbox?
6. **Seed pet and similar.** Is a creature/farm layer (seed pet, feeding) a new category of system? If so, what does the pet produce?
7. **Material sources.** Which passive routes for materials are planned, and should they be tied to the same device chains as the farms?

## 9. How to use this document

- When proposing or building a feature, check it against sections 2, 6 and 7 first; say which pillar it serves.
- When a design question comes up that this document answers, follow it. When it does not, ask the owner and then record the answer here.
- Keep this file in sync with `CLAUDE.md`: this file says what the game is for, `CLAUDE.md` says how the code works.
