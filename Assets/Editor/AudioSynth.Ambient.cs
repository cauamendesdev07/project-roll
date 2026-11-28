using System;
using UnityEngine;

// Som ambiente de cada bioma: laços de 16 s a 22 kHz.
public static partial class AudioSynth
{
    private const int AmbRate = 22050;
    private const float AmbLength = 16f;

    private static void GenerateAmbient()
    {
        Func<float[]>[] biomes = { Amazon, Caatinga, Cerrado, Pantanal, Island, Lencois, Cave, Serra, City, Sky };
        for (int i = 0; i < biomes.Length; i++)
        {
            Seed(1000 + i * 37);
            var o = biomes[i]();
            NormalizeRms(o, 0.11f, 0.85f);
            Save($"Ambient/amb_{i + 1:00}", o, AmbRate);
        }
    }

    private static int AmbN => (int)(AmbLength * AmbRate);

    // ---------- Camadas ----------

    // Vento: ruído filtrado com corte e volume subindo nas rajadas.
    private static float[] Wind(float baseFc, float fcRange, float q, float level, float gustDepth, params int[] cycles)
    {
        int n = AmbN;
        var w = NoiseTable(n);
        var gust = Gusts(n, cycles);
        var f = new Svf();
        return Loop(n, i =>
        {
            float g = gust(i);
            f.Process(w[i], baseFc + fcRange * g, q, AmbRate);
            return f.Bp * level * (1f - gustDepth + gustDepth * g * g);
        });
    }

    // Assobio do vento: banda estreita que desliza com as rajadas.
    private static float[] Whistle(float lo, float hi, float q, float level, params int[] cycles)
    {
        int n = AmbN;
        var w = NoiseTable(n);
        var gust = Gusts(n, cycles);
        var f = new Svf();
        return Loop(n, i =>
        {
            float g = gust(i);
            f.Process(w[i], Mathf.Lerp(lo, hi, g), q, AmbRate);
            return f.Bp * level * g * g;
        });
    }

    // Ronco grave (ruído marrom filtrado).
    private static float[] Rumble(float fc, float level)
    {
        int n = AmbN;
        var w = NoiseTable(n);
        var a = new Svf(); var b = new Svf();
        return Loop(n, i =>
        {
            a.Process(w[i], fc, 0.7f, AmbRate);
            b.Process(a.Lp, fc, 0.7f, AmbRate);
            return b.Lp * level;
        });
    }

    // Água corrente: banda média com cintilação rápida + bolhinhas.
    private static float[] Brook(float level, float bubbles)
    {
        int n = AmbN;
        var w = NoiseTable(n);
        var m = NoiseTable(n);
        var band = new Svf(); var flick = new Svf(); var low = new Svf();
        var o = Loop(n, i =>
        {
            band.Process(w[i], 1500f, 0.9f, AmbRate);
            low.Process(w[(i * 3 + 7) % n], 500f, 0.7f, AmbRate);
            flick.Process(m[i], 9f, 0.7f, AmbRate);
            return (band.Bp * (0.5f + 6f * Mathf.Abs(flick.Lp)) + low.Lp * 0.4f) * level;
        });
        int count = (int)(bubbles * AmbLength);
        for (int k = 0; k < count; k++)
        {
            float f0 = Range(500f, 1600f), dur = Range(0.015f, 0.04f);
            var ev = Sweep(AmbRate, dur, t => f0 * (1f + 1.2f * t / dur), t => Hump(t, dur));
            Mix(o, ev, (int)(Rand() * n), level * Range(0.05f, 0.18f), true);
        }
        return o;
    }

    // Marolas: ruído grave com volume subindo e descendo devagar.
    private static float[] Lapping(float fc, float level, int cycles)
    {
        int n = AmbN;
        var w = NoiseTable(n);
        var f = new Svf();
        float ph = Rand();
        return Loop(n, i =>
        {
            float s = 0.5f + 0.5f * Lfo(i, n, cycles, ph);
            f.Process(w[i], fc * (0.7f + 0.6f * s), 0.8f, AmbRate);
            return f.Lp * level * (0.3f + 0.7f * s * s);
        });
    }

    // Espalha eventos pelo laço (com volta no fim).
    private static void Scatter(float[] dst, float perSecond, Func<float[]> make, float minGain, float maxGain)
    {
        int count = Mathf.Max(1, Mathf.RoundToInt(perSecond * AmbLength));
        for (int k = 0; k < count; k++)
            Mix(dst, make(), (int)(Rand() * dst.Length), Range(minGain, maxGain), true);
    }

