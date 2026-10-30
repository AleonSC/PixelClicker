// ---- System notes ----
// PixelTextEdits.cs
//   The texts the user changed with Dev Hub > 'Edit text mode' (PixelTextEditMode.cs), applied everywhere in the game. ONE list of
//   'from' -> 'to' pairs in Assets/Resources/TextEdits.txt (written by the editor, so the edits ship with the game; the user can also edit the file):
//       from: Shop
//       to: Store
//   An edit matches a WHOLE text exactly (rich text tags included): every label showing exactly 'from' shows 'to' instead. Texts built with
//   live numbers ('Level 5', '1.2K / 2K') only match while they read exactly that, so edit fixed texts (titles, buttons, tips, names).
//   '\n' in the file = a new line, '\\' = a backslash.
//   HOW IT IS APPLIED: PixelUIKit.SetText / CreateText run every text through Apply first (no flicker); every other text (code setting .text
//   directly, texts from prefabs) is caught by TextMeshPro's TEXT_CHANGED event and swapped in LateUpdate (it can show its old text for one frame).
//   Texts inside input fields are never swapped (what you type stays yours). With no edits the event handler returns at once.
//   Saving: in the editor it writes Assets/Resources/TextEdits.txt; in a development build (no Assets folder) it writes
//   <persistentDataPath>/TextEdits.txt, which is read on top of the built-in file.
// ---------------------------------------------------
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

public class PixelTextEdits : MonoBehaviour
{
    public const string ResourceName = "TextEdits";
    private const string Header = "# TEXT EDITS - texts changed in the game with Dev Hub > Edit text mode (see the notes in PixelTextEdits.cs).\n" +
                                  "# Each edit is a 'from:' line (the game's exact text) and a 'to:' line (what shows instead). \\n = a new line.\n";

    private static readonly Dictionary<string, string> edits = new Dictionary<string, string>();   // original -> edited
    private static readonly Dictionary<string, string> reverse = new Dictionary<string, string>(); // edited -> original
    private static bool loaded;
    private static PixelTextEdits instance;
    private readonly HashSet<TMP_Text> pending = new HashSet<TMP_Text>();
    private readonly List<TMP_Text> pendingList = new List<TMP_Text>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { edits.Clear(); reverse.Clear(); loaded = false; instance = null; }

    /// <summary>How many edits there are.</summary>
    public static int Count { get { EnsureLoaded(); return edits.Count; } }

    /// <summary>The text to show for 'text': its edit, or the text itself.</summary>
    public static string Apply(string text)
    {
        EnsureLoaded();
        if (edits.Count == 0 || string.IsNullOrEmpty(text)) return text;
        return edits.TryGetValue(text, out string edited) ? edited : text;
    }

    /// <summary>The game's own text behind a shown text (the shown text itself when it is not an edit).</summary>
    public static string OriginalOf(string shown)
    {
        EnsureLoaded();
        return shown != null && reverse.TryGetValue(shown, out string original) ? original : shown;
    }

    /// <summary>Is this shown text one of the edits?</summary>
    public static bool IsEdited(string shown)
    {
        EnsureLoaded();
        return shown != null && reverse.ContainsKey(shown);
    }

    /// <summary>Changes 'original' to 'edited' everywhere (the same text again = the edit is removed), saves the file and updates the texts on screen.</summary>
    public static void Set(string original, string edited)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(original)) return;
        string before = edits.TryGetValue(original, out string old) ? old : original;
        if (old != null) reverse.Remove(old);
        if (string.IsNullOrEmpty(edited) || edited == original) edits.Remove(original);
        else { edits[original] = edited; reverse[edited] = original; }
        SaveFile();
        SwapOnScreen(before, edits.TryGetValue(original, out string now) ? now : original);
    }

    /// <summary>Takes the edit away: 'original' shows again.</summary>
    public static void Remove(string original) => Set(original, original);

    // Every text on screen showing 'from' shows 'to' now.
    private static void SwapOnScreen(string from, string to)
    {
        if (from == to) return;
        foreach (TMP_Text t in PixelFind.All<TMP_Text>())
            if (t != null && t.text == from && t.GetComponentInParent<TMP_InputField>() == null) t.text = to;
    }

    // ---------------- the file ----------------

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        edits.Clear(); reverse.Clear();
#if UNITY_EDITOR
        string path = EditorPath;
        Parse(File.Exists(path) ? File.ReadAllText(path) : null);   // (the file on disk: newer than the imported asset while playing)
#else
        TextAsset asset = Resources.Load<TextAsset>(ResourceName);
        Parse(asset != null ? asset.text : null);
        string local = LocalPath;
        if (File.Exists(local)) Parse(File.ReadAllText(local));   // (edits made in this build, on top)
#endif
    }

    private static string EditorPath => Path.Combine(Application.dataPath, "Resources", ResourceName + ".txt");
    private static string LocalPath => Path.Combine(Application.persistentDataPath, ResourceName + ".txt");

    private static void Parse(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        string from = null;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith("from: ")) from = Unescape(line.Substring(6));
            else if (line.StartsWith("to: ") && from != null)
            {
                string to = Unescape(line.Substring(4));
                if (from.Length > 0 && to != from) { edits[from] = to; reverse[to] = from; }
                from = null;
            }
        }
    }

    private static void SaveFile()
    {
        StringBuilder sb = new StringBuilder(Header);
        foreach (KeyValuePair<string, string> kv in edits)
            sb.Append('\n').Append("from: ").Append(Escape(kv.Key)).Append('\n').Append("to: ").Append(Escape(kv.Value)).Append('\n');
        try
        {
#if UNITY_EDITOR
            File.WriteAllText(EditorPath, sb.ToString());
#else
            File.WriteAllText(LocalPath, sb.ToString());
#endif
        }
        catch (System.Exception e) { Debug.LogWarning("Text edits could not be saved: " + e.Message); }
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n");

    private static string Unescape(string s)
    {
        StringBuilder sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                sb.Append(n == 'n' ? '\n' : n);
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    // ---------------- catching texts set directly ----------------

    private void Awake()
    {
        instance = this;
        EnsureLoaded();
    }

    private void OnEnable() { TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged); }
    private void OnDisable() { TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged); }
    private void OnDestroy() { if (instance == this) instance = null; }

    // TextMeshPro rebuilt a text. It fires inside the canvas rebuild, where changing a text again is not allowed: remember it for LateUpdate.
    private void OnTextChanged(Object changed)
    {
        if (edits.Count == 0) return;
        TMP_Text t = changed as TMP_Text;
        if (t != null && t.text != null && edits.ContainsKey(t.text) && !reverse.ContainsKey(t.text)) pending.Add(t);   // (an edited text is never edited again: no A -> B -> A loops)
    }

    private void LateUpdate()
    {
        if (pending.Count == 0) return;
        pendingList.Clear();
        pendingList.AddRange(pending);
        pending.Clear();
        foreach (TMP_Text t in pendingList)
        {
            if (t == null || t.GetComponentInParent<TMP_InputField>() != null) continue;
            string shown = Apply(t.text);
            if (shown != t.text) t.text = shown;
        }
    }
}
