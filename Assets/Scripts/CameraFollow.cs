
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

    private float horizontalRotation;
    private float verticalRotation = 20f;

    private void Start()
    {
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

        Vector3 cameraPosition = targetPosition -
            rotation * Vector3.forward * distance;

        transform.position = cameraPosition;
        transform.LookAt(targetPosition);
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
