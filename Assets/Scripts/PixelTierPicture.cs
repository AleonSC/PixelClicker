// ---- System notes ----
// PixelTierPicture.cs
//   Shared 'real model' pictures of pixel types: PixelTierPicture.Get(clicker, tier) returns a RenderTexture of the block drawn with its REAL look (PixelClicker.CreateDisplayPixel in a
//   hidden studio far below the scene with its own camera and light), PixelTierPicture.Render(tier, yaw) turns it and re-renders it (call it only for pictures that are on screen).
//   Rebuild(clicker, tier) builds the model again after its look changed (Look Studio); asking for a bigger resolution resizes the same texture in place.
//   First user: the Next-up window (PixelNextUp). The Pixel Guide, the shop pictures, the Log skin studio and the shop operation studio each still have their own copy of this studio code:
//   move them onto this helper when they are next touched (one place, one set of heights).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class PixelTierPicture
{
    private class Studio
    {
        public GameObject root;
        public Transform model;
        public Camera cam;
        public RenderTexture rt;
    }

    private const float StudioHeight = -5200f;   // apart from the other studios (-3000 .. -4300)
    private static readonly Dictionary<int, Studio> studios = new Dictionary<int, Studio>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { studios.Clear(); }

    /// <summary>The picture of this block (built on first use). Null if the block has no model.</summary>
    public static Texture Get(PixelClicker clicker, int tier, int resolution = 160)
    {
        if (clicker == null) return null;
        if (studios.TryGetValue(tier, out Studio existing))
        {
            if (existing.rt != null && existing.root != null)
            {
                // A bigger picture was asked for: resize the SAME texture (pictures already showing it keep working, just sharper).
                if (existing.rt.width < resolution) { existing.rt.Release(); existing.rt.width = existing.rt.height = resolution; existing.rt.Create(); }
                return existing.rt;
            }
            if (existing.rt != null) existing.rt.Release();
            studios.Remove(tier);
        }

        GameObject root = new GameObject("Tier Picture Studio " + tier);
        root.transform.position = new Vector3(tier * 60f, StudioHeight, 0f);
        GameObject model = clicker.CreateDisplayPixel(tier, root.transform, 1f);
        if (model == null) { Object.Destroy(root); return null; }
        model.transform.localPosition = Vector3.zero;

        Studio st = new Studio { root = root, model = model.transform };
        st.rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32) { name = "Tier Picture " + tier };
        GameObject camGo = new GameObject("Picture Camera");
        camGo.transform.SetParent(root.transform, false);
        st.cam = camGo.AddComponent<Camera>();
        st.cam.enabled = false;
        st.cam.clearFlags = CameraClearFlags.SolidColor;
        st.cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        st.cam.orthographic = true;
        st.cam.orthographicSize = 0.85f;
        st.cam.nearClipPlane = 0.1f;
        st.cam.farClipPlane = 30f;
        st.cam.allowHDR = false;
        st.cam.targetTexture = st.rt;
        camGo.transform.localPosition = new Vector3(0f, 0f, 4.4f);
        camGo.transform.LookAt(root.transform.position);

        GameObject lightGo = new GameObject("Picture Light");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(2f, 3f, 5f);
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 25f;
        l.intensity = 6f;
        l.shadows = LightShadows.None;

        studios[tier] = st;
        return st.rt;
    }

    /// <summary>Builds the block's model again so the picture shows its look as it is NOW (the Look Studio calls it after every edit). Nothing happens if the picture was never made.</summary>
    public static void Rebuild(PixelClicker clicker, int tier)
    {
        if (clicker == null || !studios.TryGetValue(tier, out Studio st) || st.root == null) return;
        if (st.model != null) { st.model.gameObject.SetActive(false); Object.Destroy(st.model.gameObject); }   // hidden at once: Destroy only happens at the end of the frame
        GameObject model = clicker.CreateDisplayPixel(tier, st.root.transform, 1f);
        if (model != null) model.transform.localPosition = Vector3.zero;
        st.model = model != null ? model.transform : null;
    }

    /// <summary>Turns the model to 'yawDegrees' (with the usual forward tilt) and renders it.</summary>
    public static void Render(int tier, float yawDegrees)
    {
        if (!studios.TryGetValue(tier, out Studio st) || st.rt == null || st.model == null || st.cam == null) return;
        st.model.localRotation = Quaternion.Euler(25f, 0f, 0f) * Quaternion.Euler(0f, yawDegrees, 0f);
        st.cam.Render();
    }
}
