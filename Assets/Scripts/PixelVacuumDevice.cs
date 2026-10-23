// ---- System notes (moved here from CLAUDE.md) ----
// PixelVacuumDevice.cs
//   (extends PixelPlacedDevice) Runtime behaviour of a placed vacuum device (lathe-turned body BuildModel /
//   LatheMesh, both in this file: flared base, waist, funnel mouth with a lip, two glowing bands, a glowing core and a small spinning turbine
//   'Spinner' turned in OnTick; it lasts only vacuumDeviceSeconds (0 = 5 s), DeviceSeconds(d)): pulls old pixels in its radius,
//   PixelClicker.AbsorbOldPixel, timer text above it, shrinks away at 0. Built by PixelConsumables.
// ---------------------------------------------------
using TMPro;
using UnityEngine;

/// <summary>
/// A placed vacuum device (the cylinder). Created and configured by <see cref="PixelConsumables"/>.
///
/// While it is active it pulls every old pixel inside its radius toward the top of the cylinder and,
/// when a pixel gets close enough, consumes it (PixelClicker.AbsorbOldPixel: pixel rules = husk XP; with husksPayCurrency
/// the original reward is re-added, like the Vacuum pixel). The countdown, timer text and shrink-away come from <see cref="PixelPlacedDevice"/>.
/// </summary>
public class PixelVacuumDevice : PixelPlacedDevice
{
    private Transform suckPoint;
    private float radius;
    private float pullAcceleration;
    private float absorbDistance;
    private Transform spinner;
    private float spinAngle;

    public void Init(PixelClicker owner, Camera camera, Transform pullTarget, TextMeshPro timer, string format,
                     float duration, float suctionRadius, float pull, float absorb, float shrinkTime)
    {
        InitCommon(owner, camera, timer, format, duration, shrinkTime);
        suckPoint = pullTarget;
        radius = suctionRadius;
        pullAcceleration = pull;
        absorbDistance = absorb;
        spinner = transform.Find("Spinner");
    }

    protected override void OnTick()
    {
        // The little turbine over the funnel spins while the device works.
        if (spinner == null) return;
        spinAngle += 540f * Time.deltaTime;
        spinner.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
    }

