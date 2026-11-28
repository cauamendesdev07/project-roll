using UnityEngine;

// Esteira: empurra a bola numa direção enquanto ela está em cima.
// As setas da textura andam no sentido em que a esteira empurra.
public class ConveyorBelt : MonoBehaviour, IPlayerSurface
{
    [Tooltip("Direção no espaço local da esteira.")]
    [SerializeField] private Vector3 localDirection = Vector3.forward;
    [SerializeField] private float speed = 3f;
    [Tooltip("Comprimento, em metros, de cada repetição das setas.")]
    [SerializeField] private float arrowSpacing = 1.5f;

    private Renderer beltRenderer;
    private MaterialPropertyBlock block;
    private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

    public float AccelerationMultiplier => 1f;

    private void Awake()
    {
        beltRenderer = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (beltRenderer == null)
            return;

        Vector3 scale = transform.lossyScale;
        float tilesAlong = scale.z / Mathf.Max(0.1f, arrowSpacing);
        float offset = -Mathf.Repeat(Time.time * speed / Mathf.Max(0.1f, arrowSpacing), 1f);

        beltRenderer.GetPropertyBlock(block);
        block.SetVector(BaseMapST, new Vector4(1f, tilesAlong, 0f, offset));
        beltRenderer.SetPropertyBlock(block);
    }

    public Vector3 GetSurfaceVelocity(Vector3 worldPoint)
    {
        Vector3 dir = transform.TransformDirection(localDirection);
        dir.y = 0f;
        return dir.normalized * speed;
    }
}
