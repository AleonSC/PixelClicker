using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// First-time tips. The first time something happens (a feature is bought, a minigame appears, a potion is drunk...)
/// a tip box (<see cref="PixelNotice"/>) explains it. Each tip shows once and is remembered between sessions
/// (PlayerPrefs "PixelClicker.Hint.&lt;id&gt;"). If several fire together they queue up and show one after another.
///
/// The texts are in the Inspector list (add or edit tips there). Other code calls <c>PixelHints.Trigger("id")</c>.
/// Added automatically by PixelClicker if the scene has none.
/// </summary>
public class PixelHints : MonoBehaviour, IPixelSaved
{
    // ---- save: this system writes its own part of the save file (PixelSaveRegistry) ----
    [System.Serializable] private class SaveState { public string[] texts, fulls; }
    string IPixelSaved.SaveId => "eventLog";
    int IPixelSaved.SaveOrder => 170;
    string IPixelSaved.Export() { ExportHistory(out string[] t, out string[] f); return PixelSaveRegistry.ToJson(new SaveState { texts = t, fulls = f }); }
    void IPixelSaved.Import(string data) { SaveState s = PixelSaveRegistry.FromJson<SaveState>(data); ImportHistory(s != null ? s.texts : null, s != null ? s.fulls : null); }

    [Serializable]
    public class Hint
    {
        [Tooltip("Unique id (also the PlayerPrefs key). Built-in ids are triggered by the game; don't rename them.")]
        public string id = "hint";

        [Tooltip("Show this tip?")]
        public bool enabled = true;

        [TextArea(2, 5)]
        [Tooltip("The message in the tip box.")]
        public string text = "";

        [Tooltip("The short line shown in the bottom-left overlay when this happens (click it, or press Enter, to reopen the full tip). Empty = no overlay line.")]
        public string shortText = "";
    }

    /// <summary>Every tip, read-only (the game database reads it).</summary>
    public IReadOnlyList<Hint> AllHints => hints;

    [Header("Settings")]
    [Tooltip("Master switch for all first-time tips.")]
    [SerializeField] private bool hintsEnabled = true;

    [Min(0f)]
    [Tooltip("Seconds to wait before a tip appears after its event (so it doesn't cover what just happened).")]
    [SerializeField] private float showDelay = 0.8f;

    [Min(0f)]
    [Tooltip("Close a tip by itself after this many seconds (0 = it stays until the player clicks Got it).")]
    [SerializeField] private float autoCloseSeconds = 0f;

    [Tooltip("Open the full tip box by itself when something happens (the overlay line always shows). Untick to only show the short line and let the player open the tip.")]
    [SerializeField] private bool autoOpenTips = true;

    [Tooltip("Ignore events during the first few seconds after the game starts (loading a save re-triggers unlocks).")]
    [SerializeField] private float startupQuietSeconds = 3f;

    [Header("Overlay (bottom-left line)")]
    [Tooltip("Show the short event line at the bottom left, above the black bar.")]
    [SerializeField] private bool showOverlay = true;

    [Min(0f)]
    [Tooltip("Seconds the line stays fully visible before fading.")]
    [SerializeField] private float overlayVisibleSeconds = 5f;

    [Min(0f)]
    [Tooltip("Seconds the line takes to fade away.")]
    [SerializeField] private float overlayFadeSeconds = 1.2f;

    [Tooltip("Font size of the overlay line.")]
    [SerializeField] private float logFontSize = 20f;

    [Tooltip("Text colour of the overlay line.")]
    [SerializeField] private Color overlayTextColor = new Color(1f, 0.95f, 0.7f, 1f);

    [Tooltip("Draw the dark box behind the log and the coloured block behind each line. Off = just the text.")]
    [SerializeField] private bool showLogBackground = false;

    [Tooltip("Background colour behind the overlay line (used when Show Log Background is on).")]
    [SerializeField] private Color overlayBackColor = new Color(0f, 0f, 0f, 0.3f);

    [Tooltip("Distance from the left edge and from the black bar (canvas units).")]
    [SerializeField] private Vector2 overlayMargin = new Vector2(24f, 16f);

    [Min(100f)]
    [Tooltip("Width of the event log box (canvas units). Its height runs from above the bottom black bar up to the top black bar.")]
    [SerializeField] private float logWidth = 420f;

    [Tooltip("Sorting order of the event log's canvas. Kept below every other window (Inventory, Log, Shop, Crafting, Bank...). Renamed so scenes saved with an older, higher value pick up the new default.")]
    [SerializeField] private int eventLogSortingOrder = 5;

    [Tooltip("Background colour of each line (used when Show Log Background is on).")]
    [SerializeField] private Color overlayRowColor = new Color(0.1f, 0.18f, 0.28f, 0.8f);

    [Tooltip("Pixels the box scrolls per mouse-wheel notch.")]
    [SerializeField] private float overlayScrollSpeed = 70f;

    [Tooltip("Lines in the upper part of the event log slowly fade out towards the top of the box.")]
    [SerializeField] private bool fadeUpperLines = true;

    [Range(0f, 1f)]
    [Tooltip("Where the fade begins, as a fraction of the box's height from the bottom (0.5 = halfway up).")]
    [SerializeField] private float fadeStartFraction = 0.5f;

    [Range(0f, 1f)]
    [Tooltip("How visible a line is at the very top of the box (0 = gone).")]
    [SerializeField] private float fadeTopAlpha = 0f;

    [Min(1)]
    [Tooltip("How many events the log keeps (saved with the save file).")]
    [SerializeField] private int historyMax = 100;


    [Header("Intro (first start of a new game)")]
    [Tooltip("On a brand-new game, open the Log and then the Inventory with a short explanation of each.")]
    [SerializeField] private bool showIntro = true;

    [Min(0f)]
    [Tooltip("Seconds to wait after the game starts (after Play) before the intro tips begin.")]
    [SerializeField] private float introDelay = 0.8f;

    [TextArea(2, 5)]
    [Tooltip("Tip shown with the Log open.")]
    [SerializeField] private string introLogMessage = "This is your Log. It tracks your achievements and your goals - what to work towards next. (Your click counts per pixel are in Settings > Stats.) Open it any time with the Log button at the bottom left.";

    [TextArea(2, 5)]
    [Tooltip("Third new-game intro tip (after the Log and Inventory ones): how you earn currency. Empty = the built-in text. The tier guide (\"Click X more...\") appears once this box is closed.")]
    [SerializeField] private string introEarnText = "Clicking the pixel is the main way to earn currency. As you play, upgrades, minigames and managing the old pixels that fall to the floor will open up other ways to earn.";

    [TextArea(2, 5)]
    [Tooltip("Fourth new-game intro tip, right after the one about clicking: the right click (Block Hold and its stake). Empty = the built-in text.")]
    [SerializeField] private string introRightClickText = "Try the right mouse button too: HOLD it on the block on the cube. It's each block's second action. On the pure pixels (black, gray, white, red, green and blue) it puts a stake on the block - the longer you hold (watch the meter by your cursor), the more it is worth when it comes round again, but the stake can also go bad and pay nothing. Skill blocks you meet later do their own thing when held.";

    [TextArea(2, 5)]
    [Tooltip("Tip shown with the Inventory open.")]
    [SerializeField] private string introInventoryText = "This is your Inventory. Currency shows how many of each pixel you hold - you spend them in the shop. Consumables holds your potions and devices. Open it any time with the Inventory button at the top left.";

    [Tooltip("Add the built-in tips that are missing from the list below.")]
    [SerializeField] private bool addDefaultHints = true;

    [Header("Tips")]
    [Tooltip("One entry per tip.")]
    [SerializeField] private List<Hint> hints = new List<Hint>();

    private static PixelHints instance;
    private readonly Queue<Hint> queue = new Queue<Hint>();
    private float waitTimer;
    private bool wasShowing;

    private const string PrefPrefix = "PixelClicker.Hint.";

