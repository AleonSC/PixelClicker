// ---- System notes (moved here from CLAUDE.md) ----
// PixelPestSwarm.cs (Pest rework)
//   The Pest block (PixelLook.blockSurface Pest, PixelBlockTextures.PestHide(level,max): black carapace plates, hazard stripes round
//   every face, red eyes that multiply and glow with hits; jagged mesh + twitching wobble, bugs crawl over it via farmFx Bugs;
//   looksVersion 43). Every non-final hit spills a few bugs (PixelPestSwarm.Scatter); when it GOES OFF PixelPestSwarm.Release sends a
//   swarm of code-built bugs (body, head, red eyes, legs, feelers; max 80 at once) over the floor to every edible planted sprout
//   (SeedSprout.GetEdible/IsEdible), they climb it and gnaw (Gnaw shakes it, more bugs = faster, pestChewSeconds 4), then
//   SeedSprout.Devour() kills it (pixel lost) and the bugs run off; with no sprouts they just scatter. Tuning on PixelClicker:
//   pestBugsPerSprout (0 = 6), pestBugsMin (8), pestBugSpeed (3.5), pestChewSeconds (4), disablePestSwarm (old instant KillAll). Dirt
//   now takes 3 digs (packDataVersion 15): each hit plays seed_dig + SeedDigFx and shrinks the live cube towards old-pixel size
//   (digShrinkTarget, reset in Materialize). Seedlings from a cracked Seed shell grow seedlingGrowSeconds (0 = 40 s; the sprout's
//   despawn timer is extended to match). Migration 15 also adds any missing farm block (Seed, Water...) to a saved Farmer's Pack, and
//   PixelSkills.EnsureDefaults makes sure Farming's types list has every farm block (a missing Seed was why it never spawned);
//   migrations 13 and 14 now run in order.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Pest's bugs. When a Pest block goes off (<see cref="Release"/>) a swarm of little code-built bugs spills out of the cube, scuttles across the
/// floor to the sprouts you have planted and climbs them; the more bugs on a sprout, the faster it is gnawed through (it shakes while they chew), then
/// it is eaten (<see cref="SeedSprout.Devour"/>: its pixel is lost) and the bugs run off and shrink away. With nothing to eat they just scatter.
/// Every non-final hit on a Pest also spills a few bugs that run off (<see cref="Scatter"/>) as a warning. Bugs are cosmetic apart from the eating;
/// at most <see cref="MaxBugs"/> exist at once. Tuning lives on <see cref="PixelClicker"/> (pestBugsPerSprout, pestBugsMin, pestBugSpeed, pestChewSeconds).
/// </summary>
public class PixelPestSwarm : MonoBehaviour
{
    private enum State { Hop, Run, Chew, Flee }

    private class Bug
    {
        public Transform root;
        public Transform body;
        public Transform[] legs;
        public State state;
        public Vector3 pos, vel, heading = Vector3.forward;
        public SeedSprout target;
        public float phase, speed, timer, angle, climb, shrink = 1f;
    }

