using UnityEngine;

// Luzes das janelas da cidade: a cada relâmpago a energia cai (as janelas apagam) e volta
// alguns segundos depois, piscando. Usa uma cópia do material em tempo de jogo
// (não altera o asset) e troca nos InstancedDecor que desenham as janelas.
public class CityLights : MonoBehaviour
{
    [SerializeField] private Material windows;
    [SerializeField] private Vector2 blackout = new Vector2(2.5f, 4.5f);

    private Material runtime;
    private Color baseEmission;
    private float offUntil = -1f;
    private float flickerEnd = -1f;

    private void Awake()
    {
        if (windows == null || !Application.isPlaying)
            return;
        runtime = new Material(windows) { name = windows.name + " (runtime)" };
        baseEmission = windows.GetColor("_EmissionColor");
        foreach (var d in FindObjectsByType<InstancedDecor>(FindObjectsInactive.Include))
            if (d.SharedMaterial == windows) d.UseMaterial(runtime);
        LightningFlashes.Struck += OnStrike;
    }

    private void OnDestroy()
    {
        LightningFlashes.Struck -= OnStrike;
        if (runtime != null) Destroy(runtime);
    }

    private void OnStrike()
    {
        // o apagão começa um instante depois do clarão
        offUntil = Time.time + Random.Range(blackout.x, blackout.y);
        flickerEnd = offUntil + 0.8f;
    }

    private void Update()
    {
        if (runtime == null)
            return;
        float level = 1f;
        if (Time.time < offUntil) level = 0f;
        else if (Time.time < flickerEnd) level = Random.value < 0.55f ? 1f : 0.1f;
        runtime.SetColor("_EmissionColor", baseEmission * level);
    }
}
