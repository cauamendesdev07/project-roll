using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = System.Random;

// Gerador do cenário de floresta low-poly do Project Roll.
// Uso: abra a cena de uma fase e rode "Tools/Project Roll/Cenário de Floresta/Gerar na cena aberta".
// O gerador não altera a jogabilidade: mantém os colisores das plataformas "Platform_*" e só troca o visual.
// Rodar de novo na mesma cena substitui o cenário anterior (o resultado é determinístico por fase).
//
// Art bible — "Forest Gorge"
// Style:   Low-poly storybook forest: faceted flat-shaded rock, moss and pine; everything in frame shares one palette atlas.
// Palette: moss #6FA34A · pine #2E5B3F · bark #6A4A35 · rock #868A80 · river #3E8E9A · mist #C3D4C8 · accent amber #F2B134
// Mood:    warm late-afternoon sun slicing across the gorge, cool blue-green mist in the depths, lantern glow at the goal.
public static class ForestLevelBuilder
{
    const string ArtRoot = "Assets/Art/Forest";
    const string EnvName = "Forest_Environment";
    const float WaterY = -12.4f;
    const float PillarBottom = -13.9f;

    static Random rng = new Random(1234);
    static float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static int Ri(int a, int bInclusive) => rng.Next(a, bInclusive + 1);
    static bool Chance(float p) => rng.NextDouble() < p;

    // ---------------------------------------------------------------- palette (single source)
    enum R { GrassTop, GrassLight, PineDark, PineMid, LeafLight, Bark, WoodRing, Rock, RockMoss, Dirt, RiverBed, MushCap, Cream, FlowerYellow, FlowerWhite, Fern }
    static readonly string[] AtlasHex =
    {
        "#6FA34A", "#9CC25B", "#2E5B3F", "#3F7A4A", "#7BA445", "#6A4A35", "#C4955E", "#868A80",
        "#6E8A5A", "#7B5A3E", "#5C6B66", "#C2553A", "#EADCC0", "#F2C14E", "#F1EEDC", "#4E7F3E",
    };
    const int Shades = 8;
    const int Roles = 16;

    static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    static readonly Color SkyZenith = Hex("#4A86C5");
    static readonly Color Horizon = Hex("#C9DCD6");
    static readonly Color WaterCol = Hex("#3E8E9A");
    static readonly Color CascadeCol = Hex("#B9E0DA");
    static readonly Color SunCol = Hex("#FFE2B0");
    static readonly Color AmbSky = Hex("#A9C6D8");
    static readonly Color AmbEquator = Hex("#8CA88A");
    static readonly Color AmbGround = Hex("#4A3F31");
    static readonly Color Accent = Hex("#F2B134");
    static readonly Color Mist = Hex("#D5E2D8");
    static readonly Color Pollen = Hex("#FFF1C4");
    static readonly Color CloudCol = Hex("#F6F0E4");

    static Vector2 UV(R r, int shade)
    {
        shade = Mathf.Clamp(shade, 0, Shades - 1);
        return new Vector2((shade + 0.5f) / Shades, ((int)r + 0.5f) / Roles);
    }
    static Vector2 UVr(R r, int lo, int hi) => UV(r, Ri(lo, hi));

