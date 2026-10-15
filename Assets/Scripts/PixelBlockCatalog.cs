// ---- System notes ----
// PixelBlockCatalog.cs
//   THE BLOCK CATALOG: one entry ("card") per pixel type holding what used to be spread over several files (steps 1-2 of 'one definition per block'):
//     - tags     what KIND of block it is (PixelTag flags: Wood, Forage, Fish, Farm, Hazard, Ore, RetiredOre, Standalone, DragonCube, RareCollectible, PrimeCube, Plant, Electrical).
//                PixelClicker.IsWoodBlock / IsHazard / IsStandalone / IsOre / IsFarm / IsForageBlock / IsFishBlock / IsRetiredOre / IsDragonCube / IsGodCube / IsInfinityStone /
//                IsPlant / IsElectricalBlock read them (through PixelBlockTags.Has).
//     - guide    the Pixel Guide / block tooltip explanation (PixelGuideTexts.Describe; a lambda so it can read the tier's own numbers, e.g. t.clicksToCollect).
//     - oldPixel what its old pixel does (PixelGuideTexts.OldPixel; null = the general text).
//     - soundAs  play another block's click / break sounds (PixelAudio; null = its own 'pixel_<name>' sounds).
//     - fxAs     use another block's break burst (PixelFarmFx.Break; null = its own).
//   ADDING A BLOCK: give it one entry here (tags + guide at least). PixelGuideTexts keeps only the few texts that need its own helpers (the Bait numbers).
//   The tags were generated from the old hand-written lists and the texts moved over word for word (checked to give the same answers).
// ---------------------------------------------------
using System;
using PixelType = PixelClicker.PixelType;
using PixelTier = PixelClicker.PixelTier;

/// <summary>One block's card: everything about a pixel type that is not its tier data (pay, clicks, colour) or its 3D look.</summary>
public sealed class PixelBlockCard
{
    public PixelType type;
    public PixelTag tags;
    public Func<PixelTier, string> guide;
    public string oldPixel;
    public PixelType? soundAs;
    public PixelType? fxAs;
}

public static class PixelBlockCatalog
{
    private static PixelBlockCard B(PixelType type, PixelTag tags, Func<PixelTier, string> guide = null, string oldPixel = null, PixelType? soundAs = null, PixelType? fxAs = null)
        => new PixelBlockCard { type = type, tags = tags, guide = guide, oldPixel = oldPixel, soundAs = soundAs, fxAs = fxAs };

