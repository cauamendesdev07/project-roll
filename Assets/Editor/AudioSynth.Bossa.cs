using UnityEngine;

// Bossa nova da fase do Rio (laço de 60 s, 128 bpm): violão de nylon (Karplus-Strong) com o polegar
// fazendo o baixo nos tempos 1 e 3 e os dedos tocando os acordes sincopados, mais chocalho e aro.
// Progressão genérica ii–V–I (sem melodia de música existente).
public static partial class AudioSynth
{
    private static void GenerateBossa()
    {
        Seed(1958);
        const int R = MusicRate;
        const float bpm = 128f;
        float beat = 60f / bpm, bar = beat * 4f;
        const int bars = 32;
        int n = (int)(bars * bar * R);

        // dois compassos por acorde: Em7(9) – A7(13) – Dmaj7(9) – Bm7(11)
        int[][] chords =
        {
            new[] { 52, 59, 62, 66, 67 },   // Em7(9): E B D F# G
            new[] { 45, 55, 61, 66, 64 },   // A7(13): A G C# F# E
            new[] { 50, 57, 61, 64, 66 },   // Dmaj7(9): D A C# E F#
            new[] { 47, 57, 62, 64, 66 },   // Bm7(11): B A D E F#
        };
        int[] roots = { 40, 33, 38, 35 };

        var guitar = new float[n];
        var bass = new float[n];
        var perc = new float[n];

        // padrão de acordes em colcheias ao longo de 2 compassos (16 colcheias): batida típica sincopada
        int[] comp = { 0, 3, 6, 10, 13 };
        for (int b = 0; b < bars; b += 2)
        {
            int ci = (b / 2) % 4;
            float t0 = b * bar;
            // baixo: tempos 1 e 3, alternando fundamental e quinta
            for (int k = 0; k < 4; k++)
            {
                float t = t0 + k * beat * 2f;
                int note = roots[ci] + (k % 2 == 1 ? 7 : 0);
                Mix(bass, Pluck(R, Midi(note), 1.4f, 0.35f, 0.996f), (int)(t * R), 0.55f, true);
            }
            foreach (int e in comp)
            {
                float t = t0 + e * beat * 0.5f + Range(-0.008f, 0.008f);
                var chord = chords[ci];
                for (int v = 1; v < chord.Length; v++)   // dedos: notas de cima, quase juntas (leve arpejo)
                    Mix(guitar, Pluck(R, Midi(chord[v]), 0.9f, 0.55f, 0.993f), (int)((t + v * 0.006f) * R), 0.22f * Range(0.85f, 1f), true);
            }
        }

        // chocalho em semicolcheias (acento no contratempo) e aro na clave da bossa
        int sixteenths = bars * 16;
        for (int i = 0; i < sixteenths; i++)
        {
            float t = i * beat * 0.25f;
            float acc = i % 4 == 2 ? 1f : (i % 2 == 1 ? 0.55f : 0.4f);
            Mix(perc, Shaker(R), (int)(t * R), 0.09f * acc, true);
        }
        int[] clave = { 0, 3, 6, 10, 13 };
        for (int b = 0; b < bars; b += 2)
            foreach (int e in clave)
                Mix(perc, Click(R, 1800f, 0.012f), (int)((b * bar + e * beat * 0.5f) * R), 0.16f, true);

        var mix = new float[n];
        Mix(mix, guitar, 1f);
        Mix(mix, bass, 1f);
        Mix(mix, perc, 1f);
        var lp = new Svf();
        mix = Loop(n, i => lp.Process(mix[i], 5200f, 0.7f, R));
        mix = LoopReverb(mix, R, 0.9f, 1.6f, 0.5f, 0.18f);
        NormalizeRms(mix, 0.12f, 0.85f);
        Save("Music/bossa", mix, R);
    }

    // Corda dedilhada (Karplus-Strong): ruído num atraso de 1 período, filtrado a cada volta.
    private static float[] Pluck(int rate, float freq, float dur, float brightness, float decay)
    {
        int n = (int)(dur * rate);
        int p = Mathf.Max(2, Mathf.RoundToInt(rate / freq));
        var buf = new float[p];
        var lp = new OnePole();
        for (int i = 0; i < p; i++) buf[i] = lp.Lp(Noise(), 800f + 6000f * brightness, rate);
        var o = new float[n];
        int idx = 0;
        for (int i = 0; i < n; i++)
        {
            int next = (idx + 1) % p;
            float v = buf[idx];
            buf[idx] = decay * 0.5f * (buf[idx] + buf[next]);
            o[i] = v;
            idx = next;
        }
        FadeEdges(o, rate, 0.001f, 0.05f);
        return o;
    }

    private static float[] Shaker(int rate)
    {
        int n = (int)(0.05f * rate);
        var o = new float[n];
        var f = new Svf();
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            f.Process(Noise(), 6000f, 0.8f, rate);
            o[i] = f.Hp * Env(t, 0.004f, 0.012f);
        }
        return o;
    }
}
