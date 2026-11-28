using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Gera as fases 6 a 10 no mesmo formato das fases 2 a 5 (blocos, sem cenário).
public static class LevelsBuilder
{
    const string MatDir = "Assets/Materials";
    const float G = 9.81f, Speed = 8f, Accel = 15f;
    static float jumpV = 10f;

    // ------------------------------------------------------------ materials
    static Material blue, black, finishMat, moving, crumble, blink, ghost, ice, conveyor, bounce, hazard, wind, windParticle, lava, cpMid, cpActive, fake;

    static Material Mat(string name, Color c, float smooth = 0.25f, bool transparent = false, float alpha = 1f, Texture tex = null, Color? emission = null)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool create = m == null;
        if (create) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha));
        m.SetFloat("_Smoothness", smooth);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
        if (transparent)
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", 1f); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
        }
        if (create) AssetDatabase.CreateAsset(m, path);
        EditorUtility.SetDirty(m);
        return m;
    }

    static PhysicsMaterial IcePhysics()
    {
        string path = MatDir + "/PM_Ice.physicMaterial";
        var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (pm == null) { pm = new PhysicsMaterial("PM_Ice"); AssetDatabase.CreateAsset(pm, path); }
        pm.dynamicFriction = 0f; pm.staticFriction = 0f; pm.frictionCombine = PhysicsMaterialCombine.Minimum; pm.bounciness = 0f;
        EditorUtility.SetDirty(pm);
        return pm;
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    static Texture2D ConveyorTexture()
    {
        string dir = MatDir + "/Textures"; Directory.CreateDirectory(dir);
        string path = dir + "/T_Conveyor.png";
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var dark = Hex("#3A3F4B"); var stripe = Hex("#F2C94C");
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                // chevron pointing to +v
                float u = Mathf.Abs(x - 31.5f) / 32f;
                float v = Mathf.Repeat(y / 64f + u * 0.5f, 1f);
                bool on = v > 0.55f && v < 0.8f && u < 0.85f;
                tex.SetPixel(x, y, on ? stripe : dark);
            }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Repeat; imp.filterMode = FilterMode.Bilinear; imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void LoadMaterials()
    {
        blue = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/MAT_Platform_Blue.mat");
        black = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/MAT_Platform_Black.mat");
        finishMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/MAT_Checkpoint_Inactive.mat");
        var blueCol = blue.GetColor("_BaseColor");
        moving = Mat("MAT_Platform_Moving", Hex("#8B5CF6"));
        crumble = Mat("MAT_Platform_Crumble", Hex("#F08A24"));
        blink = Mat("MAT_Platform_Blink", Hex("#F5D33D"));
        ghost = Mat("MAT_Platform_BlinkGhost", Hex("#F5D33D"), 0.2f, true, 0.18f);
        ice = Mat("MAT_Platform_Ice", Hex("#BDEFFF"), 0.95f);
        conveyor = Mat("MAT_Platform_Conveyor", Color.white, 0.3f, false, 1f, ConveyorTexture());
        bounce = Mat("MAT_BouncePad", Hex("#7ED957"), 0.4f, false, 1f, null, Hex("#7ED957") * 0.6f);
        hazard = Mat("MAT_Hazard", Hex("#E5484D"), 0.4f, false, 1f, null, Hex("#E5484D") * 0.35f);
        wind = Mat("MAT_WindArea", Hex("#DDF3FF"), 0f, true, 0.06f);
        lava = Mat("MAT_Lava", Hex("#FF5A1F"), 0.6f, true, 0.78f, null, Hex("#FF5A1F") * 1.2f);
        cpMid = Mat("MAT_Checkpoint_Mid", Hex("#7FD8D0"));
        cpActive = Mat("MAT_Checkpoint_Active", Hex("#3FD07A"), 0.3f, false, 1f, null, Hex("#3FD07A") * 0.3f);
        fake = Mat("MAT_Platform_Fake", blueCol, blue.GetFloat("_Smoothness"), true, 0.82f);

        string wp = MatDir + "/MAT_WindParticle.mat";
        windParticle = AssetDatabase.LoadAssetAtPath<Material>(wp);
        if (windParticle == null) { windParticle = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(windParticle, wp); }
        windParticle.SetFloat("_Surface", 1f); windParticle.SetFloat("_Blend", 0f); windParticle.SetFloat("_ZWrite", 0f);
        windParticle.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); windParticle.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        windParticle.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); windParticle.renderQueue = (int)RenderQueue.Transparent;
        windParticle.SetColor("_BaseColor", new Color(1, 1, 1, 0.55f));
        EditorUtility.SetDirty(windParticle);
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------ path model for validation
    enum Kind { Static, Moving, Spinner, Crumble, Blink, Ice, Conveyor, Bounce, Checkpoint, Fake, Finish, Start }
    class Node
    {
        public string name; public Kind kind; public Vector3 pos; public Vector3 size;
        public Vector3 offset; public float launch; public float spinRadius;
        public float Top(Vector3 p) => p.y + size.y * 0.5f;
    }
    static List<Node> path;
    static Transform root;
    static int counter;
    static Transform player;

    static GameObject Box(string name, Vector3 pos, Vector3 size, Material m, bool collider = true, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        return go;
    }

    static void Set(Component c, string prop, object value)
    {
        var so = new SerializedObject(c);
        var p = so.FindProperty(prop);
        if (p == null) throw new Exception($"{c.GetType().Name}.{prop} não existe");
        switch (value)
        {
            case float f: p.floatValue = f; break;
            case int i: p.intValue = i; break;
            case bool b: p.boolValue = b; break;
            case Vector3 v: p.vector3Value = v; break;
            case Color col: p.colorValue = col; break;
            case Object o: p.objectReferenceValue = o; break;
            default: throw new Exception("tipo não suportado " + value?.GetType());
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static string NextName() => "Platform_" + (++counter).ToString("00");

    static Node AddNode(Kind k, GameObject go, Vector3 size, Vector3 offset = default, float launch = 0f, float spinRadius = 0f)
    {
        var n = new Node { name = go.name, kind = k, pos = go.transform.position, size = size, offset = offset, launch = launch, spinRadius = spinRadius };
        path.Add(n);
        return n;
    }

    // --- element helpers (all positions = center; scale y = 1 unless given)
    static GameObject S(float x, float y, float z, float sx, float sz, Material m = null)
    {
        var size = new Vector3(sx, 1f, sz);
        var go = Box(NextName(), new Vector3(x, y, z), size, m ?? blue, true, root);
        AddNode(Kind.Static, go, size);
        return go;
    }
    static List<string> accelWarnings = new List<string>();
    static GameObject M(float x, float y, float z, float sx, float sz, Vector3 offset, float period, float phase = 0f, float pause = 0.12f, Material m = null)
    {
        float tm = period * (0.5f - pause);
        float peak = 6f * new Vector2(offset.x, offset.z).magnitude / (tm * tm);
        if (peak > 12.5f) accelWarnings.Add($"Platform_{counter + 1:00}: aceleração de pico {peak:F1} m/s² (bola acompanha até 15)");
        var size = new Vector3(sx, 1f, sz);
        var go = Box(NextName(), new Vector3(x, y, z), size, m ?? moving, true, root);
        go.AddComponent<Rigidbody>().isKinematic = true;
        var mp = go.AddComponent<MovingPlatform>();
        Set(mp, "offset", offset); Set(mp, "period", period); Set(mp, "phase", phase); Set(mp, "pauseFraction", pause);
        AddNode(Kind.Moving, go, size, offset);
        return go;
    }
    static GameObject Spin(float x, float y, float z, float length, float width, float degPerSec, Material m = null)
    {
        var size = new Vector3(length, 1f, width);
        var go = Box(NextName(), new Vector3(x, y, z), size, m ?? moving, true, root);
        go.AddComponent<Rigidbody>().isKinematic = true;
        var sp = go.AddComponent<Spinner>();
        Set(sp, "degreesPerSecond", degPerSec);
        AddNode(Kind.Spinner, go, size, default, 0f, length * 0.5f);
        return go;
    }
    static GameObject C(float x, float y, float z, float sx, float sz, float delay, float respawn = 3f)
    {
        var size = new Vector3(sx, 1f, sz);
        var go = Box(NextName(), new Vector3(x, y, z), size, crumble, true, root);
        var c = go.AddComponent<CrumblingPlatform>();
        Set(c, "delay", delay); Set(c, "respawnTime", respawn);
        AddNode(Kind.Crumble, go, size);
        return go;
    }
    static GameObject B(float x, float y, float z, float sx, float sz, float period, float onFraction, float phase)
    {
        var size = new Vector3(sx, 1f, sz);
        var go = Box(NextName(), new Vector3(x, y, z), size, blink, true, root);
        AddBlink(go, period, onFraction, phase);
        AddNode(Kind.Blink, go, size);
        return go;
    }
    static void AddBlink(GameObject go, float period, float onFraction, float phase)
    {
        var b = go.AddComponent<BlinkingPlatform>();
        Set(b, "period", period); Set(b, "onFraction", onFraction); Set(b, "phase", Mathf.Repeat(phase, 1f)); Set(b, "ghostMaterial", ghost);
    }
    static GameObject I(float x, float y, float z, float sx, float sz, float mult = 0.12f)
    {
        var size = new Vector3(sx, 1f, sz);
        var go = Box(NextName(), new Vector3(x, y, z), size, ice, true, root);
        var s = go.AddComponent<IceSurface>(); Set(s, "accelerationMultiplier", mult);
        go.GetComponent<BoxCollider>().sharedMaterial = IcePhysics();
        AddNode(Kind.Ice, go, size);
        return go;
    }
    // conveyor along world Z (length) pushing +z (dir=+1) or -z (dir=-1); or sideways along X when sideways=true
    static GameObject V(float x, float y, float z, float width, float length, float speed, int dir, bool sideways = false)
    {
        var worldSize = new Vector3(width, 1f, length);
        GameObject go;
        if (!sideways)
        {
            go = Box(NextName(), new Vector3(x, y, z), worldSize, conveyor, true, root);
            if (dir < 0) go.transform.rotation = Quaternion.Euler(0, 180, 0);
        }
        else
        {
            // local z (belt direction) -> world +x (dir=+1) or -x
            go = Box(NextName(), new Vector3(x, y, z), new Vector3(length, 1f, width), conveyor, true, root);
            go.transform.rotation = Quaternion.Euler(0, dir > 0 ? 90 : -90, 0);
        }
        var cb = go.AddComponent<ConveyorBelt>(); Set(cb, "speed", speed);
        AddNode(Kind.Conveyor, go, worldSize);
        return go;
    }
    static GameObject J(float x, float y, float z, float sx, float sz, float launch)
    {
        var size = new Vector3(sx, 1f, sz);
        var go = Box(NextName(), new Vector3(x, y, z), size, bounce, true, root);
        var bp = go.AddComponent<BouncePad>(); Set(bp, "launchSpeed", launch);
        AddNode(Kind.Bounce, go, size, default, launch);
        return go;
    }
    static GameObject CP(float x, float y, float z, float sx, float sz)
    {
        var size = new Vector3(sx, 1f, sz);
        var go = Box(NextName(), new Vector3(x, y, z), size, cpMid, true, root);
        var trig = new GameObject("MidCheckpointTrigger");
        trig.transform.SetParent(go.transform, false);
        trig.transform.localPosition = new Vector3(0, 0.8f, 0);
        trig.AddComponent<BoxCollider>().isTrigger = true;
        var mc = trig.AddComponent<MidCheckpoint>(); Set(mc, "activeMaterial", cpActive);
        AddNode(Kind.Checkpoint, go, size);
        return go;
    }
    static GameObject Fake(float x, float y, float z, float sx, float sz)
    {
        // looks like a platform, has no collider (not part of the path)
        return Box("Platform_" + (counter + 1).ToString("00") + "_Fake", new Vector3(x, y, z), new Vector3(sx, 1f, sz), fake, false, root);
    }
    static GameObject Finish(float x, float y, float z, float sx, float sz)
    {
        var f = GameObject.Find("Platform_Finish");
        f.transform.position = new Vector3(x, y, z);
        f.transform.localScale = new Vector3(sx, 1f, sz);
        f.transform.rotation = Quaternion.identity;
        AddNode(Kind.Finish, f, new Vector3(sx, 1f, sz));
        return f;
    }

    // --- hazards (not path)
    static GameObject Sweeper(float x, float platformTop, float z, float length, float degPerSec, float startAngle = 0f)
    {
        var go = Box("Hazard_Sweeper", new Vector3(x, platformTop + 0.35f, z), new Vector3(length, 0.5f, 0.45f), hazard, true, root);
        go.transform.rotation = Quaternion.Euler(0, startAngle, 0);
        go.AddComponent<Rigidbody>().isKinematic = true;
        var sp = go.AddComponent<Spinner>(); Set(sp, "degreesPerSecond", degPerSec);
        return go;
    }
    static GameObject PendulumAt(float x, float targetTop, float z, float armLength, float amplitude, float period, float phase, bool swingAcrossX = true)
    {
        var pivot = new GameObject("Hazard_Pendulum");
        pivot.transform.SetParent(root, false);
        float headSize = 1.1f;
        pivot.transform.position = new Vector3(x, targetTop + 0.5f + armLength, z);
        var arm = Box("Arm", pivot.transform.position + Vector3.down * armLength * 0.5f, new Vector3(0.15f, armLength, 0.15f), hazard, true, pivot.transform);
        var head = Box("Head", pivot.transform.position + Vector3.down * armLength, Vector3.one * headSize, hazard, true, pivot.transform);
        pivot.AddComponent<Rigidbody>().isKinematic = true;
        var p = pivot.AddComponent<Pendulum>();
        Set(p, "amplitude", amplitude); Set(p, "period", period); Set(p, "phase", Mathf.Repeat(phase, 1f));
        Set(p, "swingAxis", swingAcrossX ? Vector3.forward : Vector3.right);
        return pivot;
    }
    static GameObject Wind(Vector3 center, Vector3 size, Vector3 velocity, float gust, float gustPeriod = 2.5f)
    {
        var go = Box("Wind_Area", center, size, wind, true, root);
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        go.GetComponent<BoxCollider>().isTrigger = true;
        var w = go.AddComponent<WindArea>();
        Set(w, "windVelocity", velocity); Set(w, "gust", gust); Set(w, "gustPeriod", gustPeriod);

        var psGo = new GameObject("WindStreaks");
        psGo.transform.SetParent(go.transform, false);
        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = Mathf.Max(size.x, size.z) / Mathf.Max(1f, velocity.magnitude * 2f);
        main.startSpeed = velocity.magnitude * 2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new Color(1, 1, 1, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300; main.prewarm = true;
        var em = ps.emission; em.rateOverTime = size.x * size.z * 0.25f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = Vector3.one; // box follows the parent scale (hierarchy scaling)
        sh.rotation = Quaternion.LookRotation(velocity.normalized).eulerAngles;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        var r = psGo.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = windParticle; r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.12f; r.lengthScale = 2f;
        return go;
    }
    static GameObject Lava(float startY, float riseSpeed, float maxY, float delay)
    {
        var go = Box("Hazard_Lava", new Vector3(0, startY, 0), new Vector3(600f, 0.2f, 600f), lava, false, root);
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var rh = go.AddComponent<RisingHazard>();
        Set(rh, "player", player); Set(rh, "riseSpeed", riseSpeed); Set(rh, "maxHeight", maxY); Set(rh, "startDelay", delay);
        return go;
    }

    // ------------------------------------------------------------ jump reach
    // Simulação passo a passo igual à física da bola: gravidade, amortecimento linear 0,5
    // e o controlador (aceleração 15, velocidade 8). Retorna o alcance horizontal até descer à altura h.
    static float playerDamping = 0.5f;
    static float SimReach(float v0, float h, bool runup)
    {
        const float dt = 0.02f;
        float y = 0, vy = v0, x = 0, vx = runup ? Speed * 0.99f : 0f, maxY = 0;
        for (int i = 0; i < 2000; i++)
        {
            vx = Mathf.Min(vx + Accel * dt, Speed);
            vy -= G * dt;
            vx /= 1f + playerDamping * dt; vy /= 1f + playerDamping * dt;
            x += vx * dt; y += vy * dt; maxY = Mathf.Max(maxY, y);
            if (vy < 0 && y <= h) return maxY >= h + 0.05f ? x : -1f;
        }
        return -1f;
    }

    static float GapXZ(Vector3 pa, Vector3 sa, Vector3 pb, Vector3 sb)
    {
        float dx = Mathf.Max(0f, Mathf.Abs(pa.x - pb.x) - (sa.x + sb.x) * 0.5f);
        float dz = Mathf.Max(0f, Mathf.Abs(pa.z - pb.z) - (sa.z + sb.z) * 0.5f);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static IEnumerable<Vector3> Samples(Node n)
    {
        if (n.kind == Kind.Moving) { for (int i = 0; i <= 8; i++) yield return n.pos + n.offset * (i / 8f); }
        else yield return n.pos;
    }
    static Vector3 EffSize(Node n) => n.kind == Kind.Spinner ? new Vector3(n.spinRadius * 2f, n.size.y, n.spinRadius * 2f) : n.size;

    static string Validate(string level, float maxRatio)
    {
        var sb = new System.Text.StringBuilder();
        int bad = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            var a = path[i]; var b = path[i + 1];
            float bestRatio = float.MaxValue; float bestGap = 0, bestH = 0, bestReach = 0;
            foreach (var pa in Samples(a))
                foreach (var pb in Samples(b))
                {
                    float gap = GapXZ(pa, EffSize(a), pb, EffSize(b));
                    float h = b.Top(pb) - a.Top(pa);
                    float v0 = a.kind == Kind.Bounce ? a.launch : jumpV;
                    var dir = new Vector3(pb.x - pa.x, 0, pb.z - pa.z).normalized;
                    float extent = Mathf.Abs(dir.x) * EffSize(a).x + Mathf.Abs(dir.z) * EffSize(a).z;
                    bool runup = extent >= 3f && a.kind != Kind.Bounce && a.kind != Kind.Crumble;
                    float reach = SimReach(v0, h, runup);
                    float ratio = gap <= 0.01f && h <= 0.6f ? 0f : (reach <= 0 ? 99f : gap / reach);
                    if (ratio < bestRatio) { bestRatio = ratio; bestGap = gap; bestH = h; bestReach = reach; }
                }
            string flag = bestRatio > maxRatio ? "  <-- !!!" : "";
            if (bestRatio > maxRatio) bad++;
            sb.AppendLine($"{a.name}({a.kind}) -> {b.name}({b.kind}): gap {bestGap:F1} h {bestH:+0.0;-0.0} reach {bestReach:F1} ratio {bestRatio:F2}{flag}");
        }
        foreach (var w in accelWarnings) { sb.AppendLine("  <-- !!! " + w); bad++; }
        accelWarnings.Clear();
        return $"== {level}: {path.Count} nós, {bad} problema(s)\n" + sb;
    }

    // ------------------------------------------------------------ scene scaffolding
    static void NewLevel(int n)
    {
        string path_ = $"Assets/Scenes/Level_{n:00}.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path_) != null) AssetDatabase.DeleteAsset(path_);
        AssetDatabase.CopyAsset("Assets/Scenes/Level_05.unity", path_);
        var scene = EditorSceneManager.OpenScene(path_, OpenSceneMode.Single);
        var keep = new HashSet<string> { "Main Camera", "Directional Light", "Player", "Platform_Start", "Platform_Finish" };
        foreach (var go in scene.GetRootGameObjects())
            if (!keep.Contains(go.name)) Object.DestroyImmediate(go);
        root = new GameObject("Level").transform;
        player = GameObject.Find("Player").transform;
        player.position = new Vector3(0, 1.5f, 0);
        var start = GameObject.Find("Platform_Start");
        start.transform.position = Vector3.zero; start.transform.localScale = new Vector3(12, 1, 12);
        path = new List<Node>();
        counter = 1;
        path.Add(new Node { name = "Platform_Start", kind = Kind.Start, pos = Vector3.zero, size = new Vector3(12, 1, 12) });
        jumpV = new SerializedObject(Object.FindAnyObjectByType<PlayerController>()).FindProperty("jumpForce").floatValue / player.GetComponent<Rigidbody>().mass;
        playerDamping = player.GetComponent<Rigidbody>().linearDamping;
    }

    static string Save(int n, float maxRatio)
    {
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return Validate($"Level_{n:00}", maxRatio);
    }

    // ============================================================ LEVEL 6 — Movimento
    static string Level06()
    {
        NewLevel(6);
        M(-5, 0, 14, 4, 4, new Vector3(10, 0, 0), 6.5f, 0f, 0.15f);
        S(0, 0, 24, 3, 3);
        M(0, 0, 31, 3, 3, new Vector3(0, 6, 0), 7f, 0f, 0.2f);                 // elevador
        S(0, 6.5f, 38, 2, 2);
        M(-3, 6.5f, 45, 2, 2, new Vector3(6, 0, 0), 5f, 0f);
        M(3, 6.5f, 51, 2, 2, new Vector3(-6, 0, 0), 4.6f, 0.3f);
        M(-3, 6.5f, 57, 2, 2, new Vector3(6, 0, 0), 5.4f, 0.6f);
        CP(0, 6.5f, 64, 4, 4);
        Spin(0, 6.5f, 76, 10, 2, 35f);                                            // barra giratória
        S(0, 6.5f, 89, 2, 2);
        M(0, 7.5f, 95, 2, 2, new Vector3(0, 0, 8), 5.5f, 0.5f);                    // vai e volta em z
        M(3, 8f, 111, 1.5f, 1.5f, new Vector3(6, 4, 0), 5.5f);                    // sobe na diagonal
        S(9, 13.5f, 117, 1.5f, 1.5f);
        M(9, 13.5f, 123, 1, 1, new Vector3(0, 4, 0), 4f, 0f);                     // elevadores pequenos opostos
        M(9, 17.5f, 129, 1, 1, new Vector3(0, -4, 0), 4f, 0f);
        M(5, 17.5f, 136, 1, 1, new Vector3(8, 0, 0), 5.5f, 0.25f);               // rápida e pequena
        Finish(9, 17.5f, 145, 4, 4);
        return Save(6, 0.8f);
    }

    // ============================================================ LEVEL 7 — Tempo
    // Todo pulo dura ~2,04 s no ar; os ritmos das plataformas que piscam foram calculados com isso.
    static string Level07()
    {
        NewLevel(7);
        for (int i = 0; i < 5; i++) C(0, 0, 10 + i * 4, 2, 2, 0.7f);              // trilha que desmorona
        S(0, 0, 32, 3, 3);
        // alternância A/B: período 3 s, 55% acesa
        B(0, 0, 38, 2.5f, 2.5f, 3f, 0.55f, 0f);
        B(0, 0, 44, 2.5f, 2.5f, 3f, 0.55f, 0.5f);
        B(0, 0, 50, 2.5f, 2.5f, 3f, 0.55f, 0f);
        B(0, 0, 56, 2.5f, 2.5f, 3f, 0.55f, 0.5f);
        S(0, 0, 62, 3, 3);
        C(5, 1.5f, 67, 2, 2, 0.5f); C(9, 3f, 71, 2, 2, 0.5f); C(13, 4.5f, 75, 2, 2, 0.5f); C(17, 6f, 79, 2, 2, 0.5f); // escada
        CP(22, 6, 86, 4, 4);
        // onda: período 3 s, 1,5 s acesa; cada uma atrasa 1,03 s (= voo de 1,78 s - metade da janela)
        for (int i = 0; i < 4; i++) B(22, 6, 93 + i * 5, 1.5f, 1.5f, 3f, 0.5f, -i * 1.03f / 3f);
        float[] zx = { 25, 21, 25, 21, 25 };
        for (int i = 0; i < 5; i++) C(zx[i], 6 + i * 0.5f, 113 + i * 4, 1, 1, 0.45f);           // zigue-zague rápido
        var mb = M(23, 9f, 136, 2, 2, new Vector3(0, 0, 6), 5.5f, 0f, 0.15f, blink);              // móvel que pisca
        AddBlink(mb, 2.5f, 0.6f, 0f);
        for (int i = 0; i < 3; i++) C(23, 9, 148 + i * 4, 1, 1, 0.35f);
        Finish(23, 9, 163, 3, 3);
        return Save(7, 0.8f);
    }

    // ============================================================ LEVEL 8 — Forças
    static string Level08()
    {
        NewLevel(8);
        I(0, 0, 16, 8, 12);                                       // campo de gelo
        S(0, 0, 28, 3, 3);
        I(0, 0, 38, 1.2f, 14);                                    // viga de gelo
        S(0, 0, 50, 2, 2);
        V(0, 0, 62, 1.5f, 12, 4f, -1);                            // esteira contra
        S(0, 0, 74, 3, 3);
        S(0, 0, 88, 1f, 20);                                      // viga com vento lateral
        Wind(new Vector3(0, 2.5f, 88), new Vector3(12, 7, 16), new Vector3(3.5f, 0, 0), 0.5f, 3f);
        CP(0, 0, 104, 4, 4);
        V(0, 0, 116, 1.2f, 14, 2.5f, +1, true);                   // esteira lateral
        S(0, 1, 130, 3, 3);                                       // salto contra o vento
        Wind(new Vector3(0, 2f, 126f), new Vector3(10, 12, 5.5f), new Vector3(0, 0, -3f), 0.3f, 2f);
        // plataformas falsas: em cada par, só uma existe (a falsa é levemente transparente)
        S(-2.5f, 1, 141, 2, 2); Fake(2.5f, 1, 141, 2, 2);
        Fake(-2.5f, 1, 148, 2, 2); S(2.5f, 1, 148, 2, 2);
        S(0, 1, 155, 2, 2);
        I(0, 1.5f, 162, 1.5f, 1.5f); I(4, 2f, 167, 1.5f, 1.5f); I(0, 2.5f, 172, 1.5f, 1.5f);   // gelo pequeno
        V(0, 3, 184, 1.2f, 12, 3f, -1);                           // esteira contra + vento
        Wind(new Vector3(0, 5f, 184), new Vector3(10, 6, 12), new Vector3(-3f, 0, 0), 0.6f, 2.2f);
        Finish(0, 3, 196, 3, 3);
        return Save(8, 0.8f);
    }

    // ============================================================ LEVEL 9 — Perigo
    static string Level09()
    {
        NewLevel(9);
        Lava(-6f, 0.3f, 45f, 4f);
        S(0, 0, 15, 8, 8);
        Sweeper(0, 0.5f, 15, 7.4f, 90f);
        J(0, 0, 24, 2, 2, 20f);                                   // trampolim
        S(0, 10, 30, 3, 3);
        S(0, 10, 44, 1.5f, 22);                                   // viga com pêndulos
        PendulumAt(0, 10.5f, 38, 5.5f, 55f, 2.4f, 0f);
        PendulumAt(0, 10.5f, 44, 5.5f, 55f, 2.4f, 0.33f);
        PendulumAt(0, 10.5f, 50, 5.5f, 55f, 2.4f, 0.66f);
        CP(0, 10, 60, 4, 4);
        J(0, 10, 67, 1.5f, 1.5f, 17.5f);
        S(6, 18, 71, 2, 2);
        J(6, 18, 77, 1.5f, 1.5f, 17.5f);
        S(0, 26, 81, 2, 2);
        S(0, 26, 91, 5, 5);
        Sweeper(0, 26.5f, 91, 4.6f, 120f);
        Sweeper(0, 26.5f, 91, 4.6f, 120f, 90f);
        M(0, 26, 102, 3, 3, new Vector3(6, 0, 0), 5f);
        S(6, 27, 110, 1.5f, 1.5f); PendulumAt(6, 27.5f, 110, 5f, 50f, 2.2f, 0f, false);
        S(6, 28, 116, 1.5f, 1.5f); PendulumAt(6, 28.5f, 116, 5f, 50f, 2.2f, 0.5f, false);
        S(6, 29, 122, 1.5f, 1.5f); PendulumAt(6, 29.5f, 122, 5f, 50f, 2.2f, 0.25f, false);
        J(6, 29, 128, 1.5f, 1.5f, 15f);
        M(6, 34.5f, 133, 2, 2, new Vector3(0, 0, 6), 5f);
        Finish(6, 34.5f, 147, 4, 4);
        return Save(9, 0.8f);
    }

    // ============================================================ LEVEL 10 — Quase impossível (sem checkpoint)
    static string Level10()
    {
        NewLevel(10);
        Lava(-6f, 0.1f, 30f, 6f);
        for (int i = 0; i < 4; i++) C(0, i * 0.5f, 11 + i * 5, 1, 1, 0.3f);       // desmorona em 0,3 s
        // onda apertada: período 2 s, 0,8 s acesa, atraso de 1,38 s entre vizinhas (~0,4 s de janela)
        for (int i = 0; i < 4; i++) B(0, 1.5f, 33f + i * 6f, 1, 1, 2f, 0.4f, -i * 1.38f / 2f);
        S(0, 1.5f, 65, 0.7f, 18);                                                  // viga fina com vento
        Wind(new Vector3(0, 4f, 65), new Vector3(10, 7, 18), new Vector3(3f, 0, 0), 0.25f, 1.7f);
        I(0, 2f, 81, 1, 1); I(4, 2.5f, 88, 1, 1); I(0, 3f, 95, 1, 1);             // gelo minúsculo
        V(0, 3, 109, 0.8f, 12, 6.5f, -1);                                          // esteira forte contra
        J(0, 3, 120, 1, 1, 16.5f);
        M(0, 10f, 126, 1, 1, new Vector3(0, 0, 5), 4f);                           // pouso numa móvel pequena
        Spin(0, 10, 143, 8, 0.5f, 55f);                                           // barra fina girando
        S(0, 10, 156, 3, 3);
        Sweeper(0, 10.5f, 156, 2.8f, 160f);
        S(0, 10, 170, 0.5f, 18);                                                   // viga de 0,5 m com pêndulos
        PendulumAt(0, 10.5f, 164, 5.5f, 55f, 1.8f, 0f);
        PendulumAt(0, 10.5f, 170, 5.5f, 55f, 1.8f, 0.3f);
        PendulumAt(0, 10.5f, 176, 5.5f, 55f, 1.8f, 0.6f);
        // trio: só a da direita existe
        Fake(-3, 10, 186, 1, 1); Fake(0, 10, 186, 1, 1); S(3, 10, 186, 1, 1);
        PendulumAt(3, 10.5f, 186, 5f, 45f, 2f, 0f, false);
        B(3, 10.5f, 193.5f, 0.8f, 0.8f, 2.5f, 0.45f, 0f);
        C(0, 11, 200, 0.8f, 0.8f, 0.3f);
        S(0, 11, 211, 0.3f, 0.3f);                                                 // ponto minúsculo
        var fin = Finish(-4, 11, 220, 1.5f, 1.5f);                                 // chegada que se move
        var rb = fin.GetComponent<Rigidbody>(); if (rb == null) rb = fin.AddComponent<Rigidbody>(); rb.isKinematic = true;
        var mp = fin.GetComponent<MovingPlatform>(); if (mp == null) mp = fin.AddComponent<MovingPlatform>();
        Set(mp, "offset", new Vector3(8, 0, 0)); Set(mp, "period", 5f); Set(mp, "phase", 0f); Set(mp, "pauseFraction", 0.1f);
        path[path.Count - 1].kind = Kind.Moving; path[path.Count - 1].offset = new Vector3(8, 0, 0);
        return Save(10, 0.92f);
    }

    public static string BuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Saia do Play Mode.");
        LoadMaterials();
        var report = new System.Text.StringBuilder();
        report.AppendLine(Level06());
        report.AppendLine(Level07());
        report.AppendLine(Level08());
        report.AppendLine(Level09());
        report.AppendLine(Level10());

        // build settings: Level_06..Level_10 depois da Level_05
        var scenes = EditorBuildSettings.scenes.Where(s => !Enumerable.Range(6, 5).Any(n => s.path.EndsWith($"Level_{n:00}.unity"))).ToList();
        for (int n = 6; n <= 10; n++) scenes.Add(new EditorBuildSettingsScene($"Assets/Scenes/Level_{n:00}.unity", true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        return report.ToString();
    }
}
