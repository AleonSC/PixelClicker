// ---- System notes (moved here from CLAUDE.md) ----
// PixelEyedropper.cs
//   Eyedropper (created on demand by PixelEyedropper.Begin(callback)): hides the pause menu (PixelPauseMenu.SetMenuVisible(false)),
//   waits for the end of the frame, grabs a screenshot (ScreenCapture.CaptureScreenshotAsTexture), shows it frozen full-screen on its
//   own canvas (950) with a small loupe (colour swatch + hex code) following the pointer; left-click hands that colour to the
//   callback, right-click / Escape (PixelWindows 175) cancels, then the menu comes back. Used by the small eyedropper button next to
//   the 'Neon bar glow' tick box in Settings > Display (PixelPauseMenu.AddEyedropperButton, PixelUIKit.EyedropperSprite(); the 'Bar
//   glow colour' hue slider is gone): PixelHud.SetGlowFromColor(colour) stores an exact colour (PixelHud.GlowCustomColor, PlayerPrefs
//   PixelClicker.Setting.GlowColor, hex, per save via PixelSettingsSync), sets the hue slider to its hue and switches 'Cycle glow
//   colour' OFF; the neon bar glow of the game's black margins (PixelHud.Update) and the main menu's bar (PixelTitleScreen) read
//   PixelHud.GlowColourNow(...) = the picked colour while static, else the hue (drifting while cycling). Moving the hue slider clears
//   the picked colour; switching Cycle on drifts on from the picked colour's hue.
// ---------------------------------------------------
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// An eyedropper: freezes a picture of the screen (the menu is hidden first, so it is the game or the main menu behind it), shows a small loupe with
/// the colour under the pointer and its hex code, and hands the colour you click back. Right-click or Escape cancels. Used by Settings > Display to
/// set the neon bar glow to an exact colour taken from the background.
/// </summary>
public class PixelEyedropper : MonoBehaviour
{
    private static PixelEyedropper instance;

    private GameObject canvasRoot;
    private RectTransform loupe;
    private Image swatch;
    private TMP_Text hexLabel;
    private Texture2D shot;
    private Action<Color> onPick;
    private bool active, registered;
    private Color current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    /// <summary>True while the eyedropper is up (so the pause menu doesn't treat Escape as its own).</summary>
    public static bool Active => instance != null && instance.active;

    /// <summary>Starts picking; 'picked' is called with the colour you click (not at all if you cancel).</summary>
    public static void Begin(Action<Color> picked)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("PixelEyedropper");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PixelEyedropper>();
        }
        if (!instance.active) instance.StartCoroutine(instance.Run(picked));
    }

    private IEnumerator Run(Action<Color> picked)
    {
        active = true;
        onPick = picked;
        if (!registered)
        {
            registered = true;
            PixelWindows.Register(this, 175, () => active, Cancel);   // Escape cancels instead of opening the pause menu
        }
        PixelPauseMenu.SetMenuVisible(false);
        yield return new WaitForEndOfFrame();
        shot = ScreenCapture.CaptureScreenshotAsTexture();
        BuildOverlay();
        while (active)
        {
            Tick();
            yield return null;
        }
    }

    private void BuildOverlay()
    {
        canvasRoot = PixelUIKit.CreateCanvas("Eyedropper Canvas", 950, new Vector2(1920f, 1080f), true, false);
        canvasRoot.transform.SetParent(transform, false);
        GameObject pic = new GameObject("Screenshot", typeof(RectTransform), typeof(RawImage));
        pic.transform.SetParent(canvasRoot.transform, false);
        RawImage raw = pic.GetComponent<RawImage>();
        raw.texture = shot;
        PixelUIKit.Stretch(pic.GetComponent<RectTransform>());

        GameObject box = new GameObject("Loupe", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(canvasRoot.transform, false);
        Image back = box.GetComponent<Image>();
        back.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        back.raycastTarget = false;
        loupe = box.GetComponent<RectTransform>();
        loupe.anchorMin = loupe.anchorMax = new Vector2(0.5f, 0.5f);
        loupe.pivot = new Vector2(0f, 1f);
        loupe.sizeDelta = new Vector2(190f, 96f);

        GameObject sw = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
        sw.transform.SetParent(box.transform, false);
        swatch = sw.GetComponent<Image>();
        swatch.raycastTarget = false;
        RectTransform sr = sw.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0f, 0f);
        sr.anchorMax = new Vector2(1f, 1f);
        sr.offsetMin = new Vector2(8f, 36f);
        sr.offsetMax = new Vector2(-8f, -8f);

        PixelClicker clicker = PixelFind.First<PixelClicker>();
        hexLabel = PixelUIKit.CreateText(clicker != null ? clicker.UIFont : null, box.transform, "Hex", "", 26f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        hexLabel.raycastTarget = false;
        RectTransform hr = hexLabel.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f);
        hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.sizeDelta = new Vector2(0f, 30f);
        hr.anchoredPosition = new Vector2(0f, 4f);

        TMP_Text hint = PixelUIKit.CreateText(clicker != null ? clicker.UIFont : null, canvasRoot.transform, "Hint", "Click to pick a colour   -   right-click or Esc to cancel", 30f,
                                              TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        hint.raycastTarget = false;
        RectTransform hint_r = hint.rectTransform;
        hint_r.anchorMin = hint_r.anchorMax = hint_r.pivot = new Vector2(0.5f, 1f);
        hint_r.sizeDelta = new Vector2(1200f, 44f);
        hint_r.anchoredPosition = new Vector2(0f, -110f);
        hint.outlineWidth = 0.25f;
        hint.outlineColor = Color.black;
    }

    private void Tick()
    {
        if (shot == null || canvasRoot == null) { Finish(false); return; }
        Vector2 p = PixelInput.PointerPosition();
        int x = Mathf.Clamp((int)p.x, 0, shot.width - 1), y = Mathf.Clamp((int)p.y, 0, shot.height - 1);
        current = shot.GetPixel(x, y);
        current.a = 1f;
        swatch.color = current;
        hexLabel.text = "#" + ColorUtility.ToHtmlStringRGB(current);

        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, p, null, out Vector2 local);
        Vector2 half = canvasRect.rect.size * 0.5f, size = loupe.sizeDelta;
        Vector2 pos = local + new Vector2(26f, -26f);
        if (pos.x + size.x > half.x) pos.x = local.x - size.x - 26f;
        if (pos.y - size.y < -half.y) pos.y = local.y + size.y + 26f;
        loupe.anchoredPosition = pos;

        if (PixelInput.LeftPressed()) Finish(true);
        else if (PixelInput.RightPressed()) Finish(false);
    }

    private void Cancel() { if (active) Finish(false); }

    private void Finish(bool pick)
    {
        if (!active) return;
        active = false;
        Color picked = current;
        if (canvasRoot != null) Destroy(canvasRoot);
        if (shot != null) Destroy(shot);
        canvasRoot = null;
        shot = null;
        PixelPauseMenu.SetMenuVisible(true);
        Action<Color> callback = onPick;
        onPick = null;
        if (pick && callback != null) callback(picked);
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (shot != null) Destroy(shot);
        if (instance == this) instance = null;
    }
}
