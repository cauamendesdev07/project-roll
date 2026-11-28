using UnityEngine;

// Desloca a textura do material ao longo do tempo (água do rio, cachoeira, névoa).
// Usa MaterialPropertyBlock para não criar cópias do material.
[RequireComponent(typeof(Renderer))]
public class ScrollingTexture : MonoBehaviour
{
    [SerializeField] private Vector2 speed = new Vector2(0f, 0.05f);
    [SerializeField] private string textureProperty = "_BaseMap";

    private Renderer targetRenderer;
    private MaterialPropertyBlock block;
    private int scaleOffsetId;
    private Vector4 baseScaleOffset;

    private void Awake() => Init();

    // Também chamado no Update: depois de recompilar com o jogo rodando, o Awake não roda de novo.
    private void Init()
    {
        targetRenderer = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        scaleOffsetId = Shader.PropertyToID(textureProperty + "_ST");

        Material material = targetRenderer.sharedMaterial;
        baseScaleOffset = material != null && material.HasProperty(scaleOffsetId)
            ? material.GetVector(scaleOffsetId)
            : new Vector4(1f, 1f, 0f, 0f);
    }

    private void Update()
    {
        if (block == null) Init();
        Vector2 offset = speed * Time.time;

        targetRenderer.GetPropertyBlock(block);
        block.SetVector(scaleOffsetId, new Vector4(
            baseScaleOffset.x,
            baseScaleOffset.y,
            baseScaleOffset.z + offset.x,
            baseScaleOffset.w + offset.y));
        targetRenderer.SetPropertyBlock(block);
    }
}
