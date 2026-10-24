// ---- System notes ----
// PixelCraftingRecipes.cs
//   Part of PixelCrafting (partial class): finding recipes (plain, attuned, combo), pricing them, the recipe / Craft refresh and Craft itself. The system notes are in PixelCrafting.cs.
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
    // Recipes
    // ------------------------------------------------------------------

    private static bool Matches(Ingredient ing, Item item) => !item.combo && !ing.combo && ing.kind == item.kind && (ing.kind == ItemKind.Device ? ing.device == item.device : ing.type == item.type);

    private Recipe FindRecipe(Item x, Item y)
    {
        foreach (Recipe r in recipes)
        {
            if (r == null) continue;
            if ((Matches(r.a, x) && Matches(r.b, y)) || (Matches(r.a, y) && Matches(r.b, x))) return ScaleRecipe(r);
        }
        Recipe attune = FindAttuneRecipe(x, y);
        if (attune != null) return attune;
        return comboCraftingEnabled ? ScaleRecipe(FindComboRecipe(x, y)) : null;
    }

    /// <summary>A Vacuum Device, Fan or Magnet plus a (single-type) potion makes an ATTUNED device that only affects that pixel type.</summary>
    private Recipe FindAttuneRecipe(Item x, Item y)
    {
        Item device, potion;
        if (x.kind == ItemKind.Device && y.kind == ItemKind.Potion) { device = x; potion = y; }
        else if (y.kind == ItemKind.Device && x.kind == ItemKind.Potion) { device = y; potion = x; }
        else return null;
        if (potion.combo || !PixelConsumables.CanAttune(device.device)) return null;
        return new Recipe
        {
            label = "Attuned Device",
            a = new Ingredient(device.device, 1),
            b = new Ingredient(ItemKind.Potion, potion.type, 1),
            resultKind = ResultKind.Attuned,
            resultDevice = device.device,
            resultPotion = potion.type,
        };
    }

    /// <summary>
    /// A copy of the recipe with today's prices (see <see cref="recipesFollowShopPrices"/>): the potion's own pixel costs a share
    /// of its shop price (for a combo potion: the second pixel, priced like that pixel's potion), other pixels scale with Value.
    /// </summary>
    private Recipe ScaleRecipe(Recipe r)
    {
        if (r == null || !recipesFollowShopPrices || clicker == null || consumables == null) return r;

        bool isPotion = r.resultKind == ResultKind.Potion;
        bool isCombo = r.resultKind == ResultKind.Combo || r.resultKind == ResultKind.Triple;
        PixelClicker.PixelType priced = r.resultKind == ResultKind.Triple ? r.resultThird : isCombo ? r.resultSecond : r.resultPotion;
        bool pricedDone = false;

        Ingredient Scale(Ingredient ing)
        {
            Ingredient copy = new Ingredient(ing.kind, ing.type, ing.amount)
            {
                combo = ing.combo, second = ing.second, hasThird = ing.hasThird, third = ing.third, device = ing.device,
            };
            if (ing.kind != ItemKind.Pixel) return copy;
            if ((isPotion || isCombo) && !pricedDone && ing.type == priced)
            {
                pricedDone = true; // only the first one (the Glass potion uses Glass twice; the second is the "glass" part)
                copy.amountOverride = Math.Max(1d, Math.Ceiling(consumables.PotionShopPrice(priced) * craftPotionPriceFraction));
            }
            else
            {
                copy.amountOverride = Math.Ceiling(ing.amount * clicker.ValueMultiplier(clicker.IndexOf(ing.type)));
            }
            return copy;
        }

        return new Recipe
        {
            label = r.label,
            a = Scale(r.a),
            b = Scale(r.b),
            resultKind = r.resultKind,
            resultPotion = r.resultPotion,
            resultSecond = r.resultSecond,
            resultThird = r.resultThird,
            resultDevice = r.resultDevice,
            resultAmount = r.resultAmount,
        };
    }

    /// <summary>
    /// A potion plus ANY pixel makes a 2-type combo potion; a 2-type combo potion plus one more pixel makes a 3-type combo potion.
    /// Vacuum (and the Dragon Cubes) can't be part of a combo. (Built on the fly, so it isn't in the Recipes list.)
    /// </summary>
    private Recipe FindComboRecipe(Item x, Item y)
    {
        Item potion, pixel;
        if (x.kind == ItemKind.Potion && y.kind == ItemKind.Pixel) { potion = x; pixel = y; }
        else if (y.kind == ItemKind.Potion && x.kind == ItemKind.Pixel) { potion = y; pixel = x; }
        else return null;

        if (potion.hasThird) return null;
        if (potion.type == pixel.type || (potion.combo && potion.second == pixel.type)) return null;
        if (PixelClicker.IsRareCube(pixel.type) || pixel.type == PixelClicker.PixelType.Vacuum || potion.type == PixelClicker.PixelType.Vacuum) return null;
        if (clicker.IndexOf(pixel.type) < 0) return null;

        if (potion.combo)
        {
            if (potion.second == PixelClicker.PixelType.Vacuum) return null;
            if (consumables != null && consumables.FindComboPotion(potion.type, potion.second, pixel.type) < 0) return null;   // no such triple (a skill block can only be in 2-type combos)
            return new Recipe
            {
                label = "Triple Combo Potion",
                a = IngredientOf(potion, 1),
                b = new Ingredient(ItemKind.Pixel, pixel.type, comboPixelCost),
                resultKind = ResultKind.Triple,
                resultPotion = potion.type,
                resultSecond = potion.second,
                resultThird = pixel.type,
            };
        }

        return new Recipe
        {
            label = "Combo Potion",
            a = new Ingredient(ItemKind.Potion, potion.type, 1),
            b = new Ingredient(ItemKind.Pixel, pixel.type, comboPixelCost),
            resultKind = ResultKind.Combo,
            resultPotion = potion.type,
            resultSecond = pixel.type,
        };
    }

    private int ResultIndex(Recipe r) =>
        r.resultKind == ResultKind.Potion ? FindPotion(r.resultPotion)
        : r.resultKind == ResultKind.Combo ? (consumables != null ? consumables.FindComboPotion(r.resultPotion, r.resultSecond) : -1)
        : r.resultKind == ResultKind.Attuned ? (consumables != null ? consumables.FindAttunedDevice(r.resultDevice, r.resultPotion) : -1)
        : r.resultKind == ResultKind.Triple ? (consumables != null ? consumables.FindComboPotion(r.resultPotion, r.resultSecond, r.resultThird) : -1)
        : FindDevice(r.resultDevice);

    private static bool SameIngredient(Ingredient a, Ingredient b) => SameItem(ItemOf(a), ItemOf(b));

    private bool CanAfford(Recipe r)
    {
        Item ia = ItemOf(r.a), ib = ItemOf(r.b);
        if (SameIngredient(r.a, r.b)) return Have(ia) >= r.a.Amount + r.b.Amount;
        return Have(ia) >= r.a.Amount && Have(ib) >= r.b.Amount;
    }

    private string NeedLine(Ingredient ing, double have, double need)
    {
        Color c = have >= need ? enoughColor : shortColor;
        return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" +
               string.Format(needFormat, ItemName(ItemOf(ing)), PixelClicker.FormatNumber(have),
                             PixelClicker.FormatNumber(need)) + "</color>";
    }

    private Recipe currentRecipe;

    private void Refresh()
    {
        RefreshLevelBar();
        // An item that's used up leaves its box.
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].has && !slots[i].pinned && Have(slots[i].item) < 1d)
            {
                if (slots[i].icon != null) Destroy(slots[i].icon);
                slots[i].icon = null;
                slots[i].has = false;
                slots[i].label.text = "";
            }

        RefreshList();

        currentRecipe = null;
        bool canCraft = false;
        int possibleCrafts = 0;

        if (!slots[0].has || !slots[1].has)
        {
            recipeText.text = HintText;
        }
        else
        {
            Recipe r = FindRecipe(slots[0].item, slots[1].item);
            int result = r != null ? ResultIndex(r) : -1;
            if (r == null) recipeText.text = noRecipeText;
            else if (result < 0) recipeText.text = unavailableText;
            else
            {
                currentRecipe = r;
                Item ia = ItemOf(r.a), ib = ItemOf(r.b);
                StringBuilder sb = new StringBuilder();
                sb.Append(string.Format(makesFormat, consumables.ItemName(result), r.resultAmount)).Append('\n');
                if (SameIngredient(r.a, r.b))
                    sb.Append(NeedLine(r.a, Have(ia), r.a.Amount + r.b.Amount));
                else
                    sb.Append(NeedLine(r.a, Have(ia), r.a.Amount)).Append('\n').Append(NeedLine(r.b, Have(ib), r.b.Amount));
                bool full = consumables.ItemRoom(result) < r.resultAmount;
                if (full) sb.Append('\n').Append("<color=#" + ColorUtility.ToHtmlStringRGB(shortColor) + ">" + potionFullText + "</color>");
                recipeText.text = sb.ToString();
                canCraft = !full && CanAfford(r);
                if (canCraft) possibleCrafts = MaxCraftable(r, result);
            }
        }

        craftButton.interactable = canCraft;
        if (craftLit != (canCraft ? 1 : 0))   // (only on a change: SetRole searches every button)
        {
            craftLit = canCraft ? 1 : 0;
            PixelUIKit.SetRole(craftImage, canCraft ? ButtonRole.Success : ButtonRole.Neutral);
        }
        int times = craftAmount == 0 ? possibleCrafts : Mathf.Min(craftAmount, possibleCrafts);
        string craftTextNow = canCraft && times > 1 ? craftText + " x" + times : craftText;
        if (craftLabel != null && craftLabel.text != craftTextNow) craftLabel.text = craftTextNow;
        if (litAmount != craftAmount)
        {
            litAmount = craftAmount;
            for (int q = 0; q < amountImages.Length; q++)
                PixelUIKit.SetRole(amountImages[q], (q == 0 && craftAmount == 1) || (q == 1 && craftAmount == 5) || (q == 2 && craftAmount == 0) ? ButtonRole.Selected : ButtonRole.Neutral);
        }
    }

    /// <summary>How many times this recipe can be crafted right now (what you hold and the room for the result; at most 1000).</summary>
    private int MaxCraftable(Recipe r, int result)
    {
        Item ia = ItemOf(r.a), ib = ItemOf(r.b);
        double n;
        if (SameIngredient(r.a, r.b)) n = Math.Floor(Have(ia) / Math.Max(1e-9, r.a.Amount + r.b.Amount));
        else n = Math.Min(Math.Floor(Have(ia) / Math.Max(1e-9, r.a.Amount)), Math.Floor(Have(ib) / Math.Max(1e-9, r.b.Amount)));
        double room = consumables.ItemRoom(result) / (double)Math.Max(1, r.resultAmount);
        return (int)Math.Max(0d, Math.Min(Math.Min(n, room), 1000d));
    }

    private void Craft()
    {
        Recipe r = currentRecipe;
        if (r == null || !CanAfford(r)) return;
        int result = ResultIndex(r);
        if (result < 0 || consumables.ItemRoom(result) < r.resultAmount) return;

        int possible = MaxCraftable(r, result);
        int times = craftAmount == 0 ? possible : Math.Min(craftAmount, possible);
        int made = 0;
        for (int i = 0; i < times; i++)
        {
            if (!CanAfford(r) || consumables.ItemRoom(result) < r.resultAmount) break;
            if (!Spend(r.a) || !Spend(r.b)) break;
            consumables.AddItem(result, r.resultAmount);
            made++;
        }
        if (made <= 0) return;
        bool firstTime = PixelStats.Total(RecipeKey(r)) <= 0d && PixelStats.Total(LegacyRecipeKey(r)) <= 0d;
        PixelStats.Count(RecipeKey(r), made); // the recipe goes into the recipe book
        AddCraftingXp(XpPerCraft(r) * made + (firstTime ? (xpDiscovery > 0 ? xpDiscovery : 20) : 0));

        statusLabel.text = string.Format(craftedFormat, consumables.ItemName(result), r.resultAmount * made);
        statusTimer = 2.5f;
        Crafted?.Invoke();
        PlaySound();
        Refresh();
    }
}
