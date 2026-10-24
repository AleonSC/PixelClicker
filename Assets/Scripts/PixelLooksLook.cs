// ---- System notes ----
// PixelLooksLook.cs
//   Part of PixelLooks: the PixelLook data class (one pixel type's extra 3D style, edited in the list on PixelClicker). The system notes are in PixelLooks.cs.
// ---------------------------------------------------
using System;
using UnityEngine;

/// <summary>
/// Extra visual style for one pixel type, applied on top of the tier's own settings (colour, glow, translucent...).
/// The tier's colour is still what the UI shows; this only changes how the 3D pixel looks. Edit the list on PixelClicker.
/// </summary>
[Serializable]
public class PixelLook
{
    [Tooltip("The pixel type this style belongs to.")]
    public PixelClicker.PixelType type = PixelClicker.PixelType.White;

    [Header("Colour and surface")]
    [Tooltip("Draw the pixel in 'Color' instead of the tier's colour (the UI keeps using the tier colour).")]
    public bool useColor = false;

    [Tooltip("Colour of the 3D pixel (its alpha matters for see-through pixels).")]
    public Color color = Color.white;

    [Range(0f, 1f)]
    [Tooltip("Multiplies the colour's alpha. Lower = more see-through (only for translucent pixels).")]
    public float alpha = 1f;

    [Tooltip("Draw this pixel see-through even if its tier isn't marked Translucent.")]
    public bool forceTranslucent = false;

    [Range(-1f, 1f)]
    [Tooltip("Metallic surface (0-1). -1 = leave the material's own value.")]
    public float metallic = -1f;

    [Range(-1f, 1f)]
    [Tooltip("Glossiness (0-1). -1 = leave the material's own value.")]
    public float smoothness = -1f;

    [Min(0f)]
    [Tooltip("Plain self-lighting (emission) in the pixel's colour, even if the tier doesn't glow. 0 = none.")]
    public float emission = 0f;

    [Min(0f)]
    [Tooltip("Multiplies the glow of a glowing tier (1 = unchanged, 0.3 = much dimmer).")]
    public float glowScale = 1f;

    /// <summary>A gentle built-in surface for the basic pixels (the texture is near-white, so the colour shows through it).</summary>
    public enum BasicSurface { None = 0, Grain = 1, Brushed = 2, Sheen = 3, Mosaic = 4 }

    /// <summary>Crisp drawn surfaces for the skill packs' base blocks (PixelBlockTextures.cs). The look's colour should be white.</summary>
    public enum BlockSurface { None = 0, Wood = 1, Dirt = 2, Stone = 3, Seed = 4, Pest = 5, Whale = 6, Bottle = 7, Twigs = 8, Pinecone = 9, Moss = 10, LeafBlock = 11, Hive = 12 }

    [Tooltip("Wood, Dirt or Stone: a crisp 64 px pixel-art surface drawn on every face (set the look's colour to white).")]
    public BlockSurface blockSurface = BlockSurface.None;

    [Header("Surface texture")]
    [Tooltip("A subtle drawn surface: Grain (fine chalky speckle), Brushed (horizontal brushed-metal streaks), Sheen (soft diagonal highlights on dark pixels), Mosaic (a faint pixel-art mosaic with a lighter top edge on every face). Used by White, Gray, Black, Red, Green and Blue.")]
    public BasicSurface basicSurface = BasicSurface.None;

    [Tooltip("Draw thin white streaks on a dark base (use a white Color above so the texture shows true). Used by Obsidian.")]
    public bool streakTexture = false;

    [Tooltip("Tough pixels (several clicks to collect): white cracks spread over the pixel with every hit, more with each click.")]
    public bool damageCracks = false;

    [Header("Outline")]
    [Tooltip("Draw glowing neon lines along the 12 edges of the cube.")]
    public bool outline = false;

    [Tooltip("Use the pixel's tier colour for the outline (otherwise 'Outline Color').")]
    public bool outlineUsesTierColor = true;

    [Tooltip("Outline colour when 'Outline Uses Tier Colour' is off.")]
    public Color outlineColor = Color.white;

    [Tooltip("A bevel instead of neon: one thin SOLID line of exactly the outline colour lying flat along each edge (no glow, no bright core, no halo), so the cube looks like it has bevelled edges. Off = the glowing neon lines.")]
    public bool outlineBevel = false;

    [Tooltip("With 'Outline Shade': take the shade from the pixel's TIER colour even when the look draws the pixel in another colour (Obsidian and Seed are drawn white and get their colour from a texture).")]
    public bool outlineShadeOfTier = false;

