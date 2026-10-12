using System;
using UnityEngine;

/// <summary>
/// Code-made sounds for Fishing's special blocks (the message in a bottle, the treasure map and the whale). <see cref="For"/> returns the clips of a sound id
/// (null = not one of ours); PixelAudio uses them while a sound's clip list is empty, so your own clips in the Inspector replace them.
/// </summary>
public static class PixelOceanSynth
{
    /// <summary>The sound ids this class makes (PixelAudio adds each to its list).</summary>
    public static readonly string[] Ids =
    {
        "bottle_break", "map_unroll", "map_reveal", "dig_x", "dig_done", "whale_hook", "whale_yank", "whale_caught", "whale_escape",
    };

    public static AudioClip[] For(string id)
    {
        switch (id)
        {
            case "pixel_bottle": return new[] { Glass(1), Glass(2), Glass(3) };
            case "bottle_break": return new[] { BottleBreak(1), BottleBreak(2) };
            case "map_unroll": return new[] { Unroll() };
            case "map_reveal": return new[] { Reveal() };
            case "dig_x": return new[] { Dig(1), Dig(2), Dig(3), Dig(4) };
            case "dig_done": return new[] { DigDone() };
            case "pixel_whale": return new[] { Groan(1, 0.4f), Groan(2, 0.4f), Groan(3, 0.4f) };
            case "whale_hook": return new[] { Song(1.5f, 1f) };
            case "whale_yank": return new[] { Yank(1), Yank(2), Yank(3) };
            case "whale_caught": return new[] { Caught() };
            case "whale_escape": return new[] { Escape() };
        }
        return null;
    }

    private const int Rate = 22050;

    private static AudioClip Finish(string name, float[] data, float peakLevel = 0.8f)
    {
        float peak = 0.0001f;
        for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * peakLevel;
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Tone(float t, float hz) => Mathf.Sin(t * hz * Mathf.PI * 2f);

    /// <summary>A bright glass clink.</summary>
    private static AudioClip Glass(int seed)
    {
        int n = (int)(Rate * 0.32f);
        float[] d = new float[n];
        System.Random rng = new System.Random(7100 + seed * 31);
        float f = 2300f + seed * 420f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            d[i] = (Tone(t, f) * Mathf.Exp(-t * 22f) + Tone(t, f * 1.58f) * Mathf.Exp(-t * 30f) * 0.6f + Tone(t, f * 2.4f) * Mathf.Exp(-t * 45f) * 0.35f + noise * Mathf.Exp(-t * 160f) * 0.5f) * Mathf.Clamp01(t / 0.001f);
        }
        return Finish("Glass " + seed, d);
    }

