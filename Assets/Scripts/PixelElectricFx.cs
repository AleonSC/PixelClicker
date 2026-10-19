// ---- System notes (moved here from CLAUDE.md) ----
// PixelElectricFx.cs
//   Looks / effects of the Electrician pixels: PixelLook.electricFx (ElectricAmbient) attaches per-type ambient particles (Wire copper
//   sparks along the edges, Battery rising green bubbles, Capacitor crackling blue static, Resistor red heat embers, Transistor cold
//   blips, Fuse a spark fountain on top, Bulb warm motes), Hit / Break (called from CollectInternal for IsElectricalBlock types,
//   disableFarmFx) throw sparks plus coloured arcs (FractureFx.Play(..., core, glow); smoke for Resistor / Fuse), and each pixel has
//   its own synthesized click sound (PixelSynth.Elec(kind, seed): twang, thunk + hum, charge whine + snap, buzz, digital blip, hiss +
//   pop, glassy ting; used while the pixel_<type> clip list is empty); looksVersion 34. Hit / Break bursts are pooled (PixelFx.Rent),
//   and so are the FractureFx arc flashes.
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// Looks and effects for the Electrician skill's pixels (Wire, Battery, Capacitor, Resistor, Transistor, Fuse, Bulb): each has its own ambient
/// particles while it sits on the cube (<see cref="Attach"/>, driven by <see cref="PixelLook.electricFx"/>), a small effect on every non-final hit
/// (<see cref="Hit"/>) and a bigger break (<see cref="Break"/>) with arcs of its own colour (<see cref="FractureFx"/>). All code-made.
/// </summary>
public static class PixelElectricFx
{
    public static void Attach(Transform parent, Vector3 centre, Vector3 size, PixelLook.ElectricAmbient kind)
    {
        if (PixelFx.Material == null || kind == PixelLook.ElectricAmbient.None) return;
        float unit = Mathf.Max(0.05f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        PixelFx.Spec s = new PixelFx.Spec { loop = true, shape = ParticleSystemShapeType.Box, shapePosition = centre, randomDirection = 0.6f, maxParticles = 60 };
        Vector3 boxScale = size * 1.02f;
        switch (kind)
        {
            case PixelLook.ElectricAmbient.Wire:       // copper sparks creeping along the edges
                s.lifetime = new Vector2(0.15f, 0.35f); s.speed = new Vector2(0.2f, 0.7f) * unit; s.size = new Vector2(0.03f, 0.07f) * unit;
                s.gravity = 0.4f; s.colorA = new Color(1f, 0.75f, 0.4f); s.colorB = new Color(0.95f, 0.45f, 0.15f); s.rate = 10f;
                s.sizeOverLife = new Vector2(1f, 0f);
                break;
            case PixelLook.ElectricAmbient.Battery:    // slow green charge bubbles rising
                s.lifetime = new Vector2(1.2f, 2.2f); s.speed = new Vector2(0.05f, 0.15f) * unit; s.size = new Vector2(0.05f, 0.1f) * unit;
                s.gravity = -0.25f; s.colorA = new Color(0.7f, 1f, 0.45f); s.colorB = new Color(0.4f, 0.9f, 0.3f); s.rate = 5f;
                s.sizeOverLife = new Vector2(1f, 0.2f);
                s.overLife = PixelFx.FadeGradient(new Color(0.9f, 1f, 0.7f), new Color(0.5f, 1f, 0.35f), new Color(0.2f, 0.6f, 0.15f), 1f);
                break;
            case PixelLook.ElectricAmbient.Capacitor:  // crackling blue static
                s.lifetime = new Vector2(0.15f, 0.4f); s.speed = new Vector2(0.4f, 1.1f) * unit; s.size = new Vector2(0.03f, 0.07f) * unit;
                s.gravity = 0f; s.colorA = new Color(0.8f, 0.95f, 1f); s.colorB = new Color(0.3f, 0.6f, 1f); s.rate = 16f; s.randomDirection = 1f;
                s.sizeOverLife = new Vector2(1f, 0f);
                break;
            case PixelLook.ElectricAmbient.Resistor:   // dull red heat embers
                s.lifetime = new Vector2(1f, 2f); s.speed = new Vector2(0.03f, 0.12f) * unit; s.size = new Vector2(0.04f, 0.09f) * unit;
                s.gravity = -0.15f; s.colorA = new Color(1f, 0.55f, 0.2f); s.colorB = new Color(0.8f, 0.2f, 0.1f); s.rate = 6f;
                s.sizeOverLife = new Vector2(1f, 0.1f);
                s.overLife = PixelFx.FadeGradient(new Color(1f, 0.8f, 0.4f), new Color(0.9f, 0.35f, 0.15f), new Color(0.3f, 0.1f, 0.08f), 1f);
                break;
            case PixelLook.ElectricAmbient.Transistor: // tiny cold blips that flick on and off
                s.lifetime = new Vector2(0.1f, 0.22f); s.speed = new Vector2(0f, 0.05f) * unit; s.size = new Vector2(0.04f, 0.07f) * unit;
                s.gravity = 0f; s.colorA = Color.white; s.colorB = new Color(0.5f, 0.85f, 1f); s.rate = 14f; s.randomDirection = 0.2f;
                break;
            case PixelLook.ElectricAmbient.Fuse:       // a fizzing spark fountain at the top
                s.lifetime = new Vector2(0.3f, 0.7f); s.speed = new Vector2(0.6f, 1.6f) * unit; s.size = new Vector2(0.03f, 0.07f) * unit;
                s.gravity = 1.6f; s.colorA = new Color(1f, 0.95f, 0.6f); s.colorB = new Color(1f, 0.5f, 0.15f); s.rate = 18f; s.randomDirection = 0.4f;
                s.sizeOverLife = new Vector2(1f, 0f);
                s.shapePosition = centre + Vector3.up * size.y * 0.5f; boxScale = new Vector3(size.x * 0.2f, 0.05f * unit, size.z * 0.2f);
                break;
            case PixelLook.ElectricAmbient.Bulb:       // warm motes drifting up
                s.lifetime = new Vector2(1f, 1.8f); s.speed = new Vector2(0.04f, 0.14f) * unit; s.size = new Vector2(0.05f, 0.1f) * unit;
                s.gravity = -0.12f; s.colorA = new Color(1f, 0.98f, 0.7f); s.colorB = new Color(1f, 0.85f, 0.35f); s.rate = 8f;
                s.sizeOverLife = new Vector2(1f, 0.2f);
                s.overLife = PixelFx.FadeGradient(Color.white, new Color(1f, 0.95f, 0.6f), new Color(1f, 0.8f, 0.3f), 1f);
                break;
        }
        ParticleSystem ps = PixelFx.Make(parent, "Electric " + kind, s);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.scale = boxScale;
        ps.Play();
    }

    // (a pooled one-shot burst: PixelFx.Rent; it goes back to the pool by itself after 3.5 s)
    private static PixelFx.Burst Root(string name, Vector3 centre) => PixelFx.Rent(centre, 3.5f);

    private static void Colours(PixelClicker.PixelType type, out Color a, out Color b, out Color arcCore, out Color arcGlow)
    {
        switch (type)
        {
            case PixelClicker.PixelType.Wire: a = new Color(1f, 0.75f, 0.4f); b = new Color(0.9f, 0.4f, 0.12f); arcCore = new Color(1f, 0.9f, 0.7f); arcGlow = new Color(1f, 0.55f, 0.2f); break;
            case PixelClicker.PixelType.Battery: a = new Color(0.8f, 1f, 0.6f); b = new Color(0.35f, 0.85f, 0.25f); arcCore = new Color(0.9f, 1f, 0.8f); arcGlow = new Color(0.4f, 1f, 0.35f); break;
            case PixelClicker.PixelType.Capacitor: a = new Color(0.85f, 0.95f, 1f); b = new Color(0.3f, 0.55f, 1f); arcCore = new Color(0.95f, 1f, 1f); arcGlow = new Color(0.3f, 0.6f, 1f); break;
            case PixelClicker.PixelType.Resistor: a = new Color(1f, 0.6f, 0.25f); b = new Color(0.7f, 0.18f, 0.1f); arcCore = new Color(1f, 0.8f, 0.5f); arcGlow = new Color(0.9f, 0.3f, 0.1f); break;
            case PixelClicker.PixelType.Transistor: a = Color.white; b = new Color(0.45f, 0.8f, 1f); arcCore = Color.white; arcGlow = new Color(0.5f, 0.8f, 1f); break;
            case PixelClicker.PixelType.Fuse: a = new Color(1f, 0.95f, 0.6f); b = new Color(1f, 0.45f, 0.1f); arcCore = new Color(1f, 1f, 0.8f); arcGlow = new Color(1f, 0.55f, 0.15f); break;
            case PixelClicker.PixelType.Jellyfish: a = new Color(1f, 0.8f, 1f); b = new Color(0.9f, 0.35f, 1f); arcCore = new Color(1f, 0.92f, 1f); arcGlow = new Color(0.95f, 0.4f, 1f); break;
            default: a = new Color(1f, 1f, 0.8f); b = new Color(1f, 0.85f, 0.3f); arcCore = Color.white; arcGlow = new Color(1f, 0.9f, 0.4f); break;   // Bulb
        }
    }

    /// <summary>A non-final hit (progress 0-1): a few sparks and short arcs in the pixel's own colours.</summary>
    public static void Hit(PixelClicker.PixelType type, Vector3 centre, float size, float progress)
    {
        if (PixelFx.Material == null) return;
        Colours(type, out Color a, out Color b, out Color core, out Color glow);
        PixelFx.Burst root = Root("Electric Hit", centre);
        root.Add("Sparks", new PixelFx.Spec
        {
            lifetime = new Vector2(0.25f, 0.6f), speed = new Vector2(0.8f, 2.6f) * size, size = new Vector2(0.04f, 0.1f) * size, gravity = 1.2f,
            colorA = a, colorB = b, burst = Mathf.RoundToInt(Mathf.Lerp(5f, 12f, progress)), sizeOverLife = new Vector2(1f, 0f),
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f, randomDirection = 0.15f, maxParticles = 20,
        }).Play();
        FractureFx.Play(centre, size, Mathf.RoundToInt(Mathf.Lerp(2f, 5f, progress)), 0.7f, 0.2f, core, glow);
    }

    /// <summary>The break: a burst of the pixel's own sparks and a spray of arcs, with a bit of character per type.</summary>
    public static void Break(PixelClicker.PixelType type, Vector3 centre, float size)
    {
        if (PixelFx.Material == null) return;
        Colours(type, out Color a, out Color b, out Color core, out Color glow);
        PixelFx.Burst root = Root("Electric Break", centre);
        int count = 30;
        float gravity = 1.4f, speedScale = 1f;
        int arcs = 7; float reach = 2f, arcSeconds = 0.35f;
        switch (type)
        {
            case PixelClicker.PixelType.Jellyfish: arcs = 12; count = 36; reach = 2.8f; arcSeconds = 0.4f; gravity = 0.3f; break;                   // a violent pink-violet discharge
            case PixelClicker.PixelType.Wire: arcs = 4; reach = 2.6f; arcSeconds = 0.25f; gravity = 1.8f; break;          // snipped copper zips off
            case PixelClicker.PixelType.Battery: arcs = 8; count = 36; reach = 2.2f; gravity = 0.4f; break;                 // a swelling green discharge
            case PixelClicker.PixelType.Capacitor: arcs = 12; reach = 3.2f; arcSeconds = 0.45f; gravity = 0.2f; break;      // the stored charge arcs out
            case PixelClicker.PixelType.Resistor: arcs = 3; count = 34; gravity = 0.9f; speedScale = 0.6f; break;           // glowing heat, few arcs
            case PixelClicker.PixelType.Transistor: arcs = 5; reach = 2.8f; arcSeconds = 0.18f; break;                       // quick, sharp
            case PixelClicker.PixelType.Fuse: arcs = 3; count = 44; gravity = 2.2f; speedScale = 1.3f; break;                // a spark fountain
            default: arcs = 9; reach = 1.8f; arcSeconds = 0.3f; gravity = -0.1f; count = 40; break;                         // Bulb: a pop of light
        }
        root.Add("Burst", new PixelFx.Spec
        {
            lifetime = new Vector2(0.5f, 1.1f), speed = new Vector2(1f, 3.6f) * size * speedScale, size = new Vector2(0.05f, 0.14f) * size, gravity = gravity,
            colorA = a, colorB = b, burst = count, sizeOverLife = new Vector2(1f, 0f),
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.55f, randomDirection = 0.1f, maxParticles = count + 10,
        }).Play();
        if (type == PixelClicker.PixelType.Resistor || type == PixelClicker.PixelType.Fuse)
            root.Add("Smoke", new PixelFx.Spec
            {
                lifetime = new Vector2(0.9f, 1.6f), speed = new Vector2(0.2f, 0.8f) * size, size = new Vector2(0.2f, 0.45f) * size, gravity = -0.1f,
                colorA = new Color(0.3f, 0.3f, 0.32f, 0.5f), colorB = new Color(0.18f, 0.18f, 0.2f, 0.4f), burst = 8, sizeOverLife = new Vector2(0.6f, 1.6f),
                shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.4f, randomDirection = 0.1f, maxParticles = 12,
                overLife = PixelFx.FadeGradient(Color.white, Color.white, Color.white, 0.8f),
            }).Play();
        FractureFx.Play(centre, size, arcs, reach, arcSeconds, core, glow);
    }
}
