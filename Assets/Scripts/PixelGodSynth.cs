// ---- System notes ----
// PixelGodSynth.cs
//   Code-made sounds of the three Egyptian God Cube find shows (PixelAudio.SynthFor -> PixelGodSynth.For; put your own clips in the Sounds list to replace them). Each clip is one long piece that is
//   timed to its show in PixelGodFind (start of the show = start of the clip): god_slifer (storm rumble, three thunder cracks at 0.5 / 1.0 / 1.4 s, the dragon's roar from 1.5 s, a final blast at 4.9 s),
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
        }
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
}