    /// <summary>Distance from the left of the screen to the right edge of the event log (canvas units), for boxes placed beside it.</summary>
    public static float LogRightEdge => instance != null ? instance.overlayMargin.x + instance.logWidth + 20f : 440f;

    private static Hint H(string id, string text) => new Hint { id = id, text = text, shortText = ShortFor(id) };

    private static string ShortFor(string id)
    {
        switch (id)
        {
            case "event_log": return "This is the Event Log - click this line to read about it";
            case "woodworking": return "The woodchipper and log rack are set up!";
            case "crafting": return "You unlocked Crafting!";
            case "grab": return "You unlocked Pixel Grabbing!";
            case "timestop": return "You unlocked Time Stop! (press T)";
            case "bank": return "You unlocked the Pixel Bank!";
            case "robot": return "You hired the UFO Helper!";
            case "minigame_ghost": return "A ghost appeared!";
            case "minigame_meteor": return "A meteor appeared!";
            case "minigame_stardust": return "A star fell from the sky!";
            case "minigame_snake": return "Snake time!";
            case "minigame_sort": return "Sorting race!";
            case "minigame_breakout": return "Breakout!";
            case "minigame_dash": return "Cube Dash!";
            case "minigame_blink": return "Cube Blink!";
            case "minigame_weakspot": return "Weak Spots!";
            case "minigame_evade": return "Cube Evasion!";
            case "minigame_traffic": return "Red light, green light!";
            case "minigame_bomb": return "A bomb appeared!";
            case "minigame_pad": return "An Ultra Pad appeared!";
            case "potion": return "You drank a potion!";
            case "device_Vacuum": return "You placed a Vacuum Device!";
            case "device_Fan": return "You placed a Fan!";
            case "device_Sorter": return "You placed a Sorter!";
            case "device_Sprinkler": return "You placed a Sprinkler!";
            case "device_Magnet": return "You placed a Magnet!";
            case "device_MagnetSorter": return "You placed a Magnetized Sorter!";
            case "device_QuantumOrganizer": return "You placed a Quantum Organizer!";
            case "device_StorageCrate": return "You placed a Storage Crate!";
            case "device_SortingHopper": return "You placed a Sorting Hopper!";
            case "device_Sweeper": return "You placed a Sweeper!";
            case "device_JunctionBox": return "You placed a Junction Box!";
            case "device_PocketBiomassGenerator": return "You placed a Pocket Biomass Generator!";
            case "device_BiomassGenerator": return "You placed a Biomass Generator!";
            case "device_Funnel": return "You placed a Funnel!";
            case "husk_crush": return "Right-click a husk to crush it for XP";
            case "node_standard_aimed": return "Aimed Clicks: click near the cube's edge to aim husks";
            case "craft": return "You crafted something!";
            case "combo": return "Combo started!";
            case "ultra": return "You earned an Ultra pixel!";
            case "upgrade_interval": return "Auto Clicker upgraded: faster clicks!";
            case "upgrade_clicks": return "Auto Clicker upgraded: more clicks per tick!";
            case "upgrade_combo": return "Combo Meter upgraded!";
            case "upgrade_bank": return "Bank Storage upgraded!";
            case "upgrade_cap": return "Old Pixel Capacity upgraded!";
            case "upgrade_ultra": return "Ultra boost bought!";
            case "pixel_Glass": return "You unlocked Glass Pixels!";
            case "pixel_Vacuum": return "You unlocked Vacuum Pixels!";
            case "pixel_Obsidian": return "You unlocked Obsidian Pixels!";
            case "pixel_Luminescent": return "You unlocked Luminescent Pixels!";
            case "pixel_Singularity": return "You unlocked Singularity Pixels!";
            case "hazard": return "Hazard block!";
            case "pixel_Ghost": return "You unlocked Ghost Pixels!";
            case "pixel_Meteor": return "You unlocked Meteor Pixels!";
            case "pixel_Electric": return "You unlocked Electric Pixels!";
            case "pixel_Mirror": return "You unlocked Mirror Pixels!";
            case "pixel_Seed": return "You unlocked Seed Pixels!";
            case "pixel_Water": return "You unlocked Water Pixels!";
            case "pixel_Fire": return "You unlocked Fire Pixels!";
            case "pixel_Nuclear": return "You unlocked Nuclear Pixels!";
            case "pixel_Treasure": return "You unlocked Treasure Pixels!";
            case "pixel_Copper": return "You unlocked the Prospector's ores!";
            case "pixel_Dirt": return "You unlocked the Farmer's Pack!";
            case "pixel_Fish": return "You unlocked the Angler's Pack!";
            case "pixel_Berry": return "You unlocked the Forager's Pack!";
            case "pixel_Wood": return "You unlocked the Lumberjack's Pack!";
            case "pixel_Rock": return "You unlocked Rock Pixels!";
            case "dragon_first": return "You found a Dragon Cube!";
            case "god_first": return "You found a God Cube!";
            case "stone_first": return "You found a Prime Cube!";
            case "pixel_Red": return "You unlocked Red, Green and Blue Pixels!";
            case "bank_hose": return "Hose equipped!";
            case "bank_suck": return "Pixel stored in the Bank!";
            case "bank_spit": return "Spat out a stored pixel!";
            case "bank_full": return "The Bank is full!";
            case "timeslow": return "You unlocked Time Slow! (press S)";
            case "timestop_slow": return "Time slowed!";
            case "timestop_first": return "Time stopped!";
            case "timestop_empty": return "Time Stop ran out of energy!";
            default: return "";
        }
    }

