// ---- System notes ----
// PixelAudioSynthShows.cs
//   Part of PixelSynth (static partial class, the code-made sound synthesizer): the hold / menu / chest / Dragon Cube / fire / ore / ghost / UFO / star sounds and the Normalize helper. The system notes are in PixelAudio.cs.
// ---------------------------------------------------
using UnityEngine;

public static partial class PixelSynth
{
    /// <summary>The removal buildup: a smooth tone that climbs from low to high with a growing shimmer, 0.8 s long (PixelHold plays it with its pitch scaled so it lasts as long as the hold).</summary>
    public const float BuildupSeconds = 0.8f;

    public static AudioClip Buildup()
    {
        const int rate = 44100;
        int n = (int)(rate * BuildupSeconds);
        float[] data = new float[n];
        System.Random rng = new System.Random(77);
        float phase = 0f, phase2 = 0f, noise = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float sec = i / (float)rate;
            float hz = Mathf.Lerp(150f, 1250f, t * t);
            phase += hz / rate;
            phase2 += hz * 1.5f / rate;
            float tone = Mathf.Sin(phase * Mathf.PI * 2f) * 0.6f + Mathf.Sin(phase2 * Mathf.PI * 2f) * 0.22f * t;
            float tremolo = 1f - 0.25f * Mathf.Sin(sec * Mathf.Lerp(10f, 34f, t) * Mathf.PI * 2f) * t;
            noise = Mathf.Lerp(noise, (float)rng.NextDouble() * 2f - 1f, 0.35f);
            data[i] = (tone * tremolo + noise * 0.12f * t) * Mathf.Lerp(0.2f, 1f, t) * Mathf.Clamp01(sec / 0.03f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.7f;
        AudioClip clip = AudioClip.Create("RemoveBuildup", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A soft, mellow water-drop "tip" for opening menus: a pure tone that glides up a little (like a drop rising off the surface), a rounded 10 ms start, a short airy tail and a quiet octave above. No noise, no low thump: made to be heard thousands of times.</summary>
    public static AudioClip MenuBloop()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.26f);
        float[] data = new float[n];
        float phase = 0f, phase2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float hz = 392f * (1f + 0.12f * (1f - Mathf.Exp(-sec * 60f)));   // G4 sliding up a touch
            phase += hz / rate; phase2 += hz * 2f / rate;
            float attack = Mathf.Sin(Mathf.Clamp01(sec / 0.012f) * Mathf.PI * 0.5f);   // a rounded start (no click)
            float env = attack * Mathf.Exp(-sec * 17f);
            float tone = Mathf.Sin(phase * 6.2832f) + 0.16f * Mathf.Sin(phase2 * 6.2832f) * Mathf.Exp(-sec * 30f);
            data[i] = tone * env * 0.32f;
        }
        for (int i = n - 1; i > n - 400 && i >= 0; i--) data[i] *= (n - 1 - i) / 400f;   // (a short fade at the very end)
        AudioClip clip = AudioClip.Create("menu_bloop", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static AudioClip Charge()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.7f);
        float[] data = new float[n];
        System.Random rng = new System.Random(4242);
        float phase = 0f, crackle = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float sec = i / (float)rate;
            float hz = Mathf.Lerp(180f, 1500f, t * t);
            phase += hz / rate;
            phase -= Mathf.Floor(phase);
            float buzz = (phase * 2f - 1f) * Mathf.Lerp(0.15f, 0.45f, t);

            if (rng.NextDouble() < 0.01 + 0.06 * t) crackle = (float)rng.NextDouble() * 2f - 1f;
            crackle *= 0.88f;

            float body = (buzz + crackle * Mathf.Lerp(0.2f, 0.9f, t)) * Mathf.Clamp01(sec / 0.05f);
            float snap = t > 0.78f ? ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-(t - 0.78f) * 45f) : 0f; // the final crack
            data[i] = body * (t > 0.78f ? Mathf.Exp(-(t - 0.78f) * 18f) : 1f) + snap * 0.9f;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.85f;
        AudioClip clip = AudioClip.Create("Overcharge", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>The dragon summoning: a deep drone that rises with shimmering overtones and crackle, ending in a boom (about 5 seconds).</summary>
    public static AudioClip Summon()
    {
        const int rate = 22050;
        float length = 5.2f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        System.Random rng = new System.Random(777);
        float p1 = 0f, p2 = 0f, p3 = 0f, crackle = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float t = sec / length;
            float rise = t * t;
            float f = Mathf.Lerp(55f, 420f, rise);
            p1 += f / rate; p2 += f * 1.5f / rate; p3 += f * 2.01f / rate;
            float drone = Mathf.Sin(p1 * 6.2832f) * 0.5f + Mathf.Sin(p2 * 6.2832f) * 0.25f * rise + Mathf.Sin(p3 * 6.2832f) * 0.18f * rise;
            drone *= 1f + 0.25f * Mathf.Sin(sec * Mathf.Lerp(4f, 22f, rise) * 6.2832f); // tremolo that speeds up

            if (rng.NextDouble() < 0.002 + 0.03 * rise) crackle = (float)rng.NextDouble() * 2f - 1f;
            crackle *= 0.9f;

            float env = Mathf.Clamp01(sec / 0.4f) * Mathf.Lerp(0.3f, 1f, rise);
            float value = drone * env + crackle * 0.5f * rise;

            // The boom when the cubes burst together.
            float boomT = sec - 3.9f;
            if (boomT > 0f)
            {
                float boom = Mathf.Sin(boomT * 6.2832f * Mathf.Lerp(90f, 30f, Mathf.Clamp01(boomT * 2f))) * Mathf.Exp(-boomT * 3.5f);
                float hiss = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-boomT * 7f);
                value = value * Mathf.Exp(-boomT * 4f) + boom * 1.1f + hiss * 0.6f;
            }
            data[i] = value;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.85f;
        AudioClip clip = AudioClip.Create("Dragon Summon", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A knock on a wooden chest: a short dull thump with a woody click and a tiny coin chime. 'seed' picks a variant.</summary>
    public static AudioClip ChestHit(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.35f);
        float[] data = new float[n];
        System.Random rng = new System.Random(900 + seed * 17);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.3f;
            float thump = Mathf.Sin(sec * (120f + seed * 12f) * 6.2832f) * Mathf.Exp(-sec * 22f);
            float knock = lp * Mathf.Exp(-sec * 60f) * 0.8f;
            float chime = (Mathf.Sin(sec * (2200f + seed * 180f) * 6.2832f) + 0.5f * Mathf.Sin(sec * 3300f * 6.2832f)) * Mathf.Exp(-sec * 18f) * 0.25f * Mathf.Clamp01((sec - 0.01f) / 0.004f);
            data[i] = thump + knock + chime;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("ChestHit " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A chest bursts open: a creaking lid, then a shower of coins jingling (about 1.6 seconds).</summary>
    public static AudioClip ChestOpen()
    {
        const int rate = 22050;
        int n = (int)(rate * 1.6f);
        float[] data = new float[n];
        System.Random rng = new System.Random(321);
        float phase = 0f;
        float[] pingAt = new float[30], pingHz = new float[30];
        for (int k = 0; k < pingAt.Length; k++) { pingAt[k] = 0.3f + (float)rng.NextDouble() * 1.0f; pingHz[k] = 1500f + (float)rng.NextDouble() * 2600f; }
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            // The creak: a rough saw that glides up while it wobbles, over the first 0.35 s.
            float v = 0f;
            if (sec < 0.4f)
            {
                float f = Mathf.Lerp(80f, 210f, sec / 0.4f) * (1f + 0.15f * Mathf.Sin(sec * 60f));
                phase += f / rate;
                float saw = (phase % 1f) * 2f - 1f;
                v += saw * 0.35f * Mathf.Sin(Mathf.Clamp01(sec / 0.4f) * Mathf.PI);
            }
            // A thump as it opens, then coins.
            float boomT = sec - 0.3f;
            if (boomT > 0f) v += Mathf.Sin(boomT * 70f * 6.2832f) * Mathf.Exp(-boomT * 9f) * 0.8f;
            for (int k = 0; k < pingAt.Length; k++)
            {
                float t = sec - pingAt[k];
                if (t < 0f) continue;
                v += Mathf.Sin(t * pingHz[k] * 6.2832f) * Mathf.Exp(-t * 14f) * 0.16f * Mathf.Clamp01(t / 0.003f);
            }
            data[i] = v;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("ChestOpen", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A Dragon Cube turns up: a swelling whoosh, then a sparkling rising arpeggio of bell tones (about 3 seconds).</summary>
    public static AudioClip DragonFind()
    {
        const int rate = 22050;
        float length = 3.2f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        System.Random rng = new System.Random(4242);
        float lp = 0f;
        float[] notes = { 392f, 493.9f, 587.3f, 784f, 987.8f, 1174.7f, 1568f };
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.08f;
            float whoosh = lp * 2.2f * Mathf.Sin(Mathf.Clamp01(sec / 0.7f) * Mathf.PI) * (sec < 0.7f ? 1f : 0f);
            float v = whoosh;
            for (int k = 0; k < notes.Length; k++)
            {
                float start = 0.55f + k * 0.22f;
                float t = sec - start;
                if (t < 0f) continue;
                float env = Mathf.Exp(-t * 2.6f) * Mathf.Clamp01(t / 0.01f);
                v += (Mathf.Sin(t * notes[k] * 6.2832f) + 0.35f * Mathf.Sin(t * notes[k] * 2.01f * 6.2832f)) * env * 0.28f;
            }
            data[i] = v;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("DragonFind", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>The Dragon Cube drops into the backpack: a soft thump with a quick glittering chime.</summary>
    public static AudioClip DragonStash()
    {
        const int rate = 22050;
        int n = (int)(rate * 0.9f);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float thump = Mathf.Sin(sec * 90f * 6.2832f) * Mathf.Exp(-sec * 12f);
            float chime = (Mathf.Sin(sec * 1568f * 6.2832f) + Mathf.Sin(sec * 2093f * 6.2832f) * 0.6f) * Mathf.Exp(-sec * 5f) * 0.4f * Mathf.Clamp01((sec - 0.03f) / 0.01f);
            data[i] = thump + chime;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("DragonStash", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A whoosh of fire: a rising roar of band-limited noise with a deep thump at the start and a sprinkle of crackles. 'seed' picks a variant.</summary>
    public static AudioClip FireBurst(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.7f);
        float[] data = new float[n];
        System.Random rng = new System.Random(1100 + seed * 41);
        float lp = 0f, lp2 = 0f, crackle = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.12f;           // low-passed: a rumbling roar
            lp2 += (lp - lp2) * 0.35f;
            float env = Mathf.Clamp01(sec / 0.05f) * Mathf.Exp(-sec * 4.2f);
            float thump = Mathf.Sin(sec * (70f - seed * 6f) * Mathf.PI * 2f) * Mathf.Exp(-sec * 14f) * 0.9f;
            if (rng.NextDouble() < 0.012 * (1f - sec)) crackle = ((float)rng.NextDouble() * 2f - 1f) * 0.9f;
            crackle *= 0.86f;
            data[i] = (lp2 * 3.2f * env + thump + crackle * 0.5f * Mathf.Exp(-sec * 3f));
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.75f;
        AudioClip clip = AudioClip.Create("FireBurst " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Fire crackling: a few sharp pops and snaps over a soft hiss.</summary>
    public static AudioClip FireCrackle(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.45f);
        float[] data = new float[n];
        System.Random rng = new System.Random(1300 + seed * 53);
        float lp = 0f, pop = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.3f;
            if (rng.NextDouble() < 0.006) pop = ((float)rng.NextDouble() * 2f - 1f);
            pop *= 0.78f;
            data[i] = (pop * 1.2f + (noise - lp) * 0.1f) * Mathf.Exp(-sec * 5f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("FireCrackle " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A pickaxe hit on rock: a tick of grit and a short metallic ping. 'seed' picks the pitch.</summary>
    public static AudioClip OreChip(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.22f);
        float[] data = new float[n];
        System.Random rng = new System.Random(1500 + seed * 17);
        float hz = 1500f + seed * 260f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float tick = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-sec * 160f);
            float ping = (Mathf.Sin(sec * hz * Mathf.PI * 2f) + 0.5f * Mathf.Sin(sec * hz * 2.76f * Mathf.PI * 2f)) * Mathf.Exp(-sec * 28f);
            data[i] = tick * 0.9f + ping * 0.45f;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.7f;
        AudioClip clip = AudioClip.Create("OreChip " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>The ore breaking: a crack, a low rumble and a cascade of falling nuggets (little pings). 'seed' picks the variant.</summary>
    public static AudioClip OreBreak(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.5f);
        float[] data = new float[n];
        System.Random rng = new System.Random(1700 + seed * 29);
        float lp = 0f;
        float[] at = { 0.05f, 0.09f, 0.14f, 0.2f, 0.27f };
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.4f;
            float crack = lp * Mathf.Exp(-sec * 38f) * 1.4f;
            float rumble = Mathf.Sin(sec * (55f + (seed % 5) * 6f) * Mathf.PI * 2f) * Mathf.Exp(-sec * 9f) * 0.8f;
            float nuggets = 0f;
            for (int k = 0; k < at.Length; k++)
            {
                float t = sec - at[k] - (seed % 3) * 0.004f;
                if (t < 0f) continue;
                float hz = 1600f + k * 330f + (seed % 7) * 70f;
                nuggets += Mathf.Sin(t * hz * Mathf.PI * 2f) * Mathf.Exp(-t * 55f) * (0.5f - k * 0.07f);
            }
            data[i] = (crack + rumble + nuggets) * Mathf.Clamp01(sec / 0.002f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.75f;
        AudioClip clip = AudioClip.Create("OreBreak " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static void Normalize(float[] data, float level)
    {
        float peak = 0.0001f;
        for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * level;
    }

    /// <summary>The Ghost pixel's click: a wet gloopy "bloop" (a pitch that drops then wobbles up, a second bubble pop) with a soft airy breath after it.</summary>
    public static AudioClip GhostClick(int variant)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.5f);
        float[] data = new float[n];
        System.Random rnd = new System.Random(300 + variant * 17);
        float baseFreq = 170f + variant * 38f;
        float phase = 0f, phase2 = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            // Bloop: the pitch starts high, drops fast and then rises a touch, like a bubble closing.
            float f = baseFreq * (1f + 1.1f * Mathf.Exp(-t * 18f) + 0.18f * Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI));
            phase += f / rate;
            float v = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 9f);
            // A second smaller bubble.
            float t2 = t - 0.085f;
            if (t2 > 0f)
            {
                phase2 += baseFreq * 2.3f * (1f + 0.5f * Mathf.Exp(-t2 * 22f)) / rate;
                v += Mathf.Sin(phase2 * Mathf.PI * 2f) * Mathf.Exp(-t2 * 20f) * 0.45f;
            }
            // Wet squelch: low-passed noise right at the start.
            lp += (((float)rnd.NextDouble() * 2f - 1f) - lp) * 0.22f;
            v += lp * Mathf.Exp(-t * 34f) * 0.5f;
            // The ghost's breath: two soft detuned tones with a slow wobble.
            float breath = (Mathf.Sin(t * 520f * Mathf.PI * 2f + 2f * Mathf.Sin(t * 6f)) + 0.6f * Mathf.Sin(t * 781f * Mathf.PI * 2f)) * Mathf.Exp(-t * 5f) * Mathf.Clamp01(t / 0.05f) * 0.13f;
            data[i] = (v + breath) * Mathf.Clamp01(t / 0.002f);
        }
        Normalize(data, 0.55f);
        AudioClip clip = AudioClip.Create("GhostClick" + variant, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A soft squishy landing: a low "splut" dropping in pitch with a puff of wet noise.</summary>
    public static AudioClip GhostBounce(int variant)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.26f);
        float[] data = new float[n];
        System.Random rnd = new System.Random(500 + variant * 13);
        float phase = 0f, lp = 0f;
        float baseFreq = 70f + variant * 14f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            phase += baseFreq * (1f + 1.6f * Mathf.Exp(-t * 26f)) / rate;
            float v = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 15f);
            lp += (((float)rnd.NextDouble() * 2f - 1f) - lp) * 0.16f;
            v += lp * Mathf.Exp(-t * 40f) * 0.55f;
            float t2 = t - 0.045f;
            if (t2 > 0f) v += Mathf.Sin(t2 * (360f + variant * 60f) * Mathf.PI * 2f) * Mathf.Exp(-t2 * 38f) * 0.22f;   // a tiny bubble
            data[i] = v * Mathf.Clamp01(t / 0.002f);
        }
        Normalize(data, 0.5f);
        AudioClip clip = AudioClip.Create("GhostBounce" + variant, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>The ghost starts to float away: a hollow, breathy "whoo" that rises and fades.</summary>
    public static AudioClip GhostFloat()
    {
        const int rate = 22050;
        float length = 1.1f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        System.Random rnd = new System.Random(777);
        float phase = 0f, lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float k = t / length;
            float env = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
            env *= env;
            float f = Mathf.Lerp(300f, 520f, k) * (1f + 0.025f * Mathf.Sin(t * 9f));
            phase += f / rate;
            float tone = Mathf.Sin(phase * Mathf.PI * 2f) + 0.35f * Mathf.Sin(phase * 2f * Mathf.PI * 2f);
            // Breath: noise run through two one-pole low-passes whose cutoff rises with the sound.
            float coef = Mathf.Lerp(0.05f, 0.18f, k);
            lp += (((float)rnd.NextDouble() * 2f - 1f) - lp) * coef;
            lp2 += (lp - lp2) * coef;
            data[i] = (tone * 0.35f + lp2 * 2.2f) * env;
        }
        Normalize(data, 0.45f);
        AudioClip clip = AudioClip.Create("GhostFloat", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>The UFO's abduction beam: a rising, wobbling hum with a shimmer on top that sweeps up, then a soft pop as the victim arrives (about 2.6 s).</summary>
    public static AudioClip UfoBeam()
    {
        const int rate = 22050;
        float length = 2.7f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        System.Random rnd = new System.Random(4242);
        float phase = 0f, phase2 = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float k = Mathf.Clamp01(t / 2.4f);
            float env = Mathf.Clamp01(t / 0.12f) * (t < 2.4f ? 1f : 0f);
            // The hum climbs while a vibrato widens; a second, detuned voice beats against it.
            float f = Mathf.Lerp(140f, 520f, k * k) * (1f + 0.06f * Mathf.Sin(t * (7f + 22f * k)));
            phase += f / rate;
            phase2 += f * 1.012f / rate;
            float v = (Mathf.Sin(phase * Mathf.PI * 2f) + 0.8f * Mathf.Sin(phase2 * Mathf.PI * 2f)) * 0.3f;
            v += Mathf.Sin(phase * 3f * Mathf.PI * 2f) * 0.07f;
            // A glassy shimmer: filtered noise whose level pulses faster and louder towards the top.
            lp += (((float)rnd.NextDouble() * 2f - 1f) - lp) * Mathf.Lerp(0.1f, 0.4f, k);
            v += lp * (0.18f + 0.3f * k) * (0.6f + 0.4f * Mathf.Sin(t * (18f + 30f * k)));
            data[i] = v * env;
            // The pop when it arrives.
            float tp = t - 2.42f;
            if (tp > 0f) data[i] += Mathf.Sin(tp * 640f * Mathf.PI * 2f * (1f - tp * 2.2f)) * Mathf.Exp(-tp * 26f) * 0.9f;
        }
        Normalize(data, 0.5f);
        AudioClip clip = AudioClip.Create("UfoBeam", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A star falling: a glassy whistle that slides down while tiny sparkles tinkle behind it. 'variant' changes the pitch and the sparkles.</summary>
    public static AudioClip StarFall(int variant)
    {
        const int rate = 22050;
        float length = 1.25f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        System.Random rnd = new System.Random(900 + variant * 31);
        float top = variant == 1 ? 3300f : 3700f, bottom = variant == 1 ? 760f : 840f;
        float phase = 0f;
        // Sparkle ticks: short high pings scattered through the fall, getting sparser.
        int pings = 16;
        float[] pingAt = new float[pings], pingFreq = new float[pings], pingAmp = new float[pings];
        for (int k = 0; k < pings; k++)
        {
            pingAt[k] = Mathf.Pow((float)rnd.NextDouble(), 0.8f) * length * 0.95f;
            pingFreq[k] = 2800f + (float)rnd.NextDouble() * 4200f;
            pingAmp[k] = 0.1f + (float)rnd.NextDouble() * 0.16f;
        }
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float k01 = sec / length;
            float freq = bottom + (top - bottom) * Mathf.Exp(-sec * 3.2f);
            freq *= 1f + 0.012f * Mathf.Sin(sec * 38f);
            phase += freq / rate;
            float env = Mathf.Clamp01(sec / 0.015f) * Mathf.Exp(-sec * 1.9f) * Mathf.Clamp01((length - sec) / 0.25f);
            float v = (Mathf.Sin(phase * Mathf.PI * 2f) + 0.25f * Mathf.Sin(phase * 2f * Mathf.PI * 2f)) * env * 0.5f;
            for (int p = 0; p < pings; p++)
            {
                float t = sec - pingAt[p];
                if (t < 0f || t > 0.25f) continue;
                v += Mathf.Sin(t * pingFreq[p] * Mathf.PI * 2f) * Mathf.Exp(-t * 28f) * pingAmp[p] * (1f - k01 * 0.6f);
            }
            data[i] = v;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.55f;
        AudioClip clip = AudioClip.Create("StarFall" + variant, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A short rising three-note chime for collecting a fallen star.</summary>
    public static AudioClip StarChime()
    {
        const int rate = 22050;
        int n = (int)(rate * 0.7f);
        float[] data = new float[n];
        float[] notes = { 1046.5f, 1568f, 2093f };   // C6, G6, C7
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float v = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float t = sec - k * 0.07f;
                if (t < 0f) continue;
                v += (Mathf.Sin(t * notes[k] * Mathf.PI * 2f) + 0.2f * Mathf.Sin(t * notes[k] * 2.01f * Mathf.PI * 2f)) * Mathf.Exp(-t * 6f) * 0.5f;
            }
            data[i] = v * Mathf.Clamp01(sec / 0.003f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.55f;
        AudioClip clip = AudioClip.Create("StarChime", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A bright three-note bell sparkle: the sound of a rich vein.</summary>
    public static AudioClip OreGleam()
    {
        const int rate = 22050;
        int n = (int)(rate * 0.8f);
        float[] data = new float[n];
        float[] notes = { 1318.5f, 1760f, 2637f };   // E6, A6, E7
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float v = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float t = sec - k * 0.09f;
                if (t < 0f) continue;
                v += (Mathf.Sin(t * notes[k] * Mathf.PI * 2f) + 0.3f * Mathf.Sin(t * notes[k] * 3f * Mathf.PI * 2f)) * Mathf.Exp(-t * 5.5f) * 0.5f;
            }
            data[i] = v * Mathf.Clamp01(sec / 0.003f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("OreGleam", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A short metallic clink of a chunk of ore landing. 'seed' picks the pitch.</summary>
    public static AudioClip OreClink(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.18f);
        float[] data = new float[n];
        System.Random rng = new System.Random(1900 + seed * 13);
        float hz = 900f + (seed % 9) * 130f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float tick = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-sec * 220f);
            float ring = (Mathf.Sin(sec * hz * Mathf.PI * 2f) + 0.6f * Mathf.Sin(sec * hz * 1.59f * Mathf.PI * 2f)) * Mathf.Exp(-sec * 32f);
            data[i] = tick * 0.7f + ring * 0.5f;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("OreClink " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
