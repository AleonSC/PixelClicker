// ---- System notes ----
// PixelModelEditor.cs
//   The MODEL EDITOR (Dev Hub tab 'Models'): load any of the game's code-built 3D models, change it part by part, and SAVE a change report that
//   Claude turns into code (the models are built in code, so the edit is a recipe for the builder, not a new asset).
//   * Pick a model at the top: every device (PixelConsumables.CreateDisplayModel), every pixel block (PixelClicker.CreateDisplayPixel), plus anything
//     another system adds with PixelModelEditor.Register(name, build). It is built in a hidden studio (PixelModelPicture) and shown in the viewport:
//     DRAG to turn it, mouse WHEEL to zoom.
//   * The PARTS list (every piece of the model, indented by depth): click one to select it - it blinks in the viewport for a moment.
//     SLIDERS edit it: Position (X / Y / Z, about the model's size either way from where it was when picked), Rotation (-180..180), Scale (0..3x)
//     and Colour (hue / saturation / brightness); each shows its value beside it. A SEARCH box narrows the model drop-down.
//     Buttons: Hide / Show, Delete (Restore brings it back), Duplicate, Add Cube / Sphere / Cylinder (a new piece inside the selected one), Reset
//     (rebuilds the model as the code makes it).
//   * SAVE writes the report - only what changed: CHANGED (old -> new values), HIDDEN, DELETED, ADDED (type, parent, values) - to
//     <project>/ModelEdits/<model>.txt in the editor (<persistentDataPath>/ModelEdits in a build) and copies it to the clipboard: paste it to Claude
//     (or name the file) and the change goes into the model's builder code. Edits are never applied to the game by themselves.
//   Parts are named by their path from the model's root ('Body/Wall'); a name used twice under one parent gets '#2', '#3'...
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static PixelDevUI;   // the shared dev colours and PlaceTL

