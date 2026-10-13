// ---- System notes ----
// PixelAxeSynth.cs
//   Code-made sounds for the first person axe and the Great Oak's bird nests (hooked into PixelAudio.SynthFor like PixelOceanSynth): axe_swing (a whoosh of filtered noise that
//   sweeps up and fades), axe_chop (a wooden thock: a quick falling thump + a crack of noise, 3 variants), nest_fall (a soft rustle + a tiny chirp), nest_catch (two bright
//   rising bird tweets). All ids are registered in PixelAudio.CreateDefaultSounds.
// ---------------------------------------------------
using UnityEngine;

public static class PixelAxeSynth
{
    private const int Rate = 22050;

    public static AudioClip[] For(string id)
    {
        switch (id)
        {
            case "axe_swing": return new[] { Swing(1), Swing(2) };
            case "axe_chop": return new[] { Chop(1), Chop(2), Chop(3) };
            case "nest_fall": return new[] { NestFall() };
            case "nest_catch": return new[] { NestCatch() };
        }
        return null;
    }

    private static AudioClip Finish(string name, float[] data, float level)
    {
        float peak = 0.0001f;
        for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * level;
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A whoosh: noise through a low-pass whose cutoff sweeps up and then closes, with a bell-shaped volume.</summary>
    private static AudioClip Swing(int seed)
    {
        int n = (int)(Rate * 0.3f);
        float[] data = new float[n];
        System.Random rng = new System.Random(900 + seed * 17);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float cutoff = Mathf.Lerp(0.04f, 0.35f, Mathf.Sin(t * Mathf.PI * 0.8f));
            lp += (noise - lp) * cutoff;
            float env = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            data[i] = lp * env * env;
        }
        return Finish("Axe Swing " + seed, data, 0.55f);
    }

    /// <summary>A wood chop: a thump that falls in pitch, a crack of noise and a short hollow ring.</summary>
    private static AudioClip Chop(int seed)
    {
        int n = (int)(Rate * 0.28f);
        float[] data = new float[n];
        System.Random rng = new System.Random(1300 + seed * 41);
        float baseHz = 150f + seed * 22f;
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)Rate;
            float hz = baseHz * Mathf.Exp(-sec * 9f) + 70f;
            phase += hz / Rate;
            float thump = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-sec * 16f);
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.3f;
            float crack = lp * Mathf.Exp(-sec * 55f) * 1.4f;
            float ring = Mathf.Sin(sec * (420f + seed * 60f) * Mathf.PI * 2f) * Mathf.Exp(-sec * 30f) * 0.25f;
            data[i] = (thump + crack + ring) * Mathf.Clamp01(sec / 0.0015f);
        }
        return Finish("Axe Chop " + seed, data, 0.9f);
    }

    /// <summary>Something soft dropping through leaves: a short rustle and one tiny chirp.</summary>
    private static AudioClip NestFall()
    {
        int n = (int)(Rate * 0.35f);
        float[] data = new float[n];
        System.Random rng = new System.Random(77);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.18f;
            float rustle = lp * Mathf.Exp(-sec * 9f) * (0.6f + 0.4f * Mathf.Sin(sec * 90f));
            float chirpT = sec - 0.12f;
            float chirp = chirpT > 0f ? Mathf.Sin(chirpT * (2600f + chirpT * 9000f) * Mathf.PI * 2f) * Mathf.Exp(-chirpT * 28f) * 0.25f : 0f;
            data[i] = rustle * 0.8f + chirp;
        }
        return Finish("Nest Fall", data, 0.6f);
    }

    /// <summary>Two bright bird tweets, the second higher.</summary>
    private static AudioClip NestCatch()
    {
        int n = (int)(Rate * 0.42f);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)Rate;
            float v = 0f;
            for (int k = 0; k < 2; k++)
            {
                float t = sec - k * 0.16f;
                if (t < 0f) continue;
                float hz = (k == 0 ? 2300f : 2900f) + t * 5200f;
                v += Mathf.Sin(t * hz * Mathf.PI * 2f) * Mathf.Exp(-t * 16f) * Mathf.Clamp01(t / 0.004f);
            }
            data[i] = v;
        }
        return Finish("Nest Catch", data, 0.7f);
    }
}
