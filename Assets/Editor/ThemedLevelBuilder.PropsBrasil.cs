using System.Collections.Generic;
using UnityEngine;

// Props dos temas "brasileiros" novos (Amazônia, Noronha, Lago Azul, Urupema, São Paulo, Rio).
// Mesmo estilo dos outros: low-poly, cores pelo atlas do tema (papéis R).
public static partial class ThemedLevelBuilder
{
    // ------------------------------------------------------------ Amazônia

    // Sumaúma / árvore emergente: tronco alto e reto, sapopemas (raízes tabulares) na base,
    // galhos quase horizontais no alto, copa larga em guarda-chuva e cipós pendurados.
    static MB Kapok(float H, int seed) => WithSeed(seed, mb =>
    {
        float r = H * 0.035f;
        Trunk(mb, H * 0.78f, r, r * 0.7f, 7, Vector3.zero, 0f);

        // sapopemas: placas triangulares saindo do tronco
        int fins = Ri(4, 6);
        float rot = Rf(0, 6.28f);
        for (int i = 0; i < fins; i++)
        {
            float a = rot + i * Mathf.PI * 2 / fins + Rf(-0.2f, 0.2f);
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var side = new Vector3(-dir.z, 0, dir.x) * r * 0.22f;
            float reach = r * Rf(3.2f, 4.5f), height = H * Rf(0.12f, 0.18f);
            var top = Vector3.up * height + dir * r * 0.6f;
            var foot = dir * reach + Vector3.down * 0.3f;
            var inner = dir * r * 0.5f + Vector3.down * 0.3f;
            var c = UVr(R.Bark, 2, 4);
            mb.Tri(inner - side, top - side, foot - side, c, -side);
            mb.Tri(inner + side, foot + side, top + side, c, side);
            mb.Tri(top - side, top + side, foot + side, UVr(R.Bark, 3, 5), dir + Vector3.up);
            mb.Tri(top - side, foot + side, foot - side, UVr(R.Bark, 3, 5), dir + Vector3.up);
        }

        // galhos e copa
        var crownBase = Vector3.up * H * 0.78f;
        int branches = Ri(4, 6);
        var tips = new List<Vector3>();
        for (int b = 0; b < branches; b++)
        {
            float a = b * Mathf.PI * 2 / branches + Rf(-0.3f, 0.3f);
            var dir = new Vector3(Mathf.Cos(a), Rf(0.15f, 0.35f), Mathf.Sin(a)).normalized;
            var from = crownBase + Vector3.down * H * Rf(0f, 0.08f);
            var to = from + dir * H * Rf(0.2f, 0.3f);
            var s1 = Vector3.Cross(dir, Vector3.up).normalized; var u1 = Vector3.Cross(s1, dir);
            Vector3[] Ring(Vector3 c, float rr) { var ring = new Vector3[5]; for (int j = 0; j < 5; j++) { float t = j * Mathf.PI * 2 / 5; ring[j] = c + (s1 * Mathf.Cos(t) + u1 * Mathf.Sin(t)) * rr; } return ring; }
            Loft(mb, new List<Vector3[]> { Ring(from, r * 0.45f), Ring(to, r * 0.22f) }, (i, j, n) => UVr(R.Bark, 2, 4), from, to);
            tips.Add(to);
        }
        foreach (var t in tips)
        {
            float s = H * Rf(0.13f, 0.18f);
            bool light = Chance(0.45f);
            Blob(mb, t + Vector3.up * s * 0.15f, new Vector3(s, s * 0.38f, s), 0.18f, 1, n =>
                UV(light ? R.LeafLight : R.PineMid, Mathf.Clamp(Mathf.RoundToInt(2 + (n.y + 1) * 1.8f + Rf(-0.7f, 0.7f)), 0, 7)));
        }
        Blob(mb, crownBase + Vector3.up * H * 0.1f, new Vector3(H * 0.2f, H * 0.09f, H * 0.2f), 0.15f, 1, n =>
            UV(R.PineDark, Mathf.Clamp(Mathf.RoundToInt(2 + (n.y + 1) * 1.8f), 0, 7)));

        // cipós: fitas finas caindo das pontas dos galhos
        foreach (var t in tips)
        {
            if (!Chance(0.7f)) continue;
            var top = t + new Vector3(Rf(-0.5f, 0.5f), -H * 0.03f, Rf(-0.5f, 0.5f));
            float len = H * Rf(0.25f, 0.55f);
            var sway = new Vector3(Rf(-0.4f, 0.4f), 0, Rf(-0.4f, 0.4f));
            var w = new Vector3(0.05f, 0, 0.05f);
            var mid = top + Vector3.down * len * 0.5f + sway;
            var end = top + Vector3.down * len + sway * 0.4f;
            var c = UVr(R.Fern, 2, 4);
            mb.TriRaw(top - w, top + w, mid + w, c); mb.TriRaw(top - w, mid + w, mid - w, c);
            mb.TriRaw(mid - w, mid + w, end, c);
        }
    });