    private static List<Hint> DefaultHints() => new List<Hint>
    {
        H("minigame_traffic", "Red light, green light! Click on GREEN to score. Never click on RED: it costs you a point. On YELLOW click slowly, only when the circle closes on the cube; any other click costs a point. The auto clicker is switched off."),
        H("minigame_evade", "Cube Evasion! The cube runs away from your mouse and the auto clicker is switched off. Corner it against an edge and click it; at the end you get a bonus for every click."),
        H("minigame_weakspot", "Weak Spots! The camera zooms in and time slows down. Glowing weak spots appear on the cube: click every one before the time runs out for bonus skill XP. Some drift across the cube (blue), some shrink and swell (green) and some flicker in and out (yellow)."),
        H("minigame_blink", "Cube Blink! The cube keeps shrinking away and teleporting, and the auto clicker is switched off. Click it whenever it shows to land clicks; at the end you get a bonus for every click."),
        H("minigame_dash", "Cube Dash! The cube is dashing around the screen and the auto clicker is switched off. Click it as many times as you can before time runs out: you get a bonus of 10x its worth for every click you landed."),
        H("minigame_breakout", "Breakout! The old pixels on screen turned into a wall of bricks (the rest shrank away) and the cube stepped aside. Move the paddle along the bottom with the mouse or the Left / Right arrows to bounce the ball into them. Every brick you break pays 10x that pixel's worth; run out of balls and the rest simply drop."),
        H("minigame_sort", "Sorting race! Some old pixels froze in a jumbled grid. Click one pixel, then another, to swap them. Get every colour together in one unbroken group before the timer runs out - finishing early pays extra."),
        H("minigame_snake", "Snake! Every old pixel froze and stopped despawning. Steer the snake with the arrow keys (or the on-screen arrows) and eat every pixel without biting yourself - you are paid 10x their worth. Run out of time or bite yourself and they just drop."),
        H("minigame_stardust", "A meteor pixel knocked a star out of the sky! Click it to collect stardust before it fades. Enough stardust unlocks something new."),
        H("event_log", "This is the Event Log. It records what happens in the game, like unlocks. Scroll it with the mouse wheel, click a line to read its full tip, and press {key:EventLog} to show or hide it. You can switch it off in Settings > Interface > Event log."),
        H("crafting", "Crafting unlocked! Open the Crafting button at the bottom right, drag two items into the boxes and press Craft. A pixel + Glass makes that pixel's potion."),
        H("grab", "Pixel Grabbing unlocked! Hold the left mouse button on an old pixel to pick it up, then let go to throw it."),
        H("timestop", "Time Stop unlocked! Press T to freeze time and T again to resume. It drains an energy meter, but you can still click the cube - the old pixels pile up and burst out when time resumes."),
        H("bank", "Pixel Bank unlocked! Right-click an old pixel to store it. Open the Bank tab on the left edge, or press B for the hose: left-click spits a pixel out, hold right-click sucks up an area."),
        H("minigame_ghost", "A ghost! Click it before it floats away to catch it and get a free potion."),
        H("minigame_meteor", "A meteor! Click it as often as you like to chip off meteor chunks."),
        H("minigame_bomb", "A bomb! Cut the wire that matches the colour you have the MOST of before time runs out. A wrong wire (or timeout) clears your old pixels."),
        H("minigame_pad", "An Ultra Pad! Drop old pixels of the colour it asks for onto it to earn Ultra pixels. Other colours cost you pixels."),
        H("potion", "Potion drunk! For a while only that pixel type spawns. Right-click the active potion line at the top to cancel it."),
        H("device_Vacuum", "Vacuum Device placed! It is grey and asleep: left-click it to switch it on. Then it pulls in old pixels and collects them again for a few seconds. Hold right-click on a placed device to remove it."),
        H("device_Fan", "Fan placed! It is grey and asleep: left-click it to switch it on. Then it blows old pixels along the floor in front of it until its timer runs out."),
        H("device_Sprinkler", "Sprinkler placed! It is grey and asleep: left-click it to switch it on. Then it waters every growing seed and Seed pet within its reach about once a second, so they grow much faster."),
        H("device_MagnetSorter", "Magnetized Sorter placed! Left-click it to switch it on. It sucks loose old pixels from the arc in front of its magnet into the box. Click an exit pipe to grab it, click again to put it down; scroll over a pipe to choose which stored pixel type it shoots out."),
        H("device_QuantumOrganizer", "Quantum Organizer placed! Left-click it to switch it on, then drop a pixel onto the pad to bind its type. Every loose old pixel of that type is dematerialized and stored in the box. Click the box to let some out again."),
        H("device_StorageCrate", "Storage Crate placed! It needs no switching on. Let go of a pixel you are holding (Pixel Grabbing) above the crate and it drops into the next free section; clicking the crate also stores loose pixels lying on it. Click a stored pixel to pick it up again. Drag the handles on its short ends to move it."),
        H("device_SortingHopper", "Sorting Hopper placed! Put it near a Recycler and it latches onto the back. Husks dropped into its bowl are held (up to 20). Right-click the hopper and press Select filter: that type goes into the Recycler, every other type drops out of its bottom spout."),
        H("device_Funnel", "Funnel placed! Its glass cone hangs under the cube and catches every husk that falls into it, then sends them one by one down the tube to the spout where you placed it (scroll or Q / E turns the spout). Placing it again moves it."),
        H("device_Sweeper", "Sweeper placed! It rolls out from its pad and carries husks that are about to fade to the nearest Storage Crate, Sorting Hopper or Recycler with room. It runs about a minute and slows to a stop - click it to wind it again."),
        H("device_JunctionBox", "Junction Box placed! Fit couplings on its sides and connect belts or hoses: whatever brings husks in makes that coupling IN, whatever takes them away makes it OUT. Husks are shared out in turn between the outputs, or sent to the one whose filter is their type - drop a husk on a coupling (or click / scroll it) to set its filter."),
        H("device_PocketBiomassGenerator", "Pocket Biomass Generator placed! Every device inside its blue ring runs on its power - their timers stop and show its blue energy bar. Keep it going by dropping Woodcutting husks into the firebox on top; when it runs dry it flashes red, then shuts down for good unless you feed it."),
        H("device_BiomassGenerator", "Biomass Generator placed! A bigger ring and much more charge: every device inside runs on its power while you feed it Woodcutting husks (the firebox on top, or its intake coupling). Empty, it flashes red and then shuts down unless you feed it."),
        H("device_Magnet", "Magnet placed! It is grey and asleep: left-click it to switch it on. Then it pulls loose old pixels in around it and holds them in a heap without collecting them."),
        H("device_Sorter", "Sorter placed! It is grey and asleep: left-click its ring or pipe to switch it on. Then click its buttons to change how hard old pixels are pushed out of the pipe."),
        H("craft", "Crafted! The new item is in your Inventory under Consumables."),
        H("combo", "Combo! Keep clicking quickly to raise the combo bonus. It breaks if you stop for a moment."),
        H("upgrade_interval", "The auto clicker now clicks faster. Each level shortens the time between its clicks."),
        H("upgrade_clicks", "Multi-Click upgraded! The auto clicker now makes more clicks every time it ticks."),
        H("upgrade_combo", "Combo Meter upgraded! Quick manual clicks build a combo; reaching a combo milestone boosts your payouts, and each level unlocks a higher milestone with a bigger bonus and a flashier meter."),
        H("upgrade_cap", "Old Pixel Capacity upgraded! More old pixels can lie around at once before the oldest ones are cleared away."),
        H("upgrade_bank", "Bank Storage upgraded! The Pixel Bank can now hold more stored pixels."),
        H("upgrade_ultra", "Ultra boost bought! That pixel type now pays more for every click. You can keep boosting it with more Ultra pixels."),
        H("pixel_Glass", "Glass Pixels unlocked! They are see-through, and you can combine one with another pixel in Crafting to make a potion."),
        H("pixel_Vacuum", "Vacuum Pixels unlocked! Clicking one sucks nearby old pixels in and collects them."),
        H("pixel_Obsidian", "Obsidian Pixels unlocked! A tougher pixel that takes more than one click to collect."),
        H("pixel_Luminescent", "Luminescent Pixels unlocked! They glow, and they are worth a lot more than the plain ones."),
        H("pixel_Singularity", "Singularity Pixels unlocked! A very rare pixel born from the black holes you fed."),
        H("pixel_Ghost", "Ghost Pixels unlocked! A rare pixel from all the ghosts you caught."),
        H("pixel_Electric", "Electric Pixels unlocked! A cube held together by crackling lightning."),
        H("pixel_Treasure", "Treasure Pixels unlocked! A chest takes a random 1 to 20 clicks to open, and you can't tell how many. The more clicks it took, the bigger the reward - and when it bursts open, a pile of other pixels pours out."),
        H("pixel_Nuclear", "Nuclear Pixels unlocked! (A testing pixel.) Its old pixel is radioactive: every old pixel lying near it despawns almost at once."),
        H("pixel_Fire", "Fire Pixels unlocked! (A testing pixel.) Click one and a ring of flames bursts out of the cube; the old pixel burns away as it falls."),
        H("pixel_Dirt", "Farmer's Pack unlocked! Eight farm blocks for the Farming skill: Dirt, Seed, Water, Fertilizer, Pollen, Wheat, Weevil and Golden Fruit. Switch on Farming mode in the Toggles window (or Train it in the Skills window) to make them replace the basic pixels. Beware of weevils: click them before they eat your sprouts!"),
        H("hazard", "A hazard block! Pests and gas pockets pay nothing: a few taps are harmless, but hit one too many times and it goes off (pests swarm out and eat your sprouts, gas explodes) and your skill XP drops for a minute. Wait it out - it leaves by itself - or RIGHT-click it to work it away safely (it takes as many right clicks as it takes to break, plus the hits you already made). The auto clicker leaves it alone."),
        H("woodworking", "Your woodchipper and log rack are set up! While you train Woodcutting they stand at the sides of the screen. Drag old blocks into them with Pixel Grabbing for bonus XP: scraps (Bark, Sap, Leaf and Twig) go in the chipper, logs (Wood and Heartwood) on the rack. The blocks are used up, but they fill your bonus XP bank, which doubles the XP of your next chops."),
        H("pixel_Fish", "Angler's Pack unlocked! The Fishing skill: very common Water, Seaweed, Shell, rare Pearl and rare Fish, plus a stinging Jellyfish to avoid. Open the Skills window to train it: its blocks replace the basic pixels while it is in play. Click a Fish to start a Weak Spots round: hit every spot to catch it, miss and it gets away. Bait blocks drop pellets that bring more fish - but a Jellyfish that goes off fries them all. Rarer finds: a Message in a Bottle (break it to unroll a treasure map, then dig up the X; right-click it to ignore it) and the mighty Whale, which fights the line and runs from your mouse while you click it down."),
        H("pixel_Berry", "Forager's Pack unlocked! The Foraging skill: Berry, Mushroom, Herb and rare Truffle blocks, plus Weeds as filler and a spreading Mold to deal with (shake infected pixels clean). Open the Skills window to train it: its blocks replace the basic pixels while it is in play. Weeds are a useless filler that gets rarer as you level."),
        H("pixel_Wood", "Lumberjack's Pack unlocked! The Woodcutting skill: wood blocks (Wood, Bark, Sap, Leaf, Heartwood) and three fillers (Twig, Pinecone, Moss) plus a Termite Hive to avoid: break it and termites go for your old pixels, so click them to save them. Open the Skills window to train it: its blocks replace the basic pixels while it is in play. The fillers are useless and get rarer as you level."),
        H("pixel_Rock", "Rock Pixels unlocked! Rock is Mining's filler block: plain and almost worthless, and most of what appears at first. As your mining level rises it gets rarer."),
        H("pixel_Copper", "Prospector's ores unlocked! Copper, Tin, Iron, Silver, Gold, Obsidian and Bedrock are tough rocks: hit them to crack them open (obsidian and bedrock take 15-20 clicks but pay a lot). A glittering one is a rich vein and pays 5x. The ores only spawn while Mining is the skill you are training: pick it in the Skills window."),
        H("pixel_Water", "Water Pixels unlocked! When an old Water pixel splashes on the ground near Seed sprouts or the Seed pet, they grow and sow much faster for a while. The water is used up either way. Stream them over your sprouts with a Sorter!"),
        H("pixel_Seed", "Seed Pixels unlocked! Old Seed pixels slowly grow bigger and worth more. Vacuum them, hose them up or click a ripe one to harvest."),
        H("pixel_Mirror", "Mirror Pixels unlocked! Clicking one also counts as a click on the last other pixel you collected, so it pays that pixel's amount again."),
        H("stone_first", "A PRIME CUBE! There are six (Space, Mind, Reality, Power, Time and Soul), each rarer than a Dragon Cube, and only ONE of each can be held. Gather all six and the Wizard may know what to do with them."),
        H("god_first", "A GOD CUBE! Slifer the Sky Dragon, Ra the Winged Dragon and Obelisk the Tormentor are the rarest things in the game: roughly one pixel in millions. Only ONE of each can ever be held, so keep it safe in your backpack."),
        H("dragon_first", "A Dragon Cube! There are seven of them, each with a different number of small cubes inside, and they turn up extremely rarely among the pixels. Gather all seven and a button will appear to summon the cube dragon and make a wish."),
        H("pixel_Meteor", "Meteor Pixels unlocked! They streak away across the screen instead of falling."),
        H("pixel_Red", "Cubie unlocked Red, Green and Blue pixels! They pay the same as the black, gray and white ones; with all six pure currencies you can buy the Auto Clicker, your first skill and more."),
        H("bank_hose", "The hose is out! Scroll the mouse wheel to pick a stored pixel type. Left-click spits one out, right-click sucks up the nearest old pixel, and holding right-click sucks up everything of that type around the nozzle."),
        H("bank_suck", "Stored in the Bank! Open the Bank tab on the left edge to see what you have. Stored pixels keep their value when you spit them back out."),
        H("bank_spit", "Spat a stored pixel back out! It is worth what it was worth when you stored it."),
        H("bank_full", "The Bank is full! Spit some pixels out, or buy Bank Storage upgrades in the shop to hold more."),
        H("node_standard_aimed", "Aimed Clicks! Click near the EDGE of the cube and its husk flies out of that side - the cursor turns into an arrow that shows where. A click in the middle still scatters it at random. Send husks into a Recycler, onto a crate, or somewhere clear to crush them."),
        H("husk_crush", "Right-click a husk to crush it for XP! The blocks that fall off the cube are husks: material, never money. Crushing one gives a little XP to its own skill - watch the ??? bar at the bottom left fill up. Husks fade after a while, so use them while they lie there."),
        H("stake_intro", "Try this: HOLD the right mouse button on a basic block (black, gray, white, red, green or blue) on the cube. A stake grows on it the longer you hold (watch the number by the meter). Let go and the block goes back in line; when it comes round again it glows green and pays more - or red and pays nothing. Most stakes pay off, but not all of them."),
        H("blockhold", "Block Hold unlocked! HOLD the right mouse button on the block on the cube to pull it out of the rotation: you get a new block while it stays held. Let go and it goes back. Holding drains the meter under the cube; if it runs dry the block is put back and the meter recharges more slowly."),
        H("robot", "UFO Helper hired! It hovers off to the side of the screen. Click him, pick a consumable and place its ghost where you want it: whenever that device runs out, he puts a new one there as long as you have one to spare."),
        H("timeslow", "Time Slow unlocked! Press {key:TimeSlow} to slow time and again to return to normal. It uses the same energy meter as Time Stop, but drains it more slowly. Meteor pixels crawl while time is slow - grab one with Pixel Grabbing and put it where you like."),
        H("timestop_slow", "Time is slowed! Everything crawls - even meteor pixels, which you can now grab. The Time Stop meter drains slowly while it lasts; press {key:TimeSlow} to go back to normal speed."),
        H("timestop_first", "Time is stopped! Everything freezes, but you can keep clicking the cube. The meter at the top drains while time is stopped and refills once it runs again; press T to resume."),
        H("timestop_empty", "The Time Stop meter ran out, so time started again. It refills quickly while time runs - wait until it is a little full before stopping time again."),
        H("ultra", "Ultra pixel earned! Your Ultra pixels are one general currency, shown at the very top of the Inventory's Currency tab."),
    };

