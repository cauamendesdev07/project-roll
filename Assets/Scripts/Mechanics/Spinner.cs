using UnityEngine;

// Gira continuamente em torno de um eixo. Usado em barras para montar (a bola é carregada)
// e em barras varredoras vermelhas que empurram a bola para fora.
[RequireComponent(typeof(Rigidbody))]
public class Spinner : MonoBehaviour, IPlayerSurface
{
    [Tooltip("Graus por segundo (negativo gira no sentido contrário).")]
    [SerializeField] private float degreesPerSecond = 45f;
    [SerializeField] private Vector3 axis = Vector3.up;

    private Rigidbody rb;
    private Quaternion startRotation;

    public float AccelerationMultiplier => 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        startRotation = transform.rotation;
    }

    private void FixedUpdate()
    {
        float angle = degreesPerSecond * (Time.fixedTime + Time.fixedDeltaTime);
        rb.MoveRotation(Quaternion.AngleAxis(angle, axis.normalized) * startRotation);
    }

    public Vector3 GetSurfaceVelocity(Vector3 worldPoint)
    {
        Vector3 omega = axis.normalized * degreesPerSecond * Mathf.Deg2Rad;
        return Vector3.Cross(omega, worldPoint - rb.position);
    }
}
