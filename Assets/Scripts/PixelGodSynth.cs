// ---- System notes ----
// PixelGodSynth.cs
//   Code-made sounds of the three Egyptian God Cube find shows (PixelAudio.SynthFor -> PixelGodSynth.For; put your own clips in the Sounds list to replace them). Each clip is one long piece that is
//   timed to its show in PixelGodFind (start of the show = start of the clip): god_slifer (storm rumble, three thunder cracks at 0.5 / 1.0 / 1.4 s, the dragon's roar from 1.5 s, a final blast at 4.9 s),
//   stone_space / stone_mind / stone_reality / stone_power / stone_time / stone_soul (the Infinity Stones, 5.5 s, each one's own voice, the climax at 3.2 s) and stone_cubelette (six rising notes, the snap at 3.4 s, a chime at 5.0 s),
//   god_ra (a rising shimmering choir, a swell at 1.2 s, wing beats from 1.6 s, a golden chime at 4.0 s) and god_obelisk (a growing rumble, the fist rising, the slam at 2.55 s, falling stone).
// ----------------------
using UnityEngine;

public static class PixelGodSynth
{
    private const int Rate = 22050;

    public static AudioClip[] For(string id)
    {
        switch (id)
        {
            case "god_slifer": return new[] { Slifer() };
            case "god_ra": return new[] { Ra() };
            case "god_obelisk": return new[] { Obelisk() };
            case "stone_cubelette": return new[] { Cubelette() };
        }
        if (id != null && id.StartsWith("stone_"))
            for (int i = 0; i < 6; i++)
                if (id == PixelInfinity.StoneSoundId(i)) return new[] { Stone(i) };
        return null;
    }

    private static float Noise(System.Random r) => (float)r.NextDouble() * 2f - 1f;
    private static float Win(float t, float a, float b, float fade) => Mathf.Clamp01((t - a) / fade) * Mathf.Clamp01((b - t) / fade);

