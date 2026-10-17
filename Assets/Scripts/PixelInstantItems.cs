// ---- System notes ----
// PixelInstantItems.cs
//   ONE data-driven list of the INSTANT-USE items (consumable 'devices' that are not placed: right-click in the inventory / a keybind does something at once).
//   Each Entry: the DeviceKind, whether one is used up on success (usedUp), a stat counter, a sound, and Use(consumables, item, out message) -> true when it
//   worked (message = the event-log line, shown on success AND failure). PixelConsumables reads this list for TryUseItem (the use rule), IsSupplyItem (the
//   Supplies category) and the display model (BuildItemModel). A NEW instant item = its DeviceKind (appended to the enum), its CreateDefault... in
//   PixelConsumables (name, costs, maxHeld...), one Entry here, and a model branch in PixelConsumables.BuildItemModel.
//   Current: Combo Fuel (used by itself), Ghost Bait, Pet Treat, Bait Bucket, Prime Gauntlet (opens its window), Conveyor Belt (a tool, never used up),
//   Lumberjack's Focus, Pesticide Spray.
// ---------------------------------------------------
using UnityEngine;

public static class PixelInstantItems
{
    /// <summary>What happens on a right-click: true = it worked. 'message' = the event-log line (shown either way; empty = none).</summary>
    public delegate bool UseRule(PixelConsumables consumables, int item, out string message);

    public class Entry
    {
        public PixelConsumables.DeviceKind kind;
        /// <summary>One is used up when it worked (false: a tool, or something that uses itself up later, like the Gauntlet's window).</summary>
        public bool usedUp = true;
        /// <summary>Stat counter added when it worked (empty = none).</summary>
        public string stat = "";
        /// <summary>Sound played when it worked (empty = none).</summary>
        public string sound = "";
        public UseRule use;
        /// <summary>Listed in the shop's Utilities next to the machines it connects (the Coupling, the Container Hose), not under Supplies.</summary>
        public bool withUtilities;
    }

    /// <summary>Starts placing a bought tester piece from the game: one is used up per piece placed, placing stops when none are left.</summary>
    private static bool PlacePieces(PixelConsumables c, PixelConsumables.DeviceKind kind, string feature, string hint, out string msg)
    {
        bool ok = c.OwnedOfKind(kind) > 0 && PixelFeatureTester.StartFeature(feature, () => { c.TryUseAutoItem(kind); return c.OwnedOfKind(kind) > 0; });
        msg = ok ? hint : "";
        return ok;
    }

    public static readonly Entry[] All =
    {
        new Entry
        {
            kind = PixelConsumables.DeviceKind.ComboFuel, usedUp = false,
            use = (PixelConsumables c, int item, out string msg) => { msg = "Combo Fuel is used by itself when your combo bar runs out"; return false; },
        },
        new Entry
        {
            kind = PixelConsumables.DeviceKind.GhostBait, stat = "bait.used",
            use = (PixelConsumables c, int item, out string msg) =>
            {
                PixelGhostMinigame ghosts = PixelFind.First<PixelGhostMinigame>();
                bool ok = ghosts != null && ghosts.UseBait();
                msg = ok ? "Ghost Bait set out" : "Ghost Bait needs the Ghost Hunt running";
                return ok;
            },
        },
        new Entry
        {
            kind = PixelConsumables.DeviceKind.PetTreat, stat = "treat.used",
            use = (PixelConsumables c, int item, out string msg) =>
            {
                bool ok = PixelPets.Instance != null && PixelPets.Instance.TryUseTreat();
                msg = ok ? "Your pets are hyper!" : "Pet Treat needs a pet that is out";
                return ok;
            },
        },
        new Entry
        {
            kind = PixelConsumables.DeviceKind.BaitBucket,
            use = (PixelConsumables c, int item, out string msg) => PixelBaitBucket.TryPlace(out msg),
        },
        new Entry
        {
            kind = PixelConsumables.DeviceKind.Gauntlet, usedUp = false,   // (the window uses up the Gauntlet when the Cubelette is forged)
            use = (PixelConsumables c, int item, out string msg) => { msg = ""; return PixelInfinity.Open(c, item); },
        },
        new Entry
        {
            kind = PixelConsumables.DeviceKind.ConveyorBelt, usedUp = false,   // a tool: never used up
            use = (PixelConsumables c, int item, out string msg) =>
            {
                bool ok = PixelFeatureTester.StartFeature("Conveyor Belt");
                msg = ok ? "Click a point, then another to lay a belt. Right click stops" : "";
                return ok;
            },
        },
        new Entry
        {
            kind = PixelConsumables.DeviceKind.Coupling, usedUp = false, withUtilities = true,   // (one is used up per coupling fitted, by the tester's placing)
            use = (PixelConsumables c, int item, out string msg) => PlacePieces(c, PixelConsumables.DeviceKind.Coupling, "Coupling",
                "Bring the coupling near a device: it snaps onto a free socket (the teal rings). Click to fit it. Right click stops", out msg),
        },
        new Entry
        {
            kind = PixelConsumables.DeviceKind.ContainerHose, usedUp = false, withUtilities = true,   // (one is used up per hose laid)
            use = (PixelConsumables c, int item, out string msg) => PlacePieces(c, PixelConsumables.DeviceKind.ContainerHose, "Container Hose",
                "Click an Out coupling, then bend points, then an In coupling. Right click takes a point back", out msg),
        },
        new Entry
        {
            kind = PixelConsumables.DeviceKind.FocusPotion, stat = "focus.used",
            use = (PixelConsumables c, int item, out string msg) =>
            {
                bool ok = PixelFocus.Instance != null && PixelFocus.Instance.DrinkForSkill(PixelSkills.WoodcuttingId);
                msg = ok ? c.ItemName(item) + ": no hazards for a while" : "The focus potion can't be drunk right now";
                return ok;
            },
        },
        new Entry
        {
            kind = PixelConsumables.DeviceKind.PesticideSpray, stat = "pesticide.sprays", sound = "purchase",
            use = (PixelConsumables c, int item, out string msg) =>
            {
                int add = c.PesticideSprayCharges;
                PixelBlockRules.AddCharges(PixelClicker.PixelType.HornetNest, add);
                msg = "+" + add + " Pesticide charges (" + PixelBlockRules.Charges(PixelClicker.PixelType.HornetNest) + " ready)";
                return true;
            },
        },
    };

    /// <summary>The entry of an instant-use item kind (null = a placed device or a seed).</summary>
    public static Entry Find(PixelConsumables.DeviceKind kind)
    {
        foreach (Entry e in All) if (e.kind == kind) return e;
        return null;
    }
}
