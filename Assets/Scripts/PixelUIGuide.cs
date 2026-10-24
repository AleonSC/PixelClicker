// ---- System notes ----
// PixelUIGuide.cs
//   Part of PixelUI (partial class): the tier guide at the top of the screen (unlock message, BuildGuideText / UpdateGuide) and the active potion timer HUD. The system notes are in PixelUI.cs.
// ---------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public partial class PixelUI
{
    // ------------------------------------------------------------------
    // Active potion timer (top of the screen)
    // ------------------------------------------------------------------

    // ------------------------------------------------------------------
    // Tier guide / unlock message
    // ------------------------------------------------------------------

    private void BuildGuide()
    {
        // Own canvas, sorted below the shop (150) so the shop panel covers the guide.
        guideCanvasRoot = PixelUIKit.CreateCanvas("PixelUI Guide", guideSortingOrder, referenceResolution, false);

        // Box behind the text; resized to fit the text in UpdateGuide.
        guideRoot = new GameObject("Tier Guide", typeof(RectTransform), typeof(Image));
        guideRoot.transform.SetParent(guideCanvasRoot.transform, false);
        guideBackground = guideRoot.GetComponent<Image>();
        guideBackground.raycastTarget = false;

        RectTransform rr = guideRoot.GetComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 1f);
        rr.anchoredPosition = new Vector2(0f, -(guideTopMargin + (PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f)));

        guideLabel = MakeText(guideRoot.transform, "Text", "", guideTextSize,
                              TextAlignmentOptions.Center, FontStyles.Bold, guideColor);
        if (popupOutlineWidth > 0f)
        {
            guideLabel.outlineColor = popupOutlineColor;
            guideLabel.outlineWidth = popupOutlineWidth;
        }

        RectTransform rt = guideLabel.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        guideRoot.SetActive(false);
    }

    private void OnTierUnlocked(int index)
    {
        if (index < 0 || index >= clicker.Tiers.Length) return;

        PixelClicker.PixelTier tier = clicker.Tiers[index];
        if (!announceShopUnlocks && tier.unlockMode == PixelClicker.TierUnlockMode.ShopOnly) return;

        unlockMessage = string.Format(unlockFormat, tier.type);
        // Lightened so dark tiers (Black, Obsidian) stay readable.
        unlockColor = Color.Lerp(tier.UIColor, Color.white, 0.5f);
        unlockTimer = unlockSeconds;
    }

    /// <summary>What the player should do next, or null when there is nothing to say.</summary>
    private string BuildGuideText()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        int stages = Mathf.Min(guideTierCount, tiers.Length);

        // The early goals ARE the unlocks (PixelGoals): while one is showing, the line follows it (the progression goal first) - 'Crush 10 husks (3 / 10) - Gray unlock'.
        PixelGoals goalTable = PixelGoals.Instance;
        PixelGoals.GoalDef goal = goalTable != null ? goalTable.CurrentGuideGoal() : null;
        if (goal != null)
        {
            string reward = goalTable.RewardText(goal);
            string format = string.IsNullOrEmpty(goalGuideFormat) ? "{0}  ({1} / {2})  -  {3}" : goalGuideFormat;
            return string.Format(format, goal.title, FormatAmount(goalTable.Progress(goal)), FormatAmount(goal.target), reward);
        }
        if (PixelGuideVendor.AboutToVisit) return null;   // (a goal just asked Cubie over: nothing to count down while he is on his way)

        for (int i = 1; i < stages; i++)
        {
            if (tiers[i].unlocked || tiers[i].unlockMode == PixelClicker.TierUnlockMode.ShopOnly) continue;
            if (tiers[i].unlockMode == PixelClicker.TierUnlockMode.ByGoal && PixelGoals.UnlocksTier(tiers[i].type)) return null;   // (its goal has not appeared yet: no threshold to count)

            double remaining = Math.Ceiling(Math.Max(0d, tiers[i].unlockThreshold - tiers[i - 1].totalCollected));
            return string.Format(guideFormat, FormatAmount(remaining), tiers[i - 1].type);
        }

        // All guided tiers are unlocked: the next one is sold in the shop (rare drops like the Dragon Cubes don't count).
        int next = stages;
        while (next < tiers.Length && tiers[next].rareDrop) next++;
        if (next < tiers.Length && !tiers[next].unlocked &&
            tiers[next].unlockMode == PixelClicker.TierUnlockMode.ShopOnly)
        {
            // Before RGB: Cubie's second visit unlocks it once enough Black is earned - count that down (nothing to say once he is due).
            int black = PixelGuideVendor.BlackUntilVisitTwo;
            if (black > 0) return string.Format(guideFormat, FormatAmount(black), PixelClicker.PixelType.Black);
            if (black == 0) return null;
            return shopGuideText;
        }

        return null;
    }

    private float windowFade = 1f;

    // The guide line is worked out ~10 times a second (it formats numbers and looks at the goals), and the box is only re-measured
    // when its text or size changed (it used to do both every frame for the whole early game).
    private const float GuideRebuildSeconds = 0.1f;
    private float guideRebuildTimer;
    private string builtGuideText, measuredGuideText;
    private float measuredGuideSize = -1f;
    private RectTransform guideRootRect;

    private void UpdateGuide()
    {
        if (guideLabel == null) return;

        string text = null;
        Color color = guideColor;
        float size = guideTextSize;
        float fade = 1f;

        if (unlockTimer > 0f)
        {
            unlockTimer -= Time.unscaledDeltaTime;
            text = unlockMessage;
            size = unlockFontSize;
            color = unlockColor;
            // Fade over the last second.
            fade = Mathf.Clamp01(unlockTimer / Mathf.Min(1f, Mathf.Max(0.01f, unlockSeconds)));
        }
        else if (showTierGuide && !PixelHints.IntroHoldsGuide) // hidden until the new-game intro tips have been read
        {
            guideRebuildTimer -= Time.unscaledDeltaTime;
            if (guideRebuildTimer <= 0f) { guideRebuildTimer = GuideRebuildSeconds; builtGuideText = BuildGuideText(); }
            text = builtGuideText;
        }
        if (PixelGuideVendor.ShopHold) { text = shopGuideText; unlockTimer = 0f; }   // Cubie's hold: the 'buy the RGB Pack' line stays and animates
        PixelClicker.ClickHint = unlockTimer <= 0f && !string.IsNullOrEmpty(text) && showTierGuide; // the cube pulses while this text shows and nothing was collected yet

        bool visible = !string.IsNullOrEmpty(text);
        if (guideRoot.activeSelf != visible) guideRoot.SetActive(visible);
        if (!visible) return;

        // Fades away while a window is open and comes straight back when it closes.
        bool windowOpen = PixelWindows.AnyOpenExcept(null) && !PixelGuideVendor.ShopHold;
        windowFade = Mathf.MoveTowards(windowFade, windowOpen ? 0f : 1f, Time.unscaledDeltaTime / (windowOpen ? 0.25f : 0.06f));
        fade *= windowFade;

        if (PixelGuideVendor.ShopHold)
        {
            // The line pulses and shimmers gold so it can't be missed.
            float beat = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            color = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.25f, 1f), beat);
            size *= 1f + 0.12f * beat;
        }
        guideLabel.text = text;
        guideLabel.fontSize = size;
        color.a *= fade;
        guideLabel.color = color;

        Color bg = guideBackgroundColor;
        bg.a = guideBackgroundOpacity * fade;
        guideBackground.color = bg;

        // Fit the box around the text (only when the text or its size changed).
        if (text != measuredGuideText || size != measuredGuideSize)
        {
            measuredGuideText = text;
            measuredGuideSize = size;
            Vector2 preferred = guideLabel.GetPreferredValues(text);
            if (guideRootRect == null) guideRootRect = guideRoot.GetComponent<RectTransform>();
            guideRootRect.sizeDelta = new Vector2(preferred.x + guideBoxPadding.x * 2f, preferred.y + guideBoxPadding.y * 2f);
        }
    }

    private void BuildActiveHud()
    {
        if (!showActivePotionHud) return;

        hudLabel = PixelStatusBox.Create(font, popupCanvasRect, activeHudFontSize);   // slides out from under the top bar
        if (popupOutlineWidth > 0f)
        {
            hudLabel.outlineColor = popupOutlineColor;
            hudLabel.outlineWidth = popupOutlineWidth;
        }
    }

    // The potion line's texts, made again only when what they show changed (they used to be formatted every frame).
    private string hudPlacingOwn, hudPlacingName, hudPlacingText;
    private int hudPotionIndex = -1, hudPotionSeconds = -1;
    private string hudPotionText, hudCancelText;

    private void UpdateActiveHud()
    {
        if (hudLabel == null) return;

        // While placing a device the line tells you what to do; otherwise it shows the running potion.
        bool placing = consumables != null && consumables.IsPlacing;
        bool active = placing || (consumables != null && consumables.IsActive);
        PixelStatusBox.Show(hudLabel, active);
        if (!active) { PixelRemoveTint.Shake(hudLabel.transform, 0f); return; }

        if (placing)
        {
            string own = consumables.PlacingMessage, placingName = consumables.PlacingName;
            if (hudPlacingText == null || own != hudPlacingOwn || placingName != hudPlacingName)
            {
                hudPlacingOwn = own; hudPlacingName = placingName;
                hudPlacingText = string.Format(string.IsNullOrEmpty(own) ? placingHudFormat : own, placingName);
            }
            hudLabel.text = hudPlacingText;
            hudLabel.color = Color.white;
            return;
        }

        // Hold the right mouse button on the line to cancel the potion.
        PixelConsumables.Potion running = consumables.Get(consumables.ActiveIndex);
        int secondsLeft = Mathf.CeilToInt(consumables.Remaining);
        if (hudPotionText == null || consumables.ActiveIndex != hudPotionIndex || secondsLeft != hudPotionSeconds)
        {
            if (consumables.ActiveIndex != hudPotionIndex || hudCancelText == null) hudCancelText = string.Format(consumables.CancelPotionText, running.displayName);
            hudPotionIndex = consumables.ActiveIndex;
            hudPotionSeconds = secondsLeft;
            hudPotionText = string.Format(activeHudFormat, running.displayName, secondsLeft);
        }
        if (PixelHold.Update(this, PointerOverHudLabel() ? hudLabel : null, consumables.RemoveHoldSeconds,
                             hudCancelText, consumables.RemoveMeterColor))
        {
            consumables.CancelActive();
            PixelRemoveTint.Shake(hudLabel.transform, 0f);
            return;
        }

        PixelConsumables.Potion potion = running;
        hudLabel.text = hudPotionText;

        int tierIndex = clicker.IndexOf(potion.type);
        Color lineColor = activeHudUsesTierColor && tierIndex >= 0 ? clicker.Tiers[tierIndex].UIColor : Color.white;
        float cancel = PixelHold.ProgressOf(hudLabel);   // the line reddens and trembles a little as the hold fills (like a placed device being removed)
        hudLabel.color = PixelRemoveTint.RedText(lineColor, cancel);
        PixelRemoveTint.Shake(hudLabel.transform, cancel);
    }

    /// <summary>Is the mouse on the text of the active-potion line?</summary>
    private bool PointerOverHudLabel()
    {
        if (hudLabel == null || !PixelStatusBox.IsShown(hudLabel)) return false;
        RectTransform rt = hudLabel.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, PixelInput.PointerPosition(), null, out Vector2 local)) return false;
        float halfWidth = Mathf.Min(hudLabel.preferredWidth, rt.rect.width) * 0.5f + 12f;
        return Mathf.Abs(local.x - rt.rect.center.x) <= halfWidth && Mathf.Abs(local.y - rt.rect.center.y) <= rt.rect.height * 0.5f;
    }

    private string FormatAmount(double value)
    {
        return PixelClicker.FormatNumber(value);
    }
}
