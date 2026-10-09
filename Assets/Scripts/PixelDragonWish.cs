using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Dragon Cube wish. The seven Dragon Cubes (glassy orange cubes with 1-7 small cubes inside) are extremely rare random pixel
/// spawns. Hold one of each and a glowing "Summon the Dragon" button appears. Pressing it uses up the seven cubes, stops time, and
/// plays a summoning scene (the cubes circle, spiral together in a flash, and a cube dragon bursts out). The dragon offers three
/// wishes picked from a pool; choosing one grants it. All of it is drawn in code. Added by PixelClicker.Awake.
/// </summary>
public class PixelDragonWish : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PixelClicker whose Dragon Cubes are checked. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Button")]
    [Tooltip("Text of the summon button.")]
    [SerializeField] private string buttonText = "Summon the Dragon";

    [Header("Wishes")]
    [Min(60f)]
    [Tooltip("Fortune: how many seconds of your current income it pays out at once.")]
    [SerializeField] private float fortuneSeconds = 1200f;

    [Min(1f)]
    [Tooltip("Golden Hour: the payout multiplier.")]
    [SerializeField] private float goldenMultiplier = 3f;

    [Min(10f)]
    [Tooltip("Golden Hour: how long it lasts (seconds).")]
    [SerializeField] private float goldenSeconds = 600f;

    [Min(1)]
    [Tooltip("Ultra Blessing: how many Ultra pixels it gives (each of a random unlocked type).")]
    [SerializeField] private int blessingUltra = 5;

    [Min(1)]
    [Tooltip("Potion Stock: how many random potions it gives.")]
    [SerializeField] private int stockPotions = 3;

    [Header("Scene")]
    [Min(0.5f)]
    [Tooltip("Seconds the seven cubes circle before they spiral together.")]
    [SerializeField] private float gatherSeconds = 2.4f;

    [Min(10)]
    [Tooltip("How many cubes the dragon's body is made of.")]
    [SerializeField] private int bodySegments = 28;

    // ------------------------------------------------------------------

    private class Wish
    {
        public string name, description;
        public Func<bool> available;
        public Action apply;
    }

    private bool busy;
    private float savedTimeScale = 1f;
    private bool froze;
    private float goldenLeft;

    private GameObject buttonCanvas, buttonObject;
    private RectTransform buttonRect;
    private Image buttonGlow;
    private GameObject sceneRoot;
    private RectTransform sceneRect;
    private TMP_FontAsset Font => clicker != null ? clicker.UIFont : null;

    private void Awake()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private void OnDestroy()
    {
        Unfreeze();
        PixelClicker.WishMultiplier = 1f;
    }

    private void Update()
    {
        if (goldenLeft > 0f)
        {
            goldenLeft -= Time.deltaTime;
            if (goldenLeft <= 0f)
            {
                PixelClicker.WishMultiplier = 1f;
                PixelHints.Announce("The Golden Hour is over.");
            }
        }

        bool ready = clicker != null && !busy && clicker.HasAllDragonCubes && !PixelTitleScreen.Showing && !PixelPauseMenu.IsPaused
                     && !PixelMinigame.TakeoverActive && Time.timeScale > 0f;
        if (ready) EnsureButton();
        if (buttonObject != null && buttonObject.activeSelf != ready) buttonObject.SetActive(ready);
        if (ready && buttonRect != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            buttonRect.localScale = Vector3.one * (1f + 0.05f * pulse);
            if (buttonGlow != null) buttonGlow.color = new Color(1f, 0.6f, 0.1f, 0.25f + 0.35f * pulse);
        }
    }

    private void EnsureButton()
    {
        if (buttonCanvas != null) return;
        buttonCanvas = PixelUIKit.CreateCanvas("PixelDragonWish Canvas", 630, new Vector2(1920f, 1080f), true);
        buttonCanvas.transform.SetParent(transform, false);

        GameObject glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        glow.transform.SetParent(buttonCanvas.transform, false);
        buttonGlow = glow.GetComponent<Image>();
        buttonGlow.raycastTarget = false;
        RectTransform gr = glow.GetComponent<RectTransform>();

        Button b = PixelUIKit.CreateButton(Font, buttonCanvas.transform, "Summon Button", buttonText, new Vector2(560f, 92f),
                                           new Color(0.88f, 0.42f, 0.04f, 1f), Color.white, 42f);
        PixelUIKit.Caps(b);
        buttonObject = b.gameObject;
        buttonRect = b.GetComponent<RectTransform>();
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 1f);
        buttonRect.anchoredPosition = new Vector2(0f, -(bar + 30f));
        b.onClick.AddListener(() => { if (!busy) StartCoroutine(SummonRoutine()); });

        gr.anchorMin = gr.anchorMax = gr.pivot = new Vector2(0.5f, 1f);
        gr.sizeDelta = new Vector2(620f, 150f);
        gr.anchoredPosition = new Vector2(0f, -(bar + 1f));
        glow.transform.SetSiblingIndex(0);
    }

    // ------------------------------------------------------------------
    // Time
    // ------------------------------------------------------------------

    private void Freeze()
    {
        if (froze || Time.timeScale <= 0f) return;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        froze = true;
    }

    private void Unfreeze()
    {
        if (!froze) return;
        froze = false;
        if (Time.timeScale <= 0f) Time.timeScale = savedTimeScale;
    }

    // ------------------------------------------------------------------
    // The summoning scene
    // ------------------------------------------------------------------

    private IEnumerator SummonRoutine()
    {
        busy = true;
        if (buttonObject != null) buttonObject.SetActive(false);

        // The seven cubes are used up.
        for (int s = 0; s < 7; s++)
        {
            int index = clicker.IndexOf((PixelClicker.PixelType)((int)PixelClicker.PixelType.DragonCube1 + s));
            if (index >= 0) clicker.TrySpend(index, 1d);
        }
        PixelStats.Count("dragon.summons");
        Freeze();
        PixelWindows.Register(this, 160, () => busy, () => { }); // Escape does nothing during the wish (but doesn't open the pause menu)
        PixelAudio.Play("dragon_summon");
        PixelHints.Announce("The seven Dragon Cubes begin to glow...");

        BuildScene();
        Image dim = Fill(sceneRect, "Dim", new Color(0f, 0f, 0f, 0f));
        Image flash = null;

        // 1. The sky darkens while the seven cubes rise into a ring.
        RectTransform[] cubes = new RectTransform[7];
        for (int i = 0; i < 7; i++) cubes[i] = MakeDragonCube(sceneRect, i + 1, 96f);
        flash = Fill(sceneRect, "Flash", new Color(1f, 1f, 1f, 0f));
        flash.transform.SetAsLastSibling();

        float centreY = -30f, ringRadius = 340f;
        float angle = 0f, spin = 60f;
        for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.2f);
            dim.color = new Color(0f, 0f, 0f, 0.8f * k);
            angle += spin * Time.unscaledDeltaTime;
            PlaceRing(cubes, angle, Mathf.Lerp(0f, ringRadius, k), centreY - (1f - k) * 500f, 1f);
            yield return null;
        }

        // 2. They circle faster and faster, pulsing, with flickers of lightning.
        float flashTimer = 0f;
        for (float t = 0f; t < gatherSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / gatherSeconds;
            spin = Mathf.Lerp(60f, 420f, k * k);
            angle += spin * Time.unscaledDeltaTime;
            float pulse = 1f + 0.12f * Mathf.Sin(t * 14f);
            PlaceRing(cubes, angle, ringRadius, centreY, pulse);

            flashTimer -= Time.unscaledDeltaTime;
            if (flashTimer <= 0f) { flashTimer = UnityEngine.Random.Range(0.15f, 0.45f); flash.color = new Color(1f, 0.9f, 0.6f, UnityEngine.Random.Range(0.15f, 0.4f) * (0.4f + k)); }
            else flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, Mathf.MoveTowards(flash.color.a, 0f, Time.unscaledDeltaTime * 2.5f));
            yield return null;
        }

        // 3. They spiral into the middle and burst into a white flash.
        const float spiralSeconds = 0.9f;
        for (float t = 0f; t < spiralSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / spiralSeconds;
            spin = Mathf.Lerp(420f, 1400f, k);
            angle += spin * Time.unscaledDeltaTime;
            PlaceRing(cubes, angle, Mathf.Lerp(ringRadius, 0f, k * k), centreY, Mathf.Lerp(1f, 0.6f, k));
            flash.color = new Color(1f, 1f, 1f, k * k);
            yield return null;
        }
        foreach (RectTransform c in cubes) Destroy(c.gameObject);

        // 4. The dragon bursts out.
        Dragon dragon = BuildDragon();
        flash.transform.SetAsLastSibling();
        float dragonTime = 0f;
        for (float t = 0f; t < 2.2f; t += Time.unscaledDeltaTime)
        {
            dragonTime += Time.unscaledDeltaTime;
            flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - t / 0.5f));
            AnimateDragon(dragon, dragonTime, centreY);
            yield return null;
        }

        // 5. The offer.
        List<Wish> offer = PickWishes();
        int chosen = -1;
        GameObject panel = BuildOffer(offer, i => chosen = i);
        flash.transform.SetAsLastSibling();
        while (chosen < 0)
        {
            dragonTime += Time.unscaledDeltaTime;
            AnimateDragon(dragon, dragonTime, centreY);
            yield return null;
        }
        Destroy(panel);

        // 6. The wish is granted and the dragon leaves.
        Wish wish = offer[chosen];
        wish.apply();
        PixelStats.Count("dragon.wishes");
        PixelHints.Announce("Wish granted: " + wish.name + "!");
        PixelAudio.Play("purchase");
        GameObject doneText = BuildMessage("IT IS DONE!", "Your wish has been granted: " + wish.name);
        for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
        {
            dragonTime += Time.unscaledDeltaTime;
            AnimateDragon(dragon, dragonTime, centreY, Mathf.Clamp01((t - 0.9f) / 1.1f));
            flash.color = new Color(1f, 1f, 1f, t > 1.6f ? (t - 1.6f) / 0.4f : 0f);
            yield return null;
        }
        Destroy(doneText);
        Destroy(dragon.root);
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / 0.6f;
            flash.color = new Color(1f, 1f, 1f, k);
            dim.color = new Color(0f, 0f, 0f, 0.8f * k);
            yield return null;
        }

        Destroy(sceneRoot);
        sceneRoot = null;
        PixelWindows.Unregister(this);
        Unfreeze();
        busy = false;
    }

    private void BuildScene()
    {
        if (sceneRoot != null) Destroy(sceneRoot);
        PixelUIKit.EnsureEventSystem();
        sceneRoot = PixelUIKit.CreateCanvas("PixelDragonWish Scene", 730, new Vector2(1920f, 1080f), true);
        sceneRect = sceneRoot.GetComponent<RectTransform>();
    }

    /// <summary>A full-screen colour layer that also blocks clicks on everything behind it.</summary>
    private Image Fill(RectTransform parent, string name, Color color)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        Image im = g.GetComponent<Image>();
        im.color = color;
        im.raycastTarget = name == "Dim";
        PixelUIKit.Stretch(g.GetComponent<RectTransform>());
        return im;
    }

    private RectTransform Sq(Transform parent, string name, Color color, Vector2 size, Vector2 pos, float rotation = 0f)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        Image im = g.GetComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        r.localRotation = Quaternion.Euler(0f, 0f, rotation);
        return r;
    }

    /// <summary>A Dragon Cube drawn with blocks: a glassy orange square with 1-7 dark orange small squares laid out like dragon-ball stars.</summary>
    private RectTransform MakeDragonCube(Transform parent, int stars, float size)
    {
        RectTransform root = Sq(parent, "Dragon Cube " + stars, new Color(1f, 0.55f, 0.1f, 0f), new Vector2(size, size), Vector2.zero);
        Sq(root, "Aura", new Color(1f, 0.6f, 0.15f, 0.3f), new Vector2(size * 1.5f, size * 1.5f), Vector2.zero, 45f);
        Sq(root, "Edge", new Color(1f, 0.85f, 0.5f, 1f), new Vector2(size, size), Vector2.zero);
        Sq(root, "Body", new Color(1f, 0.58f, 0.1f, 0.92f), new Vector2(size * 0.9f, size * 0.9f), Vector2.zero);
        Sq(root, "Shine", new Color(1f, 0.9f, 0.6f, 0.45f), new Vector2(size * 0.28f, size * 0.28f), new Vector2(-size * 0.25f, size * 0.25f));
        float star = size * 0.18f;
        foreach (Vector2 spot in StarSpots(stars))
            Sq(root, "Star", new Color(0.55f, 0.18f, 0.02f, 1f), new Vector2(star, star), spot * size);
        return root;
    }

    /// <summary>Where the small cubes sit (fractions of the cube's size), the dragon-ball layouts.</summary>
    private static Vector2[] StarSpots(int count)
    {
        switch (count)
        {
            case 1: return new[] { Vector2.zero };
            case 2: return new[] { new Vector2(-0.17f, 0f), new Vector2(0.17f, 0f) };
            case 3: return new[] { new Vector2(0f, 0.17f), new Vector2(-0.18f, -0.12f), new Vector2(0.18f, -0.12f) };
            case 4: return new[] { new Vector2(-0.16f, 0.16f), new Vector2(0.16f, 0.16f), new Vector2(-0.16f, -0.16f), new Vector2(0.16f, -0.16f) };
            case 5: return new[] { new Vector2(-0.2f, 0.2f), new Vector2(0.2f, 0.2f), Vector2.zero, new Vector2(-0.2f, -0.2f), new Vector2(0.2f, -0.2f) };
            case 6: return new[] { new Vector2(-0.16f, 0.22f), new Vector2(0.16f, 0.22f), new Vector2(-0.16f, 0f), new Vector2(0.16f, 0f), new Vector2(-0.16f, -0.22f), new Vector2(0.16f, -0.22f) };
            default:
                Vector2[] spots = new Vector2[7];
                for (int i = 0; i < 6; i++) spots[i + 1] = new Vector2(Mathf.Cos(i * Mathf.PI / 3f), Mathf.Sin(i * Mathf.PI / 3f)) * 0.22f;
                return spots;
        }
    }

    private static void PlaceRing(RectTransform[] cubes, float angleDegrees, float radius, float centreY, float scale)
    {
        for (int i = 0; i < cubes.Length; i++)
        {
            float a = (angleDegrees + i * 360f / cubes.Length) * Mathf.Deg2Rad;
            cubes[i].anchoredPosition = new Vector2(Mathf.Cos(a) * radius, centreY + Mathf.Sin(a) * radius * 0.55f);
            cubes[i].localRotation = Quaternion.Euler(0f, 0f, angleDegrees * 0.5f);
            cubes[i].localScale = Vector3.one * scale;
        }
    }

    // ------------------------------------------------------------------
    // The cube dragon
    // ------------------------------------------------------------------

    private class Dragon
    {
        public RectTransform root;
        public RectTransform[] segments;
        public RectTransform head;
        public RectTransform[] whiskers;
    }

    private Dragon BuildDragon()
    {
        Dragon d = new Dragon();
        GameObject holder = new GameObject("Dragon", typeof(RectTransform));
        holder.transform.SetParent(sceneRect, false);
        d.root = holder.GetComponent<RectTransform>();
        PixelUIKit.Stretch(d.root);

        Color body = new Color(1f, 0.58f, 0.1f, 0.95f), belly = new Color(1f, 0.85f, 0.45f, 1f), spine = new Color(0.62f, 0.22f, 0.03f, 1f);
        d.segments = new RectTransform[bodySegments];
        for (int i = bodySegments - 1; i >= 0; i--) // tail first so the head ends up on top
        {
            float size = Mathf.Lerp(120f, 40f, i / (float)(bodySegments - 1));
            RectTransform s = Sq(d.root, "Segment " + i, spine, new Vector2(size, size), Vector2.zero);
            Sq(s, "Body", body, new Vector2(size * 0.9f, size * 0.9f), Vector2.zero);
            Sq(s, "Belly", belly, new Vector2(size * 0.9f, size * 0.3f), new Vector2(0f, -size * 0.3f));
            Sq(s, "Ridge", spine, new Vector2(size * 0.22f, size * 0.22f), new Vector2(0f, size * 0.52f), 45f);
            s.localScale = Vector3.zero;
            d.segments[i] = s;
        }

        // The head faces +x (the direction it moves) and is rotated to follow its path.
        d.head = Sq(d.root, "Head", spine, new Vector2(170f, 150f), Vector2.zero);
        Sq(d.head, "Mane 1", new Color(0.8f, 0.2f, 0.05f, 1f), new Vector2(70f, 70f), new Vector2(-95f, 40f), 45f);
        Sq(d.head, "Mane 2", new Color(0.8f, 0.2f, 0.05f, 1f), new Vector2(70f, 70f), new Vector2(-95f, -40f), 45f);
        Sq(d.head, "Face", body, new Vector2(155f, 135f), Vector2.zero);
        Sq(d.head, "Snout", belly, new Vector2(95f, 95f), new Vector2(95f, -8f));
        Sq(d.head, "Nostril 1", spine, new Vector2(16f, 16f), new Vector2(130f, 22f));
        Sq(d.head, "Nostril 2", spine, new Vector2(16f, 16f), new Vector2(130f, -34f));
        Sq(d.head, "Horn 1", new Color(1f, 0.95f, 0.8f, 1f), new Vector2(26f, 90f), new Vector2(-30f, 105f), 25f);
        Sq(d.head, "Horn 2", new Color(1f, 0.95f, 0.8f, 1f), new Vector2(26f, 90f), new Vector2(30f, 105f), -10f);
        Sq(d.head, "Eye Socket", spine, new Vector2(52f, 52f), new Vector2(28f, 32f));
        Sq(d.head, "Eye", new Color(1f, 1f, 0.7f, 1f), new Vector2(40f, 40f), new Vector2(28f, 32f));
        Sq(d.head, "Pupil", new Color(0.8f, 0.1f, 0.05f, 1f), new Vector2(16f, 30f), new Vector2(34f, 32f));
        d.whiskers = new[]
        {
            Sq(d.head, "Whisker 1", new Color(1f, 0.85f, 0.3f, 1f), new Vector2(150f, 8f), new Vector2(150f, 40f)),
            Sq(d.head, "Whisker 2", new Color(1f, 0.85f, 0.3f, 1f), new Vector2(150f, 8f), new Vector2(150f, -50f)),
        };
        d.head.localScale = Vector3.zero;
        return d;
    }

    /// <summary>Follow-the-leader: every segment sits where the head was a moment ago. 'leave' (0..1) lifts the dragon away at the end.</summary>
    private void AnimateDragon(Dragon d, float time, float startY, float leave = 0f)
    {
        float topY = 250f;
        Vector2 prev = Vector2.zero;
        float lag = 0.075f;
        for (int i = -1; i < d.segments.Length; i++)
        {
            float tau = time - (i + 1) * lag;
            Vector2 pos = HeadPath(tau, startY, topY) + new Vector2(0f, leave * leave * 1400f);
            float visible = tau < 0f ? 0f : Mathf.Clamp01(tau / 0.2f);
            RectTransform r = i < 0 ? d.head : d.segments[i];
            r.anchoredPosition = pos;
            r.localScale = Vector3.one * visible * (1f + (i < 0 ? 0f : 0.03f * Mathf.Sin(time * 8f - i)));
            if (i >= 0)
            {
                Vector2 dir = prev - pos;
                if (dir.sqrMagnitude > 0.01f) r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
            else
            {
                Vector2 ahead = HeadPath(tau + 0.05f, startY, topY) + new Vector2(0f, leave * leave * 1400f);
                Vector2 dir = ahead - pos;
                if (dir.sqrMagnitude > 0.01f) r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                for (int w = 0; w < d.whiskers.Length; w++)
                    d.whiskers[w].localRotation = Quaternion.Euler(0f, 0f, (w == 0 ? 12f : -12f) + Mathf.Sin(time * 5f + w * 2f) * 14f);
            }
            prev = pos;
        }
    }

    /// <summary>Where the head is at time 'tau': it shoots up from the centre, then weaves left and right near the top.</summary>
    private static Vector2 HeadPath(float tau, float startY, float topY)
    {
        if (tau <= 0f) return new Vector2(0f, startY);
        float rise = 1f - (1f - Mathf.Clamp01(tau / 1.5f)) * (1f - Mathf.Clamp01(tau / 1.5f));
        float y = startY + (topY - startY) * rise;
        float x = 360f * Mathf.Sin(tau * 2.6f) * Mathf.Clamp01(tau / 0.7f);
        if (tau > 1.5f) y += 22f * Mathf.Sin((tau - 1.5f) * 2.1f);
        return new Vector2(x, y);
    }

    // ------------------------------------------------------------------
    // The offer
    // ------------------------------------------------------------------

    private GameObject BuildOffer(List<Wish> wishes, Action<int> pick)
    {
        GameObject panel = new GameObject("Offer", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(sceneRect, false);
        panel.GetComponent<Image>().color = new Color(0.1f, 0.05f, 0.02f, 0.94f);
        RectTransform pr = panel.GetComponent<RectTransform>();
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.sizeDelta = new Vector2(1500f, 400f);
        pr.anchoredPosition = new Vector2(0f, bar + 24f);

        TMP_Text title = PixelUIKit.CreateText(Font, panel.transform, "Title", "Who has gathered the seven Dragon Cubes? Name your wish!", 40f,
                                               TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.82f, 0.4f));
        PixelUIKit.Caps(title);
        title.enableAutoSizing = true; title.fontSizeMax = 40f; title.fontSizeMin = 20f;
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-60f, 70f);
        tr.anchoredPosition = new Vector2(0f, -18f);

        float cardW = 440f, gap = 24f;
        float total = wishes.Count * cardW + (wishes.Count - 1) * gap;
        for (int i = 0; i < wishes.Count; i++)
        {
            Wish w = wishes[i];
            int captured = i;
            Button card = PixelUIKit.CreateButton(Font, panel.transform, "Wish " + i, "", new Vector2(cardW, 270f), new Color(0.32f, 0.16f, 0.05f, 1f), Color.white, 30f);
            RectTransform cr = card.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f);
            cr.pivot = new Vector2(0.5f, 0f);
            cr.anchoredPosition = new Vector2(-total * 0.5f + cardW * 0.5f + i * (cardW + gap), 24f);

            TMP_Text name = PixelUIKit.CreateText(Font, card.transform, "Name", w.name, 38f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.75f, 0.25f));
            PixelUIKit.Caps(name);
            name.enableAutoSizing = true; name.fontSizeMax = 38f; name.fontSizeMin = 18f;
            RectTransform nr = name.rectTransform;
            nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f); nr.pivot = new Vector2(0.5f, 1f);
            nr.sizeDelta = new Vector2(-30f, 60f);
            nr.anchoredPosition = new Vector2(0f, -16f);

            TMP_Text desc = PixelUIKit.CreateText(Font, card.transform, "Description", w.description, 28f, TextAlignmentOptions.Top, FontStyles.Normal, Color.white);
            desc.enableAutoSizing = true; desc.fontSizeMax = 28f; desc.fontSizeMin = 14f;
            RectTransform dr = desc.rectTransform;
            dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
            dr.offsetMin = new Vector2(20f, 16f); dr.offsetMax = new Vector2(-20f, -84f);

            // The default centred label is empty; make sure it can't catch clicks.
            TMP_Text label = card.GetComponentInChildren<TMP_Text>();
            if (label != null && label != name && label != desc) label.gameObject.SetActive(false);
            card.onClick.AddListener(() => pick(captured));
        }
        return panel;
    }

    private GameObject BuildMessage(string big, string small)
    {
        GameObject panel = new GameObject("Message", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(sceneRect, false);
        panel.GetComponent<Image>().color = new Color(0.1f, 0.05f, 0.02f, 0.94f);
        panel.GetComponent<Image>().raycastTarget = false;
        RectTransform pr = panel.GetComponent<RectTransform>();
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.sizeDelta = new Vector2(1100f, 220f);
        pr.anchoredPosition = new Vector2(0f, bar + 40f);

        TMP_Text a = PixelUIKit.CreateText(Font, panel.transform, "Big", big, 80f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.8f, 0.3f));
        PixelUIKit.Caps(a);
        RectTransform ar = a.rectTransform;
        ar.anchorMin = new Vector2(0f, 0.45f); ar.anchorMax = Vector2.one; ar.offsetMin = ar.offsetMax = Vector2.zero;
        TMP_Text b = PixelUIKit.CreateText(Font, panel.transform, "Small", small, 34f, TextAlignmentOptions.Center, FontStyles.Normal, Color.white);
        b.enableAutoSizing = true; b.fontSizeMax = 34f; b.fontSizeMin = 16f;
        RectTransform br = b.rectTransform;
        br.anchorMin = Vector2.zero; br.anchorMax = new Vector2(1f, 0.45f); br.offsetMin = new Vector2(30f, 10f); br.offsetMax = new Vector2(-30f, 0f);
        return panel;
    }

    // ------------------------------------------------------------------
    // The wishes
    // ------------------------------------------------------------------

    private List<Wish> PickWishes()
    {
        List<Wish> pool = new List<Wish>
        {
            new Wish
            {
                name = "Fortune",
                description = "A mountain of pixels pours in: about " + Mathf.RoundToInt(fortuneSeconds / 60f) + " minutes of your income, all at once.",
                available = () => true,
                apply = () => GiveIncome(fortuneSeconds),
            },
            new Wish
            {
                name = "Golden Hour",
                description = "Everything you click pays x" + goldenMultiplier.ToString("0.#") + " for " + Mathf.RoundToInt(goldenSeconds / 60f) + " minutes.",
                available = () => true,
                apply = () => { PixelClicker.WishMultiplier = goldenMultiplier; goldenLeft = goldenSeconds; },
            },
            new Wish
            {
                name = "Ultra Blessing",
                description = blessingUltra + " Ultra pixels, each of a random pixel type you have unlocked.",
                available = () => true,
                apply = GiveUltra,
            },
            new Wish
            {
                name = "Potion Stock",
                description = stockPotions + " random potions, ready to drink.",
                available = () => PixelFind.First<PixelConsumables>() != null,
                apply = GivePotions,
            },
            new Wish
            {
                name = "A New Friend",
                description = "A pet you don't have yet joins you (a random pixel type).",
                available = () => UnownedPetTypes().Count > 0,
                apply = GivePet,
            },
        };
        pool.RemoveAll(w => !w.available());

        List<Wish> offer = new List<Wish>();
        while (offer.Count < 3 && pool.Count > 0)
        {
            int i = UnityEngine.Random.Range(0, pool.Count);
            offer.Add(pool[i]);
            pool.RemoveAt(i);
        }
        return offer;
    }

    /// <summary>Pays 'seconds' of your current income at once: your auto clicker's rate plus a few manual clicks a second, split across your spawning pixels.</summary>
    private void GiveIncome(float seconds)
    {
        PixelAutoClicker auto = PixelFind.First<PixelAutoClicker>();
        float rate = 3f; // manual clicks a second
        if (auto != null && auto.Running && auto.Interval > 0f) rate += auto.ClicksPerTick / auto.Interval;
        double clicks = seconds * rate;

        float totalWeight = 0f;
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
            if (t.CanSpawn && !t.rareDrop) totalWeight += t.spawnWeight;
        if (totalWeight <= 0f) return;

        for (int i = 0; i < clicker.Tiers.Length; i++)
        {
            PixelClicker.PixelTier t = clicker.Tiers[i];
            if (!t.CanSpawn || t.rareDrop) continue;
            double amount = clicks * (t.spawnWeight / totalWeight) * t.amountPerClick * clicker.PayoutMultiplier(i)
                            * clicker.ClickMultiplier / Math.Max(1, t.clicksToCollect);
            if (amount > 0d) clicker.AddCurrency(i, Math.Floor(amount));
        }
    }

    private void GiveUltra()
    {
        List<int> unlocked = new List<int>();
        for (int i = 0; i < clicker.Tiers.Length; i++)
            if (clicker.Tiers[i].unlocked && !clicker.Tiers[i].rareDrop) unlocked.Add(i);
        if (unlocked.Count == 0) return;
        for (int n = 0; n < blessingUltra; n++) clicker.AddUltra(unlocked[UnityEngine.Random.Range(0, unlocked.Count)], 1);
    }

    private void GivePotions()
    {
        PixelConsumables consumables = PixelFind.First<PixelConsumables>();
        if (consumables == null) return;
        List<int> candidates = new List<int>();
        for (int i = 0; i < consumables.ItemCount; i++)
            if (!consumables.IsDevice(i) && !consumables.ItemCraftOnly(i) && clicker.IsUnlocked(consumables.ItemRequiredType(i))) candidates.Add(i);
        for (int n = 0; n < stockPotions && candidates.Count > 0; n++)
        {
            int item = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            if (consumables.ItemRoom(item) >= 1) consumables.AddItem(item, 1);
            else candidates.Remove(item);
        }
    }

    private List<PixelClicker.PixelType> UnownedPetTypes()
    {
        List<PixelClicker.PixelType> list = new List<PixelClicker.PixelType>();
        PixelPets pets = PixelPets.Instance;
        if (pets == null || clicker == null) return list;
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
            if (!t.rareDrop && !pets.IsOwned(t.type)) list.Add(t.type);
        return list;
    }

    private void GivePet()
    {
        List<PixelClicker.PixelType> list = UnownedPetTypes();
        if (list.Count == 0) { GiveIncome(fortuneSeconds); return; }
        PixelClicker.PixelType type = list[UnityEngine.Random.Range(0, list.Count)];
        PixelPets.Instance.Award(type, false);
        int tier = clicker.IndexOf(type);
        PixelHints.Announce("A new pet joined you: " + (tier >= 0 ? clicker.Tiers[tier].displayName : type.ToString()) + " Pet!");
    }
}
