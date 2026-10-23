// ---- System notes ----
// PixelClickerDatabase.cs
//   Part of PixelClicker: its Game Database entries (IPixelDatabaseSource, see PixelDatabase.cs) - one 'Pixel' entry per tier (the Pixel Guide
//   text + pay / clicks / weight; discovered = unlocked or collected; its 3D picture; pinnable as a currency row). Every other step links to
//   these through PixelDatabase.Builder.Pixel(type).
// ---------------------------------------------------
using System;
using UnityEngine;

public partial class PixelClicker : IPixelDatabaseSource
{
    void IPixelDatabaseSource.DatabaseSteps(PixelDatabase.Steps steps)
    {
        steps.Add(PixelDatabase.PixelsOrder, b =>
        {
            foreach (PixelTier t in Tiers)
            {
                if (t == null) continue;
                string desc;
                try { desc = PixelDatabase.Plain(PixelGuideTexts.Describe(t.type, t)); } catch (Exception) { desc = ""; }
                PixelDatabase.Entry e = b.Add("Pixel", PixelDatabase.PixelName(t), desc + "\nPays " + t.amountPerClick + " per collect, " + Mathf.Max(1, t.clicksToCollect) + " click" + (t.clicksToCollect > 1 ? "s" : "") + ", spawn weight " + t.spawnWeight + ". Internal type: " + t.type + ".");
                e.discovered = IsUnlocked(t.type) || t.totalCollected > 0d;
                e.tierIndex = Array.IndexOf(Tiers, t);
                e.pinType = "currency"; e.pinKey = t.type.ToString();
                b.Pixels[t.type] = e;
            }
        });
    }
}
