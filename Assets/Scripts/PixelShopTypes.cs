// ---- System notes ----
// PixelShopTypes.cs
//   Part of PixelShop (partial class): the data types (pack, cost, level, requirement, tab and effect types). The system notes are in PixelShop.cs.
// ---------------------------------------------------
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class PixelShop
{
    public enum ButtonCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    // ------------------------------------------------------------------
    // Data types
    // ------------------------------------------------------------------

    /// <summary>One line of a pack's price.</summary>
    [Serializable]
    public class PackCost
    {
        [Tooltip("Which currency is spent.")]
        public PixelClicker.PixelType type = PixelClicker.PixelType.White;

        [Tooltip("How much of it is spent.")]
        public double amount = 100;

        [Tooltip("Spend something that is not a pixel: a MATERIAL id (charcoal - PixelMaterials) or a minigame's id (bomb = bomb parts). Empty = spend the pixel type above.")]
        public string minigameCurrency = "";
    }

    /// <summary>What a leveled (upgrade) pack changes on the auto clicker.</summary>
    public enum UpgradeEffect
    {
        None = 0,
        /// <summary>Each level sets the auto clicker's interval (seconds) to the level's Value.</summary>
        AutoClickerInterval = 1,
        /// <summary>Each level sets the auto clicker's clicks per tick to the level's Value.</summary>
        AutoClickerClicks = 2,
        /// <summary>Level 1 switches the combo meter on; each level sets its maximum multiplier to the level's Value.</summary>
        ComboMeter = 3,
        /// <summary>Each level sets how many pixels the Pixel Bank can hold to the level's Value.</summary>
        BankCapacity = 4,
        /// <summary>Each level sets how many old pixels may lie around at once to the level's Value.</summary>
        OldPixelCap = 5,   // RETIRED 2026-10-24: never used, the number stays reserved (MigrateOldPackData removes saved packs with it; Husk Capacity comes back as a Standard reward, see the to-do)
    }

    /// <summary>The shop tabs. 'Automatic' picks Pixels or Upgrades from what the pack does.</summary>
    public enum ShopTab
    {
        /// <summary>Packs that unlock pixels go to Pixels; auto clicker / upgrade packs go to Upgrades.</summary>
        Automatic = 0,
        Pixels = 1,
        Upgrades = 2,
        Consumables = 3,
        Minigames = 4,
    }

    /// <summary>One purchasable level of an upgrade pack.</summary>
    [Serializable]
    public class PackLevel
    {
        [Tooltip("Price of reaching this level.")]
        public PackCost[] costs;

        [Tooltip("Value this level sets. Interval upgrade: seconds between clicks. Clicks upgrade: clicks per tick.")]
        public float value = 1f;
    }

    /// <summary>What a requirement checks.</summary>
    public enum RequirementKind
    {
        /// <summary>Another pack must have been bought first.</summary>
        Pack = 0,
        /// <summary>A minigame's goal (e.g. ghosts caught) must have been reached.</summary>
        MinigameGoal = 1,
        /// <summary>(2026-10-25) MACHINE DISCOVERY: a machine must have made enough of something (a PixelStats counter, e.g. the Kiln's Charcoal: 'kiln.charcoal' >= 50).
        /// One-time: the counter never goes down, so the pack stays buyable even when the machine is gone. The next making skill's blocks use it too.</summary>
        Discovery = 2,
    }

    /// <summary>One thing that must be true before a pack can be bought. A pack can have several; all must be met.</summary>
    [Serializable]
    public class PackRequirement
    {
        [Tooltip("Pack = another pack must be bought first. Minigame Goal = a minigame's goal must be reached. Discovery = a machine must have made enough of something (a stats counter).")]
        public RequirementKind kind = RequirementKind.Pack;

        [Tooltip("Pack: index (in the Packs list, starting at 0) of the pack that must be bought first.")]
        public int packIndex = -1;

        [Tooltip("Minigame Goal: id of the minigame (e.g. ghost, blackhole).")]
        public string minigameId = "";

        [Tooltip("Discovery: the PixelStats counter the machine raises (e.g. kiln.charcoal = Charcoal the Kiln has made).")]
        public string statKey = "";

        [Min(1f)]
        [Tooltip("Discovery: how many it must have made (the counter's target).")]
        public double count = 50d;

        [Tooltip("Discovery: what is discovered, in words (e.g. Charcoal): 'Charcoal: 14 / 50 to discover', 'You've discovered Charcoal!'.")]
        public string label = "";
    }

    /// <summary>Something purchasable: a price and the tiers it unlocks.</summary>
    [Serializable]
    public class ShopPack
    {
        [Tooltip("Name shown in the shop.")]
        public string displayName = "Pack";

        [TextArea(1, 3)]
        [Tooltip("Short description shown under the name. Use {interval} to show the auto clicker's interval in seconds.")]
        public string description = "";

        [Tooltip("Which shop tab this item is listed on. Automatic = Pixels for packs that unlock pixels, " +
                 "Upgrades for the auto clicker and its upgrades.")]
        public ShopTab tab = ShopTab.Automatic;

        [Tooltip("Everything the player must pay. All costs are paid together.")]
        public PackCost[] costs;

        [Tooltip("Tiers unlocked by this pack. Added to PixelClicker automatically if it doesn't have them. " +
                 "Set Spawn Weight to control how often each appears.")]
        public PixelClicker.PixelTier[] rewardTiers;

        [Tooltip("Everything that must be true before this pack can be bought (other packs bought, minigame goals reached). " +
                 "All of them are required. Empty = available from the start. " +
                 "For an upgrade pack (one with levels), its first Pack requirement is the pack whose upgrades window lists it.")]
        public PackRequirement[] requirements;

        /// <summary>Index of the first required pack, or -1. Upgrade packs use it to find the pack they belong to.</summary>
        public int ParentIndex
        {
            get
            {
                if (requirements != null)
                    foreach (PackRequirement r in requirements)
                        if (r.kind == RequirementKind.Pack && r.packIndex >= 0) return r.packIndex;
                return -1;
            }
        }

        [Tooltip("A skill pack: buying this makes the skill with this id available (e.g. 'electrician'). It is listed under 'Skill packs' on the Pixels tab.")]
        public string skillId = "";

        [Tooltip("Buying this pack switches on the auto clicker.")]
        public bool unlocksAutoClicker = false;

        [Tooltip("Buying this pack unlocks crafting (the Crafting button and window).")]
        public bool unlocksCrafting = false;

        [Tooltip("Buying this pack lets the player click and drag husks.")]
        public bool unlocksGrabbing = false;

        [Tooltip("Buying this pack lets the player stop time with the T key.")]
        public bool unlocksTimeStop = false;

        [Tooltip("Buying this unlocks Time Slow (press S to slow time; uses the Time Stop meter). Needs the Time Stop pack.")]
        public bool unlocksTimeSlow = false;

        [Tooltip("Buying this pack unlocks the Pixel Bank (the Bank tab, the hose and the B key).")]
        public bool unlocksBank = false;

        [Tooltip("Buying this pack unlocks Block Hold (hold the right mouse button on the cube's block to pull it out of the rotation for a moment).")]
        public bool unlocksBlockHold = false;

        [Tooltip("Buying this pack gives you the Robot Worker (he keeps a chosen consumable running).")]
        public bool unlocksRobot = false;

        [Tooltip("Id of a minigame that buying this pack switches on (e.g. ghost, blackhole). Empty = none.")]
        public string unlocksMinigame = "";

        [Tooltip("Owning this pack turns on TREE MODE for Woodcutting (PixelTreeMode: filler rotation + trees in a designed order). Test pack.")]
        public bool unlocksTreeMode = false;

        [Tooltip("A gathering skill pack on the shop's price ladder (PixelShop 'Skill pack price ladder'): its price is worked out from how many skill packs you own, not typed here (the costs above are ignored).")]
        public bool skillLadder = false;

        [Tooltip("Left out of the shop (never listed). It still exists and works: unlock or relock it with the Dev Tools 'Shop unlocks' widget. Used for the testing packs.")]
        public bool hideInShop = false;

        // Older versions used one tick box per minigame. They are read once and turned into the ids above.
        // Older versions had one required pack index and one minigame goal id here; they are moved into 'requirements'.
        [HideInInspector] public int requiresPackIndex = -1;
        [HideInInspector] public string requiresMinigameGoal = "";

        [HideInInspector] public bool unlocksGhostMinigame;
        [HideInInspector] public bool unlocksBlackholeMinigame;
        [HideInInspector] public bool requiresSingularity;
        [HideInInspector] public bool requiresGhosts;

        /// <summary>Has this pack been bought? (Packs with reward tiers also count as bought once all their tiers are unlocked.) Progress in the
        /// GameState by display name - tick it to test in the Pixel Game State component (State > Shop).</summary>
        public bool purchased
        {
            get { ShopState.Entry e = PixelGameState.Current.shop.Find(displayName, false); return e != null && e.purchased; }
            set { ShopState.Entry e = PixelGameState.Current.shop.Find(displayName, value); if (e != null) e.purchased = value; }
        }

        [Header("Upgrade levels (leave empty for a one-time pack)")]
        [Tooltip("What each level of this upgrade changes.")]
        public UpgradeEffect upgradeEffect = UpgradeEffect.None;

        [Tooltip("The levels, in order. Level 1 is Element 0. If this has entries, 'Costs' above is ignored and each level has its own price.")]
        public PackLevel[] levels;

        /// <summary>The upgrade's current level (0 = not bought). Progress in the GameState by display name.</summary>
        public int level
        {
            get { ShopState.Entry e = PixelGameState.Current.shop.Find(displayName, false); return e != null ? e.level : 0; }
            set { ShopState.Entry e = PixelGameState.Current.shop.Find(displayName, value != 0); if (e != null) e.level = value; }
        }

        [NonSerialized] public int appliedLevel = -1;
    }
}
