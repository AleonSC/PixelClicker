// ---- System notes ----
// PixelFirstPersonGrid.cs
//   The first person floor: while PixelFirstPerson is active the normal floor is hidden (PixelFloor.FloorOff includes PixelFirstPerson.Active, the ground collider stays) and this draws a bright neon
//   grid (Resources/PixelNeonGrid.shader, world-space lines) on a quad just above the ground, only within a radius of lit points: the player, the clicker cube and old pixels resting on the ground
//   (up to 32, nearest to the player first; collected every 0.1 s). Settings live on PixelFirstPerson (header "Neon floor"). Added/removed by PixelFirstPerson.Enter / Exit.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelFirstPersonGrid : MonoBehaviour
{
    private const int MaxPoints = 32;

    private PixelClicker clicker;
    private Material mat;
    private Vector4[] points = new Vector4[MaxPoints];
    private Color color;
    private float spacing, playerRadius, pixelRadius, brightness;
    private float timer;
    private int count;
    private float groundY;
    private readonly List<KeyValuePair<float, Vector4>> candidates = new List<KeyValuePair<float, Vector4>>();

    public void Setup(PixelClicker owner, Color gridColor, float spacingWorld, float playerRadiusWorld, float pixelRadiusWorld, float brightnessValue)
    {
        clicker = owner;
        color = gridColor; spacing = spacingWorld; playerRadius = playerRadiusWorld; pixelRadius = pixelRadiusWorld; brightness = brightnessValue;

        Bounds fb = new Bounds(Vector3.zero, new Vector3(80f, 0f, 80f));
        groundY = 0f;
        if (PixelFloor.Instance != null && PixelFloor.Instance.FloorRenderer != null) { fb = PixelFloor.Instance.FloorRenderer.bounds; groundY = fb.max.y; }

        Mesh mesh = new Mesh { name = "Neon Grid Quad" };
        mesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 1f));
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
        Shader sh = Shader.Find("Pixel Clicker/Neon Grid");
        if (sh == null) sh = PixelShaders.SpriteDefault();
        mat = new Material(sh);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        float size = Mathf.Max(fb.size.x, fb.size.z, 20f) * 1.5f;
        transform.position = new Vector3(fb.center.x, groundY + Mathf.Max(0.002f, clicker.PixelBaseSize * 0.012f), fb.center.z);
        transform.localScale = new Vector3(size, 1f, size);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Spacing", spacing);
        mat.SetFloat("_Brightness", brightness);
        Collect();
    }

    private void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f) { timer = 0.1f; Collect(); }
        if (mat != null && PixelFirstPerson.TryGetPlayerPosition(out Vector3 p))
        {
            points[0] = new Vector4(p.x, p.z, playerRadius, 0f);   // (the player moves every frame: keep its light exact)
            mat.SetVectorArray("_Points", points);
        }
    }

    private void Collect()
    {
        if (mat == null || clicker == null) return;
        PixelFirstPerson.TryGetPlayerPosition(out Vector3 player);
        candidates.Clear();
        float unit = clicker.OldPixelWorldSize;
        float touch = groundY + unit * 0.75f;
        var list = clicker.OldPixels;
        for (int i = 0; i < list.Count; i++)
        {
            Rigidbody b = list[i];
            if (b == null || b.isKinematic) continue;
            Vector3 pos = b.worldCenterOfMass;
            if (pos.y > touch) continue;
            float d = (new Vector2(pos.x, pos.z) - new Vector2(player.x, player.z)).sqrMagnitude;
            candidates.Add(new KeyValuePair<float, Vector4>(d, new Vector4(pos.x, pos.z, pixelRadius, 0f)));
        }
        if (clicker.PixelTransform != null)
        {
            Vector3 c = clicker.CubeBasePosition;
            candidates.Add(new KeyValuePair<float, Vector4>(0f, new Vector4(c.x, c.z, playerRadius, 0f)));
        }
        candidates.Sort((a, b) => a.Key.CompareTo(b.Key));
        points[0] = new Vector4(player.x, player.z, playerRadius, 0f);
        count = 1;
        for (int i = 0; i < candidates.Count && count < MaxPoints; i++) points[count++] = candidates[i].Value;
        mat.SetFloat("_Count", count);
        mat.SetVectorArray("_Points", points);
    }

    private void OnDestroy() { if (mat != null) Destroy(mat); }
}
