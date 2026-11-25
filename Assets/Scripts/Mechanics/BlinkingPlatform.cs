using UnityEngine;

// Aparece e some num ritmo fixo. Quando está apagada, fica um "fantasma" transparente
// mostrando onde vai voltar; pouco antes de sumir, pisca como aviso.
public class BlinkingPlatform : MonoBehaviour
{
    [SerializeField] private float period = 3f;
    [Range(0.05f, 0.95f)] [SerializeField] private float onFraction = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float phase;
    [SerializeField] private float warningTime = 0.45f;
    [SerializeField] private Material ghostMaterial;

    private Collider platformCollider;
    private Renderer platformRenderer;
    private Material solidMaterial;

    private void Awake()
    {
        platformCollider = GetComponent<Collider>();
        platformRenderer = GetComponent<Renderer>();
        solidMaterial = platformRenderer.sharedMaterial;
    }

    private void FixedUpdate()
    {
        float cycle = Mathf.Repeat(Time.timeSinceLevelLoad / Mathf.Max(0.1f, period) + phase, 1f);
        bool on = cycle < onFraction;
        float timeLeft = (onFraction - cycle) * period;
        bool warning = on && timeLeft < warningTime && Mathf.Repeat(timeLeft * 8f, 1f) < 0.5f;

        platformCollider.enabled = on;
        platformRenderer.enabled = on || ghostMaterial != null;
        platformRenderer.sharedMaterial = on && !warning ? solidMaterial : (ghostMaterial != null ? ghostMaterial : solidMaterial);
    }
}
