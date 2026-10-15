// ---- System notes ----
// PixelTalk.cs
//   Talking noises for dialogue boxes: while a box types its text out (PixelNotice tip / tutorial boxes, Cubie's speech panel) PixelTalk.Typed plays a short code-made blip (sound id talk_blip, six
//   voiced squeaks from PixelSynth.TalkBlip) for every second letter, its pitch following the letter (vowels a little higher) so it mumbles like speech without being words. Both boxes have a small
//   speaker button next to their back button (PixelTalk.AddMuteButton) that mutes / unmutes the blips (PlayerPrefs PixelClicker.Setting.TalkMuted, kept per save profile by PixelSettingsSync). The speaker
//   icon is a pixel-art sprite drawn from text rows (SpeakerSprite).
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class PixelTalk
{
    private const string Pref = "PixelClicker.Setting.TalkMuted";
    private static int mutedCache = -1;
    private static int letters;
    private static Sprite speakerOn, speakerOff;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { mutedCache = -1; letters = 0; speakerOn = speakerOff = null; }

    /// <summary>Are the talking blips switched off? (Saved per save profile.)</summary>
    public static bool Muted
    {
        get { if (mutedCache < 0) mutedCache = PixelPrefs.GetInt(Pref, 0); return mutedCache != 0; }
        set { mutedCache = value ? 1 : 0; PixelPrefs.SetInt(Pref, mutedCache); PixelPrefs.Save(); }
    }

    public static void ReloadFromPrefs() { mutedCache = -1; }

    /// <summary>Call as a box reveals characters 'from' .. 'to' (indices into the label's text): plays a blip for every second letter.</summary>
    public static void Typed(TMP_Text label, int from, int to, float pitch = 1f)
    {
        if (Muted || label == null || to <= from) return;
        TMP_TextInfo info = label.textInfo;
        if (info == null || info.characterInfo == null) return;
        for (int i = Mathf.Max(0, from); i < to && i < info.characterCount && i < info.characterInfo.Length; i++)
        {
            char c = info.characterInfo[i].character;
            if (!char.IsLetterOrDigit(c)) continue;
            if (++letters % 2 != 0) continue;
            bool vowel = "aeiouAEIOU".IndexOf(c) >= 0;
            PixelAudio.Play("talk_blip", pitch * (0.84f + (char.ToLowerInvariant(c) % 9) * 0.035f) * (vowel ? 1.08f : 1f), 0.8f);
        }
    }

    // ---------------- the mute button ----------------

    private static readonly string[] OnRows =
    {
        "............",
        ".....#......",
        "....##...#..",
        "###.##....#.",
        "###.##..#.#.",
        "###.##..#.#.",
        "###.##....#.",
        "....##...#..",
        ".....#......",
        "............",
    };

    private static readonly string[] OffRows =
    {
        "............",
        ".....#......",
        "....##.#...#",
        "###.##..#.#.",
        "###.##...#..",
        "###.##...#..",
        "###.##..#.#.",
        "....##.#...#",
        ".....#......",
        "............",
    };

    private static Sprite Draw(string[] rows)
    {
        int w = 12, h = rows.Length;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, h - 1 - y, x < rows[y].Length && rows[y][x] == '#' ? Color.white : new Color(1f, 1f, 1f, 0f));
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    public static Sprite SpeakerSprite(bool on)
    {
        if (on) { if (speakerOn == null) speakerOn = Draw(OnRows); return speakerOn; }
        if (speakerOff == null) speakerOff = Draw(OffRows);
        return speakerOff;
    }

    /// <summary>A small speaker button (mute / unmute the talking blips) placed with the given anchor, pivot and position inside 'parent'.</summary>
    public static Button AddMuteButton(TMP_FontAsset font, RectTransform parent, Vector2 size, Vector2 anchor, Vector2 pivot, Vector2 position, Color color)
    {
        Button b = PixelUIKit.CreateButton(font, parent, "Mute Talk", "", size, color, Color.white, 20f);
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.sizeDelta = size;
        r.anchoredPosition = position;
        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(b.transform, false);
        Image im = icon.GetComponent<Image>();
        im.raycastTarget = false;
        im.color = new Color(1f, 1f, 1f, 0.95f);
        RectTransform ir = icon.GetComponent<RectTransform>();
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
        float s = Mathf.Min(size.x, size.y) * 0.62f;
        ir.sizeDelta = new Vector2(s * 1.2f, s);
        void Refresh() { im.sprite = SpeakerSprite(!Muted); }
        Refresh();
        b.onClick.AddListener(() => { Muted = !Muted; Refresh(); if (!Muted) PixelAudio.Play("talk_blip", 1.1f, 0.8f); });
        b.gameObject.AddComponent<PixelTalkMuteRefresh>().refresh = Refresh;
        return b;
    }
}

/// <summary>Redraws a mute button's icon when its box opens (the setting may have changed elsewhere, or with a save profile).</summary>
public class PixelTalkMuteRefresh : MonoBehaviour
{
    public System.Action refresh;
    private void OnEnable() { refresh?.Invoke(); }
}
