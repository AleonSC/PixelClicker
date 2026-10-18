// ---- System notes ----
// PixelTextFile.cs
//   ONE reader / writer for the hand-editable text files in Assets/Resources (sections of 'field: value' lines). Used by PixelDialogue
//   (Dialogue.txt) and PixelTextOverrides (PixelTexts.txt); PixelShopText (ShopText.txt) still has its own copy of the parser - move it here
//   when it is next touched.
//   FORMAT: '#' lines are notes. '[Section]' starts a block; then 'field: value' lines (one line each; '\n' = a new line in the text).
//   A field that is missing or left EMPTY keeps the game's own text. A section may appear twice (the fields are merged).
//   USE: Text(section, field, current) returns the file's text or 'current'; Apply(section, field, ref value) / ApplyList(...) copy the
//   file's text over a field (the 'file wins' model: call it once where the defaults are set, the display code stays as it is).
//   LISTS (tutorial steps, Cubie's pages): fields '<prefix> 1', '<prefix> 2'...; a higher number adds an entry, '-' removes one.
//   IN THE EDITOR every Text / Apply that finds its field missing queues it with the game's current text, and Flush() appends the queued
//   fields to the end of the file (creating it with its header if needed): the file fills itself in on the first play and keeps up with
//   new texts; nothing already written there is changed. Reload() reads the file again (edits saved while playing).
// ---------------------------------------------------
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class PixelTextFile
{
    private readonly string resource, header;
    private Dictionary<string, Dictionary<string, string>> sections;
#if UNITY_EDITOR
    private readonly Dictionary<string, StringBuilder> missing = new Dictionary<string, StringBuilder>();
    private readonly List<string> missingOrder = new List<string>();
#endif

    /// <param name="resourceName">The file's name in Assets/Resources without '.txt'.</param>
    /// <param name="fileHeader">The '#' notes written at the top when the file is created.</param>
    public PixelTextFile(string resourceName, string fileHeader) { resource = resourceName; header = fileHeader; }

    /// <summary>Reads the file again (call after editing it while playing).</summary>
    public void Reload() => sections = null;

    private Dictionary<string, Dictionary<string, string>> Sections
    {
        get
        {
            if (sections != null) return sections;
            sections = new Dictionary<string, Dictionary<string, string>>();
            string text = null;
#if UNITY_EDITOR
            if (System.IO.File.Exists(FilePath)) text = System.IO.File.ReadAllText(FilePath);   // (the file itself: edits count after a Reload)
#endif
            if (text == null) { TextAsset asset = Resources.Load<TextAsset>(resource); if (asset != null) text = asset.text; }
            if (!string.IsNullOrEmpty(text)) Parse(text);
            return sections;
        }
    }

    private void Parse(string text)
    {
        Dictionary<string, string> current = null;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                string key = line.Substring(1, line.Length - 2).Trim();
                if (!sections.TryGetValue(key, out current)) { current = new Dictionary<string, string>(); sections[key] = current; }
                continue;
            }
            if (current == null) continue;
            int colon = line.IndexOf(':');
            if (colon <= 0) continue;
            current[line.Substring(0, colon).Trim().ToLowerInvariant()] = line.Substring(colon + 1).Trim().Replace("\\n", "\n");
        }
    }

    /// <summary>Does the file have this section?</summary>
    public bool HasSection(string section) => Sections.ContainsKey(section);

    /// <summary>Is the field written in the file at all (even empty)?</summary>
    public bool Has(string section, string field) => Sections.TryGetValue(section, out Dictionary<string, string> s) && s.ContainsKey(field.ToLowerInvariant());

    /// <summary>The file's text for the field, or null when it is missing or empty (keep the game's).</summary>
    public string Get(string section, string field)
        => Sections.TryGetValue(section, out Dictionary<string, string> s) && s.TryGetValue(field.ToLowerInvariant(), out string v) && v.Length > 0 ? v : null;

    /// <summary>The file's text, else 'current' (in the editor a missing field is queued for Flush with 'current').</summary>
    public string Text(string section, string field, string current)
    {
        string v = Get(section, field);
        if (v != null) return v;
        Queue(section, field, current);
        return current;
    }

    /// <summary>
    /// For a field whose EMPTY value means 'the built-in text' (a visitor's greeting): sets 'value' only when the file has a text, else lists
    /// 'shown' (what the game shows now) in the file so it can be edited - the field itself stays empty.
    /// </summary>
    public void ApplyOr(string section, string field, ref string value, string shown)
    {
        string v = Get(section, field);
        if (v != null) value = v; else Queue(section, field, shown);
    }

    /// <summary>Copies the file's text over 'value' (when it has one).</summary>
    public void Apply(string section, string field, ref string value) => value = Text(section, field, value);

    /// <summary>Copies a numbered list ('&lt;prefix&gt; 1', '&lt;prefix&gt; 2'...) over 'list': the file's entries win, a higher number adds one, '-' removes one.</summary>
    public string[] ApplyList(string section, string prefix, string[] list)
    {
        if (list == null) list = new string[0];
        int count = list.Length;
        if (Sections.TryGetValue(section, out Dictionary<string, string> s))
            foreach (string key in s.Keys)
                if (key.StartsWith(prefix.ToLowerInvariant() + " ") && int.TryParse(key.Substring(prefix.Length + 1), out int n) && n > count) count = n;
        List<string> result = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            string field = prefix + " " + (i + 1);
            string v = Text(section, field, i < list.Length ? list[i] : null);
            if (v == null || v == "-") continue;
            result.Add(v);
        }
        return result.ToArray();
    }

    /// <summary>ApplyList for a List&lt;string&gt;.</summary>
    public void ApplyList(string section, string prefix, List<string> list)
    {
        if (list == null) return;
        string[] done = ApplyList(section, prefix, list.ToArray());
        list.Clear();
        list.AddRange(done);
    }

    private void Queue(string section, string field, string current)
    {
#if UNITY_EDITOR
        if (current == null || Has(section, field)) return;
        if (!missing.TryGetValue(section, out StringBuilder sb)) { sb = new StringBuilder(); missing[section] = sb; missingOrder.Add(section); }
        string line = field + ": " + current.Replace("\r", "").Replace("\n", "\\n") + "\n";
        if (sb.ToString().Contains("\n" + field + ": ") || sb.ToString().StartsWith(field + ": ")) return;   // (queued already)
        sb.Append(line);
#endif
    }

    /// <summary>Editor: appends every queued missing field to the file (grouped by section). Does nothing in a build.</summary>
    public void Flush()
    {
#if UNITY_EDITOR
        if (missingOrder.Count == 0) return;
        StringBuilder all = new StringBuilder();
        foreach (string section in missingOrder) all.Append("\n[").Append(section).Append("]\n").Append(missing[section]);
        try
        {
            if (!System.IO.File.Exists(FilePath)) System.IO.File.WriteAllText(FilePath, header + "\n");
            System.IO.File.AppendAllText(FilePath, all.ToString());
            if (sections != null) Parse(all.ToString());   // (now they count as written, so they are not queued again)
        }
        catch (System.Exception e) { Debug.LogWarning(resource + ".txt: could not add the new texts (" + e.Message + ")"); }
        missing.Clear();
        missingOrder.Clear();
#endif
    }

#if UNITY_EDITOR
    private string FilePath => System.IO.Path.Combine(Application.dataPath, "Resources", resource + ".txt");
#endif
}