    private static AudioClip Finish(string name, float[] data, float level)
    {
        float peak = 0.0001f;
        for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        int fade = Rate / 4;
        for (int i = 0; i < data.Length; i++)
        {
            float edge = Mathf.Clamp01((data.Length - i) / (float)fade);
            data[i] = data[i] / peak * level * edge;
        }
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Storm and dragon: low rolling thunder, three sharp cracks, a growling roar that climbs, crackle, and a closing blast.</summary>
    private static AudioClip Slifer()
    {
        float length = 7.5f;
        int n = (int)(Rate * length);
        float[] data = new float[n];
        System.Random rng = new System.Random(8100);
        float lp = 0f, lp2 = 0f, roarPhase = 0f, subPhase = 0f;
        float[] cracks = { 0.5f, 1.0f, 1.4f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = Noise(rng);
            lp += (noise - lp) * 0.02f; lp2 += (lp - lp2) * 0.15f;
            float rumble = lp2 * 6f * (0.5f + 0.5f * Mathf.Sin(t * 1.7f)) * Win(t, 0f, 6f, 0.6f);
            float crack = 0f;
            for (int k = 0; k < cracks.Length; k++)
            {
                float d = t - cracks[k];
                if (d > 0f) crack += (noise * Mathf.Exp(-d * 28f) * 1.2f + Mathf.Sin(d * 70f * Mathf.PI * 2f) * Mathf.Exp(-d * 5f) * 0.9f);
            }
            // The roar: a sawtooth whose pitch climbs, wobbling, through a heavy low-pass.
            float rt = t - 1.5f;
            float roar = 0f;
            if (rt > 0f && rt < 3.4f)
            {
                float hz = 62f + rt * 18f + Mathf.Sin(rt * 9f) * 6f;
                roarPhase += hz / Rate;
                float saw = (roarPhase % 1f) * 2f - 1f;
                roar = (saw * 0.6f + noise * 0.25f) * Win(rt, 0f, 3.4f, 0.35f) * (0.7f + 0.3f * Mathf.Sin(rt * 14f));
            }
            // Crackling lightning while the dragon flies.
            float zap = (t > 1.5f && t < 4.9f && rng.NextDouble() < 0.004) ? Noise(rng) * 0.9f : 0f;
            // The closing blast.
            float bd = t - 4.9f;
            float blast = 0f;
            if (bd > 0f)
            {
                subPhase += (90f * Mathf.Exp(-bd * 3f) + 30f) / Rate;
                blast = Mathf.Sin(subPhase * Mathf.PI * 2f) * Mathf.Exp(-bd * 2.2f) * 1.6f + noise * Mathf.Exp(-bd * 9f) * 0.8f;
            }
            data[i] = rumble * 0.55f + crack * 0.8f + roar * 0.9f + zap * 0.4f + blast;
        }
        return Finish("God Slifer", data, 0.9f);
    }

    /// <summary>The sun and the winged dragon: a rising choir of overtones, a swell, rhythmic wing beats, a bright chime at the end.</summary>
    private static AudioClip Ra()
    {
        float length = 7.5f;
        int n = (int)(Rate * length);
        float[] data = new float[n];
        System.Random rng = new System.Random(8200);
        float lp = 0f;
        float[] partials = { 1f, 2f, 3f, 4f, 5f, 6f, 8f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = Noise(rng);
            lp += (noise - lp) * Mathf.Lerp(0.02f, 0.2f, Mathf.Clamp01(t / 3f));
            float vib = 1f + 0.004f * Mathf.Sin(t * 5.5f);
            float choir = 0f;
            float baseHz = Mathf.Lerp(110f, 165f, Mathf.Clamp01(t / 4f)) * vib;
            for (int k = 0; k < partials.Length; k++) choir += Mathf.Sin(t * baseHz * partials[k] * Mathf.PI * 2f) / (k + 1.2f);
            float choirEnv = Mathf.Clamp01(t / 2.2f) * Mathf.Clamp01((6.4f - t) / 1.0f) * (1f + 0.25f * Mathf.Sin(t * 3.1f));
            float whoosh = lp * 3.2f * Mathf.Exp(-Mathf.Abs(t - 1.2f) * 1.6f);
            float sd = t - 1.2f;
            float swell = sd > 0f ? Mathf.Sin(t * 880f * Mathf.PI * 2f) * Mathf.Exp(-sd * 2.2f) * 0.5f + noise * Mathf.Exp(-sd * 14f) * 0.5f : 0f;
            // Wing beats: low thumps every 0.45 s between 1.6 and 4.0.
            float beat = 0f;
            if (t > 1.6f && t < 4.2f)
            {
                float bt = (t - 1.6f) % 0.45f;
                beat = (Mathf.Sin(bt * 70f * Mathf.PI * 2f) * Mathf.Exp(-bt * 11f) * 0.9f + lp * Mathf.Exp(-bt * 14f) * 2.2f) * Win(t, 1.6f, 4.2f, 0.2f);
            }
            // The chime: high bell partials.
            float cd = t - 4.0f;
            float chime = 0f;
            if (cd > 0f)
                chime = (Mathf.Sin(cd * 1320f * Mathf.PI * 2f) + 0.6f * Mathf.Sin(cd * 1980f * Mathf.PI * 2f) + 0.4f * Mathf.Sin(cd * 3170f * Mathf.PI * 2f)) * Mathf.Exp(-cd * 1.6f) * 0.5f;
            data[i] = choir * choirEnv * 0.35f + whoosh * 0.5f + swell + beat * 0.8f + chime;
        }
        return Finish("God Ra", data, 0.85f);
    }

    /// <summary>The stone titan: a deepening rumble, a rising tone as the fist lifts, one gigantic slam, falling rubble and a dying drone.</summary>
    private static AudioClip Obelisk()
    {
        float length = 7.5f;
        int n = (int)(Rate * length);
        float[] data = new float[n];
        System.Random rng = new System.Random(8300);
        float lp = 0f, lp2 = 0f, riser = 0f, slam = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = Noise(rng);
            lp += (noise - lp) * 0.03f; lp2 += (lp - lp2) * 0.2f;
            float grow = Mathf.Clamp01(t / 2.4f);
            float rumble = (lp2 * 7f + Mathf.Sin(t * 38f * Mathf.PI * 2f) * 0.5f + Mathf.Sin(t * 52f * Mathf.PI * 2f) * 0.3f) * (0.15f + 0.85f * grow * grow) * Mathf.Exp(-Mathf.Max(0f, t - 2.55f) * 0.55f);
            float rise = 0f;
            if (t > 1.4f && t < 2.55f)
            {
                riser += Mathf.Lerp(50f, 180f, (t - 1.4f) / 1.15f) / Rate;
                rise = Mathf.Sin(riser * Mathf.PI * 2f) * Win(t, 1.4f, 2.55f, 0.2f) * 0.6f;
            }
            float d = t - 2.55f;
            float impact = 0f;
            if (d > 0f)
            {
                slam += (120f * Mathf.Exp(-d * 5f) + 28f) / Rate;
                impact = Mathf.Sin(slam * Mathf.PI * 2f) * Mathf.Exp(-d * 1.7f) * 2f + noise * Mathf.Exp(-d * 6f) * 1.4f + lp2 * Mathf.Exp(-d * 2f) * 4f;
                // Rubble: little stone ticks for a second after the hit.
                if (d > 0.1f && d < 1.6f && rng.NextDouble() < 0.0025) impact += Noise(rng) * 0.9f;
            }
            data[i] = rumble * 0.7f + rise + impact;
        }
        return Finish("God Obelisk", data, 0.95f);
    }

