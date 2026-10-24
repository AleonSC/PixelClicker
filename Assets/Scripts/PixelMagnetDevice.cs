// ---- System notes (moved here from CLAUDE.md) ----
// PixelMagnetDevice.cs
//   (extends PixelPlacedDevice) Magnet (DeviceKind.Magnet 10, auto-added like the fan by EnsureDefaultPotions, Utilities card; needs
//   Electric unlocked, 20 Electric pixels, durationSeconds 30, radius 5, pullAcceleration 24, absorbDistance 0.9 = the 'settle'
//   distance): like the Vacuum Device it pulls loose old pixels in radius towards a point in the gap of its horseshoe
//   (model: BuildModel in this file: red arch + legs, silver tips, base plate), but never collects them: pixels inside the settle distance just get
//   their speed damped, so they sit in a heap (held / flying / vanishing / kinematic pixels are skipped, ageing is unchanged); placed
//   like the vacuum (starts disarmed, click to switch on), robot-placeable (IsPlaceableKind), tip device_Magnet, stat
//   device.placed.Magnet, not part of the Operation tab yet.
// ---------------------------------------------------
using TMPro;
using UnityEngine;

/// <summary>
/// A placed Magnet (device <c>kind</c> = Magnet): works like the Vacuum Device but never consumes anything. While it is switched on it pulls
/// every loose old pixel inside its radius towards the gap of its horseshoe and holds them there in a heap (they keep their value and keep
/// ageing normally). Pixels that are held, flying away or frozen by a minigame are left alone. The countdown, timer text and shrink-away come
/// from <see cref="PixelPlacedDevice"/>; the model is built by <see cref="PixelConsumables"/>.
/// </summary>
public class PixelMagnetDevice : PixelPlacedDevice
{
    private Transform pullPoint;
    private float radius;
    private float pull;
    private float holdDistance;
    private float drop;

    public void Init(PixelClicker owner, Camera camera, Transform topPoint, TextMeshPro timer, string format, float duration,
                     float reach, float pullAcceleration, float holdRadius, float shrinkTime, float bodyHeight)
    {
        InitCommon(owner, camera, timer, format, duration, shrinkTime);
        pullPoint = topPoint;
        radius = reach;
        pull = pullAcceleration;
        holdDistance = Mathf.Max(0.1f, holdRadius);
        drop = bodyHeight * 0.55f;   // the pull centre sits in the gap of the horseshoe, not at its top
    }

    private void FixedUpdate()
    {
        if (IsEnding || !Armed || clicker == null || pullPoint == null) return;
        Vector3 target = pullPoint.position + Vector3.down * drop;
        var pixels = clicker.OldPixels;
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            Rigidbody body = pixels[i];
            if (!PixelHuskHolder.Takeable(clicker, body) || !Accepts(body)) continue;   // (one owner per husk: held, reserved or kept by another machine = not ours)

            Vector3 to = target - body.position;
            float distance = to.magnitude;
            if (distance > radius) continue;

            if (distance <= holdDistance)
            {
                // Close to the gap: settle and stay (a drag instead of more pull, so the heap doesn't jitter).
#if UNITY_6000_0_OR_NEWER
                body.linearVelocity *= 0.9f;
#else
                body.velocity *= 0.9f;
#endif
            }
            else body.AddForce(to / distance * pull, ForceMode.Acceleration);
        }
    }

    // ---- the device card (PixelDeviceCard) ----
    protected override void AddInfo(PixelDeviceInfo info)
    {
        info.Add("Pulls", "husks within " + radius.ToString("0.#") + " units into a heap");
        if (!info.Full) return;
        info.Add("Pull", pull.ToString("0.#"));
    }

    // ---- The model (moved here from PixelConsumables 2026-10-24: each device builds its own look; PixelConsumables.BuildDeviceObject calls it) ----

    /// <summary>An upright horseshoe magnet standing on its two silver tips: a red arch, two legs and a small base plate.</summary>
    public static void BuildModel(PixelClicker clicker, GameObject root, PixelConsumables.Device d, bool isPreview, float previewOpacity)
    {
        float opacity = isPreview ? previewOpacity : 1f;
        Color red = d.color; red.a = opacity;
        Color silver = new Color(0.78f, 0.8f, 0.86f, opacity);
        Color dark = new Color(0.25f, 0.27f, 0.32f, opacity);
        float D = d.bodyDiameter, H = d.bodyHeight, leg = D * 0.26f;

        GameObject Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color) =>
            PixelFactoryKit.ModelPart(clicker, type, name, root.transform, pos, scale, color, isPreview);

        Part(PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.025f, 0f), new Vector3(D * 1.3f, 0.025f, D * 1.3f), dark);
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * D * 0.4f;
            Part(PrimitiveType.Cube, "Leg", new Vector3(x, H * 0.5f, 0f), new Vector3(leg, H * 0.78f, leg), red);
            Part(PrimitiveType.Cube, "Tip", new Vector3(x, H * 0.12f, 0f), new Vector3(leg * 1.04f, H * 0.24f, leg * 1.04f), silver);
        }
        Part(PrimitiveType.Cube, "Arch", new Vector3(0f, H - leg * 0.5f, 0f), new Vector3(D * 0.8f + leg, leg, leg), red);
        Part(PrimitiveType.Cube, "Arch Cap", new Vector3(0f, H + leg * 0.02f, 0f), new Vector3(D * 0.8f + leg * 0.6f, leg * 0.25f, leg * 0.7f), silver);
    }
}