    private const int MaxBugs = 80;
    private static int active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = 0; }

    private PixelClicker clicker;
    private float unit, floorY, life, bugHalf;
    private bool hunting;   // a real swarm (goes for the sprouts) rather than a few bugs scattering
    private Vector3 origin;
    private readonly List<Bug> bugs = new List<Bug>();
    private readonly Dictionary<SeedSprout, float> progress = new Dictionary<SeedSprout, float>();
    private readonly List<SeedSprout> scratch = new List<SeedSprout>();
    private Material darkMat, redMat, legMat;

    /// <summary>A Pest went off: a swarm heads for every sprout. Returns how many sprouts are in danger.</summary>
    public static int Release(PixelClicker clicker, Vector3 origin)
    {
        List<SeedSprout> edible = new List<SeedSprout>();
        SeedSprout.GetEdible(edible);
        int wanted = Mathf.Max(clicker.PestBugsMin, edible.Count * clicker.PestBugsPerSprout);
        int count = Mathf.Min(wanted, MaxBugs - active);
        if (count > 0)
        {
            PixelPestSwarm swarm = Create(clicker, origin, true);
            for (int i = 0; i < count; i++)
            {
                Bug b = swarm.Spawn(origin, i);
                b.target = edible.Count > 0 ? edible[i % edible.Count] : null;
            }
        }
        return edible.Count;
    }

    /// <summary>A few bugs spill out of the Pest as it is hit, run off and vanish.</summary>
    public static void Scatter(PixelClicker clicker, Vector3 origin, int count)
    {
        count = Mathf.Min(count, MaxBugs - active);
        if (count <= 0) return;
        PixelPestSwarm swarm = Create(clicker, origin, false);
        for (int i = 0; i < count; i++) swarm.Spawn(origin, i);
    }

    private static PixelPestSwarm Create(PixelClicker owner, Vector3 at, bool hunt)
    {
        GameObject go = new GameObject(hunt ? "Pest Swarm" : "Pest Scatter");
        PixelPestSwarm s = go.AddComponent<PixelPestSwarm>();
        s.clicker = owner;
        s.hunting = hunt;
        s.origin = at;
        s.unit = Mathf.Max(0.01f, owner.OldPixelWorldSize);
        s.bugHalf = s.unit * 0.06f;
        s.floorY = GroundY(owner, at);
        s.darkMat = s.Mat(new Color(0.07f, 0.09f, 0.04f, 1f));
        s.redMat = s.Mat(new Color(1f, 0.15f, 0.05f, 1f));
        s.legMat = s.Mat(new Color(0.18f, 0.16f, 0.06f, 1f));
        return s;
    }

    /// <summary>The floor under a point: the lowest surface that is not a rigid body (the cube and old pixels are higher).</summary>
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

    private Bug Spawn(Vector3 at, int index)
    {
        GameObject root = new GameObject("Pest Bug");
        root.transform.SetParent(transform, false);
        Bug b = new Bug { root = root.transform, state = State.Hop, phase = Random.value * 6.28f };
        b.speed = clicker.PestBugSpeed * unit * Random.Range(0.8f, 1.25f);

        b.body = Part(root.transform, "Body", Vector3.zero, new Vector3(0.17f, 0.11f, 0.30f), darkMat);
        Part(b.body, "Head", new Vector3(0f, 0.1f, 0.62f), new Vector3(0.7f, 0.8f, 0.34f), darkMat);
        Part(root.transform, "Eye L", new Vector3(-0.04f, 0.045f, 0.255f), Vector3.one * 0.035f, redMat);
        Part(root.transform, "Eye R", new Vector3(0.04f, 0.045f, 0.255f), Vector3.one * 0.035f, redMat);
        b.legs = new[]
        {
            Part(root.transform, "Legs A", new Vector3(0f, -0.04f, 0.1f), new Vector3(0.40f, 0.012f, 0.025f), legMat),
            Part(root.transform, "Legs B", new Vector3(0f, -0.04f, 0f), new Vector3(0.44f, 0.012f, 0.025f), legMat),
            Part(root.transform, "Legs C", new Vector3(0f, -0.04f, -0.1f), new Vector3(0.40f, 0.012f, 0.025f), legMat),
        };
        Part(root.transform, "Feeler L", new Vector3(-0.045f, 0.075f, 0.34f), new Vector3(0.012f, 0.012f, 0.16f), legMat, new Vector3(-18f, -25f, 0f));
        Part(root.transform, "Feeler R", new Vector3(0.045f, 0.075f, 0.34f), new Vector3(0.012f, 0.012f, 0.16f), legMat, new Vector3(-18f, 25f, 0f));

        // Spill out of the cube in a random direction with a little hop.
        Vector2 flat = Random.insideUnitCircle.normalized;
        if (flat.sqrMagnitude < 0.01f) flat = Vector2.right;
        b.heading = new Vector3(flat.x, 0f, flat.y);
        b.pos = at + new Vector3(flat.x, 0f, flat.y) * unit * 0.2f;
        b.vel = new Vector3(flat.x, 0f, flat.y) * unit * Random.Range(1.5f, 3.5f) + Vector3.up * unit * Random.Range(3f, 5.5f);
        b.timer = index * 0.02f;
        bugs.Add(b);
        active++;
        Apply(b);
        return b;
    }

    private void OnDestroy()
    {
        active = Mathf.Max(0, active - bugs.Count);
        foreach (KeyValuePair<SeedSprout, float> p in progress) if (p.Key != null) p.Key.Gnaw(0f);
        if (darkMat != null) Destroy(darkMat);
        if (redMat != null) Destroy(redMat);
        if (legMat != null) Destroy(legMat);
    }

    private SeedSprout NearestEdible(Vector3 from)
    {
        scratch.Clear();
        SeedSprout.GetEdible(scratch);
        SeedSprout best = null;
        float bestSq = float.MaxValue;
        foreach (SeedSprout s in scratch)
        {
            float sq = (s.transform.position - from).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = s; }
        }
        return best;
    }

    private void StartFlee(Bug b)
    {
        b.state = State.Flee;
        b.timer = 0f;
        Vector3 away = b.pos - origin;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
        b.heading = Quaternion.AngleAxis(Random.Range(-50f, 50f), Vector3.up) * away.normalized;
    }

    private void Apply(Bug b)
    {
        if (b.root == null) return;
        b.root.position = b.pos;
        if (b.heading.sqrMagnitude > 0.0001f) b.root.rotation = Quaternion.LookRotation(b.heading, Vector3.up);
        b.root.localScale = Vector3.one * b.shrink;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        life += dt;
        float time = Time.time;
        float chew = clicker.PestChewSeconds;
        Dictionary<SeedSprout, int> chewers = null;

        for (int i = bugs.Count - 1; i >= 0; i--)
        {
            Bug b = bugs[i];
            if (b.root == null) { bugs.RemoveAt(i); active = Mathf.Max(0, active - 1); continue; }
            if (life > 30f && b.state != State.Flee) StartFlee(b);

            switch (b.state)
            {
                case State.Hop:
                {
                    b.timer -= dt;
                    if (b.timer > 0f) { b.pos = origin; break; }
                    b.vel += Vector3.down * unit * 20f * dt;
                    b.pos += b.vel * dt;
                    if (b.vel.y < 0f && b.pos.y <= floorY + bugHalf)
                    {
                        b.pos.y = floorY + bugHalf;
                        if (hunting && (b.target != null || NearestEdible(b.pos) != null)) b.state = State.Run;
                        else StartFlee(b);
                    }
                    if (b.vel.sqrMagnitude > 0.01f) { Vector3 h = b.vel; h.y = 0f; if (h.sqrMagnitude > 0.0001f) b.heading = h.normalized; }
                    break;
                }

                case State.Run:
                {
                    if (b.target == null || !b.target.IsEdible) b.target = NearestEdible(b.pos);
                    if (b.target == null) { StartFlee(b); break; }
                    Vector3 to = b.target.transform.position - b.pos;
                    to.y = 0f;
                    float d = to.magnitude;
                    if (d < unit * 0.55f)
                    {
                        b.state = State.Chew;
                        b.angle = Mathf.Atan2(-to.z, -to.x);
                        b.climb = 0f;
                        break;
                    }
                    Vector3 dir = Quaternion.AngleAxis(Mathf.Sin(time * 9f + b.phase) * 38f, Vector3.up) * (to / d);   // a scuttling zig-zag
                    b.pos += dir * b.speed * dt;
                    b.pos.y = floorY + bugHalf;
                    b.heading = dir;
                    break;
                }

                case State.Chew:
                {
                    SeedSprout t = b.target;
                    if (t == null || !t.IsEdible) { b.target = null; StartFlee(b); break; }
                    b.angle += dt * 3.5f;
                    b.climb = Mathf.MoveTowards(b.climb, 1f, dt * 1.4f);
                    float r = unit * 0.3f;
                    Vector3 c = t.transform.position;
                    Vector3 p = c + new Vector3(Mathf.Cos(b.angle) * r, 0f, Mathf.Sin(b.angle) * r);
                    p.y = floorY + bugHalf + b.climb * t.StemHeight * 0.75f;
                    Vector3 tangent = new Vector3(-Mathf.Sin(b.angle), 0f, Mathf.Cos(b.angle));
                    b.pos = p;
                    b.heading = tangent;
                    if (chewers == null) chewers = new Dictionary<SeedSprout, int>();
                    chewers.TryGetValue(t, out int n);
                    chewers[t] = n + 1;
                    break;
                }

                case State.Flee:
                {
                    b.timer += dt;
                    b.pos += b.heading * b.speed * 1.3f * dt;
                    b.pos.y = floorY + bugHalf;
                    if (b.timer > 1.1f) b.shrink -= dt * 2.2f;
                    if (b.shrink <= 0f)
                    {
                        Destroy(b.root.gameObject);
                        bugs.RemoveAt(i);
                        active = Mathf.Max(0, active - 1);
                        continue;
                    }
                    break;
                }
            }

            // Scuttling legs and a little body jitter.
            for (int k = 0; k < b.legs.Length; k++)
                b.legs[k].localRotation = Quaternion.Euler(0f, Mathf.Sin(time * 42f + b.phase + k * 2f) * 20f, 0f);
            if (b.body != null) b.body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(time * 21f + b.phase)) * unit * 0.012f, 0f);
            Apply(b);
        }

        // Gnawing: every sprout with bugs on it takes damage faster the more bugs there are; it is eaten when it has had enough.
        if (chewers != null)
        {
            foreach (KeyValuePair<SeedSprout, int> kv in chewers)
            {
                SeedSprout t = kv.Key;
                if (t == null) continue;
                progress.TryGetValue(t, out float done);
                done += dt * (1f + 0.35f * (kv.Value - 1));
                progress[t] = done;
                t.Gnaw(Mathf.Clamp01(done / Mathf.Max(0.5f, chew)));
                if (done < chew) continue;

                Vector3 at = t.transform.position + Vector3.up * t.StemHeight * 0.5f;
                t.Gnaw(0f);
                t.Devour();
                PixelFarmFx.Break(PixelClicker.PixelType.Weevil, at, unit, new Color(0.3f, 0.4f, 0.1f, 1f));
                PixelStats.Count("pests.sprouts_eaten");
                progress.Remove(t);
                foreach (Bug b in bugs) if (b.target == t) { b.target = null; StartFlee(b); }
                break;   // (the dictionary was iterated: the rest are handled next frame)
            }
        }

        if (bugs.Count == 0) Destroy(gameObject);
    }
}
