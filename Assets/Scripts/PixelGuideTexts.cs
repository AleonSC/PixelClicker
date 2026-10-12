using System.Text;

/// <summary>
/// The explanations shown in the Pixel Guide's orange '!' boxes: what each pixel type does, in terms of the game's real rules. Numbers that come from a
/// pixel's own data (payout, clicks) are read from its tier; the rest describe the mechanics in words. A new pixel type needs a case in <c>Mechanics</c>.
/// </summary>
public static class PixelGuideTexts
{
    public static string Describe(PixelClicker.PixelType type, PixelClicker.PixelTier t)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(Mechanics(type, t));

        // What every pixel shares, in short.
        if (t.clicksToCollect > 1 && !t.randomClicks && !PixelClicker.IsHazard(type))
            sb.Append("\n\n<b>Tough:</b> it takes ").Append(t.clicksToCollect).Append(" clicks; only the last click pays.");
        sb.Append("\n\n<b>Old pixel:</b> ").Append(OldPixel(type));
        return sb.ToString();
    }

    private static string OldPixel(PixelClicker.PixelType type)
    {
        switch (type)
        {
            case PixelClicker.PixelType.Glass: return "shatters when it lands on the ground and is gone (set it down gently with Pixel Grabbing and it survives).";
            case PixelClicker.PixelType.Ghost: return "bounces slowly a few times, then floats up and drifts off the screen.";
            case PixelClicker.PixelType.Meteor: return "streaks away across the screen in a fiery trail instead of falling.";
            case PixelClicker.PixelType.Solar: return "hovers above the floor, lasts 1.6 times longer than other old pixels and pays a small trickle.";
            case PixelClicker.PixelType.Singularity: return "becomes a gravity well that holds up to 20 other old pixels for 5 minutes.";
            case PixelClicker.PixelType.Water: return "splashes the first time it lands (no payout) and speeds up growing sprouts nearby.";
            case PixelClicker.PixelType.Fire: return "burns away slowly over about 6 seconds without paying.";
            case PixelClicker.PixelType.Nuclear: return "radiates: other old pixels near it age 40 times faster and vanish.";
            default: return "falls beside the cube and lies there for a while before it despawns.";
        }
    }

    private static string Mechanics(PixelClicker.PixelType type, PixelClicker.PixelTier t)
    {
        switch (type)
        {
            case PixelClicker.PixelType.White:
            case PixelClicker.PixelType.Gray:
            case PixelClicker.PixelType.Black:
                return "A plain pixel: one click, one payout. Nothing special happens.";
            case PixelClicker.PixelType.Red:
            case PixelClicker.PixelType.Green:
            case PixelClicker.PixelType.Blue:
                return "One of the RGB pixels: one click, one payout. In Bomb Defusal the right wire to cut is the colour of the RGB pixel you hold the most of.";
            case PixelClicker.PixelType.Glass:
                return "A see-through pixel. It is also the material for potions and the Pixel Bank's storage upgrade.";
            case PixelClicker.PixelType.Vacuum:
                return "Clicking it sucks up every old pixel lying around: they swirl into it and their stored value is paid out at once. The old Vacuum pixel then keeps those pixels floating inside its glass body. While a Vacuum potion is running only every 10th click vacuums.";
            case PixelClicker.PixelType.Obsidian:
                return "Black volcanic glass that cracks more with every hit; the click sound rises in pitch as it breaks. Breaking it throws dust and glass shards.";
            case PixelClicker.PixelType.Luminescent:
                return "A glowing pixel. Its old pixel stays bright as it falls and leaves a fading trail of light.";
            case PixelClicker.PixelType.Singularity:
                return "Rare. Its old pixel becomes a gravity well: for 5 minutes it drags up to 20 nearby old pixels toward it, keeps them from despawning, and captures meteors that fly close. Wells packed close together starve each other, so space them out. Feeding one to a black hole counts toward the Event Horizon achievement.";
            case PixelClicker.PixelType.Ghost:
                return "A gray, gelatinous blob. Clicking it is just a payout; its old pixel is the one that drifts away.";
            case PixelClicker.PixelType.Meteor:
                return "A cratered rock that needs 3 hits; the lava cracks glow brighter with each one. Its old pixel shoots off across the screen, and every one that does has a chance of knocking a star (Stardust) down beside the cube.";
            case PixelClicker.PixelType.Solar:
                return "A blazing sun. Every Solar click raises <b>Daylight</b> (it fades slowly): the higher it is, the faster seeds and Seed pets grow and the more hovering Solar pixels pay. Old Solar pixels hover and pay 8% of their value every 4 seconds; five touching ones never despawn and glow with a yellow aura.";
            case PixelClicker.PixelType.Electric:
                return "Pays a random whole amount (" + t.payoutMin + "-" + t.payoutMax + "). Clicking it triggers <b>Overcharge</b>: a burst of auto clicks (needs the Auto Clicker bought). Old Electric pixels link to each other and to placed devices with arcs, power timed devices (their countdown pauses) and never age while linked.";
            case PixelClicker.PixelType.Mirror:
                return "A chrome pixel. Clicking it pays its own value and then repeats the last payout of the previous different pixel you clicked.";
            case PixelClicker.PixelType.Seed:
                return "A shell you dig open. Every hit throws dirt; the click that cracks it pays and plants three sprouts. Each sprout grows a random unlocked pixel over about 6 seconds (rarer types are likelier; never Vacuum or Singularity) and then pops off as an old pixel.";
            case PixelClicker.PixelType.Water:
                return "A wobbly blue jelly. Its old pixel splashes the first time it lands, paying nothing, and instantly speeds up every growing sprout and planted Seed pet within about 5 pixel-widths (each only accepts water every 0.75 seconds).";
            case PixelClicker.PixelType.Fire:
                return "A flaming pixel. Clicking it blasts a ring of flames outward from the cube.";
            case PixelClicker.PixelType.Copper:
            case PixelClicker.PixelType.Tin:
            case PixelClicker.PixelType.Iron:
            case PixelClicker.PixelType.Lead:
            case PixelClicker.PixelType.Zinc:
            case PixelClicker.PixelType.Nickel:
                return "A tough ore that chips with every hit (cracks spread, rock and metal flecks fly). There is an 8% chance a fresh ore is a <b>rich vein</b>: it glitters and pays 5 times as much. Ores only come up while Mining is the skill pack in play.";
            case PixelClicker.PixelType.Treasure:
                return "A chest that needs a random 1-20 hits, decided when it appears. Opening it pays the pixel's value x the hits x (1 + 0.15 x hits) and pours out a pile of random unlocked pixels at once: 2 plus one per hit, up to 24.";
            case PixelClicker.PixelType.Nuclear:
                return "A radioactive pixel with a toxic glow. Its old pixel ages every other old pixel within about 4 pixel-widths 40 times faster, so they disappear without paying - clear space before it lands.";
            case PixelClicker.PixelType.Pest:
                return "A HAZARD. Never worth clicking: hit it " + t.clicksToCollect + " times and a swarm of bugs pours out and eats the sprouts you have growing (the more bugs on a sprout, the faster it goes), and Farming XP drops 10% for a minute (it stacks with each one you set off in that minute). Left alone it flies off by itself after a few seconds. The auto clicker never touches it. They get a bit more common as your farming level rises.";
            case PixelClicker.PixelType.GasPocket:
                return "A HAZARD. Never worth clicking: hit it " + t.clicksToCollect + " times and the gas explodes, blowing away the old pixels around the cube, and Mining XP drops 10% for a minute (it stacks with each one you set off in that minute). Left alone it drifts away by itself after a few seconds. The auto clicker never touches it. They get a bit more common as your mining level rises.";
            case PixelClicker.PixelType.Wood:
                return "A basic block of wood. It only comes up if you switch it on in Toggles > Pixels.";
            case PixelClicker.PixelType.Rock:
                return "Stone: Mining's filler block. One click, almost no pay. At low mining level it is most of what appears; as you level up it gets rarer and the ores show up more.";
            case PixelClicker.PixelType.Dirt:
                return "Farming's filler block: it takes a few digs (shrinking each time, with digging noises) and pays almost nothing. At low farming level it is most of what appears; as you level up it gets rarer and the other farm blocks show up more. Old Dirt pixels lying near a growing sprout also make it grow faster.";
            case PixelClicker.PixelType.Fertilizer:
                return "Farming block. Clicking it gives every growing sprout and Seed pet on the field an instant jump in growth.";
            case PixelClicker.PixelType.Pollen:
                return "Farming block that floats. An old Pollen pixel close to a sprout when it pops doubles the pay of the pixel that sprout grows.";
            case PixelClicker.PixelType.Wheat:
                return "Farming block. It pays more for every sprout currently growing on the field (up to a cap).";
            case PixelClicker.PixelType.Weevil:
                return "Farming pest. While a Weevil sits on the cube it eats one growing sprout every few seconds until you click it away.";
            case PixelClicker.PixelType.GoldenFruit:
                return "Farming prize. A growing sprout has a 3% chance to produce one instead of a normal pixel; it pays a lot.";
            case PixelClicker.PixelType.Wire:
                return "A copper wire, and a <b>conductor</b>: old Wire pixels carry the arcs that link devices and other conductors.";
            case PixelClicker.PixelType.Battery:
                return "A conductor that takes " + t.clicksToCollect + " clicks and also sets off an Overcharge when you click it. Linked old Batteries power timed devices.";
            case PixelClicker.PixelType.Capacitor:
                return "Every other click you make stores one charge in it (up to 40). When you click the Capacitor it pays its value x (1 + 0.15 per stored charge), then the charge resets.";
            case PixelClicker.PixelType.Resistor:
                return "A conductor that takes " + t.clicksToCollect + " clicks. Old Resistors link into the electric network like Electric pixels.";
            case PixelClicker.PixelType.Transistor:
                return "Clicking it makes the NEXT pixel you collect pay double.";
            case PixelClicker.PixelType.Fuse:
                return "Pays 3 times as much if you collect it within 3 seconds of it appearing, and only 0.3 times as much if you are slower.";
            case PixelClicker.PixelType.Bulb:
                return "Pays 10% more for every old conductor pixel (Electric, Wire, Battery, Resistor) lying around, up to 30 of them.";
            case PixelClicker.PixelType.DragonCube1:
            case PixelClicker.PixelType.DragonCube2:
            case PixelClicker.PixelType.DragonCube3:
            case PixelClicker.PixelType.DragonCube4:
            case PixelClicker.PixelType.DragonCube5:
            case PixelClicker.PixelType.DragonCube6:
            case PixelClicker.PixelType.DragonCube7:
                return "A very rare drop (roughly 1 in 70,000 pixels). It pays 1 of itself and is kept in your backpack. Collect all seven stars and you can Summon the Dragon and make a wish.";
            default:
                return "A pixel with no special effects.";
        }
    }
}
