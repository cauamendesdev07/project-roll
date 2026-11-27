using UnityEngine;

// Relâmpagos de vez em quando: a luz principal pisca forte por um instante (só visual).
public class LightningFlashes : MonoBehaviour
{
    [SerializeField] private Light targetLight;
    [SerializeField] private Color flashColor = new Color(0.8f, 0.85f, 1f);
    [SerializeField] private float flashIntensity = 3.5f;
    [SerializeField] private Vector2 interval = new Vector2(4f, 10f);

    private float baseIntensity;
    private Color baseColor;
    private float nextFlash;
    private float flashTime = -10f;

    private void Start()
    {
        if (targetLight == null)
            targetLight = RenderSettings.sun;

        baseIntensity = targetLight.intensity;
        baseColor = targetLight.color;
        nextFlash = Time.time + Random.Range(interval.x, interval.y);
    }

    private void Update()
    {
        if (Time.time >= nextFlash)
        {
            flashTime = Time.time;
            nextFlash = Time.time + Random.Range(interval.x, interval.y);
        }

        float t = Time.time - flashTime;
        // dois clarões rápidos seguidos
        float k = Mathf.Max(Mathf.Exp(-t * 18f), t > 0.12f ? Mathf.Exp(-(t - 0.12f) * 12f) * 0.7f : 0f);
        targetLight.intensity = Mathf.Lerp(baseIntensity, flashIntensity, k);
        targetLight.color = Color.Lerp(baseColor, flashColor, k);
    }
}
