using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Gera o visual da bola em Assets/Resources/Ball (carregado em tempo de jogo por BallVisuals):
// - Ball_Mesh: icosfera low-poly facetada (raio 0,5, igual à esfera padrão), faces coloridas
//   em três cores para a rotação ficar visível;
// - M_Ball: material URP Lit com a paleta;
// - M_BallFx: material de partícula (poeira e rastro).
// Só visual: o colisor e o Rigidbody da bola não mudam.
public static class BallAssets
{
    private const string Dir = "Assets/Resources/Ball";

    private static readonly Color[] Palette =
    {
        new Color(0.95f, 0.52f, 0.14f), // laranja
        new Color(0.97f, 0.92f, 0.82f), // creme
        new Color(0.10f, 0.42f, 0.47f), // azul-petróleo
        Color.white,
    };

    [MenuItem("Project Roll/Gerar visual da bola")]
    public static void Generate()
    {
        Directory.CreateDirectory(Dir);

        // Paleta 4×1 (sem filtro) e ponto suave para as partículas.
        var palette = SaveTexture("T_BallPalette", 4, 1, (x, y) => Palette[x], FilterMode.Point);
        var dot = SaveTexture("T_BallDot", 64, 64, (x, y) =>
        {
            float dx = (x + 0.5f) / 32f - 1f, dy = (y + 0.5f) / 32f - 1f;
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            return new Color(1, 1, 1, a * a);
        }, FilterMode.Bilinear);

        var mesh = BuildIcosphere(2, 0.5f);
        SaveAsset(mesh, $"{Dir}/Ball_Mesh.asset");

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var ball = LoadOrCreate($"{Dir}/M_Ball.mat", lit);
        ball.SetTexture("_BaseMap", palette);
        ball.SetColor("_BaseColor", Color.white);
        ball.SetFloat("_Smoothness", 0.35f);
        ball.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(ball);

        var unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var fx = LoadOrCreate($"{Dir}/M_BallFx.mat", unlit);
        fx.SetFloat("_Surface", 1f);
        fx.SetFloat("_Blend", 0f);
        fx.SetFloat("_ZWrite", 0f);
        fx.SetFloat("_Cull", 0f);
        fx.SetOverrideTag("RenderType", "Transparent");
        fx.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        fx.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        fx.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        fx.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        fx.SetTexture("_BaseMap", dot);
        fx.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(fx);

        AssetDatabase.SaveAssets();
        Debug.Log("Visual da bola gerado em " + Dir);
    }

    // Icosfera com faces planas; cada face herda a cor da face do icosaedro de origem.
    private static Mesh BuildIcosphere(int subdivisions, float radius)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new List<Vector3>
        {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
            new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
            new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
        };
        int[] f =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
        };

        // Três cores sem repetir entre faces vizinhas (guloso; o icosaedro precisa de 3).
        int faces = f.Length / 3;
        var color = new int[faces];
        for (int a = 0; a < faces; a++)
        {
            var used = new HashSet<int>();
            for (int b = 0; b < a; b++)
                if (SharesEdge(f, a, b)) used.Add(color[b]);
            int c = 0;
            while (used.Contains(c)) c++;
            color[a] = c % 3;
        }

        var tris = new List<(Vector3 a, Vector3 b, Vector3 c, int col)>();
        for (int i = 0; i < faces; i++)
            tris.Add((v[f[i * 3]].normalized, v[f[i * 3 + 1]].normalized, v[f[i * 3 + 2]].normalized, color[i]));

        for (int s = 0; s < subdivisions; s++)
        {
            var next = new List<(Vector3, Vector3, Vector3, int)>();
            foreach (var (a, b, c, col) in tris)
            {
                Vector3 ab = ((a + b) * 0.5f).normalized, bc = ((b + c) * 0.5f).normalized, ca = ((c + a) * 0.5f).normalized;
                next.Add((a, ab, ca, col));
                next.Add((b, bc, ab, col));
                next.Add((c, ca, bc, col));
                next.Add((ab, bc, ca, col));
            }
            tris = next;
        }

        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var idx = new List<int>();
        foreach (var (a, b, c, col) in tris)
        {
            // Na Unity a face da frente é a do lado para onde Cross(b - a, c - a) aponta: deixa para fora.
            Vector3 n = Vector3.Cross(b - a, c - a);
            var pts = Vector3.Dot(n, a + b + c) > 0f ? new[] { a, b, c } : new[] { a, c, b };
            Vector3 normal = n.normalized * Mathf.Sign(Vector3.Dot(n, a + b + c)); // normal plana da face
            Vector2 uv = new Vector2((col + 0.5f) / Palette.Length, 0.5f);
            foreach (var p in pts)
            {
                idx.Add(verts.Count);
                verts.Add(p * radius);
                normals.Add(normal);
                uvs.Add(uv);
            }
        }

        var mesh = new Mesh { name = "Ball_Mesh" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    private static bool SharesEdge(int[] f, int a, int b)
    {
        int shared = 0;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                if (f[a * 3 + i] == f[b * 3 + j]) shared++;
        return shared >= 2;
    }

    private static Texture2D SaveTexture(string name, int w, int h, System.Func<int, int, Color> pixel, FilterMode filter)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, pixel(x, y));
        tex.Apply();
        string path = $"{Dir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.filterMode = filter;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.alphaIsTransparency = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void SaveAsset(Object obj, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null && obj is Mesh m)
        {
            EditorUtility.CopySerialized(m, existing);
            EditorUtility.SetDirty(existing);
            return;
        }
        AssetDatabase.CreateAsset(obj, path);
    }

    private static Material LoadOrCreate(string path, Shader shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;
        return m;
    }
}