    private static float[] Sum(params float[][] layers)
    {
        var o = new float[layers[0].Length];
        foreach (var l in layers) Mix(o, l, 1f);
        return o;
    }

    // ---------- Vozes de bichos ----------

    // Canto de pássaro: três "espécies" diferentes.
    private static float[] BirdSong()
    {
        int R = AmbRate;
        int kind = rng.Next(3);
        var song = new float[(int)(1.6f * R)];
        float t = 0f;
        if (kind == 0)
        {
            // assobios descendo
            int notes = rng.Next(3, 6);
            float f0 = Range(2600f, 3400f);
            for (int k = 0; k < notes; k++)
            {
                float a = f0 * (1f - 0.06f * k), d = 0.09f;
                Mix(song, Sweep(R, d, x => a * (1.12f - 0.25f * x / d), x => Hump(x, d), 0.15f), (int)(t * R), 1f, false);
                t += d + 0.06f;
            }
        }
        else if (kind == 1)
        {
            // trinado rápido
            int notes = rng.Next(7, 13);
            float f0 = Range(3600f, 4400f);
            for (int k = 0; k < notes; k++)
            {
                float d = 0.035f;
                Mix(song, Sweep(R, d, x => f0 + 900f * x / d, x => Hump(x, d)), (int)(t * R), 0.8f, false);
                t += d + 0.022f;
            }
        }
        else
        {
            // "fi-fiu" de duas notas
            float hiF = Range(2500f, 3000f), loF = hiF * 0.8f;
            Mix(song, Sweep(R, 0.22f, x => hiF * (1f + 0.02f * Mathf.Sin(Tau * 30f * x)), x => Hump(x, 0.22f), 0.1f), 0, 1f, false);
            Mix(song, Sweep(R, 0.28f, x => loF * (1f + 0.15f * x), x => Hump(x, 0.28f), 0.1f), (int)(0.3f * R), 1f, false);
        }
        return song;
    }

