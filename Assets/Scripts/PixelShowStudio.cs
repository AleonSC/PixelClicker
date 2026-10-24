// ---- System notes ----
// PixelShowStudio.cs
//   ONE place for the private photo studio of a full-screen show (Dragon Find, God Find, Egg Hatch; 2026-10-24: was a copied block in each).
//   a root far below the scene with a transparent orthographic camera (switched off: the show calls Render() itself, once a frame) and a
//   point light. Pictures of a pixel type for windows use PixelTierPicture / PixelModelPicture instead - a show needs its own because it
//   moves, scales and adds parts to its model every frame.
//   - Create(name, depth, cameraDistance, farClip, lightPos, lightRange, lightIntensity, out cam): the root (at y = depth) + camera + light.
//   - AddPixelModel(clicker, tierIndex, parent): a 'Pivot' under parent holding the pixel type's real display model with its colliders
//     removed (a plain cube if there is no model), for the show to turn.
//   - Picture(name, size): a square transparent RenderTexture. Release(ref root, ref rt): destroys both.
//   (The find shows' screen layers - dark layer, backpack, title / subtitle - are in PixelShowScreen; their sparkles in PixelUISparks.)
// ---------------------------------------------------
using UnityEngine;

public static class PixelShowStudio
{
    /// <summary>A studio root at y = depth with a switched-off transparent orthographic camera 'cameraDistance' in front and a point light.</summary>
    public static GameObject Create(string name, float depth, float cameraDistance, float farClip,
                                    Vector3 lightPos, float lightRange, float lightIntensity, out Camera cam)
    {
        GameObject root = new GameObject(name);
        root.transform.position = new Vector3(0f, depth, 0f);

        GameObject camGo = new GameObject("Camera");
        camGo.transform.SetParent(root.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, -cameraDistance);
        cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.orthographic = true;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = farClip;
        cam.allowHDR = false;

        GameObject lightGo = new GameObject("Light");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = lightPos;
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = lightRange;
        light.intensity = lightIntensity;
        light.shadows = LightShadows.None;
        return root;
    }

    /// <summary>A 'Pivot' under parent holding the pixel type's display model (no colliders; a plain cube if it has none).</summary>
    public static Transform AddPixelModel(PixelClicker clicker, int tierIndex, Transform parent)
    {
        Transform pivot = new GameObject("Pivot").transform;
        pivot.SetParent(parent, false);
        GameObject model = clicker != null ? clicker.CreateDisplayPixel(tierIndex, pivot, 1f) : null;
        if (model == null)
        {
            // No model (should not happen): a plain cube so the show still works.
            model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.SetParent(pivot, false);
        }
        foreach (Collider col in model.GetComponentsInChildren<Collider>()) Object.Destroy(col);
        model.transform.localPosition = Vector3.zero;
        return pivot;
    }

    /// <summary>A square transparent picture for the studio camera.</summary>
    public static RenderTexture Picture(string name, int size = 512) =>
        new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { name = name };

    /// <summary>Destroys the studio and its picture.</summary>
    public static void Release(ref GameObject root, ref RenderTexture rt)
    {
        if (root != null) Object.Destroy(root);
        if (rt != null) { rt.Release(); Object.Destroy(rt); }
        root = null; rt = null;
    }
}
