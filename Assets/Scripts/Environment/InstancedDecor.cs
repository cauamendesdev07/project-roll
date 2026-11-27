using System;
using UnityEngine;
using UnityEngine.Rendering;

// Desenha muitas cópias de uma malha (árvores, mato, pedras) com GPU instancing.
// Guardar só posição/rotação/escala deixa as cenas e os assets bem menores
// do que juntar tudo numa malha única. Apenas visual: não tem colisão.
[ExecuteAlways]
public class InstancedDecor : MonoBehaviour
{
    [Serializable]
    public struct Instance
    {
        public Vector3 position;
        public Quaternion rotation;
        public float scale;
    }

    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;
    [SerializeField] private bool castShadows = true;
    [SerializeField] private Bounds worldBounds;
    [SerializeField] private Instance[] instances = Array.Empty<Instance>();

    private Matrix4x4[] matrices;

    private void OnEnable()
    {
        BuildMatrices();
        RenderPipelineManager.beginCameraRendering += Draw;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Draw;
    }

    private void OnValidate()
    {
        BuildMatrices();
    }

#if UNITY_EDITOR
    // Usado pelo gerador de cenários.
    public void SetData(Mesh newMesh, Material newMaterial, bool shadows, Bounds bounds, Instance[] data)
    {
        mesh = newMesh;
        material = newMaterial;
        castShadows = shadows;
        worldBounds = bounds;
        instances = data;
        BuildMatrices();
    }
#endif

    private void BuildMatrices()
    {
        if (instances == null)
            return;

        matrices = new Matrix4x4[instances.Length];
        for (int i = 0; i < instances.Length; i++)
            matrices[i] = Matrix4x4.TRS(instances[i].position, instances[i].rotation, Vector3.one * instances[i].scale);
    }

    private void Draw(ScriptableRenderContext context, Camera cam)
    {
        if (mesh == null || material == null || matrices == null || matrices.Length == 0)
            return;

        var rp = new RenderParams(material)
        {
            camera = cam,
            worldBounds = worldBounds,
            shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
            receiveShadows = true,
            layer = gameObject.layer,
        };

        for (int start = 0; start < matrices.Length; start += 1023)
        {
            int count = Mathf.Min(1023, matrices.Length - start);
            Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, count, start);
        }
    }
}
