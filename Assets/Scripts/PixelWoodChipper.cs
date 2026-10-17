// ---- System notes ----
// PixelWoodChipper.cs
//   The WOOD CHIPPER: the first thing built for the Dev Hub's Tester tab (PixelFeatureTester). A detailed machine built from primitives in code (PixelWoodChipper.Build(ghost)): a trailer chassis
//   with two tyres, hubs, fenders, a hitch with a jack stand and coupler; an orange cutter housing with a hatch, bolts, a flywheel guard with a spinning pulley, warning stripes and a danger plate;
//   a big tapered FEED HOPPER (double-sided walls, dark liner, yellow / black hazard rim) over two ribbed feed rollers; a discharge chute with a swivel hood; an engine with fins, valve cover, air
//   filter, fuel tank, muffler and exhaust stack; a control panel with levers, a gauge and an e-stop; an amber beacon; reflectors. Build(true) makes the see-through placement ghost (no colliders).
//   DISCHARGE ARROW + BELT LATCH: while placing, the ghost shows a flat arrow from the chute outwards (the chips shoot out along it). If the opening (start) of a conveyor belt lies in the
//   arrow's area, the chipper latches on (PixelWoodChipper.TrySnapToBelt, used by PixelFeatureTester's placement snap): it turns so the arrow runs the way the belt runs and the chute sits just
//   over the belt's opening; the arrow turns green. A placed chipper drops its Wood Chips blocks straight onto a belt opening in that area (BeltAtOutlet).
//   BEHAVIOUR (the placed machine): any old pixel inside the hopper (or just above its mouth, suction) is pulled to the rollers and SHREDDED: PixelClicker.ClaimOldPixel takes it out of the list
//   (no payout), it is drawn through the rollers while it squashes, and a burst of pixel-coloured chips plus wood chips comes out of the chute. While it works the machine shakes, the rollers and
//   flywheel speed up, the beacon blinks and the exhaust puffs more. Pixels you are holding are left alone.
//   WOOD CHIPS: every woodPerChips (5) wood pixels consumed (woodConsumed / chipProgress) the chute spits out one 'Wood Chips' old pixel (PixelType.WoodChips, PixelClicker.EnsureWoodChipsTier,
//   never spawns on the cube; it despawns like any old pixel until later tools turn it into currency). A BLUE bar under the health bar shows the progress to the next block.
//   GAME RULES: a WOOD pixel (PixelClicker.IsWoodBlock) gives Woodcutting XP (woodXp, PixelSkills.AddXp on the Woodcutting pack, with the usual popup). Any other pixel DAMAGES it
//   (damagePerOtherPixel off 'maxHealth'): the paint darkens, sparks fly, the exhaust turns black and thick; a health bar floats over it. At 0 it is BROKEN for good (stops, no beacon, smoke,
//   'BROKEN' label): clear it in the Tester tab and place a new one. Tunables are [SerializeField] fields (editable live with the Dev Hub's Inspector).
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PixelWoodChipper : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Where things are, in the machine's own units (before the scale)
    // ------------------------------------------------------------------

    private static readonly Vector3 ThroatLocal = new Vector3(-1.2f, 1.8f, 0f);
    private static readonly Vector3 OutletLocal = new Vector3(0.25f, 3.05f, 0.55f);
    private static readonly Vector3 OutletDirLocal = new Vector3(0f, 0.45f, 1f);
    private const float FunnelBaseY = 1.7f, FunnelMouthY = 2.75f;

    private static readonly Color Orange = new Color(0.95f, 0.48f, 0.08f), DarkOrange = new Color(0.72f, 0.33f, 0.05f), Steel = new Color(0.66f, 0.68f, 0.72f),
                                  Dark = new Color(0.17f, 0.18f, 0.2f), Rubber = new Color(0.07f, 0.07f, 0.08f), Yellow = new Color(1f, 0.84f, 0.08f),
                                  Red = new Color(0.8f, 0.08f, 0.07f), Wood = new Color(0.78f, 0.58f, 0.32f);

    // ------------------------------------------------------------------
    // Tunables
    // ------------------------------------------------------------------


    [Header("Feeding")]
    [Tooltip("How hard a pixel inside the hopper is pulled towards the rollers (world units per second squared).")]
    [SerializeField] private float pullAcceleration = 55f;

    [Tooltip("How far above the hopper's mouth pixels are sucked in (machine units, before the scale).")]
    [SerializeField] private float suctionHeight = 1.4f;

    [Tooltip("Pixels closer than this to the rollers (machine units) are shredded.")]
    [SerializeField] private float shredDistance = 0.4f;

    [Header("Shredding")]
    [Tooltip("Seconds a pixel takes to be drawn into the rollers.")]
    [SerializeField] private float drawSeconds = 0.28f;

    [Tooltip("Seconds the pixel is chewed (squashed) in the rollers before the chips come out.")]
    [SerializeField] private float chewSeconds = 0.3f;

    [Tooltip("How many chips fly out of the chute per shredded pixel.")]
    [SerializeField] private int chipsPerPixel = 16;

    [Tooltip("Seconds the machine stays 'busy' (shaking, beacon, fast rollers) after the last pixel.")]
    [SerializeField] private float busySeconds = 1.2f;

    [Header("Game rules")]
    [Tooltip("Woodcutting XP for every wood pixel shredded.")]
    [SerializeField] private float woodXp = 3f;

    [Tooltip("How much damage every non-wood pixel does to the machine.")]
    [SerializeField] private float damagePerOtherPixel = 14f;

    [Min(1)]
    [Tooltip("Every this many wood pixels shredded, the machine spits out one Wood Chips block (an old pixel that despawns like any other).")]
    [SerializeField] private int woodPerChips = 5;

    [Tooltip("The machine's health; at 0 it breaks for good.")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Look")]
    [Tooltip("How strongly the machine shakes while busy (machine units).")]
    [SerializeField] private float shakeAmount = 0.025f;

    // ------------------------------------------------------------------
    // Placement helpers (used by PixelFeatureTester)
    // ------------------------------------------------------------------

    private static float ScaleFor(PixelClicker clicker) => clicker != null ? Mathf.Max(0.05f, clicker.PixelBaseSize) * 0.9f : 1f;

    private const float ArrowLength = 2.6f;   // machine units: how far the discharge arrow reaches from the outlet (the area where a belt opening latches on)

    /// <summary>
    /// While placing: if the opening (start) of a conveyor belt lies in the discharge arrow's area, the chipper latches on: it turns so the arrow points along the belt and moves so the outlet
    /// sits just over the belt's opening. Returns true when latched (the snapped position / yaw are returned). Also tints the ghost's arrow green when latched.
    /// </summary>
    public static bool TrySnapToBelt(PixelClicker clicker, Vector3 point, float yaw, GameObject ghost, out Vector3 snappedPoint, out float snappedYaw)
    {
        snappedPoint = point; snappedYaw = yaw;
        bool latched = false;
        if (clicker != null)
        {
            float scale = ScaleFor(clicker);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 outlet = point + rot * Vector3.Scale(OutletLocal, new Vector3(scale, 0f, scale));
            Vector3 dir = rot * Vector3.forward, right = rot * Vector3.right;
            PixelConveyor best = null;
            float bestAlong = float.MaxValue;
            foreach (PixelConveyor belt in PixelConveyor.All)
            {
                if (belt == null || belt.IsGhost) continue;
                Vector3 v = belt.StartPoint - outlet; v.y = 0f;
                float along = Vector3.Dot(v, dir), lateral = Vector3.Dot(v, right);
                if (along < -0.3f * scale || along > ArrowLength * scale || Mathf.Abs(lateral) > 1.1f * scale) continue;
                if (Mathf.Abs(along) < bestAlong) { bestAlong = Mathf.Abs(along); best = belt; }
            }
            if (best != null)
            {
                Vector3 beltDir = best.Direction;
                snappedYaw = Mathf.Atan2(beltDir.x, beltDir.z) * Mathf.Rad2Deg;   // the arrow (local +Z) runs the way the belt runs
                Quaternion rot2 = Quaternion.Euler(0f, snappedYaw, 0f);
                Vector3 outletTarget = best.StartPoint + beltDir * (0.6f * clicker.OldPixelWorldSize);
                snappedPoint = outletTarget - rot2 * Vector3.Scale(OutletLocal, new Vector3(scale, 0f, scale));
                snappedPoint.y = point.y;
                latched = true;
            }
        }
        TintArrow(ghost, latched);
        return latched;
    }

    private static void TintArrow(GameObject ghost, bool latched)
    {
        if (ghost == null) return;
        Transform arrow = ghost.transform.Find("Arrow");
        if (arrow == null) return;
        Color c = latched ? new Color(0.35f, 1f, 0.45f, 0.75f) : new Color(0.4f, 0.85f, 1f, 0.7f);
        foreach (Renderer r in arrow.GetComponentsInChildren<Renderer>())
        {
            Material m = r.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
    }

    /// <summary>The belt whose opening is in this chipper's discharge area right now (null = nothing to latch onto).</summary>
    private PixelConveyor BeltAtOutlet()
    {
        Vector3 outlet = transform.TransformPoint(OutletLocal);
        Vector3 dir = transform.TransformDirection(Vector3.forward); dir.y = 0f; dir.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        float scale = transform.lossyScale.x;
        PixelConveyor best = null;
        float bestD = float.MaxValue;
        foreach (PixelConveyor belt in PixelConveyor.All)
        {
            if (belt == null || belt.IsGhost) continue;
            Vector3 v = belt.StartPoint - outlet; v.y = 0f;
            float along = Vector3.Dot(v, dir), lateral = Vector3.Dot(v, right);
            if (along < -0.8f * scale || along > ArrowLength * scale || Mathf.Abs(lateral) > 1.2f * scale) continue;
            if (Mathf.Abs(along) < bestD) { bestD = Mathf.Abs(along); best = belt; }
        }
        return best;
    }

    // ---- saving (PixelFeatureTester) ----

    [System.Serializable]
    private class Save { public Vector3 position; public float yaw; public float health; public int chipProgress, woodConsumed; public bool broken; }

    public static string ExportState(GameObject root)
    {
        PixelWoodChipper c = root != null ? root.GetComponent<PixelWoodChipper>() : null;
        if (c == null) return null;
        return JsonUtility.ToJson(new Save { position = root.transform.position, yaw = root.transform.eulerAngles.y, health = c.health, chipProgress = c.chipProgress, woodConsumed = c.woodConsumed, broken = c.broken });
    }

    public static GameObject ImportState(PixelClicker clicker, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        if (s == null) return null;
        GameObject root = Place(clicker, s.position, s.yaw);
        PixelWoodChipper c = root.GetComponent<PixelWoodChipper>();
        if (c != null)
        {
            c.health = s.health; c.chipProgress = s.chipProgress; c.woodConsumed = s.woodConsumed;
            if (s.broken) { c.broken = true; if (c.brokenLabel != null) c.brokenLabel.gameObject.SetActive(true); }
            c.RefreshCondition();
        }
        return root;
    }

    /// <summary>The see-through ghost that follows the mouse while placing.</summary>
    public static GameObject CreateGhost(PixelClicker clicker)
    {
        GameObject root = Build(clicker, true);
        root.name = "Wood Chipper (Ghost)";
        return root;
    }

    /// <summary>A solid, inert model for pictures (inventory / vendor items): no behaviour, no colliders, no arrow.</summary>
    public static GameObject CreateDisplay(PixelClicker clicker)
    {
        GameObject root = Build(clicker, true, 1f, false);
        root.name = "Wood Chipper (Display)";
        return root;
    }

    /// <summary>A working Wood Chipper standing at 'point' turned by 'yaw' degrees.</summary>
    public static GameObject Place(PixelClicker clicker, Vector3 point, float yaw)
    {
        GameObject root = Build(clicker, false);
        root.name = "Wood Chipper";
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        return root;
    }

    // ------------------------------------------------------------------
    // The behaviour
    // ------------------------------------------------------------------

    private PixelClicker clicker;
    private Transform shake, flywheel, rollerA, rollerB, beaconLamp;
    private Material beaconMat;
    private ParticleSystem chips, smoke;
    private Transform chipFloor;
    private Vector3 shakeHome;
    private float busyUntil, rollerAngle, flyAngle;
    private float scaleFactor = 1f;
    private float health;
    private bool broken;
    private int woodConsumed, chipProgress;   // wood pixels shredded in total / since the last Wood Chips block
    private Transform chipBarFill;
    private readonly List<Renderer> paintRenderers = new List<Renderer>();
    private readonly List<Color> paintColors = new List<Color>();
    private Transform barRoot, barFill;
    private Material barFillMat;
    private TextMesh brokenLabel;
    private ParticleSystem sparks;

    private bool Busy => Time.time < busyUntil;

    private void Update()
    {
        bool busy = Busy && !broken;
        float dt = Time.deltaTime;
        float rollerSpeed = busy ? 520f : 40f, flySpeed = busy ? 900f : 160f;
        rollerAngle += rollerSpeed * dt;
        flyAngle += flySpeed * dt;
        if (broken) { rollerSpeed = 0f; flySpeed = 0f; }
        if (rollerA != null) rollerA.localRotation = Quaternion.Euler(0f, 0f, rollerAngle);
        if (rollerB != null) rollerB.localRotation = Quaternion.Euler(0f, 0f, -rollerAngle);
        if (flywheel != null) flywheel.localRotation = Quaternion.Euler(0f, 0f, flyAngle);

        if (shake != null)
        {
            Vector3 jitter = busy && !broken ? new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * shakeAmount : Vector3.zero;
            jitter += new Vector3(0f, Mathf.Sin(Time.time * 38f) * 0.004f, 0f);   // the idling engine
            shake.localPosition = shakeHome + jitter;
        }
        if (beaconMat != null)
        {
            bool on = broken ? false : busy ? Mathf.Repeat(Time.time * 4f, 1f) < 0.5f : Mathf.Repeat(Time.time * 0.8f, 1f) < 0.15f;
            Color c = on ? new Color(1f, 0.7f, 0.1f) : new Color(0.45f, 0.25f, 0.03f);
            SetColor(beaconMat, c);
        }
        if (smoke != null)
        {
            float hurt = 1f - Mathf.Clamp01(health / Mathf.Max(1f, maxHealth));   // 0 = fine, 1 = wrecked
            ParticleSystem.EmissionModule em = smoke.emission;
            em.rateOverTime = (busy ? 28f : 7f) + hurt * 26f;
            ParticleSystem.MainModule sm = smoke.main;
            Color grey = Color.Lerp(new Color(0.45f, 0.45f, 0.48f, 0.6f), new Color(0.05f, 0.05f, 0.06f, 0.85f), hurt);   // thicker and blacker the worse it is
            sm.startColor = grey;
        }
        if (broken && sparks != null && Random.value < dt * 2.5f) EmitSparks(2 + Random.Range(0, 3));
        if (barRoot != null)
        {
            Camera cam = PixelClicker.ViewCameraOf(clicker);
            if (cam != null) barRoot.rotation = cam.transform.rotation;
        }
        if (brokenLabel != null)
        {
            Camera cam2 = PixelClicker.ViewCameraOf(clicker);
            if (cam2 != null) brokenLabel.transform.rotation = cam2.transform.rotation;
        }
    }

    private void FixedUpdate()
    {
        if (clicker == null || broken) return;
        IReadOnlyList<Rigidbody> list = clicker.OldPixels;
        if (list == null || list.Count == 0) return;
        Vector3 throat = transform.TransformPoint(ThroatLocal);
        float scale = transform.lossyScale.x;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (i >= list.Count) continue;
            Rigidbody rb = list[i];
            if (rb == null) continue;
            OldPixelInfo info = PixelHuskQuery.Get(rb).info;
            if (info == null || info.heldByPlayer) continue;   // what you are holding is yours

            Vector3 p = transform.InverseTransformPoint(rb.position);
            if (!InsideFeed(p)) continue;

            // Pull it down the hopper and a little to the rollers.
            Vector3 toThroat = throat - rb.position;
            float dist = toThroat.magnitude;
            if (dist < shredDistance * scale || (p.y < FunnelBaseY + 0.25f && Mathf.Abs(p.x - ThroatLocal.x) < 0.5f))
            {
                if (clicker.ClaimOldPixel(rb)) StartCoroutine(Shred(rb, info));
                continue;
            }
            rb.AddForce(toThroat / Mathf.Max(0.0001f, dist) * pullAcceleration, ForceMode.Acceleration);
            Damp(rb, 2.5f * Time.fixedDeltaTime);
        }
    }

    /// <summary>Is this machine-space point inside the hopper (or in the suction zone just above its mouth)?</summary>
    private bool InsideFeed(Vector3 p)
    {
        float top = FunnelMouthY + suctionHeight;
        if (p.y < FunnelBaseY - 0.05f || p.y > top) return false;
        float t = Mathf.Clamp01((p.y - FunnelBaseY) / (FunnelMouthY - FunnelBaseY));
        float cx = Mathf.Lerp(-1.2f, -1.5f, t), hx = Mathf.Lerp(0.42f, 1f, t), hz = Mathf.Lerp(0.55f, 1f, t);
        if (p.y > FunnelMouthY) { float k = (p.y - FunnelMouthY) / suctionHeight; hx += 0.5f * k; hz += 0.5f * k; }   // the suction cone widens a little
        return Mathf.Abs(p.x - cx) < hx && Mathf.Abs(p.z) < hz;
    }

    private static void Damp(Rigidbody rb, float amount)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity *= Mathf.Clamp01(1f - amount);
#else
        rb.velocity *= Mathf.Clamp01(1f - amount);
#endif
    }

    /// <summary>One pixel goes through the machine: drawn to the rollers, chewed, and its chips come out of the chute.</summary>
    private IEnumerator Shred(Rigidbody rb, OldPixelInfo info)
    {
        GameObject go = rb.gameObject;
        Transform t = go.transform;
        Color tint = Color.white;
        bool isWood = false;
        if (clicker != null && clicker.IsValidTierIndex(info.tierIndex))
        {
            tint = clicker.Tiers[info.tierIndex].color;
            isWood = PixelClicker.IsWoodBlock(clicker.Tiers[info.tierIndex].type);
        }
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        rb.isKinematic = true;
        OldPixelDespawn despawn = go.GetComponent<OldPixelDespawn>();
        if (despawn != null) despawn.enabled = false;
        busyUntil = Time.time + drawSeconds + chewSeconds + busySeconds;

        Vector3 startPos = t.position, startScale = t.localScale;
        float time = 0f;
        while (time < drawSeconds)
        {
            if (t == null) yield break;
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / Mathf.Max(0.01f, drawSeconds));
            float e = k * k * (3f - 2f * k);
            Vector3 throat = transform.TransformPoint(ThroatLocal);
            t.position = Vector3.Lerp(startPos, throat, e);
            t.Rotate(0f, 360f * Time.deltaTime, 200f * Time.deltaTime, Space.World);
            yield return null;
        }

        PixelAudio.Play("hit", 0.55f, 0.6f);
        if (isWood) GiveWoodXp(); else TakeDamage();
        time = 0f;
        while (time < chewSeconds)
        {
            if (t == null) yield break;
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / Mathf.Max(0.01f, chewSeconds));
            t.localScale = new Vector3(startScale.x * (1f + 0.5f * k), startScale.y * Mathf.Lerp(1f, 0.08f, k), startScale.z * (1f + 0.5f * k));
            t.position = transform.TransformPoint(ThroatLocal) + Random.insideUnitSphere * 0.02f * transform.lossyScale.x;
            yield return null;
        }

        if (go != null) Destroy(go);
        EmitChips(tint);
        if (isWood) CountWood();
    }

    /// <summary>A wood pixel was consumed: every 'woodPerChips' of them the machine spits out a Wood Chips block.</summary>
    private void CountWood()
    {
        woodConsumed++;
        chipProgress++;
        if (chipProgress >= Mathf.Max(1, woodPerChips))
        {
            chipProgress = 0;
            SpitChipsBlock();
        }
        RefreshCondition();
    }

    private void SpitChipsBlock()
    {
        if (clicker == null) return;
        int tier = clicker.EnsureWoodChipsTier();
        float s = transform.lossyScale.x;
        Vector3 pos = transform.TransformPoint(OutletLocal);
        PixelConveyor belt = BeltAtOutlet();
        if (belt != null)
        {
            // Latched to a belt: the block drops straight onto the belt's opening and rides away.
            Vector3 over = belt.PointAt(0.6f * clicker.OldPixelWorldSize);
            clicker.SpawnStoredPixel(tier, 1, new Vector3(over.x, pos.y, over.z), belt.Direction * belt.Speed * 0.5f);
            PixelAudio.Play("drop", 1.1f, 0.6f);
            return;
        }
        Vector3 dir = (transform.TransformDirection(OutletDirLocal).normalized + Vector3.up * 0.25f + Random.insideUnitSphere * 0.15f).normalized;
        clicker.SpawnStoredPixel(tier, 1, pos, dir * Random.Range(2.5f, 4f) * s);
        PixelAudio.Play("drop", 0.5f, 1.2f);
    }

    /// <summary>A wood pixel went through: Woodcutting XP.</summary>
    private void GiveWoodXp()
    {
        PixelSkills skills = PixelSkills.Instance;
        if (skills == null || woodXp <= 0f) return;
        PixelSkills.SkillPack pack = skills.Find(PixelSkills.WoodcuttingId);
        if (pack != null) skills.AddXp(pack, woodXp);
    }

    /// <summary>Something that is not wood went through: the machine takes damage, and breaks for good at 0.</summary>
    private void TakeDamage()
    {
        if (broken) return;
        health = Mathf.Max(0f, health - damagePerOtherPixel);
        EmitSparks(10);
        busyUntil = Time.time + 2f;
        PixelAudio.Play("glass_shatter", 0.6f, 0.5f);
        RefreshCondition();
        if (health <= 0f) Break();
    }

    private void Break()
    {
        broken = true;
        busyUntil = 0f;
        EmitSparks(30);
        PixelAudio.Play("device_arm", 0.5f, 0.8f);
        if (brokenLabel != null) brokenLabel.gameObject.SetActive(true);
        RefreshCondition();
        PixelHints.Announce("The Wood Chipper broke - only wood goes in it. Clear it in the Tester tab and place a new one.");
    }

    /// <summary>Health bar + paint darkness follow the health.</summary>
    private void RefreshCondition()
    {
        float k = Mathf.Clamp01(health / Mathf.Max(1f, maxHealth));
        if (barFill != null)
        {
            barFill.localScale = new Vector3(1.5f * Mathf.Max(0.001f, k), 1f, 1f);
            barFill.localPosition = new Vector3(-0.75f * (1f - k), 0f, -0.012f);
        }
        if (chipBarFill != null)
        {
            float c = Mathf.Clamp01(chipProgress / (float)Mathf.Max(1, woodPerChips));
            chipBarFill.localScale = new Vector3(1.5f * Mathf.Max(0.001f, c), 1f, 1f);
            chipBarFill.localPosition = new Vector3(-0.75f * (1f - c), -0.17f, -0.012f);
        }
        if (barFillMat != null) SetColor(barFillMat, Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(0.3f, 0.8f, 0.25f), k));
        float paint = broken ? 0.3f : Mathf.Lerp(0.5f, 1f, k);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int i = 0; i < paintRenderers.Count; i++)
        {
            Renderer r = paintRenderers[i];
            if (r == null) continue;
            Color c = paintColors[i] * paint;
            c.a = paintColors[i].a;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }
    }

    /// <summary>A shower of sparks from the engine side.</summary>
    private void EmitSparks(int count)
    {
        if (sparks == null) return;
        float s = transform.lossyScale.x;
        Vector3 pos = transform.TransformPoint(new Vector3(0.9f, 1.6f, 0f));
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = pos + Random.insideUnitSphere * 0.2f * s;
            ep.velocity = (Random.insideUnitSphere + Vector3.up * 0.8f).normalized * Random.Range(1.5f, 4f) * s;
            ep.startColor = Color.Lerp(new Color(1f, 0.55f, 0.1f), new Color(1f, 0.95f, 0.5f), Random.value);
            ep.startSize = Random.Range(0.03f, 0.07f) * s;
            ep.startLifetime = Random.Range(0.3f, 0.7f);
            sparks.Emit(ep, 1);
        }
    }

    /// <summary>A burst out of the chute: pixel-coloured squares and wood chips.</summary>
    private void EmitChips(Color pixelColor)
    {
        if (chips == null) return;
        float s = transform.lossyScale.x;
        Vector3 pos = transform.TransformPoint(OutletLocal);
        Vector3 dir = transform.TransformDirection(OutletDirLocal).normalized;
        for (int i = 0; i < chipsPerPixel; i++)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = pos + Random.insideUnitSphere * 0.05f * s;
            Vector3 spread = Random.insideUnitSphere * 0.45f;
            ep.velocity = (dir + spread).normalized * Random.Range(2.5f, 5.5f) * s;
            bool fromPixel = i % 3 == 0;
            Color c = fromPixel ? pixelColor * Random.Range(0.8f, 1.15f) : Wood * Random.Range(0.75f, 1.15f);
            c.a = 1f;
            ep.startColor = c;
            ep.startSize = Random.Range(0.05f, 0.11f) * s;
            ep.startLifetime = Random.Range(0.9f, 1.5f);
            ep.rotation = Random.Range(0f, 360f);
            ep.angularVelocity = Random.Range(-360f, 360f);
            chips.Emit(ep, 1);
        }
    }

    // ------------------------------------------------------------------
    // Building the machine
    // ------------------------------------------------------------------

    /// <summary>The discharge arrow that shows while placing: a flat arrow on the ground from the chute outwards (the chips shoot out along it; a belt opening in its area is latched onto).</summary>
    private static void BuildArrow(Transform root, PixelClicker clicker)
    {
        PixelFactoryKit k = new PixelFactoryKit(clicker, true) { alpha = 0.7f };
        GameObject arrow = new GameObject("Arrow");
        arrow.transform.SetParent(root, false);
        Color c = new Color(0.4f, 0.85f, 1f, 1f);
        float z0 = OutletLocal.z + 0.1f, z1 = OutletLocal.z + ArrowLength - 0.5f;
        k.Box("Shaft", arrow.transform, new Vector3(OutletLocal.x, 0.04f, (z0 + z1) * 0.5f), new Vector3(0.3f, 0.04f, z1 - z0), c);
        for (int sgn = -1; sgn <= 1; sgn += 2)
            k.Box("Head", arrow.transform, new Vector3(OutletLocal.x + sgn * 0.2f, 0.04f, z1 + 0.18f), new Vector3(0.3f, 0.04f, 0.75f), c, new Vector3(0f, sgn * 40f, 0f));
        k.Box("Origin Dot", arrow.transform, new Vector3(OutletLocal.x, 0.05f, OutletLocal.z), new Vector3(0.5f, 0.05f, 0.5f), c);
    }

    /// <summary>Builds the whole machine (origin on the ground under its middle) and, for a real one, adds this behaviour.</summary>
    private static GameObject Build(PixelClicker clicker, bool ghost, float ghostAlpha = 0.45f, bool withArrow = true)
    {
        Kit k = new Kit { clicker = clicker, ghost = ghost, alpha = ghostAlpha };
        GameObject root = new GameObject("Wood Chipper");
        float scale = clicker != null ? Mathf.Max(0.05f, clicker.PixelBaseSize) * 0.9f : 1f;

        // Everything except the wheels sits on a 'Shake' pivot (the idling engine / working machine shakes).
        GameObject shake = new GameObject("Shake");
        shake.transform.SetParent(root.transform, false);
        Transform sh = shake.transform;

        BuildChassis(k, root.transform, sh);
        BuildHousing(k, sh);
        Transform rA, rB;
        BuildHopper(k, sh, out rA, out rB);
        BuildChute(k, sh);
        Transform fly;
        BuildFlywheel(k, sh, out fly);
        BuildEngine(k, sh, out Transform exhaustTop);
        Material beacon = BuildControls(k, sh, out Transform lamp);

        if (ghost && withArrow) BuildArrow(root.transform, clicker);
        root.transform.localScale = Vector3.one * scale;
        if (ghost) return root;

        PixelWoodChipper chipper = root.AddComponent<PixelWoodChipper>();
        chipper.clicker = clicker;
        chipper.shake = sh; chipper.shakeHome = sh.localPosition;
        chipper.rollerA = rA; chipper.rollerB = rB; chipper.flywheel = fly; chipper.beaconLamp = lamp; chipper.beaconMat = beacon;
        chipper.scaleFactor = scale;
        chipper.MakeParticles(exhaustTop);
        chipper.health = chipper.maxHealth;
        chipper.BuildCondition(k);
        return root;
    }

    /// <summary>Remembers every part's colour (so damage can darken them), and builds the floating health bar and the BROKEN label.</summary>
    private void BuildCondition(Kit k)
    {
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>())
        {
            if (r.sharedMaterial == beaconMat) continue;
            Material m = r.sharedMaterial;
            if (m == null) continue;
            Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            paintRenderers.Add(r);
            paintColors.Add(c);
        }
        // Health bar: a dark back plate and a fill that shrinks, facing the camera, above the chute.
        GameObject bar = new GameObject("Condition Bar");
        bar.transform.SetParent(transform, false);
        bar.transform.localPosition = new Vector3(0f, 4.1f, 0f);
        barRoot = bar.transform;
        GameObject back = k.Box("Back", barRoot, Vector3.zero, new Vector3(1.64f, 0.2f, 0.02f), new Color(0.05f, 0.05f, 0.06f), default, false, 0f, 0.1f);
        GameObject fill = k.Box("Fill", barRoot, Vector3.zero, new Vector3(1.5f, 0.1f, 0.02f), new Color(0.3f, 0.8f, 0.25f), default, false, 0f, 0.1f);
        // A parent for the fill so scaling it happens along its own width, anchored at the left.
        GameObject fillPivot = new GameObject("Fill Pivot");
        fillPivot.transform.SetParent(barRoot, false);
        fillPivot.transform.localPosition = new Vector3(0f, 0f, -0.012f);
        fillPivot.transform.localScale = new Vector3(1.5f, 1f, 1f);
        fill.transform.SetParent(fillPivot.transform, false);
        fill.transform.localPosition = Vector3.zero;
        fill.transform.localScale = new Vector3(1f, 0.1f, 0.02f);
        barFill = fillPivot.transform;   // scaled along X, anchored at the left
        barFillMat = k.Mat(new Color(0.3f, 0.8f, 0.25f), 0f, 0.1f, true);
        fill.GetComponent<Renderer>().sharedMaterial = barFillMat;
        // A blue bar under the health bar: it fills with every wood pixel and empties when a Wood Chips block comes out.
        k.Box("Chip Back", barRoot, new Vector3(0f, -0.17f, 0f), new Vector3(1.64f, 0.14f, 0.02f), new Color(0.05f, 0.05f, 0.06f), default, false, 0f, 0.1f);
        GameObject chipFill = k.Box("Chip Fill", barRoot, Vector3.zero, new Vector3(1f, 0.07f, 0.02f), new Color(0.2f, 0.55f, 1f), default, false, 0f, 0.1f);
        GameObject chipPivot = new GameObject("Chip Fill Pivot");
        chipPivot.transform.SetParent(barRoot, false);
        chipPivot.transform.localPosition = new Vector3(0f, -0.17f, -0.012f);
        chipPivot.transform.localScale = new Vector3(0.001f, 1f, 1f);
        chipFill.transform.SetParent(chipPivot.transform, false);
        chipFill.transform.localPosition = Vector3.zero;
        chipFill.transform.localScale = new Vector3(1f, 0.07f, 0.02f);
        chipBarFill = chipPivot.transform;
        chipFill.GetComponent<Renderer>().sharedMaterial = k.Mat(new Color(0.2f, 0.55f, 1f), 0f, 0.1f, true);
        // The label (a TextMesh needs no font setup) shows only when it is broken.
        GameObject lg = new GameObject("Broken Label", typeof(TextMesh));
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0f, 4.55f, 0f);
        brokenLabel = lg.GetComponent<TextMesh>();
        brokenLabel.text = "BROKEN";
        brokenLabel.anchor = TextAnchor.MiddleCenter;
        brokenLabel.alignment = TextAlignment.Center;
        brokenLabel.characterSize = 0.18f;
        brokenLabel.fontSize = 48;
        brokenLabel.color = new Color(1f, 0.25f, 0.2f);
        lg.SetActive(false);
        RefreshCondition();
    }

    private void MakeParticles(Transform exhaustTop)
    {
        float s = scaleFactor;
        // The chips: little squares that fall and bounce on the ground.
        GameObject cg = new GameObject("Chips", typeof(ParticleSystem));
        cg.transform.SetParent(transform, false);
        chips = cg.GetComponent<ParticleSystem>();
        chips.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule cm = chips.main;
        cm.playOnAwake = false; cm.loop = false;
        cm.simulationSpace = ParticleSystemSimulationSpace.World;
        cm.gravityModifier = 1.3f; cm.maxParticles = 600;
        cm.startSpeed = 0f;
        ParticleSystem.EmissionModule ce = chips.emission; ce.enabled = false;
        ParticleSystem.ShapeModule cs = chips.shape; cs.enabled = false;
        ParticleSystemRenderer cr = cg.GetComponent<ParticleSystemRenderer>();
        Shader sprite = PixelShaders.SpriteDefault();
        if (sprite != null) cr.sharedMaterial = new Material(sprite);
        GameObject floorGo = new GameObject("Chip Floor");
        floorGo.transform.SetParent(transform, false);
        floorGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        chipFloor = floorGo.transform;
        ParticleSystem.CollisionModule col = chips.collision;
        col.enabled = true;
        col.type = ParticleSystemCollisionType.Planes;
        col.mode = ParticleSystemCollisionMode.Collision3D;
        col.SetPlane(0, chipFloor);
        col.bounce = 0.35f; col.dampen = 0.4f; col.lifetimeLoss = 0.15f;
        chips.Play();

        // Sparks: tiny bright squares, shot out when the machine is hurt.
        GameObject spg = new GameObject("Sparks", typeof(ParticleSystem));
        spg.transform.SetParent(transform, false);
        sparks = spg.GetComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule spm = sparks.main;
        spm.playOnAwake = false; spm.loop = false; spm.simulationSpace = ParticleSystemSimulationSpace.World;
        spm.gravityModifier = 1.5f; spm.maxParticles = 200; spm.startSpeed = 0f;
        ParticleSystem.EmissionModule spe = sparks.emission; spe.enabled = false;
        ParticleSystem.ShapeModule sps = sparks.shape; sps.enabled = false;
        ParticleSystemRenderer spr = spg.GetComponent<ParticleSystemRenderer>();
        if (sprite != null) spr.sharedMaterial = new Material(sprite);
        sparks.Play();

        // Exhaust smoke: a thin grey stream rising from the stack.
        GameObject sg = new GameObject("Smoke", typeof(ParticleSystem));
        sg.transform.SetParent(exhaustTop, false);
        sg.transform.localPosition = Vector3.zero;
        sg.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        smoke = sg.GetComponent<ParticleSystem>();
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule sm = smoke.main;
        sm.loop = true; sm.startLifetime = 1.4f; sm.startSpeed = 0.7f * s; sm.startSize = 0.1f * s;
        sm.simulationSpace = ParticleSystemSimulationSpace.World;
        sm.gravityModifier = -0.05f; sm.maxParticles = 120;
        sm.startColor = new Color(0.45f, 0.45f, 0.48f, 0.6f);
        ParticleSystem.EmissionModule se = smoke.emission; se.rateOverTime = 7f;
        ParticleSystem.ShapeModule ss = smoke.shape; ss.shapeType = ParticleSystemShapeType.Cone; ss.angle = 8f; ss.radius = 0.02f * s;
        ParticleSystem.SizeOverLifetimeModule so = smoke.sizeOverLifetime; so.enabled = true;
        so.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 2.6f)));
        ParticleSystem.ColorOverLifetimeModule co = smoke.colorOverLifetime; co.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
        co.color = g;
        ParticleSystemRenderer sr = sg.GetComponent<ParticleSystemRenderer>();
        if (sprite != null) sr.sharedMaterial = new Material(sprite);
        smoke.Play();
    }

    private static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 1.5f);
    }

    // ---- the kit: makes primitives with a cached material per colour ----

    /// <summary>How much of its colour the machine's paint keeps (1 = the full palette).</summary>
    private const float PaintBrightness = 1f;

    private sealed class Kit
    {
        public PixelClicker clicker;
        public bool ghost;
        public float alpha;
        private readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        public Material Mat(Color c, float metal = 0.25f, float smooth = 0.45f, bool unique = false)
        {
            Color col = c;
            col.r *= PaintBrightness; col.g *= PaintBrightness; col.b *= PaintBrightness;
            if (ghost) col.a = alpha;
            string key = col.ToString() + metal + smooth;
            if (!unique && mats.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material m = clicker != null ? clicker.CreateVisualMaterial(col, ghost) : null;
            if (m == null)
            {
                Shader sh = Shader.Find("Standard");
                m = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
                m.color = col;
            }
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            // The cube's material (which the copy comes from) can glow: switch every trace of emission off, or the whole machine shines like the cube.
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
            if (!unique) mats[key] = m;
            return m;
        }

        public GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default, float metal = 0.25f, float smooth = 0.45f, bool collider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null && (ghost || !collider)) Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = Mat(c, metal, smooth);
            if (ghost) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            return go;
        }

        public GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Color c, Vector3 euler = default, bool collider = false, float metal = 0.25f, float smooth = 0.45f)
            => Prim(PrimitiveType.Cube, name, parent, pos, size, c, euler, metal, smooth, collider);

        /// <summary>A cylinder of 'radius' and 'length' along the axis given by 'euler' (0 = along Y).</summary>
        public GameObject Cyl(string name, Transform parent, Vector3 pos, float radius, float length, Color c, Vector3 euler = default, float metal = 0.25f, float smooth = 0.45f)
            => Prim(PrimitiveType.Cylinder, name, parent, pos, new Vector3(radius * 2f, length * 0.5f, radius * 2f), c, euler, metal, smooth, false);

        public GameObject Ball(string name, Transform parent, Vector3 pos, float radius, Color c, float metal = 0.25f, float smooth = 0.6f)
            => Prim(PrimitiveType.Sphere, name, parent, pos, Vector3.one * radius * 2f, c, default, metal, smooth, false);
    }

    private static GameObject Empty(string name, Transform parent, Vector3 pos)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        return g;
    }

    // ---- chassis: frame, wheels, fenders, hitch ----

    private static void BuildChassis(Kit k, Transform root, Transform sh)
    {
        k.Box("Frame", sh, new Vector3(0.45f, 0.7f, 0f), new Vector3(4.5f, 0.16f, 1.2f), Dark, default, true, 0.5f, 0.35f);
        k.Box("Frame Rail L", sh, new Vector3(0.45f, 0.62f, 0.5f), new Vector3(4.5f, 0.1f, 0.1f), Steel, default, false, 0.7f, 0.5f);
        k.Box("Frame Rail R", sh, new Vector3(0.45f, 0.62f, -0.5f), new Vector3(4.5f, 0.1f, 0.1f), Steel, default, false, 0.7f, 0.5f);
        k.Box("Axle", root, new Vector3(0.6f, 0.52f, 0f), new Vector3(0.1f, 0.1f, 1.7f), Steel, default, false, 0.8f, 0.5f);

        for (int side = -1; side <= 1; side += 2)
        {
            Transform wheel = Empty("Wheel", root, new Vector3(0.6f, 0.52f, 0.8f * side)).transform;
            k.Cyl("Tyre", wheel, Vector3.zero, 0.52f, 0.3f, Rubber, new Vector3(90f, 0f, 0f), 0f, 0.15f);
            k.Cyl("Rim", wheel, new Vector3(0f, 0f, 0.03f * side), 0.33f, 0.32f, Steel, new Vector3(90f, 0f, 0f), 0.85f, 0.6f);
            k.Cyl("Hub", wheel, new Vector3(0f, 0f, 0.17f * side), 0.1f, 0.06f, Dark, new Vector3(90f, 0f, 0f), 0.6f, 0.4f);
            for (int n = 0; n < 6; n++)
            {
                float a = n * 60f * Mathf.Deg2Rad;
                k.Cyl("Lug", wheel, new Vector3(Mathf.Cos(a) * 0.19f, Mathf.Sin(a) * 0.19f, 0.17f * side), 0.022f, 0.05f, Steel, new Vector3(90f, 0f, 0f), 0.9f, 0.6f);
            }
            for (int n = 0; n < 24; n++)   // tread blocks round the tyre
            {
                float a = n * 15f * Mathf.Deg2Rad;
                k.Box("Tread", wheel, new Vector3(Mathf.Cos(a) * 0.52f, Mathf.Sin(a) * 0.52f, 0f), new Vector3(0.1f, 0.06f, 0.31f), new Color(0.04f, 0.04f, 0.05f), new Vector3(0f, 0f, n * 15f), false, 0f, 0.1f);
            }
            // fender
            k.Box("Fender", sh, new Vector3(0.6f, 1.12f, 0.8f * side), new Vector3(1.15f, 0.05f, 0.4f), Dark, default, false, 0.4f, 0.4f);
            k.Box("Fender Front", sh, new Vector3(0.07f, 1.0f, 0.8f * side), new Vector3(0.3f, 0.05f, 0.4f), Dark, new Vector3(0f, 0f, 38f), false, 0.4f, 0.4f);
            k.Box("Fender Back", sh, new Vector3(1.13f, 1.0f, 0.8f * side), new Vector3(0.3f, 0.05f, 0.4f), Dark, new Vector3(0f, 0f, -38f), false, 0.4f, 0.4f);
            k.Box("Fender Stripe", sh, new Vector3(0.6f, 1.15f, 0.8f * side), new Vector3(1.15f, 0.015f, 0.4f), Yellow, default, false, 0.1f, 0.4f);
        }

        // Hitch: an A-frame, a coupler ball, a jack stand with a foot.
        for (int side = -1; side <= 1; side += 2)
            k.Box("Hitch Arm", sh, new Vector3(3.2f, 0.7f, 0.3f * side), new Vector3(1.35f, 0.1f, 0.1f), Dark, new Vector3(0f, -side * 22f, 0f), false, 0.5f, 0.35f);
        k.Cyl("Coupler Neck", sh, new Vector3(3.78f, 0.76f, 0f), 0.09f, 0.14f, Steel, default, 0.8f, 0.5f);
        k.Ball("Coupler", sh, new Vector3(3.78f, 0.88f, 0f), 0.13f, Steel, 0.8f, 0.6f);
        k.Cyl("Jack Post", sh, new Vector3(3.2f, 0.45f, 0f), 0.05f, 0.55f, Steel, default, 0.8f, 0.5f);
        k.Box("Jack Foot", sh, new Vector3(3.2f, 0.17f, 0f), new Vector3(0.3f, 0.04f, 0.3f), Dark, default, false, 0.5f, 0.3f);
        k.Cyl("Jack Crank", sh, new Vector3(3.2f, 0.62f, 0.1f), 0.015f, 0.25f, Red, new Vector3(90f, 0f, 0f));

        // Reflectors and tail light.
        k.Box("Reflector R1", sh, new Vector3(2.68f, 0.78f, 0.55f), new Vector3(0.03f, 0.08f, 0.14f), new Color(1f, 0.45f, 0.05f));
        k.Box("Reflector R2", sh, new Vector3(2.68f, 0.78f, -0.55f), new Vector3(0.03f, 0.08f, 0.14f), new Color(1f, 0.45f, 0.05f));
        k.Box("Reflector F1", sh, new Vector3(-1.78f, 0.78f, 0.55f), new Vector3(0.03f, 0.08f, 0.14f), Red);
        k.Box("Reflector F2", sh, new Vector3(-1.78f, 0.78f, -0.55f), new Vector3(0.03f, 0.08f, 0.14f), Red);
        // Infeed table the pixels land on below the hopper.
        k.Box("Infeed Table", sh, new Vector3(-1.2f, 1.2f, 0f), new Vector3(0.84f, 0.8f, 1.1f), Dark, default, true, 0.5f, 0.3f);
    }

    // ---- cutter housing: body, hatch, bolts, danger plate, stripes ----

    private static void BuildHousing(Kit k, Transform sh)
    {
        k.Box("Housing", sh, new Vector3(0.1f, 1.45f, 0f), new Vector3(1.7f, 1.35f, 1.25f), Orange, default, true, 0.3f, 0.5f);
        k.Box("Housing Top Plate", sh, new Vector3(0.1f, 2.15f, 0f), new Vector3(1.78f, 0.06f, 1.33f), DarkOrange, default, false, 0.4f, 0.5f);
        k.Box("Housing Base Plate", sh, new Vector3(0.1f, 0.8f, 0f), new Vector3(1.78f, 0.06f, 1.33f), DarkOrange, default, false, 0.4f, 0.5f);
        for (int side = -1; side <= 1; side += 2)
        {
            float z = 0.64f * side;
            // inspection hatch with bolts and a handle
            k.Box("Hatch", sh, new Vector3(-0.15f, 1.5f, z), new Vector3(0.75f, 0.75f, 0.035f), DarkOrange, default, false, 0.4f, 0.5f);
            k.Box("Hatch Frame", sh, new Vector3(-0.15f, 1.5f, z * 0.995f), new Vector3(0.82f, 0.82f, 0.03f), Dark, default, false, 0.5f, 0.4f);
            for (int bx = -1; bx <= 1; bx += 2)
                for (int by = -1; by <= 1; by += 2)
                    k.Ball("Bolt", sh, new Vector3(-0.15f + bx * 0.33f, 1.5f + by * 0.33f, z + 0.02f * side), 0.035f, Steel, 0.9f, 0.7f);
            k.Cyl("Handle Post A", sh, new Vector3(0.1f, 1.65f, z + 0.04f * side), 0.015f, 0.08f, Steel, new Vector3(90f, 0f, 0f), 0.9f, 0.7f);
            k.Cyl("Handle Post B", sh, new Vector3(0.1f, 1.35f, z + 0.04f * side), 0.015f, 0.08f, Steel, new Vector3(90f, 0f, 0f), 0.9f, 0.7f);
            k.Cyl("Handle", sh, new Vector3(0.1f, 1.5f, z + 0.08f * side), 0.018f, 0.34f, Steel, default, 0.9f, 0.7f);
            // rivets along the top edge
            for (int n = 0; n < 9; n++) k.Ball("Rivet", sh, new Vector3(-0.68f + n * 0.17f, 2.08f, z + 0.015f * side), 0.022f, Steel, 0.9f, 0.7f);
            // hazard stripe along the bottom
            for (int n = 0; n < 8; n++)
                k.Box("Stripe", sh, new Vector3(-0.62f + n * 0.2f, 0.9f, z + 0.005f * side), new Vector3(0.1f, 0.12f, 0.012f), n % 2 == 0 ? Yellow : Rubber, new Vector3(0f, 0f, 30f));
        }
        // Danger plate on the +Z side above the hatch: a yellow triangle-ish plate with a bar and a dot.
        k.Box("Danger Plate", sh, new Vector3(0.62f, 1.8f, 0.665f), new Vector3(0.34f, 0.3f, 0.015f), Yellow, default, false, 0f, 0.3f);
        k.Box("Danger Bar", sh, new Vector3(0.62f, 1.84f, 0.675f), new Vector3(0.05f, 0.14f, 0.012f), Rubber);
        k.Ball("Danger Dot", sh, new Vector3(0.62f, 1.7f, 0.675f), 0.03f, Rubber, 0f, 0.3f);
        k.Box("Brand Band", sh, new Vector3(0.62f, 1.28f, 0.665f), new Vector3(0.42f, 0.1f, 0.012f), Rubber);
        for (int n = 0; n < 5; n++) k.Box("Brand Block", sh, new Vector3(0.48f + n * 0.07f, 1.28f, 0.672f), new Vector3(0.05f, 0.06f, 0.01f), n % 2 == 0 ? Orange : Yellow);
    }

    // ---- the feed hopper: double-sided funnel, liner, hazard rim, rollers ----

    private static Mesh FunnelMesh(Vector3[] b, Vector3[] m)
    {
        // Four walls, each with a front face (normal pointing out of the funnel) and its OWN back face (opposite normal, opposite winding) so it shows from either side.
        // The two faces must not share vertices: shared vertices make RecalculateNormals average the opposite normals to zero, and a zero normal makes the lit shader output NaN,
        // which bloom spreads into a huge white blob.
        Vector3 centre = Vector3.zero;
        for (int i = 0; i < 4; i++) centre += b[i] + m[i];
        centre /= 8f;
        List<Vector3> v = new List<Vector3>(), nrm = new List<Vector3>();
        List<int> tri = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            Vector3 p0 = b[i], p1 = b[j], p2 = m[j], p3 = m[i];
            Vector3 n = Vector3.Cross(p1 - p0, p3 - p0).normalized;
            if (Vector3.Dot(n, (p0 + p1 + p2 + p3) * 0.25f - centre) < 0f) n = -n;   // outward
            int s = v.Count;
            for (int k = 0; k < 2; k++)
            {
                v.AddRange(new[] { p0, p1, p2, p3 });
                Vector3 nn = k == 0 ? n : -n;
                nrm.AddRange(new[] { nn, nn, nn, nn });
            }
            bool flip = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) < 0f;
            if (!flip) tri.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3, s + 4, s + 6, s + 5, s + 4, s + 7, s + 6 });
            else tri.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2, s + 4, s + 5, s + 6, s + 4, s + 6, s + 7 });
        }
        Mesh mesh = new Mesh { name = "Funnel" };
        mesh.SetVertices(v);
        mesh.SetNormals(nrm);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector3[] Corners(float cx, float y, float hx, float hz)
    {
        return new[] { new Vector3(cx - hx, y, -hz), new Vector3(cx + hx, y, -hz), new Vector3(cx + hx, y, hz), new Vector3(cx - hx, y, hz) };
    }

    private static void AddMeshObject(Kit k, Transform parent, string name, Mesh mesh, Color c, float metal, float smooth)
    {
        GameObject g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        g.transform.SetParent(parent, false);
        g.GetComponent<MeshFilter>().sharedMesh = mesh;
        g.GetComponent<MeshRenderer>().sharedMaterial = k.Mat(c, metal, smooth);
        if (k.ghost) { g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
    }

    private static void BuildHopper(Kit k, Transform sh, out Transform rollerA, out Transform rollerB)
    {
        Vector3[] b = Corners(-1.2f, FunnelBaseY, 0.42f, 0.55f), m = Corners(-1.5f, FunnelMouthY, 1f, 1f);
        AddMeshObject(k, sh, "Hopper Outer", FunnelMesh(b, m), Orange, 0.3f, 0.5f);
        Vector3[] bi = Corners(-1.2f, FunnelBaseY + 0.03f, 0.39f, 0.52f), mi = Corners(-1.5f, FunnelMouthY - 0.03f, 0.96f, 0.96f);
        AddMeshObject(k, sh, "Hopper Liner", FunnelMesh(bi, mi), new Color(0.12f, 0.12f, 0.14f), 0.5f, 0.35f);

        // Thin collider walls so pixels slide down inside it (real machine only).
        if (!k.ghost)
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Vector3 mid = (b[i] + b[j] + m[i] + m[j]) * 0.25f;
                Vector3 across = ((b[j] - b[i]) + (m[j] - m[i])) * 0.5f;
                Vector3 up = ((m[i] + m[j]) - (b[i] + b[j])) * 0.5f;
                Vector3 normal = Vector3.Cross(across, up).normalized;
                GameObject wall = new GameObject("Hopper Wall " + i, typeof(BoxCollider));
                wall.transform.SetParent(sh, false);
                wall.transform.localPosition = mid;
                wall.transform.localRotation = Quaternion.LookRotation(normal, up.normalized);
                wall.GetComponent<BoxCollider>().size = new Vector3(across.magnitude, up.magnitude, 0.06f);
            }

        // Rim: four yellow bars with black hazard blocks.
        float hx = 1f, hz = 1f, cx = -1.5f, y = FunnelMouthY;
        k.Box("Rim -Z", sh, new Vector3(cx, y, -hz), new Vector3(hx * 2f + 0.1f, 0.09f, 0.1f), Yellow, default, false, 0.2f, 0.4f);
        k.Box("Rim +Z", sh, new Vector3(cx, y, hz), new Vector3(hx * 2f + 0.1f, 0.09f, 0.1f), Yellow, default, false, 0.2f, 0.4f);
        k.Box("Rim -X", sh, new Vector3(cx - hx, y, 0f), new Vector3(0.1f, 0.09f, hz * 2f), Yellow, default, false, 0.2f, 0.4f);
        k.Box("Rim +X", sh, new Vector3(cx + hx, y, 0f), new Vector3(0.1f, 0.09f, hz * 2f), Yellow, default, false, 0.2f, 0.4f);
        for (int n = 0; n < 8; n++)
        {
            float x = cx - hx + 0.15f + n * 0.24f;
            if (n % 2 == 0) { k.Box("Hazard", sh, new Vector3(x, y + 0.001f, -hz), new Vector3(0.12f, 0.095f, 0.102f), Rubber); k.Box("Hazard", sh, new Vector3(x, y + 0.001f, hz), new Vector3(0.12f, 0.095f, 0.102f), Rubber); }
            float zz = -hz + 0.15f + n * 0.24f;
            if (n % 2 == 1) { k.Box("Hazard", sh, new Vector3(cx - hx, y + 0.001f, zz), new Vector3(0.102f, 0.095f, 0.12f), Rubber); k.Box("Hazard", sh, new Vector3(cx + hx, y + 0.001f, zz), new Vector3(0.102f, 0.095f, 0.12f), Rubber); }
        }
        // Hopper braces and a splash flap.
        for (int side = -1; side <= 1; side += 2)
        {
            k.Box("Brace", sh, new Vector3(-1.35f, 2.2f, 0.78f * side), new Vector3(0.05f, 0.95f, 0.05f), Steel, new Vector3(side * 18f, 0f, 0f), false, 0.8f, 0.5f);
            k.Ball("Brace Bolt", sh, new Vector3(-1.45f, 2.64f, 0.95f * side), 0.04f, Steel, 0.9f, 0.7f);
        }
        k.Box("Safety Bar", sh, new Vector3(-2.52f, 2.6f, 0f), new Vector3(0.05f, 0.05f, 1.9f), Red, default, false, 0.2f, 0.5f);

        // Two ribbed feed rollers at the bottom of the throat (they turn).
        rollerA = BuildRoller(k, sh, new Vector3(-1.34f, 1.82f, 0f));
        rollerB = BuildRoller(k, sh, new Vector3(-1.06f, 1.82f, 0f));
        k.Box("Roller Bearing L", sh, new Vector3(-1.2f, 1.82f, 0.5f), new Vector3(0.7f, 0.2f, 0.06f), Dark, default, false, 0.7f, 0.4f);
        k.Box("Roller Bearing R", sh, new Vector3(-1.2f, 1.82f, -0.5f), new Vector3(0.7f, 0.2f, 0.06f), Dark, default, false, 0.7f, 0.4f);
        // Hydraulic lines from the rollers to the engine side.
        k.Cyl("Hose A", sh, new Vector3(-0.78f, 1.82f, 0.55f), 0.025f, 0.35f, Rubber, new Vector3(0f, 0f, 90f), 0f, 0.2f);
        k.Cyl("Hose B", sh, new Vector3(-0.78f, 1.7f, -0.55f), 0.025f, 0.35f, Rubber, new Vector3(0f, 0f, 90f), 0f, 0.2f);
    }

    private static Transform BuildRoller(Kit k, Transform parent, Vector3 pos)
    {
        Transform r = Empty("Roller", parent, pos).transform;
        k.Cyl("Drum", r, Vector3.zero, 0.12f, 0.95f, Steel, new Vector3(90f, 0f, 0f), 0.85f, 0.5f);
        for (int n = 0; n < 8; n++)
        {
            float a = n * 45f * Mathf.Deg2Rad;
            k.Box("Rib", r, new Vector3(Mathf.Cos(a) * 0.125f, Mathf.Sin(a) * 0.125f, 0f), new Vector3(0.05f, 0.03f, 0.95f), Dark, new Vector3(0f, 0f, n * 45f), false, 0.8f, 0.4f);
        }
        return r;
    }

    // ---- discharge chute with a hood ----

    private static void BuildChute(Kit k, Transform sh)
    {
        k.Cyl("Chute Base", sh, new Vector3(0.25f, 2.3f, 0f), 0.34f, 0.3f, DarkOrange, default, 0.4f, 0.5f);
        k.Cyl("Chute Tube", sh, new Vector3(0.25f, 2.7f, 0f), 0.28f, 0.8f, Steel, default, 0.8f, 0.55f);
        k.Cyl("Chute Collar", sh, new Vector3(0.25f, 3.0f, 0f), 0.31f, 0.08f, Yellow, default, 0.3f, 0.4f);
        // Swivel hood: a horizontal tube pointing out to +Z, tilted up, with a flared mouth.
        k.Cyl("Hood", sh, new Vector3(0.25f, 3.08f, 0.3f), 0.26f, 0.7f, Steel, new Vector3(90f - 24f, 0f, 0f), 0.8f, 0.55f);
        k.Cyl("Hood Mouth", sh, new Vector3(0.25f, 3.22f, 0.62f), 0.31f, 0.1f, Yellow, new Vector3(90f - 24f, 0f, 0f), 0.3f, 0.4f);
        k.Ball("Hood Cap", sh, new Vector3(0.25f, 3.0f, 0f), 0.3f, Steel, 0.8f, 0.55f);
        k.Box("Hood Handle", sh, new Vector3(0.25f, 3.3f, 0.12f), new Vector3(0.06f, 0.06f, 0.4f), Red, default, false, 0.2f, 0.5f);
        for (int n = 0; n < 6; n++)
        {
            float a = n * 60f * Mathf.Deg2Rad;
            k.Ball("Collar Bolt", sh, new Vector3(0.25f + Mathf.Cos(a) * 0.31f, 3.0f, Mathf.Sin(a) * 0.31f), 0.025f, Steel, 0.9f, 0.7f);
        }
    }

    // ---- flywheel with its guard (a spinning pulley behind a grille) ----

    private static void BuildFlywheel(Kit k, Transform sh, out Transform spinner)
    {
        Vector3 c = new Vector3(0.1f, 1.45f, 0.78f);
        spinner = Empty("Flywheel", sh, c).transform;
        k.Cyl("Pulley", spinner, Vector3.zero, 0.36f, 0.1f, Steel, new Vector3(90f, 0f, 0f), 0.85f, 0.5f);
        for (int n = 0; n < 3; n++) k.Box("Spoke", spinner, Vector3.zero, new Vector3(0.7f, 0.07f, 0.05f), Dark, new Vector3(0f, 0f, n * 60f), false, 0.7f, 0.4f);
        k.Cyl("Pulley Hub", spinner, new Vector3(0f, 0f, 0.05f), 0.08f, 0.06f, Red, new Vector3(90f, 0f, 0f), 0.4f, 0.5f);
        // The guard: an outer ring of little blocks and radial bars in front of it.
        for (int n = 0; n < 20; n++)
        {
            float a = n * 18f * Mathf.Deg2Rad;
            k.Box("Guard Ring", sh, c + new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0.1f), new Vector3(0.17f, 0.05f, 0.05f), Dark, new Vector3(0f, 0f, n * 18f + 90f), false, 0.6f, 0.4f);
        }
        for (int n = 0; n < 8; n++) k.Box("Guard Bar", sh, c + new Vector3(0f, 0f, 0.1f), new Vector3(1f, 0.025f, 0.025f), Dark, new Vector3(0f, 0f, n * 22.5f), false, 0.6f, 0.4f);
        k.Cyl("Guard Boss", sh, c + new Vector3(0f, 0f, 0.1f), 0.06f, 0.05f, Yellow, new Vector3(90f, 0f, 0f), 0.4f, 0.4f);
    }

    // ---- engine ----

    private static void BuildEngine(Kit k, Transform sh, out Transform exhaustTop)
    {
        k.Cyl("Coupling", sh, new Vector3(1.1f, 1.45f, 0f), 0.13f, 0.4f, Steel, new Vector3(0f, 0f, 90f), 0.85f, 0.5f);
        k.Box("Engine Block", sh, new Vector3(1.85f, 1.2f, 0f), new Vector3(1.0f, 0.7f, 0.9f), Dark, default, true, 0.55f, 0.35f);
        k.Box("Engine Base", sh, new Vector3(1.85f, 0.88f, 0f), new Vector3(1.1f, 0.08f, 1.0f), Steel, default, false, 0.7f, 0.4f);
        k.Box("Valve Cover", sh, new Vector3(1.85f, 1.62f, 0f), new Vector3(0.82f, 0.18f, 0.7f), Orange, default, false, 0.3f, 0.55f);
        for (int n = 0; n < 7; n++)
        {
            k.Box("Fin", sh, new Vector3(1.5f + n * 0.12f, 1.2f, 0.47f), new Vector3(0.05f, 0.62f, 0.05f), Steel, default, false, 0.8f, 0.5f);
            k.Box("Fin", sh, new Vector3(1.5f + n * 0.12f, 1.2f, -0.47f), new Vector3(0.05f, 0.62f, 0.05f), Steel, default, false, 0.8f, 0.5f);
        }
        for (int n = 0; n < 4; n++) k.Ball("Valve Bolt", sh, new Vector3(1.55f + (n % 2) * 0.6f, 1.72f, -0.3f + (n / 2) * 0.6f), 0.028f, Steel, 0.9f, 0.7f);
        // air filter, fuel tank, filler cap
        k.Cyl("Air Filter", sh, new Vector3(1.5f, 1.88f, 0.22f), 0.17f, 0.3f, Yellow, default, 0.3f, 0.45f);
        k.Cyl("Air Filter Cap", sh, new Vector3(1.5f, 2.05f, 0.22f), 0.19f, 0.05f, Dark, default, 0.6f, 0.4f);
        k.Cyl("Fuel Tank", sh, new Vector3(1.9f, 1.95f, -0.12f), 0.21f, 0.62f, Red, new Vector3(0f, 0f, 90f), 0.35f, 0.65f);
        k.Cyl("Fuel Cap", sh, new Vector3(1.9f, 2.17f, -0.12f), 0.06f, 0.06f, Dark, default, 0.6f, 0.4f);
        // muffler + exhaust stack with a rain cap
        k.Cyl("Muffler", sh, new Vector3(2.05f, 1.72f, -0.62f), 0.14f, 0.55f, Steel, new Vector3(0f, 0f, 90f), 0.85f, 0.55f);
        k.Cyl("Stack", sh, new Vector3(2.25f, 2.2f, -0.62f), 0.055f, 1.0f, Steel, default, 0.85f, 0.55f);
        k.Cyl("Stack Cap", sh, new Vector3(2.25f, 2.73f, -0.62f), 0.1f, 0.03f, Dark, default, 0.6f, 0.4f);
        exhaustTop = Empty("Exhaust Top", sh, new Vector3(2.25f, 2.7f, -0.62f)).transform;
        // heat shield grille
        for (int n = 0; n < 5; n++) k.Box("Heat Shield", sh, new Vector3(1.55f + n * 0.14f, 1.45f, -0.57f), new Vector3(0.04f, 0.5f, 0.02f), Dark, default, false, 0.5f, 0.3f);
        k.Box("Heat Shield Frame", sh, new Vector3(1.83f, 1.45f, -0.585f), new Vector3(0.72f, 0.04f, 0.02f), Dark);
    }

    // ---- control panel + beacon ----

    private static Material BuildControls(Kit k, Transform sh, out Transform lamp)
    {
        Vector3 c = new Vector3(-0.5f, 1.42f, 0.7f);
        k.Box("Panel", sh, c, new Vector3(0.38f, 0.5f, 0.08f), Dark, new Vector3(-12f, 0f, 0f), false, 0.5f, 0.3f);
        Color[] knob = { Red, new Color(0.15f, 0.7f, 0.25f), Yellow };
        for (int n = 0; n < 3; n++)
        {
            float x = c.x - 0.12f + n * 0.12f;
            k.Cyl("Lever", sh, new Vector3(x, c.y - 0.02f, c.z + 0.12f), 0.014f, 0.22f, Steel, new Vector3(-30f, 0f, 0f), 0.9f, 0.6f);
            k.Ball("Lever Knob", sh, new Vector3(x, c.y + 0.07f, c.z + 0.2f), 0.04f, knob[n], 0.2f, 0.6f);
        }
        // gauge
        k.Cyl("Gauge", sh, new Vector3(c.x - 0.1f, c.y + 0.17f, c.z + 0.05f), 0.07f, 0.03f, Color.white, new Vector3(78f, 0f, 0f), 0f, 0.7f);
        k.Box("Gauge Needle", sh, new Vector3(c.x - 0.1f, c.y + 0.18f, c.z + 0.075f), new Vector3(0.012f, 0.06f, 0.006f), Red, new Vector3(-12f, 0f, 25f));
        // e-stop: yellow base, red mushroom
        k.Cyl("E-Stop Base", sh, new Vector3(c.x + 0.1f, c.y + 0.17f, c.z + 0.05f), 0.065f, 0.03f, Yellow, new Vector3(78f, 0f, 0f), 0.2f, 0.5f);
        k.Cyl("E-Stop", sh, new Vector3(c.x + 0.1f, c.y + 0.18f, c.z + 0.08f), 0.045f, 0.045f, Red, new Vector3(78f, 0f, 0f), 0.2f, 0.7f);

        // Amber beacon on the roof (the lamp's material is returned so the machine can blink it).
        k.Cyl("Beacon Base", sh, new Vector3(-0.55f, 2.22f, -0.45f), 0.1f, 0.06f, Dark, default, 0.5f, 0.4f);
        GameObject lampGo = k.Ball("Beacon", sh, new Vector3(-0.55f, 2.3f, -0.45f), 0.09f, new Color(1f, 0.7f, 0.1f), 0f, 0.85f);
        lamp = lampGo.transform;
        Material own = k.Mat(new Color(1f, 0.7f, 0.1f), 0f, 0.85f, true);   // its own material so blinking doesn't recolour anything else
        lampGo.GetComponent<Renderer>().sharedMaterial = own;
        if (!k.ghost) own.EnableKeyword("_EMISSION");   // the beacon is the one thing that glows
        return k.ghost ? null : own;
    }
}