    // ---------------------------------------------------------------- mesh builder (flat shaded)
    class MB
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector2> U = new List<Vector2>();
        public readonly List<int> T = new List<int>();

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 uv, Vector3 hint)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), hint) < 0) { var t = b; b = c; c = t; }
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            U.Add(uv); U.Add(uv); U.Add(uv);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
        }
        public void TriRaw(Vector3 a, Vector3 b, Vector3 c, Vector2 uv)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            U.Add(uv); U.Add(uv); U.Add(uv);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv, Vector3 hint)
        {
            Tri(a, b, c, uv, hint);
            Tri(a, c, d, uv, hint);
        }
        public void Append(MB o, Matrix4x4 m)
        {
            int b = V.Count;
            foreach (var v in o.V) V.Add(m.MultiplyPoint3x4(v));
            U.AddRange(o.U);
            foreach (var t in o.T) T.Add(b + t);
        }
        public Mesh Build(string name, bool tangents = false)
        {
            var m = new Mesh { name = name };
            m.indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.SetVertices(V);
            m.SetUVs(0, U);
            m.SetTriangles(T, 0);
            m.RecalculateNormals();
            if (tangents) { m.RecalculateTangents(); m.RecalculateBounds(); return m; }
            var nrm = m.normals;
            var packed = new PackedVertex[V.Count];
            for (int i = 0; i < V.Count; i++)
            {
                packed[i] = new PackedVertex
                {
                    p = V[i],
                    nx = Mathf.FloatToHalf(nrm[i].x), ny = Mathf.FloatToHalf(nrm[i].y), nz = Mathf.FloatToHalf(nrm[i].z), nw = 0,
                    u = Mathf.FloatToHalf(U[i].x), v = Mathf.FloatToHalf(U[i].y),
                };
            }
            var outMesh = new Mesh { name = name };
            outMesh.SetVertexBufferParams(V.Count,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2));
            outMesh.SetVertexBufferData(packed, 0, 0, packed.Length);
            if (V.Count > 65000)
            {
                outMesh.SetIndexBufferParams(T.Count, IndexFormat.UInt32);
                outMesh.SetIndexBufferData(T, 0, 0, T.Count);
            }
            else
            {
                var idx16 = T.Select(t => (ushort)t).ToArray();
                outMesh.SetIndexBufferParams(T.Count, IndexFormat.UInt16);
                outMesh.SetIndexBufferData(idx16, 0, 0, idx16.Length);
            }
            outMesh.subMeshCount = 1;
            outMesh.SetSubMesh(0, new SubMeshDescriptor(0, T.Count));
            outMesh.RecalculateBounds();
            Object.DestroyImmediate(m);
            return outMesh;
        }
    }

    struct PackedVertex
    {
        public Vector3 p;
        public ushort nx, ny, nz, nw;
        public ushort u, v;
    }

    static Vector3 ClosestOnLine(Vector3 a, Vector3 b, Vector3 p)
    {
        var d = (b - a).normalized;
        return a + d * Vector3.Dot(p - a, d);
    }

    static void Loft(MB mb, List<Vector3[]> rings, Func<int, int, Vector3, Vector2> col, Vector3 axisA, Vector3 axisB)
    {
        for (int i = 0; i < rings.Count - 1; i++)
        {
            var r0 = rings[i]; var r1 = rings[i + 1]; int n = r0.Length;
            for (int j = 0; j < n; j++)
            {
                int k = (j + 1) % n;
                var a = r0[j]; var b = r0[k]; var c = r1[k]; var d = r1[j];
                var cen = (a + b + c + d) * 0.25f;
                var hint = cen - ClosestOnLine(axisA, axisB, cen);
                var nrm = Vector3.Cross(b - a, d - a).normalized;
                if (Vector3.Dot(nrm, hint) < 0) nrm = -nrm;
                mb.Quad(a, b, c, d, col(i, j, nrm), hint);
            }
        }
    }

    static void Fan(MB mb, Vector3 center, Vector3[] ring, Func<Vector2> col, Vector3 hint)
    {
        for (int j = 0; j < ring.Length; j++)
            mb.Tri(center, ring[j], ring[(j + 1) % ring.Length], col(), hint);
    }

    // ---------------------------------------------------------------- icosphere
    static (List<Vector3> v, List<int[]> f) Ico(int subdiv)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
        var f = new List<int[]>
        {
            new[] {0, 11, 5}, new[] {0, 5, 1}, new[] {0, 1, 7}, new[] {0, 7, 10}, new[] {0, 10, 11},
            new[] {1, 5, 9}, new[] {5, 11, 4}, new[] {11, 10, 2}, new[] {10, 7, 6}, new[] {7, 1, 8},
            new[] {3, 9, 4}, new[] {3, 4, 2}, new[] {3, 2, 6}, new[] {3, 6, 8}, new[] {3, 8, 9},
            new[] {4, 9, 5}, new[] {2, 4, 11}, new[] {6, 2, 10}, new[] {8, 6, 7}, new[] {9, 8, 1},
        };
        for (int s = 0; s < subdiv; s++)
        {
            var cache = new Dictionary<long, int>();
            var nf = new List<int[]>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int m)) return m;
                v.Add(((v[a] + v[b]) * 0.5f).normalized);
                cache[key] = v.Count - 1;
                return v.Count - 1;
            }
            foreach (var tri in f)
            {
                int a = Mid(tri[0], tri[1]), b = Mid(tri[1], tri[2]), c = Mid(tri[2], tri[0]);
                nf.Add(new[] { tri[0], a, c }); nf.Add(new[] { tri[1], b, a });
                nf.Add(new[] { tri[2], c, b }); nf.Add(new[] { a, b, c });
            }
            f = nf;
        }
        return (v, f);
    }

    static void Blob(MB mb, Vector3 center, Vector3 scale, float jitter, int subdiv, Func<Vector3, Vector2> col)
    {
        var (v, f) = Ico(subdiv);
        var pv = v.Select(p => center + Vector3.Scale(p * (1f + Rf(-jitter, jitter)), scale)).ToList();
        foreach (var tri in f)
        {
            var a = pv[tri[0]]; var b = pv[tri[1]]; var c = pv[tri[2]];
            var cen = (a + b + c) / 3f;
            var hint = cen - center;
            var n = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(n, hint) < 0) n = -n;
            mb.Tri(a, b, c, col(n), hint);
        }
    }

    // ---------------------------------------------------------------- props
    static void Trunk(MB mb, float h, float r0, float r1, int sides, Vector3 basePos, float lean)
    {
        var rings = new List<Vector3[]>();
        float[] ys = { -0.5f, h * 0.15f, h * 0.55f, h };
        float[] rs = { r0 * 1.35f, r0, Mathf.Lerp(r0, r1, 0.55f), r1 };
        float rot = Rf(0, 6.28f);
        for (int i = 0; i < ys.Length; i++)
        {
            var ring = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                float a = rot + j * Mathf.PI * 2 / sides;
                float rr = rs[i] * Rf(0.9f, 1.1f) * (i == 0 && j % 2 == 0 ? 1.25f : 1f);
                ring[j] = basePos + new Vector3(Mathf.Cos(a) * rr + lean * ys[i], ys[i], Mathf.Sin(a) * rr);
            }
            rings.Add(ring);
        }
        Loft(mb, rings, (i, j, n) => UVr(R.Bark, 1, 4), basePos, basePos + Vector3.up);
        var top = rings[rings.Count - 1];
        Fan(mb, basePos + new Vector3(lean * h, h + r1 * 0.5f, 0), top, () => UV(R.Bark, 2), Vector3.up);
    }

    static MB Pine(float H, int seed)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        float tr = H * 0.036f;
        Trunk(mb, H * 0.34f, tr * 1.2f, tr * 0.8f, 6, Vector3.zero, 0f);
        int tiers = 4;
        for (int t = 0; t < tiers; t++)
        {
            float f = t / (float)(tiers - 1);
            float baseY = H * (0.2f + 0.2f * t) + Rf(-0.02f, 0.02f) * H;
            float rad = H * Mathf.Lerp(0.27f, 0.11f, f) * Rf(0.9f, 1.1f);
            float apex = baseY + H * Mathf.Lerp(0.34f, 0.3f, f);
            if (t == tiers - 1) apex = Mathf.Max(apex, H);
            int n = 10;
            var ring = new Vector3[n];
            float rot = Rf(0, 6.28f);
            for (int j = 0; j < n; j++)
            {
                float a = rot + j * Mathf.PI * 2 / n;
                bool outer = j % 2 == 0;
                float rr = rad * (outer ? 1f : 0.74f) * Rf(0.9f, 1.1f);
                ring[j] = new Vector3(Mathf.Cos(a) * rr, baseY + Rf(-0.04f, 0.04f) * rad - (outer ? 0.14f * rad : 0f), Mathf.Sin(a) * rr);
            }
            var ap = new Vector3(Rf(-0.03f, 0.03f) * rad, apex, Rf(-0.03f, 0.03f) * rad);
            var under = new Vector3(0, baseY + rad * 0.25f, 0);
            for (int j = 0; j < n; j++)
            {
                int k = (j + 1) % n;
                var cen = (ap + ring[j] + ring[k]) / 3f;
                var hint = new Vector3(cen.x, 0, cen.z) + Vector3.up * 0.35f * rad;
                R role = Chance(0.55f) ? R.PineDark : R.PineMid;
                int sh = Mathf.Clamp(2 + t + Ri(-1, 2), 0, 7);
                mb.Tri(ap, ring[j], ring[k], UV(role, sh), hint);
                mb.Tri(under, ring[k], ring[j], UV(R.PineDark, 1), Vector3.down);
            }
        }
        rng = keep;
        return mb;
    }

    static MB RoundTree(float H, int seed)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        float lean = Rf(-0.05f, 0.05f);
        Trunk(mb, H * 0.58f, H * 0.05f, H * 0.03f, 6, Vector3.zero, lean);
        int blobs = Ri(3, 5);
        var top = new Vector3(lean * H * 0.58f, H * 0.7f, 0);
        for (int b = 0; b < blobs; b++)
        {
            var off = b == 0 ? Vector3.zero : new Vector3(Rf(-1, 1), Rf(-0.4f, 0.5f), Rf(-1, 1)).normalized * H * Rf(0.15f, 0.22f);
            float s = H * Rf(0.2f, 0.27f) * (b == 0 ? 1.2f : 1f);
            bool warm = Chance(0.6f);
            Blob(mb, top + off, new Vector3(s, s * Rf(0.8f, 0.95f), s), 0.18f, 1, n =>
            {
                int sh = Mathf.Clamp(Mathf.RoundToInt(2 + (n.y + 1) * 1.8f + Rf(-1, 1)), 0, 7);
                return UV(warm ? R.LeafLight : R.GrassTop, sh);
            });
        }
        rng = keep;
        return mb;
    }

    static MB RockMesh(int seed, Vector3 scale)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        Blob(mb, Vector3.zero, scale, 0.24f, 1, n => n.y > 0.55f
            ? UVr(R.RockMoss, 2, 6)
            : UV(R.Rock, Mathf.Clamp(Mathf.RoundToInt(3 + n.y * 2.5f + Rf(-1, 1)), 0, 7)));
        rng = keep;
        return mb;
    }

    static MB RockMeshLow(int seed, Vector3 scale)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        Blob(mb, Vector3.zero, scale, 0.2f, 0, n => n.y > 0.55f
            ? UVr(R.RockMoss, 2, 6)
            : UV(R.Rock, Mathf.Clamp(Mathf.RoundToInt(3 + n.y * 2.5f + Rf(-1, 1)), 0, 7)));
        rng = keep;
        return mb;
    }

    static MB Bush(int seed, float size)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        int blobs = Ri(2, 3);
        for (int b = 0; b < blobs; b++)
        {
            var off = new Vector3(Rf(-0.5f, 0.5f), 0, Rf(-0.5f, 0.5f)) * size;
            float s = size * Rf(0.45f, 0.6f);
            bool fern = Chance(0.5f);
            Blob(mb, off + Vector3.up * s * 0.45f, new Vector3(s, s * 0.75f, s), 0.2f, 0, n =>
                UV(fern ? R.Fern : R.GrassTop, Mathf.Clamp(Mathf.RoundToInt(1 + (n.y + 1) * 1.6f + Rf(-0.7f, 0.7f)), 0, 7)));
        }
        rng = keep;
        return mb;
    }

    static MB Tuft(int seed, float size)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        int blades = Ri(4, 5);
        for (int i = 0; i < blades; i++)
        {
            float a = Rf(0, 6.28f);
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var basePos = dir * Rf(0f, 0.1f) * size;
            float h = Rf(0.26f, 0.5f) * size;
            float w = Rf(0.04f, 0.06f) * size;
            var side = new Vector3(-dir.z, 0, dir.x) * w;
            var lean = dir * Rf(0.05f, 0.16f) * size;
            var b0 = basePos - side + Vector3.down * 0.06f;
            var b1 = basePos + side + Vector3.down * 0.06f;
            var m0 = basePos - side * 0.6f + lean * 0.4f + Vector3.up * h * 0.55f;
            var m1 = basePos + side * 0.6f + lean * 0.4f + Vector3.up * h * 0.55f;
            var tip = basePos + lean + Vector3.up * h;
            var c1 = UVr(R.GrassTop, 2, 4);
            mb.TriRaw(b0, b1, m1, c1);
            mb.TriRaw(b0, m1, m0, c1);
            mb.TriRaw(m0, m1, tip, UVr(R.GrassLight, 4, 7));
        }
        rng = keep;
        return mb;
    }

    static MB Flowers(int seed)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        int n = Ri(3, 5);
        R petal = Chance(0.55f) ? R.FlowerYellow : R.FlowerWhite;
        for (int i = 0; i < n; i++)
        {
            var pos = new Vector3(Rf(-0.25f, 0.25f), 0, Rf(-0.25f, 0.25f));
            float h = Rf(0.18f, 0.34f);
            var side = new Vector3(0.012f, 0, 0.012f);
            var head = pos + Vector3.up * h;
            mb.TriRaw(pos - side + Vector3.down * 0.05f, pos + side + Vector3.down * 0.05f, head, UV(R.Fern, 3));
            int p = 5;
            float rot = Rf(0, 6.28f);
            var ring = new Vector3[p];
            for (int j = 0; j < p; j++)
            {
                float a = rot + j * Mathf.PI * 2 / p;
                ring[j] = head + new Vector3(Mathf.Cos(a) * 0.075f, Rf(0.0f, 0.025f), Mathf.Sin(a) * 0.075f);
            }
            for (int j = 0; j < p; j++)
                mb.TriRaw(head + Vector3.up * 0.015f, ring[j], ring[(j + 1) % p], UVr(petal, 4, 7));
        }
        rng = keep;
        return mb;
    }

    static MB Mushrooms(int seed, float size = 1f)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        int n = Ri(1, 3);
        for (int i = 0; i < n; i++)
        {
            var pos = new Vector3(Rf(-0.2f, 0.2f), 0, Rf(-0.2f, 0.2f)) * size;
            float sh = Rf(0.12f, 0.26f) * size, sr = Rf(0.03f, 0.05f) * size, cr = Rf(0.09f, 0.16f) * size;
            int sides = 6;
            var rings = new List<Vector3[]>();
            foreach (var (y, r) in new[] { (-0.05f, sr * 1.15f), (sh, sr) })
            {
                var ring = new Vector3[sides];
                for (int j = 0; j < sides; j++) { float a = j * Mathf.PI * 2 / sides; ring[j] = pos + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
                rings.Add(ring);
            }
            Loft(mb, rings, (a, b, nn) => UVr(R.Cream, 3, 5), pos, pos + Vector3.up);
            int cs = 7;
            var cap = new Vector3[cs];
            float tilt = Rf(-0.02f, 0.02f);
            for (int j = 0; j < cs; j++) { float a = j * Mathf.PI * 2 / cs; cap[j] = pos + new Vector3(Mathf.Cos(a) * cr, sh - 0.02f * size + Mathf.Cos(a) * tilt, Mathf.Sin(a) * cr); }
            var apex = pos + Vector3.up * (sh + cr * 0.65f);
            for (int j = 0; j < cs; j++)
            {
                int k = (j + 1) % cs;
                var cen = (apex + cap[j] + cap[k]) / 3f;
                mb.Tri(apex, cap[j], cap[k], UVr(R.MushCap, 3, 6), cen - (pos + Vector3.up * sh) + Vector3.up * 0.01f);
                mb.Tri(pos + Vector3.up * (sh + 0.01f * size), cap[k], cap[j], UV(R.Cream, 2), Vector3.down);
            }
        }
        rng = keep;
        return mb;
    }

    static MB Log(int seed, float len, float rad)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        int sides = 8;
        float[] xs = { -len / 2, -len / 6, len / 6, len / 2 };
        var rings = new List<Vector3[]>();
        float rot = Rf(0, 6.28f);
        foreach (var x in xs)
        {
            var ring = new Vector3[sides];
            float r = rad * Rf(0.9f, 1.08f);
            for (int j = 0; j < sides; j++)
            {
                float a = rot + j * Mathf.PI * 2 / sides;
                float rr = r * Rf(0.92f, 1.08f);
                ring[j] = new Vector3(x, Mathf.Cos(a) * rr, Mathf.Sin(a) * rr);
            }
            rings.Add(ring);
        }
        Loft(mb, rings, (i, j, n) => n.y > 0.55f && Chance(0.7f) ? UVr(R.GrassTop, 1, 3) : UVr(R.Bark, 1, 4), Vector3.zero, Vector3.right);
        Fan(mb, new Vector3(-len / 2 - 0.02f, 0, 0), rings[0], () => UVr(R.WoodRing, 3, 5), Vector3.left);
        Fan(mb, new Vector3(len / 2 + 0.02f, 0, 0), rings[rings.Count - 1], () => UVr(R.WoodRing, 3, 5), Vector3.right);
        rng = keep;
        return mb;
    }

    // ---------------------------------------------------------------- platforms
    static Vector2 RectPerim(float hx, float hz, float ch, float s)
    {
        var poly = new[]
        {
            new Vector2(hx, -hz + ch), new Vector2(hx, hz - ch), new Vector2(hx - ch, hz), new Vector2(-hx + ch, hz),
            new Vector2(-hx, hz - ch), new Vector2(-hx, -hz + ch), new Vector2(-hx + ch, -hz), new Vector2(hx - ch, -hz),
        };
        float total = 0; var seg = new float[8];
        for (int i = 0; i < 8; i++) { seg[i] = (poly[(i + 1) % 8] - poly[i]).magnitude; total += seg[i]; }
        float d = s * total;
        for (int i = 0; i < 8; i++)
        {
            if (d <= seg[i]) return Vector2.Lerp(poly[i], poly[(i + 1) % 8], seg[i] > 0 ? d / seg[i] : 0);
            d -= seg[i];
        }
        return poly[0];
    }

    static MB PlatformPillar(float hx, float hz, float depth, int seed)
    {
        var keep = rng; rng = new Random(seed);
        var mb = new MB();
        float ex = hx + 0.1f, ez = hz + 0.1f;
        int nx = Mathf.Max(2, Mathf.RoundToInt(2f * ex / 1.15f)), nz = Mathf.Max(2, Mathf.RoundToInt(2f * ez / 1.15f));
        float cx = 2f * ex / nx, cz = 2f * ez / nz;
        float corner = Mathf.Min(0.35f, Mathf.Min(hx, hz) * 0.18f + 0.1f);
        var g = new Vector3[nx + 1, nz + 1];
        for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= nz; j++)
            {
                float x = -ex + i * cx, z = -ez + j * cz;
                bool bx = i == 0 || i == nx, bz = j == 0 || j == nz;
                if (!bx) x += Rf(-0.28f, 0.28f) * cx;
                if (!bz) z += Rf(-0.28f, 0.28f) * cz;
                if (bx && !bz) x += Mathf.Sign(x) * Rf(-0.03f, 0.09f);
                if (bz && !bx) z += Mathf.Sign(z) * Rf(-0.03f, 0.09f);
                if (bx && bz) { x -= Mathf.Sign(x) * corner; z -= Mathf.Sign(z) * corner; }
                g[i, j] = new Vector3(x, 0f, z);
            }
        Vector2 CapCol() => Chance(0.16f) ? UVr(R.GrassLight, 2, 4) : UVr(R.GrassTop, 3, 6);
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                var a = g[i, j]; var b = g[i + 1, j]; var c = g[i + 1, j + 1]; var d = g[i, j + 1];
                if (Chance(0.5f)) { mb.Tri(a, b, c, CapCol(), Vector3.up); mb.Tri(a, c, d, CapCol(), Vector3.up); }
                else { mb.Tri(a, b, d, CapCol(), Vector3.up); mb.Tri(b, c, d, CapCol(), Vector3.up); }
            }

        // boundary loop + outward directions
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
        int n = loop.Count;

        Vector3[] Ring(float expand, float y, float amp, float yJit)
        {
            var r = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                var p = loop[k] + outDir[k] * (expand + Rf(-amp * 0.35f, amp));
                p.y = y + Rf(-yJit, yJit);
                r[k] = p;
            }
            return r;
        }
        var rings = new List<Vector3[]>();
        var bands = new List<int>();
        rings.Add(loop.ToArray());
        rings.Add(Ring(0.1f, -0.22f, 0.07f, 0.04f)); bands.Add(0);
        rings.Add(Ring(-0.04f, -0.5f, 0.09f, 0.06f)); bands.Add(1);
        rings.Add(Ring(-0.12f, -1.1f, 0.16f, 0.12f)); bands.Add(2);
        float y = -1.1f; int kk = 0; float bottom = -depth;
        float amp = Mathf.Min(hx, hz) * 0.08f + 0.3f;
        while (y > bottom + 0.01f)
        {
            y = Mathf.Max(bottom, y - Rf(1.3f, 2.1f));
            float t = -y / depth;
            float expand = Mathf.Lerp(-0.15f, -0.5f, t) + (kk % 2 == 0 ? 0.14f : -0.06f);
            if (y <= bottom + 0.01f) expand += 0.6f;
            rings.Add(Ring(expand, y, amp, 0.25f));
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
        }, Vector3.zero, Vector3.down);
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
        yr.Add((-depth + 3f, radius * 1.12f, false));
        yr.Add((-depth + 1.5f, radius * 1.4f, true));
        yr.Add((-depth, radius * 1.9f, true));
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
        Loft(mb, rings, (i, j, n) =>
        {
            if (i == 0) return UVr(R.Bark, 4, 6);
            return UV(R.Bark, (j % 2 == 0 ? 4 : 2) + Ri(0, 1));
        }, Vector3.zero, Vector3.down);
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

    // ---------------------------------------------------------------- terrain
    struct Edge { public Vector2 a, b; public float wa, wb; }
    static readonly List<Edge> edges = new List<Edge>();
    static readonly List<Vector2> mainPath = new List<Vector2>();   // Start -> Finish through the platform tree
    static Vector2 riverEnd, riverEndDir;

    static float PathDist(float x, float z)
    {
        var p = new Vector2(x, z);
        float best = float.MaxValue;
        foreach (var e in edges)
        {
            var ab = e.b - e.a;
            float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(p - e.a, ab) / ab.sqrMagnitude) : 0f;
            float d = (p - (e.a + ab * t)).magnitude - Mathf.Lerp(e.wa, e.wb, t);
            if (d < best) best = d;
        }
        return best;
    }

    static Vector2 RotateTowardX(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), sn = Mathf.Sin(r);
        return new Vector2(v.x * c + v.y * sn, -v.x * sn + v.y * c);
    }

    static Edge RandomEdge() => edges[Ri(0, edges.Count - 1)];

    static bool InCascade(Vector2 p)
    {
        var e = riverEnd;
        var dir = riverEndDir;
        var rel = p - e;
        float along = Vector2.Dot(rel, dir);
        float across = Mathf.Abs(Vector2.Dot(rel, new Vector2(-dir.y, dir.x)));
        return along > -4f && along < 50f && across < 8f;
    }

    static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

    static float TerrainH(float x, float z)
    {
        float d = PathDist(x, z);
        float n1 = Mathf.PerlinNoise(x * 0.035f + 100f, z * 0.035f + 100f);
        float n2 = Mathf.PerlinNoise(x * 0.12f + 37f, z * 0.12f + 71f);
        float floorH = -13.4f + n2 * 0.7f;
        if (d < 3f) return floorH - 0.4f;
        if (d < 11f) return floorH + Smooth((d - 3f) / 8f) * 3.4f + n2 * 0.6f * Smooth((d - 3f) / 8f);
        float topH = 1.2f + n1 * 3.5f + n2 * 1.0f + Mathf.Max(0f, d - 45f) * 0.16f;
        if (d < 23f)
        {
            float t = (d - 11f) / 12f;
            return Mathf.Lerp(-9.6f + n2 * 0.6f, topH, t * t * t * (t * (t * 6 - 15) + 10));
        }
        return topH;
    }

    static float gx0, gz0, cell;
    static int gnx, gnz;
    static float[,] gh;

    static float MeshH(float x, float z)
    {
        float fx = (x - gx0) / cell, fz = (z - gz0) / cell;
        int i = Mathf.Clamp(Mathf.FloorToInt(fx), 0, gnx - 1), j = Mathf.Clamp(Mathf.FloorToInt(fz), 0, gnz - 1);
        float u = Mathf.Clamp01(fx - i), v = Mathf.Clamp01(fz - j);
        float h00 = gh[i, j], h10 = gh[i + 1, j], h01 = gh[i, j + 1], h11 = gh[i + 1, j + 1];
        if (((i + j) & 1) == 0)
            return u > v ? h00 + (h10 - h00) * u + (h11 - h10) * v : h00 + (h11 - h01) * u + (h01 - h00) * v;
        return u + v < 1 ? h00 + (h10 - h00) * u + (h01 - h00) * v : h11 + (h01 - h11) * (1 - u) + (h10 - h11) * (1 - v);
    }

    static Vector3 MeshNormal(float x, float z)
    {
        float e = 0.5f;
        float hx = MeshH(x + e, z) - MeshH(x - e, z);
        float hz = MeshH(x, z + e) - MeshH(x, z - e);
        return new Vector3(-hx, 2 * e, -hz).normalized;
    }

    static MB BuildTerrain()
    {
        cell = 3f;
        gh = new float[gnx + 1, gnz + 1];
        for (int i = 0; i <= gnx; i++)
            for (int j = 0; j <= gnz; j++)
                gh[i, j] = TerrainH(gx0 + i * cell, gz0 + j * cell);
        var mb = new MB();
        for (int i = 0; i < gnx; i++)
            for (int j = 0; j < gnz; j++)
            {
                Vector3 P(int a, int b) => new Vector3(gx0 + a * cell, gh[a, b], gz0 + b * cell);
                var p00 = P(i, j); var p10 = P(i + 1, j); var p01 = P(i, j + 1); var p11 = P(i + 1, j + 1);
                if (((i + j) & 1) == 0)
                {
                    TerrainTri(mb, p00, p10, p11);
                    TerrainTri(mb, p00, p11, p01);
                }
                else
                {
                    TerrainTri(mb, p00, p10, p01);
                    TerrainTri(mb, p10, p11, p01);
                }
            }
        return mb;
    }

    static void TerrainTri(MB mb, Vector3 a, Vector3 b, Vector3 c)
    {
        var n = Vector3.Cross(b - a, c - a).normalized;
        if (n.y < 0) n = -n;
        float hc = (a.y + b.y + c.y) / 3f;
        Vector2 uv;
        if (hc < WaterY - 0.15f) uv = Chance(0.25f) ? UVr(R.Rock, 1, 3) : UVr(R.RiverBed, 1, 5);
        else if (n.y < 0.6f) uv = Chance(0.3f) ? UVr(R.RockMoss, 1, 4) : UVr(R.Rock, 2, 5);
        else if (n.y < 0.8f) { float r = (float)rng.NextDouble(); uv = r < 0.2f ? UVr(R.Dirt, 2, 4) : (r < 0.6f ? UVr(R.RockMoss, 2, 5) : UVr(R.Fern, 2, 4)); }
        else if (hc < -6f) uv = Chance(0.5f) ? UVr(R.Fern, 1, 4) : UVr(R.GrassTop, 1, 3);
        else uv = Chance(0.12f) ? UVr(R.GrassLight, 2, 4) : UVr(R.GrassTop, 1, 5);
        mb.Tri(a, b, c, uv, Vector3.up);
    }

    // ---------------------------------------------------------------- textures
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 982451653;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)int.MaxValue;
        }
    }
    static float TileNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        int W(int v) => ((v % period) + period) % period;
        float a = Hash(W(x0), W(y0), seed), b = Hash(W(x0 + 1), W(y0), seed);
        float c = Hash(W(x0), W(y0 + 1), seed), d = Hash(W(x0 + 1), W(y0 + 1), seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }
    static float Fbm(float u, float v, int basePeriod, int oct, int seed)
    {
        float sum = 0, amp = 0.5f, norm = 0; int p = basePeriod;
        for (int o = 0; o < oct; o++) { sum += amp * TileNoise(u * p, v * p, p, seed + o); norm += amp; amp *= 0.5f; p *= 2; }
        return sum / norm;
    }

    static Texture2D SavePng(string path, int w, int h, Func<int, int, Color> px, Action<TextureImporter> setup)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var cols = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) cols[y * w + x] = px(x, y);
        tex.SetPixels(cols);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        setup(imp);
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Color ShadeColor(Color baseCol, int shade)
    {
        float t = shade / (float)(Shades - 1);
        Color.RGBToHSV(baseCol, out float h, out float s, out float v);
        h = Mathf.Repeat(h - (t - 0.5f) * 0.035f, 1f);
        s = Mathf.Clamp01(s * Mathf.Lerp(1.06f, 0.9f, t));
        v = Mathf.Clamp01(v * Mathf.Lerp(0.74f, 1.16f, t));
        return Color.HSVToRGB(h, s, v);
    }

    // ---------------------------------------------------------------- materials
    static Material MakeMat(string path, Shader shader, Action<Material> setup)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        var m = existing != null ? existing : new Material(shader);
        m.shader = shader;
        setup(m);
        if (existing == null) AssetDatabase.CreateAsset(m, path);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void MakeTransparent(Material m, int blend /*0 alpha, 2 additive*/, bool twoSided)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", blend);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", twoSided ? 0f : 2f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", blend == 2 ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", blend == 2 ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetShaderPassEnabled("DepthOnly", false);
        m.SetShaderPassEnabled("ShadowCaster", false);
    }

    // ---------------------------------------------------------------- helpers for scene objects
    static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale, bool isStatic = true, bool shadows = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static List<Mesh> meshAssets = new List<Mesh>();
    static Mesh Reg(Mesh m) { meshAssets.Add(m); return m; }

    // ================================================================= BUILD
    [MenuItem("Tools/Project Roll/Cenário de Floresta/Gerar na cena aberta")]
    static void BuildActiveSceneMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Cenário de Floresta", "Saia do Play Mode antes de gerar o cenário.", "OK"); return; }
        var scene = EditorSceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path)) { EditorUtility.DisplayDialog("Cenário de Floresta", "Salve a cena antes de gerar o cenário.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Cenário de Floresta",
                $"Gerar (ou regerar) o cenário de floresta em '{scene.name}'?\n\nO visual das plataformas, a luz, o céu e o pós-processamento desta cena serão substituídos. Os colisores e a jogabilidade não mudam.",
                "Gerar", "Cancelar")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log("[ForestLevelBuilder] " + Build(scene.path));
    }

    public static string Build(string scenePath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Saia do Play Mode antes de gerar o cenário.");
        string sceneKey = Path.GetFileNameWithoutExtension(scenePath).Replace("_", "");
        int levelNumber = 1;
        var digits = new string(sceneKey.Where(char.IsDigit).ToArray());
        if (digits.Length > 0) int.TryParse(digits, out levelNumber);
        rng = new Random(1234 + (levelNumber - 1) * 7919);
        meshAssets.Clear();
        var log = new System.Text.StringBuilder();

        foreach (var dir in new[] { ArtRoot, ArtRoot + "/Textures", ArtRoot + "/Materials", ArtRoot + "/Meshes", ArtRoot + "/Settings" })
            Directory.CreateDirectory(dir);
        AssetDatabase.Refresh();

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var light = Object.FindObjectsByType<Light>().First(l => l.type == LightType.Directional);
        light.transform.rotation = Quaternion.Euler(40f, 300f, 0f);
        Vector3 sunDir = -light.transform.forward;

        // ---------------- textures
        var palette = SavePng(ArtRoot + "/Textures/T_ForestPalette.png", Shades, Roles,
            (x, y) => ShadeColor(Hex(AtlasHex[y]), x),
            imp => { imp.filterMode = FilterMode.Point; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp; imp.sRGBTexture = true; });

        var sky = SavePng(ArtRoot + "/Textures/T_ForestSky.png", 2048, 1024, (x, y) =>
        {
            float u = (x + 0.5f) / 2048f, v = (y + 0.5f) / 1024f;
            float lon = (u - 0.5f) * 2f * Mathf.PI, lat = (1f - v) * Mathf.PI;
            var dir = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
            Color col = dir.y >= 0
                ? Color.Lerp(Horizon, SkyZenith, Mathf.Pow(Mathf.Clamp01(dir.y * 1.15f), 0.38f))
                : Color.Lerp(Horizon, Horizon * 0.9f, Mathf.Pow(-dir.y, 0.6f));
            if (dir.y > -0.05f)
            {
                float band = Smooth((dir.y + 0.02f) / 0.08f) * (1f - Smooth((dir.y - 0.08f) / 0.16f));
                float c = Fbm(u, v * 0.35f, 6, 4, 11);
                float streak = Fbm(u, v * 1.4f, 10, 3, 19);
                float dens = Smooth((c * 0.7f + streak * 0.5f - 0.62f) / 0.16f) * band;
                col = Color.Lerp(col, CloudCol, dens * 0.6f);
            }
            float s = Mathf.Max(0f, Vector3.Dot(dir, sunDir));
            col += SunCol * (Mathf.Pow(s, 6f) * 0.28f + Mathf.Pow(s, 60f) * 0.35f + Mathf.Pow(s, 900f) * 1.2f);
            col.a = 1f;
            return col;
        }, imp => { imp.mipmapEnabled = false; imp.wrapModeU = TextureWrapMode.Repeat; imp.wrapModeV = TextureWrapMode.Clamp; imp.maxTextureSize = 2048; });

        var ripple = SavePng(ArtRoot + "/Textures/T_WaterRipple.png", 256, 256, (x, y) =>
        {
            float u = x / 256f, v = y / 256f;
            float f = Fbm(u, v, 4, 4, 3);
            float ridge = 1f - Mathf.Abs(2f * Fbm(u, v, 3, 3, 7) - 1f);
            float foam = Mathf.Pow(ridge, 7f);
            float val = 0.85f + 0.1f * f + 0.25f * foam;
            return new Color(val, val, val, Mathf.Clamp01(0.82f + 0.12f * f + 0.35f * foam));
        }, imp => { imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; });

        var normal = SavePng(ArtRoot + "/Textures/T_WaterNormal.png", 256, 256, (x, y) =>
        {
            float u = x / 256f, v = y / 256f, e = 1f / 256f;
            float H(float a, float b) => Fbm(a, b, 4, 4, 5);
            float dx = (H(u + e, v) - H(u - e, v)) * 18f;
            float dy = (H(u, v + e) - H(u, v - e)) * 18f;
            var n = new Vector3(-dx, -dy, 1f).normalized;
            return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }, imp => { imp.textureType = TextureImporterType.NormalMap; imp.wrapMode = TextureWrapMode.Repeat; });

        var mistTex = SavePng(ArtRoot + "/Textures/T_Mist.png", 256, 256, (x, y) =>
        {
            float u = x / 256f, v = y / 256f;
            float f = Fbm(u, v, 3, 4, 23);
            return new Color(1, 1, 1, Smooth((f - 0.35f) / 0.4f));
        }, imp => { imp.wrapMode = TextureWrapMode.Repeat; });

        var dot = SavePng(ArtRoot + "/Textures/T_SoftDot.png", 64, 64, (x, y) =>
        {
            float d = new Vector2(x - 31.5f, y - 31.5f).magnitude / 32f;
            float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
            return new Color(1, 1, 1, a);
        }, imp => { imp.wrapMode = TextureWrapMode.Clamp; imp.alphaIsTransparency = true; });

        var leafTex = SavePng(ArtRoot + "/Textures/T_Leaf.png", 64, 64, (x, y) =>
        {
            float u = (x - 31.5f) / 32f, v = (y - 31.5f) / 32f;
            float w = 0.55f * (1f - v * v);
            float a = Mathf.Abs(u) < w ? 1f : 0f;
            if (Mathf.Abs(u) < 0.05f) return new Color(0.82f, 0.82f, 0.82f, a);
            return new Color(1, 1, 1, a);
        }, imp => { imp.wrapMode = TextureWrapMode.Clamp; imp.alphaIsTransparency = true; });

        var shaftTex = SavePng(ArtRoot + "/Textures/T_LightShaft.png", 64, 128, (x, y) =>
        {
            float u = (x + 0.5f) / 64f, v = (y + 0.5f) / 128f;
            float a = Mathf.Sin(u * Mathf.PI);
            a = Mathf.Pow(a, 2f) * Smooth(v / 0.25f) * Smooth((1f - v) / 0.35f);
            return new Color(1, 1, 1, a);
        }, imp => { imp.wrapMode = TextureWrapMode.Clamp; imp.alphaIsTransparency = true; });

        // ---------------- materials
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var pUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        var atlas = MakeMat(ArtRoot + "/Materials/M_ForestAtlas.mat", lit, m =>
        {
            m.SetTexture("_BaseMap", palette); m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.12f); m.SetFloat("_Metallic", 0f);
            m.enableInstancing = true;
        });
        var foliage = MakeMat(ArtRoot + "/Materials/M_ForestAtlas_TwoSided.mat", lit, m =>
        {
            m.SetTexture("_BaseMap", palette); m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.08f); m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Cull", 0f);
            m.enableInstancing = true;
        });
        Material WaterMat(string path, Color c, float alpha, Vector2 tiling)
        {
            return MakeMat(path, lit, m =>
            {
                MakeTransparent(m, 0, false);
                m.SetTexture("_BaseMap", ripple);
                m.SetTextureScale("_BaseMap", tiling);
                c.a = alpha; m.SetColor("_BaseColor", c);
                m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 0.55f); m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_Smoothness", 0.9f); m.SetFloat("_Metallic", 0f);
                m.SetFloat("_ReceiveShadows", 1f);
            });
        }
        var water = WaterMat(ArtRoot + "/Materials/M_Water.mat", WaterCol, 0.8f, Vector2.one);
        var cascade = WaterMat(ArtRoot + "/Materials/M_WaterCascade.mat", CascadeCol, 0.88f, Vector2.one);
        var mist = MakeMat(ArtRoot + "/Materials/M_Mist.mat", pUnlit, m =>
        {
            MakeTransparent(m, 0, true);
            m.SetTexture("_BaseMap", mistTex);
            var c = Mist; c.a = 0.18f; m.SetColor("_BaseColor", c);
        });
        var shaftMat = MakeMat(ArtRoot + "/Materials/M_LightShaft.mat", pUnlit, m =>
        {
            MakeTransparent(m, 2, true);
            m.SetTexture("_BaseMap", shaftTex);
            var c = SunCol; c.a = 0.09f; m.SetColor("_BaseColor", c);
        });
        var pollenMat = MakeMat(ArtRoot + "/Materials/M_Particle_Pollen.mat", pUnlit, m =>
        {
            MakeTransparent(m, 2, true);
            m.SetTexture("_BaseMap", dot); m.SetColor("_BaseColor", Color.white);
        });
        var fireflyMat = MakeMat(ArtRoot + "/Materials/M_Particle_Firefly.mat", pUnlit, m =>
        {
            MakeTransparent(m, 2, true);
            m.SetTexture("_BaseMap", dot); m.SetColor("_BaseColor", new Color(2.6f, 2.6f, 2.6f, 1f));
        });
        var foamMat = MakeMat(ArtRoot + "/Materials/M_Particle_Foam.mat", pUnlit, m =>
        {
            MakeTransparent(m, 0, true);
            m.SetTexture("_BaseMap", dot); m.SetColor("_BaseColor", Color.white);
        });
        var leafMat = MakeMat(ArtRoot + "/Materials/M_Particle_Leaf.mat", pUnlit, m =>
        {
            MakeTransparent(m, 0, true);
            m.SetTexture("_BaseMap", leafTex); m.SetColor("_BaseColor", Color.white);
        });
        var lantern = MakeMat(ArtRoot + "/Materials/M_LanternGlow.mat", lit, m =>
        {
            m.SetColor("_BaseColor", Accent);
            m.SetColor("_EmissionColor", Accent * 9f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetFloat("_Smoothness", 0.5f);
        });
        var skyMat = MakeMat(ArtRoot + "/Materials/M_ForestSky.mat", Shader.Find("Skybox/Panoramic"), m =>
        {
            m.SetTexture("_MainTex", sky);
            m.SetFloat("_Mapping", 1f);
            m.SetFloat("_ImageType", 0f);
            m.SetFloat("_Exposure", 1f);
            m.SetFloat("_Rotation", 0f);
        });

        // ---------------- clean previous run
        foreach (var go in scene.GetRootGameObjects().Where(g => g.name == EnvName || g.name == "Global Volume").ToArray())
            Object.DestroyImmediate(go);
        var platforms = scene.GetRootGameObjects().Where(g => g.name.StartsWith("Platform_")).ToList();
        foreach (var p in platforms)
        {
            var old = p.transform.Find("Visual");
            if (old) Object.DestroyImmediate(old.gameObject);
        }

        var plats = platforms.Select(g => g.transform).OrderBy(t => t.name, StringComparer.Ordinal).ToList();
        var start = plats.First(t => t.name == "Platform_Start");
        var finish = plats.First(t => t.name == "Platform_Finish");

        // ---------------- path: the gorge follows a minimum spanning tree of the platforms
        int pc = plats.Count;
        var pts = plats.Select(t => new Vector2(t.position.x, t.position.z)).ToArray();
        var wid = plats.Select(t => Mathf.Max(t.lossyScale.x, t.lossyScale.z) * 0.5f).ToArray();
        int si = plats.IndexOf(start), fi = plats.IndexOf(finish);
        var parent = Enumerable.Repeat(-1, pc).ToArray();
        var inTree = new bool[pc];
        var bestD = Enumerable.Repeat(float.MaxValue, pc).ToArray();
        bestD[si] = 0f;
        for (int it = 0; it < pc; it++)
        {
            int u = -1;
            for (int i = 0; i < pc; i++) if (!inTree[i] && (u < 0 || bestD[i] < bestD[u])) u = i;
            inTree[u] = true;
            for (int i = 0; i < pc; i++)
            {
                if (inTree[i]) continue;
                float d = (pts[i] - pts[u]).magnitude;
                if (d < bestD[i]) { bestD[i] = d; parent[i] = u; }
            }
        }
        edges.Clear(); mainPath.Clear();
        for (int i = 0; i < pc; i++)
            if (parent[i] >= 0) edges.Add(new Edge { a = pts[parent[i]], b = pts[i], wa = wid[parent[i]], wb = wid[i] });
        var chain = new List<int>();
        for (int i = fi; i >= 0; i = parent[i]) chain.Add(i);
        chain.Reverse();
        foreach (int i in chain) mainPath.Add(pts[i]);

        // river comes in behind the start and ends in a waterfall past the finish
        int startNext = Enumerable.Range(0, pc).Where(i => parent[i] == si).OrderBy(i => (pts[i] - pts[si]).sqrMagnitude).DefaultIfEmpty(fi).First();
        var fStart = (pts[startNext] - pts[si]).normalized;
        var upstream = pts[si] - fStart * 80f + new Vector2(-fStart.y, fStart.x) * 6f;
        edges.Add(new Edge { a = upstream, b = pts[si], wa = 5f, wb = wid[si] });
        int finishPrev = parent[fi] >= 0 ? parent[fi] : si;
        var fFinish = (pts[fi] - pts[finishPrev]).normalized;
        riverEndDir = RotateTowardX(fFinish, 14f).normalized;
        riverEnd = pts[fi] + riverEndDir * 27f;
        edges.Add(new Edge { a = pts[fi], b = riverEnd, wa = wid[fi], wb = 4f });

        // terrain bounds: platform footprint + 95 m, snapped to the grid
        float minX = plats.Min(t => t.position.x - t.lossyScale.x * 0.5f) - 95f, maxX = plats.Max(t => t.position.x + t.lossyScale.x * 0.5f) + 95f;
        float minZ = plats.Min(t => t.position.z - t.lossyScale.z * 0.5f) - 95f, maxZ = plats.Max(t => t.position.z + t.lossyScale.z * 0.5f) + 95f;
        gx0 = Mathf.Floor(minX / 3f) * 3f; gz0 = Mathf.Floor(minZ / 3f) * 3f;
        gnx = Mathf.CeilToInt((maxX - gx0) / 3f); gnz = Mathf.CeilToInt((maxZ - gz0) / 3f);

        var env = new GameObject(EnvName).transform;

        // ---------------- terrain
        var terrainMesh = Reg(BuildTerrain().Build("Terrain"));
        MeshObject("Terrain", env, terrainMesh, atlas, Vector3.zero, Quaternion.identity, Vector3.one);
        log.AppendLine($"terrain verts {terrainMesh.vertexCount}");

        // ---------------- water + mist
        {
            var mb = new MB();
            float x0 = gx0, x1 = gx0 + gnx * cell, z0 = gz0, z1 = gz0 + gnz * cell;
            var a = new Vector3(x0, 0, z0); var b = new Vector3(x0, 0, z1); var c = new Vector3(x1, 0, z1); var d = new Vector3(x1, 0, z0);
            int i0 = mb.V.Count;
            mb.V.AddRange(new[] { a, b, c, d });
            mb.U.AddRange(new[] { new Vector2(x0, z0) / 9f, new Vector2(x0, z1) / 9f, new Vector2(x1, z1) / 9f, new Vector2(x1, z0) / 9f });
            mb.T.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
            var waterMesh = Reg(mb.Build("WaterPlane", true));
            var w = MeshObject("River", env, waterMesh, water, new Vector3(0, WaterY, 0), Quaternion.identity, Vector3.one, true, false);
            AddScroll(w, new Vector2(0.01f, 0.05f));

            var mm = new MB();
            int j0 = mm.V.Count;
            mm.V.AddRange(new[] { a, b, c, d });
            mm.U.AddRange(new[] { new Vector2(x0, z0) / 45f, new Vector2(x0, z1) / 45f, new Vector2(x1, z1) / 45f, new Vector2(x1, z0) / 45f });
            mm.T.AddRange(new[] { j0, j0 + 1, j0 + 2, j0, j0 + 2, j0 + 3 });
            var mistMesh = Reg(mm.Build("MistPlane"));
            var m1 = MeshObject("Mist_Low", env, mistMesh, mist, new Vector3(0, -10.2f, 0), Quaternion.identity, Vector3.one, false, false);
            AddScroll(m1, new Vector2(0.004f, 0.006f));
            var m2 = MeshObject("Mist_High", env, mistMesh, mist, new Vector3(7f, -8.3f, 11f), Quaternion.Euler(0, 37f, 0), Vector3.one, false, false);
            AddScroll(m2, new Vector2(-0.006f, 0.003f));
        }

        // ---------------- platforms
        var decorTuft = Enumerable.Range(0, 4).Select(i => Tuft(500 + i, Rf(0.9f, 1.3f))).ToArray();
        var decorFlower = Enumerable.Range(0, 3).Select(i => Flowers(600 + i)).ToArray();
        var decorMush = Enumerable.Range(0, 3).Select(i => Mushrooms(700 + i)).ToArray();
        var decorPebble = Enumerable.Range(0, 3).Select(i => RockMesh(800 + i, new Vector3(0.22f, 0.13f, 0.18f))).ToArray();
        var decorBush = Enumerable.Range(0, 3).Select(i => Bush(900 + i, Rf(0.8f, 1.2f))).ToArray();
        var decorRock = Enumerable.Range(0, 5).Select(i => RockMesh(1000 + i, new Vector3(Rf(0.9f, 1.3f), Rf(0.55f, 0.8f), Rf(0.8f, 1.2f)))).ToArray();
        var decorRockLow = Enumerable.Range(0, 5).Select(i => RockMeshLow(1100 + i, new Vector3(Rf(0.9f, 1.3f), Rf(0.55f, 0.8f), Rf(0.8f, 1.2f)))).ToArray();

        int seed = 100;
        foreach (var p in plats)
        {
            string n = p.name.Replace(" ", "").Replace("(", "_").Replace(")", "");
            float sx = p.lossyScale.x, sy = p.lossyScale.y, sz = p.lossyScale.z;
            float topY = p.position.y + sy * 0.5f;
            float depth = topY - PillarBottom;
            var mr = p.GetComponent<MeshRenderer>();
            if (mr) mr.enabled = false;

            var vis = new GameObject("Visual").transform;
            vis.SetParent(p, false);
            vis.localPosition = new Vector3(0, 0.5f + 0.004f / sy, 0);
            vis.localRotation = Quaternion.identity;
            vis.localScale = new Vector3(1f / sx, 1f / sy, 1f / sz);

            bool stump = sx <= 2.01f && sz <= 2.01f;
            Mesh body;
            float hx = sx * 0.5f, hz = sz * 0.5f;
            if (stump) body = Reg(StumpPillar(Mathf.Max(hx, hz) * 1.28f, depth, seed++).Build(n + "_Stump"));
            else body = Reg(PlatformPillar(hx, hz, depth, seed++).Build(n + "_Pillar"));
            MeshObject("Body", vis, body, atlas, Vector3.zero, Quaternion.identity, Vector3.one);

            // top decor, combined into one mesh (no colliders)
            var dm = new MB();
            int count = Mathf.RoundToInt(sx * sz * (stump ? 2.5f : 1.1f));
            for (int i = 0; i < count; i++)
            {
                float x = Rf(-hx + 0.15f, hx - 0.15f), z = Rf(-hz + 0.15f, hz - 0.15f);
                float edge = Mathf.Min(hx - Mathf.Abs(x), hz - Mathf.Abs(z));
                float keepP = edge < 0.9f ? 1f : (stump ? 0f : 0.18f);
                if (!Chance(keepP)) continue;
                var m = Matrix4x4.TRS(new Vector3(x, 0, z), Quaternion.Euler(0, Rf(0, 360), 0), Vector3.one * Rf(0.8f, 1.2f));
                float roll = (float)rng.NextDouble();
                if (stump)
                {
                    if (roll < 0.5f) dm.Append(decorTuft[Ri(0, 3)], m);
                    else if (roll < 0.62f) dm.Append(decorMush[Ri(0, 2)], m);
                }
                else if (roll < 0.72f) dm.Append(decorTuft[Ri(0, 3)], m);
                else if (roll < 0.84f) dm.Append(decorFlower[Ri(0, 2)], m);
                else if (roll < 0.91f && edge < 0.6f) dm.Append(decorMush[Ri(0, 2)], m);
                else if (edge < 0.5f) dm.Append(decorPebble[Ri(0, 2)], m);
            }
            if (dm.V.Count > 0)
            {
                var decorMesh = Reg(dm.Build(n + "_Decor"));
                MeshObject("Decor", vis, decorMesh, foliage, Vector3.zero, Quaternion.identity, Vector3.one, true, false);
            }
        }

        // ---------------- trees
        var pines = new[] { Reg(Pine(10f, 11).Build("Pine_A")), Reg(Pine(10f, 12).Build("Pine_B")), Reg(Pine(10f, 13).Build("Pine_C")), Reg(Pine(10f, 14).Build("Pine_D")) };
        var rounds = new[] { Reg(RoundTree(8f, 21).Build("Broadleaf_A")), Reg(RoundTree(8f, 22).Build("Broadleaf_B")), Reg(RoundTree(8f, 23).Build("Broadleaf_C")) };
        var treesRoot = Child(env, "Trees");
        int treeCount = 0;
        void PlaceTree(Transform parent, Vector3 pos, float scale, bool pine, bool collider)
        {
            var mesh = pine ? pines[Ri(0, pines.Length - 1)] : rounds[Ri(0, rounds.Length - 1)];
            var go = MeshObject((pine ? "Pine_" : "Broadleaf_") + treeCount.ToString("000"), parent, mesh, atlas, pos,
                Quaternion.Euler(Rf(-2f, 2f), Rf(0, 360), Rf(-2f, 2f)), Vector3.one * scale);
            if (collider)
            {
                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = pine ? 0.4f : 0.45f; cc.height = 4f; cc.center = new Vector3(0, 2f, 0);
            }
            treeCount++;
        }

        // banks (dense forest above the gorge)
        for (float x = gx0 + 2; x < gx0 + gnx * cell - 2; x += 5.2f)
            for (float z = gz0 + 2; z < gz0 + gnz * cell - 2; z += 5.2f)
            {
                float px = x + Rf(-2.2f, 2.2f), pz = z + Rf(-2.2f, 2.2f);
                float d = PathDist(px, pz);
                if (d < 13.5f || d > 65f || InCascade(new Vector2(px, pz))) continue;
                float prob = d < 30f ? 0.72f : (d < 55f ? 0.55f : 0.4f);
                if (!Chance(prob)) continue;
                if (MeshNormal(px, pz).y < 0.72f) continue;
                bool pine = Chance(0.7f);
                PlaceTree(treesRoot, new Vector3(px, MeshH(px, pz) - 0.15f, pz), pine ? Rf(0.85f, 1.45f) : Rf(0.85f, 1.3f), pine, false);
                if (d > 26f) treesRoot.GetChild(treesRoot.childCount - 1).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        // giant pines rising from the gorge floor
        foreach (var edge in edges)
        {
            var a = edge.a; var b = edge.b;
            float len = (b - a).magnitude;
            if (len < 0.5f) continue;
            var dir = (b - a) / len; var side = new Vector2(-dir.y, dir.x);
            for (float s = 0; s < len; s += 8f)
                foreach (int sign in new[] { -1, 1 })
                {
                    if (!Chance(0.4f)) continue;
                    var p2 = a + dir * (s + Rf(-2f, 2f)) + side * sign * (Mathf.Lerp(edge.wa, edge.wb, s / len) + Rf(9.5f, 12f));
                    float d = PathDist(p2.x, p2.y);
                    if (d < 9.5f || d > 12.5f || InCascade(p2)) continue;
                    // keep the area behind the start clear: that is where the camera spawns
                    var fromStart = p2 - pts[si];
                    if (fromStart.magnitude < wid[si] + 16f && Vector2.Dot(fromStart, fStart) < wid[si]) continue;
                    PlaceTree(treesRoot, new Vector3(p2.x, MeshH(p2.x, p2.y) - 0.3f, p2.y), Rf(1.6f, 2.1f), true, false);
                }
        }
        // a few trees on the start and finish plateaus (only when the plateau is big enough)
        {
            void Plateau(Transform plat, Vector2 forwardWorld, (float side, float fwd, float scale, bool pine)[] spots)
            {
                float half = Mathf.Min(plat.lossyScale.x, plat.lossyScale.z) * 0.5f;
                if (half < 4.5f) return;
                var vis = plat.Find("Visual");
                var root = Child(vis, "Trees");
                var f3 = vis.InverseTransformDirection(new Vector3(forwardWorld.x, 0, forwardWorld.y)); f3.y = 0; f3.Normalize();
                var r3 = Vector3.Cross(Vector3.up, f3);
                float k = half / 6f;
                foreach (var sp in spots)
                    PlaceTree(root, (r3 * sp.side + f3 * sp.fwd) * k, sp.scale, sp.pine, true);
            }
            Plateau(start, fStart, new[] { (-4.9f, 4.4f, 0.8f, true), (4.9f, 4.2f, 0.8f, false) });
            Plateau(finish, fFinish, new[] { (-4.8f, 4.6f, 1.0f, true), (4.6f, 4.8f, 0.9f, false), (5.0f, -1.5f, 0.75f, true) });
        }
        log.AppendLine($"trees {treeCount}");

        // ---------------- ground decor (chunked meshes)
        var chunks = new Dictionary<(int, int), MB>();
        MB ChunkAt(float x, float z)
        {
            var key = (Mathf.FloorToInt(x / 40f), Mathf.FloorToInt(z / 40f));
            if (!chunks.TryGetValue(key, out var mb)) { mb = new MB(); chunks[key] = mb; }
            return mb;
        }
        var rockChunks = new Dictionary<(int, int), MB>();
        MB RockChunkAt(float x, float z)
        {
            var key = (Mathf.FloorToInt(x / 40f), Mathf.FloorToInt(z / 40f));
            if (!rockChunks.TryGetValue(key, out var mb)) { mb = new MB(); rockChunks[key] = mb; }
            return mb;
        }
        int tufts = 0, bushes = 0, flowers = 0, rocks = 0;
        for (float x = gx0 + 1; x < gx0 + gnx * cell - 1; x += 1.7f)
            for (float z = gz0 + 1; z < gz0 + gnz * cell - 1; z += 1.7f)
            {
                float px = x + Rf(-0.8f, 0.8f), pz = z + Rf(-0.8f, 0.8f);
                float d = PathDist(px, pz);
                if (d < 3.2f || d > 38f) continue;
                float h = MeshH(px, pz);
                if (h < WaterY + 0.25f) continue;
                var nrm = MeshNormal(px, pz);
                var rot = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, nrm, 0.5f)) * Quaternion.Euler(0, Rf(0, 360), 0);
                var pos = new Vector3(px, h, pz);
                float roll = (float)rng.NextDouble();
                bool rim = d > 11.5f;
                float tuftP = rim ? (d < 28f ? 0.4f : 0.18f) : 0.16f;
                if (nrm.y > 0.75f && roll < tuftP)
                {
                    ChunkAt(px, pz).Append(decorTuft[Ri(0, 3)], Matrix4x4.TRS(pos, rot, Vector3.one * Rf(0.9f, 1.5f))); tufts++;
                }
                else if (nrm.y > 0.75f && roll < tuftP + 0.05f)
                {
                    ChunkAt(px, pz).Append(decorBush[Ri(0, 2)], Matrix4x4.TRS(pos + Vector3.down * 0.1f, rot, Vector3.one * Rf(0.9f, 1.8f))); bushes++;
                }
                else if (rim && d < 34f && nrm.y > 0.8f && roll < tuftP + 0.085f)
                {
                    ChunkAt(px, pz).Append(decorFlower[Ri(0, 2)], Matrix4x4.TRS(pos, rot, Vector3.one * Rf(1f, 1.4f))); flowers++;
                }
                else if (roll < tuftP + 0.088f)
                {
                    var s = Rf(0.5f, 1.6f) * (rim ? 1f : 1.3f);
                    RockChunkAt(px, pz).Append(decorRockLow[Ri(0, 4)], Matrix4x4.TRS(pos + Vector3.down * 0.25f * s, Quaternion.Euler(Rf(-15, 15), Rf(0, 360), Rf(-15, 15)), Vector3.one * s)); rocks++;
                }
                else if (!rim && roll < tuftP + 0.12f)
                {
                    ChunkAt(px, pz).Append(decorMush[Ri(0, 2)], Matrix4x4.TRS(pos, rot, Vector3.one * Rf(1.4f, 2.2f)));
                }
            }
        // river boulders
        for (int i = 0; i < 70; i++)
        {
            var edge = RandomEdge();
            var a = edge.a; var b = edge.b;
            var p2 = Vector2.Lerp(a, b, Rf(0f, 1f)) + new Vector2(Rf(-9f, 9f), Rf(-9f, 9f));
            float d = PathDist(p2.x, p2.y);
            if (d > 5.5f) continue;
            float s = Rf(0.8f, 2.2f);
            var pos = new Vector3(p2.x, MeshH(p2.x, p2.y) + s * 0.15f, p2.y);
            RockChunkAt(p2.x, p2.y).Append(decorRock[Ri(0, 4)], Matrix4x4.TRS(pos, Quaternion.Euler(Rf(-20, 20), Rf(0, 360), Rf(-20, 20)), new Vector3(s, s * Rf(0.8f, 1.2f), s)));
            rocks++;
        }
        var decorRoot = Child(env, "GroundDecor");
        foreach (var kv in chunks)
        {
            var mesh = Reg(kv.Value.Build($"GroundFoliage_{kv.Key.Item1}_{kv.Key.Item2}"));
            MeshObject(mesh.name, decorRoot, mesh, foliage, Vector3.zero, Quaternion.identity, Vector3.one, true, false);
        }
        foreach (var kv in rockChunks)
        {
            var mesh = Reg(kv.Value.Build($"GroundRocks_{kv.Key.Item1}_{kv.Key.Item2}"));
            MeshObject(mesh.name, decorRoot, mesh, atlas, Vector3.zero, Quaternion.identity, Vector3.one);
        }
        log.AppendLine($"tufts {tufts} bushes {bushes} flowers {flowers} rocks {rocks}");

        // fallen logs
        var logsRoot = Child(env, "Logs");
        var logMeshes = new[] { Reg(Log(31, 4.5f, 0.45f).Build("Log_A")), Reg(Log(32, 6f, 0.55f).Build("Log_B")), Reg(Log(33, 3.2f, 0.38f).Build("Log_C")) };
        int logs = 0;
        for (int i = 0; i < 40 && logs < 14; i++)
        {
            var edge = RandomEdge();
            var a = edge.a; var b = edge.b;
            if ((b - a).sqrMagnitude < 0.25f) continue;
            var dir = (b - a).normalized; var side = new Vector2(-dir.y, dir.x);
            var p2 = Vector2.Lerp(a, b, Rf(0f, 1f)) + side * (Chance(0.5f) ? 1 : -1) * Rf(9f, 26f);
            float d = PathDist(p2.x, p2.y);
            if (d < 4f) continue;
            if (d > 11f && d < 22f) continue; // skip the steep cliff band
            var n = MeshNormal(p2.x, p2.y);
            if (n.y < 0.8f) continue;
            var rot = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0, Rf(0, 360), 0);
            MeshObject("Log_" + logs, logsRoot, logMeshes[Ri(0, 2)], atlas, new Vector3(p2.x, MeshH(p2.x, p2.y) + 0.2f, p2.y), rot, Vector3.one);
            logs++;
        }

        // ---------------- waterfall cascade at the far end of the gorge
        {
            var wf = Child(env, "Waterfall");
            var lastSeg = riverEndDir;
            var e = riverEnd;
            var side = new Vector2(-lastSeg.y, lastSeg.x);
            var mb = new MB();
            Vector3? pl = null, pr = null; float vAcc = 0;
            Vector3 prevC = Vector3.zero;
            int idx0 = 0;
            for (float s = 3f; s <= 42f; s += 1.25f)
            {
                var c2 = e + lastSeg * s;
                float halfW = Mathf.Lerp(4.6f, 3.0f, Mathf.InverseLerp(10f, 42f, s));
                var l2 = c2 + side * halfW; var r2 = c2 - side * halfW;
                float hC = Mathf.Max(MeshH(c2.x, c2.y), Mathf.Max(MeshH(l2.x, l2.y), MeshH(r2.x, r2.y))) + 0.35f;
                var cl = new Vector3(l2.x, hC, l2.y); var cr = new Vector3(r2.x, hC, r2.y); var cc = new Vector3(c2.x, hC, c2.y);
                if (pl.HasValue) vAcc += (cc - prevC).magnitude;
                int i0 = mb.V.Count;
                mb.V.Add(cl); mb.V.Add(cr);
                mb.U.Add(new Vector2(0, vAcc / 4f)); mb.U.Add(new Vector2(1.2f, vAcc / 4f));
                if (pl.HasValue)
                {
                    mb.T.AddRange(new[] { idx0, i0, i0 + 1, idx0, i0 + 1, idx0 + 1 });
                }
                idx0 = i0; pl = cl; pr = cr; prevC = cc;
            }
            // fix winding so the strip faces up
            var tmp = mb.Build("Cascade", true);
            if (tmp.normals.Length > 0 && tmp.normals.Average(n => n.y) < 0)
            {
                for (int t = 0; t < mb.T.Count; t += 3) { int x = mb.T[t + 1]; mb.T[t + 1] = mb.T[t + 2]; mb.T[t + 2] = x; }
                Object.DestroyImmediate(tmp);
                tmp = mb.Build("Cascade", true);
            }
            var cascadeMesh = Reg(tmp);
            var rockMeshes = decorRock.Select((m, i) => Reg(m.Build("Rock_" + i))).ToArray();
            var cgo = MeshObject("Cascade", wf, cascadeMesh, cascade, Vector3.zero, Quaternion.identity, Vector3.one, true, false);
            AddScroll(cgo, new Vector2(0f, 0.55f));
            // rocks lining the cascade
            for (float s = 6f; s <= 34f; s += 2.6f)
                foreach (int sign in new[] { -1, 1 })
                {
                    var p2 = e + lastSeg * (s + Rf(-0.8f, 0.8f)) + side * sign * Rf(4.4f, 5.8f);
                    float sc = Rf(0.9f, 1.8f);
                    MeshObject("CascadeRock", wf, rockMeshes[Ri(0, 4)], atlas, new Vector3(p2.x, MeshH(p2.x, p2.y) + 0.1f, p2.y), Quaternion.Euler(Rf(-15, 15), Rf(0, 360), Rf(-15, 15)), Vector3.one * sc);
                }
            var basePos = new Vector3(e.x + lastSeg.x * 7f, WaterY + 0.1f, e.y + lastSeg.y * 7f);
            var foam = Particles("CascadeFoam", wf, basePos, foamMat);
            var main = foam.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 2.0f);
            main.startColor = new ParticleSystem.MinMaxGradient(Mist, Color.Lerp(Mist, CascadeCol, 0.4f));
            main.gravityModifier = 0.05f;
            main.maxParticles = 200;
            var em = foam.emission; em.rateOverTime = 45f;
            var sh = foam.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(8f, 0.3f, 3f);
            sh.rotation = new Vector3(0, Mathf.Atan2(lastSeg.x, lastSeg.y) * Mathf.Rad2Deg, 0);
            FadeInOut(foam, 0.75f);
        }

        // ---------------- light shafts
        {
            var shafts = Child(env, "LightShafts");
            var sm = new MB();
            // crossed quads, unit size, along +Y
            void QuadXY(float rotY)
            {
                var q = Quaternion.Euler(0, rotY, 0);
                int i0 = sm.V.Count;
                sm.V.Add(q * new Vector3(-0.5f, -0.5f, 0)); sm.V.Add(q * new Vector3(0.5f, -0.5f, 0));
                sm.V.Add(q * new Vector3(0.5f, 0.5f, 0)); sm.V.Add(q * new Vector3(-0.5f, 0.5f, 0));
                sm.U.Add(new Vector2(0, 0)); sm.U.Add(new Vector2(1, 0)); sm.U.Add(new Vector2(1, 1)); sm.U.Add(new Vector2(0, 1));
                sm.T.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
            QuadXY(0); QuadXY(90);
            var shaftMesh = Reg(sm.Build("LightShaft"));
            var align = Quaternion.FromToRotation(Vector3.up, sunDir);
            float[] ts = { 0.12f, 0.27f, 0.38f, 0.52f, 0.66f, 0.8f, 0.92f };
            for (int i = 0; i < ts.Length; i++)
            {
                if (mainPath.Count < 2) break;
                float f = ts[i] * (mainPath.Count - 1);
                int seg = Mathf.Min(Mathf.FloorToInt(f), mainPath.Count - 2);
                var a = mainPath[seg]; var b = mainPath[seg + 1];
                var dir = (b - a).normalized; var side = new Vector2(-dir.y, dir.x);
                var p2 = Vector2.Lerp(a, b, f - seg) + side * (i % 2 == 0 ? 1 : -1) * Rf(5f, 9f);
                var basePos = new Vector3(p2.x, -11.5f, p2.y);
                float len = Rf(34f, 46f), w = Rf(2.5f, 5f);
                var go = MeshObject("Shaft_" + i, shafts, shaftMesh, shaftMat, basePos + sunDir * len * 0.5f, align, new Vector3(w, len, w), false, false);
            }
        }

        // ---------------- finish gate (goal marker)
        {
            var fVis = finish.Find("Visual");
            var gate = Child(fVis, "FinishGate");
            var toArrival = fVis.InverseTransformDirection(new Vector3(-fFinish.x, 0, -fFinish.y)); toArrival.y = 0; toArrival.Normalize();
            gate.localPosition = toArrival * Mathf.Min(finish.lossyScale.x, finish.lossyScale.z) * 0.27f;
            gate.localRotation = Quaternion.LookRotation(-toArrival, Vector3.up);
            var gm = new MB();
            var post = new MB();
            Trunk(post, 3.9f, 0.2f, 0.17f, 6, Vector3.zero, 0f);
            gm.Append(post, Matrix4x4.TRS(new Vector3(-2.4f, 0, 0), Quaternion.Euler(0, 0, -2f), Vector3.one));
            gm.Append(post, Matrix4x4.TRS(new Vector3(2.4f, 0, 0), Quaternion.Euler(0, 40, 2f), Vector3.one));
            gm.Append(Log(41, 6.4f, 0.24f), Matrix4x4.TRS(new Vector3(0, 3.75f, 0), Quaternion.Euler(0, 0, 1.5f), Vector3.one));
            gm.Append(Log(42, 5.2f, 0.15f), Matrix4x4.TRS(new Vector3(0, 3.15f, 0.02f), Quaternion.identity, Vector3.one));
            // hanging moss
            for (int i = 0; i < 9; i++)
            {
                float x = Rf(-2.8f, 2.8f);
                var top = new Vector3(x, 3.55f, Rf(-0.15f, 0.15f));
                float len = Rf(0.4f, 1.1f);
                gm.TriRaw(top + new Vector3(-0.09f, 0, 0), top + new Vector3(0.09f, 0, 0), top + new Vector3(Rf(-0.05f, 0.05f), -len, 0), UVr(R.Fern, 2, 4));
            }
            var gateMesh = Reg(gm.Build("FinishGate"));
            MeshObject("Gate", gate, gateMesh, foliage, Vector3.zero, Quaternion.identity, Vector3.one);

            var lm = new MB();
            Blob(lm, Vector3.zero, new Vector3(0.3f, 0.4f, 0.3f), 0.05f, 0, n => UV(R.FlowerYellow, 7));
            var lanternMesh = Reg(lm.Build("LanternGlow"));
            var frame = new MB();
            Trunk(frame, 0.55f, 0.025f, 0.025f, 4, Vector3.zero, 0f);
            var frameMesh = Reg(frame.Build("LanternCord"));
            foreach (float x in new[] { -2.05f, 2.05f })
            {
                var l = MeshObject("Lantern", gate, lanternMesh, lantern, new Vector3(x, 2.55f, 0), Quaternion.identity, Vector3.one, false, false);
                MeshObject("Cord", gate, frameMesh, atlas, new Vector3(x, 2.75f, 0), Quaternion.identity, Vector3.one, false, false);
                var lg = new GameObject("LanternLight").AddComponent<Light>();
                lg.transform.SetParent(l.transform, false);
                lg.type = LightType.Point; lg.color = Accent; lg.intensity = 5f; lg.range = 8f; lg.shadows = LightShadows.None;
            }
            var ff = Particles("Fireflies", fVis, new Vector3(0, 1.8f, 0), fireflyMat);
            var main = ff.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(Accent, Color.Lerp(Accent, Pollen, 0.5f));
            main.maxParticles = 80;
            var em = ff.emission; em.rateOverTime = 9f;
            var sh = ff.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(11f, 3f, 11f);
            var nz = ff.noise; nz.enabled = true; nz.strength = 0.6f; nz.frequency = 0.4f;
            FadeInOut(ff, 1f);
        }

        // ---------------- ambient particles
        {
            var pr = Child(env, "AmbientParticles");
            float avgTop = plats.Average(t => t.position.y + t.lossyScale.y * 0.5f);
            float total = 0f; for (int i = 0; i < mainPath.Count - 1; i++) total += (mainPath[i + 1] - mainPath[i]).magnitude;
            int count = Mathf.Clamp(Mathf.RoundToInt(total / 20f), 2, 8);
            var centers = new List<Vector3>();
            for (int c = 0; c < count; c++)
            {
                float target = total * (c + 0.5f) / count, acc = 0f;
                for (int i = 0; i < mainPath.Count - 1; i++)
                {
                    float l = (mainPath[i + 1] - mainPath[i]).magnitude;
                    if (acc + l >= target || i == mainPath.Count - 2)
                    {
                        var q = Vector2.Lerp(mainPath[i], mainPath[i + 1], l > 0 ? Mathf.Clamp01((target - acc) / l) : 0f);
                        centers.Add(new Vector3(q.x, avgTop + 2.5f, q.y));
                        break;
                    }
                    acc += l;
                }
            }
            foreach (var c in centers)
            {
                var ps = Particles("Pollen", pr, c, pollenMat);
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                main.startColor = new ParticleSystem.MinMaxGradient(Pollen, Color.Lerp(Pollen, SunCol, 0.6f));
                main.maxParticles = 400;
                var em = ps.emission; em.rateOverTime = 26f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(26f, 12f, 26f);
                var nz = ps.noise; nz.enabled = true; nz.strength = 0.25f; nz.frequency = 0.25f;
                FadeInOut(ps, 0.85f);

                var lf = Particles("FallingLeaves", pr, c + Vector3.up * 11f, leafMat);
                var lm = lf.main;
                lm.startLifetime = new ParticleSystem.MinMaxCurve(10f, 14f);
                lm.startSpeed = 0f;
                lm.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.26f);
                lm.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                lm.startColor = new ParticleSystem.MinMaxGradient(ShadeColor(Hex(AtlasHex[(int)R.LeafLight]), 5), ShadeColor(Hex(AtlasHex[(int)R.FlowerYellow]), 3));
                lm.gravityModifier = 0.025f;
                lm.maxParticles = 60;
                var le = lf.emission; le.rateOverTime = 2.5f;
                var ls = lf.shape; ls.shapeType = ParticleSystemShapeType.Box; ls.scale = new Vector3(30f, 2f, 30f);
                var ln = lf.noise; ln.enabled = true; ln.strength = 0.6f; ln.frequency = 0.2f;
                var rl = lf.rotationOverLifetime; rl.enabled = true; rl.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
                FadeInOut(lf, 1f);
            }
        }

        // ---------------- lighting, sky, fog
        light.color = SunCol;
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.82f;
        RenderSettings.sun = light;
        RenderSettings.skybox = skyMat;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AmbSky;
        RenderSettings.ambientEquatorColor = AmbEquator;
        RenderSettings.ambientGroundColor = AmbGround;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Horizon;
        RenderSettings.fogStartDistance = 30f;
        RenderSettings.fogEndDistance = 210f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        DynamicGI.UpdateEnvironment();

        // ---------------- camera + post
        var cam = Camera.main;
        var camData = cam.GetComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;
        cam.farClipPlane = 400f;

        var profilePath = ArtRoot + $"/Settings/VP_{sceneKey}_Forest.asset";
        AssetDatabase.DeleteAsset(profilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);
        var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
        var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.4f); bloom.threshold.Override(1.05f); bloom.scatter.Override(0.72f);
        var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.24f); vig.smoothness.Override(0.42f); vig.color.Override(Hex("#16241C"));
        var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(0.25f); ca.contrast.Override(10f); ca.saturation.Override(12f);
        var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(7f); wb.tint.Override(-3f);
        var smh = profile.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.Override(new Vector4(0.92f, 1.0f, 1.06f, 0f));
        smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.94f, 0f));
        foreach (var comp in profile.components) { comp.name = comp.GetType().Name; AssetDatabase.AddObjectToAsset(comp, profile); }
        EditorUtility.SetDirty(profile);
        var volGo = new GameObject("Global Volume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true; vol.priority = 1f; vol.sharedProfile = profile;

        // ---------------- save meshes into one asset
        var meshPath = ArtRoot + $"/Meshes/{sceneKey}_ForestMeshes.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(meshAssets[0], meshPath);
        foreach (var m in meshAssets.Skip(1)) AssetDatabase.AddObjectToAsset(m, meshPath);
        AssetDatabase.SaveAssets();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        long verts = meshAssets.Sum(m => (long)m.vertexCount);
        log.AppendLine($"meshes {meshAssets.Count} totalVerts {verts}");
        return log.ToString();
    }

    static void AddScroll(GameObject go, Vector2 speed)
    {
        var comp = go.AddComponent<ScrollingTexture>();
        var so = new SerializedObject(comp);
        so.FindProperty("speed").vector2Value = speed;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static ParticleSystem Particles(string name, Transform parent, Vector3 localPos, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em = ps.emission; em.enabled = true;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static void FadeInOut(ParticleSystem ps, float peakAlpha)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.2f), new GradientAlphaKey(peakAlpha, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }
}
