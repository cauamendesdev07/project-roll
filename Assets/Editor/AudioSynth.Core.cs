using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Gera todos os sons do jogo por síntese: sem arquivos de terceiros e sem licenças.
// Menu: Project Roll > Gerar sons. Os WAVs ficam em Assets/Resources/Audio e são versionados,
// então o build não depende de rodar este script. Mesma semente = mesmos sons.
//
// Loops (ambiente, música, rolamento) são feitos "periódicos": todo sinal de entrada é indexado
// em módulo n e os filtros/reverbs passam duas vezes pelo laço, guardando só a segunda volta.
// Assim o fim emenda no começo sem estalo.
public static partial class AudioSynth
{
    private const string Root = "Assets/Resources/Audio";
    private const float Tau = Mathf.PI * 2f;

    private static System.Random rng;

    [MenuItem("Project Roll/Gerar sons")]
    public static void GenerateAll()
    {
        GenerateSfx();
        GenerateAmbient();
        GenerateMusic();
        GenerateBossa();
        AssetDatabase.Refresh();
        ConfigureImporters();
        Debug.Log("Sons gerados em " + Root);
    }

    // ---------- Aleatório ----------

    private static void Seed(int seed) => rng = new System.Random(seed);
    private static float Rand() => (float)rng.NextDouble();
    private static float Range(float a, float b) => a + (b - a) * Rand();
    private static float Noise() => Rand() * 2f - 1f;

    private static float[] NoiseTable(int n)
    {
        var t = new float[n];
        for (int i = 0; i < n; i++) t[i] = Noise();
        return t;
    }

    // ---------- Filtros ----------

    // Filtro de estado variável (TPT): passa-baixa, passa-banda e passa-alta ao mesmo tempo.
    private class Svf
    {
        private float ic1, ic2;
        public float Lp, Bp, Hp;

        public float Process(float x, float fc, float q, int rate)
        {
            fc = Mathf.Clamp(fc, 10f, rate * 0.45f);
            float g = Mathf.Tan(Mathf.PI * fc / rate);
            float k = 1f / q;
            float a1 = 1f / (1f + g * (g + k));
            float a2 = g * a1;
            float a3 = g * a2;
            float v3 = x - ic2;
            float v1 = a1 * ic1 + a2 * v3;
            float v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2f * v1 - ic1;
            ic2 = 2f * v2 - ic2;
            Lp = v2; Bp = v1; Hp = x - k * v1 - v2;
            return Lp;
        }
    }

    private class OnePole
    {
        private float z;
        public float Lp(float x, float fc, int rate)
        {
            float a = 1f - Mathf.Exp(-Tau * fc / rate);
            z += a * (x - z);
            return z;
        }
    }

    // Reverb Schroeder/Freeverb simples (mono).
    private class Reverb
    {
        private readonly float[][] comb;
        private readonly int[] combIdx;
        private readonly float[] combStore;
        private readonly float[] combFb;
        private readonly float[][] ap;
        private readonly int[] apIdx;
        private readonly float damp;

        public Reverb(int rate, float size, float t60, float damping)
        {
            float[] cms = { 29.7f, 37.1f, 41.1f, 43.7f, 31.3f, 47.9f };
            comb = new float[cms.Length][];
            combIdx = new int[cms.Length];
            combStore = new float[cms.Length];
            combFb = new float[cms.Length];
            for (int c = 0; c < cms.Length; c++)
            {
                float d = cms[c] * size / 1000f;
                comb[c] = new float[Mathf.Max(1, (int)(d * rate))];
                combFb[c] = Mathf.Pow(10f, -3f * d / t60);
            }
            float[] aps = { 5.0f, 1.7f, 12.9f };
            ap = new float[aps.Length][];
            apIdx = new int[aps.Length];
            for (int a = 0; a < aps.Length; a++)
                ap[a] = new float[Mathf.Max(1, (int)(aps[a] / 1000f * rate))];
            damp = damping;
        }

