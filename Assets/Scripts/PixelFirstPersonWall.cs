// ---- System notes ----
// PixelFirstPersonWall.cs
//   Out-of-bounds protection for first person (added/removed by PixelFirstPerson.Enter / Exit; settings under its "Out of bounds" header). The play area is the same circle old pixels are kept in
//   (PixelFloor.TryGetEdgeCircle: where the floor fades out). Constrain() runs after every move: the player is pushed back inside the circle (by the controller's radius), and if they ever fall
//   far below the ground (through a gap, out of the world) they are put back at the spot they entered at. VISIBLE WALL: a neon wireframe cylinder drawn at the circle - a grid of thin quads
//   (vertical lines + rings, SpriteDefault vertex colours) built as a small PATCH of columns round the player's angle, rebuilt every frame (about a hundred quads) and faded by distance, so
//   it only appears when the player is within wallVisibleCubes of the edge and the lines stay put on the wall as the patch follows.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelFirstPersonWall : MonoBehaviour
{
    private PixelClicker clicker;
    private float unit, visibleDist, height, margin, spacing, halfWidth;
    private Color color;
    private Mesh mesh;
    private MeshRenderer renderer_;
    private Material mat;
    private Vector3[] verts;
    private Color[] cols;
    private int K, rows;
    private Vector3 rescueSpot;

    public void Setup(PixelClicker owner, Color wallColor, float visibleCubes, float heightCubes, float marginCubes, Vector3 spawnSpot)
    {
        clicker = owner;
        unit = Mathf.Max(0.01f, clicker.OldPixelWorldSize);
        color = wallColor;
        visibleDist = unit * visibleCubes;
        height = unit * heightCubes;
        margin = unit * marginCubes;
        spacing = unit * 2f;
        halfWidth = unit * 0.05f;
        rescueSpot = spawnSpot;
        K = Mathf.CeilToInt(visibleDist / spacing) + 1;
        rows = Mathf.Max(1, Mathf.RoundToInt(height / spacing));
        int quads = (2 * K + 1) + (2 * K) * (rows + 1);
        verts = new Vector3[quads * 4];
        cols = new Color[quads * 4];
        int[] tris = new int[quads * 6];
        for (int q = 0; q < quads; q++)
        {
            int v = q * 4, t = q * 6;
            tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2; tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
        }
        mesh = new Mesh { name = "First Person Wall" };
        mesh.MarkDynamic();
        mesh.vertices = verts;
        mesh.colors = cols;
        mesh.triangles = tris;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        renderer_ = gameObject.AddComponent<MeshRenderer>();
        mat = new Material(PixelShaders.SpriteDefault());
        renderer_.sharedMaterial = mat;
        renderer_.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer_.receiveShadows = false;
        renderer_.enabled = false;
    }

    private bool Circle(out Vector3 center, out float radius, out float groundY)
    {
        center = Vector3.zero; radius = 0f; groundY = 0f;
        PixelFloor floor = PixelFloor.Instance;
        if (floor == null || !floor.TryGetEdgeCircle(out center, out radius)) return false;
        groundY = floor.FloorRenderer != null ? floor.FloorRenderer.bounds.max.y : center.y;
        return true;
    }

    /// <summary>Keeps the controller inside the play circle; true if the player had fallen out of the world and was put back at the start spot.</summary>
    public bool Constrain(CharacterController cc, bool pushBack = true)
    {
        if (cc == null) return false;
        Vector3 p = cc.transform.position;
        if (!Circle(out Vector3 c, out float r, out float groundY)) return false;
        if (p.y < groundY - unit * 12f)
        {
            cc.enabled = false;
            cc.transform.position = rescueSpot;
            cc.enabled = true;
            return true;
        }
        if (!pushBack) return false;
        float limit = Mathf.Max(unit, r - margin - cc.radius);
        Vector3 d = p - c; d.y = 0f;
        float m = d.magnitude;
        if (m > limit && m > 0.0001f) cc.Move(-d / m * (m - limit));
        return false;
    }

    private void LateUpdate()
    {
        if (mesh == null || !PixelFirstPerson.TryGetPlayerPosition(out Vector3 player) || !Circle(out Vector3 c, out float r, out float groundY)) { if (renderer_ != null) renderer_.enabled = false; return; }
        Vector3 eye = player + Vector3.up * unit;
        Vector3 flat = player - c; flat.y = 0f;
        float wallR = r - margin;
        if (wallR - flat.magnitude > visibleDist) { renderer_.enabled = false; return; }   // not near the edge: nothing to show
        renderer_.enabled = true;

        int segCount = Mathf.Max(12, Mathf.RoundToInt(2f * Mathf.PI * wallR / spacing));
        float segA = 2f * Mathf.PI / segCount;
        int baseIdx = Mathf.RoundToInt(Mathf.Atan2(flat.z, flat.x) / segA);
        int kEff = Mathf.Min(K, segCount / 2 - 1);
        int q = 0;
        float top = rows * spacing;
        for (int j = -kEff; j <= kEff; j++)
        {
            float a = (baseIdx + j) * segA;
            Vector3 b = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * wallR; b.y = groundY;
            Vector3 tan = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * halfWidth;
            Vector3 t = b + Vector3.up * top;
            Quad(q++, b - tan, b + tan, t + tan, t - tan, eye);
        }
        for (int row = 0; row <= rows; row++)
        {
            float y = groundY + row * spacing;
            for (int j = -kEff; j < kEff; j++)
            {
                float a0 = (baseIdx + j) * segA, a1 = (baseIdx + j + 1) * segA;
                Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * wallR, p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * wallR;
                p0.y = p1.y = y;
                Vector3 w = Vector3.up * halfWidth;
                Quad(q++, p0 - w, p1 - w, p1 + w, p0 + w, eye);
            }
        }
        for (; q * 4 < verts.Length; q++) Quad(q, c, c, c, c, eye, true);   // (quads not needed this time collapse to a point)
        mesh.vertices = verts;
        mesh.colors = cols;
    }

    private void Quad(int q, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 eye, bool hidden = false)
    {
        int v = q * 4;
        verts[v] = a; verts[v + 1] = b; verts[v + 2] = c; verts[v + 3] = d;
        for (int i = 0; i < 4; i++)
        {
            float k = hidden ? 0f : Mathf.Clamp01(1f - Vector3.Distance(verts[v + i], eye) / visibleDist);
            Color col = color;
            col.a = color.a * k * k;
            cols[v + i] = col;
        }
    }

    private void OnDestroy() { if (mat != null) Destroy(mat); if (mesh != null) Destroy(mesh); }
}