    // ---- THE CATALOG: one card per pixel type, in enum order ----
    private static readonly PixelBlockCard[] Cards =
    {
        B(PixelType.White, PixelTag.None,
            guide: t => "A plain pixel: one click, one payout. Nothing special happens."),
        B(PixelType.Gray, PixelTag.None,
            guide: t => "A plain pixel: one click, one payout. Nothing special happens."),
        B(PixelType.Black, PixelTag.None,
            guide: t => "A plain pixel: one click, one payout. Nothing special happens."),
        B(PixelType.Red, PixelTag.None,
            guide: t => "One of the RGB pixels: one click, one payout. In Bomb Defusal the right wire to cut is the colour of the RGB pixel you hold the most of."),
        B(PixelType.Green, PixelTag.None,
            guide: t => "One of the RGB pixels: one click, one payout. In Bomb Defusal the right wire to cut is the colour of the RGB pixel you hold the most of."),
        B(PixelType.Blue, PixelTag.None,
            guide: t => "One of the RGB pixels: one click, one payout. In Bomb Defusal the right wire to cut is the colour of the RGB pixel you hold the most of."),
        B(PixelType.Glass, PixelTag.None,
            guide: t => "A see-through pixel. It is also the material for potions and the Pixel Bank's storage upgrade.",
            oldPixel: "shatters when it lands on the ground and is gone (set it down gently with Pixel Grabbing and it survives)."),
        B(PixelType.Vacuum, PixelTag.None,
            guide: t => "Clicking it sucks up every old pixel lying around: they swirl into it and their stored value is paid out at once. The old Vacuum pixel then keeps those pixels floating inside its glass body. While a Vacuum potion is running only every 10th click vacuums."),
        B(PixelType.Obsidian, PixelTag.None,
            guide: t => "A Mining ore: black volcanic glass that takes " + t.clicksToCollect + " clicks and cracks more with every hit, but pays a lot; the click sound rises in pitch as it breaks. Breaking it throws dust and glass shards."),
        B(PixelType.Luminescent, PixelTag.None,
            guide: t => "A glowing pixel. Its old pixel stays bright as it falls and leaves a fading trail of light."),
        B(PixelType.Singularity, PixelTag.None,
            guide: t => "Rare. Its old pixel becomes a gravity well: for 5 minutes it drags up to 20 nearby old pixels toward it, keeps them from despawning, and captures meteors that fly close. Wells packed close together starve each other, so space them out. Feeding one to a black hole counts toward the Event Horizon achievement.",
            oldPixel: "becomes a gravity well that holds up to 20 other old pixels for 5 minutes."),
        B(PixelType.Ghost, PixelTag.None,
            guide: t => "A gray, gelatinous blob. Clicking it is just a payout; its old pixel is the one that drifts away.",
            oldPixel: "bounces slowly a few times, then floats up and drifts off the screen."),
        B(PixelType.Meteor, PixelTag.None,
            guide: t => "A cratered rock that needs 3 hits; the lava cracks glow brighter with each one. Its old pixel shoots off across the screen, and every one that does has a chance of knocking a star (Stardust) down beside the cube.",
            oldPixel: "streaks away across the screen in a fiery trail instead of falling."),
        B(PixelType.Solar, PixelTag.None,
            guide: t => "A blazing sun. Every Solar click raises <b>Daylight</b> (it fades slowly): the higher it is, the faster seeds and Seed pets grow and the more hovering Solar pixels pay. Old Solar pixels hover and pay 8% of their value every 4 seconds; five touching ones never despawn and glow with a yellow aura.",
            oldPixel: "hovers above the floor, lasts 1.6 times longer than other old pixels and pays a small trickle."),
        B(PixelType.Electric, PixelTag.None,
            guide: t => "Pays a random whole amount (" + t.payoutMin + "-" + t.payoutMax + "). Clicking it triggers <b>Overcharge</b>: a burst of auto clicks (needs the Auto Clicker bought). Old Electric pixels link to each other and to placed devices with arcs, power timed devices (their countdown pauses) and never age while linked."),
        B(PixelType.DragonCube1, PixelTag.DragonCube,
            guide: t => "A very rare drop (roughly 1 in 70,000 pixels). It pays 1 of itself and is kept in your backpack. Collect all seven stars and you can Summon the Dragon and make a wish."),
        B(PixelType.DragonCube2, PixelTag.DragonCube,
            guide: t => "A very rare drop (roughly 1 in 70,000 pixels). It pays 1 of itself and is kept in your backpack. Collect all seven stars and you can Summon the Dragon and make a wish."),
        B(PixelType.DragonCube3, PixelTag.DragonCube,
            guide: t => "A very rare drop (roughly 1 in 70,000 pixels). It pays 1 of itself and is kept in your backpack. Collect all seven stars and you can Summon the Dragon and make a wish."),
        B(PixelType.DragonCube4, PixelTag.DragonCube,
            guide: t => "A very rare drop (roughly 1 in 70,000 pixels). It pays 1 of itself and is kept in your backpack. Collect all seven stars and you can Summon the Dragon and make a wish."),
        B(PixelType.DragonCube5, PixelTag.DragonCube,
            guide: t => "A very rare drop (roughly 1 in 70,000 pixels). It pays 1 of itself and is kept in your backpack. Collect all seven stars and you can Summon the Dragon and make a wish."),
        B(PixelType.DragonCube6, PixelTag.DragonCube,
            guide: t => "A very rare drop (roughly 1 in 70,000 pixels). It pays 1 of itself and is kept in your backpack. Collect all seven stars and you can Summon the Dragon and make a wish."),
        B(PixelType.DragonCube7, PixelTag.DragonCube,
            guide: t => "A very rare drop (roughly 1 in 70,000 pixels). It pays 1 of itself and is kept in your backpack. Collect all seven stars and you can Summon the Dragon and make a wish."),
        B(PixelType.Mirror, PixelTag.None,
            guide: t => "A chrome pixel. Clicking it pays its own value and then repeats the last payout of the previous different pixel you clicked."),
        B(PixelType.Seed, PixelTag.Farm,
            guide: t => "A shell you dig open. Every hit throws dirt; the click that cracks it pays and plants three sprouts. Each sprout grows a random unlocked pixel over about 6 seconds (rarer types are likelier; never Vacuum or Singularity) and then pops off as an old pixel."),
        B(PixelType.Water, PixelTag.Farm,
            guide: t => "A wobbly blue jelly. Its old pixel splashes the first time it lands, paying nothing, and instantly speeds up every growing sprout and planted Seed pet within about 5 pixel-widths (each only accepts water every 0.75 seconds).",
            oldPixel: "splashes the first time it lands (no payout) and speeds up growing sprouts nearby."),
        B(PixelType.Fire, PixelTag.Firemaking | PixelTag.Standalone,
            guide: t => "Firemaking block: a flaming pixel. Clicking it blasts a ring of flames outward from the cube.",
            oldPixel: "burns away slowly over about 6 seconds without paying."),
        B(PixelType.Copper, PixelTag.Ore | PixelTag.Standalone,
            guide: t => "A tough ore that chips with every hit (cracks spread, rock and metal flecks fly). There is an 8% chance a fresh ore is a <b>rich vein</b>: it glitters and pays 5 times as much. Ores only come up while Mining is the skill pack in play."),
        B(PixelType.Tin, PixelTag.Ore | PixelTag.Standalone,
            guide: t => "A tough ore that chips with every hit (cracks spread, rock and metal flecks fly). There is an 8% chance a fresh ore is a <b>rich vein</b>: it glitters and pays 5 times as much. Ores only come up while Mining is the skill pack in play."),
        B(PixelType.Iron, PixelTag.Ore | PixelTag.Standalone,
            guide: t => "A tough ore that chips with every hit (cracks spread, rock and metal flecks fly). There is an 8% chance a fresh ore is a <b>rich vein</b>: it glitters and pays 5 times as much. Ores only come up while Mining is the skill pack in play."),
        B(PixelType.Lead, PixelTag.Ore | PixelTag.Standalone,
            guide: t => "A tough ore that chips with every hit (cracks spread, rock and metal flecks fly). There is an 8% chance a fresh ore is a <b>rich vein</b>: it glitters and pays 5 times as much. Ores only come up while Mining is the skill pack in play."),
        B(PixelType.Zinc, PixelTag.Ore | PixelTag.Standalone,
            guide: t => "A tough ore that chips with every hit (cracks spread, rock and metal flecks fly). There is an 8% chance a fresh ore is a <b>rich vein</b>: it glitters and pays 5 times as much. Ores only come up while Mining is the skill pack in play."),
        B(PixelType.Nickel, PixelTag.Ore | PixelTag.Standalone,
            guide: t => "A tough ore that chips with every hit (cracks spread, rock and metal flecks fly). There is an 8% chance a fresh ore is a <b>rich vein</b>: it glitters and pays 5 times as much. Ores only come up while Mining is the skill pack in play."),
        B(PixelType.Treasure, PixelTag.Standalone,
            guide: t => "A chest that needs a random 1-20 hits, decided when it appears. Opening it pays the pixel's value x the hits x (1 + 0.15 x hits) and pours out a pile of random unlocked pixels at once: 2 plus one per hit, up to 24."),
        B(PixelType.Nuclear, PixelTag.Standalone,
            guide: t => "A radioactive pixel with a toxic glow. Its old pixel ages every other old pixel within about 4 pixel-widths 40 times faster, so they disappear without paying - clear space before it lands.",
            oldPixel: "radiates: other old pixels near it age 40 times faster and vanish."),
        B(PixelType.Wood, PixelTag.Wood | PixelTag.Standalone,
            guide: t => "Woodcutting block: a solid log. A couple of chops and it pays a fair amount."),
        B(PixelType.Rock, PixelTag.Standalone,
            guide: t => "Rock: Mining's filler block. One click, almost no pay. At low mining level it is most of what appears; as you level up it gets rarer and the ores show up more."),
        B(PixelType.Dirt, PixelTag.Farm | PixelTag.Standalone,
            guide: t => "Farming's filler block: it takes a few digs (shrinking each time, with digging noises) and pays almost nothing. At low farming level it is most of what appears; as you level up it gets rarer and the other farm blocks show up more. Old Dirt pixels lying near a growing sprout also make it grow faster."),
        B(PixelType.Fertilizer, PixelTag.Farm | PixelTag.Standalone,
            guide: t => "Farming block. Clicking it gives every growing sprout and Seed pet on the field an instant jump in growth."),
        B(PixelType.Pollen, PixelTag.Farm | PixelTag.Standalone | PixelTag.Plant,
            guide: t => "Farming block that floats. An old Pollen pixel close to a sprout when it pops doubles the pay of the pixel that sprout grows."),
        B(PixelType.Wheat, PixelTag.Farm | PixelTag.Standalone | PixelTag.Plant,
            guide: t => "Farming block. It pays more for every sprout currently growing on the field (up to a cap)."),
        B(PixelType.Weevil, PixelTag.Farm | PixelTag.Standalone,
            guide: t => "Farming pest. While a Weevil sits on the cube it eats one growing sprout every few seconds until you click it away."),
        B(PixelType.GoldenFruit, PixelTag.Farm | PixelTag.Standalone | PixelTag.Plant,
            guide: t => "Farming prize. A growing sprout has a 3% chance to produce one instead of a normal pixel; it pays a lot."),
        B(PixelType.Wire, PixelTag.Standalone | PixelTag.Electrical,
            guide: t => "A copper wire, and a <b>conductor</b>: old Wire pixels carry the arcs that link devices and other conductors."),
        B(PixelType.Battery, PixelTag.Standalone | PixelTag.Electrical,
            guide: t => "A conductor that takes " + t.clicksToCollect + " clicks and also sets off an Overcharge when you click it. Linked old Batteries power timed devices."),
        B(PixelType.Capacitor, PixelTag.Standalone | PixelTag.Electrical,
            guide: t => "Every other click you make stores one charge in it (up to 40). When you click the Capacitor it pays its value x (1 + 0.15 per stored charge), then the charge resets."),
        B(PixelType.Resistor, PixelTag.Standalone | PixelTag.Electrical,
            guide: t => "A conductor that takes " + t.clicksToCollect + " clicks. Old Resistors link into the electric network like Electric pixels."),
        B(PixelType.Transistor, PixelTag.Standalone | PixelTag.Electrical,
            guide: t => "Clicking it makes the NEXT pixel you collect pay double."),
        B(PixelType.Fuse, PixelTag.Standalone | PixelTag.Electrical,
            guide: t => "Pays 3 times as much if you collect it within 3 seconds of it appearing, and only 0.3 times as much if you are slower."),
        B(PixelType.Bulb, PixelTag.Standalone | PixelTag.Electrical,
            guide: t => "Pays 10% more for every old conductor pixel (Electric, Wire, Battery, Resistor) lying around, up to 30 of them."),
        B(PixelType.Pest, PixelTag.Hazard | PixelTag.Standalone,
            guide: t => "A HAZARD. Never worth clicking: hit it " + t.clicksToCollect + " times and a swarm of bugs pours out and eats the sprouts you have growing (the more bugs on a sprout, the faster it goes), and Farming XP drops 10% for a minute (it stacks with each one you set off in that minute). Left alone it flies off by itself after a few seconds. The auto clicker never touches it. Right-click it to clear it safely: that takes as many right clicks as it takes to break, plus the hits you already made. They get a bit more common as your farming level rises."),
        B(PixelType.GasPocket, PixelTag.Hazard | PixelTag.Standalone,
            guide: t => "A HAZARD. Never worth clicking: hit it " + t.clicksToCollect + " times and the gas explodes, blowing away the old pixels around the cube, and Mining XP drops 10% for a minute (it stacks with each one you set off in that minute). Left alone it drifts away by itself after a few seconds. The auto clicker never touches it. Right-click it to clear it safely: that takes as many right clicks as it takes to break, plus the hits you already made. They get a bit more common as your mining level rises."),
        B(PixelType.Bark, PixelTag.Wood | PixelTag.Standalone,
            guide: t => "Woodcutting block: rough bark. One chop, pays a little."),
        B(PixelType.Sap, PixelTag.Wood | PixelTag.Standalone,
            guide: t => "Woodcutting block: a blob of sticky sap. It pays well for one chop, and an old Sap pixel does not bounce when it lands."),
        B(PixelType.Leaf, PixelTag.Wood | PixelTag.Standalone,
            guide: t => "Woodcutting block: a flat green leaf with a pale midrib. One chop, pays little. An old Leaf pixel drifts down slowly instead of dropping."),
        B(PixelType.Heartwood, PixelTag.Wood | PixelTag.Standalone,
            guide: t => "Woodcutting block, rare: the glowing red core of a tree. It takes " + t.clicksToCollect + " chops but pays a lot."),
        B(PixelType.Twig, PixelTag.Wood | PixelTag.Standalone,
            guide: t => "Woodcutting filler block: a tangle of thin twigs. One click, almost no pay. Pinecone and Moss are its two siblings and share its spot: at low woodcutting level it is most of what appears; as you level up it gets rarer and the other wood blocks show up more."),
        B(PixelType.HornetNest, PixelTag.Hazard | PixelTag.Standalone,
            guide: t => "The TERMITE HIVE, a HAZARD. Never worth clicking: break it (" + t.clicksToCollect + " hits) and the termites pour out, one for every old pixel lying on the floor. Each one runs to an old pixel, and a small bar over the pixel counts down while it chews - click the pixel before the bar runs out to save it, or it is eaten for good. Breaking it also drops Woodcutting XP 10% for a minute (it stacks with each one you set off in that minute). Left alone it drifts away by itself after a few seconds. The auto clicker never touches it. Hold a Pesticide with Block Hold and one left click clears it at once, safely, like the beehive. Right-click works it away instead: 10 right clicks clear it safely, it is collected as currency and drops as an old pixel (breaking it with left clicks pays nothing). They are rare (about half as common as other hazards) and get a bit more common as your woodcutting level rises."),
        B(PixelType.Berry, PixelTag.Forage | PixelTag.Standalone,
            guide: t => "Foraging block: a juicy berry. One pick, pays a little. It squelches."),
        B(PixelType.Mushroom, PixelTag.Forage | PixelTag.Standalone,
            guide: t => "Foraging block: a mushroom. It takes " + t.clicksToCollect + " picks and releases a puff of spores."),
        B(PixelType.Herb, PixelTag.Forage | PixelTag.Standalone,
            guide: t => "Foraging block that is light and leafy. One pick, pays little. An old Herb pixel drifts down slowly instead of dropping."),
        B(PixelType.Truffle, PixelTag.Forage | PixelTag.Standalone,
            guide: t => "Foraging block, rare: a buried treasure. It takes " + t.clicksToCollect + " digs but pays a lot."),
        B(PixelType.Weeds, PixelTag.Forage | PixelTag.Standalone,
            guide: t => "Foraging's filler block. One click, almost no pay. At low foraging level it is most of what appears; as you level up it gets rarer and the good finds show up more."),
        B(PixelType.Toadstool, PixelTag.Hazard | PixelTag.Standalone,
            guide: t => "A Foraging HAZARD. One left click sets it off: its spores make you dizzy and the cube jerks away from your cursor after every click for a while (and Foraging XP drops 10% for a minute). One RIGHT click clears it safely - be quick."),
        B(PixelType.Silver, PixelTag.Ore | PixelTag.Standalone,
            guide: t => "A tough ore that chips with every hit (cracks spread, rock and metal flecks fly). There is an 8% chance a fresh ore is a <b>rich vein</b>: it glitters and pays 5 times as much. Ores only come up while Mining is the skill pack in play."),
        B(PixelType.Gold, PixelTag.Ore | PixelTag.Standalone,
            guide: t => "A tough ore that chips with every hit (cracks spread, rock and metal flecks fly). There is an 8% chance a fresh ore is a <b>rich vein</b>: it glitters and pays 5 times as much. Ores only come up while Mining is the skill pack in play."),
        B(PixelType.Bedrock, PixelTag.Ore | PixelTag.Standalone,
            guide: t => "The toughest rock: it takes a random " + t.randomClicksMin + "-" + t.randomClicksMax + " clicks to break (decided when it appears), but it pays a big chunk of currency and skill XP. Like the other ores it can be a glittering rich vein (5x)."),
        B(PixelType.Fish, PixelTag.Fish | PixelTag.Standalone,
            guide: t => "Fishing block, rare: a silvery fish. Clicking it starts a Weak Spots round (the camera zooms in and time slows): hit every spot to catch it and get paid, miss one and it gets away. The auto clicker leaves fish for you. Bait pellets on the floor (and fish and water in a Bait Bucket) make fish come up more often. With the Weak Spots minigame switched off in Toggles, a fish takes a random 7-25 clicks to reel in instead."),
        B(PixelType.Seaweed, PixelTag.Fish | PixelTag.Standalone,
            guide: t => "Fishing block: see-through seaweed. One click, pays little. An old Seaweed pixel sinks slowly instead of dropping."),
        B(PixelType.Shell, PixelTag.Fish | PixelTag.Standalone,
            guide: t => "Fishing block: a glossy shell. One click, pays a fair amount."),
        B(PixelType.Pearl, PixelTag.Fish | PixelTag.Standalone,
            guide: t => "Fishing block, rare: a shining pearl. It takes " + t.clicksToCollect + " clicks but pays a lot."),
        B(PixelType.Driftwood, PixelTag.Fish | PixelTag.Standalone,
            guide: t => "Fishing's filler block. One click, almost no pay. At low fishing level it is most of what appears; as you level up it gets rarer and the good catches show up more."),
        B(PixelType.Jellyfish, PixelTag.Hazard | PixelTag.Standalone,
            guide: t => "A HAZARD, and an electric one. Never worth clicking: hit it " + t.clicksToCollect + " times and it discharges, stinging everything around it (a wide blast that wipes out the old pixels near the cube), frying every bait pellet, and Fishing XP drops 10% for a minute (it stacks with each one you set off in that minute). Left alone it drifts away by itself after a few seconds. The auto clicker never touches it. Right-click it to clear it safely: that takes as many right clicks as it takes to break, plus the hits you already made. They get a bit more common as your fishing level rises."),
        B(PixelType.Bait, PixelTag.Fish | PixelTag.Standalone),
        B(PixelType.Bottle, PixelTag.Fish | PixelTag.Standalone,
            guide: t => "Fishing SPECIAL block, rare: a message in a bottle. It takes " + t.clicksToCollect + " clicks to break. When the glass breaks a scroll flies out and unrolls into a <b>treasure map</b>, then an <b>X</b> appears on the ground somewhere in view: click it several times to dig up the buried treasure, a haul of good fishing pixels (mostly bait). <b>Right-click the bottle</b> to let it drift away instead, if you don't want to bother with the map."),
        B(PixelType.Whale, PixelTag.Fish | PixelTag.Standalone,
            guide: t => "Fishing SPECIAL block, very rare: a whale. Click it once and you hook it: it yanks the line and swims fast away from your mouse, dashing about the screen, while you chase it and click. It needs a random " + t.randomClicksMin + "-" + t.randomClicksMax + " clicks, and the more it took the more it pays (pay x clicks x (1 + clicks x 0.05)). A thin fishing line is drawn from your cursor to the whale; it is invisible within a pixel of the cursor and fades in beyond that. Take too long (about 80 seconds) and it breaks the line and swims away. The auto clicker waits while a whale is fighting."),
        B(PixelType.Mold, PixelTag.Hazard | PixelTag.Standalone,
            guide: t => "A Foraging HAZARD. One left click bursts it and the mold covers EVERY husk on the field; moldy husks die after a while unless you shake them clean (grab one and shake it). One RIGHT click clears it safely - be quick."),
        B(PixelType.WoodChips, PixelTag.Standalone),
        B(PixelType.Tinder, PixelTag.Firemaking | PixelTag.Standalone,
            guide: t => "Firemaking block: a tuft of dry grass and fluff. (Firemaking is a test skill: its mechanics are still to come.)"),
        B(PixelType.Kindling, PixelTag.Firemaking | PixelTag.Standalone,
            guide: t => "Firemaking block: a bundle of thin, dry sticks."),
        B(PixelType.Firewood, PixelTag.Firemaking | PixelTag.Standalone,
            guide: t => "Firemaking block: a split log, ready for the fire. It takes " + t.clicksToCollect + " hits."),
        B(PixelType.Flint, PixelTag.Firemaking | PixelTag.Standalone,
            guide: t => "Firemaking block: a hard striker stone that throws sparks."),
        B(PixelType.Ember, PixelTag.Firemaking | PixelTag.Standalone,
            guide: t => "Firemaking block: a glowing coal, still hot."),
        B(PixelType.Ash, PixelTag.Firemaking | PixelTag.Standalone,
            guide: t => "Firemaking's filler block: grey ash. One click, almost no pay."),
        B(PixelType.Wildfire, PixelTag.Firemaking | PixelTag.Hazard | PixelTag.Standalone,
            guide: t => "A Firemaking HAZARD. Hit it " + t.clicksToCollect + " times and the fire spreads, burning up the husks around the cube, and Firemaking XP drops 10% for a minute. Right-click it to put it out safely."),
        B(PixelType.GreatOak, PixelTag.Wood | PixelTag.Standalone,
            guide: t => "Woodcutting block, very rare: a mighty oak. It takes a random " + t.randomClicksMin + "-" + t.randomClicksMax + " clicks, but pays a huge amount. Every hit has a chance to shake a bird nest out of the tree onto the ground round the cube: click the nest before it disappears (about 9 seconds) for bonus Woodcutting XP."),
        B(PixelType.Whetstone, PixelTag.Standalone,
            guide: t => "Woodcutting block: a hard grey stone for sharpening. It takes " + t.clicksToCollect + " clicks and pays a little - but do not just break it: grab it with Block Hold while it is on the cube (a partly broken one can be held too). While you hold it every click fires off a rapid burst of extra clicks."),
        B(PixelType.Acorn, PixelTag.Standalone,
            guide: t => "Woodcutting block: an acorn. One click, pays a little. The trick is its OLD pixel: every acorn lying on the field raises the chance that a wood block turns into a Great Oak (they only have to be there - no gathering). With enough of them out the chance is far higher. Acorns despawn like other old pixels, so keep them coming."),
        B(PixelType.Pesticide, PixelTag.Standalone,
            guide: t => "Woodcutting block: a canister of pesticide. It takes " + t.clicksToCollect + " clicks and pays a little - grab it with Block Hold instead. Held for a second, it is used up (it shrinks to husk size and pops off as a husk, no currency) and gives 3 Pesticide charges. Each charge clears the next Termite Hive automatically, safely (no termites, no XP penalty). Letting go early keeps the charges you already got, but the canister is still used up."),
        B(PixelType.Charcoal, PixelTag.Firemaking | PixelTag.Standalone,
            guide: t => "Charcoal: made by the Kiln, and a Firemaking block."),
        B(PixelType.GodSlifer, PixelTag.RareCollectible,
            guide: t => "One of the three Egyptian God Cubes: Slifer the Sky Dragon, a red cube held together by storm lightning. The rarest kind of drop in the game (roughly 1 in 7 million pixels). It pays 1 of itself and you can only ever hold one."),
        B(PixelType.GodRa, PixelTag.RareCollectible,
            guide: t => "One of the three Egyptian God Cubes: Ra the Winged Dragon, a blazing golden cube wrapped in a sun's corona. The rarest kind of drop in the game (roughly 1 in 7 million pixels). It pays 1 of itself and you can only ever hold one."),
        B(PixelType.GodObelisk, PixelTag.RareCollectible,
            guide: t => "One of the three Egyptian God Cubes: Obelisk the Tormentor, a deep blue stone cube that sheds dust. The rarest kind of drop in the game (roughly 1 in 7 million pixels). It pays 1 of itself and you can only ever hold one."),
        B(PixelType.StoneSpace, PixelTag.RareCollectible | PixelTag.PrimeCube,
            guide: t => "One of the six Prime Cubes, an extremely rare drop (like the God Cubes, roughly 1 in 7 million pixels). It pays 1 of itself and you can only ever hold one of each. Hold all six and the Wizard will offer you a Gauntlet to bind them."),
        B(PixelType.StoneMind, PixelTag.RareCollectible | PixelTag.PrimeCube,
            guide: t => "One of the six Prime Cubes, an extremely rare drop (like the God Cubes, roughly 1 in 7 million pixels). It pays 1 of itself and you can only ever hold one of each. Hold all six and the Wizard will offer you a Gauntlet to bind them."),
        B(PixelType.StoneReality, PixelTag.RareCollectible | PixelTag.PrimeCube,
            guide: t => "One of the six Prime Cubes, an extremely rare drop (like the God Cubes, roughly 1 in 7 million pixels). It pays 1 of itself and you can only ever hold one of each. Hold all six and the Wizard will offer you a Gauntlet to bind them."),
        B(PixelType.StonePower, PixelTag.RareCollectible | PixelTag.PrimeCube,
            guide: t => "One of the six Prime Cubes, an extremely rare drop (like the God Cubes, roughly 1 in 7 million pixels). It pays 1 of itself and you can only ever hold one of each. Hold all six and the Wizard will offer you a Gauntlet to bind them."),
        B(PixelType.StoneTime, PixelTag.RareCollectible | PixelTag.PrimeCube,
            guide: t => "One of the six Prime Cubes, an extremely rare drop (like the God Cubes, roughly 1 in 7 million pixels). It pays 1 of itself and you can only ever hold one of each. Hold all six and the Wizard will offer you a Gauntlet to bind them."),
        B(PixelType.StoneSoul, PixelTag.RareCollectible | PixelTag.PrimeCube,
            guide: t => "One of the six Prime Cubes, an extremely rare drop (like the God Cubes, roughly 1 in 7 million pixels). It pays 1 of itself and you can only ever hold one of each. Hold all six and the Wizard will offer you a Gauntlet to bind them."),
        B(PixelType.InfinityCubelette, PixelTag.RareCollectible,
            guide: t => "Forged with the Prime Gauntlet from all six Prime Cubes. It never drops and only one can exist."),
        B(PixelType.Pinecone, PixelTag.Wood | PixelTag.Standalone,
            guide: t => "Woodcutting filler block: a scaly pinecone. One click, almost no pay. It shares the filler's spot with Twig and Moss, so it comes up a lot at low woodcutting level and less as you level up.",
            soundAs: PixelType.Twig,
            fxAs: PixelType.Twig),
        B(PixelType.Moss, PixelTag.Wood | PixelTag.Standalone,
            guide: t => "Woodcutting filler block: a soft cushion of moss. One click, almost no pay. It shares the filler's spot with Twig and Pinecone, so it comes up a lot at low woodcutting level and less as you level up.",
            soundAs: PixelType.Leaf,
            fxAs: PixelType.Leaf),
    };

