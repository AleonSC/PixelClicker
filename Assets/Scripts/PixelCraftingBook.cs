// ---- System notes ----
// PixelCraftingBook.cs
//   Part of PixelCrafting (partial class): the Recipe Book side panel (known recipes, search, rows) and closing the window. The system notes are in PixelCrafting.cs.
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
    // Recipe book
    // ------------------------------------------------------------------

    private GameObject bookObject;
    private RectTransform bookContent;
    private GameObject bookBar;
    private ScrollRect bookScroll;
    private readonly List<GameObject> bookRows = new List<GameObject>();
    private string bookSearch = "";
    private TMP_InputField bookSearchField;

    private bool BookOpen => bookObject != null && bookObject.activeSelf;

    /// <summary>Stat counter that remembers a recipe was made (saved with the other stats). Combo recipes are remembered by their types, whichever way round they were crafted.</summary>
    private static string RecipeKey(Recipe r)
    {
        if (r.resultKind == ResultKind.Combo || r.resultKind == ResultKind.Triple)
        {
            List<int> types = new List<int> { (int)r.resultPotion, (int)r.resultSecond };
            if (r.resultKind == ResultKind.Triple) types.Add((int)r.resultThird);
            types.Sort();
            return "recipe.c." + string.Join(".", types);
        }
        return LegacyRecipeKey(r);
    }

    private static string LegacyRecipeKey(Recipe r) =>
        "recipe." + (int)r.resultKind + "." + (int)r.resultPotion + "." + (int)r.resultSecond + "." + (int)r.resultDevice;

    private bool RecipeKnown(Recipe r)
    {
        if (PixelStats.Total(RecipeKey(r)) > 0d || PixelStats.Total(LegacyRecipeKey(r)) > 0d) return true;
        return false;   // the book starts empty: a recipe is only written down once it was crafted
    }

    /// <summary>
    /// Every recipe the book lists: the Recipes list, one for every 2-type combo potion, and the 3-type combo potions you have made
    /// (there are far too many of those to list as ???).
    /// </summary>
    /// <summary>The recipes defined in the list (not the automatic combos), read-only (the game database reads it).</summary>
    public IReadOnlyList<Recipe> DefinedRecipes => recipes;

    private List<Recipe> AllRecipes()
    {
        List<Recipe> list = new List<Recipe>();
        foreach (Recipe r in recipes) if (r != null && RecipeKnown(r)) list.Add(r);
        if (comboCraftingEnabled && consumables != null)
            for (int i = 0; i < consumables.ItemCount && !consumables.IsDevice(i); i++)
            {
                if (!consumables.ItemCraftOnly(i)) continue;
                Item potion = new Item(ItemKind.Potion, consumables.ItemRequiredType(i), true, consumables.ItemSecondType(i), consumables.ItemHasThird(i), consumables.ItemThirdType(i));
                List<Recipe> ways = Decompositions(potion);
                if (ways.Count == 0) continue;
                if (!RecipeKnown(ways[0])) continue;   // listed once made
                list.Add(ways[0]);
            }
        return list;
    }

    private string ResultName(Recipe r)
    {
        int result = ResultIndex(r);
        string name = result >= 0 && consumables != null ? consumables.ItemName(result) : r.label;
        return r.resultAmount > 1 ? name + " x" + r.resultAmount : name;
    }

    private string IngredientText(Ingredient ing) =>
        ItemName(ItemOf(ing)) + " x" + PixelClicker.FormatNumber(ing.Amount);

    /// <summary>A small hand-drawn book (cover, pages, spine) on a button in the crafting window's left header slot.</summary>
    private void BuildBookButton()
    {
        Button b = windowParts.AddHeaderButton("Book Button", "", true);   // the left header slot
        b.onClick.AddListener(OpenBook);

        Image Part(string name, Color color, Vector2 size, Vector2 pos)
        {
            GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(b.transform, false);
            Image im = g.GetComponent<Image>();
            im.color = color;
            im.raycastTarget = false;
            RectTransform pr = g.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = size;
            pr.anchoredPosition = pos;
            return im;
        }
        Part("Cover", new Color(0.62f, 0.32f, 0.16f, 1f), new Vector2(42f, 50f), Vector2.zero);
        Part("Pages", new Color(0.95f, 0.92f, 0.8f, 1f), new Vector2(34f, 42f), new Vector2(2f, 0f));
        Part("Spine", new Color(0.4f, 0.2f, 0.1f, 1f), new Vector2(7f, 50f), new Vector2(-17f, 0f));
        Part("Line 1", new Color(0.5f, 0.45f, 0.35f, 1f), new Vector2(22f, 3f), new Vector2(4f, 10f));
        Part("Line 2", new Color(0.5f, 0.45f, 0.35f, 1f), new Vector2(22f, 3f), new Vector2(4f, 2f));
        Part("Line 3", new Color(0.5f, 0.45f, 0.35f, 1f), new Vector2(22f, 3f), new Vector2(4f, -6f));
    }

    /// <summary>The recipe book: a panel to the right of the Crafting window (as tall as it, as wide as the Materials list on the other side).</summary>
    private void BuildBook()
    {
        bookObject = new GameObject("Recipe Book", typeof(RectTransform), typeof(Image));
        bookObject.transform.SetParent(windowObject.transform, false);
        PixelUIKit.StyleWindow(bookObject.GetComponent<Image>(), WindowShade.Window);   // (the theme window colour, live)
        RectTransform wr = bookObject.GetComponent<RectTransform>();
        wr.anchorMin = new Vector2(1f, 0f);
        wr.anchorMax = new Vector2(1f, 1f);
        wr.pivot = new Vector2(0f, 0.5f);
        wr.sizeDelta = new Vector2(SideWidth, 0f);
        wr.anchoredPosition = new Vector2(SideGap, 0f);

        float titleH = titleFontSize * 1.4f;
        TMP_Text title = MakeLabel(bookObject.transform, "Title", bookTitle, titleFontSize * 0.7f, TextAlignmentOptions.Center, FontStyles.Bold);
        PixelUIKit.Caps(title);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        float closeSize = PixelUIKit.WindowCloseSize;
        tr.sizeDelta = new Vector2(-2f * (closeSize + PixelUIKit.WindowCloseOffset.x + 8f), titleH);   // clear of the X on both sides
        tr.anchoredPosition = new Vector2(0f, -20f);
        title.enableAutoSizing = true;   // (a narrow panel: the title shrinks rather than wrapping)
        title.fontSizeMax = titleFontSize * 0.7f;
        title.fontSizeMin = 14f;

        // The X in the standard window look (size, place and grey from PixelUIKit's window constants).
        Button close = PixelUIKit.CreateButton(font, bookObject.transform, "Close", "X", new Vector2(closeSize, closeSize),
                                               ButtonRole.Neutral, closeSize * 0.4f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-PixelUIKit.WindowCloseOffset.x, -PixelUIKit.WindowCloseOffset.y);
        close.onClick.AddListener(CloseBook);

        // Search box under the title
        float searchH = 56f;
        bookSearchField = PixelUIKit.CreateInputField(font, bookObject.transform, "Search", new Vector2(0f, searchH), slotColor, textColor, fontSize * 0.8f, "Search recipes...");
        PixelUIKit.AddSearchIcon(bookSearchField);
        RectTransform sr = bookSearchField.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0f, 1f);
        sr.anchorMax = new Vector2(1f, 1f);
        sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(-40f, searchH);
        sr.anchoredPosition = new Vector2(0f, -(20f + titleH + 6f));
        bookSearchField.onValueChanged.AddListener(text =>
        {
            bookSearch = (text ?? "").Trim().ToLowerInvariant();
            if (BookOpen) RebuildBook();
        });

        bookScroll = PixelUIKit.CreateScrollView(bookObject.transform, "Recipe List", scrollbarColor, 12f, BookRowHeight * 0.6f,
                                                 out bookContent, out bookBar);
        RectTransform vr = bookScroll.GetComponent<RectTransform>();
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = new Vector2(20f, 20f);
        vr.offsetMax = new Vector2(-20f, -(20f + titleH + 6f + searchH + 10f));

        bookObject.SetActive(false);
    }

    private float BookRowHeight => Mathf.Max(bookRowHeight, fontSize * 2.6f);   // two lines: the result and what it is made of

    /// <summary>One single-line, left-aligned label of a recipe row between two heights (0..1) of the row.</summary>
    private TMP_Text BookLine(Transform parent, string text, float size, FontStyles style, float alpha, float yMin, float yMax)
    {
        TMP_Text label = MakeLabel(parent, "Line", text, size, TextAlignmentOptions.MidlineLeft, style);
        label.richText = true;
        label.enableAutoSizing = true;
        label.fontSizeMax = size;
        label.fontSizeMin = 9f;
        label.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
        label.raycastTarget = false;
#if UNITY_2023_1_OR_NEWER
        label.textWrappingMode = TextWrappingModes.NoWrap;
#else
        label.enableWordWrapping = false;
#endif
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0f, yMin);
        lr.anchorMax = new Vector2(1f, yMax);
        lr.offsetMin = new Vector2(16f, 2f);
        lr.offsetMax = new Vector2(-12f, -2f);
        return label;
    }

    private void OpenBook()
    {
        if (BookOpen) { CloseBook(); return; }
        if (bookSearchField != null && bookSearchField.text.Length > 0) bookSearchField.SetTextWithoutNotify("");
        bookSearch = "";
        RebuildBook();
        bookObject.SetActive(true);
        PixelAudio.Play("ui_click");
    }

    private void CloseBook()
    {
        if (bookObject != null) bookObject.SetActive(false);
    }

    private void RebuildBook()
    {
        foreach (GameObject old in bookRows) if (old != null) Destroy(old);
        bookRows.Clear();

        List<Recipe> all = AllRecipes();
        float y = 0f;
        foreach (Recipe raw in all)
        {
            bool known = RecipeKnown(raw);
            Recipe r = known ? ScaleRecipe(raw) : raw;
            if (bookSearch.Length > 0)
            {
                // Only recipes you know can be searched (the others are ???).
                if (!known) continue;
                string haystack = (ResultName(r) + " " + IngredientText(r.a) + " " + IngredientText(r.b)).ToLowerInvariant();
                if (!haystack.Contains(bookSearch)) continue;
            }

            GameObject row = new GameObject("Recipe", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(bookContent, false);
            row.GetComponent<Image>().color = cellColor;
            row.GetComponent<Image>().raycastTarget = known;
            if (known)
            {
                // Click a known recipe to put its ingredients in the two boxes.
                Recipe clicked = r;
                // A click (not a drag handler) so the list still scrolls when dragged; FillBoxes plays the click sound.
                PixelUIKit.MakeClickable(row, () => FillBoxes(ScaleRecipe(clicked)), sound: false, pressTint: false);
            }
            RectTransform rr = row.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0f, 1f);
            rr.anchorMax = new Vector2(1f, 1f);
            rr.pivot = new Vector2(0.5f, 1f);
            rr.sizeDelta = new Vector2(-24f, BookRowHeight - 8f);
            rr.anchoredPosition = new Vector2(0f, -y);

            // Two tidy left-aligned lines: the result (bold) and, dimmer underneath, what it is made of.
            string title = known ? ResultName(r) : unknownText;
            string made = known ? IngredientText(r.a) + "   +   " + IngredientText(r.b) : unknownText + "   +   " + unknownText;
            TMP_Text top = BookLine(row.transform, title, fontSize * 0.85f, FontStyles.Bold, known ? 1f : 0.45f, 0.5f, 1f);
            TMP_Text bottom = BookLine(row.transform, made, fontSize * 0.68f, FontStyles.Normal, known ? 0.65f : 0.4f, 0f, 0.5f);

            bookRows.Add(row);
            y += BookRowHeight;
        }
        // A line of help at the end of the list (or when nothing matches the search).
        string note = bookRows.Count == 0 ? (bookSearch.Length == 0 ? "Nothing crafted yet. Every recipe you craft is written down here." : "No recipes found.") : bookSearch.Length == 0 ? "Click a recipe to fill the boxes." : "";
        if (note.Length > 0)
        {
            GameObject noteRow = new GameObject("Note", typeof(RectTransform));
            noteRow.transform.SetParent(bookContent, false);
            TMP_Text noteLabel = MakeLabel(noteRow.transform, "Text", note, fontSize * 0.7f, TextAlignmentOptions.Center, FontStyles.Italic);
            noteLabel.color = new Color(textColor.r, textColor.g, textColor.b, 0.6f);
            noteLabel.raycastTarget = false;
            RectTransform nr = noteRow.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0f, 1f);
            nr.anchorMax = new Vector2(1f, 1f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.sizeDelta = new Vector2(-24f, fontSize * 2.6f);
            nr.anchoredPosition = new Vector2(0f, -y);
            PixelUIKit.Stretch(noteLabel.rectTransform);
            bookRows.Add(noteRow);
            y += fontSize * 2.6f;
        }
        PixelUIKit.UpdateScrollView(bookScroll, bookBar, Mathf.Max(0f, y - 8f), bookScroll.GetComponent<RectTransform>().rect.height);
    }

    private void Close()
    {
        if (BookOpen) CloseBook();
        if (dragGhost != null) Destroy(dragGhost);
        PixelPop.Hide(windowObject);
    }
}
