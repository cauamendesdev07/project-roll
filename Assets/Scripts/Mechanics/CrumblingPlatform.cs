using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Treme quando a bola pisa, cai e some; depois de um tempo volta ao lugar.
// O efeito vale para o bloco e para as peças visuais filhas (cenário).
public class CrumblingPlatform : MonoBehaviour
{
    [Tooltip("Tempo tremendo antes de cair.")]
    [SerializeField] private float delay = 0.6f;
    [Tooltip("Tempo até reaparecer depois de cair.")]
    [SerializeField] private float respawnTime = 3f;
    [SerializeField] private Color warningColor = new Color(0.9f, 0.2f, 0.15f);

    private Collider platformCollider;
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Color> startColors = new List<Color>();
    private Vector3 startPosition;
    private bool busy;

    private void Awake()
    {
        platformCollider = GetComponent<Collider>();
        startPosition = transform.position;

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled || r is ParticleSystemRenderer)
                continue;

            renderers.Add(r);
            startColors.Add(r.material.HasProperty("_BaseColor") ? r.material.GetColor("_BaseColor") : Color.white);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (busy || !collision.collider.CompareTag("Player"))
            return;

        StartCoroutine(Crumble());
    }

    private void SetTint(float k)
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i].material.HasProperty("_BaseColor"))
                renderers[i].material.SetColor("_BaseColor", Color.Lerp(startColors[i], warningColor, k));
        }
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers)
            r.enabled = visible;
    }

    private IEnumerator Crumble()
    {
        busy = true;
        float t = 0f;

        while (t < delay)
        {
            t += Time.deltaTime;
            float k = t / delay;
            SetTint(k);
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

        SetVisible(false);
        yield return new WaitForSeconds(Mathf.Max(0f, respawnTime - 0.6f));

        transform.position = startPosition;
        SetTint(0f);
        SetVisible(true);
        platformCollider.enabled = true;
        busy = false;
    }
}
