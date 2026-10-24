// ---- System notes ----
// PixelDevToolsGod.cs
//   Part of PixelDevTools (partial class): god pixel mode (scroll = pick a pixel, left click = spawn, right click = destroy) and its on-screen display. The system notes are in PixelDevTools.cs.
// ---------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class PixelDevTools
{
    // ------------------------------------------------------------------
    // God pixel mode: scroll = pick a pixel, left click = spawn, right click = destroy
    // ------------------------------------------------------------------

    private GameObject godRoot;
    private RectTransform godRect, godCanvasRect;
    private TMP_Text godLabel;
    private Image godSwatch;
    private readonly List<int> godTiers = new List<int>();
    private int godChoice;
    private float godSpawnTimer;

    private void SetGodMode(bool on)
    {
        godMode = on;
        SetBool("God", on);
        PixelClicker.GodMode = on;
        if (clicker == null) return;
        if (on)
        {
            if (godRoot == null) BuildGodDisplay();
            godChoice = Mathf.Clamp(GetInt("GodChoice", 0), 0, 999);
        }
        if (godRoot != null) godRoot.SetActive(on);
    }

    private void BuildGodDisplay()
    {
        godRoot = PixelUIKit.CreateCanvas("PixelDevTools God Pixel", 146, referenceResolution, false);
        godRoot.transform.SetParent(transform, false);
        godCanvasRect = godRoot.GetComponent<RectTransform>();

        GameObject holder = new GameObject("Display", typeof(RectTransform));
        holder.transform.SetParent(godRoot.transform, false);
        godRect = holder.GetComponent<RectTransform>();
        godRect.anchorMin = godRect.anchorMax = godRect.pivot = new Vector2(0f, 1f);
        godRect.sizeDelta = new Vector2(10f, 10f);

        GameObject sw = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
        sw.transform.SetParent(holder.transform, false);
        godSwatch = sw.GetComponent<Image>();
        godSwatch.raycastTarget = false;
        RectTransform sr = sw.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 1f);
        sr.sizeDelta = new Vector2(godFontSize, godFontSize);
        sr.anchoredPosition = Vector2.zero;

        godLabel = PixelUIKit.CreateText(font, holder.transform, "Label", "", godFontSize, TextAlignmentOptions.MidlineLeft,
                                         FontStyles.Bold, textColor);
        godLabel.overflowMode = TextOverflowModes.Overflow;
        RectTransform lr = godLabel.rectTransform;
        lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0f, 1f);
        lr.sizeDelta = new Vector2(400f, godFontSize * 1.2f);
        lr.anchoredPosition = new Vector2(godFontSize + 8f, 0f);
    }

    private void UpdateGod()
    {
        if (!godMode || clicker == null || godRoot == null) return;
        bool active = !PixelCalm.InMenu && !IsOpen;
        if (godRoot.activeSelf != active) godRoot.SetActive(active);
        if (!active) return;

        // The pixels that can be spawned (fly-away ones can't).
        godTiers.Clear();
        for (int i = 0; i < clicker.Tiers.Length; i++)
            if (!clicker.Tiers[i].flyAway) godTiers.Add(i);
        if (godTiers.Count == 0) return;
        godChoice = ((godChoice % godTiers.Count) + godTiers.Count) % godTiers.Count;

        Vector2 pointer = PixelInput.PointerPosition();
        bool overUI = PixelInput.PointerOverUI();

        // Scroll to choose.
        float scroll = overUI ? 0f : PixelInput.ScrollY();
        if (Mathf.Abs(scroll) > 0.01f)
        {
            godChoice += scroll > 0f ? -1 : 1;
            godChoice = ((godChoice % godTiers.Count) + godTiers.Count) % godTiers.Count;
            SetInt("GodChoice", godChoice);
            PixelAudio.Play("bank_select");
        }

        PixelClicker.PixelTier tier = clicker.Tiers[godTiers[godChoice]];

        // The little display next to the cursor.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(godCanvasRect, pointer, null, out Vector2 local))
            godRect.anchoredPosition = local - new Vector2(godCanvasRect.rect.xMin, godCanvasRect.rect.yMax) + godDisplayOffset; // relative to the top-left anchor
        godSwatch.color = tier.UIColor;
        PixelUIKit.SetText(godLabel, tier.displayName);

        if (overUI || PixelBank.HoseOn) return;

        Camera cam = clicker.ViewCamera;
        if (cam == null) return;

        // Left: spawn old pixels at the cursor (hold to keep spawning).
        if (PixelInput.LeftPressed()) godSpawnTimer = 0f;
        if (PixelInput.LeftHeld())
        {
            godSpawnTimer -= Time.unscaledDeltaTime;
            if (godSpawnTimer <= 0f)
            {
                godSpawnTimer = 1f / godSpawnRate;
                Transform cube = clicker.PixelTransform;
                Vector3 anchor = cube != null ? cube.position : cam.transform.position + cam.transform.forward * 10f;
                Plane plane = new Plane(-cam.transform.forward, anchor);
                Ray ray = cam.ScreenPointToRay(pointer);
                Vector3 spawnAt = Vector3.zero;
                bool haveSpot = false;
                // Drop it above whatever the cursor is over (the floor, a hole), so it falls where it was pointed - not at the cube's depth.
                RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider.GetComponentInParent<OldPixelInfo>() != null) continue;
                    if (cube != null && hit.transform.IsChildOf(cube)) continue;
                    if (hit.normal.y < 0.5f) continue;
                    spawnAt = hit.point + Vector3.up * (clicker.PixelBaseSize * 2f);
                    haveSpot = true;
                    break;
                }
                if (!haveSpot && plane.Raycast(ray, out float enter)) { spawnAt = ray.GetPoint(enter); haveSpot = true; }
                if (haveSpot) clicker.SpawnStoredPixel(godTiers[godChoice], tier.amountPerClick, spawnAt, Random.insideUnitSphere * 0.3f);
            }
        }

        // Right: destroy old pixels near the cursor (no payout).
        if (PixelInput.RightHeld() || PixelInput.RightPressed())
        {
            var list = clicker.OldPixels;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Rigidbody rb = list[i];
                if (rb == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(rb.position);
                if (sp.z < 0f || ((Vector2)sp - pointer).sqrMagnitude > godEraseRadius * godEraseRadius) continue;
                clicker.ReleaseOldPixel(rb, false);
                Destroy(rb.gameObject);
            }
        }
    }
}