    [Range(-1f, 1f)]
    [Tooltip("Draw the outline as a different SHADE of the pixel's own colour instead of 'Outline Color': above 0 lightens it towards white (0.4 = a lighter tint), below 0 darkens it towards black (-0.4 = a darker shade). 0 = off (use the colours above).")]
    public float outlineShade = 0f;

    [Range(0.005f, 0.3f)]
    [Tooltip("Line thickness as a fraction of the cube's width.")]
    public float outlineThickness = 0.045f;

    [Range(0f, 1f)]
    [Tooltip("Overall strength of the outline (0 = invisible, 1 = full).")]
    public float outlineStrength = 1f;

    [Header("Face circles")]
    [Tooltip("Draw a flat circle on each of the 6 faces of the cube.")]
    public bool faceCircles = false;

    [Tooltip("Colour of the face circles.")]
    public Color faceCircleColor = Color.black;

    [Range(0.05f, 0.5f)]
    [Tooltip("Circle radius as a fraction of the cube's width (0.5 = touches the edges).")]
    public float faceCircleRadius = 0.3f;

    [Tooltip("Face circles only: draw each disc as a little vortex - black in the middle, a glowing ring in 'Face Ring Colour' around its edge that fades outwards - instead of a plain flat disc.")]
    public bool faceRing = false;

    [Tooltip("Colour of the glowing ring round each face circle ('Face Ring').")]
    public Color faceRingColor = new Color(0.7f, 0.35f, 1f, 1f);

    [Header("Wobble (jelly)")]
    [Tooltip("The cube squashes and stretches like jelly (the live pixel and its falling copies).")]
    public bool wobble = false;

    [Range(0f, 0.5f)]
    [Tooltip("How much it squashes and stretches (0.15 = 15%).")]
    public float wobbleAmount = 0.14f;

    [Min(0f)]
    [Tooltip("How fast it wobbles.")]
    public float wobbleSpeed = 3.2f;

    [Header("Falling (husks of this type)")]
    [Tooltip("Use the bounce / friction / gravity below for this type's husks instead of the global ones.")]
    public bool customPhysics = false;

    [Range(0f, 1.5f)]
    [Tooltip("Bounciness of the husks (1 = keeps all its speed).")]
    public float bounce = 0.85f;

    [Range(0f, 1f)]
    [Tooltip("Friction of the husks (low = slides and keeps travelling).")]
    public float friction = 0.05f;

    [Min(0f)]
    [Tooltip("Multiplies the normal gravity on these husks (0.25 = falls four times more slowly).")]
    public float gravityMultiplier = 0.25f;

    [Tooltip("Husks of this type pass through the invisible screen edges instead of bouncing off them.")]
    public bool ignoreViewBounds = false;

    [Tooltip("After a few bounces the husk starts to float away and leaves the screen.")]
    public bool floatAway = false;

    [Min(1)]
    [Tooltip("How many ground bounces before it floats away.")]
    public int floatAfterBounces = 2;

    [Min(0f)]
    [Tooltip("How strongly it drifts upwards once it floats away (fraction of gravity).")]
    public float floatLift = 0.15f;

    [Min(0f)]
    [Tooltip("Sideways speed it picks up when it starts to float away.")]
    public float floatDriftSpeed = 2.5f;

    [Header("Falling like a leaf")]
    [Tooltip("The husk falls like a leaf: it swings back and forth like a pendulum, tilting into each swing, sinking fastest through the middle of a swing and slowing at its ends, until it first touches something (OldPixelFlutter).")]
    public bool flutter = false;

    [Min(0f)]
    [Tooltip("How far each swing goes to either side (husk widths).")]
    public float flutterSwing = 1.6f;

    [Min(0.1f)]
    [Tooltip("Seconds for one full back-and-forth swing.")]
    public float flutterPeriod = 1.6f;

    [Range(0f, 80f)]
    [Tooltip("How far the husk tilts into each swing (degrees).")]
    public float flutterTilt = 35f;

    [Min(0f)]
    [Tooltip("Average fall speed while it flutters (husk widths per second).")]
    public float flutterFallSpeed = 2.2f;

    [Header("Colour-blind mark")]
    [Range(0, 32)]
    [Tooltip("Colour-blind support (Settings): draws a flat shape on every face - 3 = triangle, 4 = square, 32 = circle. 0 = none. Only shown while the colour-blind setting is on.")]
    public int colorBlindSides = 0;

