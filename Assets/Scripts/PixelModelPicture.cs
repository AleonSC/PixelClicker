// ---- System notes ----
// PixelModelPicture.cs
//   Shared 'real model' pictures of ANY built model (the general form of PixelTierPicture, which only does pixel types): Get(key, build, resolution)
//   builds the model once with 'build' in a hidden studio far below the scene (its own camera + light, the camera FITTED to the model's bounds so
//   anything from a potion to a Recycler fills the picture) and returns its RenderTexture; Render(key, yaw) turns it and redraws it (at most
//   PixelTierPicture.MaxFps a second - call it only for pictures on screen); Release(key) / ReleaseAll(prefix) destroy studios that are no longer
//   needed (a closed window). First user: the 3D consumables window (PixelConsumableGallery). The shop's Operation studios could move onto this.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class PixelModelPicture
{
    private class Studio
    {
        public GameObject root;
        public Transform model;
        public Camera cam;
        public RenderTexture rt;
        public float lastDrawn = -1f;
    }

    private const float StudioHeight = -6400f;   // apart from the other studios (-3000 .. -5200)
    private static readonly Dictionary<string, Studio> studios = new Dictionary<string, Studio>();
    private static int nextSlot;
    private static readonly List<string> scratch = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { studios.Clear(); nextSlot = 0; }

    /// <summary>The picture for 'key' (built with 'build' on first use; null when build gives nothing). 'tilt' = how far it leans towards the camera.</summary>
    public static Texture Get(string key, System.Func<Transform, GameObject> build, int resolution = 256, float tilt = 22f)
    {
        if (studios.TryGetValue(key, out Studio existing))
        {
            if (existing.rt != null && existing.root != null)
            {
                if (existing.rt.width < resolution) { existing.rt.Release(); existing.rt.width = existing.rt.height = resolution; existing.rt.Create(); existing.lastDrawn = -1f; }
                return existing.rt;
            }
            Release(key);
        }

        GameObject root = new GameObject("Model Picture Studio " + key);
        root.transform.position = new Vector3((nextSlot++ % 200) * 80f, StudioHeight, 0f);
        GameObject model = build != null ? build(root.transform) : null;
        if (model == null) { Object.Destroy(root); return null; }
        model.transform.SetParent(root.transform, false);
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);   // (a picture never takes part in the game's physics)

        // Centre the model on the studio and fit the camera to its size.
        Bounds b = new Bounds(model.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        model.transform.position -= b.center - root.transform.position;
        float radius = any ? Mathf.Max(0.05f, b.extents.magnitude) : 0.6f;

        Studio st = new Studio { root = root, model = model.transform };
        st.rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32) { name = "Model Picture " + key };
        GameObject camGo = new GameObject("Picture Camera");
        camGo.transform.SetParent(root.transform, false);
        st.cam = camGo.AddComponent<Camera>();
        st.cam.enabled = false;
        st.cam.clearFlags = CameraClearFlags.SolidColor;
        st.cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        st.cam.orthographic = true;
        st.cam.orthographicSize = radius * 1.02f;
        st.cam.nearClipPlane = 0.05f;
        st.cam.farClipPlane = radius * 8f + 10f;
        st.cam.allowHDR = false;
        st.cam.targetTexture = st.rt;
        camGo.transform.localPosition = new Vector3(0f, 0f, radius * 4f + 1f);
        camGo.transform.LookAt(root.transform.position);

        GameObject lightGo = new GameObject("Picture Light");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(radius * 2f, radius * 3f, radius * 5f);
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = radius * 20f + 10f;
        l.intensity = 6f;
        l.shadows = LightShadows.None;

        st.model.localRotation = Quaternion.Euler(tilt, 0f, 0f);
        studios[key] = st;
        return st.rt;
    }

    /// <summary>Turns the model to 'yawDegrees' (with its lean) and redraws it - at most PixelTierPicture.MaxFps a second.</summary>
    public static void Render(string key, float yawDegrees, float tilt = 22f)
    {
        if (!studios.TryGetValue(key, out Studio st) || st.rt == null || st.model == null || st.cam == null) return;
        if (!PixelTierPicture.Due(ref st.lastDrawn)) return;
        st.model.localRotation = Quaternion.Euler(tilt, 0f, 0f) * Quaternion.Euler(0f, yawDegrees, 0f);
        st.cam.Render();
    }

    /// <summary>Destroys one studio (its model, camera and texture).</summary>
    public static void Release(string key)
    {
        if (!studios.TryGetValue(key, out Studio st)) return;
        studios.Remove(key);
        if (st.cam != null) st.cam.targetTexture = null;
        if (st.rt != null) { st.rt.Release(); Object.Destroy(st.rt); }
        if (st.root != null) Object.Destroy(st.root);
    }

    /// <summary>Destroys every studio whose key starts with 'prefix' (all of a window's pictures).</summary>
    public static void ReleaseAll(string prefix)
    {
        scratch.Clear();
        foreach (string k in studios.Keys) if (k.StartsWith(prefix)) scratch.Add(k);
        foreach (string k in scratch) Release(k);
        scratch.Clear();
    }
}