    private bool EnsureDefaultHints()
    {
        if (!addDefaultHints) return false;
        if (hints == null) hints = new List<Hint>();
        bool changed = false;
        foreach (Hint d in DefaultHints())
        {
            Hint existing = hints.Find(h => h != null && h.id == d.id);
            if (existing != null)
            {
                if (existing.text != null && existing.text.Contains("Woodcutter's Pack")) { existing.text = existing.text.Replace("Woodcutter's Pack", "Lumberjack's Pack"); changed = true; }   // (renamed pack)
                if (string.IsNullOrEmpty(existing.shortText) && !string.IsNullOrEmpty(d.shortText)) { existing.shortText = d.shortText; changed = true; }
                continue;
            }
            hints.Add(d);
            changed = true;
        }
        return changed;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (EnsureDefaultHints()) UnityEditor.EditorUtility.SetDirty(this);
        };
    }
#endif

    private void Awake()
    {
        if (introRightClickText == "Try the right mouse button too: HOLD it on the block on the cube to pull the block out of the rotation. On the basic blocks (black, gray, white, red, green and blue) the longer you hold (watch the meter by your cursor), the more the block is worth when it comes round again - but the stake can also go bad and pay nothing.") introRightClickText = "Try the right mouse button too: HOLD it on the block on the cube. It's each block's second action. On the pure pixels (black, gray, white, red, green and blue) it puts a stake on the block - the longer you hold (watch the meter by your cursor), the more it is worth when it comes round again, but the stake can also go bad and pay nothing. Skill blocks you meet later do their own thing when held.";   // (2026-10-16: right-hold is each block's second action)
        if (instance != null && instance != this) { Destroy(this); return; }
        PixelSaveRegistry.Register(this);
        instance = this;
        EnsureDefaultHints();
        ApplyDialogueFile();
        // The first-time tips take their turn in the one tip queue (PixelTipQueue): this queue still decides WHICH tip and how it looks.
        PixelTipQueue.Register(this, PixelTipQueue.Priority.FirstTimeTip, "First-time tips", () => queue.Count > 0 && waitTimer <= 0f, ShowNextTip);
    }

    /// <summary>The user's own texts from Assets/Resources/Dialogue.txt (PixelDialogue) win over the Inspector / built-in tip texts.</summary>
    private void ApplyDialogueFile()
    {
        PixelTextFile f = PixelDialogue.File;
        foreach (Hint h in hints)
        {
            if (h == null || string.IsNullOrEmpty(h.id)) continue;
            f.Apply("Tip: " + h.id, "text", ref h.text);
            f.Apply("Tip: " + h.id, "overlay line", ref h.shortText);
        }
        f.Apply("Intro tips", "log", ref introLogMessage);
        f.Apply("Intro tips", "inventory", ref introInventoryText);
        f.Apply("Intro tips", "earn", ref introEarnText);
        f.Apply("Intro tips", "right click", ref introRightClickText);
        f.Flush();
    }

    private void OnEnable()
    {
        PixelConsumables.PotionDrunk += OnPotion;
        PixelConsumables.DevicePlaced += OnDevice;
        PixelCrafting.Crafted += OnCrafted;
        PixelCombo.ComboReached += OnCombo;
        PixelMinigame.Happened += OnMinigame;
        PixelClicker.UltraGained += OnUltra;
    }

    private void OnDisable()
    {
        PixelConsumables.PotionDrunk -= OnPotion;
        PixelConsumables.DevicePlaced -= OnDevice;
        PixelCrafting.Crafted -= OnCrafted;
        PixelCombo.ComboReached -= OnCombo;
        PixelMinigame.Happened -= OnMinigame;
        PixelClicker.UltraGained -= OnUltra;
    }

    private void OnDestroy()
    {
        PixelTipQueue.Unregister(this);
        PixelSaveRegistry.Unregister(this);
        if (instance == this) instance = null;
    }

    private void OnPotion(string name) => Trigger("potion");
    private void OnDevice(PixelConsumables.DeviceKind kind) => Trigger("device_" + kind);
    private void OnCrafted() => Trigger("craft");
    private void OnCombo(int combo) => Trigger("combo");
    private void OnUltra() => Trigger("ultra");

    private void OnMinigame(PixelMinigame game, PixelMinigame.MinigameEvent what)
    {
        if (what == PixelMinigame.MinigameEvent.Spawned && game != null)
        {
            // Some minigames (the black hole) show their own first-time tip instead of one from the list: no warning for those.
            string id = "minigame_" + game.Id;
            if (hints != null && hints.Exists(h => h != null && h.id == id)) Trigger(id);
        }
    }

    // ------------------------------------------------------------------

    // ------------------------------------------------------------------
    // Player settings (Settings screen)
    // ------------------------------------------------------------------

    private const string PrefTips = "PixelClicker.Setting.Tips";
    private const string PrefEventLog = "PixelClicker.Setting.EventLog";
    private const string PrefLogSize = "PixelClicker.Setting.EventLogSize";

    /// <summary>Show the first-time tip boxes? (The event log still records them either way.)</summary>
    public static bool TipsEnabled
    {
        get => PixelPrefs.GetInt(PrefTips, 1) != 0;
        set { PixelPrefs.SetInt(PrefTips, value ? 1 : 0); PixelPrefs.Save(); }
    }

    /// <summary>Show the event log overlay at the bottom left? (Events are still recorded and saved.)</summary>
    public static bool EventLogEnabled
    {
        get => PixelPrefs.GetInt(PrefEventLog, 1) != 0;
        set { PixelPrefs.SetInt(PrefEventLog, value ? 1 : 0); PixelPrefs.Save(); RefreshLog(); }
    }

    public static readonly string[] LogSizeNames = { "Small", "Medium", "Large" };
    private static readonly float[] LogSizeScale = { 0.8f, 1f, 1.3f };

    /// <summary>Event log text size: 0 = small, 1 = medium (the Inspector size), 2 = large.</summary>
    public static int LogSizeChoice
    {
        get => Mathf.Clamp(PixelPrefs.GetInt(PrefLogSize, 1), 0, LogSizeNames.Length - 1);
        set { PixelPrefs.SetInt(PrefLogSize, Mathf.Clamp(value, 0, LogSizeNames.Length - 1)); PixelPrefs.Save(); RefreshLog(); }
    }

    /// <summary>Rebuilds the event log lines (new text size) and hides it if it was switched off.</summary>
    public static void RefreshLog()
    {
        if (instance == null) return;
        instance.rowsDirty = true;
        if (!EventLogEnabled && instance.overlayRoot != null) instance.overlayRoot.SetActive(false);
    }

    private bool forceIntro; // "Replay tutorial": run the intro even though this is not a brand-new game

    /// <summary>Forgets the tips that are waiting to show (Escape on a tip box closes the whole pile).</summary>
    public static void ClearQueue()
    {
        if (instance != null) instance.queue.Clear();
    }

    /// <summary>Settings > Replay tutorial: forgets the shown tips and plays the intro tips (Log, Inventory) again.</summary>
    public static void ReplayTutorial()
    {
        ResetSeen();
        PixelGuideVendor.Replay(); // Cubie's visits are part of the tutorial
        if (instance == null) return;
        instance.forceIntro = true;
        instance.introChecked = false;
        instance.introWait = instance.introDelay; // start right away
        instance.queue.Clear();
    }

    /// <summary>Forgets which tips were shown (a brand-new game shows them all again).</summary>
    public static void ResetSeen()
    {
        try
        {
            if (instance != null && instance.hints != null)
                foreach (Hint h in instance.hints)
                    if (h != null && !string.IsNullOrEmpty(h.id)) PixelPrefs.DeleteKey(PrefPrefix + h.id);
            PixelPrefs.DeleteKey(PrefPrefix + "intro_log");
            PixelPrefs.DeleteKey(PrefPrefix + "shop_tutorial");
            PixelPrefs.DeleteKey(PrefPrefix + "BlackHole");
            PixelPrefs.Save();
        }
        catch (Exception) { /* no PlayerPrefs: nothing to forget */ }
    }

    /// <summary>Shows the tip with this id the first time it is triggered (ever). Unknown / disabled / already-seen ids do nothing.</summary>
    public static void Trigger(string id)
    {
        if (instance != null) instance.Enqueue(id);
    }

    /// <summary>Forgets that this one tip was shown (it shows again the next time it is triggered).</summary>
    public static void ForgetTip(string id)
    {
        try { if (!string.IsNullOrEmpty(id)) PixelPrefs.DeleteKey(PrefPrefix + id); }
        catch (Exception) { /* no PlayerPrefs */ }
    }

    /// <summary>Dev Events tab: shows this tip now, even if it was seen before (it still waits its turn behind other tips, and Settings > tips off still hides it).</summary>
    public static void ShowNow(string id) { ForgetTip(id); Trigger(id); }

    /// <summary>Has the tip / tutorial with this id been shown already? (Other tutorials use this.)</summary>
    public static bool IsTipSeen(string id) => Seen(id);

    /// <summary>Remembers that the tip / tutorial with this id was shown.</summary>
    public static void SetTipSeen(string id) => MarkSeen(id);

    private static bool Seen(string id)
    {
        try { return PixelPrefs.GetInt(PrefPrefix + id, 0) != 0; }
        catch (Exception) { return false; }
    }

    private static void MarkSeen(string id)
    {
        try { PixelPrefs.SetInt(PrefPrefix + id, 1); }
        catch (Exception) { /* no PlayerPrefs: it may show again next session */ }
    }

