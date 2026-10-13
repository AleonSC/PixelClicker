// ---- System notes ----
// PixelDevInspector.cs
//   'Inspector' (Dev Tools > Tools tick box, Editor / dev builds like the rest of Dev Tools): ONE draggable, minimizable window to change values while playing.
//   * World tab: the whole simulation. Time (timeScale, fixedDeltaTime, targetFrameRate, vSync), Physics (gravity incl. a quick 'Gravity Y', solver iterations,
//     bounce / sleep thresholds, contact offset...), live counts (FPS, old pixels, rigidbodies, awake ones) and a drop-down of every game system in the scene
//     (PixelShop, PixelFloor, PixelPets...) with a Select button that opens that system in the Selected tab.
//   * Selected tab: 'Pick' mode (while on, a left click on anything with a collider selects it instead of clicking the cube: PixelClicker.ExternalClickBlock),
//     Parent / Active / Destroy buttons, a filter box and a foldable list of the object's components. Game scripts show EVERY field of a supported type
//     (numbers, bool, text, enum, Vector2/3, Color; private runtime state included), Unity components show a fixed list (Transform, Rigidbody mass / drag /
//     velocity..., Collider bounciness / friction, Light, Camera, Renderer, AudioSource). An old pixel also shows its tier's data (PixelTier: spawn weight, payout...).
//   * All rows are built by reflection (Member = a getter + setter), values refresh a few times a second (not while you type), enum / bool rows are buttons.
//     'Copy edits' puts every change you made as 'Owner.field = value' lines on the clipboard so a good value can be put into the code.
//   Nothing here is saved in the game save (only open / tab / minimized / position in PlayerPrefs PixelClicker.Dev.Insp*). Edits last until the object or the session ends.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelDevInspector : MonoBehaviour
{
    private const float Width = 700f, Height = PixelDevHub.ContentH, TabsHeight = 50f, StatusHeight = 30f, RowH = 38f, RowGap = 6f, FontSize = 21f, RefreshSeconds = 0.3f;
    private const int MaxRowsPerComponent = 150, EasyOpenCount = 14;
    private const string PrefTab = "PixelClicker.Dev.InspTab";

    private static PixelDevInspector instance;
    /// <summary>The Inspector tab is on screen (the hub is open and the tab is showing / floating).</summary>
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; IsOpen = false; }

    public static void Boot()
    {
        if (instance != null) return;
        instance = new GameObject("Dev Inspector").AddComponent<PixelDevInspector>();
    }

    /// <summary>Called by the Dev Hub when the Inspector tab appears / disappears.</summary>
    public static void SetVisible(bool on)
    {
        Boot();
        instance.Apply(on);
    }

    // ------------------------------------------------------------------
    // The model: a Member is one editable value
    // ------------------------------------------------------------------

    private sealed class Member
    {
        public string label;
        public Type type;
        public Func<object> get;
        public Action<object> set;
        public bool readOnly;
        public string owner = "";
    }

    private sealed class Row
    {
        public Member member;
        public TMP_InputField field;
        public TMP_Text buttonLabel;
    }

    private static bool Supported(Type t)
    {
        return t.IsEnum || t == typeof(float) || t == typeof(int) || t == typeof(bool) || t == typeof(string) || t == typeof(double) || t == typeof(long) ||
               t == typeof(uint) || t == typeof(short) || t == typeof(byte) || t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Color);
    }

    private static string Format(object v)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        if (v == null) return "";
        if (v is float f) return f.ToString("G6", ci);
        if (v is double d) return d.ToString("G9", ci);
        if (v is Vector3 v3) return v3.x.ToString("0.###", ci) + ", " + v3.y.ToString("0.###", ci) + ", " + v3.z.ToString("0.###", ci);
        if (v is Vector2 v2) return v2.x.ToString("0.###", ci) + ", " + v2.y.ToString("0.###", ci);
        if (v is Color c) return "#" + ColorUtility.ToHtmlStringRGBA(c);
        return Convert.ToString(v, ci);
    }

    private static bool TryParse(Type t, string s, out object value)
    {
        value = null;
        s = s.Trim();
        CultureInfo ci = CultureInfo.InvariantCulture;
        if (t == typeof(string)) { value = s; return true; }
        if (t == typeof(float)) { if (!float.TryParse(s, NumberStyles.Float, ci, out float f)) return false; value = f; return true; }
        if (t == typeof(double)) { if (!double.TryParse(s, NumberStyles.Float, ci, out double d)) return false; value = d; return true; }
        if (t == typeof(int) || t == typeof(long) || t == typeof(uint) || t == typeof(short) || t == typeof(byte))
        {
            if (!double.TryParse(s, NumberStyles.Float, ci, out double n)) return false;
            try { value = Convert.ChangeType(Math.Round(n), t, ci); return true; } catch { return false; }
        }
        if (t == typeof(Vector3) || t == typeof(Vector2))
        {
            string[] p = s.Split(',');
            int need = t == typeof(Vector3) ? 3 : 2;
            if (p.Length != need) return false;
            float[] x = new float[need];
            for (int i = 0; i < need; i++) if (!float.TryParse(p[i].Trim(), NumberStyles.Float, ci, out x[i])) return false;
            value = need == 3 ? (object)new Vector3(x[0], x[1], x[2]) : new Vector2(x[0], x[1]);
            return true;
        }
        if (t == typeof(Color))
        {
            if (!ColorUtility.TryParseHtmlString(s.StartsWith("#") ? s : "#" + s, out Color c)) return false;
            value = c;
            return true;
        }
        return false;
    }

    private static Member FromField(object owner, FieldInfo f, string ownerName)
    {
        return new Member { label = f.Name, type = f.FieldType, owner = ownerName, get = () => f.GetValue(owner), set = v => f.SetValue(owner, v) };
    }

    /// <summary>A property of a static class ('Time', 'timeScale') or of an object; null when it does not exist in this Unity version.</summary>
    private static Member FromProperty(object owner, Type type, string name, string label, string ownerName)
    {
        BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        PropertyInfo p = type.GetProperty(name, flags);
        if (p != null && p.CanRead && Supported(p.PropertyType) && p.GetIndexParameters().Length == 0)
            return new Member { label = label ?? name, type = p.PropertyType, owner = ownerName, readOnly = !p.CanWrite, get = () => p.GetValue(owner), set = v => p.SetValue(owner, v) };
        return null;
    }

    /// <summary>A member found by a path like 'material.bounciness' (several alternatives separated by '|', the first one this Unity version has wins).</summary>
    private static Member FromPath(object root, string paths, string ownerName)
    {
        foreach (string alt in paths.Split('|'))
        {
            string[] parts = alt.Split('.');
            object cur = root;
            for (int i = 0; i < parts.Length - 1 && cur != null; i++)
            {
                try { PropertyInfo pp = cur.GetType().GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance); cur = pp != null ? pp.GetValue(cur) : null; }
                catch { cur = null; }
            }
            if (cur == null) continue;
            Member m = FromProperty(cur, cur.GetType(), parts[parts.Length - 1], alt, ownerName);
            if (m != null) return m;
        }
        return null;
    }

    private static readonly string[] TransformPaths = { "localPosition", "localEulerAngles", "localScale" };
    private static readonly string[] RigidbodyPaths =
    {
        "mass", "linearDamping|drag", "angularDamping|angularDrag", "useGravity", "isKinematic", "linearVelocity|velocity", "angularVelocity",
        "maxAngularVelocity", "sleepThreshold", "interpolation", "collisionDetectionMode", "constraints"
    };
    private static readonly string[] ColliderPaths = { "enabled", "isTrigger", "contactOffset", "material.bounciness", "material.dynamicFriction", "material.staticFriction" };
    private static readonly string[] LightPaths = { "enabled", "intensity", "range", "color", "shadowStrength" };
    private static readonly string[] CameraPaths = { "fieldOfView", "orthographic", "orthographicSize", "nearClipPlane", "farClipPlane" };
    private static readonly string[] RendererPaths = { "enabled", "shadowCastingMode" };
    private static readonly string[] AudioPaths = { "enabled", "volume", "pitch", "mute", "loop" };

    private static bool IsGameType(Type t) => t.Assembly == typeof(PixelDevInspector).Assembly;

    /// <summary>Every editable value of one object: all supported fields for a game script / data class, a fixed list for Unity's own components.</summary>
    private static List<Member> CollectMembers(object o, string filter)
    {
        List<Member> list = new List<Member>();
        if (o == null) return list;
        Type t = o.GetType();
        string name = t.Name;
        if (IsGameType(t))
        {
            List<Member> settings = new List<Member>(), state = new List<Member>();
            for (Type k = t; k != null && k != typeof(MonoBehaviour) && k != typeof(object) && k != typeof(Behaviour) && k != typeof(Component); k = k.BaseType)
                foreach (FieldInfo f in k.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsLiteral || f.IsInitOnly || f.Name.IndexOf('<') >= 0 || !Supported(f.FieldType)) continue;
                    bool serialized = f.IsPublic ? f.GetCustomAttribute<NonSerializedAttribute>() == null : f.GetCustomAttribute<SerializeField>() != null;
                    (serialized ? settings : state).Add(FromField(o, f, name));
                }
            list.AddRange(settings);
            list.AddRange(state);
        }
        else
        {
            string[] paths = o is Transform ? TransformPaths : o is Rigidbody ? RigidbodyPaths : o is Collider ? ColliderPaths : o is Light ? LightPaths :
                             o is Camera ? CameraPaths : o is Renderer ? RendererPaths : o is AudioSource ? AudioPaths : null;
            if (paths != null)
                foreach (string p in paths) { Member m = FromPath(o, p, name); if (m != null) list.Add(m); }
        }
        if (!string.IsNullOrEmpty(filter))
            list.RemoveAll(m => m.label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0);
        return list;
    }

    // ------------------------------------------------------------------
    // Window
    // ------------------------------------------------------------------

    private static readonly Color TabOn = new Color(0.25f, 0.45f, 0.8f, 1f), TabOff = new Color(0.2f, 0.24f, 0.34f, 1f),
                                  BoxColor = new Color(0.14f, 0.17f, 0.26f, 1f), TextColor = new Color(0.92f, 0.94f, 1f, 1f),
                                  HeaderRow = new Color(0.22f, 0.3f, 0.46f, 1f), OnColor = new Color(0.2f, 0.55f, 0.3f, 1f), OffColor = new Color(0.35f, 0.25f, 0.25f, 1f);

    private TMP_FontAsset font;
    private GameObject body;
    private RectTransform worldContent, selContent;
    private GameObject worldView, selView, worldBar, selBar;
    private ScrollRect worldScroll, selScroll;
    private Image[] tabImages = new Image[2];
    private TMP_Text pickLabel, selectedLabel, activeLabel, statusLabel;
    private TMP_Dropdown systemsDropdown;
    private TMP_InputField filterField;
    private readonly List<Row> worldRows = new List<Row>(), selRows = new List<Row>();
    private readonly List<Component> systems = new List<Component>();
    private readonly Dictionary<int, bool> openState = new Dictionary<int, bool>();
    private readonly List<string> edits = new List<string>();
    private int tab;
    private bool pickMode, pickBlocking, wasSelected;
    private GameObject selected;
    private string filter = "";
    private float refreshT, countT, fpsSmooth = 60f;
    private int oldCount, bodyCount, awakeCount;

    private void Awake()
    {
        instance = this;
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();
        tab = Mathf.Clamp(PlayerPrefs.GetInt(PrefTab, 0), 0, 1);
        Build();
        PixelDevHub.SetTabCallback("inspector", SetVisible);
    }

    private void OnDestroy()
    {
        if (pickBlocking) PixelClicker.ExternalClickBlock = false;
        if (instance == this) { instance = null; IsOpen = false; }
    }

    private static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private Button MakeButton(Transform parent, string label, float x, float y, float w, float h, Color color, UnityEngine.Events.UnityAction click, float size = FontSize)
    {
        Button b = PixelUIKit.CreateButton(font, parent, label + " Button", label, new Vector2(w, h), color, TextColor, size);
        PlaceTL(b.GetComponent<RectTransform>(), x, y, w, h);
        if (click != null) b.onClick.AddListener(click);
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        if (t != null) { t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = 10f; }
        return b;
    }

    private void Build()
    {
        // The Inspector is a tab of the Dev Hub: its content root is given to it.
        RectTransform root = PixelDevHub.TabRoot("inspector");
        float bodyHeight = Height - StatusHeight;
        body = root.gameObject;
        statusLabel = PixelUIKit.CreateText(font, root, "Status", "Edit a value and press Enter (or click away): the change is applied at once.", FontSize - 4f, TextAlignmentOptions.MidlineLeft, FontStyles.Italic, new Color(0.7f, 0.78f, 0.9f, 1f));
        PlaceTL(statusLabel.rectTransform, 14f, Height - StatusHeight, Width - 28f, StatusHeight);
        statusLabel.raycastTarget = false;
        statusLabel.enableAutoSizing = true; statusLabel.fontSizeMax = FontSize - 4f; statusLabel.fontSizeMin = 10f;

        float tabW = 200f;
        string[] names = { "World", "Selected" };
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            Button b = MakeButton(body.transform, names[i], 10f + i * (tabW + 8f), 6f, tabW, 38f, TabOff, () => SetTab(index), 24f);
            tabImages[i] = b.GetComponent<Image>();
        }
        MakeButton(body.transform, "Copy edits", 10f + 2f * (tabW + 8f), 6f, Width - 20f - 2f * (tabW + 8f), 38f, new Color(0.3f, 0.5f, 0.35f, 1f), CopyEdits, 20f);

        // ---- World tab: a systems drop-down on top, the scroll list below ----
        worldView = new GameObject("World Tab", typeof(RectTransform));
        worldView.transform.SetParent(body.transform, false);
        PlaceTL(worldView.GetComponent<RectTransform>(), 0f, TabsHeight, Width, bodyHeight - TabsHeight);
        TMP_Text sysLabel = PixelUIKit.CreateText(font, worldView.transform, "Systems Label", "System", FontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, TextColor);
        PlaceTL(sysLabel.rectTransform, 14f, 0f, 90f, RowH);
        systemsDropdown = PixelUIKit.CreateDropdown(font, worldView.transform, "Systems Dropdown", new Vector2(330f, RowH), BoxColor, new Color(0.1f, 0.12f, 0.18f, 1f), TextColor, FontSize - 2f);
        PlaceTL(systemsDropdown.GetComponent<RectTransform>(), 104f, 0f, 330f, RowH);
        MakeButton(worldView.transform, "Select", 444f, 0f, Width - 458f, RowH, HeaderRow, SelectSystem);
        worldScroll = PixelUIKit.CreateScrollView(worldView.transform, "World Scroll", new Color(0.5f, 0.5f, 0.6f, 1f), 10f, 30f, out worldContent, out worldBar);
        PlaceTL(worldScroll.GetComponent<RectTransform>(), 0f, RowH + 10f, Width, bodyHeight - TabsHeight - RowH - 14f);

        // ---- Selected tab: pick / parent / active / destroy, the name, a filter, then the scroll list ----
        selView = new GameObject("Selected Tab", typeof(RectTransform));
        selView.transform.SetParent(body.transform, false);
        PlaceTL(selView.GetComponent<RectTransform>(), 0f, TabsHeight, Width, bodyHeight - TabsHeight);
        float bw = (Width - 28f - 3f * 6f) / 4f;
        Button pick = MakeButton(selView.transform, "Pick: off", 14f, 0f, bw, RowH, OffColor, TogglePick, 19f);
        pickLabel = pick.GetComponentInChildren<TMP_Text>();
        MakeButton(selView.transform, "Parent", 14f + (bw + 6f), 0f, bw, RowH, HeaderRow, SelectParent, 19f);
        Button act = MakeButton(selView.transform, "Active", 14f + 2f * (bw + 6f), 0f, bw, RowH, HeaderRow, ToggleActive, 19f);
        activeLabel = act.GetComponentInChildren<TMP_Text>();
        MakeButton(selView.transform, "Destroy", 14f + 3f * (bw + 6f), 0f, bw, RowH, new Color(0.6f, 0.2f, 0.2f, 1f), DestroySelected, 19f);
        selectedLabel = PixelUIKit.CreateText(font, selView.transform, "Selected Name", "Nothing selected - turn Pick on and click something.", FontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, TextColor);
        PlaceTL(selectedLabel.rectTransform, 14f, RowH + 6f, Width - 28f, RowH);
        selectedLabel.raycastTarget = false;
        filterField = PixelUIKit.CreateInputField(font, selView.transform, "Filter", new Vector2(Width - 28f, RowH), BoxColor, TextColor, FontSize, "Filter fields...");
        PlaceTL(filterField.GetComponent<RectTransform>(), 14f, 2f * (RowH + 6f), Width - 28f, RowH);
        PixelUIKit.AddSearchIcon(filterField);
        filterField.gameObject.AddComponent<InspectorCaret>();
        filterField.onValueChanged.AddListener(s => { filter = s.Trim(); RebuildSelected(); });
        float top = 3f * (RowH + 6f) + 4f;
        selScroll = PixelUIKit.CreateScrollView(selView.transform, "Selected Scroll", new Color(0.5f, 0.5f, 0.6f, 1f), 10f, 30f, out selContent, out selBar);
        PlaceTL(selScroll.GetComponent<RectTransform>(), 0f, top, Width, bodyHeight - TabsHeight - top - 4f);

        Layout();
    }

    private void Layout()
    {
        worldView.SetActive(tab == 0);
        selView.SetActive(tab == 1);
        for (int i = 0; i < 2; i++) if (tabImages[i] != null) tabImages[i].color = i == tab ? TabOn : TabOff;
    }

    private void SetTab(int index)
    {
        tab = index;
        PlayerPrefs.SetInt(PrefTab, tab);
        Layout();
        if (tab == 0) RebuildWorld(); else RebuildSelected();
    }

    private void Apply(bool on)
    {
        IsOpen = on;
        if (on) { RefreshSystems(); Layout(); if (tab == 0) RebuildWorld(); else RebuildSelected(); }
        else
        {
            if (pickMode) { pickMode = false; UpdatePickUi(); }
            if (pickBlocking) { PixelClicker.ExternalClickBlock = false; pickBlocking = false; }
        }
    }

    // ------------------------------------------------------------------
    // Rows
    // ------------------------------------------------------------------

    private static void Clear(RectTransform content, List<Row> rows)
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        rows.Clear();
    }

    private float AddSection(RectTransform content, string text, float y)
    {
        TMP_Text t = PixelUIKit.CreateText(font, content, "Section", text.ToUpperInvariant(), FontSize - 2f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, new Color(1f, 0.85f, 0.4f, 1f));
        PlaceTL(t.rectTransform, 14f, y, Width - 40f, RowH - 4f);
        t.raycastTarget = false;
        return y + RowH;
    }

    private float AddFoldHeader(RectTransform content, string text, bool open, UnityEngine.Events.UnityAction click, float y)
    {
        MakeButton(content, (open ? "[-] " : "[+] ") + text, 10f, y, Width - 40f, RowH, HeaderRow, click, FontSize - 1f);
        return y + RowH + RowGap;
    }

    private float AddRow(RectTransform content, List<Row> rows, Member m, float y)
    {
        float labelW = (Width - 40f) * 0.46f, ctlX = 14f + labelW + 6f, ctlW = Width - 40f - labelW - 10f;
        TMP_Text label = PixelUIKit.CreateText(font, content, m.label + " Label", m.label, FontSize - 2f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, TextColor);
        label.enableAutoSizing = true; label.fontSizeMax = FontSize - 2f; label.fontSizeMin = 10f;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        PlaceTL(label.rectTransform, 14f, y, labelW, RowH);
        Row row = new Row { member = m };
        bool isBool = m.type == typeof(bool), isEnum = m.type.IsEnum;
        if (isBool || isEnum)
        {
            Button b = MakeButton(content, "", ctlX, y, ctlW, RowH, BoxColor, null, FontSize - 2f);
            row.buttonLabel = b.GetComponentInChildren<TMP_Text>();
            b.interactable = !m.readOnly;
            b.onClick.AddListener(() =>
            {
                try
                {
                    object cur = m.get();
                    object next;
                    if (isBool) next = !(bool)cur;
                    else { Array all = Enum.GetValues(m.type); next = all.GetValue((Array.IndexOf(all, cur) + 1) % all.Length); }
                    ApplyValue(row, next, true);
                    ShowValue(row);
                }
                catch (Exception e) { SetStatus("Could not change " + m.label + ": " + e.Message); }
            });
        }
        else
        {
            TMP_InputField f = PixelUIKit.CreateInputField(font, content, m.label + " Field", new Vector2(ctlW, RowH), BoxColor, TextColor, FontSize - 2f, "");
            PlaceTL(f.GetComponent<RectTransform>(), ctlX, y, ctlW, RowH);
            f.interactable = !m.readOnly;
            f.gameObject.AddComponent<InspectorCaret>();
            // Applied while you type (as soon as what is there makes sense) and again when you press Enter / click away.
            f.onValueChanged.AddListener(s => { if (TryParse(m.type, s, out object v)) ApplyValue(row, v, false); });
            f.onEndEdit.AddListener(s =>
            {
                if (TryParse(m.type, s, out object v)) ApplyValue(row, v, true);
                else SetStatus("Could not read '" + s + "' as " + m.type.Name + " for " + m.label);
                ShowValue(row);
            });
            row.field = f;
        }
        ShowValue(row);
        rows.Add(row);
        return y + RowH + RowGap;
    }

    private void ShowValue(Row row)
    {
        try
        {
            string s = Format(row.member.get());
            if (row.field != null) { if (!row.field.isFocused) row.field.SetTextWithoutNotify(s); }
            else if (row.buttonLabel != null) row.buttonLabel.text = s;
        }
        catch { /* the object was destroyed */ }
    }

    private void SetStatus(string text) { if (statusLabel != null) statusLabel.text = text; }

    /// <summary>Writes a value and tells you what happened (it reads the value back, so you can see when the game changes it again or refuses it).</summary>
    private void ApplyValue(Row row, object value, bool record)
    {
        Member m = row.member;
        try
        {
            m.set(value);
            object back = m.get();
            string shown = Format(back);
            if (record) RecordEdit(m, Format(value));
            SetStatus(Format(value) == shown ? "Set " + m.label + " = " + shown : "Set " + m.label + " to " + Format(value) + " but it reads back as " + shown + " (the game or Unity changed it)");
        }
        catch (Exception e) { SetStatus("Could not set " + m.label + ": " + e.Message); }
    }

    private void RecordEdit(Member m, string value)
    {
        edits.Add((string.IsNullOrEmpty(m.owner) ? "" : m.owner + ".") + m.label + " = " + value);
        if (edits.Count > 300) edits.RemoveAt(0);
    }

    private void CopyEdits()
    {
        StringBuilder sb = new StringBuilder();
        foreach (string e in edits) sb.AppendLine(e);
        GUIUtility.systemCopyBuffer = sb.Length > 0 ? sb.ToString() : "(no edits yet)";
        Debug.Log("PixelDevInspector: " + edits.Count + " edit(s) copied to the clipboard.");
    }

    // ------------------------------------------------------------------
    // World tab
    // ------------------------------------------------------------------

    private static Member Static(Type t, string name, string label, string owner)
    {
        return FromProperty(null, t, name, label, owner);
    }

    private void RebuildWorld()
    {
        Clear(worldContent, worldRows);
        float y = 4f;
        y = AddSection(worldContent, "Time", y);
        y = AddReadout(worldContent, "FPS", () => fpsSmooth.ToString("0"), y);
        y = AddReadout(worldContent, "Frame time (ms)", () => (1000f / Mathf.Max(1f, fpsSmooth)).ToString("0.0"), y);
        foreach (Member m in new[]
        {
            Static(typeof(Time), "timeScale", "Time scale", "Time"), Static(typeof(Time), "fixedDeltaTime", "Fixed step (s)", "Time"),
            Static(typeof(Time), "maximumDeltaTime", "Max frame step (s)", "Time"), Static(typeof(Application), "targetFrameRate", "Target frame rate", "Application"),
            Static(typeof(QualitySettings), "vSyncCount", "VSync count", "QualitySettings"),
        })
            if (m != null) y = AddRow(worldContent, worldRows, m, y);

        y = AddSection(worldContent, "Physics", y);
        y = AddRow(worldContent, worldRows, new Member
        {
            label = "Gravity Y", type = typeof(float), owner = "Physics",
            get = () => Physics.gravity.y, set = v => { Vector3 g = Physics.gravity; g.y = (float)v; Physics.gravity = g; }
        }, y);
        foreach (Member m in new[]
        {
            Static(typeof(Physics), "gravity", "Gravity (x, y, z)", "Physics"), Static(typeof(Physics), "defaultSolverIterations", "Solver iterations", "Physics"),
            Static(typeof(Physics), "defaultSolverVelocityIterations", "Solver velocity iterations", "Physics"), Static(typeof(Physics), "bounceThreshold", "Bounce threshold", "Physics"),
            Static(typeof(Physics), "sleepThreshold", "Sleep threshold", "Physics"), Static(typeof(Physics), "defaultContactOffset", "Contact offset", "Physics"),
            Static(typeof(Physics), "defaultMaxDepenetrationVelocity", "Max depenetration speed", "Physics"), Static(typeof(Physics), "simulationMode", "Simulation mode", "Physics"),
        })
            if (m != null) y = AddRow(worldContent, worldRows, m, y);

        y = AddSection(worldContent, "Counts", y);
        y = AddReadout(worldContent, "Old pixels", () => oldCount.ToString(), y);
        y = AddReadout(worldContent, "Rigidbodies", () => bodyCount.ToString(), y);
        y = AddReadout(worldContent, "Awake rigidbodies", () => awakeCount.ToString(), y);
        PixelUIKit.UpdateScrollView(worldScroll, worldBar, y + 10f, worldScroll.GetComponent<RectTransform>().rect.height);
    }

    private float AddReadout(RectTransform content, string label, Func<string> get, float y)
    {
        return AddRow(content, worldRows, new Member { label = label, type = typeof(string), readOnly = true, get = () => get() }, y);
    }

    private void RefreshSystems()
    {
        systems.Clear();
        HashSet<Type> seen = new HashSet<Type>();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
#if UNITY_2023_1_OR_NEWER
        MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
#else
        MonoBehaviour[] all = FindObjectsOfType<MonoBehaviour>();
#endif
        List<MonoBehaviour> sorted = new List<MonoBehaviour>();
        foreach (MonoBehaviour mb in all)
            if (mb != null && IsGameType(mb.GetType()) && mb.GetType() != typeof(PixelDevInspector) && mb.GetType() != typeof(OldPixelInfo) && mb.GetType() != typeof(OldPixelDespawn) && seen.Add(mb.GetType()))
                sorted.Add(mb);
        sorted.Sort((a, b) => string.CompareOrdinal(a.GetType().Name, b.GetType().Name));
        foreach (MonoBehaviour mb in sorted) { systems.Add(mb); options.Add(new TMP_Dropdown.OptionData(mb.GetType().Name)); }
        systemsDropdown.ClearOptions();
        systemsDropdown.AddOptions(options);
        systemsDropdown.RefreshShownValue();
    }

    private void SelectSystem()
    {
        int i = systemsDropdown.value;
        if (i < 0 || i >= systems.Count || systems[i] == null) return;
        Component c = systems[i];
        filter = "";
        if (filterField != null) filterField.SetTextWithoutNotify("");
        Select(c.gameObject);
        openState.Clear();
        openState[c.GetInstanceID()] = true;
        SetTab(1);
    }

    // ------------------------------------------------------------------
    // Selected tab
    // ------------------------------------------------------------------

    private void TogglePick() { pickMode = !pickMode; UpdatePickUi(); }

    private void UpdatePickUi()
    {
        if (pickLabel != null) { pickLabel.text = pickMode ? "Pick: ON" : "Pick: off"; pickLabel.GetComponentInParent<Image>().color = pickMode ? OnColor : OffColor; }
        if (!pickMode && pickBlocking) { PixelClicker.ExternalClickBlock = false; pickBlocking = false; }
    }

    private void SelectParent()
    {
        if (selected != null && selected.transform.parent != null) Select(selected.transform.parent.gameObject);
    }

    private void ToggleActive()
    {
        if (selected == null) return;
        selected.SetActive(!selected.activeSelf);
        UpdateSelectedLabels();
    }

    private void DestroySelected()
    {
        if (selected == null) return;
        Destroy(selected);
        Select(null);
    }

    private void Select(GameObject go)
    {
        selected = go;
        wasSelected = go != null;
        openState.Clear();
        UpdateSelectedLabels();
        if (tab == 1) RebuildSelected();
    }

    private void UpdateSelectedLabels()
    {
        if (selectedLabel == null) return;
        selectedLabel.text = selected == null ? "Nothing selected - turn Pick on and click something." : selected.name + "   (" + selected.transform.position.ToString("F1") + ")";
        if (activeLabel != null) activeLabel.text = selected != null && !selected.activeSelf ? "Active: off" : "Active";
    }

    private void RebuildSelected()
    {
        Clear(selContent, selRows);
        float y = 4f;
        if (selected != null)
        {
            Component[] comps = selected.GetComponents<Component>();
            foreach (Component c in comps)
            {
                if (c == null) continue;
                y = AddObjectBlock(c, c.GetType().Name, c.GetInstanceID(), y);
            }
            OldPixelInfo info = selected.GetComponent<OldPixelInfo>();
            PixelClicker clicker = info != null ? PixelFind.First<PixelClicker>() : null;
            if (clicker != null && clicker.IsValidTierIndex(info.tierIndex))
            {
                PixelClicker.PixelTier tier = clicker.Tiers[info.tierIndex];
                y = AddObjectBlock(tier, "Tier data: " + tier.displayName, 1000000 + info.tierIndex, y);
            }
        }
        PixelUIKit.UpdateScrollView(selScroll, selBar, y + 10f, selScroll.GetComponent<RectTransform>().rect.height);
    }

    private float AddObjectBlock(object o, string title, int id, float y)
    {
        List<Member> members = CollectMembers(o, filter);
        if (members.Count == 0) return y;
        bool filtering = filter.Length > 0;
        bool open = filtering || (openState.TryGetValue(id, out bool user) ? user : members.Count <= EasyOpenCount);
        y = AddFoldHeader(selContent, title + "  (" + members.Count + ")", open, () =>
        {
            openState[id] = !(filter.Length > 0 || (openState.TryGetValue(id, out bool u) ? u : members.Count <= EasyOpenCount));
            RebuildSelected();
        }, y);
        if (!open) return y;
        int shown = 0;
        foreach (Member m in members)
        {
            if (shown++ >= MaxRowsPerComponent) { y = AddSection(selContent, "... use the filter to narrow", y); break; }
            y = AddRow(selContent, selRows, m, y);
        }
        return y + 4f;
    }

    // ------------------------------------------------------------------
    // Per-frame
    // ------------------------------------------------------------------

    private void Update()
    {
        if (!IsOpen) return;
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f) fpsSmooth = Mathf.Lerp(fpsSmooth, 1f / dt, 0.1f);

        // Pick mode keeps cube clicks away from the game and selects whatever the left click hits.
        if (pickMode)
        {
            PixelClicker.ExternalClickBlock = true;
            pickBlocking = true;
            if (PixelInput.LeftPressed() && !PixelInput.PointerOverUI()) PickUnderPointer();
        }

        countT -= dt;
        if (countT <= 0f)
        {
            countT = 1f;
            if (tab == 0)
            {
                oldCount = PixelFind.Count<OldPixelInfo>();
                Rigidbody[] bodies =
#if UNITY_2023_1_OR_NEWER
                    FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
#else
                    FindObjectsOfType<Rigidbody>();
#endif
                bodyCount = bodies.Length;
                awakeCount = 0;
                foreach (Rigidbody b in bodies) if (!b.IsSleeping()) awakeCount++;
            }
        }

        refreshT -= dt;
        if (refreshT <= 0f)
        {
            refreshT = RefreshSeconds;
            List<Row> rows = tab == 0 ? worldRows : selRows;
            foreach (Row r in rows) ShowValue(r);
            if (tab == 1)
            {
                if (selected == null && wasSelected) { wasSelected = false; UpdateSelectedLabels(); RebuildSelected(); }
                else if (selected != null) UpdateSelectedLabels();
            }
        }
    }

    private void PickUnderPointer()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
        float best = float.MaxValue;
        Collider bestCollider = null;
        foreach (RaycastHit h in hits)
            if (h.distance < best) { best = h.distance; bestCollider = h.collider; }
        if (bestCollider == null) return;
        Rigidbody rb = bestCollider.attachedRigidbody;
        Select(rb != null ? rb.gameObject : bestCollider.gameObject);
        SetTab(1);
    }

    /// <summary>
    /// The Inspector's own blinking text cursor (the built-in one did not show in these fields): a white bar that follows the caret while the field has focus.
    /// The built-in caret is made invisible so there is never a second one.
    /// </summary>
    private sealed class InspectorCaret : MonoBehaviour
    {
        private TMP_InputField field;
        private RectTransform bar;

        private void LateUpdate()
        {
            if (field == null)
            {
                field = GetComponent<TMP_InputField>();
                if (field == null || field.textComponent == null) return;
                field.customCaretColor = true;
                field.caretColor = Color.clear;
                GameObject g = new GameObject("Inspector Caret", typeof(RectTransform), typeof(Image));
                g.transform.SetParent(field.textComponent.transform, false);
                g.GetComponent<Image>().color = Color.white;
                g.GetComponent<Image>().raycastTarget = false;
                bar = g.GetComponent<RectTransform>();
                bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0.5f);
                g.SetActive(false);
            }
            bool show = field.isFocused && Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;
            if (bar.gameObject.activeSelf != show) bar.gameObject.SetActive(show);
            if (!show) return;
            TMP_Text text = field.textComponent;
            TMP_TextInfo info = text.textInfo;
            int pos = Mathf.Clamp(field.stringPosition, 0, info.characterCount);
            float x;
            if (info.characterCount == 0) x = text.rectTransform.rect.xMin + text.margin.x;
            else if (pos == 0) x = info.characterInfo[0].origin;
            else x = info.characterInfo[pos - 1].xAdvance;
            float y = info.lineCount > 0 ? (info.lineInfo[0].ascender + info.lineInfo[0].descender) * 0.5f : 0f;
            float h = Mathf.Max(14f, text.fontSize * 1.1f);
            bar.sizeDelta = new Vector2(2f, h);
            bar.anchoredPosition = new Vector2(x, y);
            bar.SetAsLastSibling();
        }
    }
}
