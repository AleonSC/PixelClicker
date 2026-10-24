// ---- System notes ----
// PixelClickerPointer.cs
//   Part of PixelClicker (partial class): what is under the pointer (TryGetBlockAt, IsLiveCubeAt, the shared once-a-frame PointerHits ray, GetLiveProgress) and the mouse input reads (WasClickedThisFrame, PointerPosition). The system notes are in PixelClicker.cs.
// ---------------------------------------------------
using Unity.Profiling;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public partial class PixelClicker
{
    /// <summary>
    /// What block is under the pointer? The nearest of the clickable cube and the old pixels lying around (used by the hover tooltip).
    /// isLive = the clickable cube; otherwise 'body' is the old pixel.
    /// </summary>
    public bool TryGetBlockAt(Vector2 screen, out int tierIndex, out bool isLive, out Rigidbody body)
    {
        tierIndex = -1; isLive = false; body = null;
        if (targetCamera == null || pixelTransform == null || tiers == null || tiers.Length == 0) return false;
        RaycastHit[] hits = PointerHits(screen, out int count);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (PixelRobotWorker.BelongsToUfo(hit.transform)) continue;
            if (hit.transform == hitbox || hit.transform == pixelTransform || hit.transform.IsChildOf(pixelTransform))
            {
                if (isSpawning || CubeHidden) return false;
                tierIndex = GetClickTierIndex();
                isLive = true;
                return true;
            }
            Rigidbody rb = hit.rigidbody;
            if (rb != null && (oldPixels.Contains(rb) || PixelRecycler.Holds(rb)))   // (husks in a Recycler left the old-pixel list but still show their tooltip)
            {
                OldPixelInfo info = PixelHuskQuery.Get(rb).info;
                if (info == null || info.tierIndex < 0 || info.tierIndex >= tiers.Length) return false;
                tierIndex = info.tierIndex;
                body = rb;
                return true;
            }
        }
        return false;
    }

    /// <summary>Is the clickable cube under this screen point, even with old pixels lying / falling in front of it (Block Hold: a pixel dropped by clearing a hazard must not stop you grabbing the next block)?</summary>
    public bool IsLiveCubeAt(Vector2 screen)
    {
        if (targetCamera == null || pixelTransform == null || isSpawning || CubeHidden) return false;
        RaycastHit[] hits = PointerHits(screen, out int count);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (PixelRobotWorker.BelongsToUfo(hit.transform)) continue;
            if (hit.transform == hitbox || hit.transform == pixelTransform || hit.transform.IsChildOf(pixelTransform)) return true;
        }
        return false;
    }

    // ---- the shared pointer ray ----
    // The tooltip, aim cursor, Block Hold and Power Click each asked "what is under the pointer?" every frame, each with its own Physics.RaycastAll
    // (a new array every time) and sort. PointerHits does the cast ONCE per frame per screen point into a reused buffer, nearest first.

    private RaycastHit[] pointerHits = new RaycastHit[64];
    private int pointerHitCount, pointerHitFrame = -1;
    private Vector2 pointerHitScreen;
    private Camera pointerHitCamera;

    /// <summary>
    /// Everything on the clickable layers under this screen point (triggers included), nearest first: hits[0 .. count-1].
    /// Cached for the rest of the frame (same point, same camera). The array is shared and reused: read it right away, do not keep it.
    /// </summary>
    public RaycastHit[] PointerHits(Vector2 screen, out int count)
    {
        if (pointerHitFrame == Time.frameCount && pointerHitScreen == screen && pointerHitCamera == targetCamera)
        {
            // Something hit earlier this frame may have been destroyed since (Unity destroys objects between Update and LateUpdate): its hit
            // then has no collider / transform, so it is dropped here (keeping the order) instead of every caller checking for null.
            int kept = 0;
            for (int i = 0; i < pointerHitCount; i++)
            {
                if (pointerHits[i].collider == null) continue;
                if (kept != i) pointerHits[kept] = pointerHits[i];
                kept++;
            }
            pointerHitCount = count = kept;
            return pointerHits;
        }
        count = 0;
        if (targetCamera != null)
        {
            Ray ray = targetCamera.ScreenPointToRay(screen);
            count = Physics.RaycastNonAlloc(ray, pointerHits, maxRayDistance, clickableLayers, QueryTriggerInteraction.Collide);
            if (count == pointerHits.Length)   // (full: there may be more - cast again into a bigger buffer)
            {
                pointerHits = new RaycastHit[pointerHits.Length * 2];
                count = Physics.RaycastNonAlloc(ray, pointerHits, maxRayDistance, clickableLayers, QueryTriggerInteraction.Collide);
            }
            for (int i = 1; i < count; i++)   // nearest first (a few hits: a simple insertion sort, nothing allocated)
            {
                RaycastHit h = pointerHits[i];
                int j = i - 1;
                while (j >= 0 && pointerHits[j].distance > h.distance) { pointerHits[j + 1] = pointerHits[j]; j--; }
                pointerHits[j + 1] = h;
            }
        }
        pointerHitFrame = Time.frameCount;
        pointerHitScreen = screen;
        pointerHitCamera = targetCamera;
        pointerHitCount = count;
        return pointerHits;
    }

    /// <summary>Hits already put into the pixel on the cube, and how many it needs (for the hover tooltip).</summary>
    public void GetLiveProgress(out int hits, out int needed)
    {
        hits = hitsOnCurrentPixel;
        needed = tiers != null && tiers.Length > 0 ? NeededClicks(GetClickTier()) : 1;
    }

    // ------------------------------------------------------------------
    // Input
    // ------------------------------------------------------------------

    private bool WasClickedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private Vector2 PointerPosition()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current.position.ReadValue();
#else
        return Input.mousePosition;
#endif
    }
}
