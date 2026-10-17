// ---- System notes ----
// PixelLookStudio.cs
//   The LOOK STUDIO (Dev Hub tab 'Looks'): change any pixel type's look while the game runs, then save it so Claude can make it the official look.
//   * Pick a pixel type at the top. The preview (PixelTierPicture; drag it to turn it) and the cube update a moment after every change; 'Show on cube' makes only
//     that type spawn (PixelClicker.SetDevSpawnTiers) so you can click it and watch its husks. Husks dropped after a change carry the new look (ones already on
//     the floor keep the old one).
//   * The settings list is built BY REFLECTION from PixelLook's public fields: bool = tick box, number = slider + exact number box (limits from its [Range] /
//     [Min], SoftMax for open-ended ones), colour = swatch that unfolds Hue / Saturation / Brightness / Opacity sliders + hex box + eyedropper, enum = drop-down;
//     the field's [Tooltip] is the hover tip. So a NEW PixelLook field shows up here by itself: put its name in a section of Sections (otherwise it lands in 'More').
//     The 'Block' section edits the tier's own colour / see-through / glow (TierFields; the tier colour is also the UI colour).
//   * Undo (JSON snapshots per type), Reset (back to how the type looked when the studio first opened it this session), 'Copy look from...' (another type's look, or blank).
//   * Save writes <persistentDataPath>/LookStudio/<Type>.json (Load saved brings it back; 'Use my saved looks' re-applies every saved file when the game starts) and
//     <Type>.cs.txt = the look as a C# 'new PixelLook { ... }' line (only the fields that differ from a blank look) + the Block values, also copied to the clipboard.
//     Paste that to Claude (or name the file) and it goes into PixelLooks.CreateDefaults + a looksVersion step = the official look for everyone.
//   * Edits are run-time only: leaving Play mode in the Editor drops them unless saved (the PixelClicker component's list is not written back).
//   Phase 1 = the settings looks already have. Own shapes, own textures and own particle effects are later phases (Todo.txt).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PixelDevUI;   // the shared dev colours and PlaceTL

public class PixelLookStudio : MonoBehaviour
{
    private const float W = PixelDevHub.ContentW, H = PixelDevHub.ContentH, RowH = 40f, HeaderH = 38f, Gap = 4f, FontSize = 21f;
    private const float PreviewSize = 200f, ListTop = 318f, RowW = 664f, RebuildDelay = 0.06f, UndoMergeSeconds = 0.75f;
    private const int MaxUndo = 60;
    private const string P = "PixelClicker.Dev.LookStudio.";

    // Which PixelLook fields sit in which section (in this order). Fields not named here go to 'More'.
    private static readonly string[][] Sections =
    {
        new[] { "Colour", "useColor", "color", "alpha", "forceTranslucent" },
        new[] { "Surface", "metallic", "smoothness", "basicSurface", "blockSurface", "streakTexture", "damageCracks", "chromeTexture", "shellTexture",
                "oreTexture", "oreColor", "chestTexture", "meteorTexture", "sunTexture" },
        new[] { "Glow", "emission", "glowScale" },
        new[] { "Edges", "outline", "outlineUsesTierColor", "outlineColor", "outlineBevel", "outlineShadeOfTier", "outlineShade", "outlineThickness", "outlineStrength" },
        new[] { "Shape and motion", "jagged", "blob", "vacuumContainer", "wobble", "wobbleAmount", "wobbleSpeed" },
        new[] { "Face marks", "faceCircles", "faceCircleColor", "faceCircleRadius", "faceRing", "faceRingColor",
                "colorBlindSides", "colorBlindRotation", "colorBlindColor", "colorBlindRadius" },
        new[] { "Flair", "lightning", "lightningCoreColor", "lightningGlowColor", "lightningJitter", "darkMatter", "starCubes", "starColor", "sprout", "shine",
                "flames", "gelFx", "bottleMap", "windFx", "cometTail", "sunFx", "radiation", "farmFx", "electricFx", "obsidianFx" },
        new[] { "Husk behaviour", "customPhysics", "bounce", "friction", "gravityMultiplier", "ignoreViewBounds", "floatAway", "floatAfterBounces", "floatLift",
                "floatDriftSpeed", "splash", "burn", "shatter", "gravityWell", "wellRadius", "wellFlyReach", "wellLifetime", "wellPullSpeed", "wellGrip",
                "wellMaxPixels", "wellRingOpacity", "wellRingColor" },
    };
    private static readonly string[] OpenByDefault = { "Block", "Colour", "Surface", "Glow", "Edges" };
    private static readonly string[] TierFields = { "color", "translucent", "glow", "glowIntensity" };

    // Slider ends for numbers that only have a [Min] (or nothing): the number box still takes anything bigger.
    private static readonly Dictionary<string, float> SoftMax = new Dictionary<string, float>
    {
        { "emission", 3f }, { "glowScale", 3f }, { "glowIntensity", 8f }, { "wobbleSpeed", 12f }, { "gravityMultiplier", 2f }, { "floatLift", 1f },
        { "floatDriftSpeed", 8f }, { "floatAfterBounces", 10f }, { "lightningJitter", 0.4f }, { "wellRadius", 10f }, { "wellFlyReach", 10f },
        { "wellLifetime", 600f }, { "wellPullSpeed", 6f }, { "wellGrip", 20f }, { "wellMaxPixels", 60f }, { "colorBlindRotation", 360f },
    };

