// ---- System notes ----
// PixelMeteorSynth.cs
//   Code-made sounds of the Meteor minigame (PixelAudio.SynthFor -> PixelMeteorSynth.For; put your own clips in the Sounds list to replace them):
//   meteor_spawn (a fiery whoosh with a deep boom as it enters), meteor_rumble (a seamless low rumble loop while it crosses, PixelAudio.StartLoop / StopLoop),
//   meteor_hit (a dull pick-on-rock thud with gravel, every click) and meteor_click (a bright pick clink with chips, when the click gives a chunk).
// ----------------------
using UnityEngine;

public static class PixelMeteorSynth
{
    private const int Rate = 22050;

    public static AudioClip[] For(string id)
    {
        switch (id)
        {
            case "meteor_spawn": return new[] { Spawn() };
            case "meteor_rumble": return new[] { Rumble() };
            case "meteor_hit": return new[] { Hit(1), Hit(2), Hit(3), Hit(4) };
            case "meteor_click": return new[] { Clink(1), Clink(2), Clink(3) };
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

    /// <summary>It enters: noise through a rising then closing low-pass (the rush), a falling sub boom and a little crackle of burning rock.</summary>
    private static AudioClip Spawn()
    {
        int n = (int)(Rate * 1.1f);
        float[] data = new float[n];
        System.Random rng = new System.Random(4100);
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n, sec = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * Mathf.Lerp(0.03f, 0.3f, Mathf.Sin(Mathf.Clamp01(t * 1.2f) * Mathf.PI * 0.5f) * (1f - t * 0.6f));
            float rush = lp * Mathf.Sin(t * Mathf.PI) * 1.6f;
            phase += (62f * Mathf.Exp(-sec * 1.6f) + 28f) / Rate;
            float boom = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-sec * 3.2f) * 0.9f * Mathf.Clamp01(sec / 0.01f);
            float crackle = rng.NextDouble() < 0.0035 ? ((float)rng.NextDouble() * 2f - 1f) * 0.8f : 0f;
            data[i] = rush + boom + crackle * (1f - t);
        }
        return Finish("Meteor Spawn", data, 0.85f);
    }

    /// <summary>A seamless loop (its end is crossfaded into its start): deep noise and two low hums.</summary>
    private static AudioClip Rumble()
    {
        int n = (int)(Rate * 2f), fade = (int)(Rate * 0.25f);
        float[] buf = new float[n + fade];
        System.Random rng = new System.Random(4200);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float sec = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.035f;
            lp2 += (lp - lp2) * 0.2f;
            float swell = 0.75f + 0.25f * Mathf.Sin(sec * Mathf.PI * 2f * 1.5f);   // (3 cycles in the 2 s)
            buf[i] = (lp2 * 5f + Mathf.Sin(sec * 40f * Mathf.PI * 2f) * 0.35f + Mathf.Sin(sec * 55f * Mathf.PI * 2f) * 0.25f) * swell;
        }
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float v = buf[i + fade];
            if (i < fade) { float w = i / (float)fade; v = buf[i + fade] * w + buf[i] * (1f - w); }
            data[i] = v;
        }
        return Finish("Meteor Rumble", data, 0.6f);
    }

    /// <summary>A pick striking big rock: a low thud that falls in pitch, a dull crack and a few pieces of gravel.</summary>
    private static AudioClip Hit(int seed)
    {
        int n = (int)(Rate * 0.32f);
        float[] data = new float[n];
        System.Random rng = new System.Random(4300 + seed * 31);
        float phase = 0f, lp = 0f;
        float[] gravel = { 0.07f + seed * 0.004f, 0.12f, 0.17f + seed * 0.006f };
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)Rate;
            phase += (150f * Mathf.Exp(-sec * 22f) + 55f + seed * 6f) / Rate;
            float thud = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-sec * 14f);
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.28f;
            float crack = lp * Mathf.Exp(-sec * 48f) * 1.3f;
            float grit = 0f;
            for (int k = 0; k < gravel.Length; k++)
            {
                float d = sec - gravel[k];
                if (d > 0f) grit += noise * Mathf.Exp(-d * 120f) * 0.35f;
            }
            data[i] = (thud + crack + grit) * Mathf.Clamp01(sec / 0.0012f);
        }
        return Finish("Meteor Hit " + seed, data, 0.9f);
    }

    /// <summary>A chunk breaks off: a bright metallic clink (two inharmonic partials), a snap and bits of rock pinging away.</summary>
    private static AudioClip Clink(int seed)
    {
        int n = (int)(Rate * 0.4f);
        float[] data = new float[n];
        System.Random rng = new System.Random(4400 + seed * 53);
        float hz = 1700f + seed * 330f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float snap = noise * Mathf.Exp(-sec * 150f);
            float clink = (Mathf.Sin(sec * hz * Mathf.PI * 2f) + 0.6f * Mathf.Sin(sec * hz * 2.76f * Mathf.PI * 2f) + 0.3f * Mathf.Sin(sec * hz * 5.4f * Mathf.PI * 2f)) * Mathf.Exp(-sec * 16f);
            float bits = 0f;
            for (int k = 1; k <= 3; k++)
            {
                float d = sec - (0.06f + k * 0.055f + seed * 0.004f);
                if (d > 0f) bits += Mathf.Sin(d * (2300f + k * 420f) * Mathf.PI * 2f) * Mathf.Exp(-d * 60f) * 0.22f;
            }
            data[i] = (snap * 0.8f + clink * 0.45f + bits) * Mathf.Clamp01(sec / 0.0008f);
        }
        return Finish("Meteor Clink " + seed, data, 0.75f);
    }
}
