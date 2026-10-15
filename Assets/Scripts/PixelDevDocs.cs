// ---- System notes ----
// PixelDevDocs.cs
//   The 'Docs' tab of the Dev Hub (PixelDevHub): the DOCUMENTATION section with three permanent sub-tabs: To-Do (0), Changelog (1) and Notes (2).
//   * To-Do and Changelog: PixelDevDocs only provides their content roots (PixelDevDocs.Root(0) / Root(1)); PixelDevTools fills them (Resources/Todo.txt, Resources/Changelog.txt)
//     and rebuilds them when PixelDevDocs.Shown(sub) fires (the Docs tab is visible and that sub-tab is showing).
//   * Notes: your own notes, typed in-game. A list of notes on the left ('+ New note'), the selected note's title and text on the right (multi-line, scrolls), Delete (a second click
//     confirms). Saved automatically a moment after every change, when the Docs tab is hidden and when the game closes, to <persistentDataPath>/DevNotes.json (not part of the game save).
//   The selected sub-tab is remembered (PlayerPrefs PixelClicker.Dev.DocsSub).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelDevDocs : MonoBehaviour
{
    private const float W = PixelDevHub.ContentW, H = PixelDevHub.ContentH, SubH = 40f, SaveDelay = 0.6f;
    private const string PrefSub = "PixelClicker.Dev.DocsSub";

    [Serializable] private class Note { public string id, title, body; public long edited; }
    [Serializable] private class NoteFile { public List<Note> notes = new List<Note>(); public string selected; }

    private static PixelDevDocs instance;
    public static event Action<int> Shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; Shown = null; }

    public static void Boot()
    {
        if (instance != null) return;
        new GameObject("Dev Docs").AddComponent<PixelDevDocs>();
    }

    /// <summary>The content root of sub-tab 0 (To-Do), 1 (Changelog) or 2 (Notes): 700 wide, 732 tall, origin top-left.</summary>
    public static RectTransform Root(int sub) { Boot(); return instance.roots[sub]; }

    private static readonly Color BoxColor = new Color(0.14f, 0.17f, 0.26f, 1f), TextColor = new Color(0.92f, 0.94f, 1f, 1f),
                                  On = new Color(0.25f, 0.45f, 0.8f, 1f), Off = new Color(0.2f, 0.24f, 0.34f, 1f);

    private TMP_FontAsset font;
    private readonly RectTransform[] roots = new RectTransform[3];
    private readonly Image[] subImages = new Image[3];
    private int sub;
    private bool visible;

    // notes
    private NoteFile data = new NoteFile();
    private Note current;
    private RectTransform listContent;
    private ScrollRect listScroll;
    private GameObject listBar;
    private readonly List<Button> listButtons = new List<Button>();
    private readonly List<Image> listImages = new List<Image>();
    private readonly List<TMP_Text> listLabels = new List<TMP_Text>();
    private TMP_InputField titleField, bodyField;
    private TMP_Text deleteLabel;
    private bool dirty;
    private float saveAt, deleteConfirmUntil;

    private static string FilePath => Path.Combine(Application.persistentDataPath, "DevNotes.json");

    private static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private void Awake()
    {
        instance = this;
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();
        RectTransform root = PixelDevHub.TabRoot("docs");

        string[] names = { "To-Do", "Changelog", "Notes" };
        float bw = (W - 20f - 12f) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            Button b = PixelUIKit.CreateButton(font, root, names[i] + " Sub Tab", names[i], new Vector2(bw, SubH - 4f), Off, TextColor, 22f);
            PixelUIKit.Caps(b);
            PlaceTL(b.GetComponent<RectTransform>(), 10f + i * (bw + 6f), 4f, bw, SubH - 4f);
            subImages[i] = b.GetComponent<Image>();
            b.onClick.AddListener(() => Select(index));

            GameObject r = new GameObject(names[i] + " Root", typeof(RectTransform));
            r.transform.SetParent(root, false);
            roots[i] = r.GetComponent<RectTransform>();
            PlaceTL(roots[i], 0f, SubH + 4f, W, H - SubH - 4f);
        }
        LoadNotes();
        BuildNotes(roots[2]);
        sub = Mathf.Clamp(PixelPrefs.GetInt(PrefSub, 0), 0, 2);
        ApplySub();
        PixelDevHub.SetTabCallback("docs", SetVisible);
    }

    private void OnDestroy()
    {
        SaveNow();
        if (instance == this) instance = null;
    }

    private void OnApplicationQuit() { SaveNow(); }

    private void SetVisible(bool on)
    {
        visible = on;
        if (on) Shown?.Invoke(sub);
        else SaveNow();
    }

    private void Select(int index)
    {
        sub = index;
        PixelPrefs.SetInt(PrefSub, sub);
        ApplySub();
        if (visible) Shown?.Invoke(sub);
    }

    private void ApplySub()
    {
        for (int i = 0; i < 3; i++)
        {
            roots[i].gameObject.SetActive(i == sub);
            subImages[i].color = i == sub ? On : Off;
        }
    }

    // ------------------------------------------------------------------
    // Notes
    // ------------------------------------------------------------------

    private void LoadNotes()
    {
        try
        {
            if (File.Exists(FilePath)) data = JsonUtility.FromJson<NoteFile>(File.ReadAllText(FilePath)) ?? new NoteFile();
        }
        catch (Exception e) { Debug.LogWarning("PixelDevDocs: could not read the notes: " + e.Message); data = new NoteFile(); }
        if (data.notes == null) data.notes = new List<Note>();
        current = data.notes.Find(n => n.id == data.selected) ?? (data.notes.Count > 0 ? data.notes[0] : null);
    }

    private void SaveNow()
    {
        if (!dirty) return;
        dirty = false;
        try
        {
            data.selected = current != null ? current.id : null;
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e) { Debug.LogWarning("PixelDevDocs: could not save the notes: " + e.Message); }
    }

    private void MarkDirty() { dirty = true; saveAt = Time.unscaledTime + SaveDelay; }

    private void Update()
    {
        if (dirty && Time.unscaledTime >= saveAt) SaveNow();
        if (deleteLabel != null && deleteConfirmUntil > 0f && Time.unscaledTime > deleteConfirmUntil) { deleteConfirmUntil = 0f; deleteLabel.text = "DELETE"; }
    }

    private void BuildNotes(RectTransform root)
    {
        float listW = 210f, areaH = H - SubH - 4f;
        Button add = PixelUIKit.CreateButton(font, root, "New Note Button", "+ New note", new Vector2(listW, 40f), new Color(0.2f, 0.6f, 0.3f, 1f), TextColor, 22f);
        PlaceTL(add.GetComponent<RectTransform>(), 10f, 6f, listW, 40f);
        add.onClick.AddListener(NewNote);

        listScroll = PixelUIKit.CreateScrollView(root, "Notes List", new Color(0.5f, 0.5f, 0.6f, 1f), 10f, 40f, out listContent, out listBar);
        PlaceTL(listScroll.GetComponent<RectTransform>(), 10f, 54f, listW, areaH - 64f);

        float rx = listW + 22f, rw = W - rx - 10f;
        titleField = PixelUIKit.CreateInputField(font, root, "Note Title", new Vector2(rw - 108f, 40f), BoxColor, TextColor, 22f, "Title");
        PlaceTL(titleField.GetComponent<RectTransform>(), rx, 6f, rw - 108f, 40f);
        titleField.gameObject.AddComponent<PixelInputCaret>();
        titleField.onValueChanged.AddListener(s => { if (current == null) return; current.title = s; Touch(); RefreshListLabel(); });

        Button del = PixelUIKit.CreateButton(font, root, "Delete Note Button", "Delete", new Vector2(100f, 40f), new Color(0.6f, 0.2f, 0.2f, 1f), TextColor, 20f);
        PlaceTL(del.GetComponent<RectTransform>(), rx + rw - 100f, 6f, 100f, 40f);
        deleteLabel = del.GetComponentInChildren<TMP_Text>();
        deleteLabel.text = "DELETE";
        del.onClick.AddListener(DeleteClicked);

        bodyField = PixelUIKit.CreateInputField(font, root, "Note Body", new Vector2(rw, areaH - 64f), BoxColor, TextColor, 22f, "Type your note here...");
        PlaceTL(bodyField.GetComponent<RectTransform>(), rx, 54f, rw, areaH - 64f);
        bodyField.lineType = TMP_InputField.LineType.MultiLineNewline;
        bodyField.textComponent.alignment = TextAlignmentOptions.TopLeft;
        if (bodyField.placeholder is TMP_Text ph) ph.alignment = TextAlignmentOptions.TopLeft;
#if UNITY_2023_1_OR_NEWER
        bodyField.textComponent.textWrappingMode = TextWrappingModes.Normal;
#else
        bodyField.textComponent.enableWordWrapping = true;
#endif
        bodyField.gameObject.AddComponent<PixelInputCaret>();
        bodyField.onValueChanged.AddListener(s => { if (current == null) return; current.body = s; Touch(); RefreshListLabel(); });

        RebuildList();
        ShowCurrent();
    }

    private void Touch()
    {
        if (current != null) current.edited = DateTime.UtcNow.Ticks;
        MarkDirty();
    }

    private void NewNote()
    {
        Note n = new Note { id = Guid.NewGuid().ToString("N"), title = "", body = "", edited = DateTime.UtcNow.Ticks };
        data.notes.Insert(0, n);
        current = n;
        MarkDirty();
        RebuildList();
        ShowCurrent();
        titleField.ActivateInputField();
    }

    private void DeleteClicked()
    {
        if (current == null) return;
        if (Time.unscaledTime > deleteConfirmUntil || deleteConfirmUntil <= 0f)
        {
            deleteConfirmUntil = Time.unscaledTime + 3f;
            deleteLabel.text = "SURE?";
            return;
        }
        int i = data.notes.IndexOf(current);
        data.notes.Remove(current);
        current = data.notes.Count > 0 ? data.notes[Mathf.Clamp(i, 0, data.notes.Count - 1)] : null;
        deleteConfirmUntil = 0f;
        deleteLabel.text = "DELETE";
        MarkDirty();
        RebuildList();
        ShowCurrent();
    }

    private static string Label(Note n)
    {
        string s = !string.IsNullOrWhiteSpace(n.title) ? n.title.Trim() : !string.IsNullOrWhiteSpace(n.body) ? n.body.Trim().Split('\n')[0] : "(empty note)";
        return s.Length > 40 ? s.Substring(0, 40) : s;
    }

    private void RebuildList()
    {
        foreach (Button b in listButtons) if (b != null) Destroy(b.gameObject);
        listButtons.Clear(); listImages.Clear(); listLabels.Clear();
        float y = 0f;
        foreach (Note n in data.notes)
        {
            Note note = n;
            Button b = PixelUIKit.CreateButton(font, listContent, "Note", Label(n), new Vector2(190f, 48f), note == current ? On : Off, TextColor, 19f);
            RectTransform r = b.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(190f, 48f);
            r.anchoredPosition = new Vector2(0f, -y);
            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            label.enableAutoSizing = true; label.fontSizeMax = 19f; label.fontSizeMin = 11f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.offsetMin = new Vector2(10f, 2f); label.rectTransform.offsetMax = new Vector2(-6f, -2f);
            b.onClick.AddListener(() => { current = note; data.selected = note.id; MarkDirty(); RefreshListColors(); ShowCurrent(); });
            listButtons.Add(b); listImages.Add(b.GetComponent<Image>()); listLabels.Add(label);
            y += 54f;
        }
        PixelUIKit.UpdateScrollView(listScroll, listBar, y, listScroll.GetComponent<RectTransform>().rect.height);
    }

    private void RefreshListColors()
    {
        for (int i = 0; i < listImages.Count; i++) listImages[i].color = data.notes[i] == current ? On : Off;
    }

    private void RefreshListLabel()
    {
        int i = data.notes.IndexOf(current);
        if (i >= 0 && i < listLabels.Count) listLabels[i].text = Label(current);
    }

    private void ShowCurrent()
    {
        bool has = current != null;
        titleField.interactable = has;
        bodyField.interactable = has;
        titleField.SetTextWithoutNotify(has ? current.title : "");
        bodyField.SetTextWithoutNotify(has ? current.body : "");
        RefreshListColors();
    }
}
