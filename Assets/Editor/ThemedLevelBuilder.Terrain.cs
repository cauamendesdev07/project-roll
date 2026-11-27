using System.Collections.Generic;
using UnityEngine;

// Terreno: o vale segue a árvore de plataformas mais próximas (de Platform_Start a Platform_Finish).
// Perto do caminho o chão fica sempre abaixo de y = -7,5 (o FallDetector manda a bola de volta antes).
public static partial class ThemedLevelBuilder
{
    enum TerrainMode { Gorge, Cave, Wetland, Ocean, Dunes, None }

    struct Edge { public Vector2 a, b; public float wa, wb; }
    static readonly List<Edge> edges = new List<Edge>();
    static readonly List<Vector2> mainPath = new List<Vector2>();
    static Vector2 riverEnd, riverEndDir;
    static Theme T; // tema atual

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
        if (!T.waterfall) return false;
        var rel = p - riverEnd;
        float along = Vector2.Dot(rel, riverEndDir);
        float across = Mathf.Abs(Vector2.Dot(rel, new Vector2(-riverEndDir.y, riverEndDir.x)));
        return along > -4f && along < 50f && across < 8f;
    }

    static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
    static float Smoother(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6 - 15) + 10); }

    static float TerrainH(float x, float z)
    {
        float d = PathDist(x, z);
        float n1 = Mathf.PerlinNoise(x * 0.035f + 100f, z * 0.035f + 100f);
        float n2 = Mathf.PerlinNoise(x * 0.12f + 37f, z * 0.12f + 71f);
        switch (T.terrain)
        {
            case TerrainMode.Wetland:
            {
                float isl = Mathf.PerlinNoise(x * 0.05f + 13f, z * 0.05f + 7f);
                float h = -13.1f + n2 * 0.35f;
                if (isl > 0.52f) h = Mathf.Lerp(h, -11.7f + (isl - 0.52f) * 4f + n2 * 0.4f, Smooth((isl - 0.52f) / 0.06f));
                if (d < 4f) h = Mathf.Min(h, -12.9f);
                return h + Mathf.Max(0f, d - 70f) * 0.04f;
            }
            case TerrainMode.Ocean:
            {
                float isl = Mathf.PerlinNoise(x * 0.018f + 51f, z * 0.018f + 23f);
                float h = -16.5f + n1 * 2.5f + n2 * 0.8f;
                if (d > 18f && isl > 0.56f)
                {
                    float k = Smooth((isl - 0.56f) / 0.1f) * Smooth((d - 18f) / 14f);
                    h = Mathf.Lerp(h, -11.6f + (isl - 0.56f) * 70f * n1 + n2 * 1.5f, k);
                }
                if (d < 9f) h = Mathf.Min(h, -14.5f + n2);
                return h;
            }
            case TerrainMode.Dunes:
            {
                float ridge = 1f - Mathf.Abs(2f * Mathf.PerlinNoise(x * 0.028f + 5f, z * 0.02f + 9f) - 1f);
                float amp = Mathf.Lerp(4.2f, 16f, Smooth((d - 10f) / 45f));
                return -14.4f + ridge * ridge * amp + n2 * 0.4f;
            }
            case TerrainMode.None:
                return -60f;
        }

        // Gorge / Cave
        float floorH = -13.4f + n2 * 0.7f;
        if (d < 3f) return floorH - 0.4f;
        if (d < 11f) return floorH + Smooth((d - 3f) / 8f) * 3.4f + n2 * 0.6f * Smooth((d - 3f) / 8f);
        float topH = T.bankHeight + n1 * 3.5f + n2 * 1.0f + Mathf.Max(0f, d - 45f) * 0.16f;
        if (T.terrain == TerrainMode.Cave) topH += Mathf.Max(0f, d - 22f) * 2.2f; // paredes da gruta
        if (d < 23f) return Mathf.Lerp(-9.6f + n2 * 0.6f, topH, Smoother((d - 11f) / 12f));
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
                if (((i + j) & 1) == 0) { TerrainTri(mb, p00, p10, p11); TerrainTri(mb, p00, p11, p01); }
                else { TerrainTri(mb, p00, p10, p01); TerrainTri(mb, p10, p11, p01); }
            }
        return mb;
    }

    static void TerrainTri(MB mb, Vector3 a, Vector3 b, Vector3 c)
    {
        var n = Vector3.Cross(b - a, c - a).normalized;
        if (n.y < 0) n = -n;
        float hc = (a.y + b.y + c.y) / 3f;
        float w = T.waterY;
        Vector2 uv;
        switch (T.terrain)
        {
            case TerrainMode.Ocean:
                if (hc < w - 1.5f) uv = Chance(0.2f) ? UVr(R.Rock, 1, 3) : UVr(R.RiverBed, 1, 5);
                else if (hc < w + 1.1f) uv = UVr(R.Dirt, 3, 6);                 // praia
                else if (n.y < 0.62f) uv = UVr(R.Rock, 1, 5);
                else uv = Chance(0.15f) ? UVr(R.GrassLight, 2, 5) : UVr(R.GrassTop, 1, 5);
                break;
            case TerrainMode.Dunes:
                if (hc < w - 0.1f) uv = UVr(R.RiverBed, 2, 6);
                else if (hc < w + 0.35f) uv = UVr(R.Dirt, 3, 5);
                else uv = n.y < 0.8f ? UVr(R.GrassTop, 2, 4) : (Chance(0.3f) ? UVr(R.GrassLight, 4, 7) : UVr(R.GrassTop, 4, 7));
                break;
            case TerrainMode.Wetland:
                if (hc < w - 0.2f) uv = UVr(R.RiverBed, 1, 4);
                else if (hc < w + 0.3f) uv = Chance(0.5f) ? UVr(R.Dirt, 2, 4) : UVr(R.Fern, 2, 4);
                else uv = Chance(0.2f) ? UVr(R.GrassLight, 3, 5) : UVr(R.GrassTop, 2, 5);
                break;
            default:
                if (T.water != WaterMode.None && hc < w - 0.15f) uv = Chance(0.25f) ? UVr(R.Rock, 1, 3) : UVr(R.RiverBed, 1, 5);
                else if (T.water == WaterMode.None && hc < -12.6f) uv = Chance(0.3f) ? UVr(R.Dirt, 2, 4) : UVr(R.RiverBed, 2, 5);
                else if (n.y < 0.6f) uv = Chance(0.3f) ? UVr(R.RockMoss, 1, 4) : UVr(R.Rock, 2, 5);
                else if (n.y < 0.8f) { float r = (float)rng.NextDouble(); uv = r < 0.2f ? UVr(R.Dirt, 2, 4) : (r < 0.6f ? UVr(R.RockMoss, 2, 5) : UVr(R.Fern, 2, 4)); }
                else if (hc < -6f) uv = Chance(0.5f) ? UVr(R.Fern, 1, 4) : UVr(R.GrassTop, 1, 3);
                else uv = Chance(0.12f) ? UVr(R.GrassLight, 2, 4) : UVr(R.GrassTop, 1, 5);
                break;
        }
        mb.Tri(a, b, c, uv, Vector3.up);
    }
}