        public float Process(float x)
        {
            float sum = 0f;
            for (int c = 0; c < comb.Length; c++)
            {
                float y = comb[c][combIdx[c]];
                combStore[c] = y * (1f - damp) + combStore[c] * damp;
                comb[c][combIdx[c]] = x + combStore[c] * combFb[c];
                combIdx[c] = (combIdx[c] + 1) % comb[c].Length;
                sum += y;
            }
            float o = sum / comb.Length;
            for (int a = 0; a < ap.Length; a++)
            {
                float b = ap[a][apIdx[a]];
                float y = b - o;
                ap[a][apIdx[a]] = o + b * 0.5f;
                apIdx[a] = (apIdx[a] + 1) % ap[a].Length;
                o = y;
            }
            return o;
        }
    }

    // ---------- Laços e utilidades ----------

    // Roda f duas vezes ao longo de n amostras (f recebe o índice em módulo n) e guarda a segunda volta.
    private static float[] Loop(int n, Func<int, float> f)
    {
        var o = new float[n];
        for (int i = 0; i < 2 * n; i++)
        {
            float y = f(i % n);
            if (i >= n) o[i - n] = y;
        }
        return o;
    }

    // Reverb aplicado a um laço (cauda dá a volta).
    private static float[] LoopReverb(float[] x, int rate, float size, float t60, float damp, float wet)
    {
        var r = new Reverb(rate, size, t60, damp);
        return Loop(x.Length, i => x[i] * (1f - wet) + r.Process(x[i]) * wet);
    }