    private sealed class Row
    {
        public string key, label, search;
        public RectTransform rect;
        public Action refresh;
        public Row parent;      // the colour row a Hue / Saturation / Brightness / Opacity row belongs to
        public bool expanded;   // colour rows: the four sliders are showing
        public float h, s, v;   // colour rows: the hue is kept here so it isn't lost at zero saturation
    }

    private sealed class Section
    {
        public string name;
        public bool open;
        public RectTransform header;
        public TMP_Text label;
        public readonly List<Row> rows = new List<Row>();
    }

    [Serializable] private class Snap { public string look; public Color tierColor; public bool translucent, glow; public float glowIntensity; }
    [Serializable] private class SavedLook { public string type, savedAt; public PixelLook look; public Color tierColor; public bool translucent, glow; public float glowIntensity; }

    private static PixelLookStudio instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    public static void Boot()
    {
        if (instance != null) return;
        new GameObject("Look Studio").AddComponent<PixelLookStudio>();
    }

    private static string Folder => Path.Combine(Application.persistentDataPath, "LookStudio");

    private PixelClicker clicker;
    private TMP_FontAsset font;
    private RectTransform root, content;
    private ScrollRect scroll;
    private GameObject scrollBar;
    private TMP_Dropdown typeDropdown, copyDropdown;
    private int[] typeMap, copyMap;
    private readonly List<PixelClicker.PixelType> typeChoices = new List<PixelClicker.PixelType>(), copyChoices = new List<PixelClicker.PixelType>();
    private Toggle showOnCube, useSaved;
    private RawImage preview;
    private TMP_Text status;
    private TMP_InputField filter;
    private readonly List<Section> sections = new List<Section>();
    private readonly List<Row> allRows = new List<Row>();

    private bool visible, built, dirty;
    private float rebuildAt, yaw, lastDragTime = -10f;
    private PixelClicker.PixelType currentType;
    private PixelLook currentLook;
    private PixelClicker.PixelTier currentTier;
    private int tierIndex = -1;

    private readonly Dictionary<PixelClicker.PixelType, Snap> originals = new Dictionary<PixelClicker.PixelType, Snap>();
    private readonly Dictionary<PixelClicker.PixelType, List<Snap>> undo = new Dictionary<PixelClicker.PixelType, List<Snap>>();
    private string lastChangeKey;
    private float lastChangeTime;

    private void Awake()
    {
        instance = this;
        clicker = PixelFind.Cached<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        root = PixelDevHub.TabRoot("looks");
        PixelDevHub.SetTabCallback("looks", SetVisible);
    }

