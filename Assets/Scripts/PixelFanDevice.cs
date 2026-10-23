// ---- System notes (moved here from CLAUDE.md) ----
// PixelFanDevice.cs
//   (extends PixelPlacedDevice) Runtime behaviour of a placed fan (device kind = Fan): pushes old pixels inside a floor wedge (cone
//   angle/length/height) along its forward, spinning blades, timer. Placed like the vacuum, turned with scroll wheel or Q/E while
//   placing.
// ---------------------------------------------------
using TMPro;
using UnityEngine;

/// <summary>
/// A placed fan. Created and configured by <see cref="PixelConsumables"/>.
///
/// While it is active it gently blows every old pixel inside a cone-shaped area in front of it: the area is a wedge
/// on the floor (opening angle and length are adjustable) that reaches up to a set height. The blades spin.
/// The countdown, timer text and shrink-away come from <see cref="PixelPlacedDevice"/>.
/// </summary>
public class PixelFanDevice : PixelPlacedDevice
{
    private Transform blades;
    private float range;
    private float halfAngle;
    private float height;
    private float blow;
    private float lift;
    private float spinDegrees;
    private float bladeAngle;

    public void Init(PixelClicker owner, Camera camera, Transform bladesTransform, TextMeshPro timer, string format,
                     float duration, float reach, float coneAngle, float coneHeight, float blowAcceleration,
                     float liftAcceleration, float bladeSpin, float shrinkTime)
    {
        InitCommon(owner, camera, timer, format, duration, shrinkTime);
        blades = bladesTransform;
        range = reach;
        halfAngle = coneAngle * 0.5f;
        height = coneHeight;
        blow = blowAcceleration;
        lift = liftAcceleration;
        spinDegrees = bladeSpin;
    }

    protected override void OnTick()
    {
        if (blades == null) return;
        bladeAngle += spinDegrees * Time.deltaTime;
        blades.localRotation = Quaternion.Euler(0f, 0f, bladeAngle);
    }

    private void FixedUpdate()
    {
        if (IsEnding || !Armed || clicker == null) return;

        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        var pixels = clicker.OldPixels;
        for (int i = 0; i < pixels.Count; i++)
        {
            Rigidbody body = pixels[i];
            if (body == null || body.isKinematic || !Accepts(body)) continue;

            Vector3 to = body.position - origin;
            if (to.y < -0.3f || to.y > height) continue; // above / below the wedge

            to.y = 0f;
            float distance = to.magnitude;
            if (distance > range || distance < 0.05f) continue;
            if (Vector3.Angle(forward, to) > halfAngle) continue; // outside the cone
            if (!PixelHuskHolder.Takeable(clicker, body, true)) continue;   // (not the husk in the player's hand, nor one a machine keeps or carries)

            // Strongest right at the fan, fading toward the far end.
            float strength = Mathf.Lerp(1f, 0.25f, distance / range);
            body.AddForce((forward * blow + Vector3.up * lift) * strength, ForceMode.Acceleration);
        }
    }

    // ---- the device card (PixelDeviceCard) ----
    protected override void AddInfo(PixelDeviceInfo info)
    {
        info.Add("Blows", range.ToString("0.#") + " units ahead, " + Mathf.RoundToInt(halfAngle * 2f) + "\u00b0 wide");
        if (!info.Full) return;
        info.Add("Push", blow.ToString("0.#") + (lift > 0f ? " (lift " + lift.ToString("0.#") + ")" : ""));
    }

    // ---- The model (moved here from PixelConsumables 2026-10-24: each device builds its own look; PixelConsumables.BuildDeviceObject calls it) ----

