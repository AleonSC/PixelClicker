# Pixel rules: value, body and use (design, not built yet)

Status: **agreed rule; step 1 built (husks stop paying currency, 2026-10-15).** Switch: `PixelClicker.husksPayCurrency` (off = the rules; on = the old behaviour, for comparing). Still to build: the Recycler, charges from holding utility blocks, the drained look. This is a game-wide rule; every skill, machine and block should follow it. Woodcutting's design (`WoodcuttingProgression.md`) is the first place it is applied.

## The problem

Today one block can pay twice: the click gives currency, and the husk it drops can be turned into currency again (the Vacuum and Vacuum Device pay out its stored value, the Pixel Bank stores it, a hovering Solar husk pays a trickle). It is not clear which of the two is the "real" reward, and it does not make sense in the game's world: you should get the money **or** the thing, not both.

## The rule

**A click splits a block into its VALUE and its BODY.**

| What you get | What it is | What it is for |
|---|---|---|
| **Currency** (from the click) | the block's value | spending in the shop, selling to vendors |
| **Husk** (the body) | the drained, physical leftover - material | machines, crafting, charges, XP; **never currency by itself** (only the Recycler machine turns husks into currency, at a loss) |
| **Charges** (from holding a utility block) | the block's use | Pesticide, Whetstone and other tools |

### Why it makes sense in the world: extraction

The clicker does not simply "break" blocks: **a click extracts the block's essence** (its colour, its value) into your counter, and what drops out is the **drained husk**. That is why you get both: the currency *is* the essence, and the husk is what is left once the essence is gone. The husk is still a real thing - wood is still wood - so it is useful as material, but it has no value of its own to cash in.

Idea for the look: husks could be slightly duller than the block on the cube (a little desaturated, a faint "drained" shimmer as they drop), so the player can see that the value went into the counter and the husk is what is left.

### The three ways to take a block

| Action | You get | You give up |
|---|---|---|
| **Left click** (break) | its currency + its husk | nothing - this is the staple |
| **Hold** (Block Hold) on a utility block | its **charges** (e.g. Pesticide charges) + its husk | its currency |
| **Right click** on a hazard (clear) | the hazard as currency + its husk | the time the right clicks take |

- Clicking is the reliable way to earn currency, at every stage of the game; going back to farm currency is a real choice.
- Holding a plain block keeps today's Block Hold behaviour (pull it out of the rotation, the stake); holding a **utility** block converts it to charges.

### The rule, word for word

> **Husks are never currency by themselves. Only a machine can turn them into something - and the Recycler is the machine that turns them into currency, at a loss.**

> **Trading husks is not husks becoming currency (clarified 2026-10-24).** A husk never PAYS anything by itself, but a vendor can BUY husks
> (a delivery job) - that is a trade, like selling skill goods. The rule is about husks having no built-in payout, not about who may buy them.
> Balance, not the rule, limits it: jobs are capped (one active job per vendor, a few per hour) and cost real effort.

This keeps the world consistent (a machine is processing husks, like every other machine) while clicking stays the real way to earn currency.

## What husks are for

Machines exist to turn husks into something useful; despawning hurts because unused **material** is lost, not money.

| Machine / system | Turns husks into |
|---|---|
| **Recycler** (built: `PixelRecycler.cs`) | currency, at **50%** of each husk's value |
| **Husk Crush** (built 2026-10-17: `PixelHuskCrush.cs`, free from the start) | right-click a floor husk: half a click's XP to its own skill (bonuses for air / last-gasp / streak crushes, capped at a click's XP), never currency |
| Wood Chipper | skill XP (and Wood Chips) |
| Kiln | Charcoal (fuel / material) |
| Storage Crate | stored material for later |
| Pesticide Sprayer (planned) | used up to clear hives automatically |
| Crafting (planned) | recipe ingredients |
| Conveyor belts, sorters, hoses | moving husks to the right machine |

## Husks wait for you (Early game feel 1, agreed 2026-10-18 in the Design Discussion chat, built 2026-10-18)

