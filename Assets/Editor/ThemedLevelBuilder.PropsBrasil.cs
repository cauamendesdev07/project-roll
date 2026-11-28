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
