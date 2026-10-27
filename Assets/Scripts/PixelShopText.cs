// ---- System notes ----
// PixelShopText.cs
//   The shop's editable text file: Assets/Resources/ShopText.txt holds the NAME, DESCRIPTION and PRICE of every shop pack and every
//   built-in device (consumable), so they can be edited without the Inspector. The file wins: at play, PixelShop.ApplyShopText and
//   PixelConsumables.ApplyShopText copy what it says over the coded / Inspector values (a field left out or empty keeps them).
//   FORMAT: '#' lines are comments. Each item is a block that starts with its key in brackets - '[Pack: <coded name>]' or
//   '[Device: <coded name>]' (the coded default name, so renaming an item in 'Name:' keeps it matched) - followed by
//     Name: ...           Description: ... (one line; write \n for a line break)
//     Price: 75 White, 50 Green          (amount + pixel type, comma separated; K / M / B allowed: 1.5K; '@bomb' = a minigame's counter;
//                                         a currency named twice is added up: '10 White, 5 White' = 15 White - MergeCosts)
//     Price L1: ... / Price L2: ...      (upgrade packs: one line per level)
//   Pixel types are written by their code name (White, HornetNest...); display names work too. Gathering skill packs (ShopPack.skillLadder)
//   get their price from the shop's skill pack ladder, so their Price line is ignored.
//   In the Editor the file is created on the first play if it is missing, and items it doesn't list yet are appended at the end
//   (never overwriting what is there). Potions and seeds are priced from their pixel at run time, so they are not in the file.
//   Renaming an item changes the name the save file stores it under (saves match packs / devices by name).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>Reads and writes Resources/ShopText.txt: names, descriptions and prices of the shop's packs and devices.</summary>
public static class PixelShopText
{
    public const string ResourceName = "ShopText";

    /// <summary>One item's block in the file. Null fields = not given (keep the coded value).</summary>
    public sealed class Entry
    {
        public string name, description, price;
        public readonly Dictionary<int, string> levelPrices = new Dictionary<int, string>();   // level number (1-based) -> price
    }

    private static Dictionary<string, Entry> entries;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { entries = null; }   // (re-read the file every play, also when domain reload is off)

    public static string PackKey(string codedName) => "Pack: " + codedName;
    public static string DeviceKey(string codedName) => "Device: " + codedName;

    /// <summary>The block for a key ("Pack: RGB Pack"), or null when the file doesn't list it.</summary>
    public static Entry Find(string key)
    {
        if (entries == null) Load();
        return key != null && entries.TryGetValue(key, out Entry e) ? e : null;
    }

    private static void Load()
    {
        entries = new Dictionary<string, Entry>();
        TextAsset asset = Resources.Load<TextAsset>(ResourceName);
        if (asset != null) Parse(asset.text, entries);
    }

