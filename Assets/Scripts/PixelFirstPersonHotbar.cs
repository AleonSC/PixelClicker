// ---- System notes ----
// PixelFirstPersonHotbar.cs
//   The first person HOTBAR (added by PixelFirstPerson.Enter, removed on Exit): five slots along the bottom, picked with the mouse wheel (when you are not holding a cube - then the wheel
//   moves the cube) or the number keys 1-5. Slot 1 holds the AXE; the other slots are empty hands (pick things up, click the cube). While the axe is picked it is drawn in your hand
//   (a viewmodel built from primitives, child of the first person camera) and StartSwing(onImpact) plays the swing: wind-up (0 - 0.3), strike (0.3 - 0.5, onImpact fires at 0.5 = the
//   moment it connects), recover. The swing sound is played here; the hit sound and the rewards are PixelFirstPerson's (ChopOldPixel / the axe bonus on PixelCollected).
//   Tunables are serialized on PixelFirstPerson ("Axe") and passed into Setup.
// ---------------------------------------------------
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelFirstPersonHotbar : MonoBehaviour
{
    public const int Slots = 5;

    public int Selected { get; private set; }
    public bool AxeSelected => Selected == 0;
    public bool Swinging => swingT >= 0f;
    /// <summary>Set every frame by PixelFirstPerson: true while a cube is in hand (the wheel belongs to the cube then, and the axe is put away).</summary>
    public bool Busy;

    private PixelClicker clicker;
    private Transform cam;
    private float eye, swingSeconds;
    private GameObject axe;
    private Transform axeRoot;
    private Image[] frames = new Image[Slots];
    private RectTransform[] boxes = new RectTransform[Slots];
    private float swingT = -1f;
    private bool impacted;
    private Action onImpact;
    private float equip = 1f;   // 0 = lowered, 1 = in hand (slides in when selected)
    private readonly System.Collections.Generic.List<Material> materials = new System.Collections.Generic.List<Material>();

    public void Setup(PixelClicker owner, Camera camera, Transform canvas, float eyeHeight, float swingTime)
    {
        clicker = owner; cam = camera.transform; eye = eyeHeight; swingSeconds = Mathf.Max(0.15f, swingTime);
        BuildBar(canvas);
        BuildAxe();
    }

    private void OnDestroy()
    {
        if (axe != null) Destroy(axe);
        foreach (Material m in materials) if (m != null) Destroy(m);
    }

    // ---------------- selection ----------------

    private void Update()
    {
        bool locked = Cursor.lockState == CursorLockMode.Locked;   // (with the mouse free - a window is open - keys and wheel belong to the UI)
        for (int i = 0; i < Slots && locked; i++)
            if (PixelKeys.KeyPressed(KeyCode.Alpha1 + i)) Select(i);
        if (!Busy && locked)
        {
            float scroll = PixelInput.ScrollY();
            if (Mathf.Abs(scroll) > 0.001f) Select((Selected + (scroll < 0f ? 1 : Slots - 1)) % Slots);
        }

        for (int i = 0; i < Slots; i++)
        {
            bool on = i == Selected;
            boxes[i].localScale = Vector3.Lerp(boxes[i].localScale, Vector3.one * (on ? 1.15f : 1f), 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            frames[i].color = on ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(1f, 1f, 1f, 0.18f);
        }
        UpdateAxe();
    }

    private void Select(int i)
    {
        if (i == Selected) return;
        Selected = i;
        equip = 0f;
        swingT = -1f;
        onImpact = null;
        PixelAudio.Play("ui_scroll");
    }

    /// <summary>Starts a swing; 'impact' is called when the blade connects. False if the axe is not in hand or already swinging.</summary>
    public bool StartSwing(Action impact)
    {
        if (!AxeSelected || Busy || Swinging) return false;
        swingT = 0f; impacted = false; onImpact = impact;
        PixelAudio.Play("axe_swing");
        return true;
    }

    // ---------------- the axe in hand ----------------

    private void UpdateAxe()
    {
        if (axe == null) return;
        bool show = AxeSelected && !Busy;
        if (axe.activeSelf != show) axe.SetActive(show);
        if (!show) return;
        float dt = Time.unscaledDeltaTime;
        equip = Mathf.MoveTowards(equip, 1f, dt * 4f);

        Vector3 restPos = new Vector3(0.24f, -0.24f, 0.46f) * eye;
        Vector3 restEuler = new Vector3(28f, -18f, -12f);
        Vector3 pos = restPos, euler = restEuler;
        float idle = Mathf.Sin(Time.unscaledTime * 1.7f);
        pos += new Vector3(0f, idle * 0.006f, 0f) * eye;

        if (swingT >= 0f)
        {
            swingT += dt / swingSeconds;
            float t = swingT;
            Vector3 windPos = new Vector3(0.2f, -0.02f, 0.34f) * eye, windEuler = new Vector3(-62f, -8f, -4f);
            Vector3 hitPos = new Vector3(0.05f, -0.2f, 0.62f) * eye, hitEuler = new Vector3(100f, 4f, 8f);
            if (t < 0.3f) { float k = Ease(t / 0.3f); pos = Vector3.Lerp(restPos, windPos, k); euler = Vector3.Lerp(restEuler, windEuler, k); }
            else if (t < 0.5f) { float k = (t - 0.3f) / 0.2f; k = k * k; pos = Vector3.Lerp(windPos, hitPos, k); euler = Vector3.Lerp(windEuler, hitEuler, k); }
            else { float k = Ease(Mathf.Clamp01((t - 0.5f) / 0.5f)); pos = Vector3.Lerp(hitPos, restPos, k); euler = Vector3.Lerp(hitEuler, restEuler, k); }
            if (!impacted && t >= 0.5f)
            {
                impacted = true;
                Action a = onImpact; onImpact = null;
                if (a != null) a();
            }
            if (t >= 1f) swingT = -1f;
        }
        pos.y -= (1f - equip) * 0.4f * eye;   // slides up into view when picked
        axeRoot.localPosition = pos;
        axeRoot.localRotation = Quaternion.Euler(euler);
    }

    private static float Ease(float k) => k * k * (3f - 2f * k);

    private Material Mat(Color c, float metallic, float smooth)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        Material m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        materials.Add(m);
        return m;
    }

    private GameObject Part(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Vector3 euler = default)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Collider c = g.GetComponent<Collider>();
        if (c != null) Destroy(c);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = localScale;
        g.transform.localRotation = Quaternion.Euler(euler);
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return g;
    }

    /// <summary>A woodcutter's axe: a long wooden haft with a grip, a steel head with a bright edge, a back poll and a leather binding. Axe length is along the root's up axis (origin at the grip).</summary>
    private void BuildAxe()
    {
        axe = new GameObject("First Person Axe");
        axe.transform.SetParent(cam, false);
        axeRoot = axe.transform;
        Material wood = Mat(new Color(0.55f, 0.36f, 0.19f), 0f, 0.3f);
        Material grip = Mat(new Color(0.25f, 0.16f, 0.1f), 0f, 0.15f);
        Material steel = Mat(new Color(0.62f, 0.65f, 0.7f), 0.85f, 0.65f);
        Material edge = Mat(new Color(0.9f, 0.93f, 0.97f), 0.9f, 0.85f);
        Material leather = Mat(new Color(0.3f, 0.2f, 0.12f), 0f, 0.1f);
        float e = eye;
        Part("Haft", axeRoot, new Vector3(0f, 0.17f, 0f) * e, new Vector3(0.026f, 0.5f, 0.03f) * e, wood);
        Part("Grip", axeRoot, new Vector3(0f, -0.02f, 0f) * e, new Vector3(0.034f, 0.16f, 0.038f) * e, grip);
        Part("Knob", axeRoot, new Vector3(0f, -0.1f, 0f) * e, new Vector3(0.042f, 0.03f, 0.046f) * e, grip);
        // The head sits at the top of the haft: a steel block (the poll behind), a wide wedge towards the front and a bright cutting edge.
        float top = 0.36f;
        Part("Poll", axeRoot, new Vector3(0f, top, -0.045f) * e, new Vector3(0.05f, 0.075f, 0.05f) * e, steel);
        Part("Eye", axeRoot, new Vector3(0f, top, 0f) * e, new Vector3(0.05f, 0.09f, 0.05f) * e, steel);
        Part("Cheek", axeRoot, new Vector3(0f, top, 0.06f) * e, new Vector3(0.038f, 0.14f, 0.075f) * e, steel);
        Part("Bit", axeRoot, new Vector3(0f, top, 0.105f) * e, new Vector3(0.022f, 0.19f, 0.03f) * e, steel, new Vector3(0f, 0f, 0f));
        Part("Edge", axeRoot, new Vector3(0f, top, 0.125f) * e, new Vector3(0.012f, 0.2f, 0.012f) * e, edge);
        Part("Binding", axeRoot, new Vector3(0f, top - 0.075f, 0f) * e, new Vector3(0.034f, 0.035f, 0.036f) * e, leather);
        axe.SetActive(false);
    }

    // ---------------- the bar ----------------

    private void BuildBar(Transform canvas)
    {
        GameObject bar = new GameObject("Hotbar", typeof(RectTransform));
        bar.transform.SetParent(canvas, false);
        RectTransform br = bar.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0f);
        float size = 86f, gap = 10f;
        br.sizeDelta = new Vector2(Slots * size + (Slots - 1) * gap, size);
        br.anchoredPosition = new Vector2(0f, 24f);
        for (int i = 0; i < Slots; i++)
        {
            GameObject box = new GameObject("Slot " + (i + 1), typeof(RectTransform), typeof(Image));
            box.transform.SetParent(bar.transform, false);
            RectTransform r = box.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(size, size);
            r.anchoredPosition = new Vector2(size * 0.5f + i * (size + gap), 0f);
            Image frame = box.GetComponent<Image>();
            frame.color = new Color(1f, 1f, 1f, 0.18f);
            frame.raycastTarget = false;
            GameObject inner = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(box.transform, false);
            RectTransform ir = inner.GetComponent<RectTransform>();
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one; ir.offsetMin = new Vector2(3f, 3f); ir.offsetMax = new Vector2(-3f, -3f);
            Image fill = inner.GetComponent<Image>();
            fill.color = new Color(0.05f, 0.06f, 0.08f, 0.72f);
            fill.raycastTarget = false;
            TMP_Text num = PixelUIKit.CreateText(clicker.UIFont, box.transform, "Number", (i + 1).ToString(), 22f, TextAlignmentOptions.TopLeft, FontStyles.Bold, new Color(1f, 1f, 1f, 0.75f));
            num.raycastTarget = false;
            RectTransform nr = num.rectTransform;
            nr.anchorMin = Vector2.zero; nr.anchorMax = Vector2.one; nr.offsetMin = new Vector2(8f, 4f); nr.offsetMax = new Vector2(-4f, -4f);
            frames[i] = frame;
            boxes[i] = r;
            if (i == 0) AxeIcon(box.transform);
        }
    }

    /// <summary>A little axe drawn with rotated rectangles in slot 1.</summary>
    private static void AxeIcon(Transform slot)
    {
        Image Piece(string name, Vector2 pos, Vector2 size, float angle, Color color)
        {
            GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(slot, false);
            RectTransform r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size; r.anchoredPosition = pos; r.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image im = g.GetComponent<Image>();
            im.color = color; im.raycastTarget = false;
            return im;
        }
        Piece("Haft", new Vector2(0f, -2f), new Vector2(7f, 54f), 35f, new Color(0.62f, 0.4f, 0.2f));
        Piece("Head", new Vector2(-12f, 14f), new Vector2(26f, 18f), 35f, new Color(0.72f, 0.75f, 0.8f));
        Piece("Edge", new Vector2(-24f, 10f), new Vector2(6f, 24f), 35f, new Color(0.95f, 0.97f, 1f));
    }
}
