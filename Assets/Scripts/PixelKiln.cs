// ---- System notes ----
// PixelKiln.cs
//   The KILN (a Dev Hub > Tester feature, PixelFeatureTester 'Kiln'): a brick kiln with a glowing mouth on its +X side, a chimney that smokes and a chute at the back. ANYTHING that goes
//   into the mouth from the side burns: an old pixel (not held, not glued into a build) inside the mouth zone (a box just in front of the door, mouthCenter / mouthHalf in kiln units)
//   is taken out of the game (PixelClicker.ClaimOldPixel, no payout), drawn in while it shrinks and flares, and a CHARCOAL old pixel (PixelType.Charcoal 81, PixelClicker.EnsureCharcoalTier,
//   standalone, despawns like any old pixel) is queued: charcoalPerPixel of them come out of the back chute charcoalDelaySeconds after the burn, one every charcoalGapSeconds. A conveyor
//   SOCKETS (PixelSockets.cs): 'mouth' (In, +X) - a belt / connector / hose ending at an In coupling there hands its husks straight in (AcceptHusk); 'chute' (Out, -X) -
//   with an Out coupling there the charcoal goes onto the belt that starts at it (PixelCouplings.Emit), else it drops out as before. The kiln roars while it burns (light, ember sparks, thicker smoke). Saved with the tester save.
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PixelKiln : MonoBehaviour, IPixelSocketDevice
{
    [Header("Burning")]
    [Min(0f)]
    [Tooltip("How many seconds a pixel takes to burn once it is in the mouth. 0 = the coded default (1.1).")]
    [SerializeField] private float burnSeconds = 0f;

    [Min(0)]
    [Tooltip("Charcoal old pixels one burnt pixel makes. 0 = the coded default (1).")]
    [SerializeField] private int charcoalPerPixel = 0;

    [Min(0f)]
    [Tooltip("Seconds between a burn and its charcoal coming out. 0 = the coded default (1.5).")]
    [SerializeField] private float charcoalDelaySeconds = 0f;

    [Min(0f)]
    [Tooltip("Seconds between two charcoal blocks leaving the chute. 0 = the coded default (0.35).")]
    [SerializeField] private float charcoalGapSeconds = 0f;

    [Min(0)]
    [Tooltip("The most charcoal waiting inside at once (more burnt pixels are still eaten but make nothing). 0 = the coded default (30).")]
    [SerializeField] private int maxQueued = 0;

    // Kiln units: 1 = one cube width. +X is the front (the mouth), -X the back (the chute).
    private static readonly Vector3 MouthCenter = new Vector3(1.75f, 0.75f, 0f), MouthHalf = new Vector3(0.65f, 0.85f, 0.75f);
    private static readonly Vector3 OutletLocal = new Vector3(-1.95f, 0.65f, 0f);

    private PixelClicker clicker;
    private float unit;
    private Light fireLight;
    private ParticleSystem smoke, embers;
    private float roar;   // 0..1, rises when something burns and fades
    private int pendingCharcoal;
    private float nextOutAt;
    private readonly List<float> readyTimes = new List<float>();

    private float Burn => burnSeconds > 0f ? burnSeconds : 1.1f;
    private int PerPixel => charcoalPerPixel > 0 ? charcoalPerPixel : 1;
    private float Delay => charcoalDelaySeconds > 0f ? charcoalDelaySeconds : 1.5f;
    private float Gap => charcoalGapSeconds > 0f ? charcoalGapSeconds : 0.35f;
    private int MaxQueued => maxQueued > 0 ? maxQueued : 30;

    // ---------------- placement (PixelFeatureTester) ----------------

    public static GameObject CreateGhost(PixelClicker c)
    {
        GameObject root = Build(c, true);
        root.name = "Kiln (Ghost)";
        return root;
    }

    public static GameObject Place(PixelClicker c, Vector3 point, float yaw)
    {
        GameObject root = Build(c, false);
        root.name = "Kiln";
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        return root;
    }

    private static GameObject Build(PixelClicker c, bool ghost)
    {
        float u = c.OldPixelWorldSize;
        PixelFactoryKit k = new PixelFactoryKit(c, ghost);
        GameObject root = new GameObject("Kiln");
        Transform t = root.transform;
        Color brick = new Color(0.62f, 0.27f, 0.2f, 1f), brickDark = new Color(0.45f, 0.19f, 0.15f, 1f), stone = new Color(0.55f, 0.53f, 0.5f, 1f), soot = new Color(0.07f, 0.06f, 0.06f, 1f);
        Color fire = new Color(1f, 0.5f, 0.12f, 1f);

        // The body: a brick block with a stepped roof and a stone base.
        k.Box("Base", t, new Vector3(0f, 0.12f * u, 0f), new Vector3(3.5f * u, 0.24f * u, 2.5f * u), stone, default, true, 0.1f, 0.2f);
        k.Box("Body", t, new Vector3(0f, 1.05f * u, 0f), new Vector3(2.7f * u, 1.7f * u, 2.1f * u), brick, default, true, 0f, 0.15f);
        k.Box("Brick Band", t, new Vector3(0f, 0.6f * u, 0f), new Vector3(2.76f * u, 0.12f * u, 2.16f * u), brickDark, default, false, 0f, 0.1f);
        k.Box("Brick Band Top", t, new Vector3(0f, 1.55f * u, 0f), new Vector3(2.76f * u, 0.12f * u, 2.16f * u), brickDark, default, false, 0f, 0.1f);
        k.Box("Roof 1", t, new Vector3(0f, 2.0f * u, 0f), new Vector3(2.3f * u, 0.3f * u, 1.8f * u), brick, default, true, 0f, 0.15f);
        k.Box("Roof 2", t, new Vector3(-0.1f * u, 2.28f * u, 0f), new Vector3(1.7f * u, 0.3f * u, 1.4f * u), brickDark, default, false, 0f, 0.15f);
        // Brick lines on the long sides (a few thin dark strips).
        for (int i = 0; i < 4; i++)
            k.Box("Mortar", t, new Vector3(0f, (0.4f + i * 0.38f) * u, 1.052f * u), new Vector3(2.6f * u, 0.025f * u, 0.01f * u), soot, default, false, 0f, 0.05f);
        for (int i = 0; i < 4; i++)
            k.Box("Mortar", t, new Vector3(0f, (0.4f + i * 0.38f) * u, -1.052f * u), new Vector3(2.6f * u, 0.025f * u, 0.01f * u), soot, default, false, 0f, 0.05f);

        // The mouth on the +X face: a dark opening with a stone frame and a glowing fire inside.
        float fx = 1.36f * u;
        k.Box("Mouth", t, new Vector3(fx, 0.75f * u, 0f), new Vector3(0.1f * u, 1.1f * u, 1.1f * u), soot, default, false, 0f, 0.1f);
        k.Box("Fire Glow", t, new Vector3(fx + 0.02f * u, 0.55f * u, 0f), new Vector3(0.05f * u, 0.55f * u, 0.8f * u), fire, default, false, 0f, 0.2f, new Color(2.5f, 1.0f, 0.2f, 1f));
        k.Box("Frame Top", t, new Vector3(fx + 0.03f * u, 1.38f * u, 0f), new Vector3(0.18f * u, 0.2f * u, 1.45f * u), stone, default, false, 0.1f, 0.2f);
        k.Box("Frame L", t, new Vector3(fx + 0.03f * u, 0.75f * u, 0.64f * u), new Vector3(0.18f * u, 1.2f * u, 0.2f * u), stone, default, false, 0.1f, 0.2f);
        k.Box("Frame R", t, new Vector3(fx + 0.03f * u, 0.75f * u, -0.64f * u), new Vector3(0.18f * u, 1.2f * u, 0.2f * u), stone, default, false, 0.1f, 0.2f);
        // A stone apron in front of the mouth so things roll in.
        k.Box("Apron", t, new Vector3(2.0f * u, 0.05f * u, 0f), new Vector3(1.2f * u, 0.1f * u, 1.6f * u), stone, default, true, 0.1f, 0.2f);
        // Warning stripe on the apron.
        k.Box("Apron Stripe", t, new Vector3(1.55f * u, 0.105f * u, 0f), new Vector3(0.12f * u, 0.01f * u, 1.6f * u), new Color(1f, 0.78f, 0.15f, 1f), default, false, 0f, 0.2f);

        // The chimney and the charcoal chute at the back.
        k.Cyl("Chimney", t, new Vector3(-0.65f * u, 2.95f * u, 0.45f * u), 0.3f * u, 1.7f * u, stone, default, 0.1f, 0.2f);
        k.Cyl("Chimney Rim", t, new Vector3(-0.65f * u, 3.8f * u, 0.45f * u), 0.38f * u, 0.14f * u, brickDark, default, 0.1f, 0.2f);
        k.Box("Chute", t, new Vector3(-1.62f * u, 0.55f * u, 0f), new Vector3(0.5f * u, 0.7f * u, 0.9f * u), brickDark, default, true, 0f, 0.2f);
        k.Box("Chute Slope", t, new Vector3(-1.95f * u, 0.4f * u, 0f), new Vector3(0.7f * u, 0.08f * u, 0.9f * u), stone, new Vector3(0f, 0f, 14f), false, 0.1f, 0.2f);
        k.Box("Chute Hole", t, new Vector3(-1.88f * u, 0.62f * u, 0f), new Vector3(0.08f * u, 0.45f * u, 0.65f * u), soot, default, false, 0f, 0.1f);

        root.transform.localScale = Vector3.one;
        if (ghost) return root;

        PixelKiln kiln = root.AddComponent<PixelKiln>();
        kiln.clicker = c;
        kiln.unit = u;
        // Sockets (PixelSockets.cs): the MOUTH takes husks in (a belt / connector / hose ends at an In coupling there), the back CHUTE puts charcoal out.
        kiln.sockets = new[]
        {
            new PixelSocket("mouth", new Vector3(2.65f * u, 0f, 0f), Vector3.right, PixelSocketFlow.Intake),   // (at the front edge of the stone apron, so a belt ending there does not run into it)
            new PixelSocket("chute", new Vector3((OutletLocal.x - 0.25f) * u, 0f, 0f), Vector3.left, PixelSocketFlow.Output),
        };
        kiln.fireLight = MakeLight(t, new Vector3(fx + 0.5f * u, 0.7f * u, 0f), u);
        kiln.smoke = MakeSmoke(t, new Vector3(-0.65f * u, 3.9f * u, 0.45f * u), u);
        kiln.embers = MakeEmbers(t, new Vector3(fx + 0.1f * u, 0.7f * u, 0f), u);
        return root;
    }

    private static Light MakeLight(Transform parent, Vector3 pos, float u)
    {
        GameObject g = new GameObject("Fire Light");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.55f, 0.2f, 1f);
        l.range = 5f * u;
        l.intensity = 1.5f;
        l.shadows = LightShadows.None;
        return l;
    }

    private static ParticleSystem MakeSmoke(Transform parent, Vector3 pos, float u)
    {
        GameObject g = new GameObject("Smoke");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 2.6f; main.startSpeed = 0.7f * u; main.startSize = new ParticleSystem.MinMaxCurve(0.35f * u, 0.7f * u);
        main.startColor = new Color(0.3f, 0.3f, 0.32f, 0.45f); main.gravityModifier = -0.04f; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 80;
        var emission = ps.emission; emission.rateOverTime = 5f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 10f; shape.radius = 0.12f * u; shape.rotation = new Vector3(-90f, 0f, 0f);
        var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 2.2f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = new Material(PixelShaders.SpriteDefault());
        return ps;
    }

    private static ParticleSystem MakeEmbers(Transform parent, Vector3 pos, float u)
    {
        GameObject g = new GameObject("Embers");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * u, 2.2f * u); main.startSize = new ParticleSystem.MinMaxCurve(0.04f * u, 0.1f * u);
        main.startColor = new Color(1f, 0.6f, 0.15f, 1f); main.gravityModifier = 0.3f; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 120;
        var emission = ps.emission; emission.rateOverTime = 0f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 40f; shape.radius = 0.25f * u; shape.rotation = new Vector3(0f, 90f, 0f);
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = new Material(PixelShaders.SpriteDefault());
        return ps;
    }

    // ---------------- saving (PixelFeatureTester) ----------------

    [System.Serializable]
    private class Save { public Vector3 position; public float yaw; public int pending; }

    public static string ExportState(GameObject root)
    {
        PixelKiln k = root != null ? root.GetComponent<PixelKiln>() : null;
        if (k == null) return null;
        return JsonUtility.ToJson(new Save { position = root.transform.position, yaw = root.transform.eulerAngles.y, pending = k.pendingCharcoal });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        if (s == null) return null;
        GameObject root = Place(c, s.position, s.yaw);
        PixelKiln k = root.GetComponent<PixelKiln>();
        if (k != null) for (int i = 0; i < s.pending; i++) { k.pendingCharcoal++; k.readyTimes.Add(Time.time + 1f + i * 0.2f); }
        return root;
    }

    // ---------------- behaviour ----------------

    private void OnEnable() { if (clicker == null) clicker = PixelFind.First<PixelClicker>(); PixelCouplings.Register(this); }
    private void OnDisable() { PixelCouplings.Unregister(this); }

    // ---- Sockets ----
    private PixelSocket[] sockets = new PixelSocket[0];
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => new Color(0.62f, 0.27f, 0.2f, 1f);

    /// <summary>A husk comes in through the mouth's In coupling: it burns just like one that went into the mouth.</summary>
    public bool AcceptHusk(PixelSocket socket, Rigidbody pixel)
    {
        if (pixel == null || clicker == null || !clicker.ClaimOldPixel(pixel)) return false;
        StartCoroutine(BurnPixel(pixel));
        return true;
    }

    private bool InMouth(Vector3 world)
    {
        Vector3 l = transform.InverseTransformPoint(world) / Mathf.Max(0.0001f, unit);
        Vector3 d = l - MouthCenter;
        return Mathf.Abs(d.x) < MouthHalf.x && Mathf.Abs(d.y) < MouthHalf.y && Mathf.Abs(d.z) < MouthHalf.z;
    }

    private void FixedUpdate()
    {
        if (clicker == null) return;
        var list = clicker.OldPixels;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = list[i];
            if (rb == null || rb.isKinematic || PixelAttachment.IsAttached(rb) || clicker.IsFlyingPixel(rb)) continue;
            OldPixelDespawn despawn = PixelHuskQuery.Get(rb).despawn;
            if (despawn != null && (despawn.IsDespawning || despawn.Held)) continue;
            if (!InMouth(rb.worldCenterOfMass)) continue;
            if (clicker.ClaimOldPixel(rb)) StartCoroutine(BurnPixel(rb));
        }
    }

    private IEnumerator BurnPixel(Rigidbody rb)
    {
        GameObject go = rb.gameObject;
        Transform t = go.transform;
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        rb.isKinematic = true;
        OldPixelDespawn despawn = go.GetComponent<OldPixelDespawn>();
        if (despawn != null) despawn.enabled = false;
        roar = 1f;
        PixelAudio.Play("fire_burst", 0.9f, 0.7f);
        if (embers != null) embers.Emit(14);

        Vector3 start = t.position, startScale = t.localScale;
        Vector3 inside = transform.TransformPoint(new Vector3(1.2f, 0.55f, 0f) * unit);
        float time = 0f, burn = Burn;
        while (time < burn)
        {
            if (t == null) yield break;
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / burn);
            t.position = Vector3.Lerp(start, inside, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 2.2f))) + Random.insideUnitSphere * 0.01f * unit * k;
            t.localScale = startScale * Mathf.Lerp(1f, 0.15f, k * k);
            t.Rotate(0f, 240f * Time.deltaTime, 160f * Time.deltaTime, Space.World);
            if (embers != null && Random.value < 0.25f) embers.Emit(1);
            yield return null;
        }
        if (go != null) Destroy(go);
        PixelAudio.Play("fire_crackle", 1f, 0.6f);
        if (embers != null) embers.Emit(10);
        for (int i = 0; i < PerPixel && pendingCharcoal < MaxQueued; i++) { pendingCharcoal++; readyTimes.Add(Time.time + Delay); }
    }

    private void Update()
    {
        // The fire: it flickers, and roars up when something burns.
        roar = Mathf.MoveTowards(roar, 0f, Time.deltaTime * 0.8f);
        float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 17f) * Mathf.Sin(Time.time * 6.3f);
        if (fireLight != null) fireLight.intensity = (1.2f + roar * 2.2f) * flicker;
        if (smoke != null)
        {
            var em = smoke.emission; em.rateOverTime = 4f + roar * 14f + (pendingCharcoal > 0 ? 6f : 0f);
        }

        // Charcoal leaves the chute one at a time.
        if (pendingCharcoal > 0 && readyTimes.Count > 0 && Time.time >= readyTimes[0] && Time.time >= nextOutAt)
        {
            readyTimes.RemoveAt(0);
            pendingCharcoal--;
            nextOutAt = Time.time + Gap;
            SpitCharcoal();
        }
    }

    private void SpitCharcoal()
    {
        if (clicker == null) return;
        int tier = clicker.EnsureCharcoalTier();
        Vector3 pos = transform.TransformPoint(OutletLocal * unit);
        Vector3 dir = (-transform.right + Vector3.up * 0.15f + Random.insideUnitSphere * 0.08f).normalized;
        int before = clicker.OldPixels.Count;
        clicker.SpawnStoredPixel(tier, 1, pos, dir * Random.Range(1.2f, 1.9f) * unit);
        if (clicker.OldPixels.Count > before) PixelCouplings.Emit(this, "chute", clicker.OldPixels[clicker.OldPixels.Count - 1]);   // an Out coupling on the chute puts it on the belt there (else it just drops out)
        PixelAudio.Play("drop", 1.2f, 0.5f);
        if (embers != null) embers.Emit(3);
    }
}
