using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

// Visuais das plataformas (nenhum tem colisor: o colisor original do bloco é mantido).
public static partial class ThemedLevelBuilder
{
    // Grade irregular do topo + contorno, usada pelos pilares, blocos flutuantes e tampos.
    static (Vector3[,] grid, List<Vector3> loop, List<Vector3> outDir, int nx, int nz) TopGrid(float hx, float hz, float margin)
    {
        float ex = hx + margin, ez = hz + margin;
        int nx = Mathf.Max(1, Mathf.RoundToInt(2f * ex / 1.15f)), nz = Mathf.Max(1, Mathf.RoundToInt(2f * ez / 1.15f));
        float cx = 2f * ex / nx, cz = 2f * ez / nz;
        float corner = Mathf.Min(0.35f, Mathf.Min(hx, hz) * 0.18f + 0.03f);
        var g = new Vector3[nx + 1, nz + 1];
        for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= nz; j++)
            {
                float x = -ex + i * cx, z = -ez + j * cz;
                bool bx = i == 0 || i == nx, bz = j == 0 || j == nz;
                if (!bx) x += Rf(-0.28f, 0.28f) * cx;
                if (!bz) z += Rf(-0.28f, 0.28f) * cz;
                if (bx && !bz) x += Mathf.Sign(x) * Rf(-0.03f, 0.06f) * Mathf.Min(1f, hx);
                if (bz && !bx) z += Mathf.Sign(z) * Rf(-0.03f, 0.06f) * Mathf.Min(1f, hz);
                if (bx && bz) { x -= Mathf.Sign(x) * corner; z -= Mathf.Sign(z) * corner; }
                g[i, j] = new Vector3(x, 0f, z);
            }
        var loop = new List<Vector3>(); var outDir = new List<Vector3>();
        void Add(int i, int j)
        {
            loop.Add(g[i, j]);
            float dx = i == 0 ? -1 : (i == nx ? 1 : 0), dz = j == 0 ? -1 : (j == nz ? 1 : 0);
            outDir.Add(new Vector3(dx, 0, dz).normalized);
        }
        for (int i = 0; i < nx; i++) Add(i, 0);
        for (int j = 0; j < nz; j++) Add(nx, j);
        for (int i = nx; i > 0; i--) Add(i, nz);
        for (int j = nz; j > 0; j--) Add(0, j);
        return (g, loop, outDir, nx, nz);
    }

    static void GridCap(MB mb, Vector3[,] g, int nx, int nz, Func<Vector2> col)
    {
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                var a = g[i, j]; var b = g[i + 1, j]; var c = g[i + 1, j + 1]; var d = g[i, j + 1];
                if (Chance(0.5f)) { mb.Tri(a, b, c, col(), Vector3.up); mb.Tri(a, c, d, col(), Vector3.up); }
                else { mb.Tri(a, b, d, col(), Vector3.up); mb.Tri(b, c, d, col(), Vector3.up); }
            }
    }

    // Pilar até o chão (floating = false) ou bloco flutuante afunilado (floating = true).
    // cap = false omite o topo (o bloco original fica visível em cima, ex.: esteira).
    static MB PlatformPillar(float hx, float hz, float depth, int seed, bool floating = false, bool cap = true, float topY = 0f)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        var (g, loop, outDir, nx, nz) = TopGrid(hx, hz, cap ? 0.1f : 0.02f);
        int n = loop.Count;
        if (cap) GridCap(mb, g, nx, nz, () => Chance(0.16f) ? UVr(R.GrassLight, 2, 4) : UVr(R.GrassTop, 3, 6));

        Vector3[] Ring(float expand, float y, float amp, float yJit, float shrink = 0f)
        {
            var r = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                var p = loop[k] * (1f - shrink) + outDir[k] * (expand + Rf(-amp * 0.35f, amp));
                p.y = topY + y + Rf(-yJit, yJit);
                r[k] = p;
            }
            return r;
        }
        var rings = new List<Vector3[]>();
        var bands = new List<int>();
        var top = loop.ToArray();
        for (int k = 0; k < n; k++) top[k].y = topY;
        rings.Add(top);
        if (cap)
        {
            rings.Add(Ring(0.1f, -0.22f, 0.07f, 0.04f)); bands.Add(0);
            rings.Add(Ring(-0.04f, -0.5f, 0.09f, 0.06f)); bands.Add(1);
            rings.Add(Ring(-0.12f, -1.1f, 0.16f, 0.12f)); bands.Add(2);
        }
        float y = cap ? -1.1f : 0f; int kk = 0; float bottom = -depth;
        float amp = Mathf.Min(hx, hz) * 0.08f + (floating ? 0.12f : 0.3f);
        float step = floating ? Mathf.Max(0.5f, depth / 4f) : 1.7f;
        while (y > bottom + 0.01f)
        {
            y = Mathf.Max(bottom, y - Rf(step * 0.8f, step * 1.2f));
            float t = -y / depth;
            float shrink = floating ? Mathf.Pow(t, 1.4f) * 0.92f : 0f;
            float expand = floating ? Rf(-0.05f, 0.05f) : Mathf.Lerp(-0.15f, -0.5f, t) + (kk % 2 == 0 ? 0.14f : -0.06f);
            if (!floating && y <= bottom + 0.01f) expand += 0.6f;
            rings.Add(Ring(expand, y, floating ? amp * (1f - t) : amp, floating ? 0.08f : 0.25f, shrink));
            bands.Add(3 + (kk % 3));
            kk++;
        }
        Loft(mb, rings, (i, j, nrm) =>
        {
            int b = bands[i];
            switch (b)
            {
                case 0: return UVr(R.GrassTop, 2, 4);
                case 1: return UVr(R.Fern, 1, 2);
                case 2: return UVr(R.Dirt, 2, 5);
                default:
                    if (nrm.y > 0.3f || Chance(0.14f)) return UVr(R.RockMoss, 2, 5);
                    return UV(R.Rock, Mathf.Clamp(Ri(1, 5) + (b - 4), 0, 7));
            }
        }, Vector3.up * topY, Vector3.up * (topY - 1f));
        if (floating) Fan(mb, new Vector3(Rf(-0.1f, 0.1f), topY - depth - Mathf.Min(hx, hz) * 0.25f, Rf(-0.1f, 0.1f)), rings[rings.Count - 1], () => UVr(R.Rock, 1, 3), Vector3.down);
        rng = keep;
        return mb;
    }

    static MB StumpPillar(float radius, float depth, int seed)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        int sides = 12;
        var rings = new List<Vector3[]>();
        var yr = new List<(float y, float r, bool roots)> { (0f, radius, false), (-0.25f, radius * 1.05f, false) };
        float y = -0.25f;
        while (y > -depth + 3.5f) { y -= Rf(1.6f, 2.4f); yr.Add((y, radius * Rf(0.95f, 1.04f), false)); }
        if (depth > 4f)
        {
            yr.Add((-depth + 3f, radius * 1.12f, false));
            yr.Add((-depth + 1.5f, radius * 1.4f, true));
        }
        yr.Add((-depth, radius * (depth > 4f ? 1.9f : 1.1f), depth > 4f));
        float rot = Rf(0, 6.28f);
        foreach (var e in yr)
        {
            var ring = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                float a = rot + j * Mathf.PI * 2 / sides;
                float rr = e.r * Rf(0.96f, 1.04f) * (e.roots && j % 3 == 0 ? 1.35f : 1f);
                ring[j] = new Vector3(Mathf.Cos(a) * rr, e.y, Mathf.Sin(a) * rr);
            }
            rings.Add(ring);
        }
        Loft(mb, rings, (i, j, n) => i == 0 ? UVr(R.Bark, 4, 6) : UV(R.Bark, (j % 2 == 0 ? 4 : 2) + Ri(0, 1)), Vector3.zero, Vector3.down);
        float[] fr = { 1f, 0.82f, 0.62f, 0.42f, 0.22f };
        var cap = fr.Select(f => rings[0].Select(p => new Vector3(p.x * f, 0f, p.z * f)).ToArray()).ToList();
        for (int c = 0; c < cap.Count - 1; c++)
        {
            int shade = c == 0 ? 3 : (c % 2 == 0 ? 6 : 4);
            for (int j = 0; j < sides; j++)
            {
                int jj = (j + 1) % sides;
                mb.Tri(cap[c][j], cap[c][jj], cap[c + 1][jj], UV(R.WoodRing, shade), Vector3.up);
                mb.Tri(cap[c][j], cap[c + 1][jj], cap[c + 1][j], UV(R.WoodRing, shade), Vector3.up);
            }
        }
        Fan(mb, Vector3.zero, cap[cap.Count - 1], () => UV(R.WoodRing, 7), Vector3.up);
        rng = keep;
        return mb;
    }

    // Coluna de pedra hexagonal (basalto, arenito, gelo, cristal...) com topo da cor do chão.
    static MB StoneColumn(float radius, float depth, int seed, bool floating)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        int sides = 6;
        float rot = Rf(0, 1f);
        var prof = new List<(float y, float r)> { (0f, radius), (-0.18f, radius * 1.04f) };
        float y = -0.18f;
        if (floating)
        {
            float d = Mathf.Max(1.2f, radius * 3f);
            prof.Add((-d * 0.5f, radius * 0.8f)); prof.Add((-d, radius * 0.25f));
        }
        else
        {
            while (y > -depth + 2f) { y -= Rf(1.5f, 2.5f); prof.Add((y, radius * Rf(0.93f, 1.05f))); }
            prof.Add((-depth, radius * 1.35f));
        }
        var rings = Prism(Vector3.zero, sides, prof.ToArray(), rot, 0f, 0.03f);
        Loft(mb, rings, (i, j, n) => i == 0 ? UVr(R.RockMoss, 3, 5) : UV(R.Rock, 1 + (j + i) % 4), Vector3.zero, Vector3.down);
        var top = rings[0].Select(p => new Vector3(p.x, 0f, p.z)).ToArray();
        Fan(mb, Vector3.up * 0.01f, top, () => Chance(0.2f) ? UVr(R.GrassLight, 3, 5) : UVr(R.GrassTop, 3, 6), Vector3.up);
        if (floating) Fan(mb, Vector3.down * (Mathf.Max(1.2f, radius * 3f) + radius * 0.3f), rings[rings.Count - 1], () => UVr(R.Rock, 1, 3), Vector3.down);
        rng = keep;
        return mb;
    }

    static void AddBox(MB mb, Vector3 c, Vector3 s, Vector2 top, Vector2 side)
    {
        Vector3 h = s * 0.5f;
        Vector3[] p =
        {
            c + new Vector3(-h.x, -h.y, -h.z), c + new Vector3(h.x, -h.y, -h.z), c + new Vector3(h.x, -h.y, h.z), c + new Vector3(-h.x, -h.y, h.z),
            c + new Vector3(-h.x, h.y, -h.z), c + new Vector3(h.x, h.y, -h.z), c + new Vector3(h.x, h.y, h.z), c + new Vector3(-h.x, h.y, h.z),
        };
        mb.Quad(p[4], p[5], p[6], p[7], top, Vector3.up);
        mb.Quad(p[0], p[1], p[5], p[4], side, Vector3.back);
        mb.Quad(p[1], p[2], p[6], p[5], side, Vector3.right);
        mb.Quad(p[2], p[3], p[7], p[6], side, Vector3.forward);
        mb.Quad(p[3], p[0], p[4], p[7], side, Vector3.left);
        mb.Quad(p[0], p[3], p[2], p[1], side, Vector3.down);
    }

    // Ponte de tábuas no comprimento do bloco (vigas longas ou estreitas).
    static MB PlankBridge(float sx, float sz, int seed) => WithSeed(seed, mb =>
    {
        bool alongZ = sz >= sx;
        float len = alongZ ? sz : sx, wid = alongZ ? sx : sz;
        float pitch = 0.42f;
        int count = Mathf.Max(1, Mathf.RoundToInt(len / pitch));
        float realPitch = len / count;
        for (int i = 0; i < count; i++)
        {
            float t = -len * 0.5f + (i + 0.5f) * realPitch;
            float yJ = Rf(-0.02f, 0.02f);
            var size = alongZ ? new Vector3(wid + 0.08f, 0.16f, realPitch * 0.86f) : new Vector3(realPitch * 0.86f, 0.16f, wid + 0.08f);
            var c = alongZ ? new Vector3(Rf(-0.02f, 0.02f), -0.08f + yJ, t) : new Vector3(t, -0.08f + yJ, Rf(-0.02f, 0.02f));
            AddBox(mb, c, size, UV(R.WoodRing, Ri(2, 5)), UV(R.Bark, Ri(3, 5)));
        }
        foreach (float side in new[] { -1f, 1f })
        {
            float off = (wid * 0.5f - Mathf.Min(0.12f, wid * 0.2f)) * side;
            var rail = alongZ ? new Vector3(Mathf.Min(0.12f, wid * 0.3f), 0.14f, len) : new Vector3(len, 0.14f, Mathf.Min(0.12f, wid * 0.3f));
            AddBox(mb, alongZ ? new Vector3(off, -0.22f, 0) : new Vector3(0, -0.22f, off), rail, UV(R.Bark, 2), UV(R.Bark, 2));
        }
    });

    // Tampo colorido das mecânicas (usa o material da mecânica, não o atlas).
    static MB SlabCap(float hx, float hz, int seed) => WithSeed(seed, mb =>
    {
        var (g, loop, outDir, nx, nz) = TopGrid(hx, hz, 0.03f);
        GridCap(mb, g, nx, nz, () => UV(R.GrassTop, 4));
        var top = loop.ToArray();
        var low = new Vector3[loop.Count];
        for (int k = 0; k < loop.Count; k++) low[k] = loop[k] * 0.97f + Vector3.down * 0.42f;
        Loft(mb, new List<Vector3[]> { top, low }, (i, j, nrm) => UV(R.GrassTop, 3), Vector3.zero, Vector3.down);
    });
}
