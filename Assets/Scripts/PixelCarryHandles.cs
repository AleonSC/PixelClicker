// ---- System notes ----
// PixelCarryHandles.cs
//   Shared carrying handles for placed things (first used by the Storage Crate, now also the Recycler):
//   - BuildParts(part, halfWidth, depth, y, size, opacity): the model - on each short (+/-X) end two arms and a grip bar, all parts named "Handle",
//     made through the caller's own 'part' callback (so each model keeps its own materials / PixelFactoryKit). 'size' = how far they stick out.
//   - The component (AddComponent on the root, then Init): finds the child renderers named "Handle"; hovering one shows the open-hand cursor
//     (PixelHandCursor), left-drag moves the root over the floor plane (kept on screen) with the fist cursor, scroll / Q / E turn it while dragging
//     (PixelConsumables.TurnInput). Over(ray) / Dragging tell the owner the pointer is the handles'; static Hot (hovering or dragging any handles)
//     is part of PixelClicker.ExternalClickBlock and PixelGrab's guard. 'Blocked' (a Func the owner sets) pauses it, e.g. while the Recycler works.
//   The position / yaw are the root's own, so the saved PlacedState picks up where it was carried to.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Two grip handles on a placed thing's short ends: hover for the open hand, left-drag to carry it over the floor, scroll / Q / E to turn it while carrying.</summary>
public class PixelCarryHandles : MonoBehaviour
{
    [Tooltip("How close to the screen edges (viewport share) the carried thing may go: it is never dragged off the screen.")]
    [SerializeField] private Vector2 screenMargin = new Vector2(0.04f, 0.1f);

    private static int hotFrame = -10;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { hotFrame = -10; }

    /// <summary>True while the pointer is over (or dragging by) any carry handles: clicks there belong to the handles, not the cube or Pixel Grabbing.</summary>
    public static bool Hot => hotFrame >= Time.frameCount - 1;

    /// <summary>The owner can pause the handles (true = no hover, no drag), e.g. while a machine is busy.</summary>
    public Func<bool> Blocked;

    /// <summary>True while the player is carrying this by a handle.</summary>
    public bool Dragging => dragging;

    private readonly List<Renderer> handles = new List<Renderer>();
    private Func<Camera> camera;
    private PixelConsumables consumables;
    private bool dragging;
    private Vector3 dragOffset;

    /// <summary>Builds the handle parts: on each short (+/-X) end two arms and a grip bar, named "Handle". 'halfWidth' = where the end faces are,
    /// 'depth' = the thing's depth along Z, 'y' = the height of the handles, 'size' = how far they stick out (the Storage Crate uses 0.34).</summary>
    public static void BuildParts(Action<string, Vector3, Vector3, Color> part, float halfWidth, float depth, float y, float size, float opacity = 1f)
    {
        Color grip = new Color(0.25f, 0.25f, 0.3f, opacity), gripLight = new Color(0.7f, 0.72f, 0.78f, opacity);
        float arm = size * 0.206f, bar = size * 0.265f;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            float mid = sx * (halfWidth + size * 0.5f), outer = sx * (halfWidth + size);
            part("Handle", new Vector3(mid, y, -depth * 0.22f), new Vector3(size, arm, arm), grip);
            part("Handle", new Vector3(mid, y, depth * 0.22f), new Vector3(size, arm, arm), grip);
            part("Handle", new Vector3(outer, y, 0f), new Vector3(bar, bar, depth * 0.44f + bar), gripLight);
        }
    }

    /// <summary>Starts working: finds the "Handle" parts under this object. 'cam' gives the camera to use (the clicker's).</summary>
    public void Init(Func<Camera> cam)
    {
        camera = cam;
        handles.Clear();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (r.name == "Handle") handles.Add(r);
    }

    /// <summary>Is this ray on one of the handles?</summary>
    public bool Over(Ray ray)
    {
        foreach (Renderer r in handles)
            if (r != null && r.enabled && r.bounds.IntersectRay(ray, out _)) return true;
        return false;
    }

    private Camera Cam => camera != null && camera() != null ? camera() : Camera.main;

    private void Update()
    {
        if (handles.Count == 0) return;
        Camera cam = Cam;
        bool blocked = Blocked != null && Blocked();
        if (cam == null || blocked || Time.timeScale <= 0f || PixelPauseMenu.IsPaused) { dragging = false; return; }

        if (dragging)
        {
            if (!PixelInput.LeftHeld()) { dragging = false; return; }
            hotFrame = PixelPointerClaim.Claim();
            PixelHandCursor.Request(PixelHandCursor.Shape.Closed);
            if (consumables == null) consumables = PixelFind.First<PixelConsumables>();
            float turn = consumables != null ? consumables.TurnInput() : 0f;
            if (!Mathf.Approximately(turn, 0f))
            {
                transform.Rotate(0f, turn, 0f, Space.World);
                dragOffset = Quaternion.Euler(0f, turn, 0f) * dragOffset;   // the grabbed point swings round the centre with it
            }
            DragTo(cam, cam.ScreenPointToRay(PixelInput.PointerPosition()));
            return;
        }
        if (PixelInput.PointerOverUI()) return;
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        if (!Over(ray)) return;
        hotFrame = PixelPointerClaim.Claim();
        PixelHandCursor.Request(PixelHandCursor.Shape.Open);
        if (PixelInput.LeftPressed() && FloorPoint(ray, out Vector3 hit))
        {
            dragging = true;
            dragOffset = transform.position - hit;
        }
    }

    private bool FloorPoint(Ray ray, out Vector3 point)
    {
        point = default;
        Plane plane = new Plane(Vector3.up, transform.position);
        if (!plane.Raycast(ray, out float enter)) return false;
        point = ray.GetPoint(enter);
        return true;
    }

    private void DragTo(Camera cam, Ray ray)
    {
        if (!FloorPoint(ray, out Vector3 hit)) return;
        Vector3 target = hit + dragOffset;
        target.y = transform.position.y;
        Vector3 vp = cam.WorldToViewportPoint(target);
        if (vp.z <= 0f || vp.x < screenMargin.x || vp.x > 1f - screenMargin.x || vp.y < screenMargin.y || vp.y > 1f - screenMargin.y) return;   // never off the screen
        transform.position = target;
    }
}
