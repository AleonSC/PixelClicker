// ---- System notes (moved here from CLAUDE.md) ----
// PixelVacuumFx.cs
//   The Vacuum pixel's look (looksVersion 38 replaced the saved look; thick solid violet bevel outline (outlineThickness 0.055,
//   strength 1) so it never reads as the plain Glass pixel; the old six-faced vortex marks are no longer used by it, faceCircles /
//   faceRing still exist for other looks): PixelLook.vacuumContainer draws a clear lavender glass container - a hollow box with thin
//   walls (PixelVacuumMesh: outer skin, inner surfaces facing the hollow, a round opening (HoleRadius 0.21 of the edge, Wall 0.07) in
//   each of the FOUR side faces (+/-X, +/-Z) with a tube through each wall; the top and bottom are plain walls; PixelClicker.MeshFor
//   swaps the mesh like the jagged rock mesh) and PixelLook.windFx adds a glowing lavender lip round every opening (overlay ring) and
//   a suction wind at each one (rate ~26 per second per opening) (PixelVacuumFx.Attach: a local-space ConeVolume particle system
//   (narrow 26 degree cone, 1.05 cube-widths long) whose +Z points out of the opening: stretched streaks with a RADIAL velocity
//   towards the emitter origin (the opening's centre) so they funnel into the hole, a slight orbital curl, shrinking away as they
//   arrive, ~46 per second). Both live cube and old pixels get them (old pixels keep the sucked-up pixels floating inside the glass
//   via PixelVacuumContents).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Vacuum pixel's look: a clear glass container (a hollow box with thin walls) with a round opening in each of its FOUR side faces (never the top or bottom), a glowing
/// lavender lip round the opening and a "suction wind" outside it - streaks of air that curl in towards the opening and vanish into it.
/// <see cref="PixelVacuumMesh"/> builds the hollow mesh (swapped in for the plain cube by <c>PixelClicker.MeshFor</c> when
/// <c>PixelLook.vacuumContainer</c> is set); <see cref="PixelVacuumFx.Attach"/> adds the lip and the wind (<c>PixelLook.windFx</c>).
/// The sucked-up pixels the old Vacuum pixel carries (PixelVacuumContents) float inside the glass.
/// </summary>
public static class PixelVacuumMesh
{
    /// <summary>Radius of the opening as a fraction of the cube's edge.</summary>
    public const float HoleRadius = 0.21f;

    /// <summary>Wall thickness as a fraction of the cube's edge.</summary>
    public const float Wall = 0.07f;