#if UNITY_EDITOR
    private readonly HashSet<string> warnedIds = new HashSet<string>();
#endif

    private void Enqueue(string id)
    {
        if (!hintsEnabled || string.IsNullOrEmpty(id) || Seen(id)) return; // (the tip boxes can also be switched off in Settings)
        Hint hint = hints != null ? hints.Find(h => h != null && h.id == id) : null;
#if UNITY_EDITOR
        if (hint == null && warnedIds.Add(id))
            Debug.LogWarning("PixelHints: Trigger(\"" + id + "\") has no matching hint in the list (typo, or add a Hint with this id).", this);
#endif
        if (hint == null || !hint.enabled || string.IsNullOrEmpty(hint.text)) return;
        MarkSeen(id);
        string full = hint.text;
        Announce(hint.shortText, full);
        if (id == "event_log") return; // the log's own intro is just a line: clicking it opens the Got it box
        if (!TipsEnabled) return; // the event log line is kept, the tip box is not shown
        if (!autoOpenTips) return;
        queue.Enqueue(hint);
        if (waitTimer <= 0f && !PixelNotice.IsShowing) waitTimer = showDelay;
    }

    // ------------------------------------------------------------------
    // The event log (bottom-left box)
    // ------------------------------------------------------------------

    /// <summary>Goes up whenever the event log's lines change (My Window's docked Event log tab redraws on it).</summary>
    public static int HistoryVersion { get; private set; }

    public static int HistoryCount => instance != null ? instance.history.Count : 0;

    /// <summary>One line of the event log by index (0 = oldest); false when there is none.</summary>
    public static bool TryGetHistory(int index, out string text, out string full)
    {
        text = full = "";
        if (instance == null || index < 0 || index >= instance.history.Count) return false;
        text = instance.history[index].text;
        full = instance.history[index].full ?? "";
        return true;
    }

    /// <summary>One line of the event log.</summary>
    public class Entry
    {
        public string text;
        public string full; // the tip shown when the line is clicked (may be empty)
    }

    private readonly List<Entry> history = new List<Entry>();
    private float overlayTimer;
    private float scrollOffset;       // pixels scrolled up from the newest line
    private float suppressUntil;
    private bool rowsDirty;
    private GameObject overlayRoot;
    private CanvasGroup overlayGroup;
    private RectTransform boxRect, listRect, handleRect;
    private TMP_FontAsset overlayFont;
    private float contentHeight, viewHeight;
    private readonly List<RectTransform> rowRects = new List<RectTransform>();
    private readonly List<CanvasGroup> rowGroups = new List<CanvasGroup>();

    /// <summary>
    /// Adds a short line to the event log at the bottom left ("You unlocked the Auto Clicker!") and shows the box.
    /// Clicking the line shows 'fullText' in the tip box. Ignored right after the game starts or loads.
    /// </summary>
    public static void Announce(string shortText, string fullText = null)
    {
        if (instance != null) instance.ShowOverlay(shortText, fullText);
    }

    /// <summary>Ignores announcements for a moment (loading a save re-fires unlocks).</summary>
    public static void SuppressFor(float seconds)
    {
        if (instance != null) instance.suppressUntil = Time.realtimeSinceStartup + seconds;
    }

    /// <summary>The log of the current save, for the save file.</summary>
    public static void ExportHistory(out string[] texts, out string[] fulls)
    {
        List<Entry> list = instance != null ? instance.history : new List<Entry>();
        texts = new string[list.Count];
        fulls = new string[list.Count];
        for (int i = 0; i < list.Count; i++) { texts[i] = list[i].text; fulls[i] = list[i].full ?? ""; }
    }

    /// <summary>Replaces the log with the one from a save file (no box is shown).</summary>
    public static void ImportHistory(string[] texts, string[] fulls)
    {
        if (instance == null) return;
        instance.history.Clear();
        HistoryVersion++;
        if (texts != null)
        {
            for (int i = 0; i < texts.Length; i++)
                instance.history.Add(new Entry { text = texts[i], full = fulls != null && i < fulls.Length ? fulls[i] : "" });
        }
        instance.TrimHistory();
        instance.rowsDirty = true;
        instance.scrollOffset = 0f;
        instance.overlayTimer = 0f;
        if (instance.overlayRoot != null) instance.overlayRoot.SetActive(false);
    }

    private void TrimHistory()
    {
        while (history.Count > historyMax) history.RemoveAt(0);
    }

    private void ShowOverlay(string shortText, string fullText)
    {
        if (!hintsEnabled || string.IsNullOrEmpty(shortText)) return;
        if (Time.timeSinceLevelLoad < startupQuietSeconds || Time.realtimeSinceStartup < suppressUntil) return;
        history.Add(new Entry { text = shortText, full = fullText ?? "" });
        HistoryVersion++;
        TrimHistory();
        rowsDirty = true;
        scrollOffset = 0f;
        if (showOverlay) DisplayOverlay();
    }

    /// <summary>True while the whole log is showing (the player opened it); false = only the newest entry (a new event just happened).</summary>
    private bool fullView;

    private void DisplayOverlay(bool showAll = false)
    {
        if (history.Count == 0 || !EventLogEnabled) return;
        if (PixelMyWindow.LogTabShowing) return;   // the log is on show in My Window's Event log tab
        if (overlayRoot == null) BuildOverlay();
        if (fullView != showAll) { fullView = showAll; rowsDirty = true; }
        forceFade = false;
        overlayTimer = overlayVisibleSeconds + overlayFadeSeconds;
        overlayGroup.alpha = 1f;
        overlayRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
        RebuildRows();
    }

    private void BuildOverlay()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        overlayFont = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();
        overlayRoot = PixelUIKit.CreateCanvas("Pixel Event Log", eventLogSortingOrder, new Vector2(1920f, 1080f), true);
        overlayRoot.transform.SetParent(transform, false);
        overlayGroup = overlayRoot.AddComponent<CanvasGroup>();

        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        box.transform.SetParent(overlayRoot.transform, false);
        Image img = box.GetComponent<Image>();
        img.color = showLogBackground ? overlayBackColor : Color.clear;
        img.raycastTarget = false; // empty space in the box never blocks clicks
        boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0f, 0f);
        boxRect.anchorMax = new Vector2(0f, 1f);
        boxRect.pivot = new Vector2(0f, 0.5f);
        boxRect.offsetMin = new Vector2(overlayMargin.x, bar + overlayMargin.y);
        boxRect.offsetMax = new Vector2(overlayMargin.x + logWidth, -(bar + overlayMargin.y));

        GameObject list = new GameObject("List", typeof(RectTransform));
        list.transform.SetParent(box.transform, false);
        listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(1f, 0f);
        listRect.pivot = new Vector2(0.5f, 0f);
        listRect.anchoredPosition = Vector2.zero;
        listRect.sizeDelta = Vector2.zero;

        GameObject handle = new GameObject("Scroll Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(box.transform, false);
        handle.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f);
        handle.GetComponent<Image>().raycastTarget = false;
        handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(1f, 0f);
        handleRect.anchorMax = new Vector2(1f, 0f);
        handleRect.pivot = new Vector2(1f, 0f);
        handleRect.sizeDelta = new Vector2(6f, 40f);
        handleRect.anchoredPosition = new Vector2(-3f, 0f);
    }

    private void RebuildRows()
    {
        rowsDirty = false;
        if (listRect == null) return;
        for (int i = listRect.childCount - 1; i >= 0; i--) Destroy(listRect.GetChild(i).gameObject);
        rowRects.Clear();
        rowGroups.Clear();

        float width = logWidth - 40f;
        float y = 8f;
        int oldest = fullView ? 0 : history.Count - 1; // a new event shows only the newest line; the whole list is for when the player opens the log
        for (int i = history.Count - 1; i >= oldest; i--) // newest at the bottom
        {
            Entry entry = history[i];
                        string text = entry.text;

            GameObject row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            row.transform.SetParent(listRect, false);
            rowRects.Add(row.GetComponent<RectTransform>());
            rowGroups.Add(row.GetComponent<CanvasGroup>());
            row.GetComponent<Image>().color = showLogBackground ? overlayRowColor : Color.clear; // clear still takes clicks
            RectTransform rr = row.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0f, 0f);

            TMP_Text label = PixelUIKit.CreateText(overlayFont, row.transform, "Text", text, logFontSize * LogSizeScale[LogSizeChoice],
                                                   TextAlignmentOptions.TopLeft, FontStyles.Bold, overlayTextColor);
            label.overflowMode = TextOverflowModes.Overflow;
            float h = Mathf.Ceil(label.GetPreferredValues(text, width, 0f).y) + 12f;
            RectTransform lr = label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(10f, 6f); lr.offsetMax = new Vector2(-10f, -6f);

            rr.sizeDelta = new Vector2(width, h);
            rr.anchoredPosition = new Vector2(8f, y);
            y += h + 6f;

            string full = entry.full;
            bool opens = !string.IsNullOrEmpty(full);
            Button b = PixelUIKit.MakeClickable(row, opens ? () => PixelNotice.Show(full, autoCloseSeconds) : (UnityEngine.Events.UnityAction)null, sound: opens);
            b.interactable = opens;
        }

        contentHeight = y + 4f;
        listRect.sizeDelta = new Vector2(0f, contentHeight);
        ApplyScroll();
    }

    private void ApplyScroll()
    {
        if (boxRect == null) return;
        // The box's height computed from the screen (its own rect isn't valid until the canvas has updated once).
        Canvas canvas = overlayRoot.GetComponent<Canvas>();
        float scale = canvas != null && canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        viewHeight = Mathf.Max(0f, Screen.height / scale - 2f * (bar + overlayMargin.y));
        float max = Mathf.Max(0f, contentHeight - viewHeight);
        scrollOffset = Mathf.Clamp(scrollOffset, 0f, max);
        // The list's bottom edge sits at the box's bottom (newest line); scrolling up (offset > 0) slides it DOWN to reveal older lines.
        listRect.anchoredPosition = new Vector2(0f, -scrollOffset);

        bool scrolls = max > 0.5f;
        if (handleRect.gameObject.activeSelf != scrolls) handleRect.gameObject.SetActive(scrolls);
        if (scrolls)
        {
            float handleH = Mathf.Max(30f, viewHeight * viewHeight / contentHeight);
            handleRect.sizeDelta = new Vector2(6f, handleH);
            handleRect.anchoredPosition = new Vector2(-3f, (viewHeight - handleH) * (scrollOffset / max));
        }
        UpdateRowFade();
    }

    /// <summary>Fades each line by how high it sits in the box: full below the start fraction, fading out towards the top.</summary>
    private void UpdateRowFade()
    {
        float start = viewHeight * fadeStartFraction;
        float span = Mathf.Max(1f, viewHeight - start);
        for (int i = 0; i < rowRects.Count; i++)
        {
            RectTransform r = rowRects[i];
            CanvasGroup g = rowGroups[i];
            if (r == null || g == null) continue;
            float alpha = 1f;
            if (fadeUpperLines && viewHeight > 1f)
            {
                float centre = r.anchoredPosition.y + r.sizeDelta.y * 0.5f - scrollOffset; // height above the box's bottom
                float t = Mathf.Clamp01((centre - start) / span);
                alpha = Mathf.Lerp(1f, fadeTopAlpha, Mathf.SmoothStep(0f, 1f, t));
            }
            g.alpha = alpha;
            g.blocksRaycasts = alpha > 0.15f; // nearly invisible lines don't catch clicks
        }
    }

    private bool forceFade;

    /// <summary>Makes the open log fade away now, even while the mouse is over it.</summary>
    private void FadeNow()
    {
        forceFade = true;
        overlayTimer = Mathf.Min(overlayTimer, overlayFadeSeconds);
    }

    private bool ReopenKeyPressed()
    {
        return PixelKeys.Pressed(PixelAction.EventLog); // rebindable in Settings
    }

    private static float ScrollWheel()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
        return Input.mouseScrollDelta.y;
