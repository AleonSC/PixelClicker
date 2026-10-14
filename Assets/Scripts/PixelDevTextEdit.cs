// ---- System notes ----
// PixelDevTextEdit.cs
//   Custom text for the Dev Hub (component added by PixelDevHub.Awake): a quick RIGHT CLICK (not a hold: holding is the widget favorite, PixelRightTap tells them apart) on any label in the hub - widget
//   titles and control labels, tab names, tester cards, buttons - opens a small box to type your own text (Save / Reset / Cancel, Enter / Esc). A label is found by its place in the hierarchy
//   (the names of its GameObjects up to the hub canvas), so it needs no registration; labels inside drop-downs / input fields and the search results are left alone. Edits are kept per label path in
//   PlayerPrefs PixelClicker.Dev.CustomText (JSON), which PixelSettingsSync stores in every save profile (and PixelDevTextEdit.ReloadFromPrefs re-reads). Every 2 s while the hub is open (and when it opens)
//   the labels are scanned and the saved texts put on them; each frame the edited ones are kept at their text (code that rewrites a label's text, like 'Clear (3)', cannot undo your edit). Reset
//   brings back the text the label had before it was edited.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelDevTextEdit : MonoBehaviour
{
    private const string Pref = "PixelClicker.Dev.CustomText";

    [Serializable] private class Entry { public string path, text; }
    [Serializable] private class Store { public List<Entry> entries = new List<Entry>(); }

    private static PixelDevTextEdit instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    /// <summary>The saved texts were replaced (a save profile was loaded): read them again.</summary>
    public static void ReloadFromPrefs() { if (instance != null) instance.Load(); }

    private Transform canvasRoot;
    private TMP_FontAsset font;
    private readonly Dictionary<string, string> custom = new Dictionary<string, string>();
    private readonly Dictionary<string, string> original = new Dictionary<string, string>();   // path -> the text before it was edited
    private readonly Dictionary<int, string> pathCache = new Dictionary<int, string>();
    private readonly Dictionary<TMP_Text, string> tracked = new Dictionary<TMP_Text, string>();
    private readonly List<TMP_Text> dead = new List<TMP_Text>();
    private readonly PixelRightTap tap = new PixelRightTap();
    private float scanTimer;

    // the edit box
    private RectTransform box;
    private TMP_InputField input;
    private TMP_Text editing;
    private bool registered;

    public void Setup(Transform canvas, TMP_FontAsset uiFont)
    {
        instance = this;
        canvasRoot = canvas;
        font = uiFont;
        Load();
    }

    private void OnDestroy() { PixelWindows.Unregister(this); if (instance == this) instance = null; }

    // ---------------- storage ----------------

    private void Load()
    {
        custom.Clear();
        try
        {
            Store s = JsonUtility.FromJson<Store>(PlayerPrefs.GetString(Pref, ""));
            if (s != null && s.entries != null) foreach (Entry e in s.entries) if (e != null && !string.IsNullOrEmpty(e.path)) custom[e.path] = e.text ?? "";
        }
        catch (Exception) { }
        // Labels that were edited but no longer have a saved text go back to what they were.
        foreach (KeyValuePair<TMP_Text, string> t in new List<KeyValuePair<TMP_Text, string>>(tracked))
            if (t.Key != null && !custom.ContainsKey(t.Value) && original.TryGetValue(t.Value, out string o)) t.Key.text = o;
        tracked.Clear();
        scanTimer = 0f;
    }

    private void Save()
    {
        Store s = new Store();
        foreach (KeyValuePair<string, string> kv in custom) s.entries.Add(new Entry { path = kv.Key, text = kv.Value });
        PlayerPrefs.SetString(Pref, JsonUtility.ToJson(s));
    }

    // ---------------- finding labels ----------------

    private string PathOf(TMP_Text label)
    {
        int id = label.GetInstanceID();
        if (pathCache.TryGetValue(id, out string cached)) return cached;
        List<string> names = new List<string>();
        for (Transform t = label.transform; t != null && t != canvasRoot; t = t.parent) names.Add(t.name);
        names.Reverse();
        string path = string.Join("/", names);
        pathCache[id] = path;
        return path;
    }

    private bool Editable(TMP_Text label)
    {
        if (label == null || !label.gameObject.activeInHierarchy) return false;
        if (label.GetComponentInParent<TMP_Dropdown>() != null || label.GetComponentInParent<TMP_InputField>() != null) return false;   // captions / items / typed text change by themselves
        if (box != null && label.transform.IsChildOf(box)) return false;
        for (Transform t = label.transform; t != null && t != canvasRoot; t = t.parent)
            if (t.name.StartsWith("Result ") || t.name == "Search Results") return false;
        return true;
    }

    private void Update()
    {
        if (canvasRoot == null || !PixelDevHub.IsOpen) { tap.Update(null); return; }

        // Scan: put saved texts on their labels (a label found for the first time remembers the text it had).
        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = 2f;
            if (custom.Count > 0)
                foreach (TMP_Text label in canvasRoot.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (tracked.ContainsKey(label) || !Editable(label)) continue;
                    string path = PathOf(label);
                    if (!custom.ContainsKey(path)) continue;
                    if (!original.ContainsKey(path)) original[path] = label.text;
                    tracked[label] = path;
                }
        }

        // Right click on a label opens the box; right HOLD stays the favorite (PixelWidgetBoard).
        if (PixelInput.RightPressed() || PixelInput.RightHeld() || tap.Pending)
        {
            TMP_Text under = box != null && box.gameObject.activeSelf ? null : LabelUnderPointer();
            if (tap.Update(under) && under != null) OpenEditor(under);
        }
    }

    private void LateUpdate()
    {
        if (tracked.Count == 0) return;
        dead.Clear();
        foreach (KeyValuePair<TMP_Text, string> kv in tracked)
        {
            if (kv.Key == null) { dead.Add(kv.Key); continue; }
            if (custom.TryGetValue(kv.Value, out string text) && kv.Key.text != text) { kv.Key.text = text; FitIfOneLine(kv.Key); }
        }
        foreach (TMP_Text d in dead) tracked.Remove(d);
    }

    private static void FitIfOneLine(TMP_Text label)
    {
        if (label.rectTransform.rect.height < label.fontSize * 2.4f) PixelUIKit.FitOneLine(label, 10f);
    }

    private TMP_Text LabelUnderPointer()
    {
        if (PixelInput.PointerOverUI() == false && false) return null;
        Vector2 pointer = PixelInput.PointerPosition();
        TMP_Text best = null;
        float bestArea = float.MaxValue;
        foreach (TMP_Text label in canvasRoot.GetComponentsInChildren<TMP_Text>(false))
        {
            if (!Editable(label) || string.IsNullOrEmpty(label.text)) continue;
            RectTransform rt = label.rectTransform;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rt, pointer, null)) continue;
            bool visible = true;   // inside scrolling lists the pointer must be in the visible part
            foreach (ScrollRect sr in label.GetComponentsInParent<ScrollRect>(false))
            {
                RectTransform view = sr.viewport != null ? sr.viewport : sr.GetComponent<RectTransform>();
                if (!RectTransformUtility.RectangleContainsScreenPoint(view, pointer, null)) { visible = false; break; }
            }
            if (!visible) continue;
            float area = rt.rect.width * rt.rect.height * rt.lossyScale.x * rt.lossyScale.y;
            if (area < bestArea) { bestArea = area; best = label; }
        }
        return best;
    }

    // ---------------- the edit box ----------------

    private void OpenEditor(TMP_Text label)
    {
        if (box == null) BuildBox();
        editing = label;
        input.text = label.text;
        box.gameObject.SetActive(true);
        box.SetAsLastSibling();
        // Near the pointer, kept on the canvas.
        RectTransform canvasRect = canvasRoot as RectTransform;
        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local))
        {
            Vector2 half = canvasRect.rect.size * 0.5f, size = box.sizeDelta;
            local.x = Mathf.Clamp(local.x, -half.x + size.x * 0.5f + 10f, half.x - size.x * 0.5f - 10f);
            local.y = Mathf.Clamp(local.y, -half.y + size.y * 0.5f + 10f, half.y - size.y * 0.5f - 10f);
            box.anchoredPosition = local;
        }
        if (!registered) { PixelWindows.Register(this, 230, () => box != null && box.gameObject.activeSelf, CloseBox); registered = true; }
        input.Select();
        input.ActivateInputField();
    }

    private void BuildBox()
    {
        GameObject go = new GameObject("Text Edit Box", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasRoot, false);
        go.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.2f, 0.99f);
        PixelUIKit.StyleWindow(go.GetComponent<Image>(), new Color(0.1f, 0.12f, 0.2f, 0.99f));
        box = go.GetComponent<RectTransform>();
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(560f, 170f);

        TMP_Text title = PixelUIKit.CreateText(font, box, "Title", "EDIT TEXT", 22f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
        title.raycastTarget = false;
        PlaceTL(title.rectTransform, 16f, 8f, 400f, 30f);
        input = PixelUIKit.CreateInputField(font, box, "Edit Field", new Vector2(528f, 44f), new Color(0.05f, 0.06f, 0.09f, 1f), Color.white, 24f, "text");
        PlaceTL(input.GetComponent<RectTransform>(), 16f, 46f, 528f, 44f);
        input.onSubmit.AddListener(_ => Apply());
        MakeButton("Save", "Save", 16f, new Color(0.2f, 0.6f, 0.3f, 1f), Apply);
        MakeButton("Reset", "Reset", 192f, new Color(0.55f, 0.4f, 0.15f, 1f), ResetText);
        MakeButton("Cancel", "Cancel", 368f, new Color(0.45f, 0.2f, 0.2f, 1f), CloseBox);
        box.gameObject.SetActive(false);
    }

    private void MakeButton(string name, string label, float x, Color color, UnityEngine.Events.UnityAction action)
    {
        Button b = PixelUIKit.CreateButton(font, box, name, label, new Vector2(176f, 42f), color, Color.white, 22f);
        PlaceTL(b.GetComponent<RectTransform>(), x, 112f, 160f, 42f);
        b.onClick.AddListener(action);
    }

    private static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
    }

    private void Apply()
    {
        if (editing == null) { CloseBox(); return; }
        string path = PathOf(editing);
        if (!original.ContainsKey(path)) original[path] = editing.text;
        custom[path] = input.text;
        tracked[editing] = path;
        editing.text = input.text;
        FitIfOneLine(editing);
        Save();
        CloseBox();
    }

    private void ResetText()
    {
        if (editing != null)
        {
            string path = PathOf(editing);
            custom.Remove(path);
            tracked.Remove(editing);
            if (original.TryGetValue(path, out string o)) editing.text = o;
            Save();
        }
        CloseBox();
    }

    private void CloseBox()
    {
        if (box != null) box.gameObject.SetActive(false);
        editing = null;
    }
}
