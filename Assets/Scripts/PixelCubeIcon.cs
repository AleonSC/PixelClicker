using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A tiny spinning 3D cube drawn straight into the UI (no camera, render texture or lights needed).
/// Used as the icon of each achievement. Set 'color' like on any UI image; a see-through colour gives a glass cube.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class PixelCubeIcon : MaskableGraphic
{
    /// <summary>Spin speed in degrees per second.</summary>
    public float spinDegreesPerSecond = 70f;

    /// <summary>Fixed forward tilt so the top face is visible.</summary>
    public float tiltDegrees = 25f;

    /// <summary>How much of the rect the cube fills (0..1).</summary>
    public float fill = 0.62f;

    // Unit-cube faces: normal plus the two axes that span the face.
    private static readonly Vector3[] Normals =
    {
        Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
    };

    private static readonly Vector3 LightDirection = new Vector3(-0.4f, 0.8f, -0.6f).normalized;

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
    }

    private void Update()
    {
        SetVerticesDirty(); // redraw with the new angle
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        Vector2 centre = rect.center;
        float scale = Mathf.Min(rect.width, rect.height) * 0.5f * fill;

        float yaw = Application.isPlaying ? Time.unscaledTime * spinDegreesPerSecond : 30f;
        // Spin around the cube's own vertical axis first, then tip the top toward the viewer.
        Quaternion rotation = Quaternion.Euler(-tiltDegrees, 0f, 0f) * Quaternion.Euler(0f, yaw, 0f);

        UIVertex[] quad = new UIVertex[4];
        foreach (Vector3 normal in Normals)
        {
            Vector3 n = rotation * normal;
            if (n.z > 0f) continue; // faces pointing away from the viewer (z increases away from the screen)

            // Two axes spanning this face.
            Vector3 u = normal.x != 0f ? Vector3.up : Vector3.right;
            Vector3 v = Vector3.Cross(normal, u);

            Vector3[] corners =
            {
                normal + u + v, normal + u - v, normal - u - v, normal - u + v
            };

            float light = 0.5f + 0.5f * Mathf.Max(0f, Vector3.Dot(n, LightDirection));
            Color faceColor = new Color(color.r * light, color.g * light, color.b * light, color.a);

            for (int i = 0; i < 4; i++)
            {
                Vector3 p = rotation * corners[i];
                quad[i] = UIVertex.simpleVert;
                quad[i].position = new Vector3(centre.x + p.x * scale, centre.y + p.y * scale, 0f);
                quad[i].color = faceColor;
            }
            vh.AddUIVertexQuad(quad);
        }
    }
}
