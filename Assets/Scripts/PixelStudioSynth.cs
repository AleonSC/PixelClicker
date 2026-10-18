// ---- System notes ----
// PixelStudioSynth.cs
//   Code-made sounds of the Galactic Arcade studio intro (PixelAudio.SynthFor -> PixelStudioSynth.For; put your own clips in the Sounds list to replace them):
//   studio_whoosh (the fast pull-back from Earth: rising filtered noise and a falling tone), studio_gameover (an 8-bit square-wave 'game over' jingle:
//   four falling notes and a low buzz) and studio_hum (a soft seamless arcade-cabinet hum loop, PixelAudio.StartLoop / StopLoop).
// ----------------------
using UnityEngine;

public static class PixelStudioSynth
{
    private const int Rate = 22050;

    public static AudioClip[] For(string id)
    {
        switch (id)
        {
            case "studio_whoosh": return new[] { Whoosh() };
            case "studio_gameover": return new[] { GameOver() };
            case "studio_hum": return new[] { Hum() };
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

    /// <summary>The pull-back: noise through a low-pass that opens up then closes (the rush) over a tone that falls away.</summary>
    private static AudioClip Whoosh()
    {
        int n = (int)(Rate * 1.8f);
        float[] data = new float[n];
        System.Random rng = new System.Random(7100);
        float lp = 0f, lp2 = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float open = Mathf.Sin(Mathf.Clamp01(t * 1.15f) * Mathf.PI);   // (opens towards the middle, closes at the end)
            lp += (noise - lp) * Mathf.Lerp(0.02f, 0.35f, open);
            lp2 += (lp - lp2) * 0.5f;
            phase += Mathf.Lerp(420f, 60f, t) / Rate;
            float tone = Mathf.Sin(phase * Mathf.PI * 2f) * 0.25f * (1f - t);
            float env = Mathf.Clamp01(t / 0.08f) * (1f - Mathf.Pow(t, 3f));
            data[i] = (lp2 * 2.2f + tone) * env;
        }
        return Finish("Studio Whoosh", data, 0.8f);
    }

    /// <summary>The 8-bit game over: four falling square-wave notes (G, F#, F, E) and a long low buzz.</summary>
    private static AudioClip GameOver()
    {
        float[] notes = { 392f, 370f, 349.2f, 329.6f, 110f };
        float[] lengths = { 0.16f, 0.16f, 0.16f, 0.22f, 0.65f };
        int total = 0;
        foreach (float l in lengths) total += (int)(Rate * l);
        float[] data = new float[total];
        int at = 0;
        for (int k = 0; k < notes.Length; k++)
        {
            int len = (int)(Rate * lengths[k]);
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float f = notes[k] * (k == notes.Length - 1 ? 1f + 0.02f * Mathf.Sin(t * 40f) : 1f);   // (the last buzz wobbles)
                phase += f / Rate;
                float square = Frac(phase) < (k == notes.Length - 1 ? 0.25f : 0.5f) ? 1f : -1f;
                float env = Mathf.Clamp01(i / (Rate * 0.004f)) * (k == notes.Length - 1 ? 1f - t : 1f - t * 0.35f);
                data[at + i] = square * env * 0.6f;
            }
            at += len;
        }
        return Finish("Studio Game Over", data, 0.7f);
    }

    /// <summary>A seamless loop (its end is crossfaded into its start): a soft mains hum with a faint electric buzz.</summary>
    private static AudioClip Hum()
    {
        int n = (int)(Rate * 2f), fade = (int)(Rate * 0.25f);
        float[] buf = new float[n + fade];
        System.Random rng = new System.Random(7300);
        float lp = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float sec = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.02f;
            buf[i] = Mathf.Sin(sec * 60f * Mathf.PI * 2f) * 0.5f + Mathf.Sin(sec * 120f * Mathf.PI * 2f) * 0.2f + Mathf.Sin(sec * 180f * Mathf.PI * 2f) * 0.06f + lp * 1.5f;
        }
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float v = buf[i + fade];
            if (i < fade) { float k = i / (float)fade; v = buf[i + fade] * k + buf[i + n] * (1f - k); }
            data[i] = v;
        }
        return Finish("Studio Hum", data, 0.35f);
    }

    private static float Frac(float f) => f - Mathf.Floor(f);
}