    // Cigarra: chiado agudo pulsado que cresce e some.
    private static float[] Cicada(float dur)
    {
        int R = AmbRate, n = (int)(dur * R);
        var o = new float[n];
        var f = new Svf();
        float fc = Range(4200f, 5600f), pulse = Range(160f, 220f);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)R;
            f.Process(Noise(), fc, 7f, R);
            float am = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Tau * pulse * t), 3f);
            float env = Mathf.Clamp01(t / (dur * 0.35f)) * Mathf.Clamp01((dur - t) / (dur * 0.3f));
            o[i] = f.Bp * am * env * env;
        }
        return o;
    }

    // Grilos: pulsos curtos repetindo num período que fecha o laço.
    private static float[] Crickets(int count, float level)
    {
        int R = AmbRate, n = AmbN;
        var o = new float[n];
        for (int c = 0; c < count; c++)
        {
            float freq = Range(3800f, 5000f);
            int chirps = Mathf.RoundToInt(AmbLength / Range(0.5f, 0.9f));
            float period = AmbLength / chirps;
            float offset = Rand() * period;
            int pulses = rng.Next(2, 5);
            float gain = level * Range(0.4f, 1f);
            var chirp = new float[(int)(pulses * 0.032f * R)];
            for (int i = 0; i < chirp.Length; i++)
            {
                float t = i / (float)R;
                float local = t % 0.032f;
                chirp[i] = Mathf.Sin(Tau * freq * t) * Hump(local, 0.016f);
            }
            for (int k = 0; k < chirps; k++)
                if (Rand() > 0.15f)
                    Mix(o, chirp, (int)((offset + k * period) * R), gain, true);
        }
        return o;
    }

    // Sapo: coaxado grave e áspero em 2 a 4 pulsos.
    private static float[] Croak()
    {
        int R = AmbRate;
        int pulses = rng.Next(2, 5);
        float f0 = Range(170f, 320f), pd = Range(0.05f, 0.08f), gap = Range(0.02f, 0.05f);
        var o = new float[(int)(pulses * (pd + gap) * R)];
        var form = new Svf();
        float ph = 0f;
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R;
            float local = t % (pd + gap);
            float f = f0 * (1f + 0.15f * local / pd);
            ph += f / R;
            float saw = 2f * (ph - Mathf.Floor(ph)) - 1f;
            form.Process(saw, 750f, 2.5f, R);
            o[i] = form.Bp * Hump(local, pd);
        }
        return o;
    }

    private static float[] Peep()
    {
        float f0 = Range(2600f, 3200f), d = 0.045f;
        var o = new float[(int)(0.16f * AmbRate)];
        var p = Sweep(AmbRate, d, t => f0 * (1f + 0.08f * t / d), t => Hump(t, d));
        Mix(o, p, 0, 1f, false);
        Mix(o, p, (int)(0.1f * AmbRate), 0.8f, false);
        return o;
    }

    // Ave aquática: "quá" nasal.
    private static float[] WaterBird()
    {
        int R = AmbRate;
        float d = Range(0.18f, 0.28f), f0 = Range(380f, 480f);
        var o = new float[(int)(d * R)];
        var form = new Svf();
        float ph = 0f;
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R;
            ph += f0 * (1f - 0.18f * t / d) / R;
            form.Process(2f * (ph - Mathf.Floor(ph)) - 1f, 1300f, 3f, R);
            o[i] = form.Bp * Hump(t, d);
        }
        return o;
    }

    // Gaivota: "quiu" que sobe e desce.
    private static float[] Gull()
    {
        float d = Range(0.35f, 0.5f), f0 = Range(1300f, 1600f);
        return Sweep(AmbRate, d,
            t => (t < 0.08f ? f0 + 900f * t / 0.08f : f0 + 900f - 1100f * (t - 0.08f) / (d - 0.08f)) * (1f + 0.02f * Mathf.Sin(Tau * 24f * t)),
            t => Hump(t, d), 0.35f, 0.15f);
    }

    // Gota d'água em caverna.
    private static float[] Drip()
    {
        float f0 = Range(900f, 1900f), d = Range(0.03f, 0.06f);
        return Sweep(AmbRate, d, t => f0 * (1f + 1.6f * t / d), t => Env(t, 0.001f, d * 0.4f));
    }

    // Bolha de lava.
    private static float[] Bloop()
    {
        float f0 = Range(60f, 150f), d = Range(0.08f, 0.22f);
        return Sweep(AmbRate, d, t => f0 * (1f + 2.2f * t / d), t => Hump(t, d), 0.3f);
    }

    // Folhas: chiado agudo que aparece nas rajadas.
    private static float[] Leaves(float level, params int[] cycles)
    {
        int n = AmbN;
        var w = NoiseTable(n);
        var gust = Gusts(n, cycles);
        var f = new Svf();
        return Loop(n, i =>
        {
            float g = gust(i);
            f.Process(w[i], 3500f, 0.8f, AmbRate);
            return f.Hp * level * Mathf.Pow(g, 4f);
        });
    }

    // Ondas: cada onda sobe rápido e recua devagar; o corte acompanha.
    private static float[] Waves(float level)
    {
        int R = AmbRate, n = AmbN;
        var env = new float[n];
        int waves = 3;
        for (int k = 0; k < waves; k++)
        {
            float start = (k + Range(-0.1f, 0.1f)) * AmbLength / waves;
            float rise = Range(1.2f, 1.8f), fall = Range(3.5f, 4.5f), peak = Range(0.7f, 1f);
            int len = (int)((rise + fall * 1.5f) * R);
            var bump = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)R;
                bump[i] = peak * (t < rise ? Mathf.Pow(t / rise, 2f) : Mathf.Exp(-(t - rise) / (fall * 0.45f)));
            }
            Mix(env, bump, (int)(start * R), 1f, true);
        }
        var w = NoiseTable(n);
        var f = new Svf();
        return Loop(n, i =>
        {
            float e = Mathf.Clamp01(env[i]);
            f.Process(w[i], 250f + 2400f * e * e, 0.7f, R);
            return (f.Lp + f.Bp * 0.25f) * level * (0.18f + 0.82f * e);
        });
    }

    // ---------- Biomas ----------

    // Amazônia: mata densa (muitas aves, araras, insetos, sapos ao longe), rio e chuva leve que vem e vai.
    private static float[] Amazon()
    {
        var o = Sum(Wind(380f, 260f, 0.6f, 0.3f, 0.4f, 1, 2, 5), Brook(0.18f, 8f), Rain(0.22f), Crickets(3, 0.07f));
        int n = o.Length;
        Mix(o, Cicada(Range(6f, 9f)), (int)(Rand() * n), 0.08f, true);
        Scatter(o, 1.0f, BirdSong, 0.08f, 0.32f);
        Scatter(o, 0.15f, Squawk, 0.12f, 0.25f);
        Scatter(o, 0.5f, Croak, 0.04f, 0.1f);
        return o;
    }

    // Chuva leve: chiado agudo + gotas, subindo e descendo uma vez no laço.
    private static float[] Rain(float level)
    {
        int n = AmbN;
        var w = NoiseTable(n);
        var f = new Svf();
        float ph = Rand();
        var o = Loop(n, i =>
        {
            float s = 0.5f + 0.5f * Lfo(i, n, 1, ph);
            f.Process(w[i], 4200f, 0.6f, AmbRate);
            return f.Hp * level * s * s;
        });
        int drops = (int)(30 * AmbLength);
        for (int k = 0; k < drops; k++)
        {
            int at = (int)(Rand() * n);
            float s = 0.5f + 0.5f * Lfo(at, n, 1, ph);
            Mix(o, Click(AmbRate, Range(2500f, 6000f), Range(0.002f, 0.005f)), at, level * 0.5f * s * s * Range(0.3f, 1f), true);
        }
        return o;
    }

    // Arara: grito rouco e nasal.
    private static float[] Squawk()
    {
        int R = AmbRate;
        float d = Range(0.22f, 0.35f), f0 = Range(800f, 1100f);
        var o = new float[(int)(d * R)];
        var form = new Svf(); var grit = new Svf();
        float ph = 0f;
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R;
            ph += f0 * (1.1f - 0.3f * t / d) / R;
            float saw = 2f * (ph - Mathf.Floor(ph)) - 1f;
            form.Process(saw + Noise() * 0.4f, 1900f, 2f, R);
            grit.Process(Noise(), 2600f, 1.2f, R);
            o[i] = (form.Bp + grit.Bp * 0.3f) * Hump(t, d);
        }
        return o;
    }

    private static float[] Caatinga()
    {
        var o = Wind(700f, 900f, 0.8f, 1f, 0.7f, 1, 3, 4);
        int n = o.Length;
        for (int k = 0; k < 4; k++)
        {
            float dur = Range(5f, 9f);
            Mix(o, Cicada(dur), (int)((k / 4f + Range(-0.05f, 0.05f)) * n), Range(0.12f, 0.22f), true);
        }
        Scatter(o, 0.08f, BirdSong, 0.05f, 0.12f);
        return o;
    }

    private static float[] Cerrado()
    {
        return Sum(Wind(550f, 650f, 0.7f, 0.7f, 0.6f, 1, 2, 3), Leaves(0.2f, 1, 3, 5), Crickets(4, 0.18f));
    }

    private static float[] Pantanal()
    {
        var o = Sum(Lapping(450f, 0.9f, 7), Wind(400f, 300f, 0.6f, 0.25f, 0.4f, 1, 2));
        Scatter(o, 1.6f, Croak, 0.15f, 0.45f);
        Scatter(o, 0.6f, Peep, 0.05f, 0.14f);
        Scatter(o, 0.18f, WaterBird, 0.2f, 0.35f);
        return o;
    }

    private static float[] Island()
    {
        var o = Sum(Waves(1f), Wind(800f, 600f, 0.6f, 0.25f, 0.5f, 1, 2));
        Scatter(o, 0.25f, Gull, 0.06f, 0.16f);
        return o;
    }

    private static float[] Lencois()
    {
        return Sum(Wind(900f, 1200f, 0.7f, 1f, 0.6f, 1, 2, 5), Whistle(1100f, 1700f, 6f, 0.25f, 1, 3), Lapping(500f, 0.4f, 6));
    }

    private static float[] Cave()
    {
        int n = AmbN;
        var drips = new float[n];
        Scatter(drips, 1.1f, Drip, 0.15f, 0.6f);
        drips = LoopReverb(drips, AmbRate, 1.6f, 3.2f, 0.3f, 0.55f);
        return Sum(drips, Rumble(110f, 0.6f), Wind(300f, 150f, 0.6f, 0.15f, 0.3f, 1, 2));
    }

    // Urupema: vento frio da serra nos campos (pouco assobio) e quero-queros ao longe.
    private static float[] Serra()
    {
        var o = Sum(Wind(420f, 600f, 0.6f, 0.9f, 0.7f, 1, 2, 3), Whistle(600f, 1000f, 7f, 0.08f, 1, 3), Leaves(0.12f, 1, 3, 5));
        Scatter(o, 0.12f, QueroQuero, 0.12f, 0.3f);
        return o;
    }

    // "quero-quero": dois gritos agudos e estridentes descendo.
    private static float[] QueroQuero()
    {
        int R = AmbRate;
        var o = new float[(int)(0.7f * R)];
        float f0 = Range(2500f, 2900f);
        for (int k = 0; k < 2; k++)
        {
            var call = Sweep(R, 0.16f, t => f0 * (1.08f - 0.3f * t / 0.16f), t => Hump(t, 0.16f), 0.35f, 0.15f);
            Mix(o, call, (int)(k * 0.24f * R), k == 0 ? 1f : 0.85f, false);
        }
        return o;
    }

    private static float[] Snow()
    {
        return Sum(Wind(450f, 700f, 0.6f, 1f, 0.8f, 1, 2, 3), Whistle(500f, 1100f, 8f, 0.35f, 1, 2, 5), Whistle(900f, 1500f, 10f, 0.15f, 2, 3));
    }

    // São Paulo: ronco do trânsito, chiado de pneus, buzinas e uma sirene ao longe.
    private static float[] City()
    {
        int n = AmbN;
        var w = NoiseTable(n);
        var lo = new Svf(); var hiss = new Svf();
        var gust = Gusts(n, 1, 2, 5);
        var o = Loop(n, i =>
        {
            float g = gust(i);
            lo.Process(w[i], 160f + 80f * g, 0.7f, AmbRate);
            hiss.Process(w[(i * 5 + 3) % n], 1400f, 0.8f, AmbRate);
            return lo.Lp * (0.9f + 0.4f * g) + hiss.Bp * 0.18f * (0.4f + 0.6f * g);
        });
        Scatter(o, 0.45f, Honk, 0.06f, 0.22f);
        Mix(o, Siren(Range(4f, 6f)), (int)(Rand() * n), 0.05f, true);
        return o;
    }

    // Buzina: duas notas quadradas abafadas, curta ou dupla.
    private static float[] Honk()
    {
        int R = AmbRate;
        float f0 = Range(330f, 480f), f1 = f0 * Range(1.2f, 1.3f);
        int beeps = rng.Next(1, 3);
        float d = Range(0.15f, 0.4f);
        var o = new float[(int)((beeps * (d + 0.12f)) * R)];
        var f = new Svf();
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R;
            float local = t % (d + 0.12f);
            float env = local < d ? Mathf.Min(1f, local / 0.01f) * Mathf.Min(1f, (d - local) / 0.02f) : 0f;
            float sq = Mathf.Sign(Mathf.Sin(Tau * f0 * t)) + Mathf.Sign(Mathf.Sin(Tau * f1 * t));
            f.Process(sq, 1800f, 0.8f, R);
            o[i] = f.Lp * env * 0.5f;
        }
        return o;
    }

    // Sirene distante (sobe e desce), abafada.
    private static float[] Siren(float dur)
    {
        int R = AmbRate, n = (int)(dur * R);
        var o = new float[n];
        var f = new Svf();
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)R;
            float fr = 900f + 300f * Mathf.Sin(Tau * 0.7f * t);
            ph += fr / R;
            f.Process(Mathf.Sin(Tau * ph) + 0.3f * Mathf.Sin(Tau * ph * 2f), 1500f, 0.7f, R);
            o[i] = f.Lp * Hump(t, dur);
        }
        return o;
    }

    private static float[] Volcano()
    {
        var o = Rumble(70f, 1.3f);
        var hiss = Wind(3000f, 1500f, 0.7f, 0.08f, 0.6f, 1, 3);
        Mix(o, hiss, 1f);
        Scatter(o, 6f, Bloop, 0.08f, 0.3f);
        Scatter(o, 3f, () => Click(AmbRate, Range(1500f, 3000f), Range(0.003f, 0.008f)), 0.03f, 0.12f);
        return o;
    }

    private static float[] Sky()
    {
        return Sum(Wind(600f, 1000f, 0.5f, 1f, 0.75f, 1, 2, 3), Whistle(1000f, 1500f, 5f, 0.2f, 1, 4), Rumble(140f, 0.35f));
    }
}
