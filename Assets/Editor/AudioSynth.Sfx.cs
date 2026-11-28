using UnityEngine;

// Efeitos curtos (44,1 kHz).
public static partial class AudioSynth
{
    private const int SfxRate = 44100;

    private static void GenerateSfx()
    {
        Seed(101);
        int R = SfxRate;

        // Pulo: tom subindo rápido + sopro curto.
        {
            var tone = Sweep(R, 0.22f, t => 200f + 360f * (1f - Mathf.Exp(-t * 28f)), t => Env(t, 0.004f, 0.055f), 0.25f);
            var air = new float[tone.Length];
            var f = new Svf();
            for (int i = 0; i < air.Length; i++)
            {
                float t = i / (float)R;
                f.Process(Noise(), 700f + 3500f * t / 0.22f, 1.3f, R);
                air[i] = f.Bp * Env(t, 0.01f, 0.05f);
            }
            Mix(tone, air, 0.45f);
            Finish(tone, "Sfx/jump", R, 0.8f);
        }

        // Pouso: baque grave + ruído abafado (o volume em jogo depende do impacto).
        {
            var thud = Sweep(R, 0.3f, t => 52f + 85f * Mathf.Exp(-t * 32f), t => Env(t, 0.002f, 0.08f), 0.3f);
            var f = new Svf();
            for (int i = 0; i < thud.Length; i++)
            {
                float t = i / (float)R;
                f.Process(Noise(), 500f, 0.7f, R);
                thud[i] += f.Lp * Env(t, 0.001f, 0.025f) * 0.9f;
            }
            Finish(thud, "Sfx/land", R, 0.9f);
        }

        // Rolamento (laço de 2 s): ronco grave com um pouco de aspereza.
        {
            int n = 2 * R;
            var w = NoiseTable(n);
            var g = NoiseTable(n);
            var lp1 = new Svf(); var lp2 = new Svf(); var grit = new Svf(); var am = new Svf();
            var roll = Loop(n, i =>
            {
                lp1.Process(w[i], 180f, 0.7f, R);
                lp2.Process(lp1.Lp, 260f, 0.7f, R);
                am.Process(g[i], 12f, 0.7f, R);
                grit.Process(w[(i * 7 + 13) % n], 1400f, 1.5f, R);
                return lp2.Lp * 1.0f + grit.Bp * 0.12f * (0.6f + 4f * Mathf.Abs(am.Lp));
            });
            NormalizeRms(roll, 0.25f, 0.9f);
            Save("Sfx/roll", roll, R);
        }

        // Queda/respawn: sopro subindo e brilho de sininhos.
        {
            int n = (int)(0.8f * R);
            var o = new float[n];
            var f = new Svf();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)R;
                float fc = 300f * Mathf.Pow(12f, Mathf.Clamp01(t / 0.35f));
                f.Process(Noise(), fc, 1.6f, R);
                o[i] = f.Bp * Hump(t, 0.42f) * 0.9f;
            }
            Mix(o, Bell(R, Midi(88), 0.6f, 0.18f, 0.6f), (int)(0.22f * R), 0.35f, false);
            Mix(o, Bell(R, Midi(93), 0.55f, 0.16f, 0.6f), (int)(0.30f * R), 0.3f, false);
            for (int k = 0; k < 5; k++)
                Mix(o, Bell(R, Midi(Range(91f, 100f)), 0.3f, 0.06f, 0.3f), (int)(Range(0.25f, 0.55f) * R), 0.12f, false);
            Finish(o, "Sfx/respawn", R, 0.75f);
        }

        // Plataforma desmoronando: estalos cada vez mais fortes (0,6 s) e depois o desabamento.
        {
            int n = (int)(1.4f * R);
            var o = new float[n];
            float t0 = 0f;
            while (t0 < 0.6f)
            {
                float k = t0 / 0.6f;
                Mix(o, Click(R, Range(1500f, 3800f), Range(0.004f, 0.01f)), (int)(t0 * R), 0.25f + 0.6f * k * Range(0.5f, 1f), false);
                t0 += Range(0.02f, 0.09f) * (1.2f - k);
            }
            int crash = (int)(0.6f * R);
            var f = new Svf();
            for (int i = crash; i < n; i++)
            {
                float t = (i - crash) / (float)R;
                f.Process(Noise(), 200f + 1400f * Mathf.Exp(-t * 6f), 0.8f, R);
                o[i] += f.Lp * Env(t, 0.004f, 0.22f) * 1.4f;
            }
            Mix(o, Sweep(R, 0.4f, t => 45f + 60f * Mathf.Exp(-t * 20f), t => Env(t, 0.003f, 0.12f)), crash, 0.9f, false);
            for (int k = 0; k < 14; k++)
            {
                float t = Range(0.62f, 1.25f);
                Mix(o, Click(R, Range(900f, 2600f), Range(0.006f, 0.015f)), (int)(t * R), 0.35f * (1.3f - t), false);
            }
            Finish(o, "Sfx/crumble", R, 0.85f);
        }

        // Trampolim: "boing" com vibrato.
        {
            var o = Sweep(R, 0.5f,
                t => (150f + 330f * (1f - Mathf.Exp(-t * 16f))) * (1f + 0.12f * Mathf.Exp(-t * 6f) * Mathf.Sin(Tau * 26f * t)),
                t => Env(t, 0.003f, 0.16f), 0.35f, 0.1f);
            Finish(o, "Sfx/bounce", R, 0.8f);
        }

        // Checkpoint: duas notas de sino subindo.
        {
            var o = new float[(int)(0.9f * R)];
            Mix(o, Bell(R, Midi(76), 0.85f, 0.32f), 0, 0.6f, false);
            Mix(o, Bell(R, Midi(83), 0.75f, 0.36f), (int)(0.11f * R), 0.6f, false);
            o = OneShotReverb(o, R, 1f, 1.2f, 0.4f, 0.22f);
            Finish(o, "Sfx/checkpoint", R, 0.7f);
        }

        // Chegada: arpejo de Dó maior.
        {
            var o = new float[(int)(1.6f * R)];
            int[] notes = { 72, 76, 79, 84 };
            for (int k = 0; k < notes.Length; k++)
                Mix(o, Bell(R, Midi(notes[k]), 1.3f, 0.45f), (int)(k * 0.09f * R), 0.5f, false);
            Mix(o, Bell(R, Midi(79), 1.2f, 0.6f, 0.5f), (int)(0.36f * R), 0.35f, false);
            Mix(o, Bell(R, Midi(84), 1.2f, 0.6f, 0.5f), (int)(0.36f * R), 0.35f, false);
            o = OneShotReverb(o, R, 1.1f, 1.6f, 0.4f, 0.25f);
            Finish(o, "Sfx/finish", R, 0.75f);
        }

        // Fim do jogo: fanfarra mais longa.
        {
            var o = new float[(int)(3.2f * R)];
            int[] notes = { 72, 76, 79, 84, 88, 91 };
            for (int k = 0; k < notes.Length; k++)
                Mix(o, Bell(R, Midi(notes[k]), 1.6f, 0.5f), (int)(k * 0.12f * R), 0.45f, false);
            int chord = (int)(0.8f * R);
            foreach (int m in new[] { 60, 67, 72, 76, 79, 84 })
                Mix(o, Pad(R, Midi(m), 2.3f), chord, 0.12f, false);
            Mix(o, Bell(R, Midi(96), 2f, 0.8f, 0.5f), chord, 0.25f, false);
            o = OneShotReverb(o, R, 1.2f, 2.2f, 0.45f, 0.3f);
            Finish(o, "Sfx/victory", R, 0.8f);
        }

        // Clique de botão.
        {
            var o = Sweep(R, 0.05f, t => 1700f - 4000f * t, t => Env(t, 0.0008f, 0.01f));
            Mix(o, Click(R, 4000f, 0.003f), 0, 0.5f, false);
            Finish(o, "Sfx/click", R, 0.55f);
        }

        // Trovão: estalo + ronco longo com várias ondas (22 kHz basta).
        {
            int r2 = 22050;
            int n = (int)(5f * r2);
            var o = new float[n];
            var lp = new Svf(); var lp2 = new Svf(); var crack = new Svf();
            float brown = 0f;
            var swells = new float[5]; var swellT = new float[5]; var swellW = new float[5];
            for (int k = 0; k < swells.Length; k++) { swells[k] = Range(0.4f, 1f); swellT[k] = Range(0.2f, 3.2f); swellW[k] = Range(0.25f, 0.8f); }
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)r2;
                brown = brown * 0.985f + Noise() * 0.15f;
                lp.Process(brown, 160f, 0.7f, r2);
                lp2.Process(lp.Lp, 220f, 0.7f, r2);
                float env = 0.25f;
                for (int k = 0; k < swells.Length; k++)
                {
                    float d = (t - swellT[k]) / swellW[k];
                    env += swells[k] * Mathf.Exp(-d * d);
                }
                env *= Mathf.Exp(-t / 1.8f) * Mathf.Min(1f, t / 0.05f);
                crack.Process(Noise(), 2500f, 0.6f, r2);
                o[i] = lp2.Lp * env * 2.5f + crack.Lp * Env(t, 0.002f, 0.07f) * 0.6f;
            }
            var dc = new Svf();
            for (int i = 0; i < n; i++) { dc.Process(o[i], 25f, 0.7f, r2); o[i] = dc.Hp; }
            FadeEdges(o, r2, 0.002f, 0.3f);
            NormalizePeak(o, 0.9f);
            Save("Sfx/thunder", o, r2);
        }

        // ---- sons adicionados depois (no fim, para não mudar a sequência aleatória dos anteriores)
        // Avião passando baixo: ronco de turbina que cresce, passa e some (com o assobio caindo).
        {
            int n = (int)(4.5f * R);
            var o = new float[n];
            var band = new Svf(); var low = new Svf();
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)R;
                float k = t / 4.5f;
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(k)), 2.2f);
                band.Process(Noise(), Mathf.Lerp(1600f, 700f, k), 0.9f, R);
                low.Process(Noise(), 220f, 0.7f, R);
                ph += Mathf.Lerp(3200f, 1900f, k) / R;
                o[i] = (band.Bp * 0.8f + low.Lp * 1.2f + Mathf.Sin(Tau * ph) * 0.05f) * env;
            }
            Finish(o, "Sfx/plane", R, 0.85f);
        }

        // Bala perdida: zunido agudo rápido com um estalo no começo.
        {
            var o = Sweep(R, 0.4f, t => 3400f - 2200f * t / 0.4f, t => Hump(t, 0.4f) * 0.6f);
            Mix(o, Click(R, 2500f, 0.012f), 0, 0.9f, false);
            var f = new Svf();
            for (int i = 0; i < o.Length; i++) { float t = i / (float)R; f.Process(Noise(), 5000f, 1.2f, R); o[i] += f.Bp * Hump(t, 0.4f) * 0.4f; }
            Finish(o, "Sfx/whizz", R, 0.8f);
        }

    }

    private static float[] Click(int rate, float freq, float dur)
    {
        int n = Mathf.Max(8, (int)(dur * rate));
        var o = new float[n];
        var f = new Svf();
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            f.Process(Noise(), freq, 1.5f, rate);
            o[i] = f.Bp * Env(t, 0.0005f, dur * 0.3f);
        }
        return o;
    }

    // Pad suave (dois osciladores levemente desafinados, ataque e soltura lentos).
    private static float[] Pad(int rate, float freq, float dur, float attack = 0.4f, float release = 0.9f)
    {
        int n = (int)((dur + release) * rate);
        var o = new float[n];
        float d = 1.0035f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = t < attack ? t / attack : t < dur ? 1f : Mathf.Exp(-(t - dur) / (release * 0.35f));
            float a = Tau * freq * t, b = Tau * freq * d * t;
            o[i] = env * (Mathf.Sin(a) + Mathf.Sin(b) + 0.3f * (Mathf.Sin(2f * a) + Mathf.Sin(2f * b)) + 0.1f * Mathf.Sin(3f * a)) * 0.5f;
        }
        return o;
    }

    private static void Finish(float[] x, string name, int rate, float peak)
    {
        FadeEdges(x, rate);
        NormalizePeak(x, peak);
        Save(name, x, rate);
    }
}