    private void FixedUpdate()
    {
        if (IsEnding || !Armed || clicker == null) return;

        var pixels = clicker.OldPixels;
        Vector3 target = suckPoint.position;

        // Backwards: collecting a pixel removes it from the list.
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            Rigidbody body = pixels[i];
            if (body == null || !Accepts(body)) continue;

            Vector3 to = target - body.position;
            float distance = to.magnitude;
            if (distance > radius) continue;
            if (!PixelHuskHolder.Takeable(clicker, body, true)) continue;   // (one owner per husk: never one in the hand, reserved or kept by another machine, or fading)

            if (distance <= absorbDistance) clicker.AbsorbOldPixel(body, suckPoint);
            else body.AddForce(to / distance * pullAcceleration, ForceMode.Acceleration);
        }
    }

    // ---- the device card (PixelDeviceCard) ----
    protected override void AddInfo(PixelDeviceInfo info)
    {
        info.Add("Sucks in", "husks within " + radius.ToString("0.#") + " units");
        if (!info.Full) return;
        info.Add("Pull", pullAcceleration.ToString("0.#"));
    }

    // ---- The model (moved here from PixelConsumables 2026-10-24: each device builds its own look; PixelConsumables.BuildDeviceObject calls it) ----

    /// <summary>
    /// The Vacuum Device's look: a turned (lathe) body with a flared base, a slim waist and a funnel mouth at the top, two glowing bands, a glowing
    /// core in the funnel and a small spinning turbine ("Spinner", turned by PixelVacuumDevice) above it.
    /// </summary>
    public static void BuildModel(PixelClicker clicker, GameObject root, PixelConsumables.Device d, bool isPreview, float previewOpacity)
    {
        float R = d.bodyDiameter * 0.5f, H = d.bodyHeight;
        float opacity = isPreview ? previewOpacity : 1f;
        Color main = d.color; main.a = opacity;
        Color glow = Color.Lerp(d.color, Color.white, 0.55f); glow.a = opacity;

        // The turned body.
        GameObject body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
        body.transform.SetParent(root.transform, false);
        body.GetComponent<MeshFilter>().sharedMesh = LatheMesh(R, H);
        MeshRenderer bodyRenderer = body.GetComponent<MeshRenderer>();
        Material bodyMat = clicker.CreateVisualMaterial(main, isPreview);
        if (bodyMat != null) bodyRenderer.sharedMaterial = bodyMat; else bodyRenderer.material.color = main;

        // Two glowing bands round the body (unlit, so they read as lit from inside).
        float[] bandHeights = { 0.26f, 0.46f };
        foreach (float bh in bandHeights)
        {
            GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            band.name = "Glow Band";
            Destroy(band.GetComponent<Collider>());
            band.transform.SetParent(root.transform, false);
            band.transform.localScale = new Vector3(R * 2f * 0.93f, H * 0.012f, R * 2f * 0.93f);
            band.transform.localPosition = new Vector3(0f, H * bh, 0f);
            Renderer br = band.GetComponent<Renderer>();
            br.sharedMaterial = PixelLooks.OverlayMaterial();
            br.material.color = glow;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            br.receiveShadows = false;
        }

        // The glowing core in the funnel.
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        core.name = "Core";
        Destroy(core.GetComponent<Collider>());
        core.transform.SetParent(root.transform, false);
        core.transform.localScale = new Vector3(R * 2f * 0.8f, H * 0.01f, R * 2f * 0.8f);
        core.transform.localPosition = new Vector3(0f, H * 0.9f, 0f);
        Renderer cr = core.GetComponent<Renderer>();
        cr.sharedMaterial = PixelLooks.OverlayMaterial();
        cr.material.color = Color.Lerp(glow, Color.white, 0.35f);
        cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cr.receiveShadows = false;

        // A little turbine over the core.
        GameObject spinner = new GameObject("Spinner");
        spinner.transform.SetParent(root.transform, false);
        spinner.transform.localPosition = new Vector3(0f, H * 0.93f, 0f);
        Color dark = new Color(d.color.r * 0.35f, d.color.g * 0.35f, d.color.b * 0.35f, opacity);
        for (int i = 0; i < 3; i++)
        {
            GameObject blade = PixelFactoryKit.PlainPart(clicker, PrimitiveType.Cube, "Blade " + i, spinner.transform, dark, isPreview);
            blade.transform.localRotation = Quaternion.Euler(0f, i * 60f, 0f);
            blade.transform.localScale = new Vector3(R * 1.5f, H * 0.012f, R * 0.14f);
        }
    }

    /// <summary>A surface of revolution (a turned body): flared base, straight body, slim waist, funnel mouth with a thin lip and a hollow top.</summary>
    private static Mesh LatheMesh(float R, float H)
    {
        // (radius as a multiple of R, height as a fraction of H), from the middle of the bottom, out and up, over the lip and back to the middle of the funnel.
        float[,] profile =
        {
            { 0f, 0f }, { 1.3f, 0f }, { 1.3f, 0.045f }, { 1.05f, 0.09f }, { 0.9f, 0.15f }, { 0.9f, 0.58f }, { 0.78f, 0.66f }, { 0.7f, 0.74f },
            { 0.78f, 0.83f }, { 1.0f, 0.95f }, { 1.1f, 0.99f }, { 1.08f, 1.0f }, { 0.98f, 0.98f }, { 0.85f, 0.93f }, { 0.5f, 0.88f }, { 0f, 0.87f },
        };
        int rings = profile.GetLength(0), sides = 28;
        Vector3[] vertices = new Vector3[rings * sides];
        for (int i = 0; i < rings; i++)
            for (int k = 0; k < sides; k++)
            {
                float angle = k * Mathf.PI * 2f / sides;
                float r = profile[i, 0] * R;
                vertices[i * sides + k] = new Vector3(Mathf.Cos(angle) * r, profile[i, 1] * H, Mathf.Sin(angle) * r);
            }

        System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();
        for (int i = 0; i < rings - 1; i++)
        {
            // Outward normal of this stretch of the profile (the solid is on its left): rotate the tangent a quarter turn clockwise.
            float dr = profile[i + 1, 0] * R - profile[i, 0] * R, dy = profile[i + 1, 1] * H - profile[i, 1] * H;
            for (int k = 0; k < sides; k++)
            {
                int n = (k + 1) % sides;
                int a = i * sides + k, b = i * sides + n, c = (i + 1) * sides + k, e = (i + 1) * sides + n;
                float angle = (k + 0.5f) * Mathf.PI * 2f / sides;
                Vector3 expected = new Vector3(Mathf.Cos(angle) * dy, -dr, Mathf.Sin(angle) * dy);
                AddOriented(triangles, vertices, a, c, b, expected);
                AddOriented(triangles, vertices, b, c, e, expected);
            }
        }

        Mesh mesh = new Mesh { name = "VacuumBody" };
        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddOriented(System.Collections.Generic.List<int> triangles, Vector3[] v, int a, int b, int c, Vector3 expectedNormal)
    {
        Vector3 geometric = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (geometric.sqrMagnitude < 1e-12f) return;   // a collapsed triangle (at the middle of the bottom / top)
        if (Vector3.Dot(geometric, expectedNormal) < 0f) { int t = b; b = c; c = t; }
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
    }
}
