// ---- System notes ----
// PixelFloorDefaults.cs
//   Part of PixelFloor (partial class): the built-in styles: EnsureDefaultStyles (the once-only upgrades of saved lists, then the missing defaults), the S / NeonVariant makers and the DefaultStyles / ThemedStyles / ExtraStyles tables. The system notes are in PixelFloor.cs.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public partial class PixelFloor
{
    // ------------------------------------------------------------------
    // Built-in styles
    // ------------------------------------------------------------------

    private void EnsureDefaultStyles()
    {
        // Saved lists get the built-in fixes once each (before the missing defaults are added, so nothing is duplicated), then the missing defaults.
        UpgradeSavedStylesTo3();
        UpgradeSavedStylesTo4();
        UpgradeSavedStylesTo6();
        UpgradeSavedStylesTo10();
        UpgradeSavedStylesTo16();

        foreach (FloorStyle def in DefaultStyles())
            if (!styles.Exists(s => s.name == def.name)) styles.Add(def);
    }

    /// <summary>Part of EnsureDefaultStyles: defaultsVersion steps 1, 2, 3 (in this order): saved sizes, Pac-Man mazes, fit to view.</summary>
    private void UpgradeSavedStylesTo3()
    {
        // Sizes changed after these styles first shipped (they looked too zoomed in): bring saved copies up to date once.
        if (defaultsVersion < 1)
        {
            foreach (FloorStyle st in styles)
            {
                if (st == null) continue;
                switch (st.name)
                {
                    case "Yu-Gi-Oh Card Back": st.stretchToFloor = true; st.tileWorldSize = 8f; st.resolution = Mathf.Max(st.resolution, 1024); break;
                    case "Liquid Flow": st.tileWorldSize = 3f; break;
                    case "Moving Clouds": st.tileWorldSize = 5f; break;
                    case "Kaleidoscope": st.stretchToFloor = false; st.tileWorldSize = 8f; break;
                    case "Pac-Man": st.stretchToFloor = false; st.tileWorldSize = 8f; break;
                }
            }
            defaultsVersion = 1;
        }
        // Pac-Man mazes now fill their tile and join up with their neighbours.
        if (defaultsVersion < 2)
        {
            foreach (FloorStyle st in styles)
                if (st != null && st.name == "Pac-Man") st.tileAspect = 19f / 21f;
            defaultsVersion = 2;
        }
        // These four now fit one copy to the camera's view of the floor.
        if (defaultsVersion < 3)
        {
            foreach (FloorStyle st in styles)
                if (st != null && (st.name == "Yu-Gi-Oh Card Back" || st.name == "Moving Clouds" || st.name == "Kaleidoscope" || st.name == "Pac-Man"))
                    st.fitToView = true;
            defaultsVersion = 3;
        }
    }

    /// <summary>Part of EnsureDefaultStyles: defaultsVersion step 4: renamed, removed and regrouped styles.</summary>
    private void UpgradeSavedStylesTo4()
    {
        // Renamed, removed and regrouped styles: bring saved lists up to date once (before the missing defaults are added, so nothing is duplicated).
        if (defaultsVersion < 4)
        {
            string[,] renames =
            {
                { "Dragon Radar", "Radar" }, { "Super Saiyan Aura", "Super Aura" }, { "Ultra Instinct Aura", "Divine Aura" },
                { "Super Saiyan Blue Aura", "Blue Aura" }, { "Super Saiyan God Aura", "God Aura" }, { "Destruction Energy", "Death Energy" },
            };
            for (int r = 0; r < renames.GetLength(0); r++)
            {
                FloorStyle st = styles.Find(x => x != null && x.name == renames[r, 0]);
                if (st != null && !styles.Exists(x => x != null && x.name == renames[r, 1])) st.name = renames[r, 1];
            }
            styles.RemoveAll(x => x != null && (x.name == "Dragon Balls" || x.name == "Yu-Gi-Oh Card Back" || x.pattern == FloorPattern.DragonBalls || x.pattern == FloorPattern.CardBack));
            FloorStyle superAura = styles.Find(x => x != null && x.name == "Super Aura"), divine = styles.Find(x => x != null && x.name == "Divine Aura");
            if (superAura != null && divine != null)
            {
                styles.Remove(superAura);
                styles.Insert(styles.IndexOf(divine), superAura);   // the auras sit together
            }
            // The new neon grid variants go right after Neon Grid.
            int neonAt = styles.FindIndex(x => x != null && x.name == "Neon Grid");
            if (neonAt >= 0)
                foreach (FloorStyle def in DefaultStyles())
                    if (def.name.StartsWith("Neon ") && !styles.Exists(x => x != null && x.name == def.name)) styles.Insert(++neonAt, def);
            defaultsVersion = 4;
        }
    }

    /// <summary>Part of EnsureDefaultStyles: defaultsVersion steps 5, 6 (in this order): more renames / removals, the battle crater, Circuit Flow.</summary>
    private void UpgradeSavedStylesTo6()
    {
        // Renames, removals and the reworked battle crater.
        if (defaultsVersion < 5)
        {
            string[,] renames5 = { { "Planet Namek", "Alien Planet" }, { "Time Chamber", "Chamber Tiles" }, { "Tournament of Power", "Power Tournament" } };
            for (int r = 0; r < renames5.GetLength(0); r++)
            {
                FloorStyle st = styles.Find(x => x != null && x.name == renames5[r, 0]);
                if (st != null && !styles.Exists(x => x != null && x.name == renames5[r, 1])) st.name = renames5[r, 1];
            }
            styles.RemoveAll(x => x != null && (x.name == "Gravity Room" || x.pattern == FloorPattern.GravityRoom));
            FloorStyle crater = styles.Find(x => x != null && x.name == "Battle Crater");
            if (crater != null)
            {
                foreach (FloorStyle def in DefaultStyles())
                    if (def.name == "Battle Crater") { int at = styles.IndexOf(crater); styles[at] = def; break; }
            }
            defaultsVersion = 5;
        }

        if (defaultsVersion < 6)
        {
            styles.RemoveAll(x => x != null && (x.name == "Noir Smoke" || x.name == "Bloody Boards" || x.pattern == FloorPattern.NoirSmoker || x.pattern == FloorPattern.BloodBoards));
            foreach (FloorStyle st in styles)
                if (st != null && st.name == "Circuit Flow") { st.animateFps = 20f; st.cells = Mathf.Max(8, st.cells); }
            defaultsVersion = 6;
        }
    }

    /// <summary>Part of EnsureDefaultStyles: defaultsVersion steps 7, 8, 9, 10 (in this order): Circuit Flow, layout numbers, reworked and removed styles.</summary>
    private void UpgradeSavedStylesTo10()
    {
        // Circuit Flow with its sparks was heavy: a smaller texture redrawn a little less often.
        if (defaultsVersion < 7)
        {
            foreach (FloorStyle st in styles)
                if (st != null && st.name == "Circuit Flow") { st.resolution = 192; st.animateFps = 15f; }
            defaultsVersion = 7;
        }

        // A scene saved with older tile sizes kept them forever: copy the layout numbers of every built-in style from the current defaults.
        if (defaultsVersion < 8)
        {
            foreach (FloorStyle def in DefaultStyles())
            {
                FloorStyle old = styles.Find(s => s != null && s.name == def.name);
                if (old == null) continue;
                old.tileWorldSize = def.tileWorldSize; old.tileAspect = def.tileAspect; old.cells = def.cells;
                old.fitToView = def.fitToView; old.stretchToFloor = def.stretchToFloor;
            }
            defaultsVersion = 8;
        }

        // The 8-bit floors are gone, and these styles were redrawn as big non-repeating pictures: take the new definitions over the saved ones.
        if (defaultsVersion < 9)
        {
            styles.RemoveAll(x => x != null && (x.name.StartsWith("8-Bit ") || x.pattern == FloorPattern.RoadRacer || x.pattern == FloorPattern.Fighter ||
                (x.pattern >= FloorPattern.Platformer && x.pattern <= FloorPattern.Crossing)));
            string[] reworked =
            {
                "Marble", "Lava", "Deep Space", "Ice", "Matrix Rain", "Radar", "Battle Crater", "Super Aura", "Blue Aura", "Divine Aura", "God Aura",
                "Death Energy", "Laser Corridor", "Liquid Flow", "Spiral Galaxy", "Black Hole", "Moon Surface", "Red Planet", "Circuit Flow", "Cyber Hex",
                "Haunted Tiles", "Graveyard", "Watching Eyes",
            };
            foreach (FloorStyle def in DefaultStyles())
            {
                if (System.Array.IndexOf(reworked, def.name) < 0) continue;
                int at = styles.FindIndex(x => x != null && x.name == def.name);
                if (at >= 0) styles[at] = def;
            }
            defaultsVersion = 9;
        }

        // Spiral Galaxy and Black Hole are gone; Radar lost its circles; Liquid Flow, Circuit Flow, Cyber Hex and Watching Eyes are bigger.
        if (defaultsVersion < 10)
        {
            styles.RemoveAll(x => x != null && (x.name == "Spiral Galaxy" || x.name == "Black Hole" || x.pattern == FloorPattern.Galaxy || x.pattern == FloorPattern.BlackHole));
            foreach (FloorStyle def in DefaultStyles())
            {
                if (def.name != "Radar" && def.name != "Liquid Flow" && def.name != "Circuit Flow" && def.name != "Cyber Hex" && def.name != "Watching Eyes") continue;
                int at = styles.FindIndex(x => x != null && x.name == def.name);
                if (at >= 0) styles[at] = def;
            }
            defaultsVersion = 10;
        }
    }

    /// <summary>Part of EnsureDefaultStyles: defaultsVersion steps 11, 12, 13, 14, 15, 16 (in this order): ground-shader styles, Data Streams, Moon / Mars, laser floors, Coral Reef.</summary>
    private void UpgradeSavedStylesTo16()
    {
        // Lava, Marble, Ice and Deep Space are drawn by the world-space ground shader in the game (their menu pictures stay painted).
        if (defaultsVersion < 11)
        {
            foreach (FloorStyle def in DefaultStyles())
            {
                if (def.shaderGround <= 0) continue;
                FloorStyle old = styles.Find(x => x != null && x.name == def.name);
                if (old != null) { old.shaderGround = def.shaderGround; old.shaderScale = def.shaderScale; }
            }
            defaultsVersion = 11;
        }

        // Data Streams: drawn by the ground shader now (pattern 5), so it fills the whole floor circle.
        if (defaultsVersion < 12)
        {
            FloorStyle streams = styles.Find(x => x != null && x.name == "Data Streams");
            if (streams != null) { streams.shaderGround = 5; streams.shaderScale = 1.1f; }
            defaultsVersion = 12;
        }

        // Moon Surface and Red Planet became their own patterns (not one crater field in two colours); Stained Glass, Casino Felt and Nebula Field correct the camera's tilt.
        if (defaultsVersion < 13)
        {
            foreach (FloorStyle st in styles)
            {
                if (st == null) continue;
                if (st.name == "Moon Surface") { st.pattern = FloorPattern.MoonSurface; }
                if (st.name == "Red Planet") { st.pattern = FloorPattern.MarsSurface; st.colorA = new Color(0.72f, 0.36f, 0.2f); st.colorB = new Color(0.52f, 0.25f, 0.14f); st.colorC = new Color(0.12f, 0.07f, 0.06f); }
                if (st.name == "Stained Glass" || st.name == "Casino Felt" || st.name == "Nebula Field") st.tiltCorrect = true;
            }
            defaultsVersion = 13;
        }

        // The see-through laser floors got a real still picture (their menu icon / title picture was one flat pink colour at 16 px).
        if (defaultsVersion < 14)
        {
            foreach (FloorStyle st in styles)
                if (st != null && (st.name == "Neon Lasers" || st.name == "Laser Tracker")) st.resolution = Mathf.Max(st.resolution, 160);
            defaultsVersion = 14;
        }

        // The auras, Death Energy, Matrix Rain, Liquid Flow and Laser Corridor are drawn by the world-space ground shader in the game (patterns 6-10).
        if (defaultsVersion < 15)
        {
            foreach (FloorStyle def in DefaultStyles())
            {
                if (def.shaderGround < 6) continue;
                FloorStyle old = styles.Find(x => x != null && x.name == def.name);
                if (old != null) { old.shaderGround = def.shaderGround; old.shaderScale = def.shaderScale; }
            }
            defaultsVersion = 15;
        }

        // Persian Rug is gone; Coral Reef was redrawn and gets the moving caustic light overlay.
        if (defaultsVersion < 16)
        {
            styles.RemoveAll(x => x != null && (x.name == "Persian Rug" || x.pattern == FloorPattern.PersianRug));
            FloorStyle reef = styles.Find(x => x != null && x.name == "Coral Reef");
            if (reef != null) reef.overlay = FloorOverlay.Caustics;
            defaultsVersion = 16;
        }
    }

    private static FloorStyle S(string name, FloorPattern p, Color a, Color b, Color c, int res, bool pixelated,
                                float tile, int cells, float smooth = 0.2f, float glow = 0f)
        => new FloorStyle { name = name, pattern = p, colorA = a, colorB = b, colorC = c, resolution = res,
                            pixelated = pixelated, tileWorldSize = tile, cells = cells, smoothness = smooth, glow = glow,
                            seed = name.Length * 7 + 3 };

    private static FloorStyle NeonVariant(string name, FloorPattern p, Color ground, Color line, Color cross)
    {
        FloorStyle st = S(name, p, ground, line, cross, 128, false, 6f, 8, 0.7f, 1.6f);
        st.animateFps = 20f; st.pulseSpeed = 0.35f; st.pulseAmount = 0.3f;
        return st;
    }

    private static IEnumerable<FloorStyle> DefaultStyles()
    {
        yield return S("Classic", FloorPattern.Classic, Color.white, Color.gray, Color.black, 8, false, 1f, 1);

        FloorStyle neon = S("Neon Grid", FloorPattern.NeonGrid, new Color(0.03f, 0.02f, 0.08f), new Color(0.1f, 0.85f, 1f),
                            new Color(1f, 0.3f, 0.9f), 256, false, 6f, 4, 0.7f, 1.6f);
        neon.pulseSpeed = 0.35f; neon.pulseAmount = 0.35f;
        yield return neon;

        yield return NeonVariant("Neon Drift", FloorPattern.NeonDrift, new Color(0.03f, 0.02f, 0.08f), new Color(0.1f, 0.85f, 1f), new Color(1f, 0.3f, 0.9f));
        yield return NeonVariant("Neon Merge", FloorPattern.NeonMerge, new Color(0.02f, 0.03f, 0.08f), new Color(0.2f, 1f, 0.6f), new Color(1f, 0.9f, 0.3f));
        yield return NeonVariant("Neon Wave", FloorPattern.NeonWave, new Color(0.04f, 0.01f, 0.08f), new Color(1f, 0.3f, 0.8f), new Color(0.3f, 0.9f, 1f));
        yield return NeonVariant("Neon Prism", FloorPattern.NeonPrism, new Color(0.02f, 0.02f, 0.05f), new Color(1f, 0.2f, 0.3f), new Color(1f, 1f, 1f));

        yield return S("Retro Checker", FloorPattern.Checker, new Color(0.92f, 0.92f, 0.9f), new Color(0.12f, 0.12f, 0.14f),
                       Color.black, 64, true, 4f, 4, 0.45f);

        yield return S("Pixel Grass", FloorPattern.PixelGrass, new Color(0.22f, 0.55f, 0.18f), new Color(0.36f, 0.72f, 0.25f),
                       new Color(0.95f, 0.35f, 0.55f), 48, true, 4f, 1, 0.05f);

        yield return S("Wood Planks", FloorPattern.WoodPlanks, new Color(0.55f, 0.35f, 0.18f), new Color(0.42f, 0.25f, 0.12f),
                       new Color(0.16f, 0.09f, 0.05f), 64, true, 5f, 6, 0.25f);

        yield return S("Cobblestone", FloorPattern.Cobblestone, new Color(0.55f, 0.55f, 0.58f), new Color(0.42f, 0.4f, 0.38f),
                       new Color(0.12f, 0.12f, 0.13f), 128, true, 5f, 6, 0.15f);

        // The big floors below are ONE huge picture (about 40 world units wide, drawn a few milliseconds per frame) instead of a small tile repeated many times.
        FloorStyle marble = S("Marble", FloorPattern.Marble, new Color(0.95f, 0.94f, 0.92f), new Color(0.55f, 0.55f, 0.6f),
                              new Color(0.78f, 0.66f, 0.42f), 1024, false, 40f, 14, 0.9f);
        marble.stretchToFloor = true; marble.detail = 5; marble.shaderGround = 2; marble.shaderScale = 7f;
        yield return marble;

        FloorStyle lava = S("Lava", FloorPattern.Lava, new Color(0.12f, 0.05f, 0.04f), new Color(1f, 0.25f, 0.02f),
                            new Color(1f, 0.85f, 0.3f), 768, true, 40f, 36, 0.35f, 1.4f);
        lava.pulseSpeed = 0.5f; lava.pulseAmount = 0.4f; lava.scrollSpeed = new Vector2(0.0015f, 0.0009f); lava.detail = 8; lava.shaderGround = 1; lava.shaderScale = 6f;
        yield return lava;

        FloorStyle space = S("Deep Space", FloorPattern.Space, new Color(0.01f, 0.01f, 0.04f), new Color(0.35f, 0.1f, 0.55f),
                             new Color(0.1f, 0.45f, 0.8f), 1024, false, 40f, 1, 0.6f, 1.1f);
        space.scrollSpeed = new Vector2(0.001f, 0.003f); space.detail = 2; space.shaderGround = 4; space.shaderScale = 9f;
        yield return space;

        FloorStyle ice = S("Ice", FloorPattern.Ice, new Color(0.7f, 0.85f, 0.95f), new Color(0.5f, 0.7f, 0.9f),
                           new Color(0.97f, 0.99f, 1f), 1024, false, 40f, 28, 0.95f);
        ice.stretchToFloor = true; ice.detail = 6; ice.shaderGround = 3; ice.shaderScale = 8f;
        yield return ice;

        FloorStyle disco = S("Disco", FloorPattern.Disco, new Color(0.05f, 0.05f, 0.07f), Color.white, Color.white,
                             64, true, 6f, 4, 0.8f, 1.3f);
        disco.beatSeconds = 0.5f;
        yield return disco;

        FloorStyle honey = S("Honeycomb", FloorPattern.Honeycomb, new Color(1f, 0.72f, 0.15f), new Color(0.72f, 0.35f, 0.02f),
                             new Color(0.35f, 0.2f, 0.05f), 256, false, 5f, 6, 0.85f, 0.25f);
        yield return honey;

        yield return S("Bricks", FloorPattern.Bricks, new Color(0.62f, 0.24f, 0.17f), new Color(0.5f, 0.2f, 0.15f),
                       new Color(0.62f, 0.6f, 0.55f), 64, true, 4f, 8, 0.1f);

        FloorStyle circuit = S("Circuit Board", FloorPattern.CircuitBoard, new Color(0.04f, 0.3f, 0.14f), new Color(0.95f, 0.7f, 0.25f),
                               new Color(0.06f, 0.06f, 0.07f), 256, false, 6f, 8, 0.55f, 0.35f);
        circuit.pulseSpeed = 0.25f; circuit.pulseAmount = 0.5f;
        yield return circuit;

        yield return S("Sand Dunes", FloorPattern.SandDunes, new Color(0.93f, 0.8f, 0.55f), new Color(0.75f, 0.58f, 0.35f),
                       Color.white, 128, false, 6f, 7, 0.1f);

        FloorStyle ocean = S("Ocean", FloorPattern.Ocean, new Color(0.02f, 0.18f, 0.35f), new Color(0.05f, 0.4f, 0.55f),
                             new Color(0.6f, 0.95f, 1f), 256, false, 7f, 5, 0.95f, 0.35f);
        ocean.scrollSpeed = new Vector2(0.03f, 0.018f);
        yield return ocean;

        yield return S("Snow", FloorPattern.Snow, new Color(0.97f, 0.98f, 1f), new Color(0.74f, 0.8f, 0.9f),
                       Color.white, 128, false, 6f, 1, 0.75f);

        yield return S("Tartan", FloorPattern.Tartan, new Color(0.55f, 0.06f, 0.08f), new Color(0.05f, 0.12f, 0.3f),
                       new Color(0.95f, 0.8f, 0.2f), 128, true, 4f, 2, 0.05f);

        FloorStyle tread = S("Tread Plate", FloorPattern.TreadPlate, new Color(0.55f, 0.57f, 0.6f), new Color(0.35f, 0.36f, 0.4f),
                             new Color(0.92f, 0.93f, 0.95f), 256, false, 4f, 6, 0.7f);
        tread.metallic = 0.85f;
        yield return tread;

        FloorStyle holo = S("Hologram", FloorPattern.Hologram, new Color(0.6f, 0.7f, 0.9f), Color.white,
                            Color.white, 128, false, 6f, 4, 0.9f, 0.9f);
        holo.scrollSpeed = new Vector2(0.06f, 0.03f); holo.pulseSpeed = 0.6f; holo.pulseAmount = 0.3f;
        yield return holo;

        FloorStyle crystal = S("Crystal", FloorPattern.Crystal, new Color(0.55f, 0.2f, 0.85f), new Color(0.2f, 0.55f, 0.95f),
                               new Color(0.95f, 0.85f, 1f), 256, false, 5f, 6, 0.95f, 0.7f);
        crystal.pulseSpeed = 0.3f; crystal.pulseAmount = 0.4f;
        yield return crystal;

        FloorStyle matrix = S("Matrix Rain", FloorPattern.MatrixRain, new Color(0.0f, 0.03f, 0.0f), new Color(0.15f, 1f, 0.35f),
                              new Color(0.85f, 1f, 0.9f), 1024, true, 40f, 128, 0.5f, 1.3f);
        matrix.scrollSpeed = new Vector2(0f, 0.0375f); matrix.detail = 6; matrix.shaderGround = 8; matrix.shaderScale = 40f;
        yield return matrix;

        yield return S("Hazard Stripes", FloorPattern.Hazard, new Color(0.98f, 0.8f, 0.05f), new Color(0.08f, 0.08f, 0.08f),
                       new Color(0.45f, 0.42f, 0.38f), 128, false, 4f, 4, 0.3f);

        foreach (FloorStyle themed in ThemedStyles()) yield return themed;
        foreach (FloorStyle extra in ExtraStyles()) yield return extra;
        foreach (FloorStyle retro in RetroStyles()) yield return retro;
        foreach (FloorStyle world in WorldStyles()) yield return world;
        foreach (FloorStyle live in LiveStyles()) yield return live;
    }

    /// <summary>Part of DefaultStyles: the themed floors (arena, radar, auras) and the first animated ones, in menu order.</summary>
    private static IEnumerable<FloorStyle> ThemedStyles()
    {
        // ---- Dragon Ball themed ----
        yield return S("Tournament Stage", FloorPattern.TournamentTiles, new Color(0.86f, 0.84f, 0.78f), new Color(0.72f, 0.7f, 0.64f),
                       new Color(0.3f, 0.28f, 0.26f), 256, false, 6f, 4, 0.25f);

        FloorStyle radar = S("Radar", FloorPattern.DragonRadar, new Color(0.04f, 0.2f, 0.08f), new Color(0.3f, 0.8f, 0.35f),
                             new Color(1f, 0.55f, 0.05f), 512, false, 36f, 18, 0.7f, 0.9f);
        radar.stretchToFloor = true; radar.animateFps = 8f; radar.pulseSpeed = 0.5f; radar.pulseAmount = 0.2f;   // (one big radar screen; its blips glow and ping)
        yield return radar;


        // ---- Dragon Ball Z themed ----
        yield return S("Alien Planet", FloorPattern.PixelGrass, new Color(0.15f, 0.55f, 0.45f), new Color(0.3f, 0.72f, 0.55f),
                       new Color(0.95f, 0.85f, 0.3f), 48, true, 4f, 1, 0.05f);


        FloorStyle battle = S("Battle Crater", FloorPattern.BattleCrater, new Color(0.5f, 0.4f, 0.3f), new Color(0.36f, 0.29f, 0.22f),
                              new Color(0.12f, 0.09f, 0.07f), 1024, false, 40f, 7, 0.1f, 0.1f);
        battle.stretchToFloor = true; battle.detail = 6;   // (cells = how many smaller craters)
        yield return battle;

        yield return S("Chamber Tiles", FloorPattern.Checker, new Color(0.98f, 0.98f, 0.97f), new Color(0.93f, 0.93f, 0.92f),
                       Color.white, 64, false, 6f, 2, 0.9f);

        // ---- Dragon Ball Super themed ----
        FloorStyle top = S("Power Tournament", FloorPattern.PowerArena, new Color(0.62f, 0.66f, 0.74f), new Color(0.42f, 0.45f, 0.55f),
                           new Color(0.4f, 0.85f, 1f), 256, false, 6f, 6, 0.45f, 0.45f);
        top.pulseSpeed = 0.4f; top.pulseAmount = 0.4f;
        yield return top;

        FloorStyle ssj = S("Super Aura", FloorPattern.KiAura, new Color(0.35f, 0.18f, 0.0f), new Color(1f, 0.78f, 0.1f),
                           new Color(1f, 1f, 0.85f), 256, false, 6f, 1, 0.6f, 1.3f);
        ssj.scrollSpeed = new Vector2(0f, 0.0525f); ssj.pulseSpeed = 1.5f; ssj.pulseAmount = 0.3f; ssj.detail = 7; ssj.tileWorldSize = 40f; ssj.resolution = 1024; ssj.shaderGround = 6; ssj.shaderScale = 40f;
        yield return ssj;

        FloorStyle ui = S("Divine Aura", FloorPattern.KiAura, new Color(0.12f, 0.14f, 0.25f), new Color(0.65f, 0.75f, 1f),
                          new Color(1f, 1f, 1f), 256, false, 6f, 1, 0.8f, 1.2f);
        ui.scrollSpeed = new Vector2(0f, 0.0375f); ui.pulseSpeed = 0.7f; ui.pulseAmount = 0.35f; ui.seed = 91; ui.detail = 7; ui.tileWorldSize = 40f; ui.resolution = 1024; ui.shaderGround = 6; ui.shaderScale = 40f;
        yield return ui;

        FloorStyle blue = S("Blue Aura", FloorPattern.KiAura, new Color(0.0f, 0.12f, 0.3f), new Color(0.1f, 0.7f, 1f),
                            new Color(0.85f, 1f, 1f), 256, false, 6f, 1, 0.7f, 1.3f);
        blue.scrollSpeed = new Vector2(0f, 0.0525f); blue.pulseSpeed = 1.2f; blue.pulseAmount = 0.3f; blue.seed = 57; blue.detail = 7; blue.tileWorldSize = 40f; blue.resolution = 1024; blue.shaderGround = 6; blue.shaderScale = 40f;
        yield return blue;

        FloorStyle god = S("God Aura", FloorPattern.KiAura, new Color(0.3f, 0.02f, 0.05f), new Color(1f, 0.25f, 0.3f),
                           new Color(1f, 0.85f, 0.75f), 256, false, 6f, 1, 0.6f, 1.3f);
        god.scrollSpeed = new Vector2(0f, 0.045f); god.pulseSpeed = 1f; god.pulseAmount = 0.3f; god.seed = 73; god.detail = 7; god.tileWorldSize = 40f; god.resolution = 1024; god.shaderGround = 6; god.shaderScale = 40f;
        yield return god;

        FloorStyle hakai = S("Death Energy", FloorPattern.EnergyWisps, new Color(0.04f, 0.0f, 0.08f), new Color(0.55f, 0.1f, 0.85f),
                             new Color(1f, 0.7f, 1f), 256, false, 7f, 1, 0.6f, 1.2f);
        hakai.scrollSpeed = new Vector2(0.0035f, 0.006f); hakai.pulseSpeed = 0.5f; hakai.pulseAmount = 0.4f; hakai.detail = 6; hakai.tileWorldSize = 40f; hakai.resolution = 1024; hakai.shaderGround = 7; hakai.shaderScale = 40f;
        yield return hakai;

        // ---- Animated ----
        FloorStyle lasers = S("Laser Corridor", FloorPattern.LaserHall, new Color(0.2f, 0.34f, 0.36f), new Color(0.6f, 0.95f, 1f),
                              new Color(1f, 0.08f, 0.05f), 128, false, 18f, 8, 0.8f, 1.1f);   // (scaled up: bigger panels and beams)
        lasers.animateFps = 14f; lasers.metallic = 0.3f; lasers.shaderGround = 10; lasers.shaderScale = 18f;
        yield return lasers;

        FloorStyle liquid = S("Liquid Flow", FloorPattern.Liquid, new Color(0.02f, 0.15f, 0.35f), new Color(0.1f, 0.55f, 0.75f),
                              new Color(0.85f, 1f, 1f), 768, false, 40f, 12, 0.95f, 0.25f);
        liquid.detail = 3; liquid.scrollSpeed = new Vector2(0.004f, 0.006f); liquid.pulseSpeed = 0.3f; liquid.pulseAmount = 0.15f;   // (one big picture that drifts)
        liquid.shaderGround = 9; liquid.shaderScale = 40f;
        yield return liquid;

        FloorStyle clouds = S("Moving Clouds", FloorPattern.CloudSea, new Color(0.35f, 0.6f, 0.9f), new Color(0.72f, 0.78f, 0.88f),
                              Color.white, 256, false, 5f, 1, 0.2f);
        clouds.scrollSpeed = new Vector2(0.015f, 0.006f); clouds.fitToView = true;
        yield return clouds;

        FloorStyle kaleido = S("Kaleidoscope", FloorPattern.Kaleidoscope, new Color(0.02f, 0.02f, 0.05f), Color.white,
                               new Color(0.05f, 0.05f, 0.08f), 256, false, 8f, 8, 0.7f, 0.6f);
        kaleido.animateFps = 15f; kaleido.fitToView = true;
        yield return kaleido;

        FloorStyle pac = S("Pac-Man", FloorPattern.PacMan, new Color(0.15f, 0.25f, 1f), new Color(1f, 0.8f, 0.65f),
                           Color.black, 256, true, 8f, 1, 0.4f, 0.8f);
        pac.animateFps = 20f; pac.tileAspect = 19f / 21f; pac.fitToView = true;
        yield return pac;
    }

    /// <summary>The second batch: space, tech, trippy, horror, a noir smoker, a skyline and ground-and-sky scenes (patterns in PixelFloorScenes.cs).</summary>
    private static IEnumerable<FloorStyle> ExtraStyles()
    {
        FloorStyle St(string name, FloorPattern p, Color a, Color b, Color c, int res, float tile, int cells, float fps, bool fit, float glow = 0f, float smooth = 0.4f)
        {
            FloorStyle f = S(name, p, a, b, c, res, false, tile, cells, smooth, glow);
            f.animateFps = fps; f.fitToView = fit;
            return f;
        }
        // ---- Space ----
        yield return St("Hyperspace", FloorPattern.Hyperspace, new Color(0f, 0f, 0.03f), new Color(0.5f, 0.7f, 1f), new Color(0.9f, 0.5f, 1f), 192, 8f, 6, 24f, true, 1f);
        // (Moon Surface and Red Planet: one big picture with a big crater, smaller ones and pits, like Battle Crater; cells = how many smaller craters.)
        FloorStyle moon = St("Moon Surface", FloorPattern.MoonSurface, new Color(0.55f, 0.55f, 0.57f), new Color(0.4f, 0.4f, 0.43f), new Color(0.15f, 0.15f, 0.17f), 1024, 40f, 10, 0f, false, 0f, 0.1f);
        moon.stretchToFloor = true; moon.detail = 6; moon.variant = 1;
        yield return moon;
        FloorStyle red = St("Red Planet", FloorPattern.MarsSurface, new Color(0.72f, 0.36f, 0.2f), new Color(0.52f, 0.25f, 0.14f), new Color(0.12f, 0.07f, 0.06f), 1024, 40f, 8, 0f, false, 0f, 0.1f);
        red.stretchToFloor = true; red.detail = 6; red.variant = 2;
        yield return red;
        // ---- Digital / tech ----
        // (Data Streams is drawn by the world-space ground shader in the game, pattern 5: lanes of packets over the whole floor circle, never stretched; the painted version is the menu picture / fallback.)
        FloorStyle streams = St("Data Streams", FloorPattern.DataStream, new Color(0.01f, 0.02f, 0.05f), new Color(0.1f, 0.8f, 1f), new Color(1f, 0.3f, 0.7f), 192, 8f, 16, 20f, true, 1f);
        streams.shaderGround = 5; streams.shaderScale = 1.1f;
        yield return streams;
        yield return St("Circuit Flow", FloorPattern.CircuitFlow, new Color(0.02f, 0.07f, 0.08f), new Color(0.1f, 0.7f, 0.8f), new Color(0.7f, 1f, 1f), 768, 36f, 12, 8f, false, 0.8f);   // (the circuit board is drawn once; only the sparks move)
        FloorStyle hex = St("Cyber Hex", FloorPattern.CyberHex, new Color(0.02f, 0.03f, 0.07f), new Color(0.2f, 0.55f, 1f), new Color(0.6f, 0.3f, 1f), 1024, 36f, 13, 0f, false, 0.9f);
        hex.pulseSpeed = 0.5f; hex.pulseAmount = 0.3f;
        yield return hex;
        yield return St("Glitch", FloorPattern.Glitch, new Color(0.03f, 0.03f, 0.05f), new Color(0.9f, 0.2f, 0.6f), new Color(0.2f, 1f, 0.9f), 192, 8f, 1, 14f, true, 0.4f);
        // ---- Trippy ----
        yield return St("Psychedelic", FloorPattern.Psychedelic, new Color(0.3f, 0.1f, 0.5f), Color.white, Color.white, 192, 8f, 1, 20f, true, 0.6f);
        yield return St("Hypno Spiral", FloorPattern.HypnoSpiral, new Color(0.05f, 0f, 0.15f), new Color(1f, 0.2f, 0.8f), Color.white, 192, 8f, 8, 20f, true, 0.4f);
        yield return St("Plasma", FloorPattern.Plasma, new Color(0.1f, 0f, 0.4f), new Color(1f, 0.2f, 0.5f), new Color(1f, 0.9f, 0.2f), 128, 8f, 1, 20f, true, 0.7f);
        yield return St("Op Art", FloorPattern.OpArt, Color.black, Color.white, Color.gray, 192, 8f, 6, 20f, true, 0f);
        // ---- Horror ----
        FloorStyle haunted = St("Haunted Tiles", FloorPattern.HauntedTiles, new Color(0.1f, 0.1f, 0.11f), new Color(0.55f, 0.55f, 0.52f), new Color(0.35f, 0.05f, 0.04f), 1024, 36f, 26, 0f, false, 0f, 0.25f);
        haunted.detail = 5;   // (drawn once; the lights flicker by dimming the material)
        yield return haunted;
        FloorStyle yard = St("Graveyard", FloorPattern.Graveyard, new Color(0.1f, 0.08f, 0.06f), new Color(0.2f, 0.17f, 0.12f), new Color(0.55f, 0.65f, 0.7f), 1024, 40f, 1, 0f, false, 0f, 0.1f);
        yard.detail = 6;
        yield return yard;
        yield return St("Watching Eyes", FloorPattern.WatchingEyes, new Color(0.02f, 0.02f, 0.03f), Color.black, new Color(1f, 0.1f, 0.05f), 1024, 36f, 11, 10f, false, 1.2f);   // (the ground is drawn once; only the eyes that blink are repainted)
        // ---- Scenes ----
        yield return St("City Skyline", FloorPattern.Skyline, new Color(0.02f, 0.03f, 0.12f), new Color(0.55f, 0.25f, 0.45f), new Color(0.06f, 0.05f, 0.12f), 256, 8f, 1, 8f, true, 0.3f);
        yield return St("Meadow Horizon", FloorPattern.GroundSky, new Color(0.25f, 0.5f, 0.9f), new Color(0.75f, 0.88f, 0.98f), new Color(0.3f, 0.62f, 0.2f), 256, 8f, 1, 0f, true, 0f, 0.15f);
        yield return St("Desert Dusk", FloorPattern.GroundSky, new Color(0.25f, 0.1f, 0.4f), new Color(1f, 0.6f, 0.3f), new Color(0.8f, 0.55f, 0.3f), 256, 8f, 2, 0f, true, 0f, 0.1f);
        yield return St("Arctic Dawn", FloorPattern.GroundSky, new Color(0.05f, 0.1f, 0.3f), new Color(0.7f, 0.6f, 0.7f), new Color(0.9f, 0.95f, 1f), 256, 8f, 3, 6f, true, 0.1f, 0.3f);
        yield return St("Twin Moons", FloorPattern.GroundSky, new Color(0.05f, 0.02f, 0.2f), new Color(0.5f, 0.2f, 0.45f), new Color(0.05f, 0.2f, 0.22f), 256, 8f, 4, 6f, true, 0.4f, 0.3f);
        // ---- Mining ----
        yield return St("Mine Track", FloorPattern.MineTrack, new Color(0.3f, 0.27f, 0.24f), new Color(0.5f, 0.46f, 0.42f), new Color(1f, 0.85f, 0.3f), 256, 6f, 4, 0f, false, 0f, 0.15f);
        yield return St("Ore Veins", FloorPattern.OreVeins, new Color(0.14f, 0.14f, 0.16f), new Color(1f, 0.75f, 0.25f), new Color(0.3f, 0.85f, 1f), 256, 6f, 5, 0f, false, 0.15f, 0.35f);
        yield return St("Rock Strata", FloorPattern.Strata, new Color(0.55f, 0.4f, 0.28f), new Color(0.72f, 0.6f, 0.45f), new Color(0.38f, 0.3f, 0.26f), 256, 6f, 8, 0f, false, 0f, 0.1f);
        // ---- Farming ----
        yield return St("Crop Rows", FloorPattern.CropRows, new Color(0.2f, 0.12f, 0.07f), new Color(0.4f, 0.26f, 0.14f), new Color(0.3f, 0.7f, 0.22f), 256, 6f, 8, 0f, false, 0f, 0.1f);
        yield return St("Wheat Field", FloorPattern.WheatField, new Color(0.62f, 0.45f, 0.14f), new Color(0.95f, 0.8f, 0.35f), new Color(1f, 0.92f, 0.55f), 128, 6f, 1, 15f, false, 0f, 0.1f);
        yield return St("Farm Patchwork", FloorPattern.Patchwork, new Color(0.12f, 0.34f, 0.12f), new Color(0.88f, 0.72f, 0.28f), new Color(0.38f, 0.24f, 0.14f), 256, 8f, 4, 0f, false, 0f, 0.1f);
    }
}