    private static void Parse(string text, Dictionary<string, Entry> into)
    {
        Entry current = null;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                current = new Entry();
                into[line.Substring(1, line.Length - 2).Trim()] = current;
                continue;
            }
            if (current == null) continue;
            int colon = line.IndexOf(':');
            if (colon <= 0) continue;
            string field = line.Substring(0, colon).Trim().ToLowerInvariant();
            string value = line.Substring(colon + 1).Trim();
            if (value.Length == 0) continue;   // empty = keep the coded value
            if (field == "name") current.name = value;
            else if (field == "description") current.description = value.Replace("\\n", "\n");
            else if (field == "price") current.price = value;
            else if (field.StartsWith("price l") && int.TryParse(field.Substring(7), out int level) && level >= 1) current.levelPrices[level] = value;
        }
    }

    // ------------------------------------------------------------------
    // Prices
    // ------------------------------------------------------------------

    /// <summary>"75 White, 50 Green" -> costs. Returns null (and warns) if any part can't be read, so a typo never makes an item free.</summary>
    public static PixelShop.PackCost[] ParsePrice(string text, PixelClicker clicker, string where)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Trim().Equals("free", StringComparison.OrdinalIgnoreCase)) return new PixelShop.PackCost[0];
        List<PixelShop.PackCost> costs = new List<PixelShop.PackCost>();
        foreach (string partRaw in text.Split(','))
        {
            string part = partRaw.Trim();
            if (part.Length == 0) continue;
            int space = part.IndexOf(' ');
            if (space <= 0 || !PixelClicker.TryParseAmount(part.Substring(0, space), out double amount))
            {
                Debug.LogWarning("ShopText.txt: can't read the price part '" + part + "' of " + where + " (write it like '75 White').");
                return null;
            }
            string currency = part.Substring(space + 1).Trim();
            if (currency.StartsWith("@"))
            {
                costs.Add(new PixelShop.PackCost { amount = amount, minigameCurrency = currency.Substring(1).Trim() });
                continue;
            }
            if (!TryParseType(currency, clicker, out PixelClicker.PixelType type))
            {
                Debug.LogWarning("ShopText.txt: unknown pixel type '" + currency + "' in the price of " + where + ".");
                return null;
            }
            costs.Add(new PixelShop.PackCost { type = type, amount = amount });
        }
        return MergeCosts(costs.ToArray());   // "10 White, 5 White" = 15 White
    }

    /// <summary>The ONE place a price's lines are added up per currency: "10 White, 5 White" -> "15 White", so a price that names a currency
    /// twice is checked and paid as its total (the shop's and the visitors' pay checks call this too). Null lines are dropped. Returns the same
    /// array when no currency repeats (no allocation), otherwise new copies (the item's own price is never changed).</summary>
    public static PixelShop.PackCost[] MergeCosts(PixelShop.PackCost[] costs)
    {
        if (costs == null) return null;
        bool repeats = false;
        for (int i = 0; i < costs.Length && !repeats; i++)
        {
            if (costs[i] == null) { repeats = true; break; }
            for (int j = i + 1; j < costs.Length; j++)
                if (costs[j] != null && SameCurrency(costs[i], costs[j])) { repeats = true; break; }
        }
        if (!repeats) return costs;
        List<PixelShop.PackCost> merged = new List<PixelShop.PackCost>(costs.Length);
        foreach (PixelShop.PackCost c in costs)
        {
            if (c == null) continue;
            PixelShop.PackCost same = merged.Find(m => SameCurrency(m, c));
            if (same != null) same.amount += c.amount;
            else merged.Add(new PixelShop.PackCost { type = c.type, amount = c.amount, minigameCurrency = c.minigameCurrency });
        }
        return merged.ToArray();
    }

    /// <summary>Do two price lines spend the same thing (the same pixel type, or the same material / minigame id)?</summary>
    private static bool SameCurrency(PixelShop.PackCost a, PixelShop.PackCost b)
    {
        bool aPixel = string.IsNullOrEmpty(a.minigameCurrency), bPixel = string.IsNullOrEmpty(b.minigameCurrency);
        if (aPixel != bPixel) return false;
        return aPixel ? a.type == b.type : string.Equals(a.minigameCurrency.Trim(), b.minigameCurrency.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseType(string name, PixelClicker clicker, out PixelClicker.PixelType type)
    {
        string compact = name.Replace(" ", "");
        if (Enum.TryParse(compact, true, out type) && Enum.IsDefined(typeof(PixelClicker.PixelType), type)) return true;
        if (compact.EndsWith("Pixels", StringComparison.OrdinalIgnoreCase)) compact = compact.Substring(0, compact.Length - 6);
        else if (compact.EndsWith("Pixel", StringComparison.OrdinalIgnoreCase)) compact = compact.Substring(0, compact.Length - 5);
        if (Enum.TryParse(compact, true, out type) && Enum.IsDefined(typeof(PixelClicker.PixelType), type)) return true;
        if (clicker != null)   // a display name ("Hornet Nest")
            foreach (PixelClicker.PixelTier t in clicker.Tiers)
                if (t != null && t.displayName != null && t.displayName.Replace(" ", "").Equals(name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)) { type = t.type; return true; }
        return false;
    }

    /// <summary>Costs -> "75 White, 50 Green" (code names, plain numbers).</summary>
    public static string FormatPrice(PixelShop.PackCost[] costs)
    {
        if (costs == null || costs.Length == 0) return "Free";
        StringBuilder sb = new StringBuilder();
        foreach (PixelShop.PackCost c in costs)
        {
            if (c == null) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(c.amount.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ');
            sb.Append(string.IsNullOrEmpty(c.minigameCurrency) ? c.type.ToString() : "@" + c.minigameCurrency);
        }
        return sb.ToString();
    }

    /// <summary>One item's block, as written to the file.</summary>
    public static string Block(string key, string name, string description, PixelShop.PackCost[] price, PixelShop.PackCost[][] levelPrices)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append('[').Append(key).Append("]\n");
        sb.Append("Name: ").Append(name ?? "").Append('\n');
        sb.Append("Description: ").Append((description ?? "").Replace("\r", "").Replace("\n", "\\n")).Append('\n');
        if (levelPrices != null && levelPrices.Length > 0)
            for (int i = 0; i < levelPrices.Length; i++) sb.Append("Price L").Append(i + 1).Append(": ").Append(FormatPrice(levelPrices[i])).Append('\n');
        else sb.Append("Price: ").Append(FormatPrice(price)).Append('\n');
        sb.Append('\n');
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Writing (Editor only)
    // ------------------------------------------------------------------

    private const string Header =
        "# Shop text: the name, description and price of every shop pack and device. Edit, save, and press Play - this file wins over the Inspector.\n" +
        "# Leave a line out (or empty) to keep the coded value. Prices: amount + pixel type, comma separated, e.g. 'Price: 75 White, 50 Green' (1.5K / 2M work; 'Free' = no cost).\n" +
        "# Upgrade packs have one 'Price L1:', 'Price L2:'... line per level. Descriptions are one line: write \\n for a line break; {duration}-style tokens are filled in by the game.\n" +
        "# The [key] line names the item by its ORIGINAL name - don't change it (change 'Name:' instead). New items are added at the end automatically.\n\n";

    /// <summary>Editor only: adds the blocks whose key the file doesn't have yet (creates the file if it is missing). Never changes existing text.</summary>
    public static void AppendMissing(List<KeyValuePair<string, string>> blocks)
    {
#if UNITY_EDITOR
        if (blocks == null || blocks.Count == 0) return;
        string path = System.IO.Path.Combine(Application.dataPath, "Resources", ResourceName + ".txt");
        string existing = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "";
        Dictionary<string, Entry> have = new Dictionary<string, Entry>();
        Parse(existing, have);
        StringBuilder add = new StringBuilder();
        foreach (KeyValuePair<string, string> b in blocks)
            if (!have.ContainsKey(b.Key) && !add.ToString().Contains("[" + b.Key + "]")) add.Append(b.Value);
        if (add.Length == 0) return;
        try
        {
            if (existing.Length == 0) existing = Header;
            else if (!existing.EndsWith("\n")) existing += "\n";
            System.IO.File.WriteAllText(path, existing + add);
            UnityEditor.AssetDatabase.ImportAsset("Assets/Resources/" + ResourceName + ".txt");
            entries = null;   // re-read
        }
        catch (Exception e) { Debug.LogWarning("PixelShopText: couldn't write " + path + ": " + e.Message); }
#endif
    }
}
