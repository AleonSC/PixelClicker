// ---- System notes ----
// PixelTitleMusic.cs
//   (The list always shows what is audible now: a cue's clip that is not one of the tracks is named in the caption: PixelAudio.PlayingClip.)
//   The main menu's background music selector (created by PixelTitleScreen.Build under its foreground, showMusicPicker): a small bottom-centre box with a MUSIC caption, previous / next arrows
//   and a drop-down listing every track (PixelAudio.MusicTrackCount / MusicTrackName / CurrentMusicTrack / SelectMusicTrack; the track names are the clips' names). It is SHOWN when the
//   title screen opens and fades away after hideSeconds (musicPickerHideSeconds, 0 = 10) without activity (the mouse over it, a click, the list open); it fades back in while the mouse is over
//   the spot. A speaker button at its right end mutes / unmutes the music (right click opens Settings > Audio directly: PixelPauseMenu.OpenAudioSettingsFromTitle) (PixelAudio.MusicMuted, per save file, also a Settings > Audio tick box); the chosen song and the playlist are per save too. Starts the chosen track at once; the usual shuffle / order carries on after it. Hidden entirely when there are no tracks.
// ----------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelTitleMusic : MonoBehaviour
{
    private const float Width = 640f, Height = 64f, Margin = 36f, HoverMargin = 28f, SpeakerGap = 12f;

    private CanvasGroup group;
    private RectTransform rect;
    private TMP_Dropdown dropdown;
    private float hideSeconds = 10f, lastActivity, alpha;
    private bool wasShowing, refreshing, built;
    private int shownCount = -1;
    private AudioClip shownClip;
    private Button speaker;
    private Image speakerIcon;
    private int shownMuted = -1;

    public static PixelTitleMusic Create(Transform parent, TMP_FontAsset font, float hideSeconds)
    {
        GameObject go = new GameObject("Music Picker", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        PixelTitleMusic m = go.AddComponent<PixelTitleMusic>();
        m.hideSeconds = hideSeconds > 0.5f ? hideSeconds : 10f;
        m.Build(font);
        return m;
    }

    private void Build(TMP_FontAsset font)
    {
        group = GetComponent<CanvasGroup>();
        rect = GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(Width + SpeakerGap + Height, Height);   // (the speaker button sits at the right end)
        rect.anchoredPosition = new Vector2(0f, Margin);

        float arrow = Height;
        Color arrowColor = new Color(0.2f, 0.3f, 0.5f, 1f);
        Button prev = PixelUIKit.CreateButton(font, transform, "Previous", "<", new Vector2(arrow, Height), arrowColor, Color.white, 28f);
        Button next = PixelUIKit.CreateButton(font, transform, "Next", ">", new Vector2(arrow, Height), arrowColor, Color.white, 28f);
        PixelUIKit.UseArrowGlyph(prev, true);
        PixelUIKit.UseArrowGlyph(next, false);
        Place(prev.GetComponent<RectTransform>(), 0f, arrow);
        Place(next.GetComponent<RectTransform>(), Width - arrow, arrow);
        prev.onClick.AddListener(() => Step(-1));
        next.onClick.AddListener(() => Step(1));

        dropdown = PixelUIKit.CreateDropdown(font, transform, "Tracks", new Vector2(Width - arrow * 2f - 16f, Height), new Color(0.2f, 0.3f, 0.5f, 1f),
                                             new Color(0.1f, 0.12f, 0.18f, 1f), Color.white, 26f);
        Place(dropdown.GetComponent<RectTransform>(), arrow + 8f, Width - arrow * 2f - 16f);
        dropdown.onValueChanged.AddListener(v =>
        {
            if (refreshing || PixelAudio.Instance == null) return;
            lastActivity = Time.unscaledTime;
            PixelAudio.Instance.SelectMusicTrack(v);
        });

        TMP_Text cap = PixelUIKit.CreateText(font, transform, "Caption", "MUSIC", 20f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 1f, 1f, 0.75f));
        cap.raycastTarget = false;
        RectTransform cr = cap.rectTransform;
        cr.anchorMin = new Vector2(0f, 1f); cr.anchorMax = new Vector2(1f, 1f); cr.pivot = new Vector2(0.5f, 0f);
        cr.sizeDelta = new Vector2(0f, 30f);
        cr.anchoredPosition = new Vector2(0f, 2f);

        // The speaker: mutes / unmutes the music (saved per save file).
        speaker = PixelUIKit.CreateButton(font, transform, "Mute Music", "", new Vector2(Height, Height), arrowColor, Color.white, 20f);
        RectTransform sr = speaker.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = new Vector2(0f, 0.5f);
        sr.pivot = new Vector2(0f, 0.5f);
        sr.sizeDelta = new Vector2(Height, Height);
        sr.anchoredPosition = new Vector2(Width + SpeakerGap, 0f);
        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(speaker.transform, false);
        speakerIcon = icon.GetComponent<Image>();
        speakerIcon.raycastTarget = false;
        RectTransform ir = icon.GetComponent<RectTransform>();
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
        ir.sizeDelta = new Vector2(Height * 0.74f, Height * 0.62f);
        speaker.gameObject.AddComponent<SpeakerRightClick>().onRightClick = () =>
        {
            lastActivity = Time.unscaledTime;
            PixelPauseMenu.OpenAudioSettingsFromTitle();   // right click: the audio settings
        };
        speaker.onClick.AddListener(() =>
        {
            PixelAudio a = PixelAudio.Instance;
            if (a == null) return;
            lastActivity = Time.unscaledTime;
            a.MusicMuted = !a.MusicMuted;
            RefreshSpeaker();
        });

        group.alpha = 0f;
        lastActivity = Time.unscaledTime;
        built = true;
    }

    private static void Place(RectTransform r, float x, float width)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.sizeDelta = new Vector2(width, Height);
        r.anchoredPosition = new Vector2(x, 0f);
    }

    private void Step(int delta)
    {
        PixelAudio a = PixelAudio.Instance;
        if (a == null || a.MusicTrackCount == 0) return;
        lastActivity = Time.unscaledTime;
        int n = a.MusicTrackCount, cur = Mathf.Max(0, a.CurrentMusicTrack);
        a.SelectMusicTrack(((cur + delta) % n + n) % n);
    }

    private void Update()
    {
        if (!built) return;
        PixelAudio audio = PixelAudio.Instance;
        int count = audio != null ? audio.MusicTrackCount : 0;
        bool showing = PixelTitleScreen.Showing;
        if (showing && !wasShowing) lastActivity = Time.unscaledTime;   // the title screen just opened: shown for a while again
        wasShowing = showing;

        // Keep the list and the shown name in step with the music (it moves on by itself when a track ends).
        if (count != shownCount) { Rebuild(audio, count); shownClip = null; }
        // What is audible now: a playlist track (picked in the list) or a music cue's clip (named in the caption: it need not be in the list).
        AudioClip clip = audio != null ? audio.PlayingClip : null;
        if (clip != shownClip && count > 0 && clip != null)
        {
            shownClip = clip;
            int track = audio.TrackIndexOf(clip);
            refreshing = true;
            if (track >= 0) { dropdown.SetValueWithoutNotify(track); dropdown.RefreshShownValue(); }
            else if (dropdown.captionText != null) dropdown.captionText.text = PixelAudio.PrettyClipName(clip, "Music");
            refreshing = false;
        }

        int muted = audio != null && audio.MusicMuted ? 1 : 0;   // (also changes with a loaded save or Settings)
        if (muted != shownMuted) { shownMuted = muted; RefreshSpeaker(); }

        // Activity: the mouse over the box (a margin round it), a click on it, or the list being open.
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 pointer = PixelInput.PointerPosition();
        bool over = pointer.x >= corners[0].x - HoverMargin && pointer.x <= corners[2].x + HoverMargin && pointer.y >= corners[0].y - HoverMargin && pointer.y <= corners[2].y + HoverMargin;
        if (over || (dropdown != null && dropdown.IsExpanded)) lastActivity = Time.unscaledTime;

        bool visible = count > 0 && Time.unscaledTime - lastActivity < hideSeconds;
        alpha = Mathf.MoveTowards(alpha, visible ? 1f : 0f, Time.unscaledDeltaTime / (visible ? 0.25f : 0.8f));
        group.alpha = alpha;
        group.blocksRaycasts = alpha > 0.05f;
        group.interactable = alpha > 0.05f;
    }

    private void RefreshSpeaker()
    {
        if (speakerIcon == null) return;
        PixelAudio a = PixelAudio.Instance;
        bool on = a == null || !a.MusicMuted;
        speakerIcon.sprite = PixelTalk.SpeakerSprite(on);
        speakerIcon.color = new Color(1f, 1f, 1f, on ? 0.95f : 0.6f);
        shownMuted = on ? 0 : 1;
    }

    private void Rebuild(PixelAudio audio, int count)
    {
        shownCount = count;
        refreshing = true;
        dropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>(count);
        for (int i = 0; i < count; i++) options.Add(new TMP_Dropdown.OptionData(audio.MusicTrackName(i)));
        dropdown.AddOptions(options);
        refreshing = false;
    }
}

/// <summary>Reports a right click on a button (a UI Button only reacts to the left one).</summary>
public class SpeakerRightClick : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
{
    public System.Action onRightClick;
    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right) onRightClick?.Invoke();
    }
}
