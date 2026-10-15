// ---- System notes ----
// PixelRecycler.cs
//   RECYCLER (PixelConsumables DeviceKind.Recycler 19, Utilities card - listed FIRST there - and Cubie's gift on visit 1 via the {recycler} page token): a
//   permanent open-topped bin (PixelChipperDevice stub for removal / saving its spot; built here by Place / CreateGhost / CreateDisplay with PixelFactoryKit).
//   Drop old pixels (husks) into it - Pixel Grabbing, or anything that lands inside: a husk that comes to rest inside the bin's walls is CAPTURED
//   (PixelClicker.ReleaseOldPixel without credit, so it leaves the floor's old-pixel list: no despawn count, no cap, no vacuum, no termites; its
//   OldPixelDespawn is Held so it never shrinks away). The husks stay in the bin as real physical pixels until it is closed. Up to 'capacity' (12)
//   can be inside; a counter above the bin shows 'n / 12'. Husks inside never despawn (their OldPixelDespawn.Held is re-set every tick, so poking one with
//   Pixel Grabbing cannot start its timer; one taken out of the bin is let go again; with the bin full, extra husks lying in it get KeepAlive). Click the
//   red BUTTON on its front: the lid SLIDES forward along two rails from behind the bin to cover the top, every husk inside is recycled
//   into payoutShare (10%) of its stored value as currency of its own type (pixel rules: the Recycler is the one machine that turns husks back into
//   money, at a loss), the husks are destroyed, and the lid slides back open. ButtonHot (the pointer is over a button) is part of
//   PixelClicker.ExternalClickBlock and PixelGrab's guard, so pressing it neither clicks the cube nor grabs a pixel.
//   Not saved: husks inside are lost on reload, and removing the Recycler destroys them. Not a belt acceptor yet.
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class PixelRecycler : MonoBehaviour
{
    [Tooltip("How many husks fit in the bin before it has to be closed.")]
    [SerializeField] private int capacity = 12;
    [Range(0f, 1f)]
    [Tooltip("Share of each husk's stored value paid back as currency of its type when the bin is processed (0.1 = 10%).")]
    [SerializeField] private float payoutShare = 0.1f;
    [Tooltip("Seconds the lid takes to swing shut / open.")]
    [SerializeField] private float lidSeconds = 0.35f;
    [Tooltip("Seconds the lid stays shut while the husks are processed.")]
    [SerializeField] private float processSeconds = 0.8f;
    [Tooltip("Colour of the 'n / 12' counter above the bin.")]
    [SerializeField] private Color counterColor = new Color(0.75f, 1f, 0.75f, 1f);

    private static readonly List<PixelRecycler> all = new List<PixelRecycler>();
    private static int hotFrame = -10;

    /// <summary>True while the pointer is over a Recycler's button (clicks there belong to the Recycler, not the cube or Pixel Grabbing).</summary>
    public static bool ButtonHot => Time.frameCount - hotFrame <= 1;

    private PixelClicker clicker;
    private Transform lidPivot, button;
    private Collider buttonCollider;
    private TextMeshPro counter;
    private readonly List<Rigidbody> inside = new List<Rigidbody>();
    private float tick;
    private bool processing;
    private float innerW, innerD, wallH;

    // ---- Building ----

    private const float WidthHusks = 5f, DepthHusks = 4f, HeightHusks = 3f;
    private static readonly Color BodyColor = new Color(0.25f, 0.55f, 0.32f, 1f), LidColor = new Color(0.2f, 0.45f, 0.27f, 1f), ButtonColor = new Color(0.9f, 0.2f, 0.18f, 1f);

    /// <summary>Builds a placed Recycler at 'point' (with its behaviour).</summary>
    public static GameObject Place(PixelClicker clicker, Vector3 point, float yaw)
    {
        GameObject root = Build(clicker, false, true);
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        PixelRecycler r = root.AddComponent<PixelRecycler>();
        r.Init(clicker);
        return root;
    }

    /// <summary>A see-through copy that follows the mouse while placing.</summary>
    public static GameObject CreateGhost(PixelClicker clicker) => Build(clicker, true, false);

    /// <summary>A model for the shop / inventory pictures (no colliders, no behaviour).</summary>
    public static GameObject CreateDisplay(PixelClicker clicker) => Build(clicker, false, false);

    private static float Unit(PixelClicker clicker) => clicker != null && clicker.OldPixelWorldSize > 0f ? clicker.OldPixelWorldSize : 0.5f;

    private static GameObject Build(PixelClicker clicker, bool ghost, bool colliders)
    {
        float u = Unit(clicker);
        float w = WidthHusks * u, d = DepthHusks * u, h = HeightHusks * u, t = 0.25f * u;
        PixelFactoryKit kit = new PixelFactoryKit(clicker, ghost);
        GameObject root = new GameObject(ghost ? "Recycler (Preview)" : "Recycler");
        Transform tr = root.transform;
        kit.Box("Base", tr, new Vector3(0f, t * 0.5f, 0f), new Vector3(w + 2f * t, t, d + 2f * t), BodyColor, default, colliders);
        kit.Box("Wall Front", tr, new Vector3(0f, h * 0.5f, -(d + t) * 0.5f), new Vector3(w + 2f * t, h, t), BodyColor, default, colliders);
        kit.Box("Wall Back", tr, new Vector3(0f, h * 0.5f, (d + t) * 0.5f), new Vector3(w + 2f * t, h, t), BodyColor, default, colliders);
        kit.Box("Wall Left", tr, new Vector3(-(w + t) * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), BodyColor, default, colliders);
        kit.Box("Wall Right", tr, new Vector3((w + t) * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), BodyColor, default, colliders);
        // The recycling arrows: three pale bars on the front wall.
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f;
            Vector3 p = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * u * 0.55f, h * 0.5f + Mathf.Sin(a * Mathf.Deg2Rad) * u * 0.55f, -(d + t) * 0.5f - t * 0.55f);
            kit.Box("Arrow " + i, tr, p, new Vector3(u * 0.9f, u * 0.18f, t * 0.2f), new Color(0.85f, 1f, 0.85f, 1f), new Vector3(0f, 0f, a + 90f));
        }
        // The lid slides along the top: it starts open, pushed back behind the bin, and slides forward over the opening to close.
        GameObject pivot = new GameObject("Lid Pivot");
        pivot.transform.SetParent(tr, false);
        pivot.transform.localPosition = LidOpen(u);
        kit.Box("Lid", pivot.transform, new Vector3(0f, t * 0.5f, 0f), new Vector3(w + 2f * t, t, d + 2f * t), LidColor);
        // Two rails on the side walls that the lid rides on, running back past the bin where the open lid rests.
        for (int side = -1; side <= 1; side += 2)
            kit.Box("Rail " + (side < 0 ? "L" : "R"), tr, new Vector3(side * (w + t) * 0.5f, h + t * 0.15f, (d + 2f * t) * 0.5f), new Vector3(t * 0.6f, t * 0.3f, 2f * d + 3f * t), new Color(0.6f, 0.62f, 0.6f, 1f), default, false, 0.7f, 0.5f);
        // Posts holding up the far end of the rails.
        for (int side = -1; side <= 1; side += 2)
            kit.Box("Post " + (side < 0 ? "L" : "R"), tr, new Vector3(side * (w + t) * 0.5f, h * 0.5f, d + 1.5f * t), new Vector3(t * 0.6f, h, t * 0.6f), new Color(0.6f, 0.62f, 0.6f, 1f), default, false, 0.7f, 0.5f);
        // The button on top of the front wall.
        kit.Cyl("Button Base", tr, new Vector3(w * 0.32f, h + t * 0.4f, -(d + t) * 0.5f), u * 0.34f, t * 0.8f, new Color(0.15f, 0.15f, 0.15f, 1f));
        kit.Cyl("Button", tr, new Vector3(w * 0.32f, h + t * 1.1f, -(d + t) * 0.5f), u * 0.24f, t * 0.9f, ButtonColor, default, 0.1f, 0.6f, colliders);
        return root;
    }

    /// <summary>Where the lid rests: closed = over the opening, open = slid back behind the bin.</summary>
    private static Vector3 LidClosed(float u) => new Vector3(0f, HeightHusks * u, 0f);
    private static Vector3 LidOpen(float u) => new Vector3(0f, HeightHusks * u, DepthHusks * u + 0.5f * u);

    private void Init(PixelClicker owner)
    {
        clicker = owner;
        float u = Unit(clicker);
        innerW = WidthHusks * u; innerD = DepthHusks * u; wallH = HeightHusks * u;
        lidPivot = transform.Find("Lid Pivot");
        button = transform.Find("Button");
        buttonCollider = button != null ? button.GetComponent<Collider>() : null;

        GameObject cg = new GameObject("Counter");
        cg.transform.SetParent(transform, false);
        cg.transform.localPosition = new Vector3(0f, wallH + u * 1.6f, 0f);
        counter = cg.AddComponent<TextMeshPro>();
        counter.fontSize = 4f;
        counter.fontStyle = FontStyles.Bold;
        counter.alignment = TextAlignmentOptions.Center;
        counter.color = counterColor;
        if (clicker != null && clicker.UIFont != null) counter.font = clicker.UIFont;
        RefreshCounter();
    }

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { all.Remove(this); }

    private void OnDestroy()
    {
        foreach (Rigidbody b in inside) if (b != null) Destroy(b.gameObject);   // (not saved: what was inside is lost with the bin)
        inside.Clear();
    }

    // ---- Every frame ----

    private void Update()
    {
        if (clicker == null) return;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (counter != null && cam != null) counter.transform.rotation = Quaternion.LookRotation(counter.transform.position - cam.transform.position, cam.transform.up);

        bool hot = !processing && buttonCollider != null && cam != null && !PixelInput.PointerOverUI() && PointerOnButton(cam);
        if (hot) hotFrame = Time.frameCount;
        if (hot && PixelInput.LeftPressed() && Time.timeScale > 0f && !PixelPauseMenu.IsPaused) StartCoroutine(Process());

        tick -= Time.deltaTime;
        if (tick > 0f || processing) return;
        tick = 0.1f;
        Capture();
    }

    private bool PointerOnButton(Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        return buttonCollider.Raycast(ray, out _, 500f);
    }

    /// <summary>Husks resting inside the walls are taken off the floor's old-pixel list and kept in the bin.</summary>
    private void Capture()
    {
        bool changed = false;
        inside.RemoveAll(b => b == null);
        // Husks in the bin never despawn: their timer stays held every tick (Pixel Grabbing lets go of 'Held' when you poke one); one taken out of the bin is let go again.
        for (int i = inside.Count - 1; i >= 0; i--)
        {
            OldPixelDespawn hold = inside[i].GetComponent<OldPixelDespawn>();
            OldPixelInfo heldInfo = inside[i].GetComponent<OldPixelInfo>();
            bool inHand = heldInfo != null && heldInfo.heldByPlayer;
            if (Inside(inside[i].position) || inHand) { if (hold != null) hold.Held = true; continue; }
            if (hold != null) hold.Held = false;
            inside.RemoveAt(i);
            changed = true;
        }
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            Rigidbody b = pixels[i];
            if (b == null || !Inside(b.position)) continue;
            if (inside.Count >= Mathf.Max(1, capacity))   // the bin is full: husks lying in it anyway still never fade while they are inside
            {
                OldPixelDespawn keep = b.GetComponent<OldPixelDespawn>();
                if (keep != null && !keep.IsDespawning) keep.KeepAlive();
                continue;
            }
            OldPixelInfo info = b.GetComponent<OldPixelInfo>();
            if (info == null) continue;
            OldPixelDespawn despawn = b.GetComponent<OldPixelDespawn>();
            if (despawn != null && despawn.IsDespawning) continue;   // already vanishing
            if (info.heldByPlayer) continue;                          // still in the player's hand
            if (!clicker.ReleaseOldPixel(b, false)) continue;
            if (despawn != null) despawn.Held = true;   // it never shrinks away while it is in the bin
            inside.Add(b);
            changed = true;
        }
        if (changed) { RefreshCounter(); PixelAudio.Play("click"); }
    }

    private bool Inside(Vector3 world)
    {
        Vector3 p = transform.InverseTransformPoint(world);
        return Mathf.Abs(p.x) <= innerW * 0.5f && Mathf.Abs(p.z) <= innerD * 0.5f && p.y >= -0.05f && p.y <= wallH;
    }

    private void RefreshCounter()
    {
        if (counter == null) return;
        int cap = Mathf.Max(1, capacity);
        counter.text = inside.Count + " / " + cap + (inside.Count >= cap ? "\n<size=60%>full - press the button</size>" : "");
    }

    // ---- Processing ----

    private IEnumerator Process()
    {
        processing = true;
        PixelAudio.Play("ui_click");
        yield return SlideLid(LidOpen(Unit(clicker)), LidClosed(Unit(clicker)));

        Dictionary<int, double> paid = new Dictionary<int, double>();
        int count = 0;
        foreach (Rigidbody b in inside)
        {
            if (b == null) continue;
            OldPixelInfo info = b.GetComponent<OldPixelInfo>();
            if (info != null && clicker.IsValidTierIndex(info.tierIndex))
            {
                double pay = System.Math.Max(0d, info.amount) * payoutShare;
                paid.TryGetValue(info.tierIndex, out double sum);
                paid[info.tierIndex] = sum + pay;
            }
            Destroy(b.gameObject);
            count++;
        }
        inside.Clear();
        RefreshCounter();

        yield return new WaitForSeconds(Mathf.Max(0f, processSeconds));
        if (count > 0)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<int, double> kv in paid)
            {
                if (kv.Value <= 0d) continue;
                clicker.AddCurrency(kv.Key, kv.Value);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append('+').Append(PixelClicker.FormatNumberShort(kv.Value)).Append(' ').Append(clicker.Tiers[kv.Key].displayName);
            }
            PixelStats.Count("recycler.husks", count);
            PixelAudio.Play("purchase");
            PixelHints.Announce("Recycled " + count + " husk" + (count == 1 ? "" : "s") + (sb.Length > 0 ? ": " + sb : "."));
        }
        yield return SlideLid(LidClosed(Unit(clicker)), LidOpen(Unit(clicker)));
        processing = false;
    }

    private IEnumerator SlideLid(Vector3 from, Vector3 to)
    {
        if (lidPivot == null) yield break;
        float t = 0f, seconds = Mathf.Max(0.05f, lidSeconds);
        while (t < 1f)
        {
            t += Time.deltaTime / seconds;
            lidPivot.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }
    }
}
