using System.Collections;
using UnityEngine;

// Treme quando a bola pisa, cai e some; depois de um tempo volta ao lugar.
public class CrumblingPlatform : MonoBehaviour
{
    [Tooltip("Tempo tremendo antes de cair.")]
    [SerializeField] private float delay = 0.6f;
    [Tooltip("Tempo até reaparecer depois de cair.")]
    [SerializeField] private float respawnTime = 3f;
    [SerializeField] private Color warningColor = new Color(0.9f, 0.2f, 0.15f);

    private Collider platformCollider;
    private Renderer platformRenderer;
    private Vector3 startPosition;
    private Color startColor;
    private bool busy;

    private void Awake()
    {
        platformCollider = GetComponent<Collider>();
        platformRenderer = GetComponent<Renderer>();
        startPosition = transform.position;
        startColor = platformRenderer.material.color;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (busy || !collision.collider.CompareTag("Player"))
            return;

        StartCoroutine(Crumble());
    }

    private IEnumerator Crumble()
    {
        busy = true;
        float t = 0f;

        while (t < delay)
        {
            t += Time.deltaTime;
            float k = t / delay;
            platformRenderer.material.color = Color.Lerp(startColor, warningColor, k);
            transform.position = startPosition + Random.insideUnitSphere * 0.06f * k;
            yield return null;
        }

        platformCollider.enabled = false;
        t = 0f;

        while (t < 0.6f)
        {
            t += Time.deltaTime;
            transform.position = startPosition + Vector3.down * (t * t * 12f);
            yield return null;
        }

        platformRenderer.enabled = false;
        yield return new WaitForSeconds(Mathf.Max(0f, respawnTime - 0.6f));

        transform.position = startPosition;
        platformRenderer.material.color = startColor;
        platformRenderer.enabled = true;
        platformCollider.enabled = true;
        busy = false;
    }
}
