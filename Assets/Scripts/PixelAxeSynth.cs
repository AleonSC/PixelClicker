// ---- System notes ----
// PixelAxeSynth.cs
//   Code-made sounds for the first person axe and the Great Oak's bird nests (hooked into PixelAudio.SynthFor like PixelOceanSynth): axe_swing (a whoosh of filtered noise that
//   sweeps up and fades), axe_chop (a wooden thock: a quick falling thump + a crack of noise, 3 variants), nest_fall (a soft rustle + a tiny chirp), nest_catch (two bright
//   rising bird tweets), tree_creak (a stick-slip trunk groan: the cue that a tree is nearly down). WoodChop(seed, pitch, splinters, ring) is the shared axe-into-wood
//   sound (blade 'tock' + damped woody body + thump + splinters) - the axe and the Wood / Bark / Heartwood block sounds use it (PixelSynth.Wood kinds 0 / 1 / 4).
//   All ids are registered in PixelAudio.CreateDefaultSounds.
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
            case "axe_chop": return new[] { WoodChop(1, 1f, 1f, 1f), WoodChop(2, 1f, 1f, 1f), WoodChop(3, 1f, 1f, 1f) };
            case "tree_creak": return new[] { Creak(1), Creak(2) };
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

    /// <summary>
    /// An axe biting into wood (used by the axe and the Wood / Bark / Heartwood blocks): a sharp bandpassed 'tock' of noise (the blade), a woody BODY of
    /// a few damped, slightly out-of-tune resonances (a log rings briefly, not like a bell), a low thump and a short crackle of SPLINTERS after it.
    /// 'pitch' scales the body (Heartwood is deeper), 'splinters' the crackle (Bark is dry and crackly), 'ring' how long the body rings (1 = a short knock).
    /// </summary>
    public static AudioClip WoodChop(int seed, float pitch, float splinters, float ring)
    {
        int n = (int)(Rate * (0.26f + 0.12f * Mathf.Max(0f, ring - 1f)));
        float[] data = new float[n];
        System.Random rng = new System.Random(5100 + seed * 53 + Mathf.RoundToInt(pitch * 100f) + Mathf.RoundToInt(splinters * 10f));
        float detune = 1f + (seed - 2) * 0.045f;
        float[] hz = { 185f, 410f, 680f, 1060f, 1620f };
        float[] amp = { 1f, 0.65f, 0.45f, 0.3f, 0.18f };
        float[] decay = { 20f, 28f, 38f, 52f, 70f };
        float lpA = 0f, lpB = 0f, crackLp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            // the blade: noise between two low-passes = a band around 1.5-3 kHz, gone in a few milliseconds
            lpA += (noise - lpA) * 0.55f;
            lpB += (noise - lpB) * 0.18f;
            float bite = (lpA - lpB) * Mathf.Exp(-t * 320f) * 1.6f;
            // the log's body: damped, inharmonic modes
            float body = 0f;
            for (int m = 0; m < hz.Length; m++)
                body += Mathf.Sin(t * hz[m] * pitch * detune * (1f + m * 0.013f * seed) * Mathf.PI * 2f) * amp[m] * Mathf.Exp(-t * decay[m] / Mathf.Max(0.3f, ring));
            float thump = Mathf.Sin(t * 82f * pitch * Mathf.PI * 2f) * Mathf.Exp(-t * 34f) * 0.7f;
            // splinters: sparse little cracks right after the hit
            float chance = splinters * 0.22f * Mathf.Exp(-t * 22f) * Mathf.Clamp01((t - 0.012f) / 0.01f);
            float click = rng.NextDouble() < chance ? noise : 0f;
            crackLp += (click - crackLp) * 0.7f;
            float crack = (click - crackLp) * 0.9f;
            data[i] = (bite + body * 0.55f + thump + crack) * Mathf.Clamp01(t / 0.0012f);
        }
        return Finish("Wood Chop " + seed, data, 0.9f);
    }

    /// <summary>
    /// A tree's trunk CREAKING (the last blocks of a tree, an audible 'nearly down' cue): a stick-slip groan - irregular little impulses whose rate drifts
    /// up, each ringing two wooden resonances - that swells and fades.
    /// </summary>
    private static AudioClip Creak(int seed)
    {
        int n = (int)(Rate * 0.85f);
        float[] data = new float[n];
        System.Random rng = new System.Random(6100 + seed * 31);
        float phase = 0f;
        float r1 = Mathf.Exp(-2f * Mathf.PI * 60f / Rate), r2 = Mathf.Exp(-2f * Mathf.PI * 110f / Rate);
        float w1 = 2f * Mathf.PI * (330f + seed * 25f) / Rate, w2 = 2f * Mathf.PI * (760f + seed * 40f) / Rate;
        float a1 = 0f, b1 = 0f, a2 = 0f, b2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float rate = Mathf.Lerp(38f, 95f, t) * (1f + 0.25f * Mathf.Sin(t * 9f + seed)) * (0.85f + 0.3f * (float)rng.NextDouble());
            phase += rate / Rate;
            float x = 0f;
            if (phase >= 1f) { phase -= 1f; x = 0.6f + 0.4f * (float)rng.NextDouble(); }
            float y1 = 2f * r1 * Mathf.Cos(w1) * a1 - r1 * r1 * b1 + x; b1 = a1; a1 = y1;
            float y2 = 2f * r2 * Mathf.Cos(w2) * a2 - r2 * r2 * b2 + x * 0.6f; b2 = a2; a2 = y2;
            float env = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            data[i] = (y1 + y2 * 0.5f) * env * env;
        }
        return Finish("Tree Creak " + seed, data, 0.55f);
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
