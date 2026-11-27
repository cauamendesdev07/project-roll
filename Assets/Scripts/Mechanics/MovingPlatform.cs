using UnityEngine;

// Plataforma que vai e volta entre a posição inicial e (posição inicial + offset).
// Serve para plataformas laterais, elevadores e plataformas que se aproximam/afastam.
[RequireComponent(typeof(Rigidbody))]
public class MovingPlatform : MonoBehaviour, IPlayerSurface
{
    [SerializeField] private Vector3 offset = new Vector3(6f, 0f, 0f);
    [Tooltip("Tempo de ida e volta, em segundos.")]
    [SerializeField] private float period = 4f;
    [Tooltip("Defasagem do ciclo (0 a 1).")]
    [Range(0f, 1f)] [SerializeField] private float phase;
    [Tooltip("Fração do ciclo parada em cada ponta.")]
    [Range(0f, 0.45f)] [SerializeField] private float pauseFraction = 0.1f;

    private Rigidbody rb;
    private Vector3 startPosition;
    private Vector3 velocity;

    public float AccelerationMultiplier => 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        startPosition = transform.position;
    }

    private void FixedUpdate()
    {
        float t = Time.fixedTime + Time.fixedDeltaTime;
        Vector3 next = PositionAt(t);
        velocity = (next - rb.position) / Time.fixedDeltaTime;
        rb.MovePosition(next);
    }

    public Vector3 GetSurfaceVelocity(Vector3 worldPoint) => velocity;

    private Vector3 PositionAt(float time)
    {
        float cycle = Mathf.Repeat(time / Mathf.Max(0.1f, period) + phase, 1f);
        float half = 0.5f - pauseFraction;
        float f;

        if (cycle < pauseFraction * 0.5f) f = 0f;
        else if (cycle < pauseFraction * 0.5f + half) f = (cycle - pauseFraction * 0.5f) / half;
        else if (cycle < pauseFraction * 1.5f + half) f = 1f;
        else if (cycle < 1f - pauseFraction * 0.5f) f = 1f - (cycle - pauseFraction * 1.5f - half) / half;
        else f = 0f;

        f = f * f * (3f - 2f * f);
        return startPosition + offset * f;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 from = Application.isPlaying ? startPosition : transform.position;
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(from, from + offset);
        Gizmos.DrawWireCube(from + offset, transform.lossyScale);
    }
}
