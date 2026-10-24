// ---- System notes ----
// PixelCraftingLevel.cs
//   Part of PixelCrafting (partial class): the crafting level (XP, level bar, XP popup) and spending ingredients, the Crafting Cube entry points (MachineRecipes / PricedRecipe / MachineCraft) and the Game Database steps. The system notes are in PixelCrafting.cs.
// ---------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class PixelCrafting
{
    // ------------------------------------------------------------------
    // Crafting level
    // ------------------------------------------------------------------

    private const string XpKey = "crafting.xp";
    private TMP_Text levelLabel;
    private RectTransform levelFill;

    /// <summary>The crafting level (1 and up). Not used for anything yet.</summary>
    public int CraftingLevel => LevelOf(PixelStats.Total(XpKey), out _, out _);

    private int LevelOf(double xp, out double into, out double need)
    {
        double first = xpFirstLevel > 0.5f ? xpFirstLevel : 40d, growth = xpGrowth > 1.001f ? xpGrowth : 1.35d;
        int level = 1;
        need = first;
        into = xp;
        while (into >= need && level < 999)
        {
            into -= need;
            level++;
            need *= growth;
        }
        return level;
    }

    private int XpPerCraft(Recipe r)
    {
        switch (r.resultKind)
        {
            case ResultKind.Combo: return xpCombo > 0 ? xpCombo : 12;
            case ResultKind.Triple: return xpTriple > 0 ? xpTriple : 25;
            case ResultKind.Device:
            case ResultKind.Attuned: return xpDevice > 0 ? xpDevice : 30;
            default: return xpPotion > 0 ? xpPotion : 5;
        }
    }

    private void AddCraftingXp(int amount)
    {
        if (amount <= 0) return;
        int before = CraftingLevel;
        PixelStats.Count(XpKey, amount);
        int after = CraftingLevel;
        if (after > before) PixelHints.Announce("Crafting level " + after + "!", "Your crafting level went up to " + after + ".");
        ShowXpPopup(amount);
        RefreshLevelBar();
    }

    /// <summary>The level bar along the bottom of the window: 'Level N' and the XP into the level, with a filling bar.</summary>
    private void BuildLevelBar()
    {
        GameObject track = new GameObject("Level Bar", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(windowObject.transform, false);
        Image trackImage = track.GetComponent<Image>();
        trackImage.color = new Color(0.05f, 0.05f, 0.08f, 1f);
        trackImage.raycastTarget = false;
        PixelUIKit.StyleBox(trackImage, true);
        RectTransform tr = track.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f);
        tr.anchorMax = new Vector2(1f, 0f);
        tr.pivot = new Vector2(0.5f, 0f);
        tr.offsetMin = new Vector2(28f, 22f);
        tr.offsetMax = new Vector2(-28f, 22f + 46f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = levelBarColor;
        fillImage.raycastTarget = false;
        levelFill = fill.GetComponent<RectTransform>();
        levelFill.anchorMin = Vector2.zero;
        levelFill.anchorMax = new Vector2(0f, 1f);
        levelFill.pivot = new Vector2(0f, 0.5f);
        levelFill.offsetMin = new Vector2(3f, 3f);
        levelFill.offsetMax = new Vector2(-3f, -3f);

        levelLabel = MakeLabel(track.transform, "Text", "", fontSize * 0.8f, TextAlignmentOptions.Center, FontStyles.Bold);
        levelLabel.raycastTarget = false;
        PixelUIKit.Caps(levelLabel);
        PixelUIKit.Stretch(levelLabel.rectTransform);
        RefreshLevelBar();
    }

    private void RefreshLevelBar()
    {
        if (levelLabel == null) return;
        int level = LevelOf(PixelStats.Total(XpKey), out double into, out double need);
        PixelUIKit.SetText(levelLabel, "Crafting level " + level + "   -   " + PixelClicker.FormatNumberShort(Math.Floor(into)) + " / " + PixelClicker.FormatNumberShort(Math.Ceiling(need)) + " XP");
        levelFill.anchorMax = new Vector2(Mathf.Clamp01((float)(into / Math.Max(1d, need))), 1f);
        levelFill.offsetMax = new Vector2(-3f, -3f);
    }

    /// <summary>A '+n XP' that rises over the level bar in its own colour, apart from every other +n in the game.</summary>
    private void ShowXpPopup(int amount)
    {
        if (windowObject == null || !windowObject.activeInHierarchy) return;
        TMP_Text t = MakeLabel(windowObject.transform, "XP Popup", "+" + amount + " XP", fontSize * 1.4f, TextAlignmentOptions.Center, FontStyles.Bold);
        t.color = xpPopupColor;
        t.raycastTarget = false;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(300f, fontSize * 1.8f);
        r.anchoredPosition = new Vector2(UnityEngine.Random.Range(-70f, 70f), 76f);
        StartCoroutine(RiseXpPopup(t, r));
    }

    private IEnumerator RiseXpPopup(TMP_Text t, RectTransform r)
    {
        Vector2 start = r.anchoredPosition;
        float time = 0f;
        const float life = 1.4f;
        while (time < life && t != null)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / life);
            r.anchoredPosition = start + new Vector2(0f, 120f * (1f - (1f - k) * (1f - k)));
            Color c = t.color;
            c.a = k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f;
            t.color = c;
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    private bool Spend(Ingredient ing)
    {
        if (ing.kind == ItemKind.Pixel) return clicker.TrySpend(ing.type, ing.Amount);
        if (ing.kind == ItemKind.Device)
        {
            int dv = FindDevice(ing.device);
            return dv >= 0 && consumables.TryRemoveItem(dv, (int)ing.Amount);
        }
        int p = PotionIndexOf(ItemOf(ing));
        return p >= 0 && consumables.TryRemoveItem(p, (int)ing.Amount);
    }

    // ------------------------------------------------------------------
    // The Crafting Cube (PixelCraftingCube): it crafts the same recipes, paying the PIXEL ingredients with husks fed in through its couplings
    // ------------------------------------------------------------------

    /// <summary>The recipes a Crafting Cube can be set to: the defined (Inspector) recipes whose result exists in the game.</summary>
    public void MachineRecipes(List<Recipe> into)
    {
        into.Clear();
        foreach (Recipe r in recipes)
            if (r != null && ResultIndex(r) >= 0 && !UsesRetiredDevice(r)) into.Add(r);
    }

    private static bool UsesRetiredDevice(Recipe r) => UsesRetired(r.a) || UsesRetired(r.b);

    /// <summary>The recipe with today's prices (the same scaling the crafting window shows).</summary>
    public Recipe PricedRecipe(Recipe r) => ScaleRecipe(r);

    /// <summary>What a recipe makes, for labels ("Glass Potion", "Magnet x2").</summary>
    public string MachineResultName(Recipe r) => r == null ? "?" : ResultName(r);

    /// <summary>An ingredient's name for labels ("Glass", "Glass Potion", "Magnet").</summary>
    public string IngredientName(Ingredient ing) => ing == null ? "?" : ItemName(ItemOf(ing));

    /// <summary>
    /// A Crafting Cube crafts one batch of a PRICED recipe (PricedRecipe): its pixel ingredients were already paid in husks by the cube; potion / device
    /// ingredients come out of the inventory here. False (nothing taken) when those are missing or the result has no room. Counts like a craft in the window
    /// (recipe book, crafting XP, the Crafted event).
    /// </summary>
    public bool MachineCraft(Recipe priced, Recipe defined)
    {
        if (priced == null || consumables == null) return false;
        int result = ResultIndex(priced);
        if (result < 0 || consumables.ItemRoom(result) < priced.resultAmount) return false;
        bool same = SameIngredient(priced.a, priced.b);
        foreach (Ingredient ing in new[] { priced.a, priced.b })
        {
            if (ing.kind == ItemKind.Pixel) continue;
            double need = same ? priced.a.Amount + priced.b.Amount : ing.Amount;
            if (Have(ItemOf(ing)) < need) return false;
            if (same) break;
        }
        foreach (Ingredient ing in new[] { priced.a, priced.b }) if (ing.kind != ItemKind.Pixel && !Spend(ing)) return false;
        consumables.AddItem(result, priced.resultAmount);
        Recipe key = defined ?? priced;
        bool firstTime = PixelStats.Total(RecipeKey(key)) <= 0d && PixelStats.Total(LegacyRecipeKey(key)) <= 0d;
        PixelStats.Count(RecipeKey(key));
        AddCraftingXp(XpPerCraft(key) + (firstTime ? (xpDiscovery > 0 ? xpDiscovery : 20) : 0));
        Crafted?.Invoke();
        return true;
    }

    /// <summary>Is the crafting window open (the Crafting Cube button closes it before placing starts)?</summary>
    public void CloseWindow() => Close();

    private void PlaySound()
    {
        if (craftSound == null) return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        audioSource.PlayOneShot(craftSound, soundVolume);
    }

    // ---- Game Database (IPixelDatabaseSource): one 'Recipe' entry per defined recipe, linked to its ingredient pixels and the potion it makes ----

    void IPixelDatabaseSource.DatabaseSteps(PixelDatabase.Steps steps)
    {
        steps.Add(PixelDatabase.RecipesOrder, b =>
        {
            foreach (Recipe r in DefinedRecipes)
            {
                if (r == null) continue;
                PixelDatabase.Entry re = b.Add("Recipe", string.IsNullOrEmpty(r.label) ? "Recipe" : r.label, "Crafting: makes a " + r.resultKind + (r.resultKind == ResultKind.Device ? "" : " (" + r.resultPotion + ")") + ".");
                foreach (Ingredient ing in new[] { r.a, r.b })
                    if (ing != null && ing.kind != ItemKind.Device) b.Link(re, (ing.kind == ItemKind.Potion ? "Uses potion of" : "Uses pixel"), b.Pixel(ing.type), "Ingredient in recipe");
                if (r.resultKind == ResultKind.Potion || r.resultKind == ResultKind.Combo || r.resultKind == ResultKind.Triple) b.Link(re, "Makes potion of", b.Pixel(r.resultPotion), "Potion made by recipe");
            }
        });
    }
}
