// ---- System notes ----
// PixelMaterials.cs
//   MATERIALS: things the player holds that are not pixel currency and not husks - made by machines (the Kiln's Charcoal, 2026-10-24),
//   shown in the Inventory's Materials tab (with the minigame materials such as Bomb Parts) and spendable in shop prices.
//   - The kinds of material are the Inspector list 'materials' (id, name, colour); the amounts held are progress: GameState.materials
//     (MaterialsState, by id), so they save / load / reset with the game.
//   - Add(id, n) / TrySpend(id, n) / Amount(id); Changed fires on every change.
//   - ONE place for 'pay with something that is not a pixel': a price line (PixelShop.PackCost) whose 'minigameCurrency' names a material id
//     or a minigame id (bomb = bomb parts) goes through CostHeld / CostSpend / CostName / CostColor here (the shop and the visitors use them).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public class PixelMaterials : MonoBehaviour
{
    [Serializable]
    public class MaterialDef
    {
        [Tooltip("Stable id used in saves and shop prices (lower case, e.g. charcoal). Don't rename one that is in use.")]
        public string id = "material";
        [Tooltip("Name shown in the Inventory, popups and shop prices.")]
        public string displayName = "Material";
        [Tooltip("Colour of its Inventory line, its price swatch and its '+N' popup.")]
        public Color color = Color.white;
    }

    [Tooltip("Every kind of material. Missing built-in ones (Charcoal) are added back at start.")]
    [SerializeField] private List<MaterialDef> materials = new List<MaterialDef>();

    public static event Action Changed;

    private static PixelMaterials instance;
    private static MaterialsState S => PixelGameState.Current.materials;

    public const string CharcoalId = "charcoal";

    private static MaterialDef[] BuiltIn() => new[]
    {
        new MaterialDef { id = CharcoalId, displayName = "Charcoal", color = new Color(0.62f, 0.62f, 0.66f, 1f) },
    };

    private void Awake()
    {
        instance = this;
        EnsureBuiltIns();
    }

    private void OnValidate() => EnsureBuiltIns();

    private void OnDestroy() { if (instance == this) instance = null; }

    private void EnsureBuiltIns()
    {
        if (materials == null) materials = new List<MaterialDef>();
        foreach (MaterialDef d in BuiltIn())
            if (materials.Find(m => m != null && m.id == d.id) == null) materials.Add(d);
    }

    /// <summary>The definition of a material id (the Inspector list, else the built-in one), or null when it is not a material.</summary>
    public static MaterialDef Def(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        PixelMaterials m = instance != null ? instance : PixelFind.Cached<PixelMaterials>();
        if (m != null && m.materials != null)
            foreach (MaterialDef d in m.materials) if (d != null && d.id == id) return d;
        foreach (MaterialDef d in BuiltIn()) if (d.id == id) return d;
        return null;
    }

    /// <summary>Every kind of material (for the Inventory list).</summary>
    public static IEnumerable<MaterialDef> All()
    {
        PixelMaterials m = instance != null ? instance : PixelFind.Cached<PixelMaterials>();
        return m != null && m.materials != null ? (IEnumerable<MaterialDef>)m.materials : BuiltIn();
    }

    public static bool IsMaterial(string id) => Def(id) != null;
    public static double Amount(string id) => string.IsNullOrEmpty(id) ? 0d : S.Get(id);

    public static void Add(string id, double amount)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0d) return;
        S.Set(id, S.Get(id) + amount);
        Changed?.Invoke();
    }

    /// <summary>Spends some; false (and nothing spent) when there isn't enough. Infinite resources (dev) always pays.</summary>
    public static bool TrySpend(string id, double amount)
    {
        if (PixelClicker.InfiniteResources) return true;
        if (string.IsNullOrEmpty(id) || amount < 0d || S.Get(id) < amount) return false;
        S.Set(id, S.Get(id) - amount);
        Changed?.Invoke();
        return true;
    }

    public static string NameOf(string id) { MaterialDef d = Def(id); return d != null ? d.displayName : id; }
    public static Color ColorOf(string id) { MaterialDef d = Def(id); return d != null ? d.color : Color.white; }

    // ---- Price lines that are not a pixel: a material id, or a minigame id (bomb parts) ----

    /// <summary>How much of a non-pixel price currency the player holds.</summary>
    public static double CostHeld(string id)
    {
        if (IsMaterial(id)) return Amount(id);
        PixelMinigame m = PixelMinigame.Find(id);
        if (m == null) return 0d;
        if (!string.IsNullOrEmpty(m.MaterialName)) return m.MaterialAmount;   // a minigame's material (Ectoplasm, Magic Wood, Stardust, Meteor Chunks, Bomb Parts)
        return m.HasTracker ? m.SpendableCount : 0d;
    }

    public static bool CostAffordable(string id, double amount) => PixelClicker.InfiniteResources || CostHeld(id) >= amount;

    /// <summary>Pays a non-pixel price line.</summary>
    public static void CostSpend(string id, double amount)
    {
        if (PixelClicker.InfiniteResources) return;
        if (IsMaterial(id)) { TrySpend(id, amount); return; }
        PixelMinigame m = PixelMinigame.Find(id);
        if (m == null) return;
        if (!string.IsNullOrEmpty(m.MaterialName)) m.TrySpendMaterial(amount);
        else m.TrySpendTracker(amount);
    }

    public static string CostName(string id)
    {
        if (IsMaterial(id)) return NameOf(id);
        PixelMinigame m = PixelMinigame.Find(id);
        if (m != null && !string.IsNullOrEmpty(m.MaterialName)) return m.MaterialName;
        return m != null && !string.IsNullOrEmpty(m.TrackerTitle) ? m.TrackerTitle : id;
    }

    /// <summary>Swatch colour of a non-pixel price line (minigame counters: grey).</summary>
    public static Color CostColor(string id)
    {
        if (IsMaterial(id)) return ColorOf(id);
        PixelMinigame m = PixelMinigame.Find(id);
        return m != null && !string.IsNullOrEmpty(m.MaterialName) ? m.MaterialColor : new Color(0.6f, 0.6f, 0.65f, 1f);
    }
}
