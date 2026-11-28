using UnityEngine;

// Câmera do menu: gira devagar em volta da bola enquanto o robozinho joga.
public class DemoCamera : MonoBehaviour
{
    [SerializeField] private float distance = 13f;
    [SerializeField] private float height = 6.5f;
    [SerializeField] private float degreesPerSecond = 7f;
    [Tooltip("Desloca o enquadramento para a bola ficar à esquerda dos botões do menu.")]
    [SerializeField] private float lookOffsetRight = 4.5f;

    private Transform target;
    private float angle;
    private Vector3 velocity;

    public void Init(Transform ball, float startYaw)
    {
        target = ball;
        angle = startYaw;
        transform.position = Desired();
        transform.rotation = Quaternion.LookRotation(LookPoint() - transform.position, Vector3.up);
    }

    private Vector3 LookPoint() => target.position + Vector3.up * 0.8f + Quaternion.Euler(0f, angle, 0f) * Vector3.right * lookOffsetRight;

    private Vector3 Desired() => target.position + Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, -distance) + Vector3.up * height;

    private void LateUpdate()
    {
        if (target == null) return;
        angle += degreesPerSecond * Time.deltaTime;
        transform.position = Vector3.SmoothDamp(transform.position, Desired(), ref velocity, 0.7f);
        var look = Quaternion.LookRotation(LookPoint() - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 4f);
    }
}
