// ---- System notes ----
// PixelClickerLookFixups.cs
//   Part of partial class PixelClicker: RunLookFixups, called from Awake. The scene stores the Pixel Looks list (the 'looks' array in the
//   Inspector) and a looksVersion number; each 'if (looksVersion < N)' step below adds or replaces the looks that changed in version N, so a
//   scene saved before a look existed still gets it. They run in order on every Play until the scene is saved with the new number (the
//   scene's data is old, so in practice they run each time). New look changes add the next step at the END with the next number.
//   Split out of Awake in PixelClicker.cs (no behaviour change).
// ---------------------------------------------------
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
    /// <summary>Brings the saved Pixel Looks list up to the current looksVersion (one step per version, in order).</summary>
    private void RunLookFixups()
    {
        if (looksVersion < 3)
        {
            // White, gray, black and RGB are back to their plain flat look: drop the entries earlier versions added.
            if (looks != null)
            {
                System.Collections.Generic.List<PixelLook> kept = new System.Collections.Generic.List<PixelLook>();
                foreach (PixelLook l in looks)
                    if (l != null && l.type != PixelType.White && l.type != PixelType.Gray && l.type != PixelType.Black &&
                        l.type != PixelType.Red && l.type != PixelType.Green && l.type != PixelType.Blue) kept.Add(l);
                looks = kept.ToArray();
            }
            looksVersion = 3;
        }
        if (looksVersion < 4)
        {
            // The Vacuum look (dark purple glass block with black circles) is new: add it to lists saved before it existed.
            if (PixelLooks.Find(looks, PixelType.Vacuum) == null)
            {
                PixelLook vacuumLook = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Vacuum);
                System.Collections.Generic.List<PixelLook> extended = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
                if (vacuumLook != null) extended.Add(vacuumLook);
                looks = extended.ToArray();
            }
            looksVersion = 4;
        }
        if (looksVersion < 5)
        {
            // The Obsidian look (polished black metal, white streaks, cracks) is new: add it to lists saved before it existed.
            if (PixelLooks.Find(looks, PixelType.Obsidian) == null)
            {
                PixelLook obsidianLook = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Obsidian);
                System.Collections.Generic.List<PixelLook> extended = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
                if (obsidianLook != null) extended.Add(obsidianLook);
                looks = extended.ToArray();
            }
            looksVersion = 5;
        }
        if (looksVersion < 6)
        {
            // The Ghost look (wobbly, matte, floats away) is new: add it to lists saved before it existed.
            if (PixelLooks.Find(looks, PixelType.Ghost) == null)
            {
                PixelLook ghostLook = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Ghost);
                System.Collections.Generic.List<PixelLook> extended = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
                if (ghostLook != null) extended.Add(ghostLook);
                looks = extended.ToArray();
            }
            looksVersion = 6;
        }
        if (looksVersion < 7)
        {
            // Red, green and blue need an entry again (colour-blind marks); add any that are missing.
            System.Collections.Generic.List<PixelLook> list7 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            foreach (PixelType t in new[] { PixelType.Red, PixelType.Green, PixelType.Blue })
            {
                if (PixelLooks.Find(list7.ToArray(), t) != null) continue;
                PixelLook def = PixelLooks.Find(PixelLooks.CreateDefaults(), t);
                if (def != null) list7.Add(def);
            }
            looks = list7.ToArray();
            looksVersion = 7;
        }
        if (looksVersion < 8)
        {
            // Singularity pixels now pull other old pixels towards them: switch that on in lists saved before it existed.
            PixelLook singularityLook = PixelLooks.Find(looks, PixelType.Singularity);
            if (singularityLook != null && !singularityLook.gravityWell) singularityLook.gravityWell = true;
            looksVersion = 8;
        }
        if (looksVersion < 9)
        {
            // The Electric look (a cube of crackling lightning) is new: add it to lists saved before it existed.
            if (PixelLooks.Find(looks, PixelType.Electric) == null)
            {
                PixelLook electricLook = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Electric);
                System.Collections.Generic.List<PixelLook> extended = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
                if (electricLook != null) extended.Add(electricLook);
                looks = extended.ToArray();
            }
            looksVersion = 9;
        }
        if (looksVersion < 10)
        {
            // The seven Dragon Cube looks (glassy orange with 1-7 small cubes inside) are new: add any that are missing.
            System.Collections.Generic.List<PixelLook> list10 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            for (int s = 0; s < 7; s++)
            {
                PixelType t = (PixelType)((int)PixelType.DragonCube1 + s);
                if (PixelLooks.Find(list10.ToArray(), t) != null) continue;
                PixelLook def = PixelLooks.Find(PixelLooks.CreateDefaults(), t);
                if (def != null) list10.Add(def);
            }
            looks = list10.ToArray();
            looksVersion = 10;
        }
        if (looksVersion < 11)
        {
            // The Mirror look (polished chrome) is new: add it to lists saved before it existed.
            if (PixelLooks.Find(looks, PixelType.Mirror) == null)
            {
                PixelLook mirrorLook = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Mirror);
                System.Collections.Generic.List<PixelLook> extended11 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
                if (mirrorLook != null) extended11.Add(mirrorLook);
                looks = extended11.ToArray();
            }
            looksVersion = 11;
        }
        if (looksVersion < 12)
        {
            // The Seed look (brown cube with a sprout that grows) is new: add it to lists saved before it existed.
            if (PixelLooks.Find(looks, PixelType.Seed) == null)
            {
                PixelLook seedLook = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Seed);
                System.Collections.Generic.List<PixelLook> extended12 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
                if (seedLook != null) extended12.Add(seedLook);
                looks = extended12.ToArray();
            }
            looksVersion = 12;
        }
        if (looksVersion < 13)
        {
            // The Seed is a brown shell now (cracks as you click it, a sprout grows out): replace the older sprout-on-a-cube look.
            System.Collections.Generic.List<PixelLook> list13 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            list13.RemoveAll(l => l != null && l.type == PixelType.Seed);
            PixelLook shellLook = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Seed);
            if (shellLook != null) list13.Add(shellLook);
            looks = list13.ToArray();
            looksVersion = 13;
        }
        if (looksVersion < 14)
        {
            // The Mirror is reflective now (chrome texture + a glint): replace the earlier plain-metal look.
            System.Collections.Generic.List<PixelLook> list14 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            list14.RemoveAll(l => l != null && l.type == PixelType.Mirror);
            PixelLook chromeLook = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Mirror);
            if (chromeLook != null) list14.Add(chromeLook);
            looks = list14.ToArray();
            looksVersion = 14;
        }
        if (looksVersion < 15)
        {
            // The Water look (a wobbling see-through blue jelly cube) is new: add it to lists saved before it existed.
            if (PixelLooks.Find(looks, PixelType.Water) == null)
            {
                PixelLook waterLook = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Water);
                System.Collections.Generic.List<PixelLook> extended15 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
                if (waterLook != null) extended15.Add(waterLook);
                looks = extended15.ToArray();
            }
            looksVersion = 15;
        }
        if (looksVersion < 16)
        {
            // Water pixels now splash on the ground: switch that on in a Water look saved before it existed.
            PixelLook savedWater = PixelLooks.Find(looks, PixelType.Water);
            if (savedWater != null) savedWater.splash = true;
            looksVersion = 16;
        }
        if (looksVersion < 17)
        {
            // The basic pixels (White, Gray, Black, Red, Green, Blue) got a subtle surface and a bevelled rim: add the looks that are missing, and give
            // the saved ones (Red / Green / Blue, which carried only the colour-blind marks) the new surface unless you already changed them.
            System.Collections.Generic.List<PixelLook> list17 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            foreach (PixelType basic in new[] { PixelType.White, PixelType.Gray, PixelType.Black, PixelType.Red, PixelType.Green, PixelType.Blue })
            {
                PixelLook def = PixelLooks.Find(PixelLooks.CreateDefaults(), basic);
                PixelLook saved = PixelLooks.Find(list17.ToArray(), basic);
                if (def == null) continue;
                if (saved == null) { list17.Add(def); continue; }
                if (saved.outline || saved.basicSurface != PixelLook.BasicSurface.None) continue;
                saved.basicSurface = def.basicSurface;
                saved.outline = def.outline;
                saved.outlineUsesTierColor = def.outlineUsesTierColor;
                saved.outlineColor = def.outlineColor;
                saved.outlineThickness = def.outlineThickness;
                saved.outlineStrength = def.outlineStrength;
                if (def.useColor && !saved.useColor) { saved.useColor = true; saved.color = def.color; }
            }
            looks = list17.ToArray();
            looksVersion = 17;
        }
        if (looksVersion < 18)
        {
            // The basic pixels' rims are now a lighter / darker shade of the pixel's own colour: switch that on where the rim still has the version-17 default colour.
            foreach (PixelType basic in new[] { PixelType.White, PixelType.Gray, PixelType.Black, PixelType.Red, PixelType.Green, PixelType.Blue })
            {
                PixelLook def = PixelLooks.Find(PixelLooks.CreateDefaults(), basic);
                PixelLook saved = PixelLooks.Find(looks, basic);
                if (def == null || saved == null || !saved.outline || Mathf.Abs(saved.outlineShade) > 0.001f) continue;
                Color a = saved.outlineColor, b = def.outlineColor;
                if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f) saved.outlineShade = def.outlineShade;
            }
            looksVersion = 18;
        }
        if (looksVersion < 19)
        {
            // The basic pixels' rims became a flat solid bevel (the glowing neon halo looked like a blurry frame): switch it on where the rim is still the shaded default.
            foreach (PixelType basic in new[] { PixelType.White, PixelType.Gray, PixelType.Black, PixelType.Red, PixelType.Green, PixelType.Blue })
            {
                PixelLook def = PixelLooks.Find(PixelLooks.CreateDefaults(), basic);
                PixelLook saved = PixelLooks.Find(looks, basic);
                if (def == null || saved == null || !saved.outline || Mathf.Abs(saved.outlineShade) < 0.001f || saved.outlineBevel) continue;
                saved.outlineBevel = true;
                saved.outlineThickness = def.outlineThickness;
            }
            looksVersion = 19;
        }
        if (looksVersion < 20)
        {
            // Obsidian and Seed got the same flat bevelled rim as the basic pixels: add it to looks saved without one.
            foreach (PixelType special in new[] { PixelType.Obsidian, PixelType.Seed })
            {
                PixelLook def = PixelLooks.Find(PixelLooks.CreateDefaults(), special);
                PixelLook saved = PixelLooks.Find(looks, special);
                if (def == null || saved == null || saved.outline) continue;
                saved.outline = true;
                saved.outlineUsesTierColor = def.outlineUsesTierColor;
                saved.outlineBevel = def.outlineBevel;
                saved.outlineThickness = def.outlineThickness;
                saved.outlineShadeOfTier = def.outlineShadeOfTier;
                saved.outlineShade = def.outlineShade;
                saved.outlineStrength = def.outlineStrength;
            }
            looksVersion = 20;
        }
        if (looksVersion < 21)
        {
            // The basic pixels got a material feel (matte White, brushed-metal Gray, glossy Black, slightly glowing glossy RGB): applied where metallic / smoothness / emission were never set.
            foreach (PixelType basic in new[] { PixelType.White, PixelType.Gray, PixelType.Black, PixelType.Red, PixelType.Green, PixelType.Blue })
            {
                PixelLook def = PixelLooks.Find(PixelLooks.CreateDefaults(), basic);
                PixelLook saved = PixelLooks.Find(looks, basic);
                if (def == null || saved == null || saved.metallic >= 0f || saved.smoothness >= 0f || saved.emission > 0f) continue;
                saved.metallic = def.metallic;
                saved.smoothness = def.smoothness;
                saved.emission = def.emission;
            }
            looksVersion = 21;
        }
        if (looksVersion < 22)
        {
            // The Vacuum pixel got vortex face marks (a glowing ring round each black disc), a violet bevel rim and a faint inner glow.
            PixelLook defV = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Vacuum);
            PixelLook savedV = PixelLooks.Find(looks, PixelType.Vacuum);
            if (defV != null && savedV != null && savedV.faceCircles && !savedV.faceRing && !savedV.outline)
            {
                savedV.faceRing = true;
                savedV.faceRingColor = defV.faceRingColor;
                savedV.faceCircleRadius = defV.faceCircleRadius;
                savedV.emission = Mathf.Max(savedV.emission, defV.emission);
                savedV.outline = true;
                savedV.outlineUsesTierColor = false;
                savedV.outlineColor = defV.outlineColor;
                savedV.outlineBevel = true;
                savedV.outlineThickness = defV.outlineThickness;
                savedV.outlineShade = defV.outlineShade;
                savedV.outlineStrength = defV.outlineStrength;
            }
            looksVersion = 22;
        }
        if (looksVersion < 23)
        {
            // Red / Green / Blue lose their rim, Gray's rim gets darker (only where the rim is still the shaded bevel default).
            foreach (PixelType basic in new[] { PixelType.Red, PixelType.Green, PixelType.Blue })
            {
                PixelLook saved = PixelLooks.Find(looks, basic);
                if (saved != null && saved.outline && saved.outlineBevel && Mathf.Abs(saved.outlineShade) > 0.001f) saved.outline = false;
            }
            PixelLook gray = PixelLooks.Find(looks, PixelType.Gray);
            PixelLook grayDef = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Gray);
            if (gray != null && grayDef != null && gray.outline && gray.outlineBevel && Mathf.Abs(gray.outlineShade - 0.4f) < 0.001f)
            {
                gray.outlineShade = grayDef.outlineShade;
                gray.outlineStrength = grayDef.outlineStrength;
            }
            looksVersion = 23;
        }
        if (looksVersion < 24)
        {
            // Black and Gray lose their rim; Obsidian's rim turns darker (only where it is still the shaded bevel default).
            foreach (PixelType basic in new[] { PixelType.Gray, PixelType.Black })
            {
                PixelLook saved = PixelLooks.Find(looks, basic);
                if (saved != null && saved.outline && saved.outlineBevel) saved.outline = false;
            }
            PixelLook obsidian = PixelLooks.Find(looks, PixelType.Obsidian);
            PixelLook obsidianDef = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Obsidian);
            if (obsidian != null && obsidianDef != null && obsidian.outline && obsidian.outlineBevel && Mathf.Abs(obsidian.outlineShade - 0.45f) < 0.001f)
            {
                obsidian.outlineShade = obsidianDef.outlineShade;
                obsidian.outlineStrength = obsidianDef.outlineStrength;
            }
            looksVersion = 24;
        }
        if (looksVersion < 25)
        {
            // The Fire pixel and the six Prospector ores are new: add their looks to lists saved before they existed.
            System.Collections.Generic.List<PixelLook> list25 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            foreach (PixelType t in new[] { PixelType.Fire, PixelType.Copper, PixelType.Tin, PixelType.Iron, PixelType.Lead, PixelType.Zinc, PixelType.Nickel })
            {
                if (PixelLooks.Find(list25.ToArray(), t) != null) continue;
                PixelLook def = PixelLooks.Find(PixelLooks.CreateDefaults(), t);
                if (def != null) list25.Add(def);
            }
            looks = list25.ToArray();
            looksVersion = 25;
        }
        if (looksVersion < 26)
        {
            // The Treasure chest look is new.
            System.Collections.Generic.List<PixelLook> list26 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            if (PixelLooks.Find(list26.ToArray(), PixelType.Treasure) == null)
            {
                PixelLook def = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Treasure);
                if (def != null) list26.Add(def);
            }
            looks = list26.ToArray();
            looksVersion = 26;
        }
        if (looksVersion < 27)
        {
            // The Nuclear look is new.
            System.Collections.Generic.List<PixelLook> list27 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            if (PixelLooks.Find(list27.ToArray(), PixelType.Nuclear) == null)
            {
                PixelLook def = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Nuclear);
                if (def != null) list27.Add(def);
            }
            looks = list27.ToArray();
            looksVersion = 27;
        }

        if (looksVersion < 28)
        {
            // The Wood, Rock and farming looks are new.
            System.Collections.Generic.List<PixelLook> list28 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults28 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Wood, PixelType.Rock, PixelType.Dirt, PixelType.Fertilizer, PixelType.Pollen, PixelType.Wheat, PixelType.Weevil, PixelType.GoldenFruit })
            {
                if (PixelLooks.Find(list28.ToArray(), t) != null) continue;
                PixelLook def = PixelLooks.Find(defaults28, t);
                if (def != null) list28.Add(def);
            }
            looks = list28.ToArray();
            looksVersion = 28;
        }

        if (looksVersion < 29)
        {
            // The farming looks got bevelled rims and ambient particles: new ones are replaced by the fancier defaults, Seed / Water only gain the particles.
            System.Collections.Generic.List<PixelLook> list29 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults29 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Dirt, PixelType.Fertilizer, PixelType.Pollen, PixelType.Wheat, PixelType.Weevil, PixelType.GoldenFruit })
            {
                PixelLook def = PixelLooks.Find(defaults29, t);
                if (def == null) continue;
                int at = list29.FindIndex(l => l != null && l.type == t);
                if (at >= 0) list29[at] = def; else list29.Add(def);
            }
            foreach (PixelType t in new[] { PixelType.Seed, PixelType.Water })
            {
                PixelLook existing = PixelLooks.Find(list29.ToArray(), t), def = PixelLooks.Find(defaults29, t);
                if (existing != null && def != null && existing.farmFx == PixelLook.FarmAmbient.None) existing.farmFx = def.farmFx;
            }
            looks = list29.ToArray();
            looksVersion = 29;
        }

        if (looksVersion < 31)
        {
            // Obsidian: natural black glass (no violet rim or glow); the look is replaced once more.
            PixelLook def31 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Obsidian);
            System.Collections.Generic.List<PixelLook> list31 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            int at31 = list31.FindIndex(l => l != null && l.type == PixelType.Obsidian);
            if (def31 != null) { if (at31 >= 0) list31[at31] = def31; else list31.Add(def31); }
            looks = list31.ToArray();
            looksVersion = 31;
        }

        if (looksVersion < 30)
        {
            // Obsidian got a fancier look (violet glow, light sweep, sparks): replace the saved one with the new default.
            PixelLook def30 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Obsidian);
            System.Collections.Generic.List<PixelLook> list30 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            int at30 = list30.FindIndex(l => l != null && l.type == PixelType.Obsidian);
            if (def30 != null) { if (at30 >= 0) list30[at30] = def30; else list30.Add(def30); }
            looks = list30.ToArray();
            looksVersion = 30;
        }

        if (looksVersion < 34)
        {
            // The Electrician pixels (Wire, Battery, Capacitor) got their looks.
            System.Collections.Generic.List<PixelLook> list32 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults32 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Wire, PixelType.Battery, PixelType.Capacitor, PixelType.Resistor, PixelType.Transistor, PixelType.Fuse, PixelType.Bulb })
            {
                PixelLook def = PixelLooks.Find(defaults32, t);
                if (def == null) continue;
                int at = list32.FindIndex(l => l != null && l.type == t);
                if (at >= 0) list32[at] = def; else list32.Add(def);
            }
            looks = list32.ToArray();
            looksVersion = 34;
        }

        if (looksVersion < 35)
        {
            // Meteor and Solar got their own looks.
            System.Collections.Generic.List<PixelLook> list35 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults35 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Meteor, PixelType.Solar })
            {
                PixelLook def = PixelLooks.Find(defaults35, t);
                if (def == null) continue;
                int at = list35.FindIndex(l => l != null && l.type == t);
                if (at >= 0) list35[at] = def; else list35.Add(def);
            }
            looks = list35.ToArray();
            looksVersion = 35;
        }

        if (looksVersion < 36)
        {
            // The rocky pixels lost their outlines and got jagged edges.
            System.Collections.Generic.List<PixelLook> list36 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults36 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Obsidian, PixelType.Meteor, PixelType.Rock, PixelType.Copper, PixelType.Tin, PixelType.Iron, PixelType.Lead, PixelType.Zinc, PixelType.Nickel })
            {
                PixelLook def = PixelLooks.Find(defaults36, t);
                if (def == null) continue;
                int at = list36.FindIndex(l => l != null && l.type == t);
                if (at >= 0) list36[at] = def; else list36.Add(def);
            }
            looks = list36.ToArray();
            looksVersion = 36;
        }

        if (looksVersion < 38)
        {
            // The Vacuum pixel became a clear glass container with one opening and a suction wind.
            PixelLook def38 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Vacuum);
            System.Collections.Generic.List<PixelLook> list38 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            int at38 = list38.FindIndex(l => l != null && l.type == PixelType.Vacuum);
            if (def38 != null) { if (at38 >= 0) list38[at38] = def38; else list38.Add(def38); }
            looks = list38.ToArray();
            looksVersion = 38;
        }

        if (looksVersion < 39)
        {
            // The Ghost pixel became a gray gelatinous blob.
            PixelLook def39 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Ghost);
            System.Collections.Generic.List<PixelLook> list39 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            int at39 = list39.FindIndex(l => l != null && l.type == PixelType.Ghost);
            if (def39 != null) { if (at39 >= 0) list39[at39] = def39; else list39.Add(def39); }
            looks = list39.ToArray();
            looksVersion = 39;
        }

        if (looksVersion < 40)
        {
            // Wood, Stone (was Rock) and Dirt got crisp drawn surfaces.
            System.Collections.Generic.List<PixelLook> list37 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults37 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Wood, PixelType.Rock, PixelType.Dirt })
            {
                PixelLook def = PixelLooks.Find(defaults37, t);
                if (def == null) continue;
                int at = list37.FindIndex(l => l != null && l.type == t);
                if (at >= 0) list37[at] = def; else list37.Add(def);
            }
            looks = list37.ToArray();
            looksVersion = 40;
        }

        if (looksVersion < 41)
        {
            // Wood and Stone look more natural (bark, fractured rock) and the Seed became a ridged seed pod.
            System.Collections.Generic.List<PixelLook> list41 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults41 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Wood, PixelType.Rock, PixelType.Seed })
            {
                PixelLook def = PixelLooks.Find(defaults41, t);
                if (def == null) continue;
                int at = list41.FindIndex(l => l != null && l.type == t);
                if (at >= 0) list41[at] = def; else list41.Add(def);
            }
            looks = list41.ToArray();
            looksVersion = 41;
        }

        if (looksVersion < 42)
        {
            // The hazard blocks (Pest, Gas Pocket) got looks.
            System.Collections.Generic.List<PixelLook> list42 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults42 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Pest, PixelType.GasPocket })
            {
                PixelLook def = PixelLooks.Find(defaults42, t);
                if (def != null && PixelLooks.Find(list42.ToArray(), t) == null) list42.Add(def);
            }
            looks = list42.ToArray();
            looksVersion = 42;
        }

        if (looksVersion < 43)
        {
            // The Pest got a dangerous new look (hazard stripes, red eyes, twitching spiky carapace).
            System.Collections.Generic.List<PixelLook> list43 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook def43 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Pest);
            int at43 = list43.FindIndex(l => l != null && l.type == PixelType.Pest);
            if (def43 != null) { if (at43 >= 0) list43[at43] = def43; else list43.Add(def43); }
            looks = list43.ToArray();
            looksVersion = 43;
        }

        if (looksVersion < 44)
        {
            // The Woodcutting blocks (Bark, Sap, Leaf, Heartwood, Twig, Hornet Nest) got looks.
            System.Collections.Generic.List<PixelLook> list44 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults44 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Bark, PixelType.Sap, PixelType.Leaf, PixelType.Heartwood, PixelType.Twig, PixelType.HornetNest })
            {
                PixelLook def = PixelLooks.Find(defaults44, t);
                if (def != null && PixelLooks.Find(list44.ToArray(), t) == null) list44.Add(def);
            }
            looks = list44.ToArray();
            looksVersion = 44;
        }

        if (looksVersion < 45)
        {
            // The Foraging blocks (Berry, Mushroom, Herb, Truffle, Weeds, Toadstool) got looks.
            System.Collections.Generic.List<PixelLook> list45 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults45 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Berry, PixelType.Mushroom, PixelType.Herb, PixelType.Truffle, PixelType.Weeds, PixelType.Toadstool })
            {
                PixelLook def = PixelLooks.Find(defaults45, t);
                if (def != null && PixelLooks.Find(list45.ToArray(), t) == null) list45.Add(def);
            }
            looks = list45.ToArray();
            looksVersion = 45;
        }

        if (looksVersion < 46)
        {
            // New ores (Silver, Gold, Bedrock) and the Fishing blocks got looks.
            System.Collections.Generic.List<PixelLook> list46 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults46 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Silver, PixelType.Gold, PixelType.Bedrock, PixelType.Fish, PixelType.Seaweed, PixelType.Shell, PixelType.Pearl, PixelType.Driftwood, PixelType.Jellyfish })
            {
                PixelLook def = PixelLooks.Find(defaults46, t);
                if (def != null && PixelLooks.Find(list46.ToArray(), t) == null) list46.Add(def);
            }
            looks = list46.ToArray();
            looksVersion = 46;
        }

        if (looksVersion < 47)
        {
            // The Jellyfish became electric (lightning look) and the Bait block got a look.
            System.Collections.Generic.List<PixelLook> list47 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            list47.RemoveAll(l => l != null && l.type == PixelType.Jellyfish);
            PixelLook[] defaults47 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Jellyfish, PixelType.Bait })
            {
                PixelLook def = PixelLooks.Find(defaults47, t);
                if (def != null && PixelLooks.Find(list47.ToArray(), t) == null) list47.Add(def);
            }
            looks = list47.ToArray();
            looksVersion = 47;
        }

        if (looksVersion < 48)
        {
            // Fishing's special blocks (the bottle and the whale) got looks.
            System.Collections.Generic.List<PixelLook> list48 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults48 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Bottle, PixelType.Whale })
            {
                PixelLook def = PixelLooks.Find(defaults48, t);
                if (def != null && PixelLooks.Find(list48.ToArray(), t) == null) list48.Add(def);
            }
            looks = list48.ToArray();
            looksVersion = 48;
        }

        if (looksVersion < 49)
        {
            // Red / Green / Blue lost their faint emission (it made them glow: a build-proof glow shell and halo are drawn for anything emissive) and the Mirror stopped leaning on
            // reflections and emission (it came out dark / washed in a build).
            PixelLook[] defaults49 = PixelLooks.CreateDefaults();
            if (looks != null)
                foreach (PixelType t in new[] { PixelType.Red, PixelType.Green, PixelType.Blue, PixelType.Mirror })
                {
                    PixelLook saved = PixelLooks.Find(looks, t), def = PixelLooks.Find(defaults49, t);
                    if (saved == null || def == null) continue;
                    saved.emission = def.emission;
                    if (t == PixelType.Mirror) { saved.metallic = def.metallic; saved.smoothness = def.smoothness; }
                }
            looksVersion = 49;
        }

        if (looksVersion < 50)
        {
            // The Mold block got a look.
            System.Collections.Generic.List<PixelLook> list50 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook def50 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Mold);
            if (def50 != null && PixelLooks.Find(list50.ToArray(), PixelType.Mold) == null) list50.Add(def50);
            looks = list50.ToArray();
            looksVersion = 50;
        }

        if (looksVersion < 51)
        {
            // The Message in a Bottle became a clear glass cube with a map inside: replace the saved look.
            System.Collections.Generic.List<PixelLook> list51 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            list51.RemoveAll(l => l != null && l.type == PixelType.Bottle);
            PixelLook def51 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Bottle);
            if (def51 != null) list51.Add(def51);
            looks = list51.ToArray();
            looksVersion = 51;
        }

        if (looksVersion < 52)
        {
            // The Wood Chips block got a look.
            System.Collections.Generic.List<PixelLook> list52 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook def52 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.WoodChips);
            if (def52 != null && PixelLooks.Find(list52.ToArray(), PixelType.WoodChips) == null) list52.Add(def52);
            looks = list52.ToArray();
            looksVersion = 52;
        }

        if (looksVersion < 53)
        {
            // The Great Oak got a look.
            System.Collections.Generic.List<PixelLook> list53 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook def53 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.GreatOak);
            if (def53 != null && PixelLooks.Find(list53.ToArray(), PixelType.GreatOak) == null) list53.Add(def53);
            looks = list53.ToArray();
            looksVersion = 53;
        }

        if (looksVersion < 54)
        {
            // The Whetstone block got a look.
            System.Collections.Generic.List<PixelLook> list54 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook def54 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Whetstone);
            if (def54 != null && PixelLooks.Find(list54.ToArray(), PixelType.Whetstone) == null) list54.Add(def54);
            looks = list54.ToArray();
            looksVersion = 54;
        }

        if (looksVersion < 55)
        {
            // The Acorn block got a look.
            System.Collections.Generic.List<PixelLook> list55 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook def55 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Acorn);
            if (def55 != null && PixelLooks.Find(list55.ToArray(), PixelType.Acorn) == null) list55.Add(def55);
            looks = list55.ToArray();
            looksVersion = 55;
        }

        if (looksVersion < 56)
        {
            // Wood-family old pixels (and the Whetstone) hardly bounce: low bounce, grippy, normal gravity.
            if (looks != null)
                foreach (PixelLook l in looks)
                {
                    if (l == null) continue;
                    if (l.type == PixelType.Wood || l.type == PixelType.Bark || l.type == PixelType.Heartwood || l.type == PixelType.Twig || l.type == PixelType.Acorn
                        || l.type == PixelType.Whetstone || l.type == PixelType.GreatOak || l.type == PixelType.WoodChips)
                    { l.customPhysics = true; l.bounce = 0.05f; l.friction = 0.9f; l.gravityMultiplier = 1f; }
                }
            looksVersion = 56;
        }

        if (looksVersion < 57)
        {
            // The Pesticide block got a look.
            System.Collections.Generic.List<PixelLook> list57 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook def57 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Pesticide);
            if (def57 != null && PixelLooks.Find(list57.ToArray(), PixelType.Pesticide) == null) list57.Add(def57);
            looks = list57.ToArray();
            looksVersion = 57;
        }

        if (looksVersion < 58)
        {
            // The Charcoal block got a look.
            System.Collections.Generic.List<PixelLook> list58 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook def58 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Charcoal);
            if (def58 != null && PixelLooks.Find(list58.ToArray(), PixelType.Charcoal) == null) list58.Add(def58);
            looks = list58.ToArray();
            looksVersion = 58;
        }

        if (looksVersion < 59)
        {
            // The three Egyptian God Cubes got their looks.
            System.Collections.Generic.List<PixelLook> list59 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults59 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.GodSlifer, PixelType.GodRa, PixelType.GodObelisk })
            {
                PixelLook def = PixelLooks.Find(defaults59, t);
                if (def != null && PixelLooks.Find(list59.ToArray(), t) == null) list59.Add(def);
            }
            looks = list59.ToArray();
            looksVersion = 59;
        }

        if (looksVersion < 60)
        {
            // The six Prime Cubes and the Prime Cubelette got their looks.
            System.Collections.Generic.List<PixelLook> list60 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults60 = PixelLooks.CreateDefaults();
            for (int t = (int)PixelType.StoneSpace; t <= (int)PixelType.InfinityCubelette; t++)
            {
                PixelLook def = PixelLooks.Find(defaults60, (PixelType)t);
                if (def != null && PixelLooks.Find(list60.ToArray(), (PixelType)t) == null) list60.Add(def);
            }
            looks = list60.ToArray();
            looksVersion = 60;
        }

        if (looksVersion < 61)
        {
            // Twig, Leaf and the Termite Hive got new looks (replacing the saved ones); Pinecone and Moss got theirs.
            System.Collections.Generic.List<PixelLook> list61 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults61 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Twig, PixelType.Leaf, PixelType.HornetNest, PixelType.Pinecone, PixelType.Moss })
            {
                PixelLook def = PixelLooks.Find(defaults61, t);
                if (def == null) continue;
                list61.RemoveAll(l => l != null && l.type == t);
                list61.Add(def);
            }
            looks = list61.ToArray();
            looksVersion = 61;
        }

        if (looksVersion < 62)
        {
            // Mold (pale fuzzy blue-green, glowing) and Truffle (near-black and glossy) were too alike: both replaced.
            System.Collections.Generic.List<PixelLook> list62 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook[] defaults62 = PixelLooks.CreateDefaults();
            foreach (PixelType t in new[] { PixelType.Mold, PixelType.Truffle })
            {
                PixelLook def = PixelLooks.Find(defaults62, t);
                if (def == null) continue;
                list62.RemoveAll(l => l != null && l.type == t);
                list62.Add(def);
            }
            looks = list62.ToArray();
            looksVersion = 62;
        }

        if (looksVersion < 63)
        {
            // The Golden Snitch got its look.
            System.Collections.Generic.List<PixelLook> list63 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            PixelLook def63 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.GoldenSnitch);
            if (def63 != null && PixelLooks.Find(list63.ToArray(), PixelType.GoldenSnitch) == null) list63.Add(def63);
            looks = list63.ToArray();
            looksVersion = 63;
        }

        if (looksVersion < 64)
        {
            // The Golden Snitch became a gold cube (it sits on the cube until clicked).
            System.Collections.Generic.List<PixelLook> list64 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            list64.RemoveAll(l => l != null && l.type == PixelType.GoldenSnitch);
            PixelLook def64 = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.GoldenSnitch);
            if (def64 != null) list64.Add(def64);
            looks = list64.ToArray();
            looksVersion = 64;
        }
    }
}