    // Reverb aplicado a um som curto (estende o tamanho para caber a cauda).
    private static float[] OneShotReverb(float[] x, int rate, float size, float t60, float damp, float wet)
    {
        int n = x.Length + (int)(t60 * 0.6f * rate);
        var r = new Reverb(rate, size, t60, damp);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            float d = i < x.Length ? x[i] : 0f;
            o[i] = d * (1f - wet) + r.Process(d) * wet;
        }
        return o;
    }

    // Seno periódico com número inteiro de ciclos no laço.
    private static float Lfo(int i, int n, float cycles, float phase = 0f) =>
        Mathf.Sin(Tau * (cycles * i / (float)n + phase));

    // Rajadas suaves de 0 a 1 (soma de senos periódicos).
    private static Func<int, float> Gusts(int n, params int[] cycles)
    {
        var ph = new float[cycles.Length];
        for (int k = 0; k < ph.Length; k++) ph[k] = Rand();
        return i =>
        {
            float s = 0f, w = 0f;
            for (int k = 0; k < cycles.Length; k++)
            {
                float a = 1f / (k + 1);
                s += a * Lfo(i, n, cycles[k], ph[k]);
                w += a;
            }
            return 0.5f + 0.5f * s / w;
        };
    }

    // Soma um evento ao buffer; em laços o evento dá a volta no fim.
    private static void Mix(float[] dst, float[] ev, int start, float gain, bool wrap)
    {
        int n = dst.Length;
        for (int i = 0; i < ev.Length; i++)
        {
            int j = start + i;
            if (wrap) j = ((j % n) + n) % n;
            else if (j < 0 || j >= n) continue;
            dst[j] += ev[i] * gain;
        }
    }

    private static void Mix(float[] dst, float[] src, float gain)
    {
        for (int i = 0; i < dst.Length && i < src.Length; i++) dst[i] += src[i] * gain;
    }

    private static float Env(float t, float attack, float decay) =>
        t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);

    // Envoltória em sino (0 → 1 → 0) ao longo de dur.
    private static float Hump(float t, float dur) =>
        t < 0f || t > dur ? 0f : Mathf.Pow(Mathf.Sin(Mathf.PI * t / dur), 2f);

    private static float Midi(float note) => 440f * Mathf.Pow(2f, (note - 69f) / 12f);

    // Sino/marimba: parciais inarmônicos com decaimentos diferentes.
    private static float[] Bell(int rate, float freq, float dur, float decay, float brightness = 1f)
    {
        int n = (int)(dur * rate);
        var o = new float[n];
        float[] ratio = { 1f, 2.76f, 5.4f, 8.93f };
        float[] amp = { 1f, 0.45f * brightness, 0.18f * brightness, 0.07f * brightness };
        float[] dec = { 1f, 0.55f, 0.3f, 0.18f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float s = 0f;
            for (int p = 0; p < ratio.Length; p++)
            {
                if (freq * ratio[p] > rate * 0.45f) continue;
                s += amp[p] * Mathf.Sin(Tau * freq * ratio[p] * t) * Mathf.Exp(-t / (decay * dec[p]));
            }
            o[i] = s * Mathf.Min(1f, t / 0.002f);
        }
        return o;
    }

    // Tom com frequência variando no tempo (fase acumulada).
    private static float[] Sweep(int rate, float dur, Func<float, float> freq, Func<float, float> env, float h2 = 0f, float h3 = 0f)
    {
        int n = (int)(dur * rate);
        var o = new float[n];
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            ph += Tau * freq(t) / rate;
            o[i] = (Mathf.Sin(ph) + h2 * Mathf.Sin(2f * ph) + h3 * Mathf.Sin(3f * ph)) * env(t);
        }
        return o;
    }

    private static void NormalizePeak(float[] x, float peak)
    {
        float m = 1e-6f;
        foreach (float v in x) m = Mathf.Max(m, Mathf.Abs(v));
        float k = peak / m;
        for (int i = 0; i < x.Length; i++) x[i] *= k;
    }

    // Ajusta pelo volume médio (RMS) e limita picos suavemente.
    private static void NormalizeRms(float[] x, float rms, float maxPeak)
    {
        double s = 0;
        foreach (float v in x) s += v * v;
        float cur = Mathf.Max(1e-6f, Mathf.Sqrt((float)(s / x.Length)));
        float k = rms / cur;
        for (int i = 0; i < x.Length; i++)
        {
            float v = x[i] * k;
            x[i] = maxPeak * (float)Math.Tanh(v / maxPeak);
        }
    }

    private static void FadeEdges(float[] x, int rate, float fadeIn = 0.002f, float fadeOut = 0.01f)
    {
        int a = Mathf.Min(x.Length, (int)(fadeIn * rate));
        int b = Mathf.Min(x.Length, (int)(fadeOut * rate));
        for (int i = 0; i < a; i++) x[i] *= i / (float)a;
        for (int i = 0; i < b; i++) x[x.Length - 1 - i] *= i / (float)b;
    }

    // ---------- Arquivos ----------

    private static void Save(string relPath, float[] data, int rate)
    {
        // Laços: tira o nível DC (subtrair a média não cria emenda num sinal periódico).
        if (relPath.StartsWith("Ambient") || relPath.StartsWith("Music") || relPath == "Sfx/roll")
        {
            double mean = 0;
            foreach (float v in data) mean += v;
            mean /= data.Length;
            for (int i = 0; i < data.Length; i++) data[i] -= (float)mean;
        }

        string path = Path.Combine(Root, relPath + ".wav");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var bw = new BinaryWriter(File.Create(path));
        int bytes = data.Length * 2;
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + bytes);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);      // PCM
        bw.Write((short)1);      // mono
        bw.Write(rate);
        bw.Write(rate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(bytes);
        foreach (float v in data)
            bw.Write((short)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 32767f));
    }

    private static void ConfigureImporters()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not AudioImporter imp)
                continue;

            bool longClip = path.Contains("/Ambient/") || path.Contains("/Music/");
            var s = imp.defaultSampleSettings;
            s.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = longClip ? 0.45f : 0.7f;
            s.preloadAudioData = true;
            imp.defaultSampleSettings = s;
            imp.forceToMono = false; // já são mono; com true a Unity normaliza e desfaz o equilíbrio de volumes
            imp.loadInBackground = longClip;
            imp.SaveAndReimport();
        }
    }
}
