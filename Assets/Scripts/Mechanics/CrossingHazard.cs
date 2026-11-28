using UnityEngine;

// Obstáculo que cruza o caminho de tempos em tempos em linha reta (avião na fase 9, bala perdida na 10).
// Avisa com um som antes de chegar; se encostar na bola, dá um tranco no sentido do movimento
// (não mata: pode derrubar da plataforma). Fica escondido entre uma passagem e outra.
[RequireComponent(typeof(Rigidbody))]
public class CrossingHazard : MonoBehaviour
{
    [SerializeField] private Vector3 pointA;
    [SerializeField] private Vector3 pointB;
    [SerializeField] private float speed = 45f;
    [Tooltip("Segundos entre o início de uma passagem e o da próxima.")]
    [SerializeField] private float interval = 11f;
    [Tooltip("Variação aleatória do intervalo (±).")]
    [SerializeField] private float intervalJitter = 0f;
    [Tooltip("Atraso da primeira passagem.")]
    [SerializeField] private float firstDelay = 5f;
    [Tooltip("Alterna o sentido a cada passagem.")]
    [SerializeField] private bool pingPong = true;
    [SerializeField] private float pushSpeed = 12f;
    [SerializeField] private float pushUp = 3f;
    [Tooltip("Som (Resources/Audio/Sfx) tocado antes de chegar ao meio do caminho.")]
    [SerializeField] private string warnSound = "plane";
    [SerializeField] private float warnVolume = 0.9f;
    [Tooltip("Quanto tempo antes de passar pelo meio o som começa.")]
    [SerializeField] private float warnTime = 2f;

    private Rigidbody rb;
    private Renderer[] renderers;
    private Collider[] colliders;
    private float nextStart;
    private float flightTime;
    private float started = -1f;
    private bool reverse;
    private bool warned;
    private float lastHit = -10f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        flightTime = Vector3.Distance(pointA, pointB) / Mathf.Max(1f, speed);
        nextStart = firstDelay;
        SetVisible(false);
        rb.position = pointA;
        transform.position = pointA;
    }

    private void SetVisible(bool on)
    {
        foreach (var r in renderers) r.enabled = on;
        foreach (var c in colliders) c.enabled = on;
    }

    private void FixedUpdate()
    {
        float t = Time.timeSinceLevelLoad;

        if (started < 0f)
        {
            // aviso: o som começa antes de a passagem chegar ao meio
            if (!warned && !string.IsNullOrEmpty(warnSound) && t >= nextStart + flightTime * 0.5f - warnTime)
            {
                AudioManager.Play(warnSound, warnVolume, 1f, 0.06f);
                warned = true;
            }
            if (t < nextStart)
                return;
            started = t;
            // salta para o começo (sem arrastar) e limpa o rastro da passagem anterior
            Vector3 begin = reverse ? pointB : pointA;
            rb.position = begin;
            transform.position = begin;
            foreach (var trail in GetComponentsInChildren<TrailRenderer>()) trail.Clear();
            SetVisible(true);
        }

        float k = (t - started) / flightTime;
        if (k >= 1f)
        {
            started = -1f;
            warned = false;
            SetVisible(false);
            if (pingPong) reverse = !reverse;
            nextStart = t + Mathf.Max(flightTime + 1f, interval - flightTime + Random.Range(-intervalJitter, intervalJitter));
            return;
        }

        Vector3 from = reverse ? pointB : pointA, to = reverse ? pointA : pointB;
        rb.MovePosition(Vector3.Lerp(from, to, k));
        rb.MoveRotation(Quaternion.LookRotation(to - from, Vector3.up));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || Time.time - lastHit < 0.5f)
            return;
        var body = other.attachedRigidbody;
        if (body == null)
            return;
        lastHit = Time.time;
        Vector3 dir = (reverse ? pointA - pointB : pointB - pointA);
        dir.y = 0f;
        dir.Normalize();
        body.AddForce(dir * pushSpeed + Vector3.up * pushUp, ForceMode.VelocityChange);
        AudioManager.Play("bounce", 0.6f, 0.7f, 0.05f);
    }
}
