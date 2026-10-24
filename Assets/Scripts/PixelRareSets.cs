// ---- System notes ----
// PixelRareSets.cs
//   ONE table of the super-rare FAMILIES (2026-10-24, to-do 'Rare set table'): the Dragon Cubes, the God Cubes, the Prime Cubes and the Prime
//   Cubelette. Each set holds everything the game needs to know about the family, and the systems READ it instead of hand-wiring each family:
//   - members (pixel type, name, colour, glow, Collection Log flavour text), in order (a Dragon Cube's stars = its place + 1);
//   - logSection (the Collection Log section; the gallery puts each set on its own row, in table order);
//   - oneOfEach (only one of each member can ever be held; the block cards' RareCollectible tag says the same for the click rules);
//   - show (the find show that plays when one is clicked: PixelDragonFind or PixelGodFind);
//   - rarity (which PixelClicker Inspector knob sets the spawn weight / switches the set off: Dragon or God), neverRolled (weight 0: the
//     Cubelette is only forged with the Gauntlet) and translucent / renameSaved (a saved scene's tier takes the table's name again);
//   - firstFoundFormat + firstFoundTip (the first-find log line and tip box), completeText (the note when every member is held).
//   Readers: PixelClicker.EnsureRareSets (makes the tiers at start), the click's find show, the first-find announcement, HasAllDragonCubes /
//   PickMissingDragonCube, the Dragon Wish (uses the seven up), the Collection Log entries and the gallery rows.
//   A NEW family = its PixelType values (appended to the enum) + block cards in PixelBlockCatalog + one entry here + its looks (PixelLooksDefaults).
//   The Golden Snitch is a single rare (no set) and keeps its own EnsureGoldenSnitch.
//   TWO KINDS of collectible: the RARE ones you find (Dragon, God, Prime Cubes: rolled on the cube) and the ones you WORK FOR (the RELICS:
//   Rarity.Earned, never rolled; a vendor hands one over at a rapport step - PixelRapport Vendor.reward - and each does something while
//   held - PixelRelics.cs). A member's 'hint' shows on its Collection Log page before it is found (how to get it).
// ---------------------------------------------------
using UnityEngine;

public static class PixelRareSets
{
    public enum Show { None, DragonFind, GodFind }
    public enum Rarity { Dragon, God, Earned }   // (Earned = the Relics: never rolled, no Inspector switch)

    public class Member
    {
        public PixelClicker.PixelType type;
        public string name, flavour;
        public string hint;  // shown on its Collection Log page before it is found ('???' when empty): how to get it
        public Color color;
        public float glow;   // glow intensity (0 = no glow)
    }

    public class Set
    {
        public string id, logSection, firstFoundFormat, firstFoundTip, completeText;
        public bool oneOfEach, translucent, renameSaved, neverRolled;
        public Show show;
        public Rarity rarity;
        public Member[] members;

        /// <summary>The member's place in the set (0..n-1), or -1 if the type is not in it.</summary>
        public int IndexOf(PixelClicker.PixelType type)
        {
            for (int i = 0; i < members.Length; i++) if (members[i].type == type) return i;
            return -1;
        }
    }

    public static readonly Set Dragon = new Set
    {
        id = "dragon", logSection = "Dragon Cubes", show = Show.DragonFind, rarity = Rarity.Dragon, translucent = true,
        firstFoundFormat = "You found a {0}!", firstFoundTip = "dragon_first", completeText = "You hold all seven Dragon Cubes! Summon the dragon.",
        members = DragonMembers(),
    };

    public static readonly Set God = new Set
    {
        id = "god", logSection = "God Cubes", show = Show.GodFind, rarity = Rarity.God, oneOfEach = true,
        firstFoundFormat = "You found {0}! Only one of each God Cube can exist.", firstFoundTip = "god_first",
        members = new[]
        {
            new Member { type = PixelClicker.PixelType.GodSlifer, name = "Slifer the Sky Dragon", color = new Color(0.92f, 0.12f, 0.1f, 1f), glow = 1.6f,
                         flavour = "A red cube held together by storm lightning. The Sky Dragon answers to no one." },
            new Member { type = PixelClicker.PixelType.GodRa, name = "Ra the Winged Dragon", color = new Color(1f, 0.82f, 0.15f, 1f), glow = 2.2f,
                         flavour = "Polished gold wrapped in a sun's corona. The Winged Dragon of the sun." },
            new Member { type = PixelClicker.PixelType.GodObelisk, name = "Obelisk the Tormentor", color = new Color(0.2f, 0.38f, 0.95f, 1f), glow = 1.4f,
                         flavour = "A deep blue stone that sheds dust. The Tormentor's fist ends arguments." },
        },
    };

