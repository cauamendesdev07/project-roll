using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Colisores só para a câmera (layer CameraBlock): terreno, teto da gruta, vulcão e o corpo
// dos pilares das plataformas fixas. A bola não colide com essa layer (matriz de física)
// e o teste de chão a ignora, então a jogabilidade não muda.
// Roda no fim do Build e também sozinho (AddCameraColliders), como a fauna.
public static partial class ThemedLevelBuilder
{
    public const string CameraBlockLayerName = "CameraBlock";
    private const int CameraBlockLayerIndex = 8;

    static readonly string[] CameraBlockEnvMeshes = { "Terrain", "CaveCeiling", "Volcano" };

    public static string AddCameraColliders(int level)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Saia do Play Mode.");
        var scene = EditorSceneManager.OpenScene($"Assets/Scenes/Level_{level:00}.unity", OpenSceneMode.Single);
        string result = ApplyCameraColliders(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return result;
    }

    static string ApplyCameraColliders(UnityEngine.SceneManagement.Scene scene)
    {
        int layer = EnsureCameraBlockLayer();
        int env = 0, pillars = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == EnvName)
            {
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    if (CameraBlockEnvMeshes.Contains(mf.name) && Block(mf, layer)) env++;
            }

            foreach (var vis in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Visual"))
            {
                var plat = vis.parent;
                var body = vis.Find("Body");
                if (plat == null || body == null || !IsStaticSolidPlatform(plat))
                    continue;
                if (Block(body.GetComponent<MeshFilter>(), layer)) pillars++;
            }
        }
        return $"camera: {env} env, {pillars} pilares";
    }

    // Só plataformas que nunca se movem, somem ou caem (e que têm colisor de verdade).
    static bool IsStaticSolidPlatform(Transform p)
    {
        var col = p.GetComponent<Collider>();
        if (col == null || col.isTrigger) return false;
        if (p.GetComponentInParent<Rigidbody>() != null) return false;
        return p.GetComponent<MovingPlatform>() == null && p.GetComponent<Spinner>() == null
            && p.GetComponent<CrumblingPlatform>() == null && p.GetComponent<BlinkingPlatform>() == null
            && p.GetComponent<Pendulum>() == null;
    }

    static bool Block(MeshFilter mf, int layer)
    {
        if (mf == null || mf.sharedMesh == null) return false;
        var go = mf.gameObject;
        go.layer = layer;
        var mc = go.GetComponent<MeshCollider>();
        if (mc == null) mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.sharedMesh;
        mc.convex = false;
        return true;
    }

    // Cria a layer (se faltar) e desliga a colisão dela com todas as outras.
    static int EnsureCameraBlockLayer()
    {
        int layer = LayerMask.NameToLayer(CameraBlockLayerName);
        if (layer < 0)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            var slot = layers.GetArrayElementAtIndex(CameraBlockLayerIndex);
            if (!string.IsNullOrEmpty(slot.stringValue))
                throw new InvalidOperationException($"Layer {CameraBlockLayerIndex} já está em uso: {slot.stringValue}");
            slot.stringValue = CameraBlockLayerName;
            tagManager.ApplyModifiedProperties();
            layer = CameraBlockLayerIndex;
        }
        for (int i = 0; i < 32; i++)
            Physics.IgnoreLayerCollision(layer, i, true);
        return layer;
    }

    [MenuItem("Project Roll/Colisores da câmera (todas as fases)")]
    static void AddCameraCollidersAll()
    {
        var log = new System.Text.StringBuilder();
        for (int level = 1; level <= 10; level++)
            log.AppendLine($"Fase {level}: {AddCameraColliders(level)}");
        Debug.Log(log.ToString());
    }
}
