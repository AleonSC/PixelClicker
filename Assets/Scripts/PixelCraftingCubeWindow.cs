// ---- System notes ----
// PixelCraftingCubeWindow.cs
//   The Crafting Cube's RECIPE PICKER (2026-10-20): opened by a right-click tap on a Crafting Cube (PixelCraftingCube). A standard Fixed window
//   (PixelUIKit.CreateWindow, its own canvas above the device panel, Escape closes it: PixelWindows 60) with a scrolling list of the crafting window's
//   recipes (PixelCrafting.MachineRecipes): each row shows the result's picture (potions: their block's PixelTierPicture), its name and the two
//   ingredients at today's prices; the cube's current recipe is highlighted. Clicking a row sets it (PixelCraftingCube.SetRecipe) and closes the window.
//   The cube calls Changed(this) when its recipe changes and Forget(this) when it is removed.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelCraftingCubeWindow : MonoBehaviour
{
    private static PixelCraftingCubeWindow instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private PixelCraftingCube cube;
    private PixelClicker clicker;
    private TMP_FontAsset font;
    private GameObject panel;
    private RectTransform listContent;
    private ScrollRect scroll;
    private GameObject scrollBar;
    private float viewHeight;
    private bool dirty;
    private readonly List<PixelCrafting.Recipe> list = new List<PixelCrafting.Recipe>();

    private const float Width = 720f, Height = 760f, Pad = 30f, RowH = 92f, Gap = 8f;

    /// <summary>Is the picker open?</summary>
    public static bool IsOpen => instance != null && instance.panel != null && instance.panel.activeSelf;

    /// <summary>Is the picker open for this cube (its book stays open meanwhile)?</summary>
    public static bool IsOpenFor(PixelCraftingCube c) => IsOpen && instance.cube == c;

    /// <summary>Opens the picker for this cube (or switches the open picker to it).</summary>
    public static void Open(PixelCraftingCube c)
    {
        if (c == null) return;
        if (instance == null)
        {
            PixelClicker clicker = PixelFind.Cached<PixelClicker>();
            if (clicker == null || clicker.UIFont == null) return;
            PixelUIKit.EnsureEventSystem();
            GameObject canvas = PixelUIKit.CreateCanvas("Crafting Cube Recipe Picker", 660, new Vector2(1920f, 1080f), true);   // (above the device panel, 650)
            instance = canvas.AddComponent<PixelCraftingCubeWindow>();
            instance.clicker = clicker;
            instance.font = clicker.UIFont;
            instance.Build();
            PixelWindows.Register(instance, 60, () => IsOpen, Close);
        }
        instance.cube = c;
        instance.panel.SetActive(true);
        instance.Refresh();
    }

    /// <summary>Closes the picker.</summary>
    public static void Close()
    {
        if (instance == null || instance.panel == null) return;
        instance.panel.SetActive(false);
        instance.cube = null;
    }

    /// <summary>The cube's recipe changed: redraw the highlight if the picker shows it.</summary>
    public static void Changed(PixelCraftingCube c) { if (instance != null && c != null && instance.cube == c) instance.dirty = true; }

    /// <summary>The cube is going away: close the picker if it shows it.</summary>
    public static void Forget(PixelCraftingCube c) { if (instance != null && instance.cube == c) Close(); }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (instance == this) instance = null;
    }

    private void Build()
    {
        PixelWindowParts parts = PixelUIKit.CreateWindow(font, transform, "Panel", "Choose a recipe", new Vector2(Width, Height), PixelUIKit.WindowKind.Fixed, Close);
        panel = parts.Root;
        TMP_Text hint = PixelUIKit.CreateText(font, panel.transform, "Hint", "Feed the cube husks of the recipe's pixel types (couplings, or drop them on top); other ingredients come from your inventory, and what it makes goes there.",
                                              21f, TextAlignmentOptions.Center, FontStyles.Italic, PixelUITheme.DimTextColor);
        Place(hint.rectTransform, new Vector2(Pad, -parts.HeaderHeight - 4f), new Vector2(Width - 2f * Pad, 60f));
        float top = parts.HeaderHeight + 74f;
        viewHeight = Height - top - Pad;
        scroll = PixelUIKit.CreateScrollView(panel.transform, "Recipes", Color.gray, PixelUIKit.ScrollBarWidth, 30f, out listContent, out scrollBar);
        Place(scroll.GetComponent<RectTransform>(), new Vector2(Pad, -top), new Vector2(Width - 2f * Pad, viewHeight));
        panel.SetActive(false);
    }

    private static void Place(RectTransform r, Vector2 topLeft, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = topLeft;
        r.sizeDelta = size;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (cube == null || PixelCalm.InMenu) { Close(); return; }
        if (dirty) Refresh();
    }

    private void Refresh()
    {
        dirty = false;
        for (int i = listContent.childCount - 1; i >= 0; i--) Destroy(listContent.GetChild(i).gameObject);
        PixelCrafting crafting = cube != null ? cube.Crafting : null;
        if (crafting == null) return;
        crafting.MachineRecipes(list);
        float rowW = Width - 2f * Pad - PixelUIKit.ScrollBarWidth - 8f;
        for (int i = 0; i < list.Count; i++) Row(crafting, i, list[i], rowW);
        if (list.Count == 0)
        {
            TMP_Text none = PixelUIKit.CreateText(font, listContent, "None", "No recipes yet.", 24f, TextAlignmentOptions.Center, FontStyles.Italic, PixelUITheme.DimTextColor);
            Place(none.rectTransform, new Vector2(0f, 0f), new Vector2(rowW, RowH));
        }
        PixelUIKit.UpdateScrollView(scroll, scrollBar, Mathf.Max(1, list.Count) * (RowH + Gap), viewHeight);
    }

    /// <summary>One clickable recipe row: picture, result name, ingredients at today's prices; the cube's current recipe is highlighted.</summary>
    private void Row(PixelCrafting crafting, int index, PixelCrafting.Recipe r, float rowW)
    {
        GameObject go = new GameObject("Recipe", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(listContent, false);
        Image bg = go.GetComponent<Image>();
        bool selected = cube.Recipe == r;
        bg.color = selected ? PixelUITheme.ColorOf(ButtonRole.Selected) : new Color(0f, 0f, 0f, 0.25f);
        PixelUIKit.StyleBox(bg, true);
        Place(go.GetComponent<RectTransform>(), new Vector2(0f, -index * (RowH + Gap)), new Vector2(rowW, RowH));

        float textX = 12f;
        bool potion = r.resultKind == PixelCrafting.ResultKind.Potion || r.resultKind == PixelCrafting.ResultKind.Attuned;
        int tier = potion ? clicker.IndexOf(r.resultPotion) : -1;
        if (tier >= 0)
        {
            GameObject pic = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            pic.transform.SetParent(go.transform, false);
            RawImage img = pic.GetComponent<RawImage>();
            img.texture = PixelTierPicture.Get(clicker, tier, 128);
            img.raycastTarget = false;
            Place(pic.GetComponent<RectTransform>(), new Vector2(10f, -(RowH - 72f) * 0.5f), new Vector2(72f, 72f));
            PixelTierPicture.Render(tier, 30f);
            textX = 94f;
        }
        PixelCrafting.Recipe priced = crafting.PricedRecipe(r);
        TMP_Text name = PixelUIKit.CreateText(font, go.transform, "Name", crafting.MachineResultName(r), 24f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
        Place(name.rectTransform, new Vector2(textX, -8f), new Vector2(rowW - textX - 12f, 36f));
        PixelUIKit.FitOneLine(name, 14f);
        string needs = Part(crafting, priced.a) + "  +  " + Part(crafting, priced.b);
        TMP_Text ing = PixelUIKit.CreateText(font, go.transform, "Ingredients", needs, 19f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, PixelUITheme.DimTextColor);
        Place(ing.rectTransform, new Vector2(textX, -46f), new Vector2(rowW - textX - 12f, 34f));
        PixelUIKit.FitOneLine(ing, 11f);
        PixelCrafting.Recipe pick = r;
        PixelUIKit.MakeClickable(go, () => { if (cube != null) cube.SetRecipe(pick); Close(); }, true, false);
    }

    private static string Part(PixelCrafting crafting, PixelCrafting.Ingredient i) =>
        PixelClicker.FormatNumber(i.Amount) + " " + crafting.IngredientName(i) + (i.kind == PixelCrafting.ItemKind.Pixel ? " (husks)" : "");
}