    private const int Seg = 48;   // corners of the square fall on segment borders (every 7.5 degrees)
    private static readonly Dictionary<int, Mesh> cache = new Dictionary<int, Mesh>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cache.Clear(); }

    public static Mesh Get(Mesh cube)
    {
        if (cube == null) return null;
        int key = PixelObjectId.Of(cube);
        if (cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        Mesh m = Build(cube.bounds);
        cache[key] = m;
        return m;
    }

    private static Mesh Build(Bounds b)
    {
        List<Vector3> v = new List<Vector3>();
        List<Vector3> n = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> t = new List<int>();
        float h = 0.5f, hi = 0.5f - Wall, r = HoleRadius;
        Quaternion none = Quaternion.identity;

        // Top and bottom are plain walls: the outer skin and the inner surface facing the hollow.
        Quad(v, n, uv, t, new Vector3(-h, h, -h), new Vector3(h, h, -h), new Vector3(h, h, h), new Vector3(-h, h, h), Vector3.up, none);
        Quad(v, n, uv, t, new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, -h, h), new Vector3(-h, -h, h), Vector3.down, none);
        Quad(v, n, uv, t, new Vector3(-hi, hi, -hi), new Vector3(hi, hi, -hi), new Vector3(hi, hi, hi), new Vector3(-hi, hi, hi), Vector3.down, none);
        Quad(v, n, uv, t, new Vector3(-hi, -hi, -hi), new Vector3(hi, -hi, -hi), new Vector3(hi, -hi, hi), new Vector3(-hi, -hi, hi), Vector3.up, none);

        // The four SIDE walls each have a round opening (built once facing -Z and turned to each side).
        for (int side = 0; side < 4; side++)
        {
            Quaternion q = Quaternion.Euler(0f, side * 90f, 0f);
            Annulus(v, n, uv, t, -h, h, r, Vector3.back, q);          // outer skin
            Annulus(v, n, uv, t, -hi, hi, r, Vector3.forward, q);     // inner surface
            for (int i = 0; i < Seg; i++)                              // the tube through the wall
            {
                float a0 = i * Mathf.PI * 2f / Seg, a1 = (i + 1) * Mathf.PI * 2f / Seg;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, 0f), p1 = new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, 0f);
                Vector3 mid = -(p0 + p1).normalized;   // the tube's wall faces the axis
                Quad(v, n, uv, t, new Vector3(p0.x, p0.y, -h), new Vector3(p1.x, p1.y, -h), new Vector3(p1.x, p1.y, -hi), new Vector3(p0.x, p0.y, -hi), mid, q);
            }
        }

        Vector3 size = b.size, centre = b.center;
        for (int i = 0; i < v.Count; i++) v[i] = Vector3.Scale(v[i], size) + centre;
        Mesh mesh = new Mesh { name = "Vacuum Container" };
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector2 Uv(Vector3 p, Vector3 normal)
    {
        Vector3 a = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
        if (a.z >= a.x && a.z >= a.y) return new Vector2(p.x + 0.5f, p.y + 0.5f);
        if (a.x >= a.y) return new Vector2(p.z + 0.5f, p.y + 0.5f);
        return new Vector2(p.x + 0.5f, p.z + 0.5f);
    }

    private static void Quad(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Quaternion q)
    {
        a = q * a; b = q * b; c = q * c; d = q * d; normal = q * normal;
        int s = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        for (int i = 0; i < 4; i++) n.Add(normal);
        uv.Add(Uv(a, normal)); uv.Add(Uv(b, normal)); uv.Add(Uv(c, normal)); uv.Add(Uv(d, normal));
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f) { t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3); }
        else { t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2); }
    }

    /// <summary>A flat frame at height z between a square of half-extent 'half' and a round hole of radius 'r'.</summary>
    private static void Annulus(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, float z, float half, float r, Vector3 normal, Quaternion q)
    {
        for (int i = 0; i < Seg; i++)
        {
            float a0 = i * Mathf.PI * 2f / Seg, a1 = (i + 1) * Mathf.PI * 2f / Seg;
            Vector3 c0 = new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, z), c1 = new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, z);
            Vector3 s0 = Square(a0, half, z), s1 = Square(a1, half, z);
            Quad(v, n, uv, t, c0, c1, s1, s0, normal, q);
        }
    }

    private static Vector3 Square(float angle, float half, float z)
    {
        float cx = Mathf.Cos(angle), sy = Mathf.Sin(angle);
        float k = half / Mathf.Max(Mathf.Abs(cx), Mathf.Abs(sy));
        return new Vector3(cx * k, sy * k, z);
    }
}

public static class PixelVacuumFx
{
    private static Mesh lipMesh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { lipMesh = null; }

    /// <summary>Adds a glowing lip and a suction wind at each of the four side openings, under 'parent' (the cube's mesh object).</summary>
    public static void Attach(Transform parent, Vector3 centre, Vector3 size)
    {
        for (int side = 0; side < 4; side++) AttachOne(parent, centre, size, Quaternion.Euler(0f, side * 90f, 0f));
    }