    /// <summary>The Fan's look (a weighted base with a control pod, a post with a collar, a motor head with four pitched blades in a chrome guard and a
    /// glowing badge). The head faces +Z; 'blades' is the part PixelFanDevice spins.</summary>
    public static void BuildModel(PixelClicker clicker, GameObject root, PixelConsumables.Device d, bool isPreview, float previewOpacity, out Transform blades)
    {
        PixelFactoryKit k = new PixelFactoryKit(clicker, isPreview) { alpha = previewOpacity };
        Transform t = root.transform;
        float D = d.bodyDiameter, H = d.bodyHeight, R = D * 0.5f;
        Color body = d.color, dark = new Color(d.color.r * 0.3f, d.color.g * 0.3f, d.color.b * 0.3f, 1f), chrome = new Color(0.82f, 0.85f, 0.9f, 1f),
              rubber = new Color(0.08f, 0.08f, 0.09f, 1f), badge = Color.Lerp(d.color, Color.white, 0.35f);

        // Base: a weighted disc with a coloured bevel, and the control pod at the front.
        k.Disc("Base", t, 0.035f, D * 0.38f, 0.07f, dark, 0.5f, 0.55f);
        k.Disc("Base Bevel", t, 0.08f, D * 0.31f, 0.03f, body, 0.3f, 0.6f);
        k.Box("Control Pod", t, new Vector3(0f, 0.09f, D * 0.26f), new Vector3(D * 0.24f, 0.06f, D * 0.12f), dark, new Vector3(-12f, 0f, 0f), false, 0.4f, 0.5f);
        Color[] buttons = { chrome, body, new Color(0.3f, 1f, 0.45f, 1f) };
        for (int i = 0; i < 3; i++)
            k.Prim(PrimitiveType.Cylinder, "Button", t, new Vector3((i - 1) * D * 0.065f, 0.125f, D * 0.265f), new Vector3(0.04f, 0.012f, 0.04f), buttons[i],
                   new Vector3(-12f, 0f, 0f), 0.4f, 0.7f, false, i == 2 ? buttons[i] : (Color?)null);

        // Post: a dark lower tube, a collar with a knob, a chrome upper tube.
        float collarY = H * 0.5f;
        k.Cyl("Post", t, new Vector3(0f, (0.09f + collarY) * 0.5f, 0f), 0.045f, collarY - 0.09f, dark, default, 0.5f, 0.6f);
        k.Disc("Collar", t, collarY, 0.065f, 0.06f, chrome, 0.9f, 0.8f);
        k.Ball("Collar Knob", t, new Vector3(0.08f, collarY, 0f), 0.025f, rubber, 0.2f, 0.5f);
        k.Cyl("Upper Post", t, new Vector3(0f, (collarY + H - 0.1f) * 0.5f, 0f), 0.03f, H - 0.1f - collarY, chrome, default, 0.95f, 0.85f);

        // Head: the motor, the guard and the blades. It faces +Z.
        GameObject head = new GameObject("Head");
        head.transform.SetParent(t, false);
        head.transform.localPosition = new Vector3(0f, H, 0f);
        Transform h = head.transform;
        k.Box("Pivot", h, new Vector3(0f, -0.1f, -0.08f), new Vector3(0.08f, 0.1f, 0.16f), dark, default, false, 0.5f, 0.5f);
        k.Cyl("Motor", h, new Vector3(0f, 0f, -0.12f), D * 0.15f, 0.18f, body, new Vector3(90f, 0f, 0f), 0.35f, 0.65f);
        k.Prim(PrimitiveType.Sphere, "Motor Back", h, new Vector3(0f, 0f, -0.21f), new Vector3(D * 0.3f, D * 0.3f, 0.12f), body, default, 0.35f, 0.65f);
        for (int i = 0; i < 2; i++)
            k.Cyl("Vent Ring", h, new Vector3(0f, 0f, -0.16f + i * 0.06f), D * 0.155f, 0.012f, dark, new Vector3(90f, 0f, 0f), 0.4f, 0.5f);

        // Blades: four pitched blades with round tips round a chrome spinner.
        GameObject bladeRoot = new GameObject("Blades");
        bladeRoot.transform.SetParent(h, false);
        bladeRoot.transform.localPosition = new Vector3(0f, 0f, 0.08f);
        for (int i = 0; i < 4; i++)
        {
            GameObject arm = new GameObject("Blade " + i);
            arm.transform.SetParent(bladeRoot.transform, false);
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
            k.Box("Blade", arm.transform, new Vector3(0f, R * 0.46f, 0f), new Vector3(D * 0.2f, R * 0.62f, 0.012f), body, new Vector3(0f, 24f, 0f), false, 0.2f, 0.7f);
            k.Prim(PrimitiveType.Sphere, "Blade Tip", arm.transform, new Vector3(0f, R * 0.77f, 0f), new Vector3(D * 0.2f, D * 0.11f, 0.012f), body, new Vector3(0f, 24f, 0f), 0.2f, 0.7f);
        }
        k.Cyl("Hub", bladeRoot.transform, Vector3.zero, D * 0.07f, 0.05f, chrome, new Vector3(90f, 0f, 0f), 0.95f, 0.85f);
        k.Prim(PrimitiveType.Sphere, "Spinner", bladeRoot.transform, new Vector3(0f, 0f, 0.03f), new Vector3(D * 0.12f, D * 0.12f, 0.07f), chrome, default, 0.95f, 0.9f);
        blades = bladeRoot.transform;

        // The guard: a back and a front ring, bars joining them round the rim, spokes across the front and a glowing badge in the middle.
        float gr = R * 0.98f, back = -0.02f, front = 0.17f;
        const int RingParts = 20;
        float seg = 2f * Mathf.PI * gr / RingParts * 1.08f;
        for (int i = 0; i < RingParts; i++)
        {
            float a = i * 360f / RingParts;
            Vector3 dir = Quaternion.Euler(0f, 0f, a) * Vector3.up;
            foreach (float z in new[] { back, front })
                k.Box("Guard Ring", h, dir * gr + Vector3.forward * z, new Vector3(seg, 0.016f, 0.016f), chrome, new Vector3(0f, 0f, a), false, 0.9f, 0.8f);
            if (i % 2 == 0)
                k.Box("Guard Bar", h, dir * gr + Vector3.forward * ((back + front) * 0.5f), new Vector3(0.01f, 0.01f, front - back), chrome, new Vector3(0f, 0f, a), false, 0.9f, 0.8f);
        }
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f + 22.5f;
            Vector3 dir = Quaternion.Euler(0f, 0f, a) * Vector3.up;
            k.Box("Guard Spoke", h, dir * gr * 0.5f + Vector3.forward * front, new Vector3(0.008f, gr, 0.008f), chrome, new Vector3(0f, 0f, a), false, 0.9f, 0.8f);
        }
        k.Cyl("Badge", h, new Vector3(0f, 0f, front + 0.005f), D * 0.075f, 0.02f, chrome, new Vector3(90f, 0f, 0f), 0.95f, 0.85f);
        k.Prim(PrimitiveType.Cylinder, "Badge Glow", h, new Vector3(0f, 0f, front + 0.018f), new Vector3(D * 0.1f, 0.006f, D * 0.1f), badge, new Vector3(90f, 0f, 0f), 0f, 0.8f, false, badge);
    }

    /// <summary>A flat pie slice opening along +Z, lying on the XZ plane (faces up).</summary>
    public static Mesh WedgeMesh(float length, float fullAngle)
    {
        const int segments = 24;
        float half = fullAngle * 0.5f * Mathf.Deg2Rad;

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(-half, half, i / (float)segments);
            vertices[i + 1] = new Vector3(Mathf.Sin(a) * length, 0f, Mathf.Cos(a) * length);
        }
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        Mesh mesh = new Mesh { name = "Fan Wedge", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
