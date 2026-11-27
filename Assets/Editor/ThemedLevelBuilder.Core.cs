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

// Gerador de cenários temáticos low-poly do Project Roll (base compartilhada).
// Partes: Core (malhas, ruído, materiais), Props (árvores e objetos), Platforms, Terrain, Themes e Build.
public static partial class ThemedLevelBuilder
{
    static Random rng = new Random(1234);
    static float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static int Ri(int a, int bInclusive) => rng.Next(a, bInclusive + 1);
    static bool Chance(float p) => rng.NextDouble() < p;

    // ---------------------------------------------------------------- palette (one atlas per theme; roles keep the same meaning)
    enum R { GrassTop, GrassLight, PineDark, PineMid, LeafLight, Bark, WoodRing, Rock, RockMoss, Dirt, RiverBed, MushCap, Cream, FlowerYellow, FlowerWhite, Fern }
    static string[] AtlasHex = new string[16];
    static R tuftBase = R.GrassTop, tuftTip = R.GrassLight, bushAlt = R.GrassTop, canopyAlt = R.GrassTop;
    const int Shades = 8;
    const int Roles = 16;

    static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

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
                return UV(warm ? R.LeafLight : canopyAlt, sh);
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
                UV(fern ? R.Fern : bushAlt, Mathf.Clamp(Mathf.RoundToInt(1 + (n.y + 1) * 1.6f + Rf(-0.7f, 0.7f)), 0, 7)));
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
            var c1 = UVr(tuftBase, 2, 4);
            mb.TriRaw(b0, b1, m1, c1);
            mb.TriRaw(b0, m1, m0, c1);
            mb.TriRaw(m0, m1, tip, UVr(tuftTip, 4, 7));
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
                mb.TriRaw(head + Vector3.up * 0.015f, ring[(j + 1) % p], ring[j], UVr(petal, 4, 7));
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
