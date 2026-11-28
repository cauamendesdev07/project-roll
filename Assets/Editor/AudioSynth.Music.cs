using UnityEngine;

// Música: faixa calma em laço de 64 s (pad, baixo e uma marimba em escala pentatônica).
public static partial class AudioSynth
{
    private const int MusicRate = 22050;

    private static void GenerateMusic()
    {
        Seed(7);
        const int R = MusicRate;
        const float chordLen = 4f;
        const int chords = 16;
        int n = (int)(chordLen * chords * R);

        // Lá menor 7 → Fá maj7 → Dó (add9) → Sol (6), quatro vezes.
        int[][] prog =
        {
            new[] { 57, 60, 64, 67 },
            new[] { 53, 57, 60, 64 },
            new[] { 55, 60, 62, 64 },
            new[] { 55, 59, 62, 64 },
        };
        int[] roots = { 45, 41, 48, 43 };

        var pad = new float[n];
        var bass = new float[n];
        var mel = new float[n];

        for (int c = 0; c < chords; c++)
        {
            int start = (int)(c * chordLen * R);
            var chord = prog[c % 4];
            foreach (int m in chord)
                Mix(pad, Pad(R, Midi(m), chordLen, 1.4f, 2.2f), start, 0.16f, true);

            var b = Sweep(R, chordLen + 0.8f, t => Midi(roots[c % 4]), t => Mathf.Min(1f, t / 0.08f) * (0.55f + 0.45f * Mathf.Exp(-t / 0.6f)) * (t > chordLen ? Mathf.Exp(-(t - chordLen) / 0.25f) : 1f), 0.25f);
            Mix(bass, b, start, 0.28f, true);
        }

        // Melodia em Lá menor pentatônica, em colcheias (0,5 s), com frases mais cheias no meio.
        int[] scale = { 69, 72, 74, 76, 79, 81, 84, 86 };
        float[] density = { 0.12f, 0.32f, 0.4f, 0.22f };
        int idx = 3;
        int steps = (int)(chordLen * chords / 0.5f);
        for (int s = 0; s < steps; s++)
        {
            float time = s * 0.5f;
            int section = (int)(time / (chordLen * 4)) % 4;
            if (Rand() > density[section])
                continue;
            idx = Mathf.Clamp(idx + rng.Next(-2, 3), 0, scale.Length - 1);
            // nos tempos fortes, prefere notas do acorde
            var chord = prog[(int)(time / chordLen) % 4];
            if (s % 2 == 0)
            {
                int best = idx, bestDist = 99;
                for (int k = 0; k < scale.Length; k++)
                {
                    bool tone = false;
                    foreach (int m in chord)
                        if (((scale[k] - m) % 12 + 12) % 12 == 0) tone = true;
                    if (tone && Mathf.Abs(k - idx) < bestDist) { best = k; bestDist = Mathf.Abs(k - idx); }
                }
                idx = best;
            }
            float vel = Range(0.5f, 1f);
            Mix(mel, Bell(R, Midi(scale[idx]), 1.6f, 0.55f, 0.35f), (int)(time * R), 0.22f * vel, true);
        }

        // Eco na melodia (3/4 de tempo).
        int delay = (int)(0.75f * R);
        var echo = new float[n];
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++)
                echo[i] = mel[i] + 0.35f * echo[(i - delay + n) % n];

        var mix = new float[n];
        Mix(mix, pad, 1f);
        Mix(mix, bass, 1f);
        Mix(mix, echo, 1f);

        // Suaviza o brilho do pad e põe tudo numa sala grande.
        var lp = new Svf();
        mix = Loop(n, i => lp.Process(mix[i], 3200f, 0.7f, R));
        mix = LoopReverb(mix, R, 1.3f, 2.8f, 0.45f, 0.3f);
        NormalizeRms(mix, 0.12f, 0.85f);
        Save("Music/music", mix, R);
    }
}