    /// <summary>One Infinity Stone: a build-up in its own voice, then the climax at 3.2 s (matching the show) and a ringing tail.</summary>
    private static AudioClip Stone(int stone)
    {
        float length = 6.2f;
        int n = (int)(Rate * length);
        float[] data = new float[n];
        System.Random rng = new System.Random(9100 + stone);
        float lp = 0f, ph1 = 0f, ph2 = 0f, ph3 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float build = Mathf.Clamp01(t / 3.2f);
            float after = Mathf.Max(0f, t - 3.2f);
            float noise = Noise(rng);
            lp += (noise - lp) * 0.05f;
            float v = 0f;
            switch (stone)
            {
                case 0: // space: a whoosh that sweeps up, a gliding sine, a deep boom
                {
                    ph1 += (200f + 900f * build * build) / Rate;
                    v += Mathf.Sin(ph1 * 6.2832f) * 0.35f * build + lp * 2.2f * build * Win(t, 0f, 3.2f, 0.4f);
                    ph2 += (55f * Mathf.Exp(-after * 1.2f)) / Rate;
                    v += Mathf.Sin(ph2 * 6.2832f) * 1.6f * Mathf.Exp(-after * 1.4f) * (t >= 3.2f ? 1f : 0f);
                    v += Mathf.Sin(t * 6.2832f * 1320f) * 0.18f * Mathf.Exp(-after * 2f) * (t >= 3.2f ? 1f : 0f);
                    break;
                }
                case 1: // mind: bright pulsing chimes on the 1.8 Hz beat, a sparkling chord at the end
                {
                    float beat = Mathf.Repeat(t * 1.8f, 1f);
                    float env = Mathf.Exp(-beat * 5f) * (0.25f + 0.75f * build);
                    v += (Mathf.Sin(t * 6.2832f * 880f) + 0.5f * Mathf.Sin(t * 6.2832f * 1320f) + 0.3f * Mathf.Sin(t * 6.2832f * 1760f)) * 0.3f * env * (t < 3.2f ? 1f : 0.2f);
                    if (t >= 3.2f) v += (Mathf.Sin(t * 6.2832f * 523f) + Mathf.Sin(t * 6.2832f * 659f) + Mathf.Sin(t * 6.2832f * 784f) + Mathf.Sin(t * 6.2832f * 1047f)) * 0.3f * Mathf.Exp(-after * 1.1f);
                    break;
                }
                case 2: // reality: a warbling detuned drone, ring-modulated glitches, a crack
                {
                    float wob = 1f + 0.04f * Mathf.Sin(t * 9f);
                    ph1 += 70f * wob / Rate; ph2 += 74.5f * wob / Rate;
                    float drone = (Mathf.Sin(ph1 * 6.2832f) + Mathf.Sin(ph2 * 6.2832f)) * 0.5f * (0.3f + 0.7f * build);
                    float gate = Mathf.Sin(t * 6.2832f * (7f + 20f * build)) > 0.2f ? 1f : 0.35f;
                    v += drone * gate * Mathf.Sign(Mathf.Sin(t * 6.2832f * 190f));
                    if (t < 3.2f && rng.NextDouble() < 0.0006 * (1f + 4f * build)) ph3 = 1f;
                    ph3 *= 0.9992f; v += noise * ph3 * 0.7f;
                    if (t >= 3.2f) v += (noise * Mathf.Exp(-after * 18f) + Mathf.Sin(t * 6.2832f * 60f) * Mathf.Exp(-after * 2f)) * 1.2f;
                    break;
                }
                case 3: // power: a rising rumble and surge, then a huge boom with a long tail
                {
                    ph1 += (40f + 70f * build) / Rate;
                    v += (Mathf.Sin(ph1 * 6.2832f) * 0.8f + lp * 3f) * build * build * (t < 3.2f ? 1f : 0f);
                    ph2 += (30f * Mathf.Exp(-after * 0.7f)) / Rate;
                    if (t >= 3.2f) v += Mathf.Sin(ph2 * 6.2832f) * 2.2f * Mathf.Exp(-after * 0.9f) + noise * 0.9f * Mathf.Exp(-after * 5f) + lp * 1.5f * Mathf.Exp(-after * 1.6f);
                    break;
                }
                case 4: // time: ticking that slows and reverses, a rising whir, a bell
                {
                    float rate = t < 2.6f ? 5f : 12f;
                    float tick = Mathf.Repeat(t * rate, 1f);
                    v += Mathf.Sin(t * 6.2832f * (t < 2.6f ? 900f : 1500f)) * Mathf.Exp(-tick * 40f) * 0.6f;
                    ph1 += (120f + 700f * build * build) / Rate;
                    v += Mathf.Sin(ph1 * 6.2832f) * 0.2f * build * (t < 3.2f ? 1f : 0f);
                    if (t >= 3.2f) v += (Mathf.Sin(t * 6.2832f * 660f) + 0.6f * Mathf.Sin(t * 6.2832f * 990f * 1.01f) + 0.4f * Mathf.Sin(t * 6.2832f * 1585f)) * 0.6f * Mathf.Exp(-after * 1.4f);
                    break;
                }
                default: // soul: a breathy choir that swells, whispers, a warm chord
                {
                    float vib = 1f + 0.012f * Mathf.Sin(t * 5.5f);
                    float choir = 0f;
                    float[] f = { 220f, 277f, 330f, 440f };
                    for (int k = 0; k < f.Length; k++) choir += Mathf.Sin(t * 6.2832f * f[k] * vib * (1f + 0.03f * build) + k);
                    v += choir * 0.2f * Mathf.Pow(build, 1.3f) * (t < 3.2f ? 1f : Mathf.Exp(-after * 0.6f) * 1.4f);
                    v += lp * 1.6f * Mathf.Sin(t * 3f) * Mathf.Sin(t * 3f) * build;
                    if (t >= 3.2f) v += Mathf.Sin(t * 6.2832f * 110f) * Mathf.Exp(-after * 1.5f);
                    break;
                }
            }
            data[i] = v;
        }
        return Finish("stone_" + stone, data, 0.85f);
    }

    /// <summary>The Infinity Cubelette: six notes climb (one per stone), the snap at 3.4 s, a golden chime at 5.0 s.</summary>
    private static AudioClip Cubelette()
    {
        float length = 8f;
        int n = (int)(Rate * length);
        float[] data = new float[n];
        System.Random rng = new System.Random(9200);
        float[] notes = { 262f, 330f, 392f, 494f, 587f, 784f };
        float lp = 0f, ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = Noise(rng);
            lp += (noise - lp) * 0.04f;
            float v = 0f;
            for (int k = 0; k < 6; k++)
            {
                float on = k * 0.5f;
                if (t < on) continue;
                float held = Mathf.Exp(-(t - on) * 0.35f) * Mathf.Clamp01((t - on) / 0.05f) * (t < 3.4f ? 1f : Mathf.Exp(-(t - 3.4f) * 2.5f));
                v += Mathf.Sin(t * 6.2832f * notes[k]) * 0.22f * held;
            }
            v += lp * 1.4f * Mathf.Pow(Mathf.Clamp01(t / 3.4f), 2f) * (t < 3.4f ? 1f : 0f);
            if (t >= 3.4f)
            {
                float a = t - 3.4f;
                ph += (50f * Mathf.Exp(-a * 1.3f)) / Rate;
                v += Mathf.Sin(ph * 6.2832f) * 2f * Mathf.Exp(-a * 1.3f) + noise * Mathf.Exp(-a * 14f) * 0.9f;
                for (int k = 0; k < 6; k++) v += Mathf.Sin(t * 6.2832f * notes[k] * 2f) * 0.12f * Mathf.Exp(-a * 0.9f);
            }
            if (t >= 5.0f) v += (Mathf.Sin(t * 6.2832f * 1568f) + 0.5f * Mathf.Sin(t * 6.2832f * 2349f)) * 0.3f * Mathf.Exp(-(t - 5f) * 1.6f);
            data[i] = v;
        }
        return Finish("stone_cubelette", data, 0.85f);
    }
}
