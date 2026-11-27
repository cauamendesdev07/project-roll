using System.Collections.Generic;
using UnityEngine;

// Aparece e some num ritmo fixo. Quando está apagada, fica um "fantasma" transparente
// mostrando onde vai voltar; pouco antes de sumir, pisca como aviso.
// O efeito vale para o bloco e para as peças visuais filhas (cenário).
public class BlinkingPlatform : MonoBehaviour
{
    [SerializeField] private float period = 3f;
    [Range(0.05f, 0.95f)] [SerializeField] private float onFraction = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float phase;
    [SerializeField] private float warningTime = 0.45f;
    [SerializeField] private Material ghostMaterial;

    private Collider platformCollider;
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Material[]> solidMaterials = new List<Material[]>();
    private readonly List<Material[]> ghostMaterials = new List<Material[]>();
    private int lastState = -1;

    private void Awake()
    {
        platformCollider = GetComponent<Collider>();

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled || r is ParticleSystemRenderer)
                continue;

            renderers.Add(r);
            solidMaterials.Add(r.sharedMaterials);
            var ghosts = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < ghosts.Length; i++)
                ghosts[i] = ghostMaterial != null ? ghostMaterial : r.sharedMaterials[i];
            ghostMaterials.Add(ghosts);
        }
    }

    private void FixedUpdate()
    {
        float cycle = Mathf.Repeat(Time.timeSinceLevelLoad / Mathf.Max(0.1f, period) + phase, 1f);
        bool on = cycle < onFraction;
        float timeLeft = (onFraction - cycle) * period;
        bool warning = on && timeLeft < warningTime && Mathf.Repeat(timeLeft * 8f, 1f) < 0.5f;

        platformCollider.enabled = on;

        // 0 = sólida, 1 = fantasma/aviso, 2 = escondida
        int state = on && !warning ? 0 : (ghostMaterial != null ? 1 : (on ? 0 : 2));
        if (state == lastState)
            return;

        lastState = state;
        for (int i = 0; i < renderers.Count; i++)
        {
            renderers[i].enabled = state != 2;
            renderers[i].sharedMaterials = state == 0 ? solidMaterials[i] : ghostMaterials[i];
        }
    }
}
