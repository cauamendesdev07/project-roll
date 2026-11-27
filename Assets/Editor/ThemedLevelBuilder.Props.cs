using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

// Props dos biomas (todos low-poly e coloridos pelo atlas do tema).
public static partial class ThemedLevelBuilder
{
    static MB WithSeed(int seed, Action<MB> build)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        build(mb);
        rng = keep;
        return mb;
    }

    // vertical prism helper: rings at given heights/radii, optional rib pattern
    static List<Vector3[]> Prism(Vector3 basePos, int sides, (float y, float r)[] profile, float rot, float rib = 0f, float jitter = 0.05f)
    {
        var rings = new List<Vector3[]>();
        foreach (var (y, r) in profile)
        {
            var ring = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                float a = rot + j * Mathf.PI * 2 / sides;
                float rr = r * (1f + (j % 2 == 0 ? rib : -rib)) * (1f + Rf(-jitter, jitter));
                ring[j] = basePos + new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
            }
            rings.Add(ring);
        }
        return rings;
    }

    // Mandacaru: coluna sulcada com braços que sobem.
    static MB Cactus(float H, int seed) => WithSeed(seed, mb =>
    {
        float r = H * 0.075f;
        void Column(Vector3 basePos, float h, float rad, bool flower)
        {
            var rings = Prism(basePos, 8, new[] { (-0.3f, rad * 1.05f), (h * 0.5f, rad), (h * 0.92f, rad * 0.9f), (h, rad * 0.5f) }, Rf(0, 6.28f), 0.12f, 0.03f);
            Loft(mb, rings, (i, j, n) => UV(j % 2 == 0 ? R.PineMid : R.PineDark, Mathf.Clamp(3 + i + Ri(-1, 1), 0, 7)), basePos, basePos + Vector3.up);
            Fan(mb, basePos + Vector3.up * (h + rad * 0.25f), rings[rings.Count - 1], () => UVr(R.PineMid, 5, 7), Vector3.up);
            if (flower) Blob(mb, basePos + Vector3.up * (h + rad * 0.3f), Vector3.one * rad * 0.55f, 0.1f, 0, n => UVr(R.MushCap, 5, 7));
        }
        Column(Vector3.zero, H, r, Chance(0.3f));
        int arms = Ri(1, 3);
        for (int a = 0; a < arms; a++)
        {
            float ang = Rf(0, 6.28f);
            var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
            float startY = H * Rf(0.3f, 0.55f), outLen = r * Rf(2.4f, 3.4f), armH = H * Rf(0.25f, 0.45f);
            float ar = r * 0.75f;
            // horizontal elbow
            var elbow = new List<Vector3[]>();
            for (int k = 0; k <= 2; k++)
            {
                float t = k / 2f;
                var c = dir * outLen * t + Vector3.up * (startY + t * t * r * 1.2f);
                var ring = new Vector3[6];
                var side = Vector3.Cross(Vector3.up, dir).normalized;
                for (int j = 0; j < 6; j++)
                {
                    float aa = j * Mathf.PI * 2 / 6;
                    ring[j] = c + (side * Mathf.Cos(aa) + Vector3.up * Mathf.Sin(aa)) * ar;
                }
                elbow.Add(ring);
            }
            Loft(mb, elbow, (i, j, n) => UVr(R.PineMid, 3, 5), Vector3.up * startY, Vector3.up * startY + dir);
            Column(dir * outLen + Vector3.up * (startY + r * 0.6f), armH, ar, Chance(0.25f));
        }
    });

    // Árvore seca e retorcida da caatinga: galhos nus e poucas folhas.
    static MB DryTree(float H, int seed, bool leaves) => WithSeed(seed, mb =>
    {
        void Branch(Vector3 from, Vector3 dir, float len, float rad, int depth)
        {
            var to = from + dir * len;
            var side = Vector3.Cross(dir, Vector3.up).sqrMagnitude < 0.01f ? Vector3.right : Vector3.Cross(dir, Vector3.up).normalized;
            var up = Vector3.Cross(side, dir).normalized;
            Vector3[] Ring(Vector3 c, float rr)
            {
                var ring = new Vector3[5];
                for (int j = 0; j < 5; j++) { float a = j * Mathf.PI * 2 / 5; ring[j] = c + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * rr * Rf(0.9f, 1.1f); }
                return ring;
            }
            Loft(mb, new List<Vector3[]> { Ring(from, rad), Ring(to, rad * 0.65f) }, (i, j, n) => UVr(R.Bark, 2, 5), from, to);
            if (depth <= 0)
            {
                if (leaves && Chance(0.7f)) Blob(mb, to, new Vector3(1f, 0.6f, 1f) * H * Rf(0.07f, 0.11f), 0.2f, 0, n => UV(R.LeafLight, Mathf.Clamp(Mathf.RoundToInt(3 + n.y * 2 + Rf(-1, 1)), 0, 7)));
                return;
            }
            int kids = Ri(2, 3);
            for (int k = 0; k < kids; k++)
            {
                var nd = (dir + new Vector3(Rf(-0.9f, 0.9f), Rf(0.1f, 0.6f), Rf(-0.9f, 0.9f))).normalized;
                Branch(to, nd, len * Rf(0.55f, 0.75f), rad * 0.62f, depth - 1);
            }
        }
        var trunkDir = new Vector3(Rf(-0.25f, 0.25f), 1f, Rf(-0.25f, 0.25f)).normalized;
        Branch(Vector3.down * 0.3f, trunkDir, H * 0.45f, H * 0.045f, 2);
    });

    // Árvore do cerrado / savana: tronco torto e copa larga e achatada.
    static MB FlatTopTree(float H, int seed) => WithSeed(seed, mb =>
    {
        float lean = Rf(-0.12f, 0.12f);
        Trunk(mb, H * 0.62f, H * 0.045f, H * 0.03f, 6, Vector3.zero, lean);
        var top = new Vector3(lean * H * 0.62f, H * 0.66f, 0);
        int blobs = Ri(3, 5);
        for (int b = 0; b < blobs; b++)
        {
            var off = new Vector3(Rf(-1, 1), 0, Rf(-1, 1)).normalized * H * Rf(0.08f, 0.26f) + Vector3.up * Rf(-0.03f, 0.06f) * H;
            float s = H * Rf(0.22f, 0.3f);
            bool dark = Chance(0.5f);
            Blob(mb, top + off, new Vector3(s, s * 0.32f, s), 0.15f, 1, n => UV(dark ? R.PineMid : R.LeafLight, Mathf.Clamp(Mathf.RoundToInt(2 + (n.y + 1) * 1.8f + Rf(-0.6f, 0.6f)), 0, 7)));
        }
    });

    // Coqueiro / palmeira: tronco curvo em anéis e folhas longas caídas.
    static MB Palm(float H, int seed) => WithSeed(seed, mb =>
    {
        int segs = 7;
        var bend = new Vector3(Rf(-1, 1), 0, Rf(-1, 1)).normalized * H * Rf(0.12f, 0.3f);
        var pts = new List<Vector3>();
        for (int i = 0; i <= segs; i++) { float t = i / (float)segs; pts.Add(bend * t * t + Vector3.up * (H * t - 0.3f)); }
        var rings = new List<Vector3[]>();
        for (int i = 0; i <= segs; i++)
        {
            float r = H * Mathf.Lerp(0.04f, 0.026f, i / (float)segs);
            var ring = new Vector3[6];
            for (int j = 0; j < 6; j++) { float a = j * Mathf.PI * 2 / 6; ring[j] = pts[i] + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            rings.Add(ring);
        }
        Loft(mb, rings, (i, j, n) => UV(R.Bark, i % 2 == 0 ? 4 : 2), Vector3.zero, Vector3.up);
        var crown = pts[segs];
        int fronds = Ri(7, 9);
        for (int f = 0; f < fronds; f++)
        {
            float ang = f * Mathf.PI * 2 / fronds + Rf(-0.2f, 0.2f);
            var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
            var side = Vector3.Cross(Vector3.up, dir);
            float len = H * Rf(0.38f, 0.5f), w = H * 0.07f;
            Vector3 P(float t) => crown + dir * len * t + Vector3.up * (len * (0.35f * t - 0.75f * t * t));
            for (int k = 0; k < 4; k++)
            {
                float t0 = k / 4f, t1 = (k + 1) / 4f;
                float w0 = w * Mathf.Sin((t0 * 0.85f + 0.1f) * Mathf.PI), w1 = w * Mathf.Sin((t1 * 0.85f + 0.1f) * Mathf.PI);
                var a = P(t0) - side * w0; var b = P(t0) + side * w0; var c = P(t1) + side * w1; var d = P(t1) - side * w1;
                var col = UV(k % 2 == 0 ? R.PineMid : R.LeafLight, Ri(3, 5));
                mb.TriRaw(a, P(t0) + Vector3.up * 0.02f, P(t1), col); mb.TriRaw(a, P(t1), d, col);
                mb.TriRaw(P(t0) + Vector3.up * 0.02f, b, c, col); mb.TriRaw(P(t0) + Vector3.up * 0.02f, c, P(t1), col);
            }
        }
        if (Chance(0.6f)) for (int c = 0; c < 3; c++) Blob(mb, crown + new Vector3(Rf(-0.3f, 0.3f), -0.25f, Rf(-0.3f, 0.3f)) * H * 0.15f, Vector3.one * H * 0.03f, 0.1f, 0, n => UVr(R.Dirt, 2, 4));
    });

    // Cupinzeiro: monte de terra irregular.
    static MB TermiteMound(float H, int seed) => WithSeed(seed, mb =>
    {
        var rings = Prism(Vector3.zero, 7, new[] { (-0.3f, H * 0.38f), (H * 0.35f, H * 0.3f), (H * 0.7f, H * 0.2f), (H * 0.95f, H * 0.08f) }, Rf(0, 6.28f), 0.08f, 0.18f);
        Loft(mb, rings, (i, j, n) => UVr(R.Dirt, 2 + i / 2, 5), Vector3.zero, Vector3.up);
        Fan(mb, Vector3.up * H, rings[rings.Count - 1], () => UVr(R.Dirt, 4, 6), Vector3.up);
        if (Chance(0.6f)) Blob(mb, new Vector3(H * 0.22f, H * 0.35f, 0), Vector3.one * H * 0.16f, 0.2f, 0, n => UVr(R.Dirt, 3, 5));
    });

    // Junco / taboa: hastes altas com ponta marrom.
    static MB Reeds(int seed) => WithSeed(seed, mb =>
    {
        int n = Ri(5, 8);
        for (int i = 0; i < n; i++)
        {
            var p = new Vector3(Rf(-0.35f, 0.35f), 0, Rf(-0.35f, 0.35f));
            float h = Rf(0.9f, 1.6f);
            var lean = new Vector3(Rf(-0.15f, 0.15f), 0, Rf(-0.15f, 0.15f));
            var side = new Vector3(0.03f, 0, 0.03f);
            mb.TriRaw(p - side + Vector3.down * 0.2f, p + side + Vector3.down * 0.2f, p + lean + Vector3.up * h, UVr(R.Fern, 2, 5));
            if (Chance(0.5f)) Blob(mb, p + lean * 0.85f + Vector3.up * h * 0.85f, new Vector3(0.05f, 0.14f, 0.05f), 0.05f, 0, nn => UVr(R.Dirt, 1, 3));
        }
    });

    // Vitória-régia: disco com borda e, às vezes, uma flor.
    static MB LilyPad(int seed) => WithSeed(seed, mb =>
    {
        float r = Rf(0.6f, 1.3f);
        int sides = 10;
        var rim = new Vector3[sides]; var top = new Vector3[sides];
        float notch = Rf(0, 6.28f);
        for (int j = 0; j < sides; j++)
        {
            float a = j * Mathf.PI * 2 / sides;
            float rr = r * (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, notch * Mathf.Rad2Deg)) < 18f ? 0.75f : 1f);
            top[j] = new Vector3(Mathf.Cos(a) * rr, 0.06f, Mathf.Sin(a) * rr);
            rim[j] = new Vector3(Mathf.Cos(a) * rr * 1.04f, 0.16f, Mathf.Sin(a) * rr * 1.04f);
        }
        Fan(mb, Vector3.up * 0.06f, top, () => UVr(R.PineMid, 3, 5), Vector3.up);
        Loft(mb, new List<Vector3[]> { top, rim }, (i, j, nn) => UVr(R.PineDark, 3, 5), Vector3.zero, Vector3.up);
        if (Chance(0.35f)) Blob(mb, new Vector3(r * 0.3f, 0.25f, 0), new Vector3(0.18f, 0.15f, 0.18f), 0.2f, 0, nn => UVr(R.FlowerWhite, 5, 7));
    });

    // Cristais: prismas pontudos (usa o material brilhante do tema).
    static MB Crystals(float size, int seed) => WithSeed(seed, mb =>
    {
        int n = Ri(3, 6);
        for (int i = 0; i < n; i++)
        {
            var dir = (Vector3.up + new Vector3(Rf(-0.6f, 0.6f), 0, Rf(-0.6f, 0.6f))).normalized;
            float len = size * Rf(0.5f, 1.2f), r = size * Rf(0.08f, 0.16f);
            var basePos = new Vector3(Rf(-0.2f, 0.2f), -0.1f, Rf(-0.2f, 0.2f)) * size;
            var side = Vector3.Cross(dir, Vector3.forward).normalized; var up = Vector3.Cross(side, dir);
            Vector3[] Ring(Vector3 c, float rr) { var ring = new Vector3[6]; for (int j = 0; j < 6; j++) { float a = j * Mathf.PI / 3; ring[j] = c + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * rr; } return ring; }
            var r0 = Ring(basePos, r); var r1 = Ring(basePos + dir * len * 0.78f, r);
            Loft(mb, new List<Vector3[]> { r0, r1 }, (a, b, nn) => UV(R.MushCap, 6 + (b % 2)), basePos, basePos + dir);
            Fan(mb, basePos + dir * len, r1, () => UV(R.MushCap, 7), dir);
        }
    });

    // Estalagmite (ou estalactite, se invertida).
    static MB Stalagmite(float H, int seed) => WithSeed(seed, mb =>
    {
        var rings = Prism(Vector3.zero, 7, new[] { (-0.5f, H * 0.2f), (H * 0.3f, H * 0.13f), (H * 0.65f, H * 0.07f), (H * 0.92f, H * 0.025f) }, Rf(0, 6.28f), 0.06f, 0.15f);
        Loft(mb, rings, (i, j, n) => UV(i % 2 == 0 ? R.Rock : R.RockMoss, Mathf.Clamp(2 + i + Ri(-1, 1), 0, 7)), Vector3.zero, Vector3.up);
        Fan(mb, Vector3.up * H, rings[rings.Count - 1], () => UVr(R.Rock, 4, 6), Vector3.up);
    });

    // Colunas de basalto hexagonais agrupadas.
    static MB BasaltColumns(float H, int seed) => WithSeed(seed, mb =>
    {
        int n = Ri(3, 7);
        for (int i = 0; i < n; i++)
        {
            var p = new Vector3(Rf(-1, 1), 0, Rf(-1, 1)) * H * 0.18f;
            float h = H * Rf(0.4f, 1f), r = H * Rf(0.07f, 0.11f);
            var rings = Prism(p, 6, new[] { (-0.5f, r), (h, r * 0.96f) }, 0.4f, 0f, 0.02f);
            Loft(mb, rings, (a, b, nn) => UV(R.Rock, 1 + (b % 3)), p, p + Vector3.up);
            Fan(mb, p + Vector3.up * (h + Rf(-0.05f, 0.05f) * H * 0.1f), rings[1], () => UVr(R.RockMoss, 2, 4), Vector3.up);
        }
    });

    // Pinheiro com neve: mesmo pinheiro, faces viradas para cima ficam brancas.
    static MB SnowPine(float H, int seed)
    {
        var mb = Pine(H, seed);
        // recolor: faces whose normal points up get the snow role
        var keep = rng; rng = new Random(seed * 7 + 3);
        for (int t = 0; t < mb.T.Count; t += 3)
        {
            var a = mb.V[mb.T[t]]; var b = mb.V[mb.T[t + 1]]; var c = mb.V[mb.T[t + 2]];
            var n = Vector3.Cross(b - a, c - a).normalized;
            float v = mb.U[mb.T[t]].y * Roles;
            bool foliage = v >= (int)R.PineDark && v < (int)R.LeafLight;
            if (foliage && n.y > 0.35f && Chance(0.8f))
            {
                var uv = UV(Chance(0.7f) ? R.GrassTop : R.GrassLight, Ri(4, 7));
                mb.U[mb.T[t]] = uv; mb.U[mb.T[t + 1]] = uv; mb.U[mb.T[t + 2]] = uv;
            }
        }
        rng = keep;
        return mb;
    }

    // Ilha flutuante decorativa: topo gramado e fundo de pedra afunilado.
    static MB FloatingIsland(float radius, int seed) => WithSeed(seed, mb =>
    {
        int sides = 9;
        float rot = Rf(0, 6.28f);
        var top = new Vector3[sides];
        for (int j = 0; j < sides; j++) { float a = rot + j * Mathf.PI * 2 / sides; float rr = radius * Rf(0.85f, 1.1f); top[j] = new Vector3(Mathf.Cos(a) * rr, Rf(-0.05f, 0.05f), Mathf.Sin(a) * rr); }
        Fan(mb, Vector3.up * 0.05f, top, () => Chance(0.2f) ? UVr(R.GrassLight, 3, 5) : UVr(R.GrassTop, 3, 6), Vector3.up);
        var rings = new List<Vector3[]> { top };
        float depth = radius * Rf(1.4f, 2.2f);
        int levels = 4;
        for (int k = 1; k <= levels; k++)
        {
            float t = k / (float)levels;
            var ring = new Vector3[sides];
            for (int j = 0; j < sides; j++) { var p = top[j] * (1f - t * 0.92f) * Rf(0.85f, 1.15f); p.y = -depth * Mathf.Pow(t, 0.8f) + Rf(-0.1f, 0.1f) * radius; ring[j] = p; }
            rings.Add(ring);
        }
        Loft(mb, rings, (i, j, n) => i == 0 ? UVr(R.Dirt, 2, 4) : (n.y > 0.2f ? UVr(R.RockMoss, 2, 4) : UVr(R.Rock, 1, 4)), Vector3.zero, Vector3.down);
        Fan(mb, Vector3.down * depth * 1.05f, rings[rings.Count - 1], () => UVr(R.Rock, 1, 3), Vector3.down);
    });
}