    public static readonly Set Prime = new Set
    {
        id = "prime", logSection = "Prime Cubes", show = Show.GodFind, rarity = Rarity.God, oneOfEach = true, renameSaved = true,
        firstFoundFormat = "You found the {0}! Only one of each Prime Cube can exist.", firstFoundTip = "stone_first",
        members = PrimeMembers(),
    };

    public static readonly Set Cubelette = new Set
    {
        id = "cubelette", logSection = "Prime", show = Show.GodFind, rarity = Rarity.God, neverRolled = true, oneOfEach = true, renameSaved = true,
        firstFoundFormat = "You forged the {0}!",
        members = new[]
        {
            new Member { type = PixelClicker.PixelType.InfinityCubelette, name = "Prime Cubelette", color = new Color(1f, 0.9f, 0.55f, 1f), glow = 2.2f,
                         flavour = "Forged in the Gauntlet from all six Prime Cubes. Only one can exist." },
        },
    };

    /// <summary>The RELICS: collectibles you work for (never drop; a vendor gives each one at a rapport step; PixelRelics.cs says what they do).</summary>
    public static readonly Set Relics = new Set
    {
        id = "relic", logSection = "Relics", show = Show.None, rarity = Rarity.Earned, neverRolled = true, oneOfEach = true, renameSaved = true,
        firstFoundFormat = "You earned the {0}! It is in your Collection Log.", firstFoundTip = "relic_first",
        members = new[]
        {
            new Member { type = PixelClicker.PixelType.GoldenAxe, name = "Golden Axe", color = new Color(1f, 0.78f, 0.2f, 1f), glow = 1.2f,
                         flavour = "Big Tim's golden axe, handed down to a true friend. While you hold it, Woodcutting earns more XP.",
                         hint = "Big Tim the Lumberjack gives it to a friend. Deal with him, sell him wood and bring him gifts he loves." },
            new Member { type = PixelClicker.PixelType.MarketCharm, name = "Market Charm", color = new Color(0.72f, 0.42f, 1f, 1f), glow = 1.6f,
                         flavour = "A violet charm that hums when coins change hands. Cast it from any vendor's Sell view and every vendor pays full price again. It recharges for an hour.",
                         hint = "The Wizard trusts only his partners with it. Deal with him, sell him Ectoplasm and Stardust and bring him gifts he loves." },
        },
    };

    /// <summary>Every set, in the order the tiers are made, the Collection Log lists them and the gallery lays out its rows.</summary>
    public static readonly Set[] All = { Dragon, God, Prime, Cubelette, Relics };

    /// <summary>The set this pixel type belongs to (null for every other type).</summary>
    public static Set Of(PixelClicker.PixelType type)
    {
        foreach (Set s in All) if (s.IndexOf(type) >= 0) return s;
        return null;
    }

    private static Member[] DragonMembers()
    {
        Member[] m = new Member[7];
        for (int i = 0; i < 7; i++)
            m[i] = new Member
            {
                type = PixelClicker.PixelType.DragonCube1 + i, name = "Dragon Cube (" + (i + 1) + ")", color = new Color(1f, 0.62f, 0.12f, 0.6f),
                flavour = "A glassy orange cube with " + (i + 1) + (i == 0 ? " small cube" : " small cubes") + " inside, like a dragon ball. Gather all seven and the dragon can be summoned.",
            };
        return m;
    }

    private static Member[] PrimeMembers()
    {
        string[] flavour =
        {
            "Bends space itself. A tesseract turns inside it.", "Thoughts crackle through it like lightning.", "Reality warps wherever it sits.",
            "Unbelievable power, packed into a tiny cube.", "Its clock hands run backwards and forwards at will.", "Wisps of orange mist circle it. It remembers.",
        };
        Member[] m = new Member[6];
        for (int i = 0; i < 6; i++)
            m[i] = new Member { type = PixelClicker.PixelType.StoneSpace + i, name = PixelInfinity.StoneName(i), color = PixelInfinity.StoneColor(i), glow = 1.5f, flavour = flavour[i] };
        return m;
    }
}
