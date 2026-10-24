// ---- System notes ----
// PixelAudioSynth.cs
//   Part of PixelSynth (static partial class, the code-made sound synthesizer): the block / gathering / device sounds (Bloop ... Dig). The rest is in PixelAudioSynthShows.cs; the system notes are in PixelAudio.cs.
// ---------------------------------------------------
using UnityEngine;

/// <summary>Tiny sound synthesizer: makes AudioClips in code for sounds that have no audio file yet.</summary>
public static partial class PixelSynth
{
    /// <summary>A small high bloop (a quick upward chirp with a soft overtone) for an old pixel vanishing. 'seed' picks the pitch.</summary>
    public static AudioClip Bloop(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.12f);
        float[] data = new float[n];
        float start = 1100f + seed * 140f, end = 2300f + seed * 220f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n, sec = i / (float)rate;
            float hz = Mathf.Lerp(start, end, 1f - Mathf.Pow(1f - t, 2.2f));
            phase += hz / rate;
            float env = Mathf.Clamp01(sec / 0.004f) * Mathf.Exp(-sec * 30f);
            data[i] = (Mathf.Sin(phase * Mathf.PI * 2f) + 0.25f * Mathf.Sin(phase * 2f * Mathf.PI * 2f)) * env * 0.6f;
        }
        AudioClip clip = AudioClip.Create("Bloop " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A short electric zap: a buzzing chirp falling in pitch under a burst of crackle, with a fast decay.</summary>
    public static AudioClip Zap(int seed)
    {
        const int rate = 44100;
        float length = 0.3f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        System.Random rng = new System.Random(seed * 7919);

        float phase = 0f, crackle = 0f;
        float startHz = 1100f + seed * 140f, endHz = 160f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float sec = i / (float)rate;
            float env = Mathf.Exp(-sec * 13f);

            // Buzz: a sawtooth chirp, a little ragged.
            float hz = Mathf.Lerp(startHz, endHz, Mathf.Pow(t, 0.45f)) * (1f + ((float)rng.NextDouble() - 0.5f) * 0.04f);
            phase += hz / rate;
            phase -= Mathf.Floor(phase);
            float saw = phase * 2f - 1f;

            // Crackle: random sparks that are held for a few samples.
            if (rng.NextDouble() < 0.035 * (1f - t)) crackle = ((float)rng.NextDouble() * 2f - 1f);
            crackle *= 0.9f;

            float noise = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-sec * 40f); // the initial snap
            data[i] = (saw * 0.35f + crackle * 0.9f + noise * 0.8f) * env;
        }

        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.85f;

        AudioClip clip = AudioClip.Create("Zap " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>One speech blip for dialogue boxes: a short voiced squeak (a few harmonics of a falling pitch under a soft envelope), about 70 ms. 'variant' picks the base pitch.</summary>
    public static AudioClip TalkBlip(int variant)
    {
        const int rate = 22050;
        const float length = 0.075f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        float baseHz = 190f + variant * 26f, phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate, t = i / (float)n;
            float hz = baseHz * (1.12f - 0.16f * t) * (1f + 0.018f * Mathf.Sin(sec * 70f));   // a little downward glide and wobble
            phase += hz / rate;
            phase -= Mathf.Floor(phase);
            float w = Mathf.Sin(phase * 6.2832f) + 0.5f * Mathf.Sin(phase * 12.566f) + 0.28f * Mathf.Sin(phase * 18.85f) + 0.12f * Mathf.Sin(phase * 25.13f);
            lp += (w - lp) * 0.35f;   // soften the buzz
            float env = Mathf.Clamp01(sec / 0.006f) * Mathf.Exp(-sec * 26f) * (1f - t * t);
            data[i] = lp * env;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.5f;
        AudioClip clip = AudioClip.Create("TalkBlip " + variant, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A charge-up: a rising buzz with growing crackle that ends in a big snap.</summary>
    /// <summary>A watery splash: a hiss of filtered noise plus a few quick rising bubble "bloops". 'seed' picks a variant.</summary>
    public static AudioClip Splash(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.32f);
        float[] data = new float[n];
        System.Random rng = new System.Random(500 + seed * 31);
        float lp = 0f;
        float[] bloopAt = { 0.015f, 0.06f + seed * 0.01f, 0.115f };
        float[] bloopHz = { 230f + seed * 30f, 310f + seed * 25f, 400f };
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.07f;
            float hiss = lp * 4f * Mathf.Exp(-sec * 11f);              // a soft, low wash instead of a bright hiss
            float bloops = 0f;
            for (int k = 0; k < bloopAt.Length; k++)
            {
                float t = sec - bloopAt[k];
                if (t < 0f) continue;
                float hz = bloopHz[k] * (1f + t * 6f);               // each bubble rises in pitch as it pops
                bloops += Mathf.Sin(t * hz * Mathf.PI * 2f) * Mathf.Exp(-t * 38f) * 0.45f;
            }
            data[i] = (hiss * 0.8f + bloops) * Mathf.Clamp01(sec / 0.003f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.7f;
        AudioClip clip = AudioClip.Create("Splash " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A short splashy click for a Water pixel: a quick droplet "plip" with a little splash hiss around it. 'seed' picks a variant.</summary>
    /// <summary>Farming sounds. kind: 0 soft hit, 1 dirt thud, 2 wet squelch, 3 airy puff, 4 dry rustle, 5 squeaky crunch, 6 golden chime, 9 gleam.</summary>
    public static AudioClip Farm(int kind, int seed)
    {
        const int rate = 22050;
        float len = kind == 6 ? 0.55f : kind == 9 ? 0.9f : 0.22f;
        int n = (int)(rate * len);
        float[] data = new float[n];
        System.Random rng = new System.Random(1300 + kind * 101 + seed * 17);
        float lp = 0f;
        float baseHz = 180f + seed * 25f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float v = 0f;
            switch (kind)
            {
                case 0: lp += (noise - lp) * 0.3f; v = lp * Mathf.Exp(-t * 40f) + Mathf.Sin(t * 260f * Mathf.PI * 2f) * Mathf.Exp(-t * 35f) * 0.5f; break;
                case 1: lp += (noise - lp) * 0.12f; v = lp * Mathf.Exp(-t * 22f) * 1.4f + Mathf.Sin(t * baseHz * 0.5f * Mathf.PI * 2f) * Mathf.Exp(-t * 28f); break;
                case 2: lp += (noise - lp) * 0.18f; v = lp * Mathf.Exp(-t * 14f) + Mathf.Sin(t * (140f + 90f * Mathf.Sin(t * 30f)) * Mathf.PI * 2f) * Mathf.Exp(-t * 16f) * 0.7f; break;
                case 3: lp += (noise - lp) * 0.5f; v = (noise - lp) * Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI) * 0.6f; break;
                case 4: v = (rng.NextDouble() < 0.12 ? noise : 0f) * Mathf.Exp(-t * 12f) + (noise - lp) * 0.2f * Mathf.Exp(-t * 18f); lp += (noise - lp) * 0.4f; break;
                case 5: v = Mathf.Sin(t * (1400f + seed * 200f) * (1f + t * 6f) * Mathf.PI * 2f) * Mathf.Exp(-t * 30f) * 0.5f + noise * Mathf.Exp(-t * 45f) * 0.5f; break;
                case 6:
                    {
                        float f = 880f + seed * 220f;
                        v = (Mathf.Sin(t * f * Mathf.PI * 2f) + 0.5f * Mathf.Sin(t * f * 2.01f * Mathf.PI * 2f) + 0.25f * Mathf.Sin(t * f * 3.02f * Mathf.PI * 2f)) * Mathf.Exp(-t * 7f) * 0.5f;
                        break;
                    }
                default:
                    {
                        float f1 = 1046f, f2 = 1568f, f3 = 2093f;
                        v = Mathf.Sin(t * f1 * Mathf.PI * 2f) * Mathf.Exp(-t * 4f) + (t > 0.12f ? Mathf.Sin((t - 0.12f) * f2 * Mathf.PI * 2f) * Mathf.Exp(-(t - 0.12f) * 4f) : 0f)
                            + (t > 0.24f ? Mathf.Sin((t - 0.24f) * f3 * Mathf.PI * 2f) * Mathf.Exp(-(t - 0.24f) * 4f) : 0f);
                        v *= 0.4f;
                        break;
                    }
            }
            data[i] = v * Mathf.Clamp01(t / 0.002f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("Farm " + kind + "-" + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A glassy, ringing tap of black volcanic glass; the game pitches it up with every click.</summary>
    public static AudioClip ObsidianPing(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.45f);
        float[] data = new float[n];
        System.Random rng = new System.Random(2100 + seed * 31);
        float f = 380f + seed * 40f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float ring = Mathf.Sin(t * f * Mathf.PI * 2f) * Mathf.Exp(-t * 7f)
                       + 0.55f * Mathf.Sin(t * f * 2.76f * Mathf.PI * 2f) * Mathf.Exp(-t * 11f)
                       + 0.3f * Mathf.Sin(t * f * 5.4f * Mathf.PI * 2f) * Mathf.Exp(-t * 18f);
            data[i] = (ring + noise * Mathf.Exp(-t * 90f) * 0.8f) * Mathf.Clamp01(t / 0.001f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("Obsidian " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// The Electrician pixels' own click sounds: 0 Wire (a metallic twang), 1 Battery (a low thunk with a hum), 2 Capacitor (a rising charge whine ending in a snap),
    /// 3 Resistor (a dull buzz), 4 Transistor (a two-tone digital blip), 5 Fuse (a hiss that ends in a pop), 6 Bulb (a glassy ting).
    /// </summary>
    public static AudioClip Elec(int kind, int seed)
    {
        const int rate = 22050;
        float[] lengths = { 0.28f, 0.38f, 0.5f, 0.3f, 0.2f, 0.5f, 0.55f };
        int n = (int)(rate * lengths[Mathf.Clamp(kind, 0, 6)]);
        float[] data = new float[n];
        System.Random rng = new System.Random(3300 + kind * 131 + seed * 19);
        float detune = 1f + 0.06f * (seed - 2);
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float v = 0f;
            switch (kind)
            {
                case 0:   // Wire: frequency-modulated twang that dips
                {
                    float f = 880f * detune;
                    v = Mathf.Sin(t * f * Mathf.PI * 2f + 4f * Mathf.Sin(t * f * 1.5f * Mathf.PI * 2f) * Mathf.Exp(-t * 18f)) * Mathf.Exp(-t * 15f);
                    v += noise * Mathf.Exp(-t * 90f) * 0.3f;
                    break;
                }
                case 1:   // Battery: a deep thunk and a settling hum
                {
                    float f = (120f - 50f * Mathf.Min(1f, t / 0.12f)) * detune;
                    phase += f / rate;
                    v = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 9f) + 0.35f * Mathf.Sin(t * 55f * Mathf.PI * 2f) * Mathf.Exp(-t * 5f);
                    v += noise * Mathf.Exp(-t * 110f) * 0.4f;
                    break;
                }
                case 2:   // Capacitor: charge sweep up, then a snap
                {
                    float charge = 0.32f;
                    if (t < charge)
                    {
                        float k = t / charge;
                        phase += (350f + 2300f * k * k) * detune / rate;
                        v = Mathf.Sin(phase * Mathf.PI * 2f) * (0.2f + 0.6f * k);
                    }
                    else
                    {
                        float d = t - charge;
                        lp += (noise - lp) * 0.5f;
                        v = lp * Mathf.Exp(-d * 35f) * 1.3f + Mathf.Sin(d * 170f * Mathf.PI * 2f) * Mathf.Exp(-d * 30f);
                    }
                    break;
                }
                case 3:   // Resistor: a rough 100 Hz buzz
                {
                    float saw = 2f * Mathf.Repeat(t * 105f * detune, 1f) - 1f;
                    lp += (noise - lp) * 0.25f;
                    v = saw * Mathf.Exp(-t * 11f) * 0.55f + lp * Mathf.Exp(-t * 16f) * 0.6f;
                    break;
                }
                case 4:   // Transistor: two quick square blips
                {
                    float f = (t < 0.085f ? 1250f : 1870f) * detune;
                    float local = t < 0.085f ? t : t - 0.085f;
                    float sq = Mathf.Sin(t * f * Mathf.PI * 2f) >= 0f ? 1f : -1f;
                    v = sq * 0.45f * Mathf.Exp(-local * 22f);
                    break;
                }
                case 5:   // Fuse: a hiss that thins out, then a pop
                {
                    float pop = 0.36f;
                    if (t < pop)
                    {
                        lp += (noise - lp) * 0.8f;
                        v = (noise - lp) * (0.25f + 0.2f * Mathf.Sin(t * 60f)) * (1f - t / pop * 0.5f);   // high-passed noise
                    }
                    else
                    {
                        float d = t - pop;
                        v = Mathf.Sin(d * 190f * detune * Mathf.PI * 2f) * Mathf.Exp(-d * 45f) + noise * Mathf.Exp(-d * 70f) * 0.7f;
                    }
                    break;
                }
                default:  // Bulb: a glassy ting
                {
                    float f = 2200f * detune;
                    v = (Mathf.Sin(t * f * Mathf.PI * 2f) + 0.5f * Mathf.Sin(t * f * 1.5f * Mathf.PI * 2f) + 0.3f * Mathf.Sin(t * f * 2.3f * Mathf.PI * 2f)) * Mathf.Exp(-t * 8f);
                    v += noise * Mathf.Exp(-t * 120f) * 0.35f;
                    break;
                }
            }
            data[i] = v * Mathf.Clamp01(t / 0.002f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("Elec " + kind + "-" + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// A deep, smooth water sound for a Water pixel: one to three low bubbles that climb a little in pitch, over a soft, heavily filtered wash.
    /// Every 'seed' rolls its own pitch, bubble count, timing and length, so a pool of them never sounds the same twice.
    /// </summary>
    public static AudioClip WaterClick(int seed)
    {
        const int rate = 22050;
        System.Random rng = new System.Random(900 + seed * 37);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float length = R(0.28f, 0.42f);
        int n = (int)(rate * length);
        float[] data = new float[n];

        int bubbles = 1 + rng.Next(0, 3);
        float[] at = new float[bubbles], hz = new float[bubbles], amp = new float[bubbles], rise = new float[bubbles], decay = new float[bubbles];
        float t0 = 0f;
        for (int k = 0; k < bubbles; k++)
        {
            at[k] = t0;
            hz[k] = R(130f, 330f) * (k == 0 ? 1f : R(0.9f, 1.5f));
            amp[k] = k == 0 ? 1f : R(0.35f, 0.7f);
            rise[k] = R(2f, 6f);
            decay[k] = R(8f, 15f);
            t0 += R(0.05f, 0.11f);
        }
        float washDecay = R(6f, 11f), washAmount = R(0.2f, 0.45f);
        float lp1 = 0f, lp2 = 0f, lp3 = 0f, smooth = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp1 += (noise - lp1) * 0.05f;
            lp2 += (lp1 - lp2) * 0.1f;
            lp3 += (lp2 - lp3) * 0.2f;
            float wash = lp3 * 7f * Mathf.Exp(-sec * washDecay) * Mathf.Clamp01(sec / 0.02f) * washAmount;   // a soft, low rumble of moving water
            float body = 0f;
            for (int k = 0; k < bubbles; k++)
            {
                float t = sec - at[k];
                if (t < 0f) continue;
                float phase = hz[k] * (t + rise[k] * t * t * 0.5f) * Mathf.PI * 2f;                 // pitch climbs as the bubble rises
                float env = Mathf.Exp(-t * decay[k]) * Mathf.Clamp01(t / 0.008f);
                body += (Mathf.Sin(phase) + 0.18f * Mathf.Sin(phase * 2f)) * env * amp[k];
            }
            float raw = wash + body * 0.7f;
            smooth += (raw - smooth) * 0.55f;   // one more gentle low-pass: no sharp edges
            data[i] = smooth;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("Water Click " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A small crunch, like a seed being pressed into gravelly soil: a few quick crackles over a soft low squash. 'seed' picks a variant.</summary>
    public static AudioClip Crunch(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.2f);
        float[] data = new float[n];
        System.Random rng = new System.Random(300 + seed * 23);
        // A handful of crackle bursts at slightly different times: the "crunch".
        float[] at = { 0.004f, 0.03f + seed * 0.004f, 0.065f, 0.1f + seed * 0.003f };
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.55f;
            float env = 0f;
            for (int k = 0; k < at.Length; k++)
                if (sec >= at[k]) env += Mathf.Exp(-(sec - at[k]) * 70f) * (0.9f - k * 0.15f);
            float squash = Mathf.Sin(sec * (95f - seed * 8f) * Mathf.PI * 2f) * Mathf.Exp(-sec * 30f) * 0.5f;
            data[i] = (lp * env + squash) * Mathf.Clamp01(sec / 0.002f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.7f;
        AudioClip clip = AudioClip.Create("Crunch " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A husk crushed by hand (Husk Crush): a sharp crack, a short low thump as it gives way, then a few chunks skittering apart. 'seed' picks a variant.</summary>
    public static AudioClip HuskCrush(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.32f);
        float[] data = new float[n];
        System.Random rng = new System.Random(700 + seed * 37);
        // The chunks: small clicks scattered after the break, quieter and further apart as they fly off.
        float[] chunkAt = new float[5];
        for (int k = 0; k < chunkAt.Length; k++) chunkAt[k] = 0.05f + k * 0.035f + (float)rng.NextDouble() * 0.025f + seed * 0.002f;
        float lp = 0f, hp = 0f, prev = 0f;
        float thumpHz = 82f - seed * 6f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.35f;              // darker noise for the body of the crunch
            hp = noise - prev; prev = noise;          // brighter noise for the crack and the chunks
            float crack = hp * Mathf.Exp(-sec * 90f) * 0.9f;
            float crunch = lp * Mathf.Exp(-sec * 22f) * (0.6f + 0.4f * Mathf.Sin(sec * 260f + seed)) * 0.8f;
            float thump = Mathf.Sin(sec * thumpHz * (1f - sec * 1.2f) * Mathf.PI * 2f) * Mathf.Exp(-sec * 28f) * 0.7f;
            float chunks = 0f;
            for (int k = 0; k < chunkAt.Length; k++)
                if (sec >= chunkAt[k]) chunks += hp * Mathf.Exp(-(sec - chunkAt[k]) * 160f) * (0.55f - k * 0.08f);
            data[i] = (crack + crunch + thump + chunks) * Mathf.Clamp01(sec / 0.0015f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.75f;
        AudioClip clip = AudioClip.Create("Husk Crush " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>The woodchipper taking a block in: a motor whine that revs up, a grinding crunch of chips, then a rattle as they spit out.</summary>
    /// <summary>
    /// The Recycler working: a low motor hum that climbs in pitch, a rattle of husks banging round the bin that gets faster and louder over
    /// 'seconds', and a heavy clunk at the end (when it pays).
    /// </summary>
    public static AudioClip RecyclerShake(float seconds)
    {
        const int rate = 22050;
        float length = Mathf.Max(0.5f, seconds) + 0.25f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        System.Random rng = new System.Random(4242);
        float phase = 0f, lp = 0f, nextRattle = 0f, rattleT = 99f, rattleHz = 900f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float k = Mathf.Clamp01(sec / seconds);                       // 0 -> 1 over the shake
            float hum = 0f, rattle = 0f, clunk = 0f;
            if (sec < seconds)
            {
                float hz = 55f + 70f * k * k;                               // the motor winds up
                phase += hz / rate * Mathf.PI * 2f;
                hum = (Mathf.Sin(phase) + 0.45f * Mathf.Sin(phase * 2f) + 0.2f * Mathf.Sign(Mathf.Sin(phase * 3f))) * (0.18f + 0.3f * k);
                // Rattles: short bright knocks, closer together and louder as it shakes harder.
                if (sec >= nextRattle)
                {
                    rattleT = 0f;
                    rattleHz = 600f + (float)rng.NextDouble() * 900f;
                    nextRattle = sec + Mathf.Lerp(0.16f, 0.03f, k) * (0.6f + (float)rng.NextDouble() * 0.8f);
                }
                rattleT += 1f / rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += (noise - lp) * 0.35f;
                rattle = (Mathf.Sin(rattleT * rattleHz * Mathf.PI * 2f) * 0.6f + lp * 0.8f) * Mathf.Exp(-rattleT * 70f) * (0.25f + 0.75f * k);
            }
            else
            {
                float t = sec - seconds;                                     // the clunk as the lid lets go
                float noise = (float)rng.NextDouble() * 2f - 1f;
                clunk = (Mathf.Sin(t * 70f * Mathf.PI * 2f) * 1.2f + noise * 0.5f * Mathf.Exp(-t * 60f)) * Mathf.Exp(-t * 14f);
            }
            data[i] = (hum + rattle + clunk) * Mathf.Clamp01(sec / 0.05f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.75f;
        AudioClip clip = AudioClip.Create("Recycler Shake", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static AudioClip Chipper(int seed)
    {
        const int rate = 22050;
        float len = 0.7f;
        int n = (int)(rate * len);
        float[] data = new float[n];
        System.Random rng = new System.Random(3300 + seed * 31);
        float phase = 0f, lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 3.2f);
            phase += (150f + 160f * Mathf.Min(1f, t * 3f) + 12f * seed) / rate;
            float motor = (Mathf.Sin(phase * Mathf.PI * 2f) + 0.5f * Mathf.Sin(phase * 3f * Mathf.PI * 2f)) * 0.3f;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.22f;
            lp2 += (lp - lp2) * 0.5f;
            float crunch = (rng.NextDouble() < 0.28 ? noise : lp2) * (0.6f + 0.4f * Mathf.Sin(t * 70f)) * Mathf.Exp(-t * 5f);
            data[i] = (motor * 0.8f + crunch * 0.9f) * env;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.75f;
        AudioClip clip = AudioClip.Create("Chipper " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Fishing sounds. kind: 0 fish splash + flop, 1 seaweed slap, 2 shell clack, 3 pearl chime, 4 driftwood knock, 5 jellyfish squelch + zap.</summary>
    public static AudioClip Fishing(int kind, int seed)
    {
        const int rate = 22050;
        float len = kind == 3 ? 0.55f : kind == 5 ? 0.4f : 0.25f;
        int n = (int)(rate * len);
        float[] data = new float[n];
        System.Random rng = new System.Random(3400 + kind * 83 + seed * 17);
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float v = 0f;
            switch (kind)
            {
                case 0: // a splash of water then a wet flop
                    lp += (noise - lp) * 0.3f;
                    v = (noise - lp) * Mathf.Exp(-t * 14f) * 0.8f + Mathf.Sin(t * (140f + seed * 15f) * Mathf.PI * 2f) * Mathf.Exp(-t * 18f) * 0.6f * (t > 0.05f ? 1f : 0f);
                    break;
                case 1: // a wet slap
                    lp += (noise - lp) * 0.2f;
                    v = lp * Mathf.Exp(-t * 20f) * 1.4f + Mathf.Sin(t * (110f + seed * 10f) * Mathf.PI * 2f) * Mathf.Exp(-t * 26f) * 0.5f;
                    break;
                case 2: // a hard clack
                    v = Mathf.Sin(t * (1400f + seed * 160f) * Mathf.PI * 2f) * Mathf.Exp(-t * 60f) + noise * Mathf.Exp(-t * 90f) * 0.5f;
                    break;
                case 3: // a soft pearly chime
                    phase += (1040f + seed * 130f) / rate;
                    v = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 6f) * 0.7f + Mathf.Sin(phase * Mathf.PI * 3f) * Mathf.Exp(-t * 9f) * 0.25f + noise * Mathf.Exp(-t * 80f) * 0.2f;
                    break;
                case 4: // a dry wooden knock
                    v = Mathf.Sin(t * (190f + seed * 25f) * Mathf.PI * 2f) * Mathf.Exp(-t * 35f) + noise * Mathf.Exp(-t * 70f) * 0.4f;
                    break;
                default: // a squelch with an electric crackle
                    lp += (noise - lp) * 0.15f;
                    v = lp * Mathf.Exp(-t * 9f) + (rng.NextDouble() < 0.1 ? noise : 0f) * Mathf.Exp(-t * 7f) * 0.8f;
                    break;
            }
            data[i] = v * Mathf.Clamp01(t / 0.003f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("Fishing " + kind + "_" + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Foraging sounds. kind: 0 berry squish, 1 mushroom pop, 2 herb rustle, 3 truffle dig + chime, 4 weed rip, 5 toadstool hiss.</summary>
    public static AudioClip Forage(int kind, int seed)
    {
        const int rate = 22050;
        float len = kind == 3 ? 0.45f : kind == 5 ? 0.5f : 0.2f;
        int n = (int)(rate * len);
        float[] data = new float[n];
        System.Random rng = new System.Random(2900 + kind * 71 + seed * 13);
        float lp = 0f, lp2 = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float v = 0f;
            switch (kind)
            {
                case 0: // a wet squish
                    lp += (noise - lp) * 0.14f;
                    v = lp * Mathf.Exp(-t * 16f) * 1.2f + Mathf.Sin(t * (200f - 80f * t * 6f + seed * 20f) * Mathf.PI * 2f) * Mathf.Exp(-t * 22f) * 0.7f;
                    break;
                case 1: // a soft pop with a puff of air
                    lp += (noise - lp) * 0.35f;
                    v = Mathf.Sin(t * (320f + seed * 30f) * Mathf.PI * 2f) * Mathf.Exp(-t * 45f) + lp * Mathf.Exp(-t * 14f) * 0.5f;
                    break;
                case 2: // a leafy rustle
                    lp += (noise - lp) * 0.5f;
                    v = (noise - lp) * Mathf.Sin(Mathf.Clamp01(t / 0.18f) * Mathf.PI) * 0.6f;
                    break;
                case 3: // digging up something precious: a scrape then a small bright chime
                    lp += (noise - lp) * 0.12f;
                    phase += (880f + seed * 110f) / rate;
                    v = lp * Mathf.Exp(-t * 18f) * 1.2f + (t > 0.06f ? Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-(t - 0.06f) * 9f) * 0.45f : 0f);
                    break;
                case 4: // tearing out a weed: a short ripping crackle
                    v = (rng.NextDouble() < 0.3 ? noise : 0f) * Mathf.Exp(-t * 20f) + (noise - lp) * 0.2f * Mathf.Exp(-t * 28f);
                    lp += (noise - lp) * 0.4f;
                    break;
                default: // a poisonous hiss
                    lp += (noise - lp) * 0.6f;
                    lp2 += (lp - lp2) * 0.6f;
                    v = (noise - lp2) * Mathf.Exp(-t * 6f) * 0.7f;
                    break;
            }
            data[i] = v * Mathf.Clamp01(t / 0.003f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("Forage " + kind + "_" + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Woodcutting sounds. kind: 2 sap squelch, 3 leaf rustle, 5 twig snap, 6 hornet buzz (Wood / Bark / Heartwood chops are PixelAxeSynth.WoodChop now).</summary>
    public static AudioClip Wood(int kind, int seed)
    {
        const int rate = 22050;
        float len = kind == 6 ? 0.45f : 0.22f;
        int n = (int)(rate * len);
        float[] data = new float[n];
        System.Random rng = new System.Random(2100 + kind * 97 + seed * 19);
        float lp = 0f, lp2 = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float v = 0f;
            switch (kind)
            {
                case 2: // sap: a wet squelch
                    lp += (noise - lp) * 0.15f;
                    v = lp * Mathf.Exp(-t * 14f) + Mathf.Sin(t * (170f + 120f * Mathf.Sin(t * 26f)) * Mathf.PI * 2f) * Mathf.Exp(-t * 15f) * 0.7f;
                    break;
                case 3: // leaf: a soft rustle
                    lp += (noise - lp) * 0.5f;
                    v = (noise - lp) * Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI) * 0.55f;
                    break;
                case 5: // twig: a sharp snap
                    lp += (noise - lp) * 0.6f;
                    v = (noise - lp) * Mathf.Exp(-t * 70f) + Mathf.Sin(t * (1800f + seed * 220f) * Mathf.PI * 2f) * Mathf.Exp(-t * 90f) * 0.4f;
                    break;
                default: // hornets: an angry buzz
                    phase += (230f + 40f * Mathf.Sin(t * 40f)) / rate;
                    lp += (noise - lp) * 0.3f;
                    v = (Mathf.Sin(phase * Mathf.PI * 2f) * 0.6f + Mathf.Sign(Mathf.Sin(phase * Mathf.PI * 2f)) * 0.2f + lp * 0.25f) * Mathf.Exp(-t * 5f) * (0.7f + 0.3f * Mathf.Sin(t * 90f));
                    break;
            }
            lp2 += (v - lp2) * 0.7f;
            data[i] = lp2 * Mathf.Clamp01(t / 0.003f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("Wood " + kind + "_" + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A short digging sound: a gritty scrape of dirt over a low thud. 'seed' picks one of a few variants.</summary>
    public static AudioClip Dig(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.26f);
        float[] data = new float[n];
        System.Random rng = new System.Random(900 + seed * 17);
        float lp = 0f, lp2 = 0f;
        float thumpHz = 60f + seed * 9f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.32f;            // rough, mid-low scrape
            lp2 += (lp - lp2) * 0.5f;
            float scrape = lp2 * (0.55f + 0.45f * Mathf.Sin(sec * (70f + seed * 12f))) * Mathf.Exp(-sec * 11f);
            float grit = rng.NextDouble() < 0.035 ? ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-sec * 18f) : 0f;
            float thump = Mathf.Sin(sec * thumpHz * Mathf.PI * 2f) * Mathf.Exp(-sec * 24f) * 0.9f;
            data[i] = (scrape * 1.4f + grit * 0.6f + thump) * Mathf.Clamp01(sec / 0.004f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("Dig " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
