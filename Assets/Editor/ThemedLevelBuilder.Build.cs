using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = System.Random;

// Monta o cenário temático de uma fase. Não muda a jogabilidade: colisores, scripts e posições das
// plataformas ficam iguais; só o visual delas é trocado e o cenário é criado em volta.
// Rodar de novo substitui o cenário anterior (resultado determinístico por fase).
public static partial class ThemedLevelBuilder
{
    const string ArtRoot = "Assets/Art/Themes";
    const string SharedDir = "Assets/Art/Shared";
    const string EnvName = "Scenery";
    static float pillarBottom = -13.9f;

    [MenuItem("Tools/Project Roll/Cenários/Gerar cenário na cena aberta")]
    static void BuildActiveSceneMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Cenários", "Saia do Play Mode antes de gerar o cenário.", "OK"); return; }
        var scene = EditorSceneManager.GetActiveScene();
        var digits = new string(Path.GetFileNameWithoutExtension(scene.path).Where(char.IsDigit).ToArray());
        if (!scene.name.StartsWith("Level_") || !int.TryParse(digits, out int level)) { EditorUtility.DisplayDialog("Cenários", "Abra a cena de uma fase (Level_XX).", "OK"); return; }
        var th = ThemeFor(level);
        if (!EditorUtility.DisplayDialog("Cenários", $"Gerar (ou regerar) o cenário \"{th.title}\" em {scene.name}?\n\nO visual, a luz e o céu desta cena serão substituídos. Colisores e jogabilidade não mudam.", "Gerar", "Cancelar")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log("[ThemedLevelBuilder] " + Build(scene.path, level));
    }

