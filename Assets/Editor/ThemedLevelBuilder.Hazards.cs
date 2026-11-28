using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Obstáculos que cruzam o caminho (CrossingHazard): aviões na fase 9 e balas perdidas na fase 10.
// São peças de jogo (na raiz da cena, como os martelos), não fazem parte do cenário regerável.
// As malhas e materiais ficam em Assets/Art/Shared para sobreviverem às regerações.
public static partial class ThemedLevelBuilder
{
    [MenuItem("Project Roll/Obstáculos/Aviões (fase 9)")]
    static void MenuPlanes() => Debug.Log(AddCrossingHazards(9, "Plane"));

    [MenuItem("Project Roll/Obstáculos/Balas perdidas (fase 10)")]
    static void MenuBullets() => Debug.Log(AddCrossingHazards(10, "Bullet"));

    public static string AddCrossingHazards(int level, string kind)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Saia do Play Mode.");
        var scene = EditorSceneManager.OpenScene($"Assets/Scenes/Level_{level:00}.unity", OpenSceneMode.Single);
        string prefix = $"Hazard_{kind}";
        foreach (var old in scene.GetRootGameObjects().Where(g => g.name.StartsWith(prefix)).ToArray()) Object.DestroyImmediate(old);

        // plataformas do caminho em ordem (pela distância ao início, como a fauna faz)
        var plats = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name.StartsWith("Platform_") && t.GetComponent<Collider>() != null && !t.GetComponent<Collider>().isTrigger).ToList();
        var start = plats.First(t => t.name == "Platform_Start");
        var ordered = plats.Where(t => t != start && t.name != "Platform_Finish").OrderBy(t => (t.position - start.position).sqrMagnitude).ToList();

        var log = new System.Text.StringBuilder();
        float[] fracs = kind == "Plane" ? new[] { 0.35f, 0.72f } : new[] { 0.25f, 0.5f, 0.78f };
        for (int i = 0; i < fracs.Length; i++)
        {
            int idx = Mathf.Clamp(Mathf.RoundToInt(fracs[i] * (ordered.Count - 1)), 1, ordered.Count - 2);
            var p = ordered[idx];
            var dir = ordered[idx + 1].position - ordered[idx - 1].position; dir.y = 0; dir.Normalize();
            var across = new Vector3(-dir.z, 0, dir.x);
            float top = p.GetComponent<Collider>().bounds.max.y;
            var go = kind == "Plane" ? MakePlane() : MakeBullet();
            go.name = $"{prefix} ({i})";
            float height = kind == "Plane" ? top + 1.3f : top + 0.55f;
            float reach = kind == "Plane" ? 150f : 70f;
            var a = new Vector3(p.position.x, height, p.position.z) - across * reach;
            var b = new Vector3(p.position.x, height, p.position.z) + across * reach;
            go.transform.position = a;
            var hz = go.GetComponent<CrossingHazard>();
            var so = new SerializedObject(hz);
            so.FindProperty("pointA").vector3Value = a;
            so.FindProperty("pointB").vector3Value = b;
            if (kind == "Plane")
            {
                so.FindProperty("speed").floatValue = 42f;
                so.FindProperty("interval").floatValue = 11f;
                so.FindProperty("firstDelay").floatValue = 6f + i * 4.5f;
                so.FindProperty("pushSpeed").floatValue = 12f;
                so.FindProperty("pushUp").floatValue = 3f;
                so.FindProperty("warnSound").stringValue = "plane";
                so.FindProperty("warnTime").floatValue = 2.2f;
            }
            else
            {
                so.FindProperty("speed").floatValue = 80f;   // 1,6 m por passo de física: não atravessa a bola sem detectar
                so.FindProperty("interval").floatValue = 8f;
                so.FindProperty("intervalJitter").floatValue = 3f;
                so.FindProperty("firstDelay").floatValue = 5f + i * 2.7f;
                so.FindProperty("pushSpeed").floatValue = 9f;
                so.FindProperty("pushUp").floatValue = 2f;
                so.FindProperty("warnSound").stringValue = "whizz";
                so.FindProperty("warnVolume").floatValue = 0.8f;
                so.FindProperty("warnTime").floatValue = 0.35f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"{go.name}: cruza {p.name} a y={height:0.0}");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return log.ToString();
    }

    static GameObject NewHazardRoot()
    {
        var go = new GameObject("Hazard");
        var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
        go.AddComponent<CrossingHazard>();
        return go;
    }

    static Mesh SharedMesh(string name, MB mb)
    {
        string path = $"{SharedDir}/{name}.asset";
        var mesh = mb.Build(name);
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); return existing; }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // Avião de linha estilizado (~14 m), frente em +z. Usa a paleta da fauna.
    static GameObject MakePlane()
    {
        var mb = new MB();
        Rod(mb, new Vector3(0, 0, -6.5f), new Vector3(0, 0, 5.2f), 1.05f, 1.05f, F.White, 8);
        ConeTo(mb, new Vector3(0, 0, 5.2f), new Vector3(0, -0.2f, 7.2f), 1.05f, F.White);
        ConeTo(mb, new Vector3(0, 0, -6.5f), new Vector3(0, 0.5f, -8.2f), 1.0f, F.White);
        Ell(mb, new Vector3(0, 0.25f, 0), new Vector3(1.07f, 0.22f, 5.5f), F.Blue, 0);           // faixa das janelas
        Ell(mb, new Vector3(0, 0.25f, 6.0f), new Vector3(0.6f, 0.3f, 0.4f), F.Black, 0);          // cabine
        foreach (float sx in new[] { -1f, 1f })
        {
            mb.Quad(new Vector3(0, -0.3f, 1.4f), new Vector3(sx * 7f, 0.2f, -1.2f), new Vector3(sx * 7f, 0.2f, -2.3f), new Vector3(0, -0.3f, -1.4f), FU(F.Gray, 4), Vector3.up);
            mb.Quad(new Vector3(0, -0.4f, -1.4f), new Vector3(sx * 7f, 0.1f, -2.3f), new Vector3(sx * 7f, 0.1f, -1.2f), new Vector3(0, -0.4f, 1.4f), FU(F.Gray, 3), Vector3.down);
            Rod(mb, new Vector3(sx * 2.8f, -0.9f, 0.6f), new Vector3(sx * 2.8f, -0.9f, -1.6f), 0.5f, 0.42f, F.Gray, 6);
            mb.Quad(new Vector3(0, 0.3f, -6.2f), new Vector3(sx * 2.8f, 0.5f, -7.4f), new Vector3(sx * 2.8f, 0.5f, -8f), new Vector3(0, 0.3f, -7.6f), FU(F.Gray, 4), Vector3.up);
        }
        mb.Quad(new Vector3(0, 0.8f, -5.6f), new Vector3(0, 3.6f, -7.6f), new Vector3(0, 3.6f, -8.4f), new Vector3(0, 0.8f, -7.6f), FU(F.Blue, 4), Vector3.right);
        mb.Quad(new Vector3(0, 0.8f, -7.6f), new Vector3(0, 3.6f, -8.4f), new Vector3(0, 3.6f, -7.6f), new Vector3(0, 0.8f, -5.6f), FU(F.Blue, 4), Vector3.left);
        var mesh = SharedMesh("Hazard_Airliner", mb);
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{SharedDir}/M_Fauna.mat");

        var go = NewHazardRoot();
        var vis = new GameObject("Visual");
        vis.transform.SetParent(go.transform, false);
        vis.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = vis.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        var body = new GameObject("HitBody"); body.transform.SetParent(go.transform, false);
        var bc = body.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(2.3f, 2.3f, 15f); bc.center = new Vector3(0, 0, -0.5f);
        var wing = new GameObject("HitWings"); wing.transform.SetParent(go.transform, false);
        var wc = wing.AddComponent<BoxCollider>(); wc.isTrigger = true; wc.size = new Vector3(14f, 0.9f, 3f); wc.center = new Vector3(0, -0.1f, -0.5f);
        return go;
    }

    // Bala perdida: risco brilhante curto com rastro.
    static GameObject MakeBullet()
    {
        var mb = new MB();
        Rod(mb, new Vector3(0, 0, -0.5f), new Vector3(0, 0, 0.4f), 0.09f, 0.09f, F.Yellow, 6);
        ConeTo(mb, new Vector3(0, 0, 0.4f), new Vector3(0, 0, 0.6f), 0.09f, F.Orange);
        var mesh = SharedMesh("Hazard_Bullet", mb);
        var glow = MakeMat($"{SharedDir}/M_Tracer.mat", Shader.Find("Universal Render Pipeline/Unlit"), m =>
        {
            m.SetColor("_BaseColor", new Color(3f, 2.2f, 0.9f, 1f));
        });

        var go = NewHazardRoot();
        var vis = new GameObject("Visual");
        vis.transform.SetParent(go.transform, false);
        vis.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = vis.AddComponent<MeshRenderer>(); mr.sharedMaterial = glow; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var tr = vis.AddComponent<TrailRenderer>();
        tr.sharedMaterial = glow; tr.time = 0.12f; tr.minVertexDistance = 0.2f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.16f), new Keyframe(1f, 0f));
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var sc = go.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 0.6f;
        return go;
    }
}
