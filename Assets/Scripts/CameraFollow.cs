
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [Header("Alvo")]
    [SerializeField] private Transform target;

    [Header("Distância e altura")]
    [SerializeField] private float distance = 10f;
    [SerializeField] private float height = 2f;

    [Header("Controle do mouse")]
    [SerializeField] private float minVerticalAngle = -20f;
    [SerializeField] private float maxVerticalAngle = 70f;

    [Header("Colisão")]
    [Tooltip("Raio da esfera que testa obstáculos entre a bola e a câmera.")]
    [SerializeField] private float collisionRadius = 0.35f;
    [SerializeField] private float minDistance = 1.5f;
    [Tooltip("Tempo para voltar à distância normal depois de um obstáculo.")]
    [SerializeField] private float returnTime = 0.35f;

    private float horizontalRotation;
    private float currentDistance = -1f;
    private float distanceVelocity;
    private int obstacleMask;
    private readonly RaycastHit[] hits = new RaycastHit[16];
    private float verticalRotation = 20f;

    private void Start()
    {
        obstacleMask = LayerMask.GetMask("Default", "CameraBlock");
        currentDistance = distance;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (target == null || Mouse.current == null)
            return;

        // Pausado ou na tela final: a câmera não gira com o mouse.
        if (GameManager.InputBlocked)
            return;

        // Sensibilidade e eixo invertido vêm das opções (GameSettings).
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float sensitivity = GameSettings.MouseSensitivity;
        float invert = GameSettings.InvertY ? -1f : 1f;

        horizontalRotation += mouseDelta.x * sensitivity;
        verticalRotation -= mouseDelta.y * sensitivity * invert;

        verticalRotation = Mathf.Clamp(
            verticalRotation,
            minVerticalAngle,
            maxVerticalAngle
        );

        Quaternion rotation = Quaternion.Euler(
            verticalRotation,
            horizontalRotation,
            0f
        );

        Vector3 targetPosition = target.position + Vector3.up * height;

        Vector3 back = rotation * Vector3.back;
        float wanted = Mathf.Max(minDistance, ObstacleDistance(targetPosition, back));

        // Aproxima na hora (para não entrar na parede) e volta suavemente.
        if (currentDistance < 0f || wanted < currentDistance)
        {
            currentDistance = wanted;
            distanceVelocity = 0f;
        }
        else
        {
            currentDistance = Mathf.SmoothDamp(currentDistance, wanted, ref distanceVelocity, returnTime);
        }

        transform.position = targetPosition + back * currentDistance;
        transform.LookAt(targetPosition);
    }

    // Distância livre da bola até a câmera (ignora a própria bola e triggers).
    private float ObstacleDistance(Vector3 origin, Vector3 direction)
    {
        float free = distance;
        int count = Physics.SphereCastNonAlloc(origin, collisionRadius, direction, hits, distance, obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var h = hits[i];
            if (h.distance <= 0f) continue; // começou dentro do colisor: não há o que fazer
            if (h.collider.transform == target || h.collider.transform.IsChildOf(target)) continue;
            if (h.distance < free) free = h.distance;
        }
        return free;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
