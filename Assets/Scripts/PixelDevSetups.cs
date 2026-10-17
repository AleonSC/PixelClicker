// ---- System notes ----
// PixelDevSetups.cs
//   TEST SETUPS (Dev Hub tab 'Setups'): save the game as it is now under a name and jump back to it with one click, to test a feature without
//   replaying or cheating your way back to the right state.
//   * A setup is an ordinary save file (PixelSaveGame.SaveToFile / LoadFile) in <persistentDataPath>/TestSetups/<name>.json, with its screenshot next to it.
//     Saving or loading a setup never touches the numbered save slots or the autosave list (a manual Save after loading still writes the current slot).
//   * BUILT-IN setups ship with the project: every TextAsset in Assets/Resources/TestSetups (a setup file renamed .json -> .json stays a TextAsset) is listed
//     with a 'built-in' tag and can't be deleted here. Send Claude a setup file (or its name) and it can be added there so it is in every copy of the game.
//   * Delete asks for a second click. 'Open folder' shows the files.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelDevSetups : MonoBehaviour
{
    private const float W = PixelDevHub.ContentW, H = PixelDevHub.ContentH, RowH = 46f, ListTop = 214f, ConfirmSeconds = 3f;
    private const string ResourceFolder = "TestSetups";

    private sealed class Entry { public string name, path; public TextAsset builtIn; public DateTime when; }

    private static PixelDevSetups instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    public static void Boot()
    {
        if (instance != null) return;
        new GameObject("Dev Setups").AddComponent<PixelDevSetups>();
    }

    private static string Folder => Path.Combine(Application.persistentDataPath, "TestSetups");

    private RectTransform root, content;
    private ScrollRect scroll;
    private GameObject scrollBar;
    private TMP_InputField nameField;
    private TMP_Text status;
    private bool built;
    private string confirmDelete;
    private float confirmUntil;

    private void Awake()
    {
        instance = this;
        root = PixelDevHub.TabRoot("setups");
        PixelDevHub.SetTabCallback("setups", on => { if (!on) return; if (!built) Build(); Rebuild(); });
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void Build()
    {
        built = true;
        PixelDevUI.Heading(root, "Test setups", 10f, 8f, 400f);
        PixelDevUI.WrapText(root, "Intro", 10f, 44f, 680f, 52f, 18f, PixelDevUI.DimText).text =
            "Save the game as it is right now under a name, then load it again whenever you want to test from that point. Your own save slots are never touched.";

        nameField = PixelDevUI.Input(root, "Setup Name", 10f, 102f, 440f, 42f, "Name, e.g. Woodcutting level 20");
        nameField.onSubmit.AddListener(_ => SaveCurrent());
        PixelDevUI.Button(root, "Save current game", 458f, 102f, 232f, 42f, PixelDevUI.Green, SaveCurrent);

        status = PixelDevUI.WrapText(root, "Status", 10f, 152f, 520f, 54f, 18f, PixelDevUI.DimText);
        PixelDevUI.Button(root, "Open folder", 540f, 156f, 150f, 40f, PixelDevUI.ButtonColor, () =>
        {
            Directory.CreateDirectory(Folder);
            Application.OpenURL("file://" + Folder);
        });

        scroll = PixelUIKit.CreateScrollView(root, "Setups", PixelDevUI.Accent, 10f, 40f, out content, out scrollBar);
        PixelDevUI.PlaceTL(scroll.GetComponent<RectTransform>(), 10f, ListTop, 680f, H - ListTop - 8f);
    }

    private List<Entry> ListSetups()
    {
        List<Entry> list = new List<Entry>();
        if (Directory.Exists(Folder))
            foreach (string path in Directory.GetFiles(Folder, "*.json"))
                list.Add(new Entry { name = Path.GetFileNameWithoutExtension(path), path = path, when = File.GetLastWriteTime(path) });
        list.Sort((a, b) => b.when.CompareTo(a.when));   // newest first
        foreach (TextAsset t in Resources.LoadAll<TextAsset>(ResourceFolder))
            if (t.text.TrimStart().StartsWith("{"))   // (only save files: a note or readme in the folder is skipped)
                list.Add(new Entry { name = t.name, builtIn = t });
        return list;
    }

    private void Rebuild()
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        List<Entry> list = ListSetups();
        float y = 0f;
        if (list.Count == 0)
            PixelDevUI.Label(content, "No setups yet - type a name above and press Save current game.", 8f, 4f, 650f, RowH, 19f, PixelDevUI.DimText);
        foreach (Entry e in list)
        {
            GameObject row = new GameObject(e.name + " Row", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(content, false);
            row.GetComponent<Image>().color = PixelDevUI.RowColor;
            RectTransform rr = row.GetComponent<RectTransform>();
            PixelDevUI.PlaceTL(rr, 0f, y, 664f, RowH);

            TMP_Text name = PixelDevUI.Label(rr, e.name, 12f, 0f, 330f, RowH * 0.62f, 21f, PixelDevUI.TextColor);
            PixelUIKit.FitOneLine(name, 13f);
            PixelDevUI.Label(rr, e.builtIn != null ? "built-in (ships with the game)" : e.when.ToString("yyyy-MM-dd HH:mm"), 12f, RowH * 0.55f, 330f, RowH * 0.42f, 15f, PixelDevUI.DimText);

            Entry entry = e;
            PixelDevUI.Button(rr, "Load", 350f, 5f, 140f, RowH - 10f, PixelDevUI.Green, () => Load(entry));
            if (e.builtIn == null)
            {
                bool confirming = confirmDelete == e.path && Time.unscaledTime < confirmUntil;
                PixelDevUI.Button(rr, confirming ? "Sure?" : "Delete", 498f, 5f, 160f, RowH - 10f, confirming ? PixelDevUI.Red : PixelDevUI.ButtonColor, () => Delete(entry));
            }
            y += RowH + 4f;
        }
        PixelUIKit.UpdateScrollView(scroll, scrollBar, y, H - ListTop - 8f);
    }

    private static string SafeName(string raw)
    {
        string name = (raw ?? "").Trim();
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    private void SaveCurrent()
    {
        PixelSaveGame save = PixelFind.Cached<PixelSaveGame>();
        string name = SafeName(nameField.text);
        if (save == null) { status.text = "There is no save system in this scene."; return; }
        if (name.Length == 0) name = "Setup " + DateTime.Now.ToString("yyyy-MM-dd HH-mm");
        try
        {
            Directory.CreateDirectory(Folder);
            bool ok = save.SaveToFile(Path.Combine(Folder, name + ".json"));
            status.text = ok ? "Saved setup '" + name + "'." : "Could not save (on the main menu? start or load a game first).";
            if (ok) nameField.SetTextWithoutNotify("");
        }
        catch (Exception ex) { status.text = "Could not save: " + ex.Message; }
        Rebuild();
    }

    private void Load(Entry e)
    {
        PixelSaveGame save = PixelFind.Cached<PixelSaveGame>();
        if (save == null) { status.text = "There is no save system in this scene."; return; }
        string path = e.path;
        try
        {
            if (e.builtIn != null)
            {
                // A built-in setup lives inside the game: write it to a temporary file and load that.
                path = Path.Combine(Application.temporaryCachePath, "builtin_setup.json");
                File.WriteAllText(path, e.builtIn.text);
            }
            bool ok = save.LoadFile(path);
            status.text = ok ? "Loaded '" + e.name + "'. Your save slots are untouched (a manual Save still writes your current slot)." : "That setup could not be loaded.";
        }
        catch (Exception ex) { status.text = "Could not load: " + ex.Message; }
    }

    private void Delete(Entry e)
    {
        if (confirmDelete != e.path || Time.unscaledTime > confirmUntil)
        {
            confirmDelete = e.path;
            confirmUntil = Time.unscaledTime + ConfirmSeconds;
            Rebuild();
            return;
        }
        try
        {
            File.Delete(e.path);
            string shot = PixelSaveShots.ThumbPath(e.path);
            if (File.Exists(shot)) File.Delete(shot);
            status.text = "Deleted '" + e.name + "'.";
        }
        catch (Exception ex) { status.text = "Could not delete: " + ex.Message; }
        confirmDelete = null;
        Rebuild();
    }

    private void Update()
    {
        // The 'Sure?' button goes back to 'Delete' when the second click doesn't come.
        if (confirmDelete != null && Time.unscaledTime > confirmUntil && built) { confirmDelete = null; Rebuild(); }
    }
}
