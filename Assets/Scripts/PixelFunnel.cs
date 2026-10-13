// ---- System notes ----
// PixelFunnel.cs
//   The FUNNEL (consumable device, PixelConsumables DeviceKind.Funnel 16, one at a time; the placed device is the thin PixelChipperDevice stub on the same object). A wide glass cone hangs just UNDER
//   the clicker cube (mouth radius mouthRadius cube widths, its mouth mouthDrop below the cube's underside) and catches every loose old pixel that falls into it: the pixel is locked (kinematic,
//   moved by position by the shared PixelPathCarrier), drawn to the throat and carried down a see-through TUBE (PixelTube, a smooth curve from the throat to the output) at tubeSpeed cube widths per second,
//   one gap apart. The output sits on the floor where the player clicked (the object's own position; the spout points away from the cube, Scroll / Q / E turns it a bit, a conveyor belt
//   opening in front of the spout makes it latch on like the Wood Chipper); at the end the pixel is put back as an ordinary old pixel (physics again) with a small push, or dropped onto the
//   belt. While placing, the output can only be put within maxReach cube widths of the cube. Placing a second funnel MOVES the first (no extra one is used up).
//   Ghost / display (shop picture) models are built by the same code (Mode); the cone and tube are pinned to the WORLD (the object's yaw only turns the spout). Riders in the tube are plain
//   old pixels for the save (they come back where they were); a rider you grab or that despawns is simply let go. Layout is in cube widths, recomputed when the object / floor moves.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelFunnel : MonoBehaviour
{
    public enum Mode { Real, Ghost, Display }

    [Header("Shape (cube widths)")]
    [Min(0f)]
    [Tooltip("Radius of the funnel's mouth. 0 = the coded default (3.4).")]
    [SerializeField] private float mouthRadius = 0f;

    [Min(0f)]
    [Tooltip("How far below the cube's underside the mouth sits. 0 = the coded default (0.35).")]
    [SerializeField] private float mouthDrop = 0f;

    [Min(0f)]
    [Tooltip("Height of the cone from mouth to throat (it is made shorter if the floor is near). 0 = the coded default (2.4).")]
    [SerializeField] private float coneHeight = 0f;

    [Min(0f)]
    [Tooltip("How far from the cube the output may be put. 0 = the coded default (16).")]
    [SerializeField] private float maxReach = 0f;

    [Header("Carrying")]
    [Min(0f)]
    [Tooltip("How fast pixels travel down the tube, in cube widths per second. 0 = the coded default (5).")]
    [SerializeField] private float tubeSpeed = 0f;

    [Min(0f)]
    [Tooltip("The space kept between two pixels in the tube, in cube widths. 0 = the coded default (0.9).")]
    [SerializeField] private float pixelGap = 0f;

    private float MouthR => mouthRadius > 0f ? mouthRadius : 3.4f;
    private float MouthDrop => mouthDrop > 0f ? mouthDrop : 0.35f;
    private float ConeH => coneHeight > 0f ? coneHeight : 2.4f;
    private float Reach => maxReach > 0f ? maxReach : 16f;
    private float Speed => tubeSpeed > 0f ? tubeSpeed : 5f;
    private float Gap => pixelGap > 0f ? pixelGap : 0.9f;

    private const float SpoutLen = 0.6f;      // cube widths: the straight bit of the spout past the output point
    private const float OutletH = 0.55f;      // cube widths: height of the tube's end above the floor
    private const float MinReach = 1.6f;      // cube widths: the closest the output may be to the cube's axis
    private const float ApproachSeconds = 0.25f;
    private const int PathSamples = 32;

    private static readonly List<PixelFunnel> real = new List<PixelFunnel>();
    private static Material glassMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { real.Clear(); glassMat = null; }

    /// <summary>Is a working funnel standing in the world (placing another one then moves it)?</summary>
    public static bool Exists { get { real.RemoveAll(f => f == null); return real.Count > 0; } }

    private Mode mode;
    private PixelClicker clicker;
    private PixelPlacedDevice device;
    private float unit = 1f;
    private float mouthR, throatR, mouthY, throatY, coneH;
    private Vector3 axisBottom;
    private Transform coneHolder, coneVisual, rim, tubeHolder;
    private readonly List<Transform> legs = new List<Transform>();
    private MeshFilter tubeFilter;
    private Mesh tubeMesh;
    private Vector3[] path = new Vector3[0];
    private float[] pathDist = new float[0];
    private float pathLen;
    private Vector3 lastPos = new Vector3(1e9f, 0f, 0f);
    private float lastYaw = 1e9f;
    private bool displayDone;
    private readonly PixelPathCarrier carrier = new PixelPathCarrier();   // locks the caught pixels and moves them down the tube

    // ---------------- building ----------------

    public static GameObject CreateGhost(PixelClicker c) { return Build(c, Mode.Ghost, "Funnel (Ghost)"); }

    /// <summary>A solid, inert model for pictures (inventory / shop): the cone, its legs, the tube and the spout, no behaviour.</summary>
    public static GameObject CreateDisplay(PixelClicker c) { return Build(c, Mode.Display, "Funnel (Display)"); }

    /// <summary>A working funnel with its output at 'point' (on the floor), the spout turned by 'yaw'. Any other funnel is taken away first.</summary>
    public static GameObject Place(PixelClicker c, Vector3 point, float yaw)
    {
        real.RemoveAll(f => f == null);
        foreach (PixelFunnel old in real.ToArray()) Destroy(old.gameObject);
        real.Clear();
        GameObject root = Build(c, Mode.Real, "Funnel");
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        return root;
    }

    private static GameObject Build(PixelClicker c, Mode mode, string name)
    {
        GameObject root = new GameObject(name);
        PixelFunnel f = root.AddComponent<PixelFunnel>();
        f.mode = mode;
        f.clicker = c;
        f.unit = Mathf.Max(0.05f, c != null ? c.PixelBaseSize : 1f);
        f.BuildParts();
        if (mode == Mode.Real) real.Add(f);
        return root;
    }

    private static Material Glass()
    {
        if (glassMat == null) glassMat = new Material(PixelShaders.SpriteDefault());
        return glassMat;
    }

    private void BuildParts()
    {
        float u = unit;
        float oldSize = clicker != null ? clicker.OldPixelWorldSize : 0.4f * u;
        mouthR = MouthR * u;
        throatR = Mathf.Max(0.16f * u, oldSize * 0.8f);
        PixelFactoryKit kit = new PixelFactoryKit(clicker, mode == Mode.Ghost);
        Color steel = new Color(0.62f, 0.66f, 0.72f, 1f), accent = new Color(0.95f, 0.62f, 0.12f, 1f);

        // The cone (a double-sided frustum, unit height, pinned under the cube) with a rim and three legs.
        coneHolder = new GameObject("Cone").transform; coneHolder.SetParent(transform, false);
        coneVisual = new GameObject("Glass").transform; coneVisual.SetParent(coneHolder, false);
        coneVisual.gameObject.AddComponent<MeshFilter>().sharedMesh = FrustumMesh(throatR / mouthR, mode == Mode.Ghost ? 0.25f : 0.38f);
        coneVisual.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Glass();
        coneVisual.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rim = new GameObject("Rim").transform; rim.SetParent(coneHolder, false);
        Vector3[] ring = new Vector3[41];
        for (int i = 0; i < ring.Length; i++) { float a = i / 40f * Mathf.PI * 2f; ring[i] = new Vector3(Mathf.Cos(a) * mouthR, 0f, Mathf.Sin(a) * mouthR); }
        rim.gameObject.AddComponent<MeshFilter>().sharedMesh = PixelTube.Build(ring, 0.09f * u, 8, null);
        rim.gameObject.AddComponent<MeshRenderer>().sharedMaterial = kit.Mat(accent, 0.3f, 0.5f);
        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * Mathf.PI * 2f + 0.5f;
            Transform leg = kit.Cyl("Leg", coneHolder, new Vector3(Mathf.Cos(a) * mouthR * 0.94f, 0f, Mathf.Sin(a) * mouthR * 0.94f), 0.09f * u, 2f, steel).transform;
            legs.Add(leg);
        }

        // The tube (see-through, rebuilt when the object moves) and the output: a base plate, a collar and a rim at the spout end.
        tubeHolder = new GameObject("Tube").transform; tubeHolder.SetParent(transform, false);
        tubeFilter = tubeHolder.gameObject.AddComponent<MeshFilter>();
        tubeHolder.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Glass();
        tubeHolder.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tubeMesh = new Mesh { name = "Funnel Tube" };
        tubeFilter.sharedMesh = tubeMesh;
        kit.Cyl("Plate", transform, new Vector3(0f, 0.04f * u, 0f), 0.8f * u, 0.08f * u, steel);
        kit.Cyl("Collar", transform, new Vector3(0f, OutletH * u, SpoutLen * u), throatR * 1.3f, 0.14f * u, accent, new Vector3(90f, 0f, 0f), 0.3f, 0.5f);
        kit.Cyl("Collar Base", transform, new Vector3(0f, OutletH * u * 0.5f, 0f), throatR * 1.2f, OutletH * u, steel);

        Layout();
    }

    /// <summary>A cone shell, height 1, top radius 1, bottom radius 'bottom', visible from both sides; vertex colours give the glass tint.</summary>
    private static Mesh FrustumMesh(float bottom, float alpha)
    {
        const int N = 40;
        Vector3[] v = new Vector3[(N + 1) * 2];
        Color[] col = new Color[v.Length];
        for (int i = 0; i <= N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f;
            v[i] = new Vector3(Mathf.Cos(a) * bottom, 0f, Mathf.Sin(a) * bottom);
            v[N + 1 + i] = new Vector3(Mathf.Cos(a), 1f, Mathf.Sin(a));
            col[i] = new Color(0.55f, 0.8f, 1f, alpha * 1.4f);
            col[N + 1 + i] = new Color(0.7f, 0.9f, 1f, alpha * 0.6f);
        }
        List<int> t = new List<int>();
        for (int i = 0; i < N; i++)
        {
            int a = i, b = i + 1, c = N + 1 + i, d = N + 2 + i;
            t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d);
            t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c);
        }
        Mesh m = new Mesh { name = "Funnel Cone", vertices = v, colors = col, triangles = t.ToArray() };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ---------------- layout ----------------

    private Vector3 CubePosition()
    {
        if (mode == Mode.Display) return transform.position + new Vector3(0f, 4.6f * unit, -4f * unit);
        return clicker != null ? clicker.CubeBasePosition : Vector3.zero;
    }

    private void LateUpdate()
    {
        if (mode == Mode.Display && displayDone) return;
        Layout();
    }

    /// <summary>Places the cone, legs and tube from the cube, the floor and where the output is. Only does work when something moved.</summary>
    private void Layout()
    {
        Vector3 pos = transform.position;
        float yaw = transform.eulerAngles.y;
        if (!(Mathf.Abs(pos.x - lastPos.x) > 0.0005f || Mathf.Abs(pos.y - lastPos.y) > 0.0005f || Mathf.Abs(pos.z - lastPos.z) > 0.0005f || Mathf.Abs(Mathf.DeltaAngle(yaw, lastYaw)) > 0.01f)) return;
        lastPos = pos; lastYaw = yaw;
        float u = unit;
        Vector3 cube = CubePosition();
        float floorY = pos.y;
        mouthY = cube.y - 0.5f * u - MouthDrop * u;
        throatY = mouthY - ConeH * u;
        float minThroat = floorY + 1.1f * u;
        if (throatY < minThroat) throatY = minThroat;
        if (mouthY < throatY + 0.9f * u) mouthY = throatY + 0.9f * u;
        coneH = mouthY - throatY;
        axisBottom = new Vector3(cube.x, throatY, cube.z);

        bool pinned = mode != Mode.Display;
        if (pinned) { coneHolder.SetPositionAndRotation(axisBottom, Quaternion.identity); tubeHolder.SetPositionAndRotation(pos, Quaternion.identity); }
        else { coneHolder.localPosition = axisBottom - pos; tubeHolder.localPosition = Vector3.zero; }
        coneVisual.localScale = new Vector3(mouthR, coneH, mouthR);
        rim.localPosition = new Vector3(0f, coneH, 0f);
        float floorLocal = floorY - throatY, legLen = coneH - floorLocal;
        foreach (Transform leg in legs)
        {
            Vector3 lp = leg.localPosition;
            leg.localPosition = new Vector3(lp.x, floorLocal + legLen * 0.5f, lp.z);
            leg.localScale = new Vector3(leg.localScale.x, legLen * 0.5f, leg.localScale.z);
        }

        // The tube: a cubic curve that leaves the throat straight down and arrives at the spout level with the spout.
        Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 a0 = axisBottom, e = pos + Vector3.up * (OutletH * u) + fwd * (SpoutLen * u);
        float k = Mathf.Max(0.9f * u, 0.42f * Vector3.Distance(a0, e));
        Vector3 p1 = a0 + Vector3.down * k, p2 = e - fwd * k;
        if (path.Length != PathSamples + 1) { path = new Vector3[PathSamples + 1]; pathDist = new float[PathSamples + 1]; }
        Vector3[] local = new Vector3[PathSamples + 1];
        for (int i = 0; i <= PathSamples; i++)
        {
            float t = i / (float)PathSamples, s = 1f - t;
            Vector3 p = s * s * s * a0 + 3f * s * s * t * p1 + 3f * s * t * t * p2 + t * t * t * e;
            p.y = Mathf.Max(p.y, floorY + throatR * 1.05f);
            path[i] = p;
            pathDist[i] = i == 0 ? 0f : pathDist[i - 1] + Vector3.Distance(path[i - 1], p);
            local[i] = pinned ? p - pos : p - pos;
        }
        pathLen = pathDist[PathSamples];
        PixelTube.Build(local, throatR * 1.12f, 10, tubeMesh, (ring, vp, vn) => new Color(0.6f, 0.85f, 1f, mode == Mode.Ghost ? 0.18f : 0.3f));
        tubeMesh.RecalculateBounds();
        if (mode == Mode.Display) displayDone = true;
    }

    // ---------------- placing ----------------

    /// <summary>
    /// While placing: puts the output at the clicked floor spot (kept inside the reach), turns the spout away from the cube plus the player's turn, and latches onto a conveyor belt opening in
    /// front of the spout. Returns true when latched. 'point' / 'yaw' are where the funnel will really stand.
    /// </summary>
    public bool Adjust(Vector3 floorPoint, float turnOffset, out Vector3 point, out float yaw)
    {
        float u = unit;
        Vector3 cube = CubePosition();
        Vector3 flat = floorPoint - cube; flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) flat = Vector3.back;
        float len = Mathf.Clamp(flat.magnitude, MinReach * u, Reach * u);
        flat = flat.normalized * len;
        point = new Vector3(cube.x + flat.x, floorPoint.y, cube.z + flat.z);
        yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg + turnOffset;

        Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        Vector3 exit = point + fwd * (SpoutLen * u);
        PixelConveyor best = null;
        float bestAlong = float.MaxValue;
        foreach (PixelConveyor belt in PixelConveyor.All)
        {
            if (belt == null || belt.IsGhost) continue;
            Vector3 v = belt.StartPoint - exit; v.y = 0f;
            float along = Vector3.Dot(v, fwd), lateral = Vector3.Dot(v, right);
            if (along < -0.3f * u || along > 2f * u || Mathf.Abs(lateral) > 1.1f * u) continue;
            if (Mathf.Abs(along) < bestAlong) { bestAlong = Mathf.Abs(along); best = belt; }
        }
        if (best == null) return false;
        Vector3 bd = best.Direction;
        yaw = Mathf.Atan2(bd.x, bd.z) * Mathf.Rad2Deg;
        Vector3 f2 = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 over = best.PointAt(0.6f * (clicker != null ? clicker.OldPixelWorldSize : 0.4f * u));
        point = new Vector3(over.x - f2.x * SpoutLen * u, floorPoint.y, over.z - f2.z * SpoutLen * u);
        return true;
    }

    // ---------------- behaviour ----------------

    private void OnDestroy()
    {
        real.Remove(this);
        carrier.ReleaseAll();
    }

    private bool InMouth(Vector3 c)
    {
        float u = unit;
        if (c.y < throatY - 0.2f * u || c.y > mouthY + 0.8f * u) return false;
        float t = Mathf.Clamp01((c.y - throatY) / Mathf.Max(0.01f, mouthY - throatY));
        float r = Mathf.Lerp(throatR, mouthR, t);
        float dx = c.x - axisBottom.x, dz = c.z - axisBottom.z;
        return dx * dx + dz * dz < r * r;
    }

    private bool CanCatch(Rigidbody rb)
    {
        if (rb == null || rb.isKinematic || carrier.Contains(rb) || PixelAttachment.IsAttached(rb) || clicker.IsFlyingPixel(rb)) return false;
        OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
        if (info != null && info.heldByPlayer) return false;
        OldPixelDespawn despawn = rb.GetComponent<OldPixelDespawn>();
        if (despawn != null && (despawn.IsDespawning || despawn.Held)) return false;
        return true;
    }

    private void FixedUpdate()
    {
        if (mode != Mode.Real || clicker == null || pathLen <= 0f) return;
        if (device == null) device = GetComponent<PixelPlacedDevice>();
        bool removing = device != null && device.IsRemoving;

        // 1. Catch: every loose old pixel inside the cone is locked and drawn to the throat.
        if (!removing)
        {
            IReadOnlyList<Rigidbody> list = clicker.OldPixels;
            for (int i = 0; i < list.Count; i++)
            {
                Rigidbody rb = list[i];
                if (CanCatch(rb) && InMouth(rb.worldCenterOfMass)) carrier.Add(rb);
            }
        }

        // 2. Carry them down the tube (a gap apart); at the end each one is put back into the world.
        carrier.Step(path, pathDist, Speed * unit, Gap * unit, Time.fixedDeltaTime, Release, ApproachSeconds);
    }

    /// <summary>The pixel leaves the spout: ordinary physics again, or dropped onto a belt opening in front of it.</summary>
    private void Release(Rigidbody rb)
    {
        float u = unit;
        Vector3 end = path[path.Length - 1], dir = (path[path.Length - 1] - path[path.Length - 2]).normalized;
        float oldSize = clicker.OldPixelWorldSize;
        Vector3 velocity = dir * Speed * u * 0.4f;
        foreach (PixelConveyor belt in PixelConveyor.All)
        {
            if (belt == null || belt.IsGhost) continue;
            Vector3 d = belt.StartPoint - end; d.y = 0f;
            if (d.magnitude > 1.3f * u) continue;
            Vector3 over = belt.PointAt(0.6f * oldSize);
            end = new Vector3(over.x, over.y + belt.SurfaceHeight + oldSize * 0.7f, over.z);
            velocity = belt.Direction * belt.Speed * 0.5f;
            break;
        }
        rb.position = end;
        PixelPathCarrier.SetVelocity(rb, velocity);
    }
}