    [Tooltip("Turns the colour-blind shape (degrees).")]
    public float colorBlindRotation = 0f;

    [Tooltip("Colour of the colour-blind shape.")]
    public Color colorBlindColor = new Color(1f, 1f, 1f, 0.95f);

    [Range(0.05f, 0.5f)]
    [Tooltip("Size of the colour-blind shape as a fraction of the cube's width.")]
    public float colorBlindRadius = 0.28f;

    [Header("Gravity well (husks of this type pull others in)")]
    [Tooltip("A husk of this type slowly pulls other husks towards it, inside a radius shown by a very faint ring.")]
    public bool gravityWell = false;

    [Min(0.5f)]
    [Tooltip("How far the pull reaches (world units).")]
    public float wellRadius = 3.5f;

    [Min(0f)]
    [Tooltip("How far (in pull radii) a streaking meteor pixel is bent towards the well. Inside the ring it is captured. 0 = 4.")]
    public float wellFlyReach = 4f;

    [Min(0f)]
    [Tooltip("Seconds the gravity-well pixel itself lasts before it vanishes (0 = 300, i.e. 5 minutes). Other husks inside its pull radius don't age while it is working.")]
    public float wellLifetime = 300f;

    [Min(0f)]
    [Tooltip("How fast nearby husks are dragged towards it (world units per second). The drag is constant inside the ring and overcomes the floor's friction.")]
    public float wellPullSpeed = 1.6f;

    [Min(0f)]
    [Tooltip("How quickly a pixel settles into that drag speed (per second). Higher = it grabs hold harder.")]
    public float wellGrip = 8f;

    [Min(0)]
    [Tooltip("The most husks one well pulls at a time (0 = 20). Other gravity-well pixels inside its radius count towards this, so wells placed close together use up each other's slots and have to be spaced out. A pixel is only ever held by one well.")]
    public int wellMaxPixels = 20;

    [Range(0f, 1f)]
    [Tooltip("How visible the ring is (0 = invisible). Keep it very faint.")]
    public float wellRingOpacity = 0.16f;

    [Tooltip("Colour of the ring.")]
    public Color wellRingColor = Color.black;

    [Header("Special")]
    [Tooltip("A swirling dark-matter core inside the cube (best with 'Force Translucent' and a dark, see-through colour).")]
    public bool darkMatter = false;

    [Tooltip("A cube made of lightning: jagged bolts crackle along all 12 edges and arc through the inside, re-rolled many times a second (best with a dark, very see-through colour and glow).")]
    public bool lightning = false;

    [Tooltip("Colour of the bright centre of each bolt.")]
    public Color lightningCoreColor = new Color(0.92f, 1f, 1f, 1f);

    [Tooltip("Colour of the softer glow around each bolt.")]
    public Color lightningGlowColor = new Color(0.25f, 0.7f, 1f, 1f);

    [Min(0f)]
    [Tooltip("How far the bolts wander from a straight line, as a fraction of the cube's size.")]
    public float lightningJitter = 0.09f;

    [Range(0, 7)]
    [Tooltip("Dragon Cubes: how many small cubes (1-7) float inside the cube, laid out like the stars on a dragon ball. 0 = none.")]
    public int starCubes = 0;

    [Tooltip("Colour of the small cubes inside a Dragon Cube.")]
    public Color starColor = new Color(0.55f, 0.18f, 0.02f, 1f);

    [Tooltip("Seed: a small green sprout (stem and two leaves) grows out of the top of the cube.")]
    public bool sprout = false;

    [Tooltip("Mirror: a chrome texture (sky, bright horizon band and dark ground, with soft streaks) so the cube looks reflective in any render pipeline.")]
    public bool chromeTexture = false;

    [Tooltip("Mirror: a bright diagonal glint sweeps across the faces in turn.")]
    public bool shine = false;

    [Tooltip("Seed: draw a brown shell texture (tan with dark cracks that spread as the shell is hit) instead of the default dark surface texture. Needs Damage Cracks.")]
    public bool shellTexture = false;

    [Tooltip("Water: husks of this type splash on the ground (they break into smaller Water pixels that fly out and break into droplets, PixelClicker 'Water splash pieces') and are gone (whether or not anything needed watering); nearby Seed sprouts and the Seed pet are watered.")]
    public bool splash = false;

    [Tooltip("Ore pixels (Prospector's Pack): draw a rock texture speckled and veined with 'Ore Color' metal; cracks spread over it with every hit (needs Damage Cracks).")]
    public bool oreTexture = false;