    private static PixelBlockCard[] byType;

    private static void Build()
    {
        int max = 0;
        foreach (PixelBlockCard c in Cards) if ((int)c.type > max) max = (int)c.type;
        PixelBlockCard[] arr = new PixelBlockCard[max + 1];
        foreach (PixelBlockCard c in Cards) arr[(int)c.type] = c;
        byType = arr;
    }

    /// <summary>The card of a pixel type (null for a type without one).</summary>
    public static PixelBlockCard Card(PixelType type)
    {
        if (byType == null) Build();
        int i = (int)type;
        return i >= 0 && i < byType.Length ? byType[i] : null;
    }

    public static PixelTag TagsOf(PixelType type) { PixelBlockCard c = Card(type); return c != null ? c.tags : PixelTag.None; }
    public static Func<PixelTier, string> GuideText(PixelType type) => Card(type)?.guide;
    public static string OldPixelText(PixelType type) => Card(type)?.oldPixel;

    /// <summary>The block whose sounds this one plays (itself unless its card says soundAs).</summary>
    public static PixelType SoundType(PixelType type) { PixelBlockCard c = Card(type); return c != null && c.soundAs.HasValue ? c.soundAs.Value : type; }

    /// <summary>The block whose break burst this one uses (itself unless its card says fxAs).</summary>
    public static PixelType FxType(PixelType type) { PixelBlockCard c = Card(type); return c != null && c.fxAs.HasValue ? c.fxAs.Value : type; }
}
