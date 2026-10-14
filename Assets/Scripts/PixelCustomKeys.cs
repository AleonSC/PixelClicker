// ---- System notes ----
// PixelCustomKeys.cs
//   Custom keybinds: bind any item (a potion, a device, a seed, a supply...) or tool (the Pixel hose, the docked menu buttons) to a key of your own. Component added by PixelClicker.Awake; it
//   fires the keys (Update: not while paused, on the title screen, typing or capturing a key) by doing what a My Window favourite does: item:<name> -> PixelConsumables.TryUseItem (drinks the potion,
//   starts placing the device), tool:<id> -> PixelMyWindow.TryGetTool (the hose, or PixelMenuKeys.Click). One potion per key is fine (a black potion on one key, a white one on another).
//   The bindings (target + KeyCode) are saved per save file as JSON in PlayerPrefs PixelClicker.Setting.CustomKeys (listed in PixelSettingsSync, ReloadFromPrefs). A key used by one of the normal
//   actions (Settings > Key Bindings) is refused; a key used by another custom binding moves to the new one. Escape cancels a capture, Backspace / Delete clears the key.
//   The window is the KEYBINDS TAB of My Window (PixelMyWindow builds a container and calls BuildView; Refresh / SetVisible keep it current): a drop-down of everything bindable + Add, then one row
//   per binding (name, key box = click then press a key, X removes it).
// ----------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelCustomKeys : MonoBehaviour
{
    [Serializable] private class Binding { public string target; public int key; }
    [Serializable] private class Store { public Binding[] list = new Binding[0]; }

    private const string Pref = "PixelClicker.Setting.CustomKeys";

    private static PixelCustomKeys instance;
    private readonly List<Binding> bindings = new List<Binding>();
    private int capturing = -1;
    private string status = "";

    // view
    private RectTransform viewRoot;
    private TMP_Dropdown dropdown;
    private TMP_InputField searchField;
    private string search = "";
    private readonly List<string> targets = new List<string>();
    private ScrollRect scroll;
    private RectTransform content;
    private GameObject scrollBar;
    private TMP_Text statusLabel;
    private bool viewVisible, dirty = true;
    private float lastViewHeight;
    private TMP_FontAsset font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private void Awake()
    {
        instance = this;
        Load();
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    public static PixelCustomKeys Instance => instance;

    /// <summary>Re-reads the bindings (a save was loaded and brought its own).</summary>
    public static void ReloadFromPrefs()
    {
        if (instance == null) return;
        instance.Load();
        instance.capturing = -1;
        instance.dirty = true;
    }

    private void Load()
    {
        bindings.Clear();
        string json = PlayerPrefs.GetString(Pref, "");
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            Store s = JsonUtility.FromJson<Store>(json);
            if (s != null && s.list != null) foreach (Binding b in s.list) if (b != null && !string.IsNullOrEmpty(b.target)) bindings.Add(b);
        }
        catch (Exception) { }
    }

    private void Save()
    {
        PlayerPrefs.SetString(Pref, JsonUtility.ToJson(new Store { list = bindings.ToArray() }));
    }

    // ------------------------------------------------------------------
    // Firing
    // ------------------------------------------------------------------

    private void Update()
    {
        if (capturing >= 0) { UpdateCapture(); return; }
        if (viewVisible && (dirty || (scroll != null && Mathf.Abs(scroll.GetComponent<RectTransform>().rect.height - lastViewHeight) > 0.5f))) RebuildRows();
        if (bindings.Count == 0 || PixelKeys.Typing || PixelPauseMenu.IsPaused || PixelTitleScreen.Showing) return;
        foreach (Binding b in bindings)
            if (b.key != (int)KeyCode.None && PixelKeys.KeyPressed((KeyCode)b.key)) { Use(b.target); return; }
    }

    private static void Use(string target)
    {
        if (target.StartsWith("tool:"))
        {
            string id = target.Substring(5);
            if (id == "hose") { PixelBank bank = PixelFind.First<PixelBank>(); if (bank != null) bank.ToggleHose(); }
            else if (PixelMyWindow.TryGetTool(id, out _, out PixelAction action)) PixelMenuKeys.Click(action);
            return;
        }
        PixelConsumables consumables = PixelFind.First<PixelConsumables>();
        if (consumables == null) return;
        string name = target.StartsWith("item:") ? target.Substring(5) : target;
        for (int i = 0; i < consumables.ItemCount; i++)
        {
            if (consumables.ItemName(i) != name) continue;
            if (consumables.ItemOwned(i) <= 0) { PixelHints.Announce("You have no " + name + " left"); return; }
            consumables.TryUseItem(i);
            return;
        }
    }

    // ------------------------------------------------------------------
    // Capturing a key
    // ------------------------------------------------------------------

    private void UpdateCapture()
    {
        if (capturing >= bindings.Count) { capturing = -1; dirty = true; return; }
        if (!PixelKeys.TryCapture(out KeyCode key)) return;
        int index = capturing;
        capturing = -1;
        dirty = true;
        if (key == KeyCode.Escape) { status = ""; return; }
        if (key == KeyCode.Backspace || key == KeyCode.Delete) { bindings[index].key = (int)KeyCode.None; Save(); status = "Key cleared"; return; }
        for (int a = 0; a < PixelKeys.Count; a++)
            if (PixelKeys.Get((PixelAction)a) == key) { status = PixelKeys.KeyName(key) + " is already used by " + ((PixelAction)a) + " (Settings > Key Bindings)"; return; }
        for (int i = 0; i < bindings.Count; i++) if (i != index && bindings[i].key == (int)key) bindings[i].key = (int)KeyCode.None;   // the newest choice takes the key
        bindings[index].key = (int)key;
        Save();
        status = PixelKeys.KeyName(key) + " is set";
    }

    // ------------------------------------------------------------------
    // The Keybinds tab (built into a container My Window owns)
    // ------------------------------------------------------------------

    /// <summary>Builds the tab's contents into 'root' (stretched by My Window under its tabs).</summary>
    public void BuildView(RectTransform root, TMP_FontAsset uiFont)
    {
        viewRoot = root; font = uiFont;
        TMP_Text hint = PixelUIKit.CreateText(font, root, "Hint", "", 20f, TextAlignmentOptions.TopLeft, FontStyles.Italic, new Color(1f, 1f, 1f, 0.7f));
        statusLabel = hint;
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(-8f, 52f); hr.anchoredPosition = new Vector2(0f, -112f);

        // The search bar: filters the drop-down above (what you can add) and the list below (what is bound).
        searchField = PixelUIKit.CreateInputField(font, root, "Search", new Vector2(100f, 44f), new Color(0.12f, 0.15f, 0.22f, 1f), Color.white, 22f, "Search items, tools and keys...");
        RectTransform sfr = searchField.GetComponent<RectTransform>();
        sfr.anchorMin = new Vector2(0f, 1f); sfr.anchorMax = new Vector2(1f, 1f); sfr.pivot = new Vector2(0.5f, 1f);
        sfr.offsetMin = new Vector2(4f, -104f); sfr.offsetMax = new Vector2(-4f, -60f);
        PixelUIKit.AddSearchIcon(searchField);
        searchField.onValueChanged.AddListener(v => { search = (v ?? "").Trim().ToLowerInvariant(); FillTargets(); dirty = true; });

        dropdown = PixelUIKit.CreateDropdown(font, root, "Targets", new Vector2(100f, 50f), new Color(0.2f, 0.3f, 0.5f, 1f), new Color(0.1f, 0.12f, 0.18f, 1f), Color.white, 22f);
        RectTransform dr = dropdown.GetComponent<RectTransform>();
        dr.anchorMin = new Vector2(0f, 1f); dr.anchorMax = new Vector2(1f, 1f); dr.pivot = new Vector2(0.5f, 1f);
        dr.offsetMin = new Vector2(4f, -54f); dr.offsetMax = new Vector2(-124f, -4f);
        Button add = PixelUIKit.CreateButton(font, root, "Add", "Add", new Vector2(110f, 50f), new Color(0.2f, 0.6f, 0.3f, 1f), Color.white, 24f);
        RectTransform ar = add.GetComponent<RectTransform>();
        ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(1f, 1f);
        ar.anchoredPosition = new Vector2(-4f, -4f);
        add.onClick.AddListener(AddSelected);

        scroll = PixelUIKit.CreateScrollView(root, "Bindings", new Color(1f, 1f, 1f, 0.35f), 12f, 60f, out content, out scrollBar);
        RectTransform sr = scroll.GetComponent<RectTransform>();
        sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
        sr.offsetMin = new Vector2(0f, 0f); sr.offsetMax = new Vector2(0f, -150f);
        dirty = true;
    }

    /// <summary>My Window shows / hides the tab: the lists refresh when it opens.</summary>
    public void SetVisible(bool visible)
    {
        viewVisible = visible;
        if (visible) { FillTargets(); dirty = true; }
        else if (capturing >= 0) capturing = -1;
    }

    private void FillTargets()
    {
        if (dropdown == null) return;
        targets.Clear();
        List<string> names = new List<string>();
        foreach (string id in PixelMyWindow.ToolIds())
        {
            PixelMyWindow.TryGetTool(id, out string toolName, out _);
            if (search.Length > 0 && !("tool " + toolName).ToLowerInvariant().Contains(search)) continue;
            names.Add("Tool: " + toolName); targets.Add("tool:" + id);
        }
        PixelConsumables consumables = PixelFind.First<PixelConsumables>();
        if (consumables != null)
            for (int i = 0; i < consumables.ItemCount; i++)
                if (consumables.ItemListable(i) && (search.Length == 0 || consumables.ItemName(i).ToLowerInvariant().Contains(search))) { names.Add(consumables.ItemName(i)); targets.Add("item:" + consumables.ItemName(i)); }
        if (names.Count == 0) { names.Add("(nothing matches)"); }
        int keep = dropdown.value;
        dropdown.ClearOptions();
        dropdown.AddOptions(names);
        dropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, names.Count - 1)));
        dropdown.RefreshShownValue();
    }

    private void AddSelected()
    {
        if (dropdown == null || targets.Count == 0) return;
        string target = targets[Mathf.Clamp(dropdown.value, 0, targets.Count - 1)];
        bindings.Add(new Binding { target = target, key = (int)KeyCode.None });
        Save();
        capturing = bindings.Count - 1;   // press the key right away
        status = "";
        dirty = true;
    }

    private static string DisplayName(string target)
    {
        if (target.StartsWith("tool:")) return PixelMyWindow.TryGetTool(target.Substring(5), out string n, out _) ? "Tool: " + n : target;
        return target.StartsWith("item:") ? target.Substring(5) : target;
    }

    private void RebuildRows()
    {
        dirty = false;
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        float y = 4f, h = 54f;
        for (int i = 0; i < bindings.Count; i++)
        {
            int index = i;
            Binding b = bindings[i];
            if (search.Length > 0 && !DisplayName(b.target).ToLowerInvariant().Contains(search) && !PixelKeys.KeyName((KeyCode)b.key).ToLowerInvariant().Contains(search)) continue;
            GameObject go = new GameObject("Binding " + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(content, false);
            go.GetComponent<Image>().color = new Color(0.14f, 0.17f, 0.26f, 1f);
            PixelUIKit.StyleBox(go.GetComponent<Image>());
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-6f, h); rt.anchoredPosition = new Vector2(0f, -y);

            TMP_Text label = PixelUIKit.CreateText(font, rt, "Name", DisplayName(b.target), 24f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
            label.enableAutoSizing = true; label.fontSizeMax = 24f; label.fontSizeMin = 11f; label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 0f); label.rectTransform.offsetMax = new Vector2(-190f, 0f);

            bool waiting = capturing == i;
            Button keyBox = PixelUIKit.CreateButton(font, rt, "Key", waiting ? "Press a key..." : PixelKeys.KeyName((KeyCode)b.key), new Vector2(120f, 40f),
                                                    waiting ? new Color(0.8f, 0.55f, 0.15f, 1f) : new Color(0.25f, 0.45f, 0.8f, 1f), Color.white, 20f);
            RectTransform kr = keyBox.GetComponent<RectTransform>();
            kr.anchorMin = kr.anchorMax = kr.pivot = new Vector2(1f, 0.5f);
            kr.anchoredPosition = new Vector2(-64f, 0f);
            TMP_Text kl = keyBox.GetComponentInChildren<TMP_Text>();
            kl.enableAutoSizing = true; kl.fontSizeMax = 20f; kl.fontSizeMin = 10f; kl.textWrappingMode = TextWrappingModes.NoWrap;
            keyBox.onClick.AddListener(() => { capturing = index; status = "Press the key for " + DisplayName(bindings[index].target) + " (Esc cancels, Backspace clears)"; dirty = true; });

            Button remove = PixelUIKit.CreateButton(font, rt, "Remove", "X", new Vector2(44f, 40f), new Color(0.55f, 0.22f, 0.2f, 1f), Color.white, 22f);
            RectTransform xr = remove.GetComponent<RectTransform>();
            xr.anchorMin = xr.anchorMax = xr.pivot = new Vector2(1f, 0.5f);
            xr.anchoredPosition = new Vector2(-10f, 0f);
            remove.onClick.AddListener(() => { bindings.RemoveAt(index); capturing = -1; Save(); status = ""; dirty = true; });
            y += h + 6f;
        }
        lastViewHeight = scroll.GetComponent<RectTransform>().rect.height;
        PixelUIKit.UpdateScrollView(scroll, scrollBar, y, lastViewHeight);
        if (statusLabel != null)
            statusLabel.text = !string.IsNullOrEmpty(status) ? status
                : bindings.Count == 0 ? "Pick an item or tool above and press Add, then press the key you want for it."
                : "Click a key box to change its key.";
    }
}