    private void Start()
    {
        // Your saved looks come back when the game starts (only while the tick box is on), so you can play with them before they are made official.
        if (PixelPrefs.GetBool(P + "UseSaved", false)) ApplyAllSaved();
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void SetVisible(bool on)
    {
        visible = on;
        if (!on) return;
        if (clicker == null) clicker = PixelFind.Cached<PixelClicker>();
        if (!built && clicker != null && root != null) Build();
        if (built) { RefreshTypeLists(); RefreshAll(); }
    }

    // ------------------------------------------------------------------
    // Building the tab
    // ------------------------------------------------------------------

    private void Build()
    {
        built = true;

        // ---- top: pixel type + show on cube ----
        PixelDevUI.Label(root, "Pixel", 10f, 8f, 70f, 40f, FontSize, TextColor);
        typeDropdown = PixelUIKit.CreateDropdown(font, root, "Type", new Vector2(330f, 40f), Box, Box, TextColor, FontSize);
        PlaceTL(typeDropdown.GetComponent<RectTransform>(), 80f, 8f, 330f, 40f);
        typeDropdown.onValueChanged.AddListener(i => { int o = PixelUIKit.Original(typeMap, i); if (o >= 0 && o < typeChoices.Count) Select(typeChoices[o]); });

        TMP_Text showLabel = PixelDevUI.Label(root, "Show on cube", 424f, 8f, 210f, 40f, FontSize, TextColor);
        showOnCube = PixelUIKit.CreateTickBox(root, 34f, Box, Accent);
        PlaceTL(showOnCube.GetComponent<RectTransform>(), 646f, 11f, 34f, 34f);
        showOnCube.isOn = false;
        showOnCube.onValueChanged.AddListener(_ => ApplyShowOnCube());
        PixelDevTools.Tip(showOnCube.gameObject, "Only this pixel type spawns on the cube (even if it is locked) so you can click it and watch its husks. Untick to go back to normal.");
        PixelDevTools.Tip(showLabel.gameObject, "Only this pixel type spawns on the cube (even if it is locked) so you can click it and watch its husks. Untick to go back to normal.");
        showLabel.raycastTarget = true;

        // ---- preview (drag to turn) ----
        GameObject frame = new GameObject("Preview Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(root, false);
        frame.GetComponent<Image>().color = Box;
        PixelUIKit.StyleBox(frame.GetComponent<Image>(), true);
        PlaceTL(frame.GetComponent<RectTransform>(), 10f, 58f, PreviewSize, PreviewSize);
        GameObject pic = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
        pic.transform.SetParent(frame.transform, false);
        preview = pic.GetComponent<RawImage>();
        PixelUIKit.Stretch(preview.rectTransform);
        PixelDragRelay drag = pic.AddComponent<PixelDragRelay>();
        drag.onDrag = e => { yaw -= e.delta.x * 0.5f; lastDragTime = Time.unscaledTime; };
        PixelDevTools.Tip(pic, "Drag to turn it. It turns by itself again a moment after you let go.");

        // ---- right column: undo / reset / copy from, save / load, use saved, status ----
        const float rx = 222f;
        PixelDevUI.Button(root, "Undo", rx, 58f, 110f, 40f, ButtonColor, Undo);
        PixelDevUI.Button(root, "Reset", rx + 116f, 58f, 110f, 40f, ButtonColor, ResetType);
        copyDropdown = PixelUIKit.CreateDropdown(font, root, "Copy From", new Vector2(236f, 40f), Box, Box, TextColor, FontSize - 2f);
        PlaceTL(copyDropdown.GetComponent<RectTransform>(), rx + 232f, 58f, 236f, 40f);
        copyDropdown.onValueChanged.AddListener(CopyFromChosen);
        PixelDevTools.Tip(copyDropdown.gameObject, "Start from another pixel's look (or a blank one). Undo takes it back.");

        Button save = PixelDevUI.Button(root, "Save + copy code", rx, 106f, 230f, 40f, Green, Save);
        PixelDevTools.Tip(save.gameObject, "Saves this look to a file and copies it as code. Paste it to Claude (or tell Claude the file name) to make it the official look.");
        PixelDevUI.Button(root, "Load saved", rx + 238f, 106f, 230f, 40f, ButtonColor, LoadSaved);

        TMP_Text useLabel = PixelDevUI.Label(root, "Use my saved looks when the game starts", rx, 154f, 420f, 40f, FontSize - 2f, TextColor);
        PixelUIKit.FitOneLine(useLabel);
        useSaved = PixelUIKit.CreateTickBox(root, 34f, Box, Accent);
        PlaceTL(useSaved.GetComponent<RectTransform>(), rx + 434f, 157f, 34f, 34f);
        useSaved.isOn = PixelPrefs.GetBool(P + "UseSaved", false);
        useSaved.onValueChanged.AddListener(on => { PixelPrefs.SetBool(P + "UseSaved", on); if (on) { ApplyAllSaved(); SetStatus("Every saved look is applied now and again each time the game starts."); } });
        PixelDevTools.Tip(useSaved.gameObject, "Every look you saved is put back on its pixel when the game starts, until Claude makes them official.");

        status = PixelDevUI.WrapText(root, "Status", rx, 200f, 468f, 62f, 18f, DimText);

        // ---- filter + the settings list ----
        filter = PixelUIKit.CreateInputField(font, root, "Filter", new Vector2(680f, 38f), Box, TextColor, FontSize - 1f, "Filter settings (glow, edge, colour...)");
        PlaceTL(filter.GetComponent<RectTransform>(), 10f, 270f, 680f, 38f);
        PixelUIKit.AddSearchIcon(filter);
        filter.onValueChanged.AddListener(_ => Layout());

        scroll = PixelUIKit.CreateScrollView(root, "Settings", Accent, 10f, 40f, out content, out scrollBar);
        PlaceTL(scroll.GetComponent<RectTransform>(), 10f, ListTop, 680f, H - ListTop - 8f);

        BuildRows();
        RefreshTypeLists();
        PixelClicker.PixelType start = (PixelClicker.PixelType)PixelPrefs.GetInt(P + "Type", (int)PixelClicker.PixelType.Red);
        if (clicker.IndexOf(start) < 0 && typeChoices.Count > 0) start = typeChoices[0];
        Select(start);
    }

    /// <summary>Fills the type drop-down (every tier) and the 'Copy look from' drop-down (types that have a look + Blank).</summary>
    private void RefreshTypeLists()
    {
        typeChoices.Clear();
        List<string> names = new List<string>();
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
        {
            if (t == null || typeChoices.Contains(t.type)) continue;
            typeChoices.Add(t.type);
            names.Add(string.IsNullOrEmpty(t.displayName) ? t.type.ToString() : t.displayName);
        }
        typeMap = PixelUIKit.AddSortedOptions(typeDropdown, names);
        typeDropdown.SetValueWithoutNotify(PixelUIKit.OptionOf(typeMap, typeChoices.IndexOf(currentType)));

        copyChoices.Clear();
        List<string> copyNames = new List<string> { "Copy look from...", "Blank look" };
        copyChoices.Add(currentType); copyChoices.Add(currentType);   // (placeholders for the two fixed entries)
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
        {
            if (t == null || copyChoices.IndexOf(t.type) >= 2 || clicker.CurrentLook(t.type) == null) continue;
            copyChoices.Add(t.type);
            copyNames.Add(string.IsNullOrEmpty(t.displayName) ? t.type.ToString() : t.displayName);
        }
        copyMap = PixelUIKit.AddSortedOptions(copyDropdown, copyNames, 2);
        copyDropdown.SetValueWithoutNotify(0);
    }

    // ------------------------------------------------------------------
    // The settings rows (built once, by reflection; they read / write whichever type is selected)
    // ------------------------------------------------------------------

    private static bool Supported(Type t) => t == typeof(bool) || t == typeof(float) || t == typeof(int) || t == typeof(Color) || t.IsEnum;

    /// <summary>PixelLook's editable fields in the order they are written in the code.</summary>
    private static List<FieldInfo> LookFields()
    {
        List<FieldInfo> list = new List<FieldInfo>();
        foreach (FieldInfo f in typeof(PixelLook).GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (f.Name != "type" && Supported(f.FieldType) && !f.IsDefined(typeof(NonSerializedAttribute), false)) list.Add(f);
        list.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
        return list;
    }

    /// <summary>'outlineShadeOfTier' -> 'Outline shade of tier'.</summary>
    private static string Nice(string name)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i == 0) { sb.Append(char.ToUpperInvariant(c)); continue; }
            if (char.IsUpper(c) || (char.IsDigit(c) && !char.IsDigit(name[i - 1]))) sb.Append(' ').Append(char.ToLowerInvariant(c));
            else sb.Append(c);
        }
        return sb.ToString().Replace("Fx", " effects").Replace(" fx", " effects");
    }