public class PixelModelEditor : MonoBehaviour
{
    private const string Key = "medit.model";
    private static PixelModelEditor instance;
    private static readonly List<KeyValuePair<string, Func<Transform, GameObject>>> extraSources = new List<KeyValuePair<string, Func<Transform, GameObject>>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; extraSources.Clear(); }

    /// <summary>Called by the Dev Hub when it builds its tabs.</summary>
    public static void Boot()
    {
        if (instance != null) return;
        new GameObject("Dev Model Editor").AddComponent<PixelModelEditor>();
    }

    /// <summary>Lets another system offer a model to the editor (a name for the list and how to build it under a parent).</summary>
    public static void Register(string name, Func<Transform, GameObject> build)
    {
        extraSources.RemoveAll(s => s.Key == name);
        extraSources.Add(new KeyValuePair<string, Func<Transform, GameObject>>(name, build));
    }

    private sealed class Snap { public string path; public Vector3 pos, euler, scale; public Color color; public bool hasColor, active; }

    private PixelClicker clicker;
    private PixelConsumables consumables;
    private RectTransform root, partsContent;
    private TMP_Dropdown modelDropdown;
    private RawImage viewport;
    private ScrollRect partsScroll;
    private GameObject partsBar;
    private TMP_Text selectedLabel, status;
    // Sliders for the selected part: [0..2] position X/Y/Z, [3..5] rotation, [6..8] scale, [9..11] colour hue / saturation / brightness.
    private readonly Slider[] sliders = new Slider[12];
    private readonly TMP_Text[] sliderValues = new TMP_Text[12];
    private bool settingSliders;
    private float modelRadius = 1f;
    private TMP_InputField searchBox;
    private readonly List<int> shownSources = new List<int>();
    private Button hideButton, deleteButton;
    private bool built, visible;

    private readonly List<KeyValuePair<string, Func<Transform, GameObject>>> sources = new List<KeyValuePair<string, Func<Transform, GameObject>>>();
    private int sourceIndex = -1;
    private Transform model, selected;
    private readonly Dictionary<Transform, Snap> originals = new Dictionary<Transform, Snap>();
    private readonly List<Transform> added = new List<Transform>();
    private readonly Dictionary<Transform, string> addedWhat = new Dictionary<Transform, string>();
    private readonly HashSet<Transform> deleted = new HashSet<Transform>();
    private readonly List<Transform> partOrder = new List<Transform>();
    private readonly List<GameObject> partRows = new List<GameObject>();
    private float yaw = 30f, tilt = 22f, zoom = 1f, blinkUntil;

    private void Awake()
    {
        instance = this;
        clicker = PixelFind.Cached<PixelClicker>();
        root = PixelDevHub.TabRoot("models");
        PixelDevHub.SetTabCallback("models", SetVisible);
    }

    private void OnDestroy()
    {
        PixelModelPicture.Release(Key);
        if (instance == this) instance = null;
    }

    private void SetVisible(bool on)
    {
        visible = on;
        if (!on || root == null) return;
        if (clicker == null) clicker = PixelFind.Cached<PixelClicker>();
        if (consumables == null) consumables = PixelFind.Cached<PixelConsumables>();
        if (!built && clicker != null) Build();
        if (built) RefreshSources();
    }

    // ------------------------------------------------------------------
    // The tab
    // ------------------------------------------------------------------

    private void Build()
    {
        built = true;
        Label(root, "Model", 10f, 8f, 80f, 40f, FontSize, TextColor);
        searchBox = Input(root, "Search", 90f, 8f, 200f, 40f, "Search...");
        PixelUIKit.AddSearchIcon(searchBox);
        searchBox.onValueChanged.AddListener(_ => FillDropdown());
        PixelDevTools.Tip(searchBox.gameObject, "Type part of a name to narrow the list of models (e.g. 'recyc', 'wood').");
        modelDropdown = Dropdown(root, "Model", 298f, 8f, 280f, 40f);
        Button(root, "Load", 586f, 8f, 104f, 40f, Green, () =>
        {
            int pick = PixelUIKit.Original(sourceMap, modelDropdown.value);
            if (pick >= 0 && pick < shownSources.Count) Load(shownSources[pick]);
        });

        // Viewport (drag to turn, wheel to zoom).
        GameObject vp = new GameObject("Viewport", typeof(RectTransform), typeof(RawImage));
        vp.transform.SetParent(root, false);
        viewport = vp.GetComponent<RawImage>();
        viewport.color = Color.clear;
        PlaceTL(viewport.rectTransform, 10f, 58f, 330f, 330f);
        GameObject vpBack = new GameObject("Viewport Back", typeof(RectTransform), typeof(Image));
        vpBack.transform.SetParent(root, false);
        vpBack.transform.SetSiblingIndex(vp.transform.GetSiblingIndex());
        Image back = vpBack.GetComponent<Image>();
        PixelUIKit.StyleBox(back, true);
        back.color = new Color(0.04f, 0.05f, 0.08f, 1f);
        back.raycastTarget = false;
        PlaceTL(back.rectTransform, 10f, 58f, 330f, 330f);
        PixelModelEditorDrag drag = vp.AddComponent<PixelModelEditorDrag>();
        drag.onDrag = d => { yaw -= d.x * 0.45f; tilt = Mathf.Clamp(tilt + d.y * 0.35f, -85f, 85f); PixelModelPicture.MarkDirty(Key); };
        drag.onScroll = s => { zoom = Mathf.Clamp(zoom * (s > 0f ? 1.12f : 1f / 1.12f), 0.3f, 6f); PixelModelPicture.SetZoom(Key, zoom); };
        PixelDevTools.Tip(vp, "Drag to turn the model, mouse wheel to zoom.");

        // Parts list.
        Label(root, "Parts", 350f, 56f, 200f, 28f, FontSize - 2f, DimText);
        partsScroll = PixelUIKit.CreateScrollView(root, "Parts", ButtonColor, 10f, 40f, out partsContent, out partsBar);
        PlaceTL(partsScroll.GetComponent<RectTransform>(), 350f, 86f, 340f, 302f);

        // The selected part.
        selectedLabel = Label(root, "Pick a part in the list", 10f, 398f, 680f, 32f, FontSize, Accent);
        selectedLabel.fontStyle = FontStyles.Bold;
        string[] rows = { "Position", "Rotation", "Scale", "Colour" };
        string[][] axes = { new[] { "X", "Y", "Z" }, new[] { "X", "Y", "Z" }, new[] { "X", "Y", "Z" }, new[] { "Hue", "Sat", "Bri" } };
        for (int r = 0; r < 4; r++)
        {
            float y = 436f + r * 46f;
            Label(root, rows[r], 10f, y, 110f, 40f, FontSize, TextColor);
            for (int a = 0; a < 3; a++)
            {
                int index = r * 3 + a;
                float x = 120f + a * 190f;
                Label(root, axes[r][a], x, y, 44f, 40f, FontSize - 4f, DimText);
                Slider sl = PixelUIKit.CreateSlider(root, rows[r] + " " + axes[r][a], Box, Accent, TextColor);
                PlaceTL(sl.GetComponent<RectTransform>(), x + 40f, y + 4f, 96f, 32f);
                sl.onValueChanged.AddListener(_ => { if (!settingSliders) ApplySliders(index); });
                sliders[index] = sl;
                sliderValues[index] = Label(root, "", x + 138f, y, 52f, 40f, FontSize - 6f, TextColor);
            }
        }
        PixelDevTools.Tip(sliders[0].gameObject, "Position: each slider moves the part along one axis, up to about the model's size either way from where it was when you picked it.");

        hideButton = Button(root, "Hide", 10f, 626f, 160f, 42f, ButtonColor, ToggleHide);
        deleteButton = Button(root, "Delete", 180f, 626f, 160f, 42f, Red, ToggleDelete);
        Button(root, "Duplicate", 350f, 626f, 170f, 42f, ButtonColor, Duplicate);
        Button(root, "Add cube", 10f, 676f, 160f, 42f, ButtonColor, () => AddPrimitive(PrimitiveType.Cube));
        Button(root, "Add sphere", 180f, 676f, 160f, 42f, ButtonColor, () => AddPrimitive(PrimitiveType.Sphere));
        Button(root, "Add cylinder", 350f, 676f, 170f, 42f, ButtonColor, () => AddPrimitive(PrimitiveType.Cylinder));
        Button(root, "Reset model", 530f, 626f, 160f, 42f, ButtonColor, () => Load(sourceIndex));
        Button(root, "Save changes", 530f, 676f, 160f, 42f, Green, Save);
        status = WrapText(root, "Status", 10f, 726f, 680f, 50f, 17f, DimText);
        status.text = "Load a model, pick a part, change it, then Save: the report goes to the ModelEdits folder and the clipboard for Claude.";
        ShowSelected();   // (nothing picked yet: the sliders start greyed out)
    }

    private int[] sourceMap;

    private void RefreshSources()
    {
        sources.Clear();
        if (consumables != null)
        {
            HashSet<PixelConsumables.DeviceKind> seen = new HashSet<PixelConsumables.DeviceKind>();
            for (int i = 0; i < consumables.DeviceCount; i++)
            {
                PixelConsumables.Device d = consumables.GetDevice(i);
                if (d == null || d.attuned || !seen.Add(d.kind)) continue;
                PixelConsumables.DeviceKind kind = d.kind;
                sources.Add(new KeyValuePair<string, Func<Transform, GameObject>>("Device: " + d.displayName, parent => consumables.CreateDisplayModel(kind)));
            }
        }
        if (clicker != null)
            for (int t = 0; t < clicker.Tiers.Length; t++)
            {
                int tier = t;
                sources.Add(new KeyValuePair<string, Func<Transform, GameObject>>("Block: " + clicker.Tiers[t].displayName, parent => clicker.CreateDisplayPixel(tier, parent, 1f)));
            }
        sources.AddRange(extraSources);
        FillDropdown();
    }

    /// <summary>The drop-down lists the models whose name contains the search text (all of them when it is empty).</summary>
    private void FillDropdown()
    {
        string find = searchBox != null ? (searchBox.text ?? "").Trim().ToLowerInvariant() : "";
        shownSources.Clear();
        List<string> labels = new List<string>();
        for (int i = 0; i < sources.Count; i++)
        {
            if (find.Length > 0 && !sources[i].Key.ToLowerInvariant().Contains(find)) continue;
            shownSources.Add(i);
            labels.Add(sources[i].Key);
        }
        modelDropdown.ClearOptions();
        if (labels.Count == 0) { modelDropdown.options.Add(new TMP_Dropdown.OptionData("(no model matches)")); sourceMap = null; }
        else sourceMap = PixelUIKit.AddSortedOptions(modelDropdown, labels);
        modelDropdown.SetValueWithoutNotify(0);
        modelDropdown.RefreshShownValue();
    }

    // ------------------------------------------------------------------
    // Loading / the part list
    // ------------------------------------------------------------------

    private void Load(int index)
    {
        if (index < 0 || index >= sources.Count) return;
        sourceIndex = index;
        StopBlink();
        PixelModelPicture.Release(Key);
        originals.Clear(); added.Clear(); addedWhat.Clear(); deleted.Clear(); selected = null;
        Func<Transform, GameObject> build = sources[index].Value;
        viewport.texture = PixelModelPicture.Get(Key, build, 512, tilt);
        viewport.color = viewport.texture != null ? Color.white : Color.clear;
        model = PixelModelPicture.ModelOf(Key);
        zoom = 1f;
        if (model == null) { status.text = "That model could not be built here (it may need the game to be further along)."; RebuildPartList(); return; }
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) originals[t] = TakeSnap(t);
        Bounds bounds = new Bounds(model.position, Vector3.zero);
        foreach (Renderer rr in model.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(rr.bounds);
        modelRadius = Mathf.Max(0.25f, bounds.extents.magnitude);   // (how far the position sliders reach)
        RebuildPartList();
        ShowSelected();
        status.text = "Loaded " + sources[index].Key + " (" + (originals.Count - 1) + " parts).";
    }

    private Snap TakeSnap(Transform t)
    {
        Snap s = new Snap { path = PathOf(t), pos = t.localPosition, euler = t.localEulerAngles, scale = t.localScale, active = t.gameObject.activeSelf };
        Renderer r = t.GetComponent<Renderer>();
        if (r != null && r.sharedMaterial != null) { s.hasColor = true; s.color = ColorOf(r.sharedMaterial); }
        return s;
    }

    private string PathOf(Transform t)
    {
        if (t == model || t == null) return "(model)";
        List<string> parts = new List<string>();
        while (t != null && t != model)
        {
            string n = t.name;
            if (t.parent != null)
            {
                int same = 0, mine = 0;
                for (int i = 0; i < t.parent.childCount; i++)
                {
                    Transform c = t.parent.GetChild(i);
                    if (c.name != n) continue;
                    same++;
                    if (c == t) mine = same;
                }
                if (same > 1) n += "#" + mine;
            }
            parts.Insert(0, n);
            t = t.parent;
        }
        return string.Join("/", parts);
    }

    private void RebuildPartList()
    {
        foreach (GameObject g in partRows) if (g != null) Destroy(g);
        partRows.Clear();
        partOrder.Clear();
        if (model != null) AddPartRows(model, 0);
        float y = 0f;
        foreach (Transform t in partOrder)
        {
            Transform captured = t;
            int depth = Depth(t);
            string label = new string(' ', depth * 2) + (deleted.Contains(t) ? "<s>" + t.name + "</s>" : t.name) + (added.Contains(t) ? "  <color=#7FD48F>new</color>" : "") + (!t.gameObject.activeSelf && !deleted.Contains(t) ? "  <color=#8F97A8>hidden</color>" : "");
            GameObject row = Button(partsContent, label, 4f, y, 312f, 30f, t == selected ? Accent * 0.6f : RowColor, () => Select(captured), 16f).gameObject;
            TMP_Text text = row.GetComponentInChildren<TMP_Text>();
            if (text != null) { text.alignment = TextAlignmentOptions.MidlineLeft; text.richText = true; }
            partRows.Add(row);
            y += 32f;
        }
        partsContent.sizeDelta = new Vector2(partsContent.sizeDelta.x, y);
        PixelUIKit.UpdateScrollView(partsScroll, partsBar, y, 302f);
    }

    private void AddPartRows(Transform t, int depth)
    {
        if (t != model) partOrder.Add(t);
        for (int i = 0; i < t.childCount; i++) AddPartRows(t.GetChild(i), depth + 1);
    }

    private int Depth(Transform t) { int d = 0; while (t != null && t != model) { d++; t = t.parent; } return Mathf.Max(0, d - 1); }

    private void Select(Transform t)
    {
        StopBlink();
        selected = t;
        blinkUntil = Time.unscaledTime + 1.2f;
        RebuildPartList();
        ShowSelected();
    }

    /// <summary>Makes sure the part that was blinking is drawn again (before another one is picked).</summary>
    private void StopBlink()
    {
        Renderer r = selected != null ? selected.GetComponent<Renderer>() : null;
        if (r != null) r.enabled = true;
    }

    private void ShowSelected()
    {
        bool on = selected != null;
        selectedLabel.text = on ? PathOf(selected) : "Pick a part in the list";
        // Each slider's range is set round the part's values as they are now: position +- about the model's size, rotation -180..180, scale 0..3x.
        settingSliders = true;
        Renderer r = on ? selected.GetComponent<Renderer>() : null;
        bool colored = r != null && r.sharedMaterial != null;
        float span = Mathf.Max(0.25f, modelRadius);
        for (int a = 0; a < 3; a++)
        {
            SetSlider(a, on, on ? selected.localPosition[a] - span : -1f, on ? selected.localPosition[a] + span : 1f, on ? selected.localPosition[a] : 0f);
            SetSlider(3 + a, on, -180f, 180f, on ? Mathf.DeltaAngle(0f, selected.localEulerAngles[a]) : 0f);
            float sc = on ? selected.localScale[a] : 1f;
            SetSlider(6 + a, on, 0.01f, Mathf.Max(0.1f, Mathf.Abs(sc) * 3f), sc);
        }
        Color.RGBToHSV(colored ? ColorOf(r.sharedMaterial) : Color.white, out float h, out float sat, out float v);
        SetSlider(9, colored, 0f, 1f, h); SetSlider(10, colored, 0f, 1f, sat); SetSlider(11, colored, 0f, 1f, v);
        settingSliders = false;
        RefreshSliderValues();
        if (hideButton != null) hideButton.GetComponentInChildren<TMP_Text>().text = on && !selected.gameObject.activeSelf && !deleted.Contains(selected) ? "Show" : "Hide";
        if (deleteButton != null) deleteButton.GetComponentInChildren<TMP_Text>().text = on && deleted.Contains(selected) ? "Restore" : "Delete";
    }

    // ------------------------------------------------------------------
    // Editing
    // ------------------------------------------------------------------

    private void SetSlider(int i, bool on, float min, float max, float value)
    {
        Slider sl = sliders[i];
        sl.interactable = on;
        sl.minValue = min;
        sl.maxValue = Mathf.Max(min + 0.0001f, max);
        sl.SetValueWithoutNotify(Mathf.Clamp(value, sl.minValue, sl.maxValue));
    }

    /// <summary>A slider moved: its row's values go onto the selected part (position / rotation / scale, or the colour from hue / saturation / brightness).</summary>
    private void ApplySliders(int index)
    {
        if (selected == null) return;
        int row = index / 3;
        Vector3 v = new Vector3(sliders[row * 3].value, sliders[row * 3 + 1].value, sliders[row * 3 + 2].value);
        if (row == 0) selected.localPosition = v;
        else if (row == 1) selected.localEulerAngles = v;
        else if (row == 2) selected.localScale = v;
        else
        {
            Renderer r = selected.GetComponent<Renderer>();
            if (r != null)
            {
                Color c = Color.HSVToRGB(v.x, v.y, v.z);
                Material m = r.material;   // (its own copy, so other parts sharing the material keep theirs)
                if (m.HasProperty("_BaseColor")) { c.a = m.GetColor("_BaseColor").a; m.SetColor("_BaseColor", c); }
                if (m.HasProperty("_Color")) { c.a = m.GetColor("_Color").a; m.SetColor("_Color", c); }
            }
        }
        RefreshSliderValues();
        PixelModelPicture.MarkDirty(Key);
    }

    private void RefreshSliderValues()
    {
        for (int i = 0; i < 12; i++)
        {
            if (sliderValues[i] == null) continue;
            if (!sliders[i].interactable) { sliderValues[i].text = ""; continue; }
            sliderValues[i].text = i >= 9 ? Mathf.RoundToInt(sliders[i].value * 100f) + "%" : i >= 3 && i < 6 ? Mathf.RoundToInt(sliders[i].value) + "°" : F(sliders[i].value);
        }
    }

    private void ToggleHide()
    {
        if (selected == null || deleted.Contains(selected)) return;
        selected.gameObject.SetActive(!selected.gameObject.activeSelf);
        PixelModelPicture.MarkDirty(Key);
        RebuildPartList(); ShowSelected();
    }

    private void ToggleDelete()
    {
        if (selected == null) return;
        if (deleted.Remove(selected)) selected.gameObject.SetActive(true);
        else { deleted.Add(selected); selected.gameObject.SetActive(false); }
        PixelModelPicture.MarkDirty(Key);
        RebuildPartList(); ShowSelected();
    }

    private void Duplicate()
    {
        if (selected == null || selected == model) return;
        Transform copy = Instantiate(selected.gameObject, selected.parent).transform;
        copy.name = selected.name + " copy";
        copy.localPosition = selected.localPosition + Vector3.right * 0.1f;
        added.Add(copy);
        addedWhat[copy] = "copy of '" + PathOf(selected) + "'";
        PixelModelPicture.MarkDirty(Key);
        Select(copy);
    }

    private void AddPrimitive(PrimitiveType type)
    {
        if (model == null) return;
        Transform parent = selected != null ? selected : model;
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.name = "New " + type;
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * 0.25f;
        Renderer src = parent.GetComponentInChildren<Renderer>();
        Material m = clicker != null ? clicker.CreateVisualMaterial(new Color(0.85f, 0.85f, 0.85f, 1f), false) : null;
        if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
        else if (src != null) go.GetComponent<Renderer>().sharedMaterial = src.sharedMaterial;
        added.Add(go.transform);
        addedWhat[go.transform] = type.ToString().ToLowerInvariant();
        PixelModelPicture.MarkDirty(Key);
        Select(go.transform);
    }

    // ------------------------------------------------------------------
    // Saving the report
    // ------------------------------------------------------------------

    private void Save()
    {
        if (model == null || sourceIndex < 0) { status.text = "Load a model first."; return; }
        StringBuilder sb = new StringBuilder();
        string name = sources[sourceIndex].Key;
        sb.Append("# MODEL EDIT: ").Append(name).Append("   (saved ").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(" UTC)\n");
        sb.Append("# Made with the Dev Hub Model Editor. Paste this to Claude to put the changes into the model's builder code.\n");
        sb.Append("# Values are LOCAL to the part's parent: pos (x, y, z), rot (Euler degrees), scale (x, y, z), colour hex.\n\n");
        int count = 0;
        foreach (KeyValuePair<Transform, Snap> kv in originals)
        {
            Transform t = kv.Key;
            Snap o = kv.Value;
            if (t == null) continue;
            if (deleted.Contains(t)) { sb.Append("DELETED  ").Append(o.path).Append('\n'); count++; continue; }
            if (o.active && !t.gameObject.activeSelf) { sb.Append("HIDDEN   ").Append(o.path).Append('\n'); count++; }
            else if (!o.active && t.gameObject.activeSelf) { sb.Append("SHOWN    ").Append(o.path).Append('\n'); count++; }
            List<string> diffs = new List<string>();
            if ((t.localPosition - o.pos).sqrMagnitude > 1e-8f) diffs.Add("pos " + V(o.pos) + " -> " + V(t.localPosition));
            if (Quaternion.Angle(Quaternion.Euler(o.euler), t.localRotation) > 0.01f) diffs.Add("rot " + V(o.euler) + " -> " + V(t.localEulerAngles));
            if ((t.localScale - o.scale).sqrMagnitude > 1e-8f) diffs.Add("scale " + V(o.scale) + " -> " + V(t.localScale));
            Renderer r = t.GetComponent<Renderer>();
            if (o.hasColor && r != null && r.sharedMaterial != null)
            {
                Color now = ColorOf(r.sharedMaterial);
                if (ColorUtility.ToHtmlStringRGB(now) != ColorUtility.ToHtmlStringRGB(o.color)) diffs.Add("colour #" + ColorUtility.ToHtmlStringRGB(o.color) + " -> #" + ColorUtility.ToHtmlStringRGB(now));
            }
            if (diffs.Count > 0) { sb.Append("CHANGED  ").Append(o.path).Append("   ").Append(string.Join(" ; ", diffs)).Append('\n'); count++; }
        }
        foreach (Transform t in added)
        {
            if (t == null) continue;
            Renderer r = t.GetComponent<Renderer>();
            sb.Append("ADDED    ").Append(addedWhat.TryGetValue(t, out string what) ? what : "part").Append(" named '").Append(t.name).Append("' under '").Append(PathOf(t.parent))
              .Append("'   pos ").Append(V(t.localPosition)).Append(" ; rot ").Append(V(t.localEulerAngles)).Append(" ; scale ").Append(V(t.localScale));
            if (r != null && r.sharedMaterial != null) sb.Append(" ; colour #").Append(ColorUtility.ToHtmlStringRGB(ColorOf(r.sharedMaterial)));
            if (deleted.Contains(t) || !t.gameObject.activeSelf) sb.Append("   (then deleted / hidden - ignore)");
            sb.Append('\n');
            count++;
        }
        if (count == 0) { status.text = "Nothing has changed yet - there is nothing to save."; return; }

        string text = sb.ToString();
        string folder;
#if UNITY_EDITOR
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ModelEdits"));
#else
        folder = Path.Combine(Application.persistentDataPath, "ModelEdits");
#endif
        string file = Path.Combine(folder, Safe(name) + ".txt");
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(file, text);
            GUIUtility.systemCopyBuffer = text;
            status.text = "Saved " + count + " change" + (count == 1 ? "" : "s") + " to " + file + " and copied it to the clipboard - paste it to Claude.";
        }
        catch (Exception e) { status.text = "Could not save: " + e.Message; }
    }

    private static string Safe(string s)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : c == ' ' || c == '-' ? '_' : '\0');
        return sb.ToString().Replace("\0", "").Trim('_');
    }

    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static string V(Vector3 v) => "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";

    private static Color ColorOf(Material m)
    {
        if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
        if (m.HasProperty("_Color")) return m.GetColor("_Color");
        return Color.white;
    }

    // ------------------------------------------------------------------

    private void Update()
    {
        if (!visible || model == null) return;
        // The selected part blinks for a moment so you can see which one it is.
        if (selected != null && !deleted.Contains(selected))
        {
            Renderer r = selected.GetComponent<Renderer>();
            if (r != null)
            {
                bool show = Time.unscaledTime >= blinkUntil || Mathf.Repeat(Time.unscaledTime * 5f, 1f) < 0.5f;
                if (r.enabled != show) { r.enabled = show; PixelModelPicture.MarkDirty(Key); }
            }
        }
        PixelModelPicture.Render(Key, yaw, tilt);
    }
}

/// <summary>The Model Editor's viewport: drag to turn, wheel to zoom.</summary>
public class PixelModelEditorDrag : MonoBehaviour, IDragHandler, IScrollHandler
{
    public Action<Vector2> onDrag;
    public Action<float> onScroll;
    public void OnDrag(PointerEventData e) { onDrag?.Invoke(e.delta); }
    public void OnScroll(PointerEventData e) { onScroll?.Invoke(e.scrollDelta.y); }
}