#endif
    }

    private void UpdateOverlay()
    {
        if (PixelPauseMenu.IsPaused || history.Count == 0) return;

        // Don't steal Enter from a text box.
        UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
        bool typing = es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;

        bool visible = overlayRoot != null && overlayRoot.activeSelf;
        if (!typing && !EventLogEnabled && PixelMyWindow.Available && ReopenKeyPressed())
        {
            PixelMyWindow.ShowLogTab();   // the on-screen log is off: the log key opens My Window on its Event log tab
            return;
        }
        if (PixelMyWindow.LogTabShowing)
        {
            if (visible) overlayRoot.SetActive(false);
            return;
        }
        if (!EventLogEnabled)
        {
            if (overlayRoot != null && overlayRoot.activeSelf) overlayRoot.SetActive(false);
            return;
        }
        if (!typing && ReopenKeyPressed())
        {
            if (visible && fullView) FadeNow();                   // the full log is open: close it
            else { scrollOffset = 0f; DisplayOverlay(true); }     // closed, or showing just the newest line: open the whole log
            return;
        }
        if (visible && PixelInput.RightPressed()) FadeNow(); // right-click anywhere closes the log

        if (!visible) return;
        if (rowsDirty) RebuildRows();

        // Hovering the box keeps it open and the mouse wheel scrolls it.
        bool hover = !forceFade && RectTransformUtility.RectangleContainsScreenPoint(boxRect, PixelInput.PointerPosition(), null);
        if (hover)
        {
            float wheel = ScrollWheel();
            if (Mathf.Abs(wheel) > 0.001f) { scrollOffset += wheel * overlayScrollSpeed; ApplyScroll(); }
            overlayTimer = overlayVisibleSeconds + overlayFadeSeconds;
        }

        overlayTimer -= Time.unscaledDeltaTime;
        overlayGroup.alpha = overlayFadeSeconds > 0f ? Mathf.Clamp01(overlayTimer / overlayFadeSeconds) : (overlayTimer > 0f ? 1f : 0f);
        overlayGroup.blocksRaycasts = overlayGroup.alpha > 0.3f;
        if (overlayTimer <= 0f) overlayRoot.SetActive(false);
    }

    private void Start()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker != null && clicker.onTierUnlocked != null) clicker.onTierUnlocked.AddListener(OnTierUnlocked);
    }

    private void OnTierUnlocked(int index)
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker == null || !clicker.IsValidTierIndex(index)) return;
        ShowOverlay("New pixel unlocked: " + clicker.Tiers[index].displayName + "!", "");
        Trigger("event_log"); // the very first line of the log: explain what the log is
    }

    private bool introChecked, freshAtStart = true;
    private bool introRunning;
    private float introIdle;

    /// <summary>True from the start of a new game until the intro tips (Log, Inventory) have been closed: the "Click 100 more..." guide stays hidden meanwhile.</summary>
    /// <summary>Are tips waiting to show (or the new-game intro still running)? Vendors don't walk in over them.</summary>
    public static bool TipsPending => instance != null && (instance.queue.Count > 0 || IntroHoldsGuide);

    public static bool IntroHoldsGuide => instance != null && instance.IntroHoldsGuideNow;

    private bool IntroHoldsGuideNow
    {
        get
        {
            if (introRunning) return true;
            if (introChecked) return false;
            if (!showIntro || !hintsEnabled || !TipsEnabled) return false;
            return forceIntro || PixelTitleScreen.NewGameStarted || (freshAtStart && !Seen("intro_log"));
        }
    }

    [Tooltip("A game still counts as NEW (so the intro tips play) while fewer than this many pixels have been collected in it - a few early clicks never cost the player the intro.")]
    [SerializeField] private int introFreshClicks = 60;

    private bool AnyCollected()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return false;
        double sum = 0d;
        foreach (PixelClicker.PixelTier t in clicker.Tiers) sum += t.totalCollected;
        return sum >= Mathf.Max(1, introFreshClicks);
    }
    private float introWait;
    private bool freshJudged;   // freshAtStart has been worked out (on the title screen, or at the first update after it)

    private void TryStartIntro()
    {
        if (introChecked) return;
        if (PixelTitleScreen.Showing)
        {
            freshAtStart = !AnyCollected(); // judged on the title screen, before the player can click anything
            freshJudged = true;
            return;
        }
        if (!freshJudged)
        {
            // The title screen never showed while this ran (a New Game / Restart presses Play at once, and which script updates first is not fixed): judge now, the player cannot have clicked yet.
            freshAtStart = !AnyCollected();
            freshJudged = true;
        }
        if (PixelCameraIntro.Moving || PixelCameraIntro.ClicksLocked) { introWait = 0f; return; }   // the intro tips wait until the camera animation is over
        introWait += Time.unscaledDeltaTime; // counts from the moment the game starts (after Play) and the camera intro has finished
        if (introWait < introDelay) return;
        introChecked = true;
        bool newGame = PixelTitleScreen.NewGameStarted;   // Play started a new game (decided when Play was pressed): the intro plays, whatever was clicked since
        if (!showIntro || !hintsEnabled || !TipsEnabled || (Seen("intro_log") && !newGame))
        {
            PixelDebug.Info("Intro tips not started: showIntro " + showIntro + ", hintsEnabled " + hintsEnabled + ", TipsEnabled " + TipsEnabled + ", intro_log already seen " + Seen("intro_log"), this);
            return;
        }

        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return;
        if (!forceIntro && !freshAtStart && !newGame) { PixelDebug.Info("Intro tips not started: not a new game (something was already collected when the title screen was left) and no replay was asked for", this); return; } // not a new game
        PixelDebug.Info("Intro tips start (replay " + forceIntro + ")", this);
        bool replay = forceIntro;
        forceIntro = false;

        introRunning = true;
        introIdle = 0f;
        PixelLog.SetLogOpen(true);
        PixelNotice.Show(introLogMessage, 0f, compact: true, beside: PixelLog.WindowRect, besideBottom: true, onClosed: () =>
        {
            PixelLog.SetLogOpen(false);
            int pinnedBefore = PixelUI.CompactMode;   // a game reset can leave the pinned (compact) view on: the intro always shows the full window
            PixelUI.CompactMode = 0;
            PixelUI.SetInventoryOpen(true);
            PixelNotice.Show(introInventoryText, 0f, compact: true, beside: PixelUI.WindowRect, onClosed: () =>
            {
                PixelUI.SetInventoryOpen(false, true);
                PixelUI.CompactMode = pinnedBefore;
                // Third tip: how currency is earned. The tier guide ("Click X more...") shows once it is closed.
                string earn = string.IsNullOrEmpty(introEarnText)
                    ? "Clicking the pixel is the main way to earn currency. As you play, upgrades, minigames and managing the old pixels that fall to the floor will open up other ways to earn."
                    : introEarnText;
                PixelNotice.Show(earn, 0f, compact: true, onClosed: () =>
                {
                    System.Action finish = () =>
                    {
                        introRunning = false; // the tier guide may show now (and the cube can be clicked: PixelClicker.ExternalClickBlock)
                        MarkSeen("intro_log"); // only counts once the player has been through all the tips
                        if (replay) ShowEventLogTip(); // the tutorial's last step: what the event log is
                    };
                    PixelBlockHold hold = PixelFind.Cached<PixelBlockHold>();
                    if (hold == null || !hold.Active) { finish(); return; }   // Block Hold comes from the Standard skill tree now: no right-click tip until it is unlocked
                    // Fourth tip, right after the one about clicking: the right click (Block Hold + stake).
                    string right = string.IsNullOrEmpty(introRightClickText)
                        ? "Try the right mouse button too: HOLD it on the block on the cube. It's each block's second action. On the pure pixels (black, gray, white, red, green and blue) it puts a stake on the block - the longer you hold (watch the meter by your cursor), the more it is worth when it comes round again, but the stake can also go bad and pay nothing. Skill blocks you meet later do their own thing when held."
                        : introRightClickText;
                    MarkSeen("stake_intro");   // (the later 25-click reminder is not needed now)
                    PixelNotice.Show(right, 0f, compact: true, onClosed: () => finish());
                });
            });
        });
    }

    /// <summary>Shows the event log (bottom left) and the small tip beside it explaining it.</summary>
    private void ShowEventLogTip()
    {
        Hint hint = hints != null ? hints.Find(h => h != null && h.id == "event_log") : null;
        if (hint == null || !hint.enabled || string.IsNullOrEmpty(hint.text)) return;
        MarkSeen("event_log"); // so the first pixel unlock doesn't repeat it
        Announce(hint.shortText, hint.text); // a line in the log; clicking it opens the tip
        DisplayOverlay(true);
        overlayTimer = Mathf.Max(overlayTimer, 20f); // keep the log up so the line can be seen
    }

    private bool windowWasOpen;

    private void Update()
    {
        TryStartIntro();
        // The event log steps aside the moment any window opens.
        bool windowOpen = PixelWindows.AnyOpen();
        if (windowOpen && !windowWasOpen && overlayRoot != null && overlayRoot.activeSelf) FadeNow();
        windowWasOpen = windowOpen;
        UpdateOverlay();
        bool showing = PixelNotice.IsShowing;
        if (introRunning)
        {
            // Safety: if the intro tips were replaced by another tip, don't hide the guide forever.
            introIdle = showing ? 0f : introIdle + Time.unscaledDeltaTime;
            if (introIdle > 2f) introRunning = false;
        }
        if (wasShowing && !showing) waitTimer = Mathf.Max(waitTimer, 0.3f); // short gap between queued tips
        wasShowing = showing;

        if (queue.Count == 0 || showing) return;
        if (waitTimer > 0f) waitTimer -= Time.unscaledDeltaTime;   // (PixelTipQueue shows the next tip once it is 0: ShowNextTip)
    }

    /// <summary>Shows the next queued first-time tip (called by PixelTipQueue when it is this source's turn).</summary>
    private void ShowNextTip()
    {
        if (queue.Count == 0) return;
        Hint next = queue.Dequeue();
        // The tips for something that just happened in a minigame are small; the rest are shop-sized.
        // The first ghost tip appears beside the ghost and stops time until it is closed.
        PixelGhostMinigame ghostGame = next.id == "minigame_ghost" ? PixelMinigame.Find("ghost") as PixelGhostMinigame : null;
        if (next.id != null && next.id.StartsWith("minigame_"))
        {
            // Every first-time minigame tip stops time and darkens the screen until 'Got it' (the first ghost tip sits beside the ghost).
            PixelTutorialDim.Begin(null);
            System.Func<Vector2?> near = ghostGame != null && ghostGame.GhostScreenPosition().HasValue ? ghostGame.GhostScreenPosition : null;
            PixelNotice.Show(next.text, 0f, small: true, nearScreen: near, freezeTime: true, onClosed: PixelTutorialDim.End);
            wasShowing = true;
            return;
        }
        if (next.id == "device_Sorter")
        {
            // The Sorter tip is a small box and stops time until it is closed.
            PixelNotice.Show(next.text, 0f, small: true, freezeTime: true);
            wasShowing = true;
            return;
        }
        if (next.id == PixelHuskCrush.TipId)
        {
            // The husk crush tip: a small box at the right edge of the screen, half way down (the point sits just past the edge, so the box lands left of it), and time keeps running (you try it while it shows).
            PixelNotice.Show(next.text, 0f, small: true, nearScreen: () => new Vector2(Screen.width * 1.03f, Screen.height * 0.55f), freezeTime: false);
            wasShowing = true;
            return;
        }
        if (next.id == "potion")
        {
            // The first potion tip: a small box at the TOP RIGHT of the screen (just under the top bar), and time keeps running - it should not get in the way.
            PixelNotice.Show(next.text, 0f, small: true, nearScreen: () => new Vector2(Screen.width * 1.03f, Screen.height * 0.8f), freezeTime: false);
            wasShowing = true;
            return;
        }
        PixelNotice.Show(next.text, autoCloseSeconds, small: next.id != null && (next.id.StartsWith("device_") || next.id.StartsWith("minigame_") || next.id.StartsWith("timestop_") || next.id.StartsWith("bank_")),
                         aboveCombo: next.id == "combo", nearLog: false); // the first combo tip sits just above the combo meter
        wasShowing = true;
    }
}