    private Section AddSection(string name)
    {
        Section s = new Section { name = name, open = PixelPrefs.GetBool(P + "Open." + name, Array.IndexOf(OpenByDefault, name) >= 0) };
        Button b = PixelUIKit.CreateButton(font, content, name + " Header", "", new Vector2(RowW, HeaderH), HeaderColor, TextColor, FontSize);
        s.header = b.GetComponent<RectTransform>();
        s.label = b.GetComponentInChildren<TMP_Text>();
        s.label.alignment = TextAlignmentOptions.MidlineLeft;
        s.label.fontStyle = FontStyles.Bold;
        s.label.rectTransform.offsetMin = new Vector2(14f, 0f);
        b.onClick.AddListener(() =>
        {
            s.open = !s.open;
            PixelPrefs.SetBool(P + "Open." + s.name, s.open);
            PixelAudio.Play("ui_click");
            Layout();
        });
        sections.Add(s);
        return s;
    }

    private void BuildRows()
    {
        Section block = AddSection("Block");
        foreach (string name in TierFields)
        {
            FieldInfo f = typeof(PixelClicker.PixelTier).GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) AddFieldRow(block, f, () => currentTier, name == "color" ? "Block colour (also the UI colour)" : null);
        }

        List<FieldInfo> fields = LookFields();
        HashSet<string> placed = new HashSet<string>();
        foreach (string[] def in Sections)
        {
            Section s = AddSection(def[0]);
            for (int i = 1; i < def.Length; i++)
            {
                FieldInfo f = fields.Find(x => x.Name == def[i]);
                if (f == null) continue;
                AddFieldRow(s, f, () => currentLook, null);
                placed.Add(f.Name);
            }
        }
        Section more = null;
        foreach (FieldInfo f in fields)
        {
            if (placed.Contains(f.Name)) continue;
            if (more == null) more = AddSection("More");
            AddFieldRow(more, f, () => currentLook, null);
        }
    }

    private Row NewRow(Section s, string key, string label, string tip, float indent = 0f)
    {
        GameObject go = new GameObject(label + " Row", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(content, false);
        go.GetComponent<Image>().color = RowColor;
        Row r = new Row { key = key, label = label, search = (label + " " + key + " " + tip + " " + s.name).ToLowerInvariant(), rect = go.GetComponent<RectTransform>() };
        PlaceTL(r.rect, 0f, 0f, RowW, RowH);
        TMP_Text t = PixelDevUI.Label(r.rect, label, 12f + indent, 0f, 300f - indent, RowH, FontSize - 1f, indent > 0f ? DimText : TextColor);
        PixelUIKit.FitOneLine(t, 13f);
        if (!string.IsNullOrEmpty(tip)) PixelDevTools.Tip(go, tip);
        s.rows.Add(r);
        allRows.Add(r);
        return r;
    }

    private void AddFieldRow(Section s, FieldInfo f, Func<object> owner, string labelOverride)
    {
        TooltipAttribute tipAttr = f.GetCustomAttribute<TooltipAttribute>();
        string tip = tipAttr != null ? tipAttr.tooltip : "";
        Row r = NewRow(s, f.Name, labelOverride ?? Nice(f.Name), tip);
        Type t = f.FieldType;

        if (t == typeof(bool))
        {
            Toggle tb = PixelUIKit.CreateTickBox(r.rect, 32f, Box, Accent);
            PlaceTL(tb.GetComponent<RectTransform>(), RowW - 44f, 4f, 32f, 32f);
            tb.onValueChanged.AddListener(on => { PixelAudio.Play("ui_click"); Set(f, owner, on); });
            r.refresh = () => { object o = owner(); if (o != null) tb.SetIsOnWithoutNotify((bool)f.GetValue(o)); };
        }
        else if (t == typeof(float) || t == typeof(int))
        {
            bool whole = t == typeof(int);
            RangeAttribute range = f.GetCustomAttribute<RangeAttribute>();
            MinAttribute min = f.GetCustomAttribute<MinAttribute>();
            float lo = range != null ? range.min : min != null ? min.min : 0f;
            float hi = range != null ? range.max : SoftMax.TryGetValue(f.Name, out float sm) ? sm : Mathf.Max(lo + 1f, 10f);
            bool capped = range != null;

            Slider slider = PixelUIKit.CreateSlider(r.rect, "Slider", Box, Accent, Color.white);
            PlaceTL(slider.GetComponent<RectTransform>(), 318f, 6f, 240f, 28f);
            slider.minValue = lo; slider.maxValue = hi; slider.wholeNumbers = whole;
            TMP_InputField box = PixelUIKit.CreateInputField(font, r.rect, "Number", new Vector2(90f, 32f), Box, TextColor, FontSize - 3f, "");
            PlaceTL(box.GetComponent<RectTransform>(), RowW - 98f, 4f, 90f, 32f);

            Action show = () =>
            {
                object o = owner();
                if (o == null) return;
                float v = whole ? (int)f.GetValue(o) : (float)f.GetValue(o);
                slider.SetValueWithoutNotify(Mathf.Clamp(v, lo, hi));
                box.SetTextWithoutNotify(whole ? ((int)v).ToString(CultureInfo.InvariantCulture) : v.ToString("0.###", CultureInfo.InvariantCulture));
            };
            slider.onValueChanged.AddListener(v =>
            {
                if (whole) Set(f, owner, Mathf.RoundToInt(v)); else Set(f, owner, (float)Math.Round(v, 3));
                show();
            });
            box.onEndEdit.AddListener(text =>
            {
                if (float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                {
                    v = Mathf.Max(lo, capped ? Mathf.Min(v, hi) : v);   // [Range] fields stay inside it; [Min] fields can go past the slider's end
                    if (whole) Set(f, owner, Mathf.RoundToInt(v)); else Set(f, owner, v);
                }
                show();
            });
            r.refresh = show;
        }
        else if (t == typeof(Color))
        {
            Button swatch = PixelUIKit.CreateButton(font, r.rect, "Swatch", "", new Vector2(150f, 32f), Color.white, Color.black, FontSize - 4f);
            PlaceTL(swatch.GetComponent<RectTransform>(), 318f, 4f, 150f, 32f);
            Image swatchImage = swatch.GetComponent<Image>();
            TMP_InputField hex = PixelUIKit.CreateInputField(font, r.rect, "Hex", new Vector2(130f, 32f), Box, TextColor, FontSize - 4f, "#RRGGBBAA");
            PlaceTL(hex.GetComponent<RectTransform>(), 476f, 4f, 130f, 32f);
            Button dropper = PixelUIKit.CreateButton(font, r.rect, "Eyedropper", "", new Vector2(42f, 32f), ButtonColor, TextColor, FontSize);
            PlaceTL(dropper.GetComponent<RectTransform>(), RowW - 50f, 4f, 42f, 32f);
            GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(dropper.transform, false);
            icon.GetComponent<Image>().sprite = PixelUIKit.EyedropperSprite();
            icon.GetComponent<Image>().preserveAspect = true;
            icon.GetComponent<Image>().raycastTarget = false;
            PixelUIKit.Stretch(icon.GetComponent<RectTransform>());
            icon.GetComponent<RectTransform>().offsetMin = new Vector2(6f, 4f);
            icon.GetComponent<RectTransform>().offsetMax = new Vector2(-6f, -4f);
            PixelDevTools.Tip(dropper.gameObject, "Pick a colour from anywhere on the screen (right click cancels).");

            Func<Color> get = () => { object o = owner(); return o != null ? (Color)f.GetValue(o) : Color.white; };
            Action<Color, bool> put = (c, keepHue) =>
            {
                if (!keepHue) { Color.RGBToHSV(c, out float hh, out float ss, out float vv); if (ss > 0.001f && vv > 0.001f) r.h = hh; r.s = ss; r.v = vv; }
                Set(f, owner, c);
            };
            List<Action> childShows = new List<Action>();
            r.refresh = () =>
            {
                Color c = get();
                swatchImage.color = new Color(c.r, c.g, c.b, Mathf.Max(0.25f, c.a));
                hex.SetTextWithoutNotify("#" + ColorUtility.ToHtmlStringRGBA(c));
                Color.RGBToHSV(c, out float hh, out float ss, out float vv);
                if (ss > 0.001f && vv > 0.001f) r.h = hh;
                r.s = ss; r.v = vv;
                foreach (Action a in childShows) a();
            };
            swatch.onClick.AddListener(() => { r.expanded = !r.expanded; PixelAudio.Play("ui_click"); Layout(); });
            hex.onEndEdit.AddListener(text =>
            {
                string h = text.Trim();
                if (!h.StartsWith("#")) h = "#" + h;
                if (ColorUtility.TryParseHtmlString(h, out Color c)) put(c, false);
                r.refresh();
            });
            dropper.onClick.AddListener(() => PixelEyedropper.Begin(c => { c.a = get().a; put(c, false); r.refresh(); }));

            // The four sliders under it (shown while the swatch is unfolded).
            string[] parts = { "Hue", "Saturation", "Brightness", "Opacity" };
            for (int p = 0; p < 4; p++)
            {
                int part = p;
                Row child = NewRow(s, f.Name + "." + parts[p], parts[p], "", 28f);
                child.parent = r;
                child.search = r.search;
                Slider sl = PixelUIKit.CreateSlider(child.rect, "Slider", Box, Accent, Color.white);
                PlaceTL(sl.GetComponent<RectTransform>(), 318f, 6f, RowW - 330f, 28f);
                childShows.Add(() =>
                {
                    float v = part == 0 ? r.h : part == 1 ? r.s : part == 2 ? r.v : get().a;
                    sl.SetValueWithoutNotify(v);
                });
                sl.onValueChanged.AddListener(v =>
                {
                    if (part == 0) r.h = v; else if (part == 1) r.s = v; else if (part == 2) r.v = v;
                    Color c = part == 3 ? get() : Color.HSVToRGB(r.h, r.s, r.v);
                    c.a = part == 3 ? v : get().a;
                    Set(f, owner, c);
                    Color shown = get();
                    swatchImage.color = new Color(shown.r, shown.g, shown.b, Mathf.Max(0.25f, shown.a));
                    hex.SetTextWithoutNotify("#" + ColorUtility.ToHtmlStringRGBA(shown));
                });
            }
        }
        else if (t.IsEnum)
        {
            Array values = Enum.GetValues(t);
            TMP_Dropdown dd = PixelUIKit.CreateDropdown(font, r.rect, "Choice", new Vector2(RowW - 326f, 34f), Box, Box, TextColor, FontSize - 3f, escapeMasks: true);
            PlaceTL(dd.GetComponent<RectTransform>(), 318f, 3f, RowW - 326f, 34f);
            List<string> names = new List<string>();
            foreach (object v in values) names.Add(Nice(v.ToString()));
            dd.ClearOptions();
            dd.AddOptions(names);
            dd.onValueChanged.AddListener(i => { if (i >= 0 && i < values.Length) Set(f, owner, values.GetValue(i)); });
            r.refresh = () =>
            {
                object o = owner();
                if (o == null) return;
                dd.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(values, f.GetValue(o))));
            };
        }
    }

    /// <summary>Places the section headers and the rows that should show (open sections, or every row matching the filter).</summary>
    private void Layout()
    {
        if (!built) return;
        string q = filter != null ? filter.text.Trim().ToLowerInvariant() : "";
        bool filtering = q.Length > 0;
        float y = 0f;
        foreach (Section s in sections)
        {
            int matches = 0;
            foreach (Row r in s.rows) if (r.parent == null && (!filtering || r.search.Contains(q))) matches++;
            bool showHeader = !filtering || matches > 0;
            s.header.gameObject.SetActive(showHeader);
            s.label.text = (s.open || filtering ? "-  " : "+  ") + s.name + "  (" + matches + ")";
            if (showHeader) { PlaceTL(s.header, 0f, y, RowW, HeaderH); y += HeaderH + Gap; }

            foreach (Row r in s.rows)
            {
                bool show = showHeader && (s.open || filtering) && (!filtering || r.search.Contains(q)) && (r.parent == null || r.parent.expanded);
                r.rect.gameObject.SetActive(show);
                if (!show) continue;
                PlaceTL(r.rect, 0f, y, RowW, RowH);
                y += RowH + Gap;
            }
            if (showHeader) y += Gap;
        }
        PixelUIKit.UpdateScrollView(scroll, scrollBar, y, H - ListTop - 8f);
    }

    private void RefreshAll()
    {
        foreach (Row r in allRows) r.refresh?.Invoke();
        Layout();
        if (clicker != null && !clicker.UseLooks) SetStatus("Pixel Looks are switched OFF on the PixelClicker component ('Use Looks'), so nothing you change here shows.");
    }

    private void SetStatus(string text) { if (status != null) status.text = text; }

    // ------------------------------------------------------------------
    // Selecting a type, changing values, undo
    // ------------------------------------------------------------------

    private void Select(PixelClicker.PixelType type)
    {
        int index = clicker.IndexOf(type);
        if (index < 0) return;
        currentType = type;
        tierIndex = index;
        currentTier = clicker.Tiers[index];
        currentLook = clicker.EditableLook(type);
        if (!originals.ContainsKey(type)) originals[type] = Take();
        lastChangeKey = null;
        PixelPrefs.SetInt(P + "Type", (int)type);
        if (typeDropdown != null) typeDropdown.SetValueWithoutNotify(PixelUIKit.OptionOf(typeMap, typeChoices.IndexOf(type)));
        preview.texture = PixelTierPicture.Get(clicker, index, 400);
        ApplyShowOnCube();
        MarkDirty();
        RefreshAll();
        SetStatus(File.Exists(SavedPath(type, ".json")) ? "There is a saved look for this pixel ('Load saved' puts it back)." : "Change anything below: the preview and the cube follow.");
    }

    private void ApplyShowOnCube()
    {
        if (clicker == null || showOnCube == null) return;
        if (showOnCube.isOn) clicker.SetDevSpawnTiers(new[] { currentType });
        else if (clicker.HasDevSpawn) clicker.SetDevSpawnTiers(null);
    }

    private void Set(FieldInfo f, Func<object> owner, object value)
    {
        object o = owner();
        if (o == null) return;
        string key = f.DeclaringType.Name + "." + f.Name;
        if (key != lastChangeKey || Time.unscaledTime - lastChangeTime > UndoMergeSeconds) PushUndo();   // one undo step per control (a slider drag is one step)
        lastChangeKey = key;
        lastChangeTime = Time.unscaledTime;
        f.SetValue(o, value);
        MarkDirty();
    }

    private void MarkDirty()
    {
        if (!dirty) rebuildAt = Time.unscaledTime + RebuildDelay;   // while a slider is dragged it rebuilds a few times a second, not every frame
        dirty = true;
    }

    private Snap Take() => new Snap
    {
        look = JsonUtility.ToJson(currentLook),
        tierColor = currentTier.color, translucent = currentTier.translucent, glow = currentTier.glow, glowIntensity = currentTier.glowIntensity,
    };

    private void Put(Snap s)
    {
        JsonUtility.FromJsonOverwrite(s.look, currentLook);
        currentLook.type = currentType;
        currentTier.color = s.tierColor;
        currentTier.translucent = s.translucent;
        currentTier.glow = s.glow;
        currentTier.glowIntensity = s.glowIntensity;
        lastChangeKey = null;
        MarkDirty();
        RefreshAll();
    }

    private void PushUndo()
    {
        if (!undo.TryGetValue(currentType, out List<Snap> stack)) undo[currentType] = stack = new List<Snap>();
        stack.Add(Take());
        if (stack.Count > MaxUndo) stack.RemoveAt(0);
    }

    private void Undo()
    {
        if (currentLook == null) return;
        if (!undo.TryGetValue(currentType, out List<Snap> stack) || stack.Count == 0) { SetStatus("Nothing to undo."); return; }
        Snap s = stack[stack.Count - 1];
        stack.RemoveAt(stack.Count - 1);
        Put(s);
        SetStatus("Undone (" + stack.Count + " more step" + (stack.Count == 1 ? "" : "s") + ").");
    }

    private void ResetType()
    {
        if (currentLook == null || !originals.TryGetValue(currentType, out Snap s)) return;
        PushUndo();
        Put(s);
        SetStatus("Back to how it looked when you opened it (Undo takes the reset back).");
    }

    private void CopyFromChosen(int option)
    {
        int o = PixelUIKit.Original(copyMap, option);
        copyDropdown.SetValueWithoutNotify(0);
        if (o <= 0 || o >= copyChoices.Count || currentLook == null) return;
        PixelLook source = o == 1 ? new PixelLook() : clicker.CurrentLook(copyChoices[o]);
        if (source == null) return;
        PushUndo();
        string json = JsonUtility.ToJson(source);
        JsonUtility.FromJsonOverwrite(json, currentLook);
        currentLook.type = currentType;
        lastChangeKey = null;
        MarkDirty();
        RefreshAll();
        SetStatus(o == 1 ? "Started from a blank look." : "Copied the look of " + copyChoices[o] + " (the Block colour / glow stay this pixel's own).");
    }

    // ------------------------------------------------------------------
    // Save / load / export as code
    // ------------------------------------------------------------------

    private static string SavedPath(PixelClicker.PixelType type, string ext) => Path.Combine(Folder, type + ext);

    private void Save()
    {
        if (currentLook == null) return;
        try
        {
            Directory.CreateDirectory(Folder);
            SavedLook saved = new SavedLook
            {
                type = currentType.ToString(), savedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC", look = currentLook,
                tierColor = currentTier.color, translucent = currentTier.translucent, glow = currentTier.glow, glowIntensity = currentTier.glowIntensity,
            };
            File.WriteAllText(SavedPath(currentType, ".json"), JsonUtility.ToJson(saved, true));
            string code = ExportCode();
            File.WriteAllText(SavedPath(currentType, ".cs.txt"), code);
            GUIUtility.systemCopyBuffer = code;
            SetStatus("Saved and copied! File: LookStudio/" + currentType + ".cs.txt (in the save folder). Paste it to Claude to make it official.");
            RefreshTypeLists();
        }
        catch (Exception e)
        {
            SetStatus("Could not save: " + e.Message);
        }
    }

    private void LoadSaved()
    {
        if (currentLook == null) return;
        string path = SavedPath(currentType, ".json");
        if (!File.Exists(path)) { SetStatus("No saved look for this pixel yet."); return; }
        PushUndo();
        if (ApplySavedFile(path)) { lastChangeKey = null; MarkDirty(); RefreshAll(); SetStatus("Loaded your saved look (Undo takes it back)."); }
        else SetStatus("That saved file could not be read.");
    }

    /// <summary>Puts one saved file's look (and Block values) on its pixel type. False if the file is broken or the type is gone.</summary>
    private bool ApplySavedFile(string path)
    {
        try
        {
            SavedLook saved = JsonUtility.FromJson<SavedLook>(File.ReadAllText(path));
            if (saved == null || saved.look == null || !Enum.TryParse(saved.type, out PixelClicker.PixelType type)) return false;
            int index = clicker.IndexOf(type);
            if (index < 0) return false;
            PixelLook look = clicker.EditableLook(type);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(saved.look), look);
            look.type = type;
            PixelClicker.PixelTier tier = clicker.Tiers[index];
            tier.color = saved.tierColor;
            tier.translucent = saved.translucent;
            tier.glow = saved.glow;
            tier.glowIntensity = saved.glowIntensity;
            PixelTierPicture.Rebuild(clicker, index);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void ApplyAllSaved()
    {
        if (clicker == null) clicker = PixelFind.Cached<PixelClicker>();
        if (clicker == null || !Directory.Exists(Folder)) return;
        foreach (string path in Directory.GetFiles(Folder, "*.json")) ApplySavedFile(path);
        clicker.RefreshLooks();
        if (built) RefreshAll();
    }

    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture) + "f";

    private static string Code(object v)
    {
        switch (v)
        {
            case bool b: return b ? "true" : "false";
            case float f: return F(f);
            case int i: return i.ToString(CultureInfo.InvariantCulture);
            case Color c: return "new Color(" + F(c.r) + ", " + F(c.g) + ", " + F(c.b) + ", " + F(c.a) + ")";
        }
        Type t = v.GetType();
        return (t.DeclaringType != null ? t.DeclaringType.Name + "." : "") + t.Name + "." + v;
    }

    private static bool Same(object a, object b)
    {
        if (a is float fa && b is float fb) return Mathf.Abs(fa - fb) < 0.0005f;
        if (a is Color ca && b is Color cb) return Mathf.Abs(ca.r - cb.r) < 0.002f && Mathf.Abs(ca.g - cb.g) < 0.002f && Mathf.Abs(ca.b - cb.b) < 0.002f && Mathf.Abs(ca.a - cb.a) < 0.002f;
        return Equals(a, b);
    }

    /// <summary>The look as a C# line for PixelLooks.CreateDefaults (only the fields that differ from a blank look), plus the Block (tier) values.</summary>
    private string ExportCode()
    {
        PixelLook blank = new PixelLook();
        StringBuilder sb = new StringBuilder();
        sb.Append("// Look Studio export - ").Append(currentTier.displayName).Append(" (PixelType.").Append(currentType).Append("), ")
          .Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")).AppendLine(" UTC");
        sb.AppendLine("// Look (for PixelLooks.CreateDefaults):");
        sb.Append("new PixelLook { type = PixelClicker.PixelType.").Append(currentType);
        foreach (FieldInfo f in LookFields())
        {
            object v = f.GetValue(currentLook);
            if (Same(v, f.GetValue(blank))) continue;
            sb.Append(", ").Append(f.Name).Append(" = ").Append(Code(v));
        }
        sb.AppendLine(" },");

        originals.TryGetValue(currentType, out Snap was);
        sb.Append("// Block (tier) values: color = ").Append(Code(currentTier.color)).Append(", translucent = ").Append(Code(currentTier.translucent))
          .Append(", glow = ").Append(Code(currentTier.glow)).Append(", glowIntensity = ").Append(Code(currentTier.glowIntensity));
        if (was != null)
        {
            List<string> changed = new List<string>();
            if (!Same(was.tierColor, currentTier.color)) changed.Add("color");
            if (was.translucent != currentTier.translucent) changed.Add("translucent");
            if (was.glow != currentTier.glow) changed.Add("glow");
            if (!Same(was.glowIntensity, currentTier.glowIntensity)) changed.Add("glowIntensity");
            sb.Append(changed.Count > 0 ? "   (changed: " + string.Join(", ", changed) + ")" : "   (unchanged)");
        }
        sb.AppendLine();
        return sb.ToString();
    }

    // ------------------------------------------------------------------

    private void Update()
    {
        if (!visible || !built || clicker == null) return;
        if (dirty && Time.unscaledTime >= rebuildAt)
        {
            dirty = false;
            PixelTierPicture.Rebuild(clicker, tierIndex);
            clicker.RefreshLooks();   // the cube shows the change too (when it is showing this type)
        }
        if (Time.unscaledTime - lastDragTime > 1.5f) yaw += Time.unscaledDeltaTime * 40f;
        if (tierIndex >= 0) PixelTierPicture.Render(tierIndex, yaw);
    }
}