Clicks and husks must not compete: husks wait long enough that the player clicks a while, builds a pile, then clears it (docs/GameDesign.md 'Early game feel: click, pile, crush').
- **Lifetime 35 s** (`PixelClicker.oldPixelLifetime`, was 15 s). Standard's **Lingering pixels** perk still adds 5 s per level above 1 (level 2 = 40 s, level 7 = 65 s).
- **Floor limit 120** (`PixelClicker.baseOldPixelCap`, was 50): at ~3.5 blocks a second 50 husks filled the floor in ~14 s and the oldest were culled, so a longer lifetime alone did nothing. When the floor is full the OLDEST husk still goes first. Standard's **Roomy floor** perk now adds 10 per level from level 4 (was 5 on the 50 floor; level 7 = 160).
- **Big piles stay cheap:** resting husks sleep (see 'Husks settle into small heaps' below) and vanished husks are reused (`PixelHuskPool`). Dev Hub > Widgets > 'Husk pile' > Fill floor drops husks until the limit is reached, for profiling a full floor.
- Fixed-second husk timings (Husk Crush's last-gasp 2 s, Clean Break's 2 s back, a gentle set-down's +12 s) were left as they are; they matter less against a 35 s life.

## Husks settle into small heaps (agreed 2026-10-17 in the Design Discussion chat, built 2026-10-17)

A husk lands, maybe rolls or tips once, and stays put - no more bouncing about.
- **Where they land:** the burst (`PixelBurstPop.TryBurstVelocity`) still throws each husk out to a ring round the cube, so the floor fills with **a ring of small heaps**, never one tower right under the cube. A Recycler parked under the cube must not catch everything: aiming (Aimed Clicks) and the Recycler's suction are what route husks (docs/GameDesign.md 'Husk routing rework').
- **How they feel:** the default husk material is barely bouncy and a little grippier (`PixelClicker` bounciness 0.08, friction 0.65; were 0.3 / 0.5). Types whose look sets its own physics (`look.customPhysics`: Water's jelly, Sap, Leaf / Herb / Seaweed drifting...) keep their own values.
- **Heaps are free:** a resting husk falls asleep quickly (`huskSleepThreshold` 0.03, Rigidbody.sleepThreshold; Unity's default 0.005) and stays asleep - the script gravity (`ScaledGravity`) no longer pushes sleeping husks, which used to keep every husk awake for ever. Anything that touches one wakes it as usual (a grab, a crush taking a husk out from under it, Recycler suction, Magnet, Fan, belts, another husk landing on it).
- **Throws still tumble:** a thrown husk gets its spin and brief extra grip (`PixelGrab`, 'Tumbling when thrown'); its throw grip (`thrownGrip` 0.85) stays above the resting friction 0.65 so corners catch and tip it over.

## Currency, husks and PRODUCTS (agreed 2026-10-19, not built)

**Currency comes from the cube, husks come from clicks, products come from machines.** A machine chain never makes a new husk (a husk
always has a block and its currency behind it). What machines make is a PRODUCT: it has a body (rides belts / tubes / chutes, sits in
crates and the Workbench, falls like a husk) but it is not a pixel type - no block, no click currency. Its value is in USE: crafting
ingredients, vendor orders, upgrades, cosmetics; vendors may BUY products (the exit to currency). Chains stay short (2 steps, sometimes
3): ore husk -> ingot -> tool; leaf / moss / seaweed husks -> cloth -> pet outfit. Wood Chips and Charcoal (pixel types today, no use yet)
are really products and should move over.

**Discovery: products unlock new blocks (agreed 2026-10-21, not built).** A product never turns into a husk, but making enough of a product
DISCOVERS a new pixel type: from then on the cube spawns it like any block (its own currency, husks, Value upgrades). Example: the Kiln burns
wood husks into Charcoal PRODUCTS; make enough of them and the Charcoal PIXEL is discovered (a Firemaking block), which then drops real
Charcoal husks. This is how the MAKING skills (Smithing, Firemaking, ...) get their blocks, while gathering skills keep theirs from shop
packs - the factory becomes the gateway to new skills. Rules: discovery is ONE-TIME (no feeding a machine to keep a block); NO LOOPS (a
discovered block's husks process into something new or higher, never back into the product that discovered it); watch the currency count
(group the Inventory by skill before there are dozens).

## Delivery jobs (agreed 2026-10-24, not built)
Vendors post DELIVERY JOBS for specific husks ('30 Bark + 10 Sap husks within 10 minutes'). The VENDOR DROPS the DELIVERY CRATE on a FIXED
SPOT (decided 2026-10-24: so players are forced to route) - each vendor has their own DOCK at the edge of the floor, always in the same
place, so a line built to Big Tim's dock keeps paying off on every later job. The crate has an In coupling and an open top, a big label ('Bark 18 / 30') and the job's timer; it takes only the requested types (others bounce off),
one section per type for mixed jobs. Full = it seals and the vendor hauls it away (a small moment). Pay: pure currency + rapport (`PixelRapport`,
the biggest rapport source) + sometimes a reward item. Out of time = no penalty: the job expires, the crate empties back out, partial rapport.
Jobs scale: early ones are small (~15 husks, carried or thrown in by hand before belts exist), later ones big or mixed and only practical with belts, filters and
sorters (Cubie's trust Tier 1) - the reason to build a factory, and husks fading adds pressure. They replace the to-do's 'Vendor orders':
the same crates take PRODUCTS later (the ~3x rung of the value ladder) once products exist.

## Crafting uses husks (agreed 2026-10-18, not built)

Currency buys access (shop); husks build things: crafting consumes HUSKS stored in the Workbench (one placeable bench, keeps its stock when moved), not pixel currency. Design: docs/GameDesign.md section 8 'Crafting uses husks: the Workbench'.

## The Recycler (built 2026-10-15, `PixelRecycler.cs`)

**Reworked 2026-10-17 (docs/GameDesign.md section 6 'Husk routing rework'):** the Recycler is CURRENCY ONLY - each husk pays 50% of a click's value times the same-type streak (cap 80%, never above 95% of a click), and NO XP; XP from husks comes from Husk Crush and the skill machines. A modest suction ring round the rim draws nearby loose husks in (Operation: Suction), several Recyclers can stand at once at a rising price (x2 each), and whoever captures a husk first owns it (`PixelHuskHolder`). Edge-click aiming is now Standard's tree node 'Aimed Clicks' (Standard 3).

Built version: an open bin (DeviceKind.Recycler, first on the shop's Utilities card, 75 White + 50 Green and rising; Cubie gives one on his first visit). Every husk that lands in it (or is drawn in) is recycled half a second later for **50%** of its value as currency of its type, raised by a same-colour streak. (The first version filled up and was emptied with the red button - still there for anything left inside.) The notes below are the original plan; the live numbers are on the component.

- **What it does:** takes husks in and pays out currency of that block's type at **15% of the block's click value** per husk (start value, to be tuned). A Wood husk from a Wood block that pays 3 gives 0.45 Wood.
- **Takes time and setup:** it processes one husk every so often (proposed: one every 1.5 seconds), so throughput is a real limit; husks are fed by belt, Funnel, hose or by hand (through a coupling on its socket, like the Kiln).
- **Upgrades through other skills:** Electrician power raises its speed; a later tier raises its rate (e.g. to 25%). Another cross-skill link.
- **It competes with the other uses:** the same husk can go to the Recycler (a little currency), the Wood Chipper (XP), crafting (material) or the Pesticide Sprayer (charges). Choosing where husks go is the builder's puzzle.
- **A home for the old systems:** the Vacuum can collect husks and hand them to a Recycler instead of paying currency out directly. (The Pixel Bank is storage, not a cash-in: see the table below.)
- **Never better than clicking:** whatever its upgrades, the rate stays well below 100%, so a block never pays twice in full.

## Hands first, machines later (first person)

First person is **not** a second clicker mode. You can still click the cube high above you, but the mode exists for **handling husks by hand**: breaking husks for XP, picking them up and carrying them, and feeding machines yourself (e.g. carrying wood to the Wood Chipper).

The progression:

1. **The cube** makes currency and husks.
2. **Hands (first person)**: you save husks from despawning, break them for XP and carry them to machines. Free but slow.
3. **Machines** take the carrying and processing over (belts, Funnel, hoses, sorters, Chipper, Kiln, Recycler). Faster, but they cost something to buy and place.
4. **Machines break** (see below), and your hands are needed again. That sends you back to fix the layout properly.

Rules that keep it balanced:

- **Hand-breaking a husk never beats clicking.** It gives a fraction of the XP the click on that block gave, so clicking stays the main source and camping in first person is never the best play.
- **Hands are free but slow, machines are fast but cost something.** Otherwise machines lose their point, or hands do.
- **Its first moment:** before the player owns any machine, first person is the only way to save husks from despawning. Cubie can point at it the first time husks start to vanish.
- **Jobs only hands can do keep it alive later:** repairing machines (below), grabbing a rare husk before a sorter takes it, carrying a Hive to the Pesticide Sprayer, clearing a jam.

## Machine breakdowns and repair (design)

Some machines break, and the player goes into first person to repair them.

**Wrong input (the main cause).** A machine that gets something it should not take (e.g. a Gas Pocket husk in the Kiln) bangs, smokes and stops.
- This makes sorting matter: a Belt Sorter or Junction Box protects the machine, not just tidies.
- The player caused it and can fix it for good with a better layout; it teaches what each machine takes.
- Each machine lists what it accepts (shown in its tooltip / the Database), so a breakdown is never a surprise rule.

**Wear over time (optional, gentle).** If used at all:
- a worn machine **slows down**, it does not stop;
- **nothing wears while offline**;
- upgrades / perks (Electrician) reduce or remove wear.

**What a breakdown does.** The machine **stops and keeps its contents**; nothing is lost. It holds things up instead of destroying them, so being busy elsewhere never costs material.

**Seeing it.** Smoke, sparks and a warning icon over the machine, visible from the normal clicker view, so the player chooses *when* to go down and fix it instead of checking all the time.

**Repairing.**
- Quick: a few hits or a short hold on the machine in first person (a small bonus for doing it well is optional). The real cost is the walk, not a long minigame.
- **Repairs use husks from other skills**: metal husks from Mining, Wire from Electrician. This gives Mining a link the connection map says it is missing, and gives those husks another use.

Open questions: which machines can break, which inputs break each one, whether wear exists at all, and which husks repair which machines.

## Systems that needed a new job under this rule

Step 1 (built): with `husksPayCurrency` off none of these pay a husk's value any more. Consumed husks give **husk XP** instead (`PixelClicker.GiveHuskXp` -> `PixelSkills.AddHuskXp`: a share of a manual click's XP, `huskXpShare` 0 = 0.5; pure husks train Standard, a skill's husks train that skill).

| System | Today | Direction |
|---|---|---|
| Vacuum pixel / Vacuum Device | paid the stored value of every husk it sucked up | **built:** gives husk XP per husk sucked up (no '+N' popup any more). Later it may hand husks to a Recycler / crate instead |
| Pixel Bank (hose) | stores husks with their payout, which comes back when they are spat out | **decided:** husk storage for their EFFECT, never to cash in: keep husks you cannot use yet (e.g. Electric husks before Electrician) and spit them out when a machine wants them. Stored husks carry no payout; capacity is one shared total (already how `PixelBank.IsFull` works) |
| Solar husk | hovered and paid a small currency trickle | **built:** each trickle tick adds a little daylight instead (`PixelDaylight.hoverDaylight`, 0 = 0.02 per tick), so a hovering Solar husk keeps the day going |
| `PixelClicker.ReleaseOldPixel(body, credit: true)` | re-added the husk's reward (black hole option, Ultra Holes right hole) | **built:** the credit only pays with `husksPayCurrency` on; Ultra Holes gives husk XP for a correctly sorted husk; the black hole already gives its minigame XP |
| First-person axe | paid 1.5x a block's value for chopping a wood husk | **built:** Woodcutting XP only (swinging the axe at a wood block on the cube still adds its tool bonus: that is a click) |

Not changed (open question for the developer): the minigames that use husks as play pieces (Snake, Sorting Race, Breakout) still pay currency per husk as their minigame reward. The Pixel Bank's stored payout is kept in the data but nothing pays it any more.

## Open questions

- Recycler: is 15% right, how fast does it process, what does it cost, and is there one per save or many?

- Should vendors buy only currency counts (as now), or also husks (e.g. the Lumberjack buys a pile of wood husks for a little)?
- The "drained" look for husks: worth doing, and how subtle?
- Which blocks become utility blocks with charges, besides Pesticide (and the Whetstone once it moves to Mining)?
