using UnityEngine;

// Balança de um lado para o outro em torno do pivô (o próprio objeto).
// O peso fica num filho; ao bater na bola, empurra ela para fora.
[RequireComponent(typeof(Rigidbody))]
public class Pendulum : MonoBehaviour
{
    [SerializeField] private float amplitude = 60f;
    [SerializeField] private float period = 2.5f;
    [Range(0f, 1f)] [SerializeField] private float phase;
    [Tooltip("Eixo de balanço, no espaço do mundo.")]
    [SerializeField] private Vector3 swingAxis = Vector3.forward;

    private Rigidbody rb;
    private Quaternion startRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        startRotation = transform.rotation;
    }

    private void FixedUpdate()
    {
        float t = Time.fixedTime + Time.fixedDeltaTime;
        float angle = amplitude * Mathf.Sin((t / Mathf.Max(0.1f, period) + phase) * Mathf.PI * 2f);
        rb.MoveRotation(Quaternion.AngleAxis(angle, swingAxis.normalized) * startRotation);
    }
}
