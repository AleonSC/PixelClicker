using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static PixelInput;

/// <summary>
/// A placed sorter. Created and configured by <see cref="PixelConsumables"/>.
///
/// It is a ring around the clicker cube with a bendable output pipe sticking out of one side (everything faces the
/// camera; the pipe is aimed and bent while placing). While it works, every pixel you collect is not thrown out in a
/// random direction but spat out of the end of the pipe in a stream. Three buttons on the ring (click them) set how
/// hard the pixels are spat out. The countdown, timer text and shrink-away come from <see cref="PixelPlacedDevice"/>.
/// </summary>
public class PixelSorterDevice : PixelPlacedDevice
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        Current = null;
    }

    /// <summary>The sorter that is currently working (null = none). <see cref="PixelClicker"/> asks it where to send each pixel.</summary>
    public static PixelSorterDevice Current { get; private set; }

    private Parts parts;
    private PixelConsumables.Device settings;
    private float aimDegrees;
    private float bendDegrees;
    private int force = 1;

    /// <summary>Called by the consumables script after it built the sorter.</summary>
    /// <summary>Where the pipe points (degrees around the ring, 0 = right on screen).</summary>
    public float AimDegrees => aimDegrees;

    /// <summary>How far the pipe is bent (degrees).</summary>
    public float BendDegrees => bendDegrees;

    /// <summary>Which force button is selected (0 = the first).</summary>
    public int Force => force;

    public void Init(PixelClicker owner, Camera camera, Parts builtParts, PixelConsumables.Device device, TextMeshPro timer,
                     string format, float duration, float aim, float bend, int startForce, float shrinkTime)
    {
        InitCommon(owner, camera, timer, format, duration, shrinkTime);
        parts = builtParts;
        settings = device;
        aimDegrees = aim;
        bendDegrees = bend;
        force = Mathf.Clamp(startForce < 0 ? device.sorterDefaultForce : startForce, 0, Forces(device).Length - 1);
        parts.Apply(aim, bend, false, force);
        Current = this;
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    protected override void OnTick()
    {
        if (parts == null || cam == null) return;
        if (PixelPauseMenu.IsPaused || Time.timeScale <= 0f || !LeftPressed() || PointerOverUI()) return;

        // Clicking one of the three buttons sets the force.
        Vector2 pointer = PointerPosition();
        for (int i = 0; i < parts.Buttons.Length; i++)
        {
            Transform b = parts.Buttons[i];
            Vector3 screen = cam.WorldToScreenPoint(b.position);
            if (screen.z <= 0f) continue;
            Vector3 edge = cam.WorldToScreenPoint(b.position + cam.transform.right * (settings.sorterButtonSize * 0.5f));
            float radius = Mathf.Max(22f, Mathf.Abs(edge.x - screen.x) * 1.4f); // a forgiving click area
            if (((Vector2)screen - pointer).sqrMagnitude <= radius * radius)
            {
                force = i;
                parts.Apply(aimDegrees, bendDegrees, false, force);
                PixelAudio.Play("sorter_button");
                return;
            }
        }
    }

    /// <summary>The sorter wraps round the cube, so only its ring and pipe count as 'on' the device (clicking the cube inside must not switch it on).</summary>
    public override bool HitTest(Ray ray, out float distance)
    {
        distance = float.MaxValue;
        if (parts == null) return false;
        bool hit = false;
        Renderer pipe = parts.PipeRenderer;
        if (pipe != null && pipe.bounds.IntersectRay(ray, out float pd)) { distance = pd; hit = true; }
        Camera c = cam != null ? cam : Camera.main;
        if (c != null)
        {
            Plane plane = new Plane(-c.transform.forward, transform.position);
            if (plane.Raycast(ray, out float e))
            {
                float d = Vector3.Distance(ray.GetPoint(e), transform.position);
                if (Mathf.Abs(d - parts.Outer) < parts.Outer * 0.22f && e < distance) { distance = e; hit = true; }
            }
        }
        return hit;
    }

    /// <summary>
    /// Where a freshly collected pixel should appear (the mouth of the pipe) and how fast it leaves.
    /// Returns false when the sorter is shutting down.
    /// </summary>
    public bool TryRoute(out Vector3 mouth, out Vector3 velocity)
    {
        mouth = transform.position;
        velocity = Vector3.zero;
        if (IsDying || !Armed || parts == null) return false;

        mouth = transform.TransformPoint(parts.MouthLocal);
        Vector3 direction = transform.TransformDirection(new Vector3(Mathf.Cos(parts.ExitHeading * Mathf.Deg2Rad),
                                                                     Mathf.Sin(parts.ExitHeading * Mathf.Deg2Rad), 0f));
        Quaternion wobble = Quaternion.AngleAxis(Random.Range(-settings.sorterSpreadDegrees, settings.sorterSpreadDegrees), transform.forward);
        float[] forces = Forces(settings);
        float multiplier = forces[Mathf.Clamp(force, 0, forces.Length - 1)];
        velocity = wobble * direction * (settings.sorterExitSpeed * multiplier);
        return true;
    }

    // ------------------------------------------------------------------
    // Building
    // ------------------------------------------------------------------

    /// <summary>The pieces of a sorter (ring, bent pipe, lip, three buttons, and in the preview a cone) and how to reshape them.</summary>
    public class Parts
    {
        public GameObject Root;
        public Transform[] Buttons;

        private PixelConsumables.Device d;
        private float outer;
        private Transform cone;
        private MeshFilter pipeFilter;
        private Transform lip;
        private Renderer[] buttonRenderers;
        private Transform[] buttonLabels;
        private Material dimMaterial, litMaterial;
        private Mesh coneMesh;

        /// <summary>The renderer of the bent pipe.</summary>
        public Renderer PipeRenderer => pipeFilter != null ? pipeFilter.GetComponent<Renderer>() : null;

        /// <summary>Outer radius of the ring.</summary>
        public float Outer => outer;

        /// <summary>Local position of the pipe's mouth (set by <see cref="Apply"/>).</summary>
        public Vector3 MouthLocal { get; private set; }

        /// <summary>Direction (degrees, 0 = right on screen) the pixels leave the mouth.</summary>
        public float ExitHeading { get; private set; }

        /// <summary>Where the pipe starts: a touch inside the ring's outer edge so there is no gap.</summary>
        public float PipeStart => outer - 0.05f;

        /// <summary>Apex of the bend cone (the pipe's starting point) in local space.</summary>
        public Vector2 ConeApex(float aim) => Polar(PipeStart, aim);

        public static Parts Create(PixelClicker clicker, PixelConsumables.Device device, Camera cam, bool isPreview, float previewOpacity)
        {
            Parts p = new Parts { d = device };
            p.Root = new GameObject(isPreview ? device.displayName + " (Preview)" : device.displayName);
            Vector3 center = clicker.PixelTransform != null ? clicker.PixelTransform.position : Vector3.zero;
            p.Root.transform.SetPositionAndRotation(center, cam != null ? cam.transform.rotation : Quaternion.identity);

            float thickness = device.sorterRingThickness;
            p.outer = clicker.PixelBaseSize * 0.5f * 1.42f + device.sorterRingPadding + thickness;
            float inner = p.outer - thickness;

            float opacity = isPreview ? previewOpacity : 1f;
            Color body = device.color; body.a = opacity;
            Color dark = new Color(device.color.r * 0.45f, device.color.g * 0.45f, device.color.b * 0.45f, opacity);
            Color lit = Color.Lerp(device.color, Color.white, 0.55f); lit.a = opacity;

            // Ring.
            GameObject ring = new GameObject("Ring", typeof(MeshFilter), typeof(MeshRenderer));
            ring.transform.SetParent(p.Root.transform, false);
            ring.GetComponent<MeshFilter>().sharedMesh = BuildRingMesh(inner, p.outer, thickness, 48);
            Material ringMat = clicker.CreateVisualMaterial(body, isPreview);
            if (ringMat != null) ring.GetComponent<MeshRenderer>().sharedMaterial = ringMat;

            // Bent pipe (its mesh is rebuilt by Apply).
            GameObject pipe = new GameObject("Pipe", typeof(MeshFilter), typeof(MeshRenderer));
            pipe.transform.SetParent(p.Root.transform, false);
            p.pipeFilter = pipe.GetComponent<MeshFilter>();
            if (ringMat != null) pipe.GetComponent<MeshRenderer>().sharedMaterial = ringMat;

            // Darker lip at the mouth.
            p.lip = MakePrimitive(clicker, PrimitiveType.Cylinder, "Lip", p.Root.transform, dark, isPreview).transform;
            p.lip.localScale = new Vector3(device.sorterPipeDiameter * 1.25f, 0.06f, device.sorterPipeDiameter * 1.25f);

            // Three force buttons (small pucks on the ring facing the camera), brighter when selected.
            int count = Forces(device).Length;
            p.Buttons = new Transform[count];
            p.buttonRenderers = new Renderer[count];
            p.buttonLabels = new Transform[count];
            p.dimMaterial = clicker.CreateVisualMaterial(dark, isPreview);
            p.litMaterial = clicker.CreateVisualMaterial(lit, isPreview);
            for (int i = 0; i < count; i++)
            {
                GameObject b = MakePrimitive(clicker, PrimitiveType.Cylinder, "Force Button " + (i + 1), p.Root.transform, dark, isPreview);
                b.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // flat face towards the camera
                b.transform.localScale = new Vector3(device.sorterButtonSize, 0.05f, device.sorterButtonSize);
                p.Buttons[i] = b.transform;
                p.buttonRenderers[i] = b.GetComponent<Renderer>();

                TextMeshPro label = new GameObject("Label").AddComponent<TextMeshPro>();
                label.transform.SetParent(p.Root.transform, false);
                label.text = (i + 1).ToString();
                label.fontSize = device.sorterButtonSize * 8f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                if (clicker.UIFont != null) label.font = clicker.UIFont;
                label.rectTransform.sizeDelta = new Vector2(device.sorterButtonSize * 2f, device.sorterButtonSize);
                p.buttonLabels[i] = label.transform;
            }

            // The cone you click inside to bend the pipe (preview only).
            if (isPreview)
            {
                GameObject c = new GameObject("Bend Cone", typeof(MeshFilter), typeof(MeshRenderer));
                c.transform.SetParent(p.Root.transform, false);
                p.cone = c.transform;
                Color coneColor = device.color; coneColor.a = 0.22f;
                Material coneMat = clicker.CreateVisualMaterial(coneColor, true);
                if (coneMat != null) c.GetComponent<MeshRenderer>().sharedMaterial = coneMat;
                p.coneMesh = new Mesh { name = "Sorter Cone" };
                c.GetComponent<MeshFilter>().sharedMesh = p.coneMesh;
            }
            return p;
        }

        /// <summary>Re-shapes the pipe, lip, buttons and cone for the given aim and bend (degrees).</summary>
        public void Apply(float aim, float bend, bool showCone, int selectedForce)
        {
            // Pipe: a constant-curvature arc that turns by 'bend' degrees over its length.
            const int samples = 20;
            Vector3[] pts = new Vector3[samples + 1];
            float[] headings = new float[samples + 1];
            float step = d.sorterPipeLength / samples;
            Vector2 pos = Polar(PipeStart, aim);
            pts[0] = pos; headings[0] = aim;
            for (int i = 0; i < samples; i++)
            {
                float mid = aim + bend * (i + 0.5f) / samples;
                pos += new Vector2(Mathf.Cos(mid * Mathf.Deg2Rad), Mathf.Sin(mid * Mathf.Deg2Rad)) * step;
                pts[i + 1] = pos;
                headings[i + 1] = aim + bend * (i + 1f) / samples;
            }
            pipeFilter.sharedMesh = BuildPipeMesh(pts, headings, d.sorterPipeDiameter * 0.5f, 12, pipeFilter.sharedMesh);

            MouthLocal = pts[samples];
            ExitHeading = aim + bend;
            lip.localPosition = MouthLocal;
            lip.localRotation = Quaternion.Euler(0f, 0f, ExitHeading - 90f);

            // Buttons sit on the ring's rim, on the side opposite the pipe.
            for (int i = 0; i < Buttons.Length; i++)
            {
                float angle = aim + 180f + (i - (Buttons.Length - 1) * 0.5f) * d.sorterButtonSpacing;
                Vector2 at = Polar(outer - d.sorterRingThickness * 0.5f, angle);
                Buttons[i].localPosition = new Vector3(at.x, at.y, -(d.sorterRingThickness * 0.5f + 0.02f));
                buttonLabels[i].localPosition = Buttons[i].localPosition + new Vector3(0f, 0f, -0.04f);
                buttonLabels[i].localRotation = Quaternion.identity;
                if (buttonRenderers[i] != null)
                {
                    Material m = i == selectedForce ? litMaterial : dimMaterial;
                    if (m != null) buttonRenderers[i].sharedMaterial = m;
                }
            }

            if (cone != null)
            {
                cone.gameObject.SetActive(showCone);
                if (showCone) BuildConeMesh(coneMesh, ConeApex(aim), aim, d.sorterBendConeDegrees, d.sorterConeLength);
            }
        }

        private static GameObject MakePrimitive(PixelClicker clicker, PrimitiveType type, string name, Transform parent, Color color, bool transparent)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            Material mat = clicker.CreateVisualMaterial(color, transparent);
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            else go.GetComponent<Renderer>().material.color = color;
            return go;
        }
    }

    private static readonly float[] DefaultForces = { 0.5f, 1f, 1.8f };

    /// <summary>The force multipliers of the buttons (falls back to three defaults if the list is empty).</summary>
    private static float[] Forces(PixelConsumables.Device device) =>
        device.sorterForceMultipliers != null && device.sorterForceMultipliers.Length > 0 ? device.sorterForceMultipliers : DefaultForces;

    private static Vector2 Polar(float radius, float degrees) =>
        new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * radius;

    // ------------------------------------------------------------------
    // Meshes
    // ------------------------------------------------------------------

    private class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        /// <summary>A flat triangle that faces 'outward' (flips itself if the winding is the wrong way round).</summary>
        public void Flat(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, outward) < 0f) { Vector3 t = b; b = c; c = t; n = -n; }
            Add(a, b, c, n.normalized, n.normalized, n.normalized);
        }

        /// <summary>A smooth triangle with per-corner normals (winding fixed to match them).</summary>
        public void Smooth(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, na + nb + nc) < 0f) { Vector3 t = b; b = c; c = t; t = nb; nb = nc; nc = t; }
            Add(a, b, c, na, nb, nc);
        }

        private void Add(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(na); normals.Add(nb); normals.Add(nc);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }

        public Mesh ToMesh(Mesh into, string name)
        {
            Mesh mesh = into != null ? into : new Mesh { name = name };
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Flat ring (annulus) in the local XY plane, 'depth' thick along Z.</summary>
    public static Mesh BuildRingMesh(float innerRadius, float outerRadius, float depth, int segments)
    {
        MeshBuilder m = new MeshBuilder();
        float h = depth * 0.5f;

        for (int i = 0; i < segments; i++)
        {
            float a0 = Mathf.PI * 2f * i / segments;
            float a1 = Mathf.PI * 2f * (i + 1) / segments;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
            Vector3 d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
            Vector3 mid = (d0 + d1).normalized;

            Vector3 oF0 = d0 * outerRadius + Vector3.back * h, oF1 = d1 * outerRadius + Vector3.back * h;
            Vector3 iF0 = d0 * innerRadius + Vector3.back * h, iF1 = d1 * innerRadius + Vector3.back * h;
            Vector3 oB0 = d0 * outerRadius + Vector3.forward * h, oB1 = d1 * outerRadius + Vector3.forward * h;
            Vector3 iB0 = d0 * innerRadius + Vector3.forward * h, iB1 = d1 * innerRadius + Vector3.forward * h;

            // Front (towards the camera is -Z), back, outer wall, inner wall.
            m.Flat(oF0, oF1, iF1, Vector3.back); m.Flat(oF0, iF1, iF0, Vector3.back);
            m.Flat(oB0, oB1, iB1, Vector3.forward); m.Flat(oB0, iB1, iB0, Vector3.forward);
            m.Flat(oF0, oF1, oB1, mid); m.Flat(oF0, oB1, oB0, mid);
            m.Flat(iF0, iF1, iB1, -mid); m.Flat(iF0, iB1, iB0, -mid);
        }
        return m.ToMesh(null, "Sorter Ring");
    }

    /// <summary>A round tube along a path in the XY plane (points + the heading in degrees at each point).</summary>
    private static Mesh BuildPipeMesh(Vector3[] points, float[] headings, float radius, int sides, Mesh reuse)
    {
        MeshBuilder m = new MeshBuilder();
        int n = points.Length;
        Vector3[,] ring = new Vector3[n, sides];
        Vector3[,] normal = new Vector3[n, sides];
        for (int i = 0; i < n; i++)
        {
            float h = headings[i] * Mathf.Deg2Rad;
            Vector3 side = new Vector3(-Mathf.Sin(h), Mathf.Cos(h), 0f); // in the screen plane, across the pipe
            for (int j = 0; j < sides; j++)
            {
                float a = Mathf.PI * 2f * j / sides;
                Vector3 dir = side * Mathf.Cos(a) + Vector3.forward * Mathf.Sin(a);
                ring[i, j] = points[i] + dir * radius;
                normal[i, j] = dir;
            }
        }
        for (int i = 0; i < n - 1; i++)
        {
            for (int j = 0; j < sides; j++)
            {
                int k = (j + 1) % sides;
                m.Smooth(ring[i, j], ring[i + 1, j], ring[i + 1, k], normal[i, j], normal[i + 1, j], normal[i + 1, k]);
                m.Smooth(ring[i, j], ring[i + 1, k], ring[i, k], normal[i, j], normal[i + 1, k], normal[i, k]);
            }
        }
        return m.ToMesh(reuse, "Sorter Pipe");
    }

    /// <summary>A flat pie slice in the XY plane (both sides visible), opening along 'headingDegrees' from 'apex'.</summary>
    private static void BuildConeMesh(Mesh mesh, Vector2 apex, float headingDegrees, float halfAngleDegrees, float length)
    {
        MeshBuilder m = new MeshBuilder();
        const int segments = 16;
        Vector3 origin = new Vector3(apex.x, apex.y, -0.03f);
        for (int i = 0; i < segments; i++)
        {
            float a0 = (headingDegrees + Mathf.Lerp(-halfAngleDegrees, halfAngleDegrees, i / (float)segments)) * Mathf.Deg2Rad;
            float a1 = (headingDegrees + Mathf.Lerp(-halfAngleDegrees, halfAngleDegrees, (i + 1) / (float)segments)) * Mathf.Deg2Rad;
            Vector3 p0 = origin + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * length;
            Vector3 p1 = origin + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * length;
            m.Flat(origin, p0, p1, Vector3.back);
            m.Flat(origin, p0, p1, Vector3.forward);
        }
        m.ToMesh(mesh, "Sorter Cone");
    }

    /// <summary>
    /// Given the pointer, returns the pipe's bend (degrees) toward it and whether the pointer is inside the bend cone.
    /// 'root' is the sorter's (preview) object; the cone is measured on the plane through the ring.
    /// </summary>
    public static bool TryGetBend(Parts parts, Camera cam, float aim, float halfCone, out float bend)
    {
        bend = 0f;
        if (cam == null || parts == null) return false;
        Transform root = parts.Root.transform;
        Plane plane = new Plane(root.forward, root.position);
        Ray ray = cam.ScreenPointToRay(PointerPosition());
        if (!plane.Raycast(ray, out float enter)) return false;

        Vector3 local = root.InverseTransformPoint(ray.GetPoint(enter));
        Vector2 v = new Vector2(local.x, local.y) - parts.ConeApex(aim);
        if (v.sqrMagnitude < 0.0001f) return false;

        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(aim, angle);
        bend = Mathf.Clamp(delta, -halfCone, halfCone);
        return Mathf.Abs(delta) <= halfCone;
    }
}