    /// <summary>A bottle bursting: a crash of glass, a rain of little shards and a cork popping.</summary>
    private static AudioClip BottleBreak(int seed)
    {
        int n = (int)(Rate * 1.0f);
        float[] d = new float[n];
        System.Random rng = new System.Random(7200 + seed * 53);
        float lp = 0f;
        // Shards tinkling down after the crash.
        int shards = 26;
        float[] at = new float[shards], hz = new float[shards];
        for (int s = 0; s < shards; s++) { at[s] = 0.03f + (float)rng.NextDouble() * 0.7f; hz[s] = 2000f + (float)rng.NextDouble() * 3600f; }
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.5f;
            float v = (noise - lp) * Mathf.Exp(-t * 14f) * 0.9f;                                   // the crash (high-passed noise)
            for (int s = 0; s < shards; s++)
            {
                float u = t - at[s];
                if (u > 0f && u < 0.12f) v += Tone(u, hz[s]) * Mathf.Exp(-u * 55f) * 0.3f;
            }
            float c = t - 0.11f;                                                                     // the cork pops out
            if (c > 0f && c < 0.2f) v += Tone(c, 520f * Mathf.Exp(-c * 9f) + 120f) * Mathf.Exp(-c * 28f) * 0.9f;
            d[i] = v * Mathf.Clamp01(t / 0.002f);
        }
        return Finish("Bottle break " + seed, d);
    }

    /// <summary>Old paper unrolling: a long dry crinkle that gets busier, with a soft swoosh underneath.</summary>
    private static AudioClip Unroll()
    {
        int n = (int)(Rate * 1.5f);
        float[] d = new float[n];
        System.Random rng = new System.Random(7300);
        float lp = 0f, lp2 = 0f, crackle = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float k = t / 1.5f;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.4f;
            lp2 += (noise - lp2) * 0.04f;
            if (rng.NextDouble() < 0.0025 + 0.01 * Mathf.Sin(k * Mathf.PI)) crackle = (float)rng.NextDouble() * 1.4f;   // sudden little creases
            crackle *= 0.985f;
            float env = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
            d[i] = ((noise - lp) * 0.25f + crackle * (noise - lp) + lp2 * 1.2f) * env;
        }
        return Finish("Map unroll", d, 0.7f);
    }

    /// <summary>The map shows its secret: a soft rising chime.</summary>
    private static AudioClip Reveal()
    {
        int n = (int)(Rate * 1.1f);
        float[] d = new float[n];
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float v = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float u = t - k * 0.11f;
                if (u < 0f) continue;
                v += (Tone(u, notes[k]) + Tone(u, notes[k] * 2.01f) * 0.3f) * Mathf.Exp(-u * 4.5f) * 0.5f;
            }
            d[i] = v * Mathf.Clamp01(t / 0.004f);
        }
        return Finish("Map reveal", d, 0.7f);
    }

    /// <summary>A shovel biting into dirt: a scrape, a heavy thud and a few falling grains.</summary>
    private static AudioClip Dig(int seed)
    {
        int n = (int)(Rate * 0.42f);
        float[] d = new float[n];
        System.Random rng = new System.Random(7400 + seed * 61);
        float lp = 0f, lp2 = 0f;
        float thudHz = 62f + seed * 9f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.22f;
            lp2 += (lp - lp2) * 0.3f;
            float scrape = lp * Mathf.Exp(-t * 16f) * Mathf.Clamp01(t / 0.015f) * 0.9f;                 // the blade scraping in
            float thud = Tone(t - 0.05f, thudHz) * Mathf.Exp(-(t - 0.05f) * 16f) * (t > 0.05f ? 1.1f : 0f);   // the dirt landing
            float grains = (rng.NextDouble() < 0.02 && t > 0.1f ? noise : 0f) * Mathf.Exp(-t * 6f);
            d[i] = scrape + thud + lp2 * Mathf.Exp(-t * 22f) * 0.5f + grains * 0.5f;
        }
        return Finish("Dig " + seed, d);
    }

    /// <summary>The treasure is found: a wooden creak, a pile of coins and a bright chime.</summary>
    private static AudioClip DigDone()
    {
        int n = (int)(Rate * 1.5f);
        float[] d = new float[n];
        System.Random rng = new System.Random(7500);
        float phase = 0f;
        int coins = 30;
        float[] at = new float[coins], hz = new float[coins];
        for (int c = 0; c < coins; c++) { at[c] = 0.28f + (float)rng.NextDouble() * 0.7f; hz[c] = 1900f + (float)rng.NextDouble() * 2400f; }
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            // The lid creaks open (a wobbling saw tone).
            float creakHz = 150f + 70f * Mathf.Sin(t * 14f) + t * 120f;
            phase += creakHz / Rate;
            float creak = ((phase % 1f) * 2f - 1f) * Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI) * 0.35f;
            float v = creak;
            for (int c = 0; c < coins; c++)
            {
                float u = t - at[c];
                if (u > 0f && u < 0.2f) v += (Tone(u, hz[c]) + Tone(u, hz[c] * 1.5f) * 0.4f) * Mathf.Exp(-u * 24f) * 0.22f;
            }
            float ch = t - 0.4f;
            if (ch > 0f) v += (Tone(ch, 1318.5f) + Tone(ch, 1975.5f) * 0.5f) * Mathf.Exp(-ch * 3.5f) * 0.4f;   // the glittering chime
            v += noise * Mathf.Exp(-Mathf.Abs(t - 0.28f) * 40f) * 0.15f;
            d[i] = v * Mathf.Clamp01(t / 0.004f);
        }
        return Finish("Dig done", d, 0.75f);
    }

    /// <summary>A short low whale groan (a click on the whale block).</summary>
    private static AudioClip Groan(int seed, float length)
    {
        int n = (int)(Rate * length);
        float[] d = new float[n];
        float phase = 0f;
        float baseHz = 82f + seed * 10f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float k = t / length;
            float hz = baseHz * (1f + 0.35f * Mathf.Sin(k * Mathf.PI) - 0.25f * k);
            phase += hz / Rate;
            float env = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.9f + 0.1f;
            d[i] = (Mathf.Sin(phase * Mathf.PI * 2f) + Mathf.Sin(phase * Mathf.PI * 4f) * 0.45f + Mathf.Sin(phase * Mathf.PI * 6f) * 0.2f) * env * Mathf.Clamp01(t / 0.01f);
        }
        return Finish("Groan " + seed, d, 0.85f);
    }

    /// <summary>A long whale song: a slow rising and falling call with a ghostly echo.</summary>
    private static AudioClip Song(float length, float pitch)
    {
        int n = (int)(Rate * length);
        float[] d = new float[n];
        float phase = 0f, phase2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float k = t / length;
            float hz = (85f + 90f * Mathf.Sin(k * Mathf.PI * 1.4f) + 25f * Mathf.Sin(k * 22f)) * pitch;
            phase += hz / Rate;
            phase2 += hz * 1.5f / Rate;
            float env = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
            d[i] = (Mathf.Sin(phase * Mathf.PI * 2f) + Mathf.Sin(phase * Mathf.PI * 4f) * 0.4f + Mathf.Sin(phase2 * Mathf.PI * 2f) * 0.3f) * env;
        }
        return Finish("Whale song", d, 0.8f);
    }

    /// <summary>The whale yanks the line: a taut twang, a deep groan and a sloshing of water.</summary>
    private static AudioClip Yank(int seed)
    {
        int n = (int)(Rate * 0.7f);
        float[] d = new float[n];
        System.Random rng = new System.Random(7600 + seed * 41);
        float lp = 0f, phase = 0f;
        float stringHz = 330f + seed * 70f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.08f;
            float twang = (Tone(t, stringHz * (1f + 0.04f * Mathf.Exp(-t * 20f))) + Tone(t, stringHz * 2.01f) * 0.4f) * Mathf.Exp(-t * 11f) * 0.7f;
            float hz = (90f + seed * 8f) * (1f - 0.3f * t);
            phase += hz / Rate;
            float groan = (Mathf.Sin(phase * Mathf.PI * 2f) + Mathf.Sin(phase * Mathf.PI * 4f) * 0.4f) * Mathf.Sin(Mathf.Clamp01(t / 0.6f) * Mathf.PI) * 0.8f;
            float slosh = lp * 3f * Mathf.Exp(-t * 7f) * Mathf.Clamp01(t / 0.02f);
            d[i] = (twang + groan + slosh) * Mathf.Clamp01(t / 0.002f);
        }
        return Finish("Whale yank " + seed, d);
    }

    /// <summary>The whale is landed: a big splash, the whale's call and a three-note cheer.</summary>
    private static AudioClip Caught()
    {
        int n = (int)(Rate * 2.0f);
        float[] d = new float[n];
        System.Random rng = new System.Random(7700);
        float lp = 0f, phase = 0f;
        float[] notes = { 392f, 523.25f, 659.25f, 783.99f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.07f;
            float splash = lp * 4f * Mathf.Exp(-t * 4f) * Mathf.Clamp01(t / 0.01f);
            float k = t / 1.6f;
            float hz = 88f + 70f * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
            phase += hz / Rate;
            float call = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.8f;
            float v = splash * 0.6f + call;
            for (int q = 0; q < notes.Length; q++)
            {
                float u = t - 0.5f - q * 0.13f;
                if (u > 0f) v += (Tone(u, notes[q]) + Tone(u, notes[q] * 2f) * 0.25f) * Mathf.Exp(-u * 3.2f) * 0.35f;
            }
            d[i] = v;
        }
        return Finish("Whale caught", d, 0.85f);
    }

    /// <summary>The line snaps: a sharp crack, a falling twang and a sad little call.</summary>
    private static AudioClip Escape()
    {
        int n = (int)(Rate * 1.0f);
        float[] d = new float[n];
        System.Random rng = new System.Random(7800);
        float phase = 0f, phase2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float crack = noise * Mathf.Exp(-t * 90f);
            phase += (900f * Mathf.Exp(-t * 5f) + 120f) / Rate;
            float twang = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 6f) * 0.6f;
            phase2 += (130f - 40f * t) / Rate;
            float call = Mathf.Sin(phase2 * Mathf.PI * 2f) * Mathf.Sin(Mathf.Clamp01((t - 0.1f) / 0.8f) * Mathf.PI) * 0.5f * (t > 0.1f ? 1f : 0f);
            d[i] = crack + twang + call;
        }
        return Finish("Whale escape", d, 0.8f);
    }
}
