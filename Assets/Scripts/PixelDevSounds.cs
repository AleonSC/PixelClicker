// ---- System notes ----
// PixelDevSounds.cs
//   SOUND BOARD (Dev Hub tab 'Sounds'): every sound effect of the game (PixelAudio.AllSounds, the Inspector list incl. the code-made ones) in one list:
//   a Play button, the id, how many variants it has, and a volume slider that changes the sound live (the Sound object's volume, so the game uses it at once).
//   * Filter box (part of an id, e.g. 'wood', 'ui_', 'pixel_bounce'). 'Stop all' stops every sound; 'Undo changes' puts every volume back to how it was when
//     the board opened this session.
//   * 'Copy changes for Claude' copies 'id: volume old -> new' lines: paste them to Claude to make the new volumes the defaults (PixelAudio.CreateDefaultSounds).
//   Volume edits are run-time only (leaving Play mode drops them unless Claude puts them in the code).
// ---------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelDevSounds : MonoBehaviour
{
    private const float H = PixelDevHub.ContentH, RowH = 40f, ListTop = 160f;

    private sealed class Row { public PixelAudio.Sound sound; public RectTransform rect; public Slider slider; public TMP_Text value; }

    private static PixelDevSounds instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    public static void Boot()
    {
        if (instance != null) return;
        new GameObject("Dev Sounds").AddComponent<PixelDevSounds>();
    }

    private RectTransform root, content;
    private ScrollRect scroll;
    private GameObject scrollBar;
    private TMP_InputField filter;
    private TMP_Text status;
    private bool built;
    private int builtCount = -1;
    private readonly List<Row> rows = new List<Row>();
    private readonly Dictionary<PixelAudio.Sound, float> original = new Dictionary<PixelAudio.Sound, float>();

    private void Awake()
    {
        instance = this;
        root = PixelDevHub.TabRoot("sounds");
        PixelDevHub.SetTabCallback("sounds", on => { if (!on) return; if (!built) Build(); if (builtCount != PixelAudio.AllSounds.Count) BuildRows(); else Layout(); });
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void Build()
    {
        built = true;
        PixelDevUI.Heading(root, "Sound board", 10f, 8f, 400f);
        filter = PixelDevUI.Input(root, "Filter", 10f, 48f, 380f, 40f, "Filter (wood, ui_, bounce...)");
        PixelUIKit.AddSearchIcon(filter);
        filter.onValueChanged.AddListener(_ => Layout());
        PixelDevUI.Button(root, "Stop all", 398f, 48f, 140f, 40f, PixelDevUI.ButtonColor, () =>
        {
            foreach (PixelAudio.Sound s in PixelAudio.AllSounds) if (s != null) PixelAudio.Stop(s.id);
        });
        PixelDevUI.Button(root, "Undo changes", 546f, 48f, 144f, 40f, PixelDevUI.ButtonColor, UndoChanges);
        PixelDevUI.Button(root, "Copy changes for Claude", 10f, 96f, 300f, 40f, PixelDevUI.Green, CopyChanges);
        status = PixelDevUI.WrapText(root, "Status", 320f, 96f, 370f, 56f, 17f, PixelDevUI.DimText);
        status.text = "Play a sound, drag its volume. Changes are heard at once.";

        scroll = PixelUIKit.CreateScrollView(root, "Sounds", PixelDevUI.Accent, 10f, 40f, out content, out scrollBar);
        PixelDevUI.PlaceTL(scroll.GetComponent<RectTransform>(), 10f, ListTop, 680f, H - ListTop - 8f);
    }

    private void BuildRows()
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        rows.Clear();
        List<PixelAudio.Sound> sounds = new List<PixelAudio.Sound>();
        foreach (PixelAudio.Sound s in PixelAudio.AllSounds) if (s != null && !string.IsNullOrEmpty(s.id)) sounds.Add(s);
        sounds.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        builtCount = PixelAudio.AllSounds.Count;

        foreach (PixelAudio.Sound s in sounds)
        {
            if (!original.ContainsKey(s)) original[s] = s.volume;
            GameObject go = new GameObject(s.id + " Row", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(content, false);
            go.GetComponent<Image>().color = PixelDevUI.RowColor;
            Row r = new Row { sound = s, rect = go.GetComponent<RectTransform>() };
            PixelDevUI.PlaceTL(r.rect, 0f, 0f, 664f, RowH);

            PixelAudio.Sound sound = s;
            PixelDevUI.Button(r.rect, "Play", 4f, 4f, 70f, RowH - 8f, PixelDevUI.Green, () => PixelAudio.Play(sound.id), 18f);
            int clips = s.clips != null ? s.clips.Length : 0;
            TMP_Text id = PixelDevUI.Label(r.rect, s.id + (clips > 1 ? "  <color=#8a93a8>x" + clips + "</color>" : clips == 0 ? "  <color=#c86060>(no clip)</color>" : ""),
                                           84f, 0f, 300f, RowH, 18f, PixelDevUI.TextColor);
            id.richText = true;
            PixelUIKit.FitOneLine(id, 12f);

            r.slider = PixelUIKit.CreateSlider(r.rect, "Volume", PixelDevUI.Box, PixelDevUI.Accent, Color.white);
            PixelDevUI.PlaceTL(r.slider.GetComponent<RectTransform>(), 392f, 6f, 200f, RowH - 12f);
            r.slider.SetValueWithoutNotify(s.volume);
            r.value = PixelDevUI.Label(r.rect, "", 600f, 0f, 60f, RowH, 18f, PixelDevUI.TextColor, TextAlignmentOptions.MidlineRight);
            Row row = r;
            r.slider.onValueChanged.AddListener(v => { row.sound.volume = Mathf.Round(v * 100f) / 100f; ShowValue(row); });
            ShowValue(r);
            rows.Add(r);
        }
        Layout();
    }

    private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private void ShowValue(Row r)
    {
        bool changed = original.TryGetValue(r.sound, out float was) && Mathf.Abs(was - r.sound.volume) > 0.001f;
        r.value.text = F(r.sound.volume);
        r.value.color = changed ? new Color(1f, 0.8f, 0.35f, 1f) : PixelDevUI.TextColor;   // changed volumes show in gold
    }

    private void Layout()
    {
        string q = filter != null ? filter.text.Trim().ToLowerInvariant() : "";
        float y = 0f;
        foreach (Row r in rows)
        {
            bool show = q.Length == 0 || r.sound.id.ToLowerInvariant().Contains(q);
            r.rect.gameObject.SetActive(show);
            if (!show) continue;
            r.slider.SetValueWithoutNotify(r.sound.volume);
            ShowValue(r);
            PixelDevUI.PlaceTL(r.rect, 0f, y, 664f, RowH);
            y += RowH + 4f;
        }
        PixelUIKit.UpdateScrollView(scroll, scrollBar, y, H - ListTop - 8f);
    }

    private void UndoChanges()
    {
        foreach (KeyValuePair<PixelAudio.Sound, float> kv in original) kv.Key.volume = kv.Value;
        Layout();
        status.text = "Every volume is back to how it was.";
    }

    private void CopyChanges()
    {
        StringBuilder sb = new StringBuilder("// Sound board volume changes (PixelAudio sound id: old -> new)\n");
        int n = 0;
        foreach (Row r in rows)
        {
            if (!original.TryGetValue(r.sound, out float was) || Mathf.Abs(was - r.sound.volume) <= 0.001f) continue;
            sb.Append(r.sound.id).Append(": ").Append(F(was)).Append(" -> ").Append(F(r.sound.volume)).Append('\n');
            n++;
        }
        if (n == 0) { status.text = "Nothing changed yet."; return; }
        GUIUtility.systemCopyBuffer = sb.ToString();
        status.text = "Copied " + n + " change" + (n == 1 ? "" : "s") + ". Paste them to Claude to make them the defaults.";
    }
}