    [MenuItem("Tools/Project Roll/Cenários/Gerar cenários de todas as fases")]
    static void BuildAllMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorUtility.DisplayDialog("Cenários", "Gerar (ou regerar) o cenário das fases 1 a 10?", "Gerar", "Cancelar")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        for (int i = 1; i <= 10; i++) Debug.Log("[ThemedLevelBuilder] " + BuildLevel(i));
    }

    public static string BuildLevel(int level) => Build($"Assets/Scenes/Level_{level:00}.unity", level);

    // ------------------------------------------------------------------ instanced decor
    class Group { public Mesh mesh; public Material mat; public bool shadows; public List<InstancedDecor.Instance> list = new List<InstancedDecor.Instance>(); public Bounds bounds; public bool has; }
    static readonly Dictionary<string, Group> groups = new Dictionary<string, Group>();

    static void AddInst(Mesh mesh, Material mat, bool shadows, Vector3 pos, Quaternion rot, float scale)
    {
        string key = $"{mesh.name}|{mat.name}|{shadows}|{Mathf.FloorToInt(pos.x / 100f)}|{Mathf.FloorToInt(pos.z / 100f)}";
        if (!groups.TryGetValue(key, out var g)) { g = new Group { mesh = mesh, mat = mat, shadows = shadows }; groups[key] = g; }
        g.list.Add(new InstancedDecor.Instance { position = pos, rotation = rot, scale = scale });
        var b = new Bounds(pos + rot * (mesh.bounds.center * scale), mesh.bounds.size * scale * 1.5f + Vector3.one * 0.5f);
        if (!g.has) { g.bounds = b; g.has = true; } else g.bounds.Encapsulate(b);
    }

    static int EmitGroups(Transform parent)
    {
        int count = 0;
        foreach (var kv in groups.OrderBy(k => k.Key))
        {
            var g = kv.Value;
            var go = new GameObject($"Decor_{g.mesh.name}");
            go.transform.SetParent(parent, false);
            go.AddComponent<InstancedDecor>().SetData(g.mesh, g.mat, g.shadows, g.bounds, g.list.ToArray());
            count += g.list.Count;
        }
        return count;
    }

    // ------------------------------------------------------------------ props
    static readonly Dictionary<string, Mesh[]> props = new Dictionary<string, Mesh[]>();
    static Material atlasMat, foliageMat, glowMat, fakeMat, thinMat, lanternMat, fireflyMat, pollenMat, foamMat, leafMat;

    static Mesh[] Prop(string kind)
    {
        if (props.TryGetValue(kind, out var m)) return m;
        int baseSeed = Math.Abs(kind.GetHashCode()) % 10000;
        Func<int, MB> f;
        int n = 3;
        switch (kind)
        {
            case "pine": f = s => Pine(10f, s); n = 4; break;
            case "broadleaf": f = s => RoundTree(8f, s); break;
            case "cactus": f = s => Cactus(6f, s); n = 4; break;
            case "drytree": f = s => DryTree(7f, s, true); break;
            case "drybare": f = s => DryTree(7f, s, false); break;
            case "flattop": f = s => FlatTopTree(8f, s); n = 4; break;
            case "palm": f = s => Palm(9f, s); n = 4; break;
            case "termite": f = s => TermiteMound(2.4f, s); break;
            case "snowpine": f = s => SnowPine(10f, s); n = 4; break;
            case "stalagmite": f = s => Stalagmite(7f, s); n = 4; break;
            case "basalt": f = s => BasaltColumns(6f, s); break;
            case "crystal": f = s => Crystals(3.5f, s); n = 4; break;
            case "crystalsmall": f = s => Crystals(0.8f, s); break;
            case "basaltsmall": f = s => BasaltColumns(1.2f, s); break;
            case "tuft": f = s => Tuft(s, 1.1f); n = 4; break;
            case "bush": f = s => Bush(s, 1f); break;
            case "flower": f = s => Flowers(s); break;
            case "rock": f = s => RockMeshLow(s, new Vector3(1.1f, 0.65f, 1f)); n = 5; break;
            case "rockbig": f = s => RockMesh(s, new Vector3(1.1f, 0.7f, 1f)); n = 5; break;
            case "mushroom": f = s => Mushrooms(s); break;
            case "reeds": f = s => Reeds(s); break;
            case "lily": f = s => LilyPad(s); n = 4; break;
            case "smallcactus": f = s => Cactus(1.5f, s); break;
            case "log": f = s => Log(s, 4.5f, 0.45f); break;
            case "island": f = s => FloatingIsland(1f, s); n = 6; break;
            case "kapok": f = s => Kapok(18f, s); n = 3; break;
            case "acai": f = s => Acai(11f, s); n = 4; break;
            case "heliconia": f = s => Heliconia(s); n = 4; break;
            case "bromeliad": f = s => Bromeliad(s); n = 3; break;
            default: throw new Exception("prop desconhecido " + kind);
        }
        m = new Mesh[n];
        for (int i = 0; i < n; i++) m[i] = Reg(f(baseSeed + i * 31).Build($"{T.key}_{kind}_{i}"));
        props[kind] = m;
        return m;
    }

    static (float, float) PropScale(string kind)
    {
        switch (kind)
        {
            case "pine": return (0.85f, 1.45f);
            case "snowpine": return (0.8f, 1.5f);
            case "stalagmite": return (0.6f, 1.7f);
            case "basalt": return (0.7f, 1.5f);
            case "crystal": return (0.6f, 1.4f);
            case "tuft": return (0.9f, 1.5f);
            case "bush": return (0.9f, 1.8f);
            case "flower": return (1f, 1.4f);
            case "rock": return (0.5f, 1.6f);
            case "rockbig": return (0.8f, 2.2f);
            case "mushroom": return (1.4f, 2.2f);
            case "crystalsmall": return (0.8f, 1.6f);
            case "basaltsmall": return (0.8f, 1.6f);
            case "lily": return (0.8f, 1.4f);
            case "termite": return (0.7f, 1.4f);
            case "kapok": return (0.8f, 1.25f);
            case "acai": return (0.8f, 1.3f);
            case "heliconia": return (0.9f, 1.5f);
            case "bromeliad": return (0.8f, 1.4f);
            default: return (0.8f, 1.3f);
        }
    }

    static bool IsGlow(string kind) => kind == "crystal" || kind == "crystalsmall" || (kind == "mushroom" && T.glowMushrooms);
    static bool IsTwoSided(string kind) => kind == "tuft" || kind == "flower" || kind == "reeds" || kind == "palm"
        || kind == "kapok" || kind == "acai" || kind == "heliconia" || kind == "bromeliad";
    static Material PropMat(string kind) => IsGlow(kind) ? glowMat : (IsTwoSided(kind) ? foliageMat : atlasMat);
    static Mesh PickMesh(string kind) { var v = Prop(kind); return v[Ri(0, v.Length - 1)]; }

    static void PlaceProp(string kind, Vector3 pos, Vector3 normal, bool shadows, float extraScale = 1f, float alignToNormal = 0.5f)
    {
        var (a, b) = PropScale(kind);
        float s = Rf(a, b) * extraScale;
        var rot = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, normal, alignToNormal)) * Quaternion.Euler(0, Rf(0, 360), 0);
        if (kind == "rock" || kind == "rockbig") { pos += Vector3.down * 0.25f * s; rot = Quaternion.Euler(Rf(-15, 15), Rf(0, 360), Rf(-15, 15)); }
        AddInst(PickMesh(kind), PropMat(kind), shadows, pos, rot, s);
    }

    static string PickTree()
    {
        float total = T.treeWeights.Sum(), r = Rf(0, total);
        for (int i = 0; i < T.trees.Length; i++) { r -= T.treeWeights[i]; if (r <= 0) return T.trees[i]; }
        return T.trees[T.trees.Length - 1];
    }

    static Texture2D SharedTexture(string name, int w, int h, Func<int, int, Color> px, Action<TextureImporter> setup)
    {
        string path = $"{SharedDir}/{name}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return existing != null ? existing : SavePng(path, w, h, px, setup);
    }

    class PlatInfo { public Transform t; public Vector3 size; public float top, bottom; public Rect rect; }

    // ================================================================== BUILD
    public static string Build(string scenePath, int level)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Saia do Play Mode antes de gerar o cenário.");
        T = ThemeFor(level);
        AtlasHex = T.atlas;
        tuftBase = T.tuftBase; tuftTip = T.tuftTip; bushAlt = T.bushAlt; canopyAlt = T.canopyAlt;
        rng = new Random(1234 + (level - 1) * 7919);
        meshAssets.Clear(); groups.Clear(); props.Clear();
        pillarBottom = T.terrain == TerrainMode.Ocean ? -17.5f : -13.9f;
        var log = new System.Text.StringBuilder($"Level_{level:00} ({T.title}): ");

        string dir = $"{ArtRoot}/{T.key}";
        foreach (var d in new[] { ArtRoot, dir, SharedDir }) Directory.CreateDirectory(d);
        AssetDatabase.Refresh();

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var light = Object.FindObjectsByType<Light>().First(l => l.type == LightType.Directional);
        light.transform.rotation = Quaternion.Euler(T.sunElev, T.sunYaw, 0f);
        Vector3 sunDir = -light.transform.forward;

        // ---------------- textures
        var ripple = SharedTexture("T_WaterRipple", 256, 256, (x, y) =>
        {
            float u = x / 256f, v = y / 256f;
            float f = Fbm(u, v, 4, 4, 3);
            float ridge = 1f - Mathf.Abs(2f * Fbm(u, v, 3, 3, 7) - 1f);
            float foam = Mathf.Pow(ridge, 7f);
            float val = 0.85f + 0.1f * f + 0.25f * foam;
            return new Color(val, val, val, Mathf.Clamp01(0.82f + 0.12f * f + 0.35f * foam));
        }, imp => { imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; });
        var normal = SharedTexture("T_WaterNormal", 256, 256, (x, y) =>
        {
            float u = x / 256f, v = y / 256f, e = 1f / 256f;
            float H(float a, float b) => Fbm(a, b, 4, 4, 5);
            float dx = (H(u + e, v) - H(u - e, v)) * 18f, dy = (H(u, v + e) - H(u, v - e)) * 18f;
            var nn = new Vector3(-dx, -dy, 1f).normalized;
            return new Color(nn.x * 0.5f + 0.5f, nn.y * 0.5f + 0.5f, nn.z * 0.5f + 0.5f, 1f);
        }, imp => { imp.textureType = TextureImporterType.NormalMap; imp.wrapMode = TextureWrapMode.Repeat; });
        var mistTex = SharedTexture("T_Mist", 256, 256, (x, y) =>
        {
            float f = Fbm(x / 256f, y / 256f, 3, 4, 23);
            return new Color(1, 1, 1, Smooth((f - 0.35f) / 0.4f));
        }, imp => { imp.wrapMode = TextureWrapMode.Repeat; });
        var dot = SharedTexture("T_SoftDot", 64, 64, (x, y) =>
        {
            float dd = new Vector2(x - 31.5f, y - 31.5f).magnitude / 32f;
            return new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - dd), 2.2f));
        }, imp => { imp.wrapMode = TextureWrapMode.Clamp; imp.alphaIsTransparency = true; });
        var leafTex = SharedTexture("T_Leaf", 64, 64, (x, y) =>
        {
            float u = (x - 31.5f) / 32f, v = (y - 31.5f) / 32f;
            float a = Mathf.Abs(u) < 0.55f * (1f - v * v) ? 1f : 0f;
            return Mathf.Abs(u) < 0.05f ? new Color(0.82f, 0.82f, 0.82f, a) : new Color(1, 1, 1, a);
        }, imp => { imp.wrapMode = TextureWrapMode.Clamp; imp.alphaIsTransparency = true; });
        var shaftTex = SharedTexture("T_LightShaft", 64, 128, (x, y) =>
        {
            float u = (x + 0.5f) / 64f, v = (y + 0.5f) / 128f;
            float a = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 2f) * Smooth(v / 0.25f) * Smooth((1f - v) / 0.35f);
            return new Color(1, 1, 1, a);
        }, imp => { imp.wrapMode = TextureWrapMode.Clamp; imp.alphaIsTransparency = true; });

        Texture2D lavaCrust = null, lavaGlow = null;
        if (T.water == WaterMode.Lava || (T.hazardCol.a > 0f && !T.hazardIsCloud))
        {
            const int P = 7;
            var pts2 = new Vector2[P * P];
            for (int i = 0; i < P; i++) for (int j = 0; j < P; j++) pts2[i * P + j] = new Vector2(i + Hash(i, j, 71), j + Hash(i, j, 73)) / P;
            (float f1, float f2) Cells(float u, float v)
            {
                float a = 9f, b = 9f;
                foreach (var q in pts2)
                    for (int ox = -1; ox <= 1; ox++) for (int oz = -1; oz <= 1; oz++)
                    {
                        float d = (new Vector2(u, v) - (q + new Vector2(ox, oz))).magnitude;
                        if (d < a) { b = a; a = d; } else if (d < b) b = d;
                    }
                return (a, b);
            }
            lavaGlow = SharedTexture("T_LavaGlow", 256, 256, (x, y) =>
            {
                float u = x / 256f, v = y / 256f;
                var (f1, f2) = Cells(u, v);
                float crack = 1f - Smooth((f2 - f1) / 0.045f);
                float pool = Fbm(u, v, 4, 3, 91);
                float g = Mathf.Clamp01(crack + Smooth((pool - 0.62f) / 0.12f) * 0.8f);
                return new Color(g, g * 0.85f, g * 0.7f, 1f);
            }, imp => { imp.wrapMode = TextureWrapMode.Repeat; });
            lavaCrust = SharedTexture("T_LavaCrust", 256, 256, (x, y) =>
            {
                float u = x / 256f, v = y / 256f;
                var (f1, f2) = Cells(u, v);
                float crack = 1f - Smooth((f2 - f1) / 0.06f);
                float n = Fbm(u, v, 8, 3, 97);
                var crust = Color.Lerp(new Color(0.09f, 0.07f, 0.065f), new Color(0.2f, 0.12f, 0.09f), n);
                return Color.Lerp(crust, new Color(0.9f, 0.35f, 0.08f), crack);
            }, imp => { imp.wrapMode = TextureWrapMode.Repeat; });
        }

        var palette = SavePng($"{dir}/T_{T.key}_Palette.png", Shades, Roles, (x, y) => ShadeColor(Hex(AtlasHex[y]), x),
            imp => { imp.filterMode = FilterMode.Point; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp; imp.sRGBTexture = true; });
        var sky = SavePng($"{dir}/T_{T.key}_Sky.png", 2048, 1024, (x, y) =>
        {
            float u = (x + 0.5f) / 2048f, v = (y + 0.5f) / 1024f;
            float lon = (u - 0.5f) * 2f * Mathf.PI, lat = (1f - v) * Mathf.PI;
            var sd = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
            Color col = sd.y >= 0 ? Color.Lerp(T.skyHorizon, T.skyZenith, Mathf.Pow(Mathf.Clamp01(sd.y * 1.15f), 0.38f)) : Color.Lerp(T.skyHorizon, T.skyHorizon * 0.9f, Mathf.Pow(-sd.y, 0.6f));
            if (T.stars && sd.y > 0.04f)
            {
                float st = Hash(x, y, 99);
                if (st > 0.9975f) col += Color.white * (st - 0.9975f) * 400f * Mathf.Clamp01(sd.y * 3f);
                float band = Mathf.Exp(-Mathf.Pow((sd.y - 0.35f + 0.15f * Mathf.Sin(lon * 2f)) / 0.12f, 2f));
                col += new Color(0.25f, 0.18f, 0.45f) * band * 0.35f * Fbm(u, v, 8, 3, 41);
            }
            if (T.cloudAmount > 0f && sd.y > -0.05f)
            {
                float band = Smooth((sd.y + 0.02f) / 0.08f) * (1f - Smooth((sd.y - 0.08f) / (0.16f + T.cloudAmount * 0.15f)));
                float c = Fbm(u, v * 0.35f, 6, 4, 11), streak = Fbm(u, v * 1.4f, 10, 3, 19);
                float dens = Smooth((c * 0.7f + streak * 0.5f - (0.74f - T.cloudAmount * 0.18f)) / 0.16f) * band;
                col = Color.Lerp(col, T.cloudCol, dens * 0.65f);
            }
            float s = Mathf.Max(0f, Vector3.Dot(sd, sunDir));
            col += T.sunCol * (Mathf.Pow(s, 6f) * 0.28f + Mathf.Pow(s, 60f) * 0.35f + Mathf.Pow(s, 900f) * 1.2f) * (T.terrain == TerrainMode.Cave ? 0f : 1f);
            col.a = 1f;
            return col;
        }, imp => { imp.mipmapEnabled = false; imp.wrapModeU = TextureWrapMode.Repeat; imp.wrapModeV = TextureWrapMode.Clamp; imp.maxTextureSize = 2048; });

        // ---------------- materials
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var pUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        string M(string n) => $"{dir}/M_{T.key}_{n}.mat";
        atlasMat = MakeMat(M("Atlas"), lit, m => { m.SetTexture("_BaseMap", palette); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.12f); m.SetFloat("_Metallic", 0f); m.enableInstancing = true; });
        foliageMat = MakeMat(M("Atlas_TwoSided"), lit, m => { m.SetTexture("_BaseMap", palette); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.08f); m.SetFloat("_Cull", 0f); m.enableInstancing = true; });
        fakeMat = MakeMat(M("Atlas_Fake"), lit, m => { MakeTransparent(m, 0, false); m.SetTexture("_BaseMap", palette); m.SetColor("_BaseColor", new Color(1, 1, 1, 0.82f)); m.SetFloat("_Smoothness", 0.12f); });
        thinMat = MakeMat(M("Thin"), lit, m => { m.SetColor("_BaseColor", ShadeColor(Hex(AtlasHex[(int)R.Bark]), 4)); m.SetFloat("_Smoothness", 0.2f); });
        glowMat = MakeMat(M("Glow"), lit, m => { m.SetColor("_BaseColor", T.glow); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", T.glow * 1.5f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; m.SetFloat("_Smoothness", 0.7f); m.enableInstancing = true; });
        lanternMat = MakeMat(M("Lantern"), lit, m => { m.SetColor("_BaseColor", T.accent); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", T.accent * 9f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; m.SetFloat("_Smoothness", 0.5f); });
        pollenMat = MakeMat(M("Particle"), pUnlit, m => { MakeTransparent(m, 2, true); m.SetTexture("_BaseMap", dot); m.SetColor("_BaseColor", Color.white); });
        fireflyMat = MakeMat(M("ParticleGlow"), pUnlit, m => { MakeTransparent(m, 2, true); m.SetTexture("_BaseMap", dot); m.SetColor("_BaseColor", new Color(2.6f, 2.6f, 2.6f, 1f)); });
        foamMat = MakeMat(M("ParticleSoft"), pUnlit, m => { MakeTransparent(m, 0, true); m.SetTexture("_BaseMap", dot); m.SetColor("_BaseColor", Color.white); });
        leafMat = MakeMat(M("ParticleLeaf"), pUnlit, m => { MakeTransparent(m, 0, true); m.SetTexture("_BaseMap", leafTex); m.SetColor("_BaseColor", Color.white); });
        var mist = MakeMat(M("Mist"), pUnlit, m => { MakeTransparent(m, 0, true); m.SetTexture("_BaseMap", mistTex); var c = T.mistCol; c.a = T.mistAlpha; m.SetColor("_BaseColor", c); });
        var shaftMat = MakeMat(M("LightShaft"), pUnlit, m => { MakeTransparent(m, 2, true); m.SetTexture("_BaseMap", shaftTex); var c = T.sunCol; c.a = 0.09f; m.SetColor("_BaseColor", c); });
        Material waterMat = null, cascadeMat = null;
        switch (T.water)
        {
            case WaterMode.Water:
                waterMat = MakeMat(M("Water"), lit, m =>
                {
                    MakeTransparent(m, 0, false); m.SetTexture("_BaseMap", ripple); var c = T.waterCol; c.a = T.waterAlpha; m.SetColor("_BaseColor", c);
                    m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 0.55f); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_Smoothness", 0.9f); m.SetFloat("_Metallic", 0f);
                });
                break;
            case WaterMode.Ice:
                waterMat = MakeMat(M("Ice"), lit, m =>
                {
                    m.SetTexture("_BaseMap", ripple); m.SetColor("_BaseColor", T.waterCol);
                    m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 0.25f); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_Smoothness", 0.85f);
                });
                break;
            case WaterMode.Lava:
                waterMat = MakeMat(M("Lava"), lit, m =>
                {
                    m.SetTexture("_BaseMap", lavaCrust); m.SetColor("_BaseColor", Color.white);
                    m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", lavaGlow); m.SetColor("_EmissionColor", T.waterCol * 2.6f);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; m.SetFloat("_Smoothness", 0f); m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                });
                break;
            case WaterMode.Clouds:
                waterMat = MakeMat(M("CloudSea"), pUnlit, m => { MakeTransparent(m, 0, true); m.SetTexture("_BaseMap", mistTex); var c = T.waterCol; c.a = 0.9f; m.SetColor("_BaseColor", c); });
                break;
        }
        if (T.waterfall)
            cascadeMat = MakeMat(M("WaterCascade"), lit, m =>
            {
                MakeTransparent(m, 0, false); m.SetTexture("_BaseMap", ripple); var c = Color.Lerp(T.waterCol, Color.white, 0.55f); c.a = 0.88f; m.SetColor("_BaseColor", c);
                m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 0.55f); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_Smoothness", 0.9f);
            });
        var skyMat = MakeMat(M("Sky"), Shader.Find("Skybox/Panoramic"), m => { m.SetTexture("_MainTex", sky); m.SetFloat("_Mapping", 1f); m.SetFloat("_ImageType", 0f); m.SetFloat("_Exposure", 1f); m.SetFloat("_Rotation", 0f); });
        Material hazardMat = null;
        if (T.hazardCol.a > 0f)
            hazardMat = T.hazardIsCloud
                ? MakeMat(M("HazardStorm"), pUnlit, m => { MakeTransparent(m, 0, true); m.SetTexture("_BaseMap", mistTex); var c = T.hazardCol; c.a = 0.95f; m.SetColor("_BaseColor", c); })
                : MakeMat(M("HazardLava"), lit, m =>
                {
                    m.SetTexture("_BaseMap", lavaCrust); m.SetColor("_BaseColor", Color.white); m.SetTextureScale("_BaseMap", new Vector2(40f, 40f));
                    m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", lavaGlow); m.SetColor("_EmissionColor", T.hazardCol * 2.6f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; m.SetFloat("_Smoothness", 0f); m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                });

        // ---------------- clean previous run
        foreach (var go in scene.GetRootGameObjects().Where(g => g.name == EnvName || g.name == "Forest_Environment" || g.name == "Global Volume").ToArray())
            Object.DestroyImmediate(go);
        var plats = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name.StartsWith("Platform_") && t.GetComponent<MeshRenderer>() != null && !t.name.Contains("Trigger"))
            .OrderBy(t => t.name, StringComparer.Ordinal).ToList();
        foreach (var p in plats) { var old = p.Find("Visual"); if (old) Object.DestroyImmediate(old.gameObject); }
        var start = plats.First(t => t.name == "Platform_Start");
        var finish = plats.First(t => t.name == "Platform_Finish");
        var infos = plats.Select(t => new PlatInfo
        {
            t = t, size = t.lossyScale, top = t.position.y + t.lossyScale.y * 0.5f, bottom = t.position.y - t.lossyScale.y * 0.5f,
            rect = new Rect(t.position.x - t.lossyScale.x * 0.5f, t.position.z - t.lossyScale.z * 0.5f, t.lossyScale.x, t.lossyScale.z),
        }).ToList();

        // ---------------- path: minimum spanning tree of the platforms
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
            for (int i = 0; i < pc; i++) { if (inTree[i]) continue; float dd = (pts[i] - pts[u]).magnitude; if (dd < bestD[i]) { bestD[i] = dd; parent[i] = u; } }
        }
        edges.Clear(); mainPath.Clear();
        for (int i = 0; i < pc; i++) if (parent[i] >= 0) edges.Add(new Edge { a = pts[parent[i]], b = pts[i], wa = wid[parent[i]], wb = wid[i] });
        var chain = new List<int>();
        for (int i = fi; i >= 0; i = parent[i]) chain.Add(i);
        chain.Reverse();
        foreach (int i in chain) mainPath.Add(pts[i]);
        int startNext = Enumerable.Range(0, pc).Where(i => parent[i] == si).OrderBy(i => (pts[i] - pts[si]).sqrMagnitude).DefaultIfEmpty(fi).First();
        var fStart = (pts[startNext] - pts[si]).normalized;
        edges.Add(new Edge { a = pts[si] - fStart * 80f + new Vector2(-fStart.y, fStart.x) * 6f, b = pts[si], wa = 5f, wb = wid[si] });
        int finishPrev = parent[fi] >= 0 ? parent[fi] : si;
        var fFinish = (pts[fi] - pts[finishPrev]).normalized;
        riverEndDir = RotateTowardX(fFinish, 14f).normalized;
        riverEnd = pts[fi] + riverEndDir * 27f;
        edges.Add(new Edge { a = pts[fi], b = riverEnd, wa = wid[fi], wb = 4f });

        float minX = plats.Min(t => t.position.x - t.lossyScale.x * 0.5f) - 95f, maxX = plats.Max(t => t.position.x + t.lossyScale.x * 0.5f) + 95f;
        float minZ = plats.Min(t => t.position.z - t.lossyScale.z * 0.5f) - 95f, maxZ = plats.Max(t => t.position.z + t.lossyScale.z * 0.5f) + 95f;
        cell = (maxX - minX) * (maxZ - minZ) > 150000f ? 4f : 3f;
        gx0 = Mathf.Floor(minX / cell) * cell; gz0 = Mathf.Floor(minZ / cell) * cell;
        gnx = Mathf.CeilToInt((maxX - gx0) / cell); gnz = Mathf.CeilToInt((maxZ - gz0) / cell);
        float x0 = gx0, x1 = gx0 + gnx * cell, z0 = gz0, z1 = gz0 + gnz * cell;

        var env = new GameObject(EnvName).transform;
        var decorRoot = Child(env, "Decor");
        bool hasGround = T.terrain != TerrainMode.None;

        // ---------------- terrain
        if (hasGround)
        {
            var terrainMesh = Reg(BuildTerrain().Build("Terrain"));
            MeshObject("Terrain", env, terrainMesh, atlasMat, Vector3.zero, Quaternion.identity, Vector3.one);
            log.Append($"terrain {terrainMesh.vertexCount}v; ");
        }
        else gh = new float[1, 1];

        Mesh Quad(string name, float tile, float extra = 0f)
        {
            var mb = new MB();
            float ax = x0 - extra, bx = x1 + extra, az = z0 - extra, bz = z1 + extra;
            mb.V.AddRange(new[] { new Vector3(ax, 0, az), new Vector3(ax, 0, bz), new Vector3(bx, 0, bz), new Vector3(bx, 0, az) });
            mb.U.AddRange(new[] { new Vector2(ax, az) / tile, new Vector2(ax, bz) / tile, new Vector2(bx, bz) / tile, new Vector2(bx, az) / tile });
            mb.T.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            return Reg(mb.Build(name, true));
        }

        // ---------------- water / clouds / mist
        if (waterMat != null)
        {
            if (T.water == WaterMode.Clouds)
            {
                var cq = Quad("CloudSea", 60f, 250f);
                var c1 = MeshObject("CloudSea_Low", env, cq, waterMat, new Vector3(0, -13f, 0), Quaternion.identity, Vector3.one, false, false);
                AddScroll(c1, new Vector2(0.004f, 0.006f));
                var c2 = MeshObject("CloudSea_High", env, cq, waterMat, new Vector3(9f, -9.5f, 13f), Quaternion.Euler(0, 31f, 0), Vector3.one, false, false);
                AddScroll(c2, new Vector2(-0.006f, 0.003f));
            }
            else if (hasGround)
            {
                var w = MeshObject(T.water == WaterMode.Lava ? "LavaRiver" : (T.water == WaterMode.Ice ? "FrozenRiver" : "Water"), env, Quad("WaterPlane", 9f), waterMat, new Vector3(0, T.waterY, 0), Quaternion.identity, Vector3.one, true, false);
                if (T.water == WaterMode.Water) AddScroll(w, new Vector2(0.01f, 0.05f));
                if (T.water == WaterMode.Lava) AddScroll(w, new Vector2(0.006f, 0.02f));
            }
        }
        if (T.mist)
        {
            var mq = Quad("MistPlane", 45f);
            var m1 = MeshObject("Mist_Low", env, mq, mist, new Vector3(0, -10.2f, 0), Quaternion.identity, Vector3.one, false, false);
            AddScroll(m1, new Vector2(0.004f, 0.006f));
            var m2 = MeshObject("Mist_High", env, mq, mist, new Vector3(7f, -8.3f, 11f), Quaternion.Euler(0, 37f, 0), Vector3.one, false, false);
            AddScroll(m2, new Vector2(-0.006f, 0.003f));
        }

        // ---------------- platforms
        bool Blocked(PlatInfo me)
        {
            var r = new Rect(me.rect.x - 0.3f, me.rect.y - 0.3f, me.rect.width + 0.6f, me.rect.height + 0.6f);
            return infos.Any(o => o != me && o.top < me.bottom - 0.2f && o.rect.Overlaps(r));
        }
        var finishLooks = new List<(Transform vis, PlatInfo info)>();
        int seed = 100 + level * 1000;
        int pillars = 0, floats = 0, bridges = 0, mechs = 0;
        foreach (var info in infos)
        {
            var p = info.t;
            var mr = p.GetComponent<MeshRenderer>();
            var col = p.GetComponent<Collider>();
            Vector3 s = info.size;
            float hx = s.x * 0.5f, hz = s.z * 0.5f;
            float minSide = Mathf.Min(s.x, s.z), maxSide = Mathf.Max(s.x, s.z);
            string matName = mr.sharedMaterial != null ? mr.sharedMaterial.name : "";
            bool finishLook = matName.StartsWith("MAT_Checkpoint_Inactive");
            bool dark = matName.StartsWith("MAT_Platform_Black");
            bool fake = col == null;
            bool moving = p.GetComponent<MovingPlatform>() != null || p.GetComponent<Spinner>() != null;
            bool conveyor = p.GetComponent<ConveyorBelt>() != null;
            bool checkpoint = p.GetComponentInChildren<MidCheckpoint>() != null;
            bool mech = moving || conveyor || checkpoint || p.GetComponent<CrumblingPlatform>() != null || p.GetComponent<BlinkingPlatform>() != null
                        || p.GetComponent<IceSurface>() != null || p.GetComponent<BouncePad>() != null;
            if (dark) continue;

            var vis = new GameObject("Visual").transform;
            vis.SetParent(p, false);
            vis.localPosition = new Vector3(0, 0.5f + 0.004f / s.y, 0);
            vis.localRotation = Quaternion.identity;
            vis.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            int seedP = seed++;
            float floatDepth = Mathf.Clamp(Mathf.Min(hx, hz) * 1.3f + 0.8f, 1.2f, 4.5f);

            if (mech && !finishLook)
            {
                mechs++;
                if (conveyor || checkpoint)
                {
                    mr.enabled = true;
                    MeshObject("Body", vis, Reg(PlatformPillar(hx, hz, floatDepth, seedP, true, false, -s.y).Build($"{p.name}_Body")), atlasMat, Vector3.zero, Quaternion.identity, Vector3.one, false);
                }
                else
                {
                    var mechMat = mr.sharedMaterial;
                    mr.enabled = false;
                    MeshObject("Top", vis, Reg(SlabCap(hx, hz, seedP).Build($"{p.name}_Top")), mechMat, Vector3.zero, Quaternion.identity, Vector3.one, false);
                    MeshObject("Body", vis, Reg(PlatformPillar(hx, hz, floatDepth, seedP + 1, true, false, -0.42f).Build($"{p.name}_Body")), atlasMat, Vector3.zero, Quaternion.identity, Vector3.one, false);
                }
                continue;
            }

            bool thin = minSide < 0.6f;
            bool bridge = !thin && (maxSide > 14f || minSide < 1.0f);
            bool floating = T.floatingOnly || moving || Blocked(info);
            if (thin)
            {
                mr.enabled = true;
                if (!fake) mr.sharedMaterial = thinMat;
                Object.DestroyImmediate(vis.gameObject);
                continue;
            }
            mr.enabled = false;
            float depth = Mathf.Max(1.5f, info.top - pillarBottom);
            bool squarish = maxSide / minSide <= 1.35f;
            MB body;
            if (bridge) { body = PlankBridge(s.x, s.z, seedP); bridges++; }
            else if (floating)
            {
                body = maxSide <= 2.6f && squarish && !T.woodStumps ? StoneColumn(Mathf.Max(hx, hz) * 1.12f, 0f, seedP, true) : PlatformPillar(hx, hz, Mathf.Clamp(Mathf.Min(hx, hz) * 1.2f + 1.2f, 1.5f, 6f), seedP, true);
                floats++;
            }
            else if (maxSide <= 2.6f && squarish)
            {
                body = T.woodStumps ? StumpPillar(Mathf.Max(hx, hz) * 1.28f, depth, seedP) : StoneColumn(Mathf.Max(hx, hz) * 1.16f, depth, seedP, false);
                pillars++;
            }
            else { body = PlatformPillar(hx, hz, depth, seedP); pillars++; }
            MeshObject("Body", vis, Reg(body.Build($"{p.name.Replace(" ", "").Replace("(", "_").Replace(")", "")}_Body")), fake ? fakeMat : atlasMat, Vector3.zero, Quaternion.identity, Vector3.one, !moving);

            // top decor (static, solid platforms only)
            if (!bridge && !fake && !moving && minSide >= 1.0f && T.topDecor.Length > 0)
            {
                int count = Mathf.RoundToInt(s.x * s.z * 1.0f);
                for (int i = 0; i < count; i++)
                {
                    float lx = Rf(-hx + 0.15f, hx - 0.15f), lz = Rf(-hz + 0.15f, hz - 0.15f);
                    float edge = Mathf.Min(hx - Mathf.Abs(lx), hz - Mathf.Abs(lz));
                    if (!Chance(edge < 0.9f ? 0.9f : 0.15f)) continue;
                    string kind = T.topDecor[Ri(0, T.topDecor.Length - 1)];
                    if ((kind == "rock" || kind == "mushroom" || kind == "crystalsmall" || kind == "smallcactus") && (edge > 0.6f || Chance(0.6f))) kind = T.topDecor[0];
                    var wp = new Vector3(p.position.x + lx, info.top + 0.004f, p.position.z + lz);
                    float sc = kind == "rock" ? 0.2f : (kind == "crystalsmall" ? 0.5f : (kind == "mushroom" ? 0.55f : (kind == "smallcactus" ? 0.4f : (kind == "reeds" ? 0.6f : 0.9f))));
                    PlaceProp(kind, wp, Vector3.up, false, sc, 0f);
                }
            }
            if (finishLook) finishLooks.Add((vis, info));
        }
        log.Append($"platforms {plats.Count} (pillars {pillars}, floating {floats}, bridges {bridges}, mechanics {mechs}); ");

        // ---------------- trees / props on the ground
        int treeCount = 0;
        if (hasGround && T.treeDensity > 0f)
        {
            for (float x = gx0 + 2; x < x1 - 2; x += 5.2f)
                for (float z = gz0 + 2; z < z1 - 2; z += 5.2f)
                {
                    float px = x + Rf(-2.2f, 2.2f), pz = z + Rf(-2.2f, 2.2f);
                    float d = PathDist(px, pz);
                    float h = MeshH(px, pz);
                    bool ok;
                    switch (T.terrain)
                    {
                        case TerrainMode.Wetland: ok = d > 5f && d < 70f && h > T.waterY + 0.25f; break;
                        case TerrainMode.Ocean: ok = d > 12f && d < 90f && h > T.waterY + 1.4f; break;
                        case TerrainMode.Dunes: ok = d > 10f && d < 70f && h > T.waterY + 0.6f; break;
                        case TerrainMode.Cave: ok = d > 13.5f && d < 34f; break;
                        default: ok = d > 13.5f && d < 65f; break;
                    }
                    if (!ok || InCascade(new Vector2(px, pz))) continue;
                    float prob = (d < 30f ? 0.72f : (d < 55f ? 0.55f : 0.4f)) * T.treeDensity / 0.6f;
                    if (!Chance(prob) || MeshNormal(px, pz).y < 0.72f) continue;
                    string kind = PickTree();
                    PlaceProp(kind, new Vector3(px, h - 0.15f, pz), Vector3.up, d < 26f, 1f, 0f);
                    treeCount++;
                }
            if (!string.IsNullOrEmpty(T.gorgeTree) && (T.terrain == TerrainMode.Gorge || T.terrain == TerrainMode.Cave))
                foreach (var edge in edges)
                {
                    var a = edge.a; var b = edge.b;
                    float len = (b - a).magnitude;
                    if (len < 0.5f) continue;
                    var dirE = (b - a) / len; var side = new Vector2(-dirE.y, dirE.x);
                    for (float s2 = 0; s2 < len; s2 += 8f)
                        foreach (int sign in new[] { -1, 1 })
                        {
                            if (!Chance(0.4f)) continue;
                            var p2 = a + dirE * (s2 + Rf(-2f, 2f)) + side * sign * (Mathf.Lerp(edge.wa, edge.wb, s2 / len) + Rf(9.5f, 12f));
                            float d = PathDist(p2.x, p2.y);
                            if (d < 9.5f || d > 12.5f || InCascade(p2)) continue;
                            var fromStart = p2 - pts[si];
                            if (fromStart.magnitude < wid[si] + 16f && Vector2.Dot(fromStart, fStart) < wid[si]) continue;
                            PlaceProp(T.gorgeTree, new Vector3(p2.x, MeshH(p2.x, p2.y) - 0.3f, p2.y), Vector3.up, true, 1.85f, 0f);
                            treeCount++;
                        }
                }
        }

        // plateau trees on big start/finish platforms (with trunk colliders, like level 1)
        void Plateau(Transform plat, Vector2 forwardWorld, (float side, float fwd, float scale, bool first)[] spots)
        {
            float half = Mathf.Min(plat.lossyScale.x, plat.lossyScale.z) * 0.5f;
            var vis = plat.Find("Visual");
            if (half < 4.5f || vis == null || plat.GetComponent<MovingPlatform>() != null) return;
            var root = Child(vis, "Trees");
            var f3 = vis.InverseTransformDirection(new Vector3(forwardWorld.x, 0, forwardWorld.y)); f3.y = 0; f3.Normalize();
            var r3 = Vector3.Cross(Vector3.up, f3);
            float k = half / 6f;
            foreach (var sp in spots)
            {
                string kind = sp.first ? T.plateauTree : T.plateauTree2;
                var go = MeshObject("Tree_" + kind, root, PickMesh(kind), PropMat(kind), (r3 * sp.side + f3 * sp.fwd) * k, Quaternion.Euler(0, Rf(0, 360), 0), Vector3.one * sp.scale);
                var cc = go.AddComponent<CapsuleCollider>(); cc.radius = 0.4f; cc.height = 4f; cc.center = new Vector3(0, 2f, 0);
                treeCount++;
            }
        }
        Plateau(start, fStart, new[] { (-4.9f, 4.4f, 0.8f, true), (4.9f, 4.2f, 0.8f, false) });
        Plateau(finish, fFinish, new[] { (-4.8f, 4.6f, 1.0f, true), (4.6f, 4.8f, 0.9f, false), (5.0f, -1.5f, 0.75f, true) });

        // ground decor
        int decorCount = 0;
        if (hasGround && T.decor.Length > 0)
        {
            for (float x = gx0 + 1; x < x1 - 1; x += 1.7f)
                for (float z = gz0 + 1; z < z1 - 1; z += 1.7f)
                {
                    float px = x + Rf(-0.8f, 0.8f), pz = z + Rf(-0.8f, 0.8f);
                    float d = PathDist(px, pz);
                    if (d < 3.2f || d > 40f) continue;
                    float h = MeshH(px, pz);
                    if (T.water != WaterMode.None && h < T.waterY + 0.25f) continue;
                    if (T.terrain == TerrainMode.Cave && h > 8f) continue;
                    var nrm = MeshNormal(px, pz);
                    if (nrm.y < 0.7f) continue;
                    bool rim = d > 11.5f;
                    float roll = (float)rng.NextDouble(), acc = 0f;
                    foreach (var (kind, p0) in T.decor)
                    {
                        float pk = p0 * (kind == "tuft" ? (rim ? (d < 28f ? 1f : 0.45f) : 0.45f) : 1f);
                        acc += pk;
                        if (roll < acc)
                        {
                            PlaceProp(kind, new Vector3(px, h, pz), nrm, kind == "rock" || kind == "termite", 1f);
                            decorCount++;
                            break;
                        }
                    }
                }
            // water plants
            if (T.terrain == TerrainMode.Wetland)
                for (float x = gx0 + 1; x < x1 - 1; x += 1.9f)
                    for (float z = gz0 + 1; z < z1 - 1; z += 1.9f)
                    {
                        float px = x + Rf(-0.9f, 0.9f), pz = z + Rf(-0.9f, 0.9f);
                        float d = PathDist(px, pz);
                        if (d < 2f || d > 60f) continue;
                        float h = MeshH(px, pz);
                        if (h < T.waterY - 0.25f && Chance(0.05f)) { PlaceProp("lily", new Vector3(px, T.waterY + 0.005f, pz), Vector3.up, false, 1f, 0f); decorCount++; }
                        else if (Mathf.Abs(h - T.waterY) < 0.35f && Chance(0.14f)) { PlaceProp("reeds", new Vector3(px, h, pz), Vector3.up, false, 1f, 0f); decorCount++; }
                    }
            // boulders in the river / gorge floor
            if (T.terrain == TerrainMode.Gorge || T.terrain == TerrainMode.Cave)
                for (int i = 0; i < 70; i++)
                {
                    var e = RandomEdge();
                    var p2 = Vector2.Lerp(e.a, e.b, Rf(0f, 1f)) + new Vector2(Rf(-9f, 9f), Rf(-9f, 9f));
                    if (PathDist(p2.x, p2.y) > 5.5f) continue;
                    float sc = Rf(0.8f, 2.2f);
                    AddInst(PickMesh("rockbig"), atlasMat, true, new Vector3(p2.x, MeshH(p2.x, p2.y) + sc * 0.15f, p2.y), Quaternion.Euler(Rf(-20, 20), Rf(0, 360), Rf(-20, 20)), sc);
                }
            if (T.key == "Amazonia" || T.key == "Pantanal")
                for (int i = 0, logs = 0; i < 40 && logs < 14; i++)
                {
                    var e = RandomEdge();
                    if ((e.b - e.a).sqrMagnitude < 0.25f) continue;
                    var dirE = (e.b - e.a).normalized; var side = new Vector2(-dirE.y, dirE.x);
                    var p2 = Vector2.Lerp(e.a, e.b, Rf(0f, 1f)) + side * (Chance(0.5f) ? 1 : -1) * Rf(9f, 26f);
                    float d = PathDist(p2.x, p2.y);
                    if (d < 4f || (d > 11f && d < 22f)) continue;
                    var nn = MeshNormal(p2.x, p2.y);
                    if (nn.y < 0.8f || MeshH(p2.x, p2.y) < T.waterY + 0.2f) continue;
                    AddInst(PickMesh("log"), atlasMat, true, new Vector3(p2.x, MeshH(p2.x, p2.y) + 0.2f, p2.y), Quaternion.FromToRotation(Vector3.up, nn) * Quaternion.Euler(0, Rf(0, 360), 0), 1f);
                    logs++;
                }
        }

        // ---------------- set pieces
        if (T.waterfall && hasGround) BuildCascade(env, cascadeMat, foamMat);
        if (T.terrain == TerrainMode.Cave) BuildCaveCeiling(env, x0, x1, z0, z1);
        if (T.key == "Vulcao") BuildVolcano(env, waterMat);
        if (T.key == "Noronha") BuildNoronha(env);
        if (T.terrain == TerrainMode.None) BuildSkyIslands(x0, x1, z0, z1, infos);
        if (T.lightShafts) BuildShafts(env, shaftMat, sunDir);
        if (T.caveLights) BuildCaveCrystals(env);

        // ---------------- finish gates (also on the decoy finish of level 5)
        foreach (var (vis, info) in finishLooks)
        {
            int nearest = Enumerable.Range(0, pc).Where(i => plats[i] != info.t).OrderBy(i => (pts[i] - new Vector2(info.t.position.x, info.t.position.z)).sqrMagnitude).First();
            var arrival = (new Vector2(info.t.position.x, info.t.position.z) - pts[nearest]).normalized;
            BuildGate(vis, info, arrival);
        }

        // ---------------- particles
        BuildParticles(env, infos);

        // ---------------- rising hazard skin (levels 9 and 10)
        var hazard = GameObject.Find("Hazard_Lava");
        if (hazard != null && hazardMat != null) hazard.GetComponent<MeshRenderer>().sharedMaterial = hazardMat;

        // ---------------- lighting, sky, fog
        light.color = T.sunCol;
        light.intensity = T.sunIntensity;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.82f;
        RenderSettings.sun = light;
        RenderSettings.skybox = skyMat;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = T.ambSky;
        RenderSettings.ambientEquatorColor = T.ambEq;
        RenderSettings.ambientGroundColor = T.ambGround;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = T.fogCol;
        RenderSettings.fogStartDistance = T.fogStart;
        RenderSettings.fogEndDistance = T.fogEnd;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        DynamicGI.UpdateEnvironment();
        if (T.lightning)
        {
            var lf = env.gameObject.AddComponent<LightningFlashes>();
            var so = new SerializedObject(lf); so.FindProperty("targetLight").objectReferenceValue = light; so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- camera + post
        var cam = Camera.main;
        var camData = cam.GetComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;
        cam.farClipPlane = T.terrain == TerrainMode.None ? 600f : 450f;

        var profilePath = $"{dir}/VP_Level{level:00}.asset";
        AssetDatabase.DeleteAsset(profilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);
        var tm = profile.Add<Tonemapping>(true); tm.mode.Override(T.neutralTonemap ? TonemappingMode.Neutral : TonemappingMode.ACES);
        var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(T.bloom); bloom.threshold.Override(1.05f); bloom.scatter.Override(0.72f);
        var vig = profile.Add<Vignette>(true); vig.intensity.Override(T.vignette); vig.smoothness.Override(0.42f); vig.color.Override(T.vignetteCol);
        var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(T.exposure); ca.contrast.Override(T.contrast); ca.saturation.Override(T.saturation);
        var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(T.temperature); wb.tint.Override(T.tint);
        var smh = profile.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.Override(new Vector4(0.92f, 1.0f, 1.06f, 0f));
        smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.94f, 0f));
        foreach (var comp in profile.components) { comp.name = comp.GetType().Name; AssetDatabase.AddObjectToAsset(comp, profile); }
        EditorUtility.SetDirty(profile);
        var vol = new GameObject("Global Volume").AddComponent<Volume>();
        vol.isGlobal = true; vol.priority = 1f; vol.sharedProfile = profile;

        // ---------------- instanced decor + mesh asset
        int inst = EmitGroups(decorRoot);
        var meshPath = $"{dir}/Level{level:00}_Meshes.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(meshAssets[0], meshPath);
        foreach (var m in meshAssets.Skip(1)) AssetDatabase.AddObjectToAsset(m, meshPath);
        AssetDatabase.SaveAssets();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Append($"trees {treeCount}, decor {decorCount}, instances {inst} in {groups.Count} groups, meshes {meshAssets.Count} ({meshAssets.Sum(m => (long)m.vertexCount)}v)");
        log.Append(" | " + AddFauna(level));   // fauna por último, com RNG próprio
        log.Append(" | " + AddCameraColliders(level));
        return log.ToString();
    }

    // ================================================================== set pieces
    static void BuildCascade(Transform env, Material cascade, Material foamM)
    {
        var wf = Child(env, "Waterfall");
        var lastSeg = riverEndDir; var e = riverEnd;
        var side = new Vector2(-lastSeg.y, lastSeg.x);
        var mb = new MB();
        bool first = true; float vAcc = 0; Vector3 prevC = Vector3.zero; int idx0 = 0;
        for (float s = 3f; s <= 42f; s += 1.25f)
        {
            var c2 = e + lastSeg * s;
            float halfW = Mathf.Lerp(4.6f, 3.0f, Mathf.InverseLerp(10f, 42f, s));
            var l2 = c2 + side * halfW; var r2 = c2 - side * halfW;
            float hC = Mathf.Max(MeshH(c2.x, c2.y), Mathf.Max(MeshH(l2.x, l2.y), MeshH(r2.x, r2.y))) + 0.35f;
            var cl = new Vector3(l2.x, hC, l2.y); var cr = new Vector3(r2.x, hC, r2.y); var cc = new Vector3(c2.x, hC, c2.y);
            if (!first) vAcc += (cc - prevC).magnitude;
            int i0 = mb.V.Count;
            mb.V.Add(cl); mb.V.Add(cr);
            mb.U.Add(new Vector2(0, vAcc / 4f)); mb.U.Add(new Vector2(1.2f, vAcc / 4f));
            if (!first) mb.T.AddRange(new[] { idx0, i0, i0 + 1, idx0, i0 + 1, idx0 + 1 });
            idx0 = i0; prevC = cc; first = false;
        }
        var tmp = mb.Build("Cascade", true);
        if (tmp.normals.Length > 0 && tmp.normals.Average(n => n.y) < 0)
        {
            for (int t = 0; t < mb.T.Count; t += 3) { int x = mb.T[t + 1]; mb.T[t + 1] = mb.T[t + 2]; mb.T[t + 2] = x; }
            Object.DestroyImmediate(tmp);
            tmp = mb.Build("Cascade", true);
        }
        var cgo = MeshObject("Cascade", wf, Reg(tmp), cascade, Vector3.zero, Quaternion.identity, Vector3.one, true, false);
        AddScroll(cgo, new Vector2(0f, 0.55f));
        for (float s = 6f; s <= 34f; s += 2.6f)
            foreach (int sign in new[] { -1, 1 })
            {
                var p2 = e + lastSeg * (s + Rf(-0.8f, 0.8f)) + side * sign * Rf(4.4f, 5.8f);
                AddInst(PickMesh("rockbig"), atlasMat, true, new Vector3(p2.x, MeshH(p2.x, p2.y) + 0.1f, p2.y), Quaternion.Euler(Rf(-15, 15), Rf(0, 360), Rf(-15, 15)), Rf(0.9f, 1.8f));
            }
        var foam = Particles("CascadeFoam", wf, new Vector3(e.x + lastSeg.x * 7f, T.waterY + 0.1f, e.y + lastSeg.y * 7f), foamM);
        var main = foam.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 2.0f);
        main.startColor = new ParticleSystem.MinMaxGradient(T.mistCol, Color.Lerp(T.mistCol, T.waterCol, 0.3f));
        main.gravityModifier = 0.05f; main.maxParticles = 200;
        var em = foam.emission; em.rateOverTime = 45f;
        var sh = foam.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(8f, 0.3f, 3f);
        sh.rotation = new Vector3(0, Mathf.Atan2(lastSeg.x, lastSeg.y) * Mathf.Rad2Deg, 0);
        FadeInOut(foam, 0.75f);
    }

    static void BuildCaveCeiling(Transform env, float x0, float x1, float z0, float z1)
    {
        float cs = 6f;
        int nx = Mathf.CeilToInt((x1 - x0) / cs), nz = Mathf.CeilToInt((z1 - z0) / cs);
        var hgt = new float[nx + 1, nz + 1];
        for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= nz; j++)
            {
                float x = x0 + i * cs, z = z0 + j * cs;
                hgt[i, j] = 30f + Mathf.PerlinNoise(x * 0.04f + 3f, z * 0.04f + 9f) * 10f + Mathf.PerlinNoise(x * 0.15f, z * 0.15f) * 2.5f;
            }
        var mb = new MB();
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                Vector3 P(int a, int b) => new Vector3(x0 + a * cs, hgt[a, b], z0 + b * cs);
                mb.Tri(P(i, j), P(i + 1, j), P(i + 1, j + 1), UVr(R.Rock, 1, 4), Vector3.down);
                mb.Tri(P(i, j), P(i + 1, j + 1), P(i, j + 1), UVr(R.Rock, 1, 4), Vector3.down);
            }
        MeshObject("CaveCeiling", env, Reg(mb.Build("CaveCeiling")), atlasMat, Vector3.zero, Quaternion.identity, Vector3.one, true, false);
        for (float x = x0 + 3; x < x1 - 3; x += 7f)
            for (float z = z0 + 3; z < z1 - 3; z += 7f)
            {
                if (!Chance(0.55f)) continue;
                float px = x + Rf(-3f, 3f), pz = z + Rf(-3f, 3f);
                float ci = Mathf.Clamp((px - x0) / cs, 0, nx - 0.01f), cj = Mathf.Clamp((pz - z0) / cs, 0, nz - 0.01f);
                float hh = hgt[Mathf.FloorToInt(ci), Mathf.FloorToInt(cj)];
                bool crystal = Chance(0.12f);
                AddInst(PickMesh(crystal ? "crystal" : "stalagmite"), crystal ? glowMat : atlasMat, false, new Vector3(px, hh + 0.5f, pz), Quaternion.Euler(180f, Rf(0, 360), 0), Rf(0.6f, 1.8f));
            }
    }

    // grandes cristais com luz própria ao longo do caminho (gruta)
    static void BuildCaveCrystals(Transform env)
    {
        var root = Child(env, "CrystalLights");
        int n = 0;
        float total = 0f; for (int i = 0; i < mainPath.Count - 1; i++) total += (mainPath[i + 1] - mainPath[i]).magnitude;
        for (float t = 6f; t < total && n < 16; t += 13f)
        {
            float acc = 0f; Vector2 q = mainPath[0], dirE = Vector2.up;
            for (int i = 0; i < mainPath.Count - 1; i++)
            {
                float l = (mainPath[i + 1] - mainPath[i]).magnitude;
                if (acc + l >= t) { q = Vector2.Lerp(mainPath[i], mainPath[i + 1], l > 0 ? (t - acc) / l : 0f); dirE = l > 0 ? (mainPath[i + 1] - mainPath[i]) / l : Vector2.up; break; }
                acc += l;
            }
            var side = new Vector2(-dirE.y, dirE.x) * (n % 2 == 0 ? 1 : -1);
            var p2 = q + side * Rf(7f, 10f);
            float h = MeshH(p2.x, p2.y);
            var pos = new Vector3(p2.x, h - 0.2f, p2.y);
            AddInst(PickMesh("crystal"), glowMat, false, pos, Quaternion.Euler(0, Rf(0, 360), 0), Rf(1.8f, 2.8f));
            var lg = new GameObject("CrystalLight").AddComponent<Light>();
            lg.transform.SetParent(root, false);
            lg.transform.position = pos + Vector3.up * 4f;
            lg.type = LightType.Point; lg.color = n % 3 == 2 ? Hex(AtlasHex[(int)R.FlowerYellow]) : T.glow; lg.intensity = 4f; lg.range = 22f; lg.shadows = LightShadows.None;
            n++;
        }
    }

    static void BuildVolcano(Transform env, Material lavaMat)
    {
        var c2 = riverEnd + riverEndDir * 105f;
        var center = new Vector3(c2.x, 0, c2.y);
        (float r, float y)[] prof = { (95f, -15f), (70f, 2f), (45f, 22f), (24f, 44f), (15f, 52f), (11f, 49f), (8f, 42f) };
        var rings = new List<Vector3[]>();
        int sides = 18;
        foreach (var (r, y) in prof)
        {
            var ring = new Vector3[sides];
            for (int j = 0; j < sides; j++) { float a = j * Mathf.PI * 2 / sides; float rr = r * Rf(0.88f, 1.1f); ring[j] = center + new Vector3(Mathf.Cos(a) * rr, y + Rf(-1.5f, 1.5f), Mathf.Sin(a) * rr); }
            rings.Add(ring);
        }
        var mb = new MB();
        for (int i = 0; i < rings.Count - 1; i++)
            for (int j = 0; j < sides; j++)
            {
                int k = (j + 1) % sides;
                var a = rings[i][j]; var b = rings[i][k]; var c = rings[i + 1][k]; var d = rings[i + 1][j];
                var cen = (a + b + c + d) * 0.25f;
                var hint = i >= 4 ? (center + Vector3.up * cen.y - cen) : (cen - (center + Vector3.up * cen.y));
                var uv = i >= 4 ? UVr(R.RockMoss, 2, 5) : (i >= 2 ? UVr(R.Rock, 1, 4) : UVr(R.GrassTop, 1, 4));
                mb.Quad(a, b, c, d, uv, hint + Vector3.up * 0.3f);
            }
        MeshObject("Volcano", env, Reg(mb.Build("Volcano")), atlasMat, Vector3.zero, Quaternion.identity, Vector3.one, true, true);
        var crater = new MB();
        Fan(crater, center + Vector3.up * 41f, rings[rings.Count - 1].Select(p => new Vector3(p.x, 41f, p.z)).ToArray(), () => UV(R.MushCap, 7), Vector3.up);
        MeshObject("CraterLava", env, Reg(crater.Build("CraterLava", true)), lavaMat, Vector3.zero, Quaternion.identity, Vector3.one, false, false);
        var smoke = Particles("VolcanoSmoke", env, center + Vector3.up * 45f, foamMat);
        var main = smoke.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 18f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(10f, 22f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.18f, 0.14f, 0.13f, 0.8f), new Color(0.32f, 0.22f, 0.2f, 0.7f));
        main.maxParticles = 120;
        var em = smoke.emission; em.rateOverTime = 5f;
        var sh = smoke.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 6f; sh.rotation = new Vector3(-90, 0, 0);
        var vel = smoke.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(1.5f, 2.5f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var size = smoke.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.2f));
        FadeInOut(smoke, 0.8f);
    }

    static void BuildSkyIslands(float x0, float x1, float z0, float z1, List<PlatInfo> infos)
    {
        int placed = 0;
        for (int i = 0; i < 400 && placed < 70; i++)
        {
            float px = Rf(x0, x1), pz = Rf(z0, z1);
            float d = PathDist(px, pz);
            if (d < 16f || d > 140f) continue;
            float r = Rf(2.5f, 9f) * (d > 60f ? 1.6f : 1f);
            float y = Rf(-6f, 26f);
            var pos = new Vector3(px, y, pz);
            AddInst(PickMesh("island"), atlasMat, d < 60f, pos, Quaternion.Euler(0, Rf(0, 360), 0), r);
            int trees = Ri(0, Mathf.RoundToInt(r * 0.6f));
            for (int k = 0; k < trees; k++)
            {
                var off = new Vector3(Rf(-0.6f, 0.6f), 0, Rf(-0.6f, 0.6f)) * r;
                string kind = Chance(0.6f) ? "pine" : "broadleaf";
                AddInst(PickMesh(kind), atlasMat, d < 60f, pos + off - Vector3.up * 0.1f, Quaternion.Euler(0, Rf(0, 360), 0), Rf(0.35f, 0.7f) * Mathf.Min(1.4f, r / 5f));
            }
            placed++;
        }
    }

    static void BuildShafts(Transform env, Material shaftMat, Vector3 sunDir)
    {
        if (mainPath.Count < 2) return;
        var shafts = Child(env, "LightShafts");
        var sm = new MB();
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
            float f = ts[i] * (mainPath.Count - 1);
            int seg = Mathf.Min(Mathf.FloorToInt(f), mainPath.Count - 2);
            var a = mainPath[seg]; var b = mainPath[seg + 1];
            if ((b - a).sqrMagnitude < 0.01f) continue;
            var dirE = (b - a).normalized; var side = new Vector2(-dirE.y, dirE.x);
            var p2 = Vector2.Lerp(a, b, f - seg) + side * (i % 2 == 0 ? 1 : -1) * Rf(5f, 9f);
            float len = Rf(34f, 46f), w = Rf(2.5f, 5f);
            MeshObject("Shaft_" + i, shafts, shaftMesh, shaftMat, new Vector3(p2.x, -11.5f, p2.y) + sunDir * len * 0.5f, align, new Vector3(w, len, w), false, false);
        }
    }

    static void BuildGate(Transform fVis, PlatInfo info, Vector2 arrivalWorld)
    {
        float minSide = Mathf.Min(info.size.x, info.size.z);
        float k = Mathf.Clamp(minSide / 5.5f, 0.3f, 1f);
        var gate = Child(fVis, "FinishGate");
        var toArrival = fVis.InverseTransformDirection(new Vector3(-arrivalWorld.x, 0, -arrivalWorld.y)); toArrival.y = 0; toArrival.Normalize();
        gate.localPosition = toArrival * minSide * 0.27f;
        gate.localRotation = Quaternion.LookRotation(-toArrival, Vector3.up);
        gate.localScale = Vector3.one * k;
        var gm = new MB();
        var post = new MB();
        Trunk(post, 3.9f, 0.2f, 0.17f, 6, Vector3.zero, 0f);
        gm.Append(post, Matrix4x4.TRS(new Vector3(-2.4f, 0, 0), Quaternion.Euler(0, 0, -2f), Vector3.one));
        gm.Append(post, Matrix4x4.TRS(new Vector3(2.4f, 0, 0), Quaternion.Euler(0, 40, 2f), Vector3.one));
        gm.Append(Log(41, 6.4f, 0.24f), Matrix4x4.TRS(new Vector3(0, 3.75f, 0), Quaternion.Euler(0, 0, 1.5f), Vector3.one));
        gm.Append(Log(42, 5.2f, 0.15f), Matrix4x4.TRS(new Vector3(0, 3.15f, 0.02f), Quaternion.identity, Vector3.one));
        for (int i = 0; i < 9; i++)
        {
            float x = Rf(-2.8f, 2.8f);
            var top = new Vector3(x, 3.55f, Rf(-0.15f, 0.15f));
            gm.TriRaw(top + new Vector3(-0.09f, 0, 0), top + new Vector3(0.09f, 0, 0), top + new Vector3(Rf(-0.05f, 0.05f), -Rf(0.4f, 1.1f), 0), UVr(R.Fern, 2, 4));
        }
        MeshObject("Gate", gate, Reg(gm.Build($"{info.t.name.Replace(" ", "")}_Gate")), foliageMat, Vector3.zero, Quaternion.identity, Vector3.one, false);
        var lm = new MB();
        Blob(lm, Vector3.zero, new Vector3(0.3f, 0.4f, 0.3f), 0.05f, 0, n => UV(R.FlowerYellow, 7));
        var lanternMesh = Reg(lm.Build($"{info.t.name.Replace(" ", "")}_Lantern"));
        foreach (float x in new[] { -2.05f, 2.05f })
        {
            var l = MeshObject("Lantern", gate, lanternMesh, lanternMat, new Vector3(x, 2.55f, 0), Quaternion.identity, Vector3.one, false, false);
            var lg = new GameObject("LanternLight").AddComponent<Light>();
            lg.transform.SetParent(l.transform, false);
            lg.type = LightType.Point; lg.color = T.accent; lg.intensity = 5f; lg.range = 8f * k; lg.shadows = LightShadows.None;
        }
        var ff = Particles("Fireflies", fVis, new Vector3(0, 1.8f, 0), fireflyMat);
        var main = ff.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
        main.startColor = new ParticleSystem.MinMaxGradient(T.accent, Color.Lerp(T.accent, Color.white, 0.4f));
        main.maxParticles = 80;
        var em = ff.emission; em.rateOverTime = 9f * Mathf.Max(0.3f, k);
        var sh = ff.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(info.size.x, 3f, info.size.z);
        var nz = ff.noise; nz.enabled = true; nz.strength = 0.6f; nz.frequency = 0.4f;
        FadeInOut(ff, 1f);
    }

    static void BuildParticles(Transform env, List<PlatInfo> infos)
    {
        var pr = Child(env, "AmbientParticles");
        float avgTop = infos.Average(i => i.top);
        float total = 0f; for (int i = 0; i < mainPath.Count - 1; i++) total += (mainPath[i + 1] - mainPath[i]).magnitude;
        int count = Mathf.Clamp(Mathf.RoundToInt(total / 22f), 2, 14);
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
            ParticleSystem ps;
            ParticleSystem.MainModule main;
            switch (T.particles)
            {
                case ParticleMode.Snow:
                    ps = Particles("Snow", pr, c + Vector3.up * 10f, foamMat); main = ps.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 14f); main.startSpeed = 0f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f); main.gravityModifier = 0.08f; main.maxParticles = 1500;
                    { var em = ps.emission; em.rateOverTime = T.particleRate; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(40f, 2f, 40f); var nz = ps.noise; nz.enabled = true; nz.strength = 0.5f; nz.frequency = 0.25f; }
                    break;
                case ParticleMode.Embers:
                    ps = Particles("Embers", pr, c + Vector3.down * 9f, fireflyMat); main = ps.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f); main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f); main.maxParticles = 400;
                    { var em = ps.emission; em.rateOverTime = T.particleRate; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(30f, 2f, 30f); sh.rotation = new Vector3(-90, 0, 0); var nz = ps.noise; nz.enabled = true; nz.strength = 0.8f; nz.frequency = 0.5f; }
                    var ash = Particles("Ash", pr, c + Vector3.up * 10f, foamMat); var am = ash.main;
                    am.startLifetime = new ParticleSystem.MinMaxCurve(10f, 14f); am.startSpeed = 0f; am.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f); am.gravityModifier = 0.03f; am.maxParticles = 300;
                    am.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.28f, 0.27f, 0.8f), new Color(0.5f, 0.45f, 0.42f, 0.7f));
                    { var em = ash.emission; em.rateOverTime = 14f; var sh = ash.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(36f, 2f, 36f); var nz = ash.noise; nz.enabled = true; nz.strength = 0.4f; }
                    FadeInOut(ash, 0.9f);
                    break;
                case ParticleMode.Sand:
                    ps = Particles("BlowingSand", pr, c, foamMat); main = ps.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f); main.startSpeed = 0f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f); main.maxParticles = 400;
                    { var em = ps.emission; em.rateOverTime = T.particleRate; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(30f, 10f, 30f);
                      var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(2.5f, 4.5f); vel.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.3f); vel.z = new ParticleSystem.MinMaxCurve(0.5f, 1.5f); }
                    break;
                case ParticleMode.Fireflies:
                case ParticleMode.Spores:
                case ParticleMode.Sparkles:
                    ps = Particles(T.particles.ToString(), pr, c, fireflyMat); main = ps.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
                    main.startSize = T.particles == ParticleMode.Sparkles ? new ParticleSystem.MinMaxCurve(0.03f, 0.07f) : new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
                    main.maxParticles = 400;
                    { var em = ps.emission; em.rateOverTime = T.particleRate; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(28f, 12f, 28f); var nz = ps.noise; nz.enabled = true; nz.strength = 0.5f; nz.frequency = 0.35f; }
                    break;
                default: // Pollen / Dust
                    ps = Particles(T.particles.ToString(), pr, c, pollenMat); main = ps.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
                    main.startSize = T.particles == ParticleMode.Dust ? new ParticleSystem.MinMaxCurve(0.05f, 0.12f) : new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                    main.maxParticles = 400;
                    { var em = ps.emission; em.rateOverTime = T.particleRate; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(26f, 12f, 26f); var nz = ps.noise; nz.enabled = true; nz.strength = 0.25f; nz.frequency = 0.25f; }
                    break;
            }
            main.startColor = new ParticleSystem.MinMaxGradient(T.particleA, T.particleB);
            FadeInOut(ps, 0.85f);

            if (T.fallingLeaves)
            {
                var lf = Particles("FallingLeaves", pr, c + Vector3.up * 11f, leafMat);
                var lm = lf.main;
                lm.startLifetime = new ParticleSystem.MinMaxCurve(10f, 14f); lm.startSpeed = 0f;
                lm.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.26f); lm.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                lm.startColor = new ParticleSystem.MinMaxGradient(ShadeColor(Hex(AtlasHex[(int)R.LeafLight]), 5), ShadeColor(Hex(AtlasHex[(int)R.FlowerYellow]), 3));
                lm.gravityModifier = 0.025f; lm.maxParticles = 60;
                var le = lf.emission; le.rateOverTime = 2.5f;
                var ls = lf.shape; ls.shapeType = ParticleSystemShapeType.Box; ls.scale = new Vector3(30f, 2f, 30f);
                var ln = lf.noise; ln.enabled = true; ln.strength = 0.6f; ln.frequency = 0.2f;
                var rl = lf.rotationOverLifetime; rl.enabled = true; rl.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
                FadeInOut(lf, 1f);
            }
        }
    }
}
