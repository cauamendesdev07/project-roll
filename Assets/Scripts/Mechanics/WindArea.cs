using UnityEngine;

// Área de vento (trigger): empurra a bola enquanto ela está dentro, no chão ou no ar.
// Com rajadas, a força oscila entre (1 - gust) e (1 + gust) vezes a velocidade base.
[RequireComponent(typeof(BoxCollider))]
public class WindArea : MonoBehaviour
{
    [SerializeField] private Vector3 windVelocity = new Vector3(4f, 0f, 0f);
    [Range(0f, 1f)] [SerializeField] private float gust;
    [SerializeField] private float gustPeriod = 2.5f;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
            return;

        float factor = 1f + gust * Mathf.Sin(Time.time / Mathf.Max(0.1f, gustPeriod) * Mathf.PI * 2f);
        player.AddExternalVelocity(windVelocity * factor);
    }
}
