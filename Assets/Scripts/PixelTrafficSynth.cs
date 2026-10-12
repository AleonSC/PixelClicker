using System;
using UnityEngine;

/// <summary>Code-made sound effects for the Red Light Green Light minigame (used while the sounds' clip lists in PixelAudio are empty).</summary>
public static class PixelTrafficSynth
{
    private const int Rate = 22050;

    private static AudioClip Build(string name, float seconds, Func<float, float> sample)
    {
        int n = Mathf.Max(1, (int)(Rate * seconds));
        float[] data = new float[n];
        float peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            data[i] = sample(i / (float)Rate);
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.7f;
        AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A bell-like note starting at 'start' seconds: the pitch plus a soft octave, fading with 'decay'.</summary>
    private static float Note(float sec, float start, float hz, float decay)
    {
        float t = sec - start;
        if (t < 0f) return 0f;
        float tone = Mathf.Sin(t * hz * Mathf.PI * 2f) + 0.35f * Mathf.Sin(t * hz * 2f * Mathf.PI * 2f);
        return tone * Mathf.Exp(-t * decay) * Mathf.Clamp01(t / 0.004f);
    }

    /// <summary>A flat beep (square-ish) with a short fade at both ends.</summary>
    private static float Beep(float sec, float start, float length, float hz)
    {
        float t = sec - start;
        if (t < 0f || t > length) return 0f;
        float s = Mathf.Sin(t * hz * Mathf.PI * 2f);
        s = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.5f);
        return s * Mathf.Clamp01(t / 0.004f) * Mathf.Clamp01((length - t) / 0.01f);
    }

    /// <summary>Round starts: three quick rising notes.</summary>
    public static AudioClip Start() => Build("Traffic Start", 0.5f, s => Note(s, 0f, 392f, 9f) + Note(s, 0.11f, 523f, 9f) + Note(s, 0.22f, 659f, 7f));

    /// <summary>Green light: a bright two-note 'go'.</summary>
    public static AudioClip Green() => Build("Traffic Green", 0.35f, s => Note(s, 0f, 659f, 10f) + Note(s, 0.08f, 988f, 8f));

    /// <summary>Yellow light: two short warning beeps.</summary>
    public static AudioClip Yellow() => Build("Traffic Yellow", 0.4f, s => Beep(s, 0f, 0.09f, 740f) + Beep(s, 0.15f, 0.09f, 740f));

    /// <summary>Red light: a low buzzer that sags in pitch.</summary>
    public static AudioClip Red()
    {
        return Build("Traffic Red", 0.55f, s =>
        {
            float hz = 120f - s * 40f;
            float saw = (Mathf.Repeat(s * hz, 1f) * 2f - 1f);
            float body = saw * 0.7f + Mathf.Sin(s * hz * Mathf.PI * 2f) * 0.4f;
            return body * Mathf.Exp(-s * 3.2f) * Mathf.Clamp01(s / 0.01f);
        });
    }

    /// <summary>The yellow-light beat: a short woody tick on the moment the ring closes.</summary>
    public static AudioClip Tick()
    {
        System.Random rng = new System.Random(77);
        return Build("Traffic Tick", 0.08f, s =>
        {
            float click = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-s * 160f) * 0.5f;
            return click + Mathf.Sin(s * 1320f * Mathf.PI * 2f) * Mathf.Exp(-s * 70f);
        });
    }

    /// <summary>A good click: a clean little ping.</summary>
    public static AudioClip Good(int variant) => Build("Traffic Good " + variant, 0.22f, s => Note(s, 0f, variant == 1 ? 880f : 988f, 14f) + Note(s, 0.02f, 1320f, 18f) * 0.6f);

    /// <summary>A mistake: a short low buzz-thud.</summary>
    public static AudioClip Bad()
    {
        return Build("Traffic Bad", 0.26f, s =>
        {
            float hz = 150f - s * 120f;
            float sq = Mathf.Sign(Mathf.Sin(s * hz * Mathf.PI * 2f));
            return (sq * 0.5f + Mathf.Sin(s * 70f * Mathf.PI * 2f) * 0.6f) * Mathf.Exp(-s * 14f) * Mathf.Clamp01(s / 0.003f);
        });
    }

    /// <summary>Round won: a rising arpeggio.</summary>
    public static AudioClip Win() => Build("Traffic Win", 0.9f, s => Note(s, 0f, 523f, 6f) + Note(s, 0.1f, 659f, 6f) + Note(s, 0.2f, 784f, 6f) + Note(s, 0.32f, 1047f, 4f));

    /// <summary>Round lost: three falling notes.</summary>
    public static AudioClip Lose() => Build("Traffic Lose", 0.9f, s => Note(s, 0f, 392f, 5f) + Note(s, 0.18f, 330f, 5f) + Note(s, 0.36f, 262f, 3.5f));
}