    [Tooltip("The metal showing in the ore's veins and flecks.")]
    public Color oreColor = new Color(0.85f, 0.47f, 0.28f, 1f);

    [Tooltip("Treasure pixel: draw a wooden chest (gold bands, lock, rivets) on every face; light leaks out along the lid seam as the hits add up (needs Damage Cracks).")]
    public bool chestTexture = false;

    [Tooltip("Fire: flames rise off the pixel (a little stream of flame squares).")]
    public bool flames = false;

    [Tooltip("Ghost: draw a very rounded gel blob instead of a cube. Draws a different mesh; the hit box stays a cube.")]
    public bool blob = false;

    [Tooltip("Ghost: a darker nucleus drifts inside the pixel and tiny bubbles rise through it.")]
    public bool gelFx = false;
    /// <summary>The Message in a Bottle: a small 3D treasure map floats inside the (clear glass) cube - see PixelBottleMap.cs.</summary>
    public bool bottleMap = false;

    [Tooltip("Vacuum: draw a clear glass container (hollow, thin walls) with ONE round opening in its front face instead of a solid cube. Draws a different mesh; the hit box stays a cube.")]
    public bool vacuumContainer = false;

    [Tooltip("Vacuum: a glowing lip round the opening and a suction wind (streaks of air curling into it) outside it.")]
    public bool windFx = false;

    [Tooltip("Rocky pixels (Obsidian, Meteor, Rock, the ores): the cube's edges and corners are chipped into an irregular jagged shape instead of straight lines. Draws a different mesh; the hit box stays a cube.")]
    public bool jagged = false;

    [Tooltip("Meteor: dark cratered rock with glowing lava cracks that spread with every hit (needs Damage Cracks).")]
    public bool meteorTexture = false;

    [Tooltip("Meteor: a few embers rise off the cube, and the husk that streaks away leaves a fiery wake.")]
    public bool cometTail = false;

    [Tooltip("Solar: churning gold with a white-hot middle on every face.")]
    public bool sunTexture = false;

    [Tooltip("Solar: a soft turning corona with rays behind the cube, and the husk hovers and pays a trickle (swells with daylight).")]
    public bool sunFx = false;

    [Tooltip("Nuclear: radioactive motes drift off the pixel, and the HUSK makes every other husk near it age extremely fast (they vanish within a second or two).")]
    public bool radiation = false;

    /// <summary>The ambient particle effects of the farming blocks.</summary>
    public enum FarmAmbient { None = 0, Pollen = 1, Dew = 2, Crumbs = 3, Spores = 4, Chaff = 5, Sparkle = 6, Bugs = 7, Leaves = 8, Sap = 9, Hornets = 10, Termites = 11 }

    [Tooltip("Farming blocks: a loop of little particles around the cube (pollen motes, dew drips, crumbs, spores, chaff, golden sparkles, scurrying bugs).")]
    public FarmAmbient farmFx = FarmAmbient.None;
    public enum ElectricAmbient { None = 0, Wire = 1, Battery = 2, Capacitor = 3, Resistor = 4, Transistor = 5, Fuse = 6, Bulb = 7 }
    [Tooltip("Ambient particles of an Electrician pixel (Wire, Battery, Capacitor...).")] public ElectricAmbient electricFx = ElectricAmbient.None;
    [Tooltip("Violet sparks drift off the cube (Obsidian).")] public bool obsidianFx = false;

    [Tooltip("Fire: husks of this type burn - they fall while slowly shrinking away into embers, then vanish (no payout).")]
    public bool burn = false;

    [Tooltip("Husks of this type shatter into shards when they hit the ground (they are gone afterwards).")]
    public bool shatter = false;

    /// <summary>Does this look add objects to the cube (outline / core)?</summary>
    public bool HasExtras => bottleMap || gelFx || windFx || flames || radiation || cometTail || sunFx || farmFx != FarmAmbient.None || electricFx != ElectricAmbient.None || obsidianFx || outline || darkMatter || lightning || sprout || shine || starCubes > 0 || faceCircles || (colorBlindSides >= 3 && PixelDisplaySettings.ColorBlind);

    /// <summary>Does this look put its own texture on the pixel (streaks and/or damage)?</summary>
    public bool HasSurfaceTexture => meteorTexture || sunTexture || streakTexture || damageCracks || chromeTexture || oreTexture || chestTexture || blockSurface != BlockSurface.None || basicSurface != BasicSurface.None;
}