    // Açaizeiro: touceira de estipes finos e altos, cada um com folhas caídas e um cacho escuro.
    static MB Acai(float H, int seed) => WithSeed(seed, mb =>
    {
        int stems = Ri(2, 4);
        for (int s = 0; s < stems; s++)
        {
            float h = H * Rf(0.75f, 1.05f);
            var basePos = new Vector3(Rf(-0.5f, 0.5f), 0, Rf(-0.5f, 0.5f));
            var bend = new Vector3(Rf(-1, 1), 0, Rf(-1, 1)).normalized * h * Rf(0.05f, 0.14f);
            int segs = 5;
            var pts = new List<Vector3>();
            for (int i = 0; i <= segs; i++) { float t = i / (float)segs; pts.Add(basePos + bend * t * t + Vector3.up * (h * t - 0.3f)); }
            var rings = new List<Vector3[]>();
            for (int i = 0; i <= segs; i++)
            {
                float rr = H * 0.014f * (1f - 0.25f * i / segs);
                var ring = new Vector3[5];
                for (int j = 0; j < 5; j++) { float a = j * Mathf.PI * 2 / 5; ring[j] = pts[i] + new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr); }
                rings.Add(ring);
            }
            Loft(mb, rings, (i, j, n) => UV(R.Bark, i % 2 == 0 ? 3 : 4), basePos, basePos + Vector3.up);
            var crown = pts[segs];
            // palmito (parte verde lisa no alto)
            Blob(mb, crown + Vector3.down * h * 0.04f, new Vector3(0.12f, h * 0.06f, 0.12f), 0.05f, 0, n => UVr(R.PineMid, 3, 5));
            int fronds = Ri(7, 9);
            for (int f = 0; f < fronds; f++)
            {
                float ang = f * Mathf.PI * 2 / fronds + Rf(-0.2f, 0.2f);
                var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                var side = Vector3.Cross(Vector3.up, dir);
                float len = H * Rf(0.28f, 0.36f), w = H * 0.03f;
                Vector3 P(float t) => crown + dir * len * t + Vector3.up * (len * (0.5f * t - 1.1f * t * t));
                for (int k = 0; k < 4; k++)
                {
                    float t0 = k / 4f, t1 = (k + 1) / 4f;
                    float w0 = w * (1f - t0 * 0.6f), w1 = w * (1f - t1 * 0.6f);
                    var col = UV(k % 2 == 0 ? R.PineMid : R.LeafLight, Ri(3, 5));
                    mb.TriRaw(P(t0) - side * w0, P(t0), P(t1), col); mb.TriRaw(P(t0) - side * w0, P(t1), P(t1) - side * w1, col);
                    mb.TriRaw(P(t0), P(t0) + side * w0, P(t1) + side * w1, col); mb.TriRaw(P(t0), P(t1) + side * w1, P(t1), col);
                }
            }
            // cacho de açaí (escuro)
            if (Chance(0.7f))
                Blob(mb, crown + Vector3.down * h * 0.09f + bend.normalized * 0.15f, new Vector3(0.18f, 0.28f, 0.18f), 0.2f, 0, n => UVr(R.RiverBed, 0, 2));
        }
    });

    // Helicônia: folhas grandes em remo e brácteas vermelhas em zigue-zague com pontas amarelas.
    static MB Heliconia(int seed) => WithSeed(seed, mb =>
    {
        int leaves = Ri(3, 5);
        for (int i = 0; i < leaves; i++)
        {
            float a = Rf(0, 6.28f);
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var side = Vector3.Cross(Vector3.up, dir);
            float h = Rf(0.7f, 1.2f), len = Rf(0.6f, 0.9f), w = Rf(0.14f, 0.2f);
            var stalkTop = dir * 0.15f + Vector3.up * h;
            mb.TriRaw(Vector3.down * 0.1f - side * 0.02f, Vector3.down * 0.1f + side * 0.02f, stalkTop, UVr(R.Fern, 2, 4));
            var tip = stalkTop + dir * len + Vector3.up * len * 0.35f;
            var mid = (stalkTop + tip) * 0.5f;
            var c = UVr(R.LeafLight, 4, 6);
            mb.TriRaw(stalkTop, mid + side * w, tip, c);
            mb.TriRaw(stalkTop, tip, mid - side * w, UVr(R.GrassLight, 4, 6));
        }
        // inflorescência
        float fh = Rf(0.9f, 1.3f);
        var p = new Vector3(Rf(-0.1f, 0.1f), 0, Rf(-0.1f, 0.1f));
        int bracts = Ri(4, 6);
        for (int k = 0; k < bracts; k++)
        {
            float y = fh - k * 0.12f;
            float sx = k % 2 == 0 ? 1f : -1f;
            var b0 = p + Vector3.up * y;
            var b1 = b0 + new Vector3(0.16f * sx, 0.06f, 0f);
            var b2 = b0 + new Vector3(0.02f * sx, -0.1f, 0.04f);
            mb.TriRaw(b0, b1, b2, UVr(R.MushCap, 4, 6));
            mb.TriRaw(b1, b0 + Vector3.forward * 0.03f, b2, UVr(R.FlowerYellow, 4, 6));
        }
        mb.TriRaw(p - Vector3.right * 0.015f + Vector3.down * 0.1f, p + Vector3.right * 0.015f + Vector3.down * 0.1f, p + Vector3.up * fh, UVr(R.Fern, 2, 4));
    });

    // Bromélia: roseta de folhas em fita com o centro vermelho.
    static MB Bromeliad(int seed) => WithSeed(seed, mb =>
    {
        int leaves = Ri(8, 11);
        float rot = Rf(0, 6.28f);
        for (int i = 0; i < leaves; i++)
        {
            float a = rot + i * Mathf.PI * 2 / leaves;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var side = Vector3.Cross(Vector3.up, dir) * 0.06f;
            float len = Rf(0.35f, 0.55f), rise = Rf(0.25f, 0.45f);
            var b = Vector3.up * 0.05f;
            var m = dir * len * 0.5f + Vector3.up * rise;
            var t = dir * len + Vector3.up * rise * 0.8f;
            var c = UVr(i % 3 == 0 ? R.GrassLight : R.LeafLight, 4, 6);
            mb.TriRaw(b - side, b + side, m + side * 0.8f, c); mb.TriRaw(b - side, m + side * 0.8f, m - side * 0.8f, c);
            mb.TriRaw(m - side * 0.8f, m + side * 0.8f, t, c);
        }
        Blob(mb, Vector3.up * 0.18f, new Vector3(0.1f, 0.14f, 0.1f), 0.1f, 0, n => UVr(R.MushCap, 4, 6));
    });

    // ------------------------------------------------------------ Urupema (serra catarinense)

    // Araucária: tronco reto e nu, copa em candelabro (galhos em verticilos que sobem nas pontas,
    // com tufos escuros), formando uma "taça" achatada no alto.
    static MB Araucaria(float H, int seed) => WithSeed(seed, mb =>
    {
        float r = H * 0.028f;
        Trunk(mb, H * 0.92f, r, r * 0.55f, 6, Vector3.zero, 0f);
        int whorls = 3;
        for (int w = 0; w < whorls; w++)
        {
            float y = H * (0.68f + w * 0.1f);
            float len = H * (0.26f - w * 0.06f);
            int arms = Ri(5, 7);
            float rot = Rf(0, 6.28f);
            for (int a = 0; a < arms; a++)
            {
                float ang = rot + a * Mathf.PI * 2 / arms;
                var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                var from = Vector3.up * y;
                var mid = from + dir * len * 0.65f + Vector3.up * len * 0.05f;
                var tip = from + dir * len + Vector3.up * len * 0.35f;
                var s1 = Vector3.Cross(dir, Vector3.up).normalized * r * 0.25f;
                var c = UVr(R.Bark, 2, 4);
                mb.TriRaw(from - s1, from + s1, mid, c); mb.TriRaw(from + s1 + Vector3.up * 0.05f, from - s1 + Vector3.up * 0.05f, mid, c);
                mb.TriRaw(mid - s1 * 0.6f, mid + s1 * 0.6f, tip, c);
                // tufo de grimpas na ponta (achatado, virado para cima)
                Blob(mb, tip + Vector3.up * 0.15f, new Vector3(len * 0.32f, len * 0.16f, len * 0.32f), 0.15f, 0, n =>
                    UV(n.y > 0.3f ? R.PineMid : R.PineDark, Mathf.Clamp(Mathf.RoundToInt(2 + (n.y + 1) * 1.6f + Rf(-0.6f, 0.6f)), 0, 7)));
                Blob(mb, mid + Vector3.up * 0.1f, new Vector3(len * 0.2f, len * 0.1f, len * 0.2f), 0.15f, 0, n => UVr(R.PineDark, 2, 4));
            }
        }
        Blob(mb, Vector3.up * H * 0.95f, new Vector3(H * 0.06f, H * 0.04f, H * 0.06f), 0.1f, 0, n => UVr(R.PineMid, 3, 5));
    });

    // Cerca de madeira dos campos: dois mourões e duas travessas.
    static MB Fence(int seed) => WithSeed(seed, mb =>
    {
        float len = Rf(2.6f, 3.4f), h = Rf(0.9f, 1.1f);
        var c = UVr(R.WoodRing, 2, 4);
        foreach (float x in new[] { -len * 0.5f, len * 0.5f })
        {
            var rings = Prism(new Vector3(x, 0, 0), 4, new[] { (-0.3f, 0.07f), (h + 0.1f, 0.06f) }, 0.78f, 0f, 0.05f);
            Loft(mb, rings, (i, j, n) => UVr(R.Bark, 2, 4), new Vector3(x, 0, 0), new Vector3(x, 1, 0));
            Fan(mb, new Vector3(x, h + 0.12f, 0), rings[1], () => UVr(R.Bark, 3, 5), Vector3.up);
        }
        foreach (float y in new[] { h * 0.45f, h * 0.85f })
        {
            var a = new Vector3(-len * 0.55f, y, 0); var b = new Vector3(len * 0.55f, y + Rf(-0.04f, 0.04f), 0);
            var up = Vector3.up * 0.05f; var fw = Vector3.forward * 0.03f;
            mb.Quad(a - up - fw, b - up - fw, b + up - fw, a + up - fw, c, -Vector3.forward);
            mb.Quad(a + up + fw, b + up + fw, b - up + fw, a - up + fw, c, Vector3.forward);
            mb.Quad(a + up - fw, b + up - fw, b + up + fw, a + up + fw, c, Vector3.up);
        }
    });

    // ------------------------------------------------------------ São Paulo

    // Caixa alinhada aos eixos (6 faces).
    static void BoxMB(MB mb, Vector3 c, Vector3 size, Vector2 uv, Vector2? topUv = null)
    {
        var h = size * 0.5f;
        Vector3 P(float x, float y, float z) => c + new Vector3(x * h.x, y * h.y, z * h.z);
        mb.Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), uv, Vector3.forward);
        mb.Quad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), uv, Vector3.back);
        mb.Quad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), uv, Vector3.right);
        mb.Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), uv, Vector3.left);
        mb.Quad(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), topUv ?? uv, Vector3.up);
        mb.Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), uv, Vector3.down);
    }

    // Prédio: casco em faixas de andar (parede / faixa escura das janelas), às vezes com recuo,
    // caixa d'água e antena; e uma malha separada com as janelas acesas (algumas apagadas),
    // um pouco para fora da parede, desenhada com o material de janelas (acende/apaga).
    // A base desce bastante para não flutuar em encostas.
    static readonly Dictionary<int, (MB shell, MB windows)> buildingCache = new Dictionary<int, (MB, MB)>();

    static (MB shell, MB windows) BuildingPair(float H, int seed)
    {
        if (buildingCache.TryGetValue(seed, out var cached)) return cached;
        var keep = rng; rng = new System.Random(seed);
        var mb = new MB(); var win = new MB();
        float w = Rf(7f, 13f), d = Rf(7f, 13f), h = H * Rf(0.55f, 1.35f);
        R[] walls = { R.Cream, R.Rock, R.RockMoss, R.Dirt, R.WoodRing };
        R wall = walls[Ri(0, walls.Length - 1)];
        var winUv = UV(R.FlowerWhite, 7);
        void Box(float x0, float z0, float x1, float z1, float y0, float y1, bool windows)
        {
            var c = new[] { new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1) };
            var center = new Vector3((x0 + x1) * 0.5f, 0, (z0 + z1) * 0.5f);
            for (int f = 0; f < 4; f++)
            {
                var a = c[f]; var b = c[(f + 1) % 4];
                var mid = (a + b) * 0.5f; var outward = (mid - center).normalized;
                float y = y0;
                int band = 0;
                while (y < y1 - 0.01f)
                {
                    float hb = band == 0 ? Mathf.Min(y1 - y, 6f) : Mathf.Min(y1 - y, band % 2 == 0 ? 1.6f : 3.2f);
                    bool isWin = windows && band % 2 == 1 && band > 0;
                    var uv = isWin ? UV(R.RiverBed, Ri(3, 5)) : UV(wall, Ri(3, 5));
                    mb.Quad(a + Vector3.up * y, b + Vector3.up * y, b + Vector3.up * (y + hb), a + Vector3.up * (y + hb), uv, outward);
                    if (isWin && y + hb > 0f)
                    {
                        // janelas acesas: trechos da faixa, alguns apagados
                        int segs = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / 2.2f));
                        for (int k = 0; k < segs; k++)
                        {
                            if (!Chance(0.55f)) continue;
                            var p0 = Vector3.Lerp(a, b, (k + 0.15f) / segs) + outward * 0.05f;
                            var p1 = Vector3.Lerp(a, b, (k + 0.85f) / segs) + outward * 0.05f;
                            win.Quad(p0 + Vector3.up * (y + 0.5f), p1 + Vector3.up * (y + 0.5f), p1 + Vector3.up * (y + hb - 0.5f), p0 + Vector3.up * (y + hb - 0.5f), winUv, outward);
                        }
                    }
                    y += hb; band++;
                }
            }
            Fan(mb, new Vector3((x0 + x1) * 0.5f, y1, (z0 + z1) * 0.5f), new[] { c[0] + Vector3.up * y1, c[1] + Vector3.up * y1, c[2] + Vector3.up * y1, c[3] + Vector3.up * y1 }, () => UV(R.Rock, 2), Vector3.up);
        }
        float hw = w * 0.5f, hd = d * 0.5f;
        bool setback = Chance(0.45f) && h > 24f;
        float mainTop = setback ? h * Rf(0.65f, 0.8f) : h;
        Box(-hw, -hd, hw, hd, -8f, mainTop, true);
        if (setback) Box(-hw * 0.7f, -hd * 0.7f, hw * 0.7f, hd * 0.7f, mainTop, h, true);
        float top = h;
        if (Chance(0.7f))
        {
            float tx = Rf(-hw * 0.4f, hw * 0.4f), tz = Rf(-hd * 0.4f, hd * 0.4f);
            Box(tx - 1.2f, tz - 1.2f, tx + 1.2f, tz + 1.2f, top, top + 2.2f, false);
        }
        if (Chance(0.35f))
        {
            var rings = Prism(new Vector3(0, top, 0), 4, new[] { (0f, 0.15f), (h * 0.18f, 0.05f) }, 0.4f, 0f, 0f);
            Loft(mb, rings, (i2, j2, n2) => UV(R.Cream, 6), Vector3.up * top, Vector3.up * (top + 1));
        }
        rng = keep;
        var pair = (mb, win);
        buildingCache[seed] = pair;
        return pair;
    }

    // Poste de rua.
    static MB Lamp(int seed) => WithSeed(seed, mb =>
    {
        var rings = Prism(Vector3.zero, 5, new[] { (-0.3f, 0.09f), (5.5f, 0.06f) }, 0f, 0f, 0f);
        Loft(mb, rings, (i, j, n) => UV(R.Rock, 2), Vector3.zero, Vector3.up);
        mb.Quad(new Vector3(0, 5.4f, -0.05f), new Vector3(1.4f, 5.6f, -0.05f), new Vector3(1.4f, 5.7f, -0.05f), new Vector3(0, 5.5f, -0.05f), UV(R.Rock, 2), Vector3.back);
        Blob(mb, new Vector3(1.45f, 5.55f, 0), new Vector3(0.35f, 0.12f, 0.2f), 0f, 0, n => n.y < 0 ? UV(R.Cream, 7) : UV(R.Rock, 2));
    });

    // Coisas de laje: aparelho de ar e caixa d'água.
    static MB Rooftop(int seed) => WithSeed(seed, mb =>
    {
        BoxMB(mb, new Vector3(Rf(-0.3f, 0.3f), 0.3f, 0), new Vector3(1f, 0.7f, 0.8f), UVr(R.Cream, 4, 6));
        if (Chance(0.6f))
        {
            var rings = Prism(new Vector3(0.9f, 0, 0.3f), 8, new[] { (-0.1f, 0.45f), (0.9f, 0.45f) }, 0f, 0f, 0f);
            Loft(mb, rings, (i, j, n) => UV(R.RiverBed, 5), Vector3.zero, Vector3.up);
            Fan(mb, new Vector3(0.9f, 0.95f, 0.3f), rings[1], () => UV(R.RiverBed, 6), Vector3.up);
        }
    });

    // Carro (comprimento em z). Cor pela paleta.
    static MB Car(R color) => WithSeed(77 + (int)color, mb =>
    {
        BoxMB(mb, new Vector3(0, 0.6f, 0), new Vector3(1.8f, 0.6f, 4.2f), UV(color, 4), UV(color, 5));
        BoxMB(mb, new Vector3(0, 1.12f, -0.2f), new Vector3(1.55f, 0.5f, 2.2f), UV(R.RiverBed, 5), UV(color, 5));
        foreach (var (x, z) in new[] { (-0.85f, 1.3f), (0.85f, 1.3f), (-0.85f, -1.3f), (0.85f, -1.3f) })
            BoxMB(mb, new Vector3(x, 0.32f, z), new Vector3(0.25f, 0.64f, 0.64f), UV(R.Bark, 0));
    });

    // Viadutos com trânsito acompanhando o caminho pelos lados (no corredor sem prédios),
    // elevados acima do terreno.
    static void BuildSaoPaulo(Transform env)
    {
        var root = Child(env, "Traffic");
        var deck = new MB();
        var carMeshes = new[] { Reg(Car(R.Cream).Build("Car_0")), Reg(Car(R.MushCap).Build("Car_1")), Reg(Car(R.Rock).Build("Car_2")), Reg(Car(R.FlowerYellow).Build("Car_3")), Reg(Car(R.Bark).Build("Car_4")) };

        // caminho reamostrado a cada ~10 m
        var path = new List<Vector2>();
        for (int i = 0; i < mainPath.Count - 1; i++)
        {
            var a = mainPath[i]; var b = mainPath[i + 1];
            int steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 10f));
            for (int k = 0; k < steps; k++) path.Add(Vector2.Lerp(a, b, k / (float)steps));
        }
        path.Add(mainPath[mainPath.Count - 1]);
        // estica um pouco antes do início e depois do fim
        var d0 = (path[1] - path[0]).normalized; var d1 = (path[path.Count - 1] - path[path.Count - 2]).normalized;
        path.Insert(0, path[0] - d0 * 40f); path.Add(path[path.Count - 1] + d1 * 40f);

        int lanes = 0;
        foreach (float sideSign in new[] { -1f, 1f })
        {
            // linha central do viaduto, deslocada para o lado e suavizada
            var line = new List<Vector2>();
            for (int i = 0; i < path.Count; i++)
            {
                var dir = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                line.Add(path[i] + new Vector2(-dir.y, dir.x) * sideSign * 17f);
            }
            for (int pass = 0; pass < 2; pass++)
                for (int i = 1; i < line.Count - 1; i++) line[i] = (line[i - 1] + line[i] * 2f + line[i + 1]) * 0.25f;
            var ys = new float[line.Count];
            for (int i = 0; i < line.Count; i++) ys[i] = Mathf.Max(2f, MeshH(line[i].x, line[i].y) + 1.5f);
            for (int pass = 0; pass < 3; pass++)
                for (int i = 1; i < ys.Length - 1; i++) ys[i] = Mathf.Max(ys[i], (ys[i - 1] + ys[i + 1]) * 0.5f);

            var left = new List<Vector3>(); var right = new List<Vector3>();
            var laneA = new List<Vector3>(); var laneB = new List<Vector3>();
            for (int i = 0; i < line.Count; i++)
            {
                var dir = (line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 0)]).normalized;
                var n = new Vector3(-dir.y, 0, dir.x);
                var c = new Vector3(line[i].x, ys[i], line[i].y);
                left.Add(c - n * 5f); right.Add(c + n * 5f);
                laneA.Add(c - n * 2.4f + Vector3.up * 0.02f); laneB.Add(c + n * 2.4f + Vector3.up * 0.02f);
            }
            for (int i = 0; i < line.Count - 1; i++)
            {
                deck.Quad(left[i], left[i + 1], right[i + 1], right[i], UV(R.Rock, 2), Vector3.up);
                var down = Vector3.down * 0.8f;
                deck.Quad(left[i] + down, right[i] + down, right[i + 1] + down, left[i + 1] + down, UV(R.Rock, 1), Vector3.down);
                var nl = Vector3.Cross(Vector3.up, left[i + 1] - left[i]).normalized;
                deck.Quad(left[i] + down, left[i + 1] + down, left[i + 1] + Vector3.up * 0.5f, left[i] + Vector3.up * 0.5f, UV(R.Cream, 3), nl);
                deck.Quad(right[i] + down, right[i + 1] + down, right[i + 1] + Vector3.up * 0.5f, right[i] + Vector3.up * 0.5f, UV(R.Cream, 3), -nl);
                // faixa tracejada no meio
                var m0 = (left[i] + right[i]) * 0.5f + Vector3.up * 0.03f; var m1 = Vector3.Lerp(m0, (left[i + 1] + right[i + 1]) * 0.5f + Vector3.up * 0.03f, 0.4f);
                var w = (right[i] - left[i]).normalized * 0.12f;
                deck.Quad(m0 - w, m1 - w, m1 + w, m0 + w, UV(R.FlowerWhite, 6), Vector3.up);
                // pilar a cada dois trechos
                if (i % 2 == 0)
                {
                    var c = (left[i] + right[i]) * 0.5f;
                    float ground = MeshH(c.x, c.z);
                    if (ground < c.y - 1.5f)
                    {
                        var rings = Prism(new Vector3(c.x, ground - 1f, c.z), 6, new[] { (0f, 1.0f), (c.y - ground, 0.85f) }, 0.3f, 0f, 0f);
                        Loft(deck, rings, (a, b, nn) => UVr(R.Rock, 1, 3), new Vector3(c.x, 0, c.z), new Vector3(c.x, 1, c.z));
                    }
                }
            }
            foreach (var (pts, rev) in new[] { (laneA, false), (laneB, true) })
            {
                var list = new List<Vector3>(pts); if (rev) list.Reverse();
                var go = new GameObject($"Lane_{lanes++}");
                go.transform.SetParent(root, false);
                var lane = go.AddComponent<TrafficLane>();
                var so = new UnityEditor.SerializedObject(lane);
                var arrP = so.FindProperty("points"); arrP.arraySize = list.Count;
                for (int k = 0; k < list.Count; k++) arrP.GetArrayElementAtIndex(k).vector3Value = list[k];
                var arr = so.FindProperty("carMeshes"); arr.arraySize = carMeshes.Length;
                for (int k = 0; k < carMeshes.Length; k++) arr.GetArrayElementAtIndex(k).objectReferenceValue = carMeshes[k];
                so.FindProperty("material").objectReferenceValue = atlasMat;
                float len = 0f; for (int k = 1; k < list.Count; k++) len += Vector3.Distance(list[k - 1], list[k]);
                so.FindProperty("cars").intValue = Mathf.RoundToInt(len / 16f);
                so.FindProperty("speed").floatValue = Rf(9f, 13f);
                so.FindProperty("seed").intValue = lanes * 7;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        MeshObject("Viaducts", env, Reg(deck.Build("Viaducts")), atlasMat, Vector3.zero, Quaternion.identity, Vector3.one, true, true);
    }

    // ------------------------------------------------------------ Rio de Janeiro

    // Casinha de morro: caixa colorida com laje, às vezes um segundo andar.
    static MB House(int seed) => WithSeed(seed, mb =>
    {
        R[] colors = { R.MushCap, R.FlowerYellow, R.Cream, R.RiverBed, R.WoodRing, R.Fern, R.FlowerWhite };
        var c = colors[Ri(0, colors.Length - 1)];
        float w = Rf(2.2f, 3.4f), d = Rf(2.2f, 3.2f), h = Rf(2.2f, 2.8f);
        BoxMB(mb, new Vector3(0, h * 0.5f - 1f, 0), new Vector3(w, h + 2f, d), UV(c, Ri(3, 5)), UV(R.Rock, 3));
        BoxMB(mb, new Vector3(w * 0.25f, h - 0.4f, d * 0.51f), new Vector3(0.6f, 0.8f, 0.05f), UV(R.Bark, 1));
        if (Chance(0.45f))
        {
            var c2 = colors[Ri(0, colors.Length - 1)];
            BoxMB(mb, new Vector3(Rf(-0.3f, 0.3f), h + h * 0.4f, 0), new Vector3(w * 0.8f, h * 0.8f, d * 0.85f), UV(c2, Ri(3, 5)), UV(R.Rock, 3));
        }
    });

    // Cristo Redentor (≈ 26 m): pedestal, túnica, braços abertos e cabeça.
    static void Christ(MB mb, Vector3 b)
    {
        var stone = UV(R.Cream, 5);
        BoxMB(mb, b + new Vector3(0, 3f, 0), new Vector3(5f, 6f, 5f), UV(R.Cream, 4));
        var robe = Prism(b + Vector3.up * 6f, 8, new[] { (0f, 2.4f), (8f, 1.9f), (15f, 1.7f), (19f, 1.9f) }, 0.2f, 0f, 0f);
        Loft(mb, robe, (i, j, n) => UV(R.Cream, 4 + (j % 2)), b, b + Vector3.up);
        Fan(mb, b + Vector3.up * 25.4f, robe[robe.Count - 1], () => stone, Vector3.up);
        BoxMB(mb, b + new Vector3(0, 24.2f, 0), new Vector3(28f, 1.6f, 1.6f), stone);          // braços
        BoxMB(mb, b + new Vector3(0, 22.5f, 0), new Vector3(4.6f, 4f, 2.4f), stone);           // ombros/peito
        Blob(mb, b + new Vector3(0, 26.8f, 0), new Vector3(1.2f, 1.5f, 1.3f), 0f, 1, n => stone); // cabeça
    }

    // Corcovado com o Cristo e Pão de Açúcar (com o morro da Urca e o bondinho), ao fundo.
    static void BuildRio(Transform env)
    {
        var a0 = mainPath[0]; var a1 = mainPath[mainPath.Count - 1];
        var c2 = (a0 + a1) * 0.5f; var dir2 = (a1 - a0).normalized; var side2 = new Vector2(-dir2.y, dir2.x);
        Vector3 At(float fwd, float side) { var p = c2 + dir2 * fwd + side2 * side; return new Vector3(p.x, 0, p.y); }
        float y0 = T.waterY - 6f;
        var mb = new MB();

        // Corcovado: pico íngreme e coberto de mata, com o Cristo no alto
        var corc = At(260f, -110f);
        Mount(mb, corc, new[] { (80f, y0), (66f, T.waterY + 6f), (48f, 30f), (32f, 60f), (20f, 88f), (12f, 104f), (7f, 112f) }, 16, 96f, 0.12f);
        Christ(mb, corc + Vector3.up * 111f);

        // Pão de Açúcar e Urca: domos arredondados no mar
        var pao = At(210f, 150f);
        Mount(mb, pao, new[] { (32f, y0), (31f, T.waterY + 3f), (29f, 30f), (25f, 58f), (19f, 80f), (12f, 94f), (5f, 101f), (1.5f, 103f) }, 16, 22f, 0.05f);
        var urca = pao + new Vector3(side2.x, 0, side2.y) * -70f + new Vector3(dir2.x, 0, dir2.y) * -30f;
        Mount(mb, urca, new[] { (34f, y0), (32f, T.waterY + 3f), (28f, 18f), (20f, 34f), (10f, 43f), (3f, 46f) }, 14, 38f, 0.06f);

        // bondinho: cabo e uma cabine no meio
        var topPao = pao + Vector3.up * 102f; var topUrca = urca + Vector3.up * 46f;
        var cable = (topPao - topUrca); var n = Vector3.Cross(cable.normalized, Vector3.up).normalized * 0.12f;
        mb.Quad(topUrca - n, topPao - n, topPao + n, topUrca + n, UV(R.Bark, 1), Vector3.up);
        mb.Quad(topUrca + n, topPao + n, topPao - n, topUrca - n, UV(R.Bark, 1), Vector3.down);
        BoxMB(mb, Vector3.Lerp(topUrca, topPao, 0.45f) + Vector3.down * 2.2f, new Vector3(2.4f, 2.6f, 2.4f), UV(R.MushCap, 4));

        MeshObject("RioLandmarks", env, Reg(mb.Build("RioLandmarks")), atlasMat, Vector3.zero, Quaternion.identity, Vector3.one, true, true);
    }

    // ------------------------------------------------------------ Fernando de Noronha

    // Centro e raio aproximados do percurso (para pôr marcos ao fundo).
    static (Vector3 center, float radius) CourseBounds()
    {
        var b = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (!t.name.StartsWith("Platform_") || t.GetComponent<Collider>() == null) continue;
            if (first) { b = new Bounds(t.position, Vector3.zero); first = false; } else b.Encapsulate(t.position);
        }
        return (new Vector3(b.center.x, 0, b.center.z), Mathf.Max(b.extents.x, b.extents.z));
    }

    // Morro em anéis (perfil raio/altura), com cor por altura: mata embaixo, rocha em cima.
    static void Mount(MB mb, Vector3 c, (float r, float y)[] prof, int sides, float greenUntil, float jitter = 0.1f)
    {
        var rings = new List<Vector3[]>();
        float rot = Rf(0, 6.28f);
        foreach (var (r, y) in prof)
        {
            var ring = new Vector3[sides];
            for (int j = 0; j < sides; j++) { float a = rot + j * Mathf.PI * 2 / sides; float rr = r * Rf(1f - jitter, 1f + jitter); ring[j] = c + new Vector3(Mathf.Cos(a) * rr, y + Rf(-1f, 1f), Mathf.Sin(a) * rr); }
            rings.Add(ring);
        }
        for (int i = 0; i < rings.Count - 1; i++)
            for (int j = 0; j < sides; j++)
            {
                int k = (j + 1) % sides;
                var a = rings[i][j]; var b = rings[i][k]; var cc = rings[i + 1][k]; var d = rings[i + 1][j];
                var cen = (a + b + cc + d) * 0.25f;
                var hint = cen - (c + Vector3.up * cen.y) + Vector3.up * 0.3f;
                var uv = cen.y < greenUntil ? (Chance(0.15f) ? UVr(R.Rock, 1, 3) : UVr(R.PineMid, 2, 5)) : (Chance(0.25f) ? UVr(R.RockMoss, 2, 4) : UVr(R.Rock, 1, 4));
                mb.Quad(a, b, cc, d, uv, hint);
            }
        var top = rings[rings.Count - 1];
        var tc = Vector3.zero; foreach (var p in top) tc += p; tc /= top.Length;
        Fan(mb, tc + Vector3.up * 1.5f, top, () => UVr(R.Rock, 2, 4), Vector3.up);
    }

    // Morro do Pico (torre de rocha alta) e Morro Dois Irmãos (dois morros arredondados no mar).
    static void BuildNoronha(Transform env)
    {
        // a partir do início (o percurso desta fase é muito comprido): fica à vista da câmera inicial
        var startT = GameObject.Find("Platform_Start").transform;
        var center = new Vector3(startT.position.x, 0, startT.position.z);
        float d = 190f;
        float y0 = T.waterY - 6f;
        var mb = new MB();
        // Morro do Pico: à esquerda-frente do início
        var pico = center + new Vector3(-0.47f, 0, 0.88f).normalized * d;
        // base verde larga e uma torre de rocha íngreme por cima
        Mount(mb, pico, new[] { (50f, y0), (40f, T.waterY + 4f), (29f, 10f), (20f, 20f), (13f, 27f), (12f, 40f), (11f, 52f), (9f, 63f), (6f, 71f), (2.5f, 75f) }, 16, 24f, 0.16f);
        // Dois Irmãos: à direita
        var irmaos = center + new Vector3(0.53f, 0, 0.85f).normalized * (d + 25f);
        var side = new Vector3(0.85f, 0, -0.53f).normalized;
        foreach (float s in new[] { -1f, 1f })
        {
            var c = irmaos + side * s * 24f;
            float h = s < 0 ? 38f : 32f;
            Mount(mb, c, new[] { (24f, y0), (22f, T.waterY + 3f), (19f, h * 0.45f), (14f, h * 0.75f), (8f, h * 0.93f), (3f, h) }, 12, h * 0.85f, 0.08f);
        }
        MeshObject("NoronhaLandmarks", env, Reg(mb.Build("NoronhaLandmarks")), atlasMat, Vector3.zero, Quaternion.identity, Vector3.one, true, true);
    }
}
