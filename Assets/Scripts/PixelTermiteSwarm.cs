// ---- System notes ----
// PixelTermiteSwarm.cs
//   The Termite Hive's termites (Woodcutting's hazard, PixelType.HornetNest keeps its old enum name so saves stay valid). Every non-final hit on the hive spills a few termites that run off (Scatter); when it GOES OFF
//   (PixelClicker.TriggerHazard) Release sends one termite for EVERY old pixel lying about (up to PixelClicker.TermiteMax, 0 = 60): they spill out of the cube, scuttle over the floor to their pixel and start eating it - a
//   small bar over the pixel counts down (PixelClicker.TermiteEatSeconds, 0 = 5). CLICK the old pixel (or grab it) before the bar runs out and the termite gives up and runs off; if the bar empties the pixel is
//   eaten: removed with no payout. A pixel that vanishes some other way (despawn, vacuum, collected) just frees its termite. Cosmetic code-built termites (body, head, legs, feelers; at most 80 at once), nothing is saved.
//   Tuning on PixelClicker: termiteSpeed (0 = 3.8 cube widths / s), termiteEatSeconds, termiteMax, disableTermites (the hive then just costs the usual XP penalty).
// ----------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelTermiteSwarm : MonoBehaviour
{
    private enum State { Hop, Run, Eat, Flee }

    private class Target
    {
        public Rigidbody rb;
        public float left, total;
        public bool eating, done;
        public GameObject bar;
        public Transform fill;
        public Material fillMat;
    }

    private class Termite
    {
        public Transform root, body;
        public Transform[] legs;
        public State state;
        public Vector3 pos, vel, heading = Vector3.forward;
        public Target target;
        public float phase, speed, timer, angle, shrink = 1f;
    }

    private const int MaxTermites = 80;
    private static int active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = 0; }

    private PixelClicker clicker;
    private float unit, floorY, half, life;
    private bool hunting;
    private Vector3 origin;
    private readonly List<Termite> termites = new List<Termite>();
    private readonly List<Target> targets = new List<Target>();
    private Material bodyMat, headMat, legMat, barBack;
    private float checkTimer;

    /// <summary>The hive went off: one termite for every old pixel. Returns how many pixels are in danger.</summary>
    public static int Release(PixelClicker clicker, Vector3 origin)
    {
        List<Rigidbody> pixels = new List<Rigidbody>();
        foreach (Rigidbody rb in clicker.OldPixels)
        {
            if (rb == null) continue;
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info == null || info.heldByPlayer) continue;
            pixels.Add(rb);
        }
        int count = Mathf.Min(pixels.Count, Mathf.Min(clicker.TermiteMax, MaxTermites - active));
        if (count <= 0) return 0;
        // With more pixels than termites, a random selection is attacked.
        for (int i = pixels.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); Rigidbody t = pixels[i]; pixels[i] = pixels[j]; pixels[j] = t; }
        PixelTermiteSwarm swarm = Create(clicker, origin, true);
        for (int i = 0; i < count; i++)
        {
            Termite t = swarm.Spawn(origin, i);
            Target target = new Target { rb = pixels[i], total = clicker.TermiteEatSeconds, left = clicker.TermiteEatSeconds };
            swarm.targets.Add(target);
            t.target = target;
        }
        return count;
    }

    /// <summary>A few termites spill out of the hive as it is hit, run off and vanish.</summary>
    public static void Scatter(PixelClicker clicker, Vector3 origin, int count)
    {
        count = Mathf.Min(count, MaxTermites - active);
        if (count <= 0) return;
        PixelTermiteSwarm swarm = Create(clicker, origin, false);
        for (int i = 0; i < count; i++) swarm.Spawn(origin, i);
    }

    private static PixelTermiteSwarm Create(PixelClicker owner, Vector3 at, bool hunt)
    {
        GameObject go = new GameObject(hunt ? "Termite Swarm" : "Termite Scatter");
        PixelTermiteSwarm s = go.AddComponent<PixelTermiteSwarm>();
        s.clicker = owner;
        s.hunting = hunt;
        s.origin = at;
        s.unit = Mathf.Max(0.01f, owner.OldPixelWorldSize);
        s.half = s.unit * 0.05f;
        s.floorY = GroundY(owner, at);
        s.bodyMat = s.Mat(new Color(0.88f, 0.78f, 0.58f, 1f));
        s.headMat = s.Mat(new Color(0.5f, 0.33f, 0.16f, 1f));
        s.legMat = s.Mat(new Color(0.62f, 0.52f, 0.36f, 1f));
        return s;
    }

    private static float GroundY(PixelClicker owner, Vector3 at)
    {
        float size = Mathf.Max(0.01f, owner.PixelBaseSize);
        float best = at.y - size * 1.5f;
        float lowest = float.MaxValue;
        RaycastHit[] hits = Physics.RaycastAll(at + Vector3.up * size, Vector3.down, size * 60f, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
            if (h.rigidbody == null && h.point.y < lowest) lowest = h.point.y;
        return lowest < float.MaxValue ? lowest : best;
    }

    private Material Mat(Color c)
    {
        Material m = clicker != null ? clicker.CreateVisualMaterial(c, false) : null;
        if (m == null)
        {
            Shader sh = PixelShaders.SpriteDefault();
            m = sh != null ? new Material(sh) : new Material(Shader.Find("Standard"));
            m.color = c;
        }
        return m;
    }

    private Transform Part(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat, Vector3 euler = default)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos * unit;
        g.transform.localScale = localScale * unit;
        g.transform.localRotation = Quaternion.Euler(euler);
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }

    private Termite Spawn(Vector3 at, int index)
    {
        GameObject root = new GameObject("Termite");
        root.transform.SetParent(transform, false);
        Termite t = new Termite { root = root.transform, state = State.Hop, phase = Random.value * 6.28f };
        t.speed = clicker.TermiteSpeed * unit * Random.Range(0.85f, 1.2f);

        t.body = Part(root.transform, "Abdomen", Vector3.zero, new Vector3(0.14f, 0.1f, 0.26f), bodyMat);
        Part(t.body, "Thorax", new Vector3(0f, 0f, 0.62f), new Vector3(0.8f, 0.85f, 0.45f), bodyMat);
        Part(root.transform, "Head", new Vector3(0f, 0.005f, 0.2f), new Vector3(0.095f, 0.085f, 0.1f), headMat);
        Part(root.transform, "Jaw L", new Vector3(-0.03f, 0f, 0.26f), new Vector3(0.015f, 0.015f, 0.06f), headMat, new Vector3(0f, 25f, 0f));
        Part(root.transform, "Jaw R", new Vector3(0.03f, 0f, 0.26f), new Vector3(0.015f, 0.015f, 0.06f), headMat, new Vector3(0f, -25f, 0f));
        t.legs = new[]
        {
            Part(root.transform, "Legs A", new Vector3(0f, -0.035f, 0.07f), new Vector3(0.34f, 0.01f, 0.02f), legMat),
            Part(root.transform, "Legs B", new Vector3(0f, -0.035f, 0f), new Vector3(0.38f, 0.01f, 0.02f), legMat),
            Part(root.transform, "Legs C", new Vector3(0f, -0.035f, -0.07f), new Vector3(0.34f, 0.01f, 0.02f), legMat),
        };
        Part(root.transform, "Feeler L", new Vector3(-0.03f, 0.05f, 0.27f), new Vector3(0.01f, 0.01f, 0.13f), legMat, new Vector3(-15f, -22f, 0f));
        Part(root.transform, "Feeler R", new Vector3(0.03f, 0.05f, 0.27f), new Vector3(0.01f, 0.01f, 0.13f), legMat, new Vector3(-15f, 22f, 0f));

        Vector2 flat = Random.insideUnitCircle.normalized;
        if (flat.sqrMagnitude < 0.01f) flat = Vector2.right;
        t.heading = new Vector3(flat.x, 0f, flat.y);
        t.pos = at + new Vector3(flat.x, 0f, flat.y) * unit * 0.2f;
        t.vel = new Vector3(flat.x, 0f, flat.y) * unit * Random.Range(1.5f, 3.5f) + Vector3.up * unit * Random.Range(3f, 5.5f);
        t.timer = index * 0.02f;
        termites.Add(t);
        active++;
        Apply(t);
        return t;
    }

    private void Apply(Termite t)
    {
        if (t.root == null) return;
        t.root.position = t.pos;
        if (t.heading.sqrMagnitude > 0.0001f) t.root.rotation = Quaternion.LookRotation(t.heading, Vector3.up);
        t.root.localScale = Vector3.one * t.shrink;
    }

    private void StartFlee(Termite t)
    {
        t.state = State.Flee;
        t.timer = 0f;
        t.target = null;
        Vector3 away = t.pos - origin;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
        t.heading = Quaternion.AngleAxis(Random.Range(-60f, 60f), Vector3.up) * away.normalized;
    }

    // ---- the countdown bar over a pixel ----

    private void ShowBar(Target tg)
    {
        if (tg.bar != null) return;
        tg.bar = new GameObject("Termite Bar");
        tg.bar.transform.SetParent(transform, false);
        if (barBack == null) barBack = Mat(new Color(0.05f, 0.05f, 0.06f, 0.9f));
        Transform back = Quad(tg.bar.transform, "Back", new Vector3(1.16f, 0.2f, 1f), barBack);
        back.localPosition = Vector3.zero;
        tg.fillMat = Mat(Color.green);
        tg.fill = Quad(tg.bar.transform, "Fill", new Vector3(1.08f, 0.13f, 1f), tg.fillMat);
        tg.fill.localPosition = new Vector3(0f, 0f, -0.001f * unit);
    }

    private Transform Quad(Transform parent, string name, Vector3 size, Material mat)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = name;
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localScale = size * unit;
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }

    private void UpdateBar(Target tg, Camera cam)
    {
        if (tg.bar == null || tg.rb == null) return;
        float k = Mathf.Clamp01(tg.left / Mathf.Max(0.01f, tg.total));
        tg.bar.transform.position = tg.rb.position + Vector3.up * unit * 0.8f;
        if (cam != null) tg.bar.transform.rotation = cam.transform.rotation;
        float w = 1.08f * unit;
        tg.fill.localScale = new Vector3(Mathf.Max(0.001f, w * k), 0.13f * unit, 1f);
        tg.fill.localPosition = new Vector3(-(w - w * k) * 0.5f, 0f, -0.001f * unit);
        Color c = Color.Lerp(new Color(1f, 0.2f, 0.12f), new Color(0.3f, 0.95f, 0.3f), k);
        if (tg.fillMat != null) tg.fillMat.color = c;
    }

    private void EndTarget(Target tg)
    {
        tg.done = true;
        if (tg.bar != null) Destroy(tg.bar);
        if (tg.fillMat != null) Destroy(tg.fillMat);
        tg.bar = null;
        foreach (Termite t in termites) if (t.target == tg) StartFlee(t);
    }

    /// <summary>The player clicked (or picked up) the pixel: the termite gives up.</summary>
    private void Save(Target tg)
    {
        if (tg.done) return;
        EndTarget(tg);
        PixelStats.Count("termites.pixels_saved");
        PixelAudio.Play("grab");
    }

    private void Eaten(Target tg)
    {
        if (tg.done) return;
        Vector3 at = tg.rb != null ? tg.rb.position : origin;
        if (tg.rb != null)
        {
            clicker.ReleaseOldPixel(tg.rb, false);   // eaten: taken away with no payout
            Destroy(tg.rb.gameObject);
            PixelFarmFx.Break(PixelClicker.PixelType.Weevil, at, unit, new Color(0.7f, 0.55f, 0.3f, 1f));
            PixelAudio.Play("pixel_vanish");
        }
        EndTarget(tg);
        PixelStats.Count("termites.pixels_eaten");
    }

    private void CheckClicks()
    {
        if (!PixelInput.LeftPressed()) return;
        Ray ray;
        if (PixelFirstPerson.Active) { if (!PixelFirstPerson.TryGetViewRay(out ray)) return; }
        else
        {
            Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
            if (cam == null || PixelInput.PointerOverUI()) return;
            ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        }
        foreach (RaycastHit h in Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide))
        {
            if (h.rigidbody == null) continue;
            Target tg = targets.Find(x => !x.done && x.rb == h.rigidbody);
            if (tg != null) Save(tg);
        }
    }

    private void OnDestroy()
    {
        active = Mathf.Max(0, active - termites.Count);
        foreach (Target tg in targets) { if (tg.bar != null) Destroy(tg.bar); if (tg.fillMat != null) Destroy(tg.fillMat); }
        if (bodyMat != null) Destroy(bodyMat);
        if (headMat != null) Destroy(headMat);
        if (legMat != null) Destroy(legMat);
        if (barBack != null) Destroy(barBack);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        life += dt;
        float time = Time.time;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (hunting) CheckClicks();

        // Pixels that went away some other way (despawned, vacuumed, grabbed) free their termite.
        checkTimer -= dt;
        if (hunting && checkTimer <= 0f)
        {
            checkTimer = 0.4f;
            HashSet<Rigidbody> existing = new HashSet<Rigidbody>(clicker.OldPixels);
            foreach (Target tg in targets)
            {
                if (tg.done) continue;
                OldPixelInfo info = tg.rb != null ? tg.rb.GetComponent<OldPixelInfo>() : null;
                if (tg.rb == null || !existing.Contains(tg.rb)) EndTarget(tg);
                else if (info != null && info.heldByPlayer) Save(tg);   // (picked up: safe)
            }
        }

        for (int i = termites.Count - 1; i >= 0; i--)
        {
            Termite t = termites[i];
            if (t.root == null) { termites.RemoveAt(i); active = Mathf.Max(0, active - 1); continue; }
            if (life > 60f && t.state != State.Flee) StartFlee(t);

            switch (t.state)
            {
                case State.Hop:
                {
                    t.timer -= dt;
                    if (t.timer > 0f) { t.pos = origin; break; }
                    t.vel += Vector3.down * unit * 20f * dt;
                    t.pos += t.vel * dt;
                    if (t.vel.y < 0f && t.pos.y <= floorY + half)
                    {
                        t.pos.y = floorY + half;
                        if (hunting && t.target != null && !t.target.done) t.state = State.Run; else StartFlee(t);
                    }
                    Vector3 h = t.vel; h.y = 0f;
                    if (h.sqrMagnitude > 0.0001f) t.heading = h.normalized;
                    break;
                }

                case State.Run:
                {
                    Target tg = t.target;
                    if (tg == null || tg.done || tg.rb == null) { StartFlee(t); break; }
                    Vector3 to = tg.rb.position - t.pos;
                    to.y = 0f;
                    float d = to.magnitude;
                    if (d < unit * 0.55f) { t.state = State.Eat; t.angle = Mathf.Atan2(-to.z, -to.x); tg.eating = true; ShowBar(tg); break; }
                    Vector3 dir = Quaternion.AngleAxis(Mathf.Sin(time * 9f + t.phase) * 34f, Vector3.up) * (to / d);
                    t.pos += dir * t.speed * dt;
                    t.pos.y = floorY + half;
                    t.heading = dir;
                    break;
                }

                case State.Eat:
                {
                    Target tg = t.target;
                    if (tg == null || tg.done || tg.rb == null) { StartFlee(t); break; }
                    t.angle += dt * 2.4f;
                    float r = unit * 0.34f;
                    Vector3 c = tg.rb.position;
                    Vector3 want = c + new Vector3(Mathf.Cos(t.angle) * r, -unit * 0.18f, Mathf.Sin(t.angle) * r);
                    t.pos = Vector3.Lerp(t.pos, want, 1f - Mathf.Exp(-9f * dt));
                    t.heading = new Vector3(-Mathf.Cos(t.angle), 0f, -Mathf.Sin(t.angle));
                    tg.left -= dt;
                    UpdateBar(tg, cam);
                    if (tg.left <= 0f) Eaten(tg);
                    break;
                }

                case State.Flee:
                {
                    t.timer += dt;
                    t.pos += t.heading * t.speed * 1.3f * dt;
                    t.pos.y = Mathf.Lerp(t.pos.y, floorY + half, 1f - Mathf.Exp(-6f * dt));
                    if (t.timer > 1.0f) t.shrink -= dt * 2.2f;
                    if (t.shrink <= 0f)
                    {
                        Destroy(t.root.gameObject);
                        termites.RemoveAt(i);
                        active = Mathf.Max(0, active - 1);
                        continue;
                    }
                    break;
                }
            }

            for (int k = 0; k < t.legs.Length; k++)
                t.legs[k].localRotation = Quaternion.Euler(0f, Mathf.Sin(time * 40f + t.phase + k * 2f) * 20f, 0f);
            if (t.body != null) t.body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(time * 20f + t.phase)) * unit * 0.012f, 0f);
            Apply(t);
        }

        if (termites.Count == 0 && (!hunting || targets.TrueForAll(x => x.done))) Destroy(gameObject);
    }
}