    private static void AttachOne(Transform parent, Vector3 centre, Vector3 size, Quaternion q)
    {
        float unit = Mathf.Max(0.05f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        Vector3 mouth = centre + q * new Vector3(0f, 0f, -size.z * 0.5f);
        float hole = Mathf.Min(size.x, size.y) * PixelVacuumMesh.HoleRadius;

        // The lip: a bright lavender ring that fades outwards, lying just outside the face.
        GameObject lip = new GameObject("Opening Lip", typeof(MeshFilter), typeof(MeshRenderer));
        lip.transform.SetParent(parent, false);
        lip.transform.localPosition = mouth + q * new Vector3(0f, 0f, -size.z * 0.004f);
        lip.transform.localRotation = q;
        lip.layer = parent.gameObject.layer;
        lip.GetComponent<MeshFilter>().sharedMesh = LipMesh(hole);
        MeshRenderer lr = lip.GetComponent<MeshRenderer>();
        lr.sharedMaterial = PixelLooks.OverlayMaterial();
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        if (PixelFx.Material == null) return;
        // The wind: streaks born inside a narrow cone in front of the opening that all fly straight at the opening's centre (radial velocity towards the
        // emitter's origin = a funnel), curling a little as they go, and shrink away as they reach it. The emitter's +Z points out of the opening.
        ParticleSystem wind = PixelFx.Make(parent, "Suction Wind", new PixelFx.Spec
        {
            lifetime = new Vector2(0.16f, 0.4f), speed = Vector2.zero, size = new Vector2(0.02f, 0.04f) * unit, gravity = 0f,
            colorA = new Color(0.92f, 0.88f, 1f, 0.9f), colorB = new Color(0.62f, 0.48f, 1f, 0.8f),
            rate = 26f, loop = true, localSpace = true, shape = ParticleSystemShapeType.ConeVolume, shapeRadius = hole * 0.5f, shapeAngle = 26f,
            sizeOverLife = new Vector2(1f, 0.1f), maxParticles = 40,
            overLife = PixelFx.FadeGradient(Color.white, new Color(0.9f, 0.85f, 1f), new Color(0.6f, 0.5f, 1f), 0.9f),
        });
        Transform wt = wind.transform;
        wt.localPosition = mouth;
        wt.localRotation = q * Quaternion.LookRotation(Vector3.back, Vector3.up);
        ParticleSystem.ShapeModule shape = wind.shape;
        shape.length = unit * 1.05f;
        ParticleSystem.VelocityOverLifetimeModule vol = wind.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.Local;
        vol.x = new ParticleSystem.MinMaxCurve(0f);
        vol.y = new ParticleSystem.MinMaxCurve(0f);
        vol.z = new ParticleSystem.MinMaxCurve(0f);
        vol.radial = new ParticleSystem.MinMaxCurve(-unit * 2.6f);     // straight at the opening: a funnel
        vol.orbitalZ = new ParticleSystem.MinMaxCurve(2.2f);           // with a slight curl round it
        ParticleSystemRenderer pr = wind.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.1f;
        pr.lengthScale = 1.2f;
        wind.Play();
    }

    private static Mesh LipMesh(float hole)
    {
        if (lipMesh != null) return lipMesh;
        const int seg = 40;
        float[] rr = { 0.9f, 1f, 1.25f, 1.55f };
        Color[] cc =
        {
            new Color(0.12f, 0.03f, 0.22f, 0.9f),
            new Color(0.75f, 0.55f, 1f, 0.95f),
            new Color(0.7f, 0.5f, 1f, 0.4f),
            new Color(0.7f, 0.5f, 1f, 0f),
        };
        List<Vector3> v = new List<Vector3>();
        List<Color> c = new List<Color>();
        List<int> t = new List<int>();
        for (int r = 0; r < rr.Length; r++)
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                v.Add(new Vector3(Mathf.Cos(a) * hole * rr[r], Mathf.Sin(a) * hole * rr[r], 0f));
                c.Add(cc[r]);
            }
        for (int r = 0; r < rr.Length - 1; r++)
            for (int i = 0; i < seg; i++)
            {
                int n = (i + 1) % seg;
                int a = r * seg + i, b = r * seg + n, d = (r + 1) * seg + i, e = (r + 1) * seg + n;
                t.Add(a); t.Add(d); t.Add(e); t.Add(a); t.Add(e); t.Add(b);
                t.Add(a); t.Add(e); t.Add(d); t.Add(a); t.Add(b); t.Add(e);   // double-sided
            }
        lipMesh = new Mesh { name = "Vacuum Lip" };
        lipMesh.SetVertices(v);
        lipMesh.SetColors(c);
        lipMesh.SetTriangles(t, 0);
        lipMesh.RecalculateBounds();
        return lipMesh;
    }
}
