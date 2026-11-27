
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimentação")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 15f;
    [SerializeField] private Transform cameraTransform;

    [Header("Pulo")]
    [SerializeField] private float jumpForce = 9f;
    [SerializeField] private float groundCheckDistance = 0.7f;

    private Rigidbody rb;
    private Vector2 movementInput;
    private bool jumpRequested;
    private bool isGrounded;

    // Superfície sob a bola (plataforma móvel, esteira, gelo...) e velocidades externas (vento).
    private IPlayerSurface groundSurface;
    private Vector3 groundPoint;
    private Vector3 inheritedVelocity;
    private Vector3 pendingExternalVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        movementInput = Vector2.zero;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            movementInput.y += 1;

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            movementInput.y -= 1;

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            movementInput.x += 1;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            movementInput.x -= 1;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
            jumpRequested = true;
    }

    // Chamado por áreas como o vento: soma uma velocidade ao alvo neste passo de física.
    public void AddExternalVelocity(Vector3 velocity)
    {
        pendingExternalVelocity += velocity;
    }

    private void FixedUpdate()
    {
        CheckGround();

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 movement = (
            right * movementInput.x +
            forward * movementInput.y
        ).normalized;

        // No chão, a bola acompanha a superfície (plataforma móvel, esteira).
        // No ar, mantém a velocidade herdada da última superfície até pousar.
        float accelerationMultiplier = 1f;

        if (isGrounded)
        {
            if (groundSurface != null)
            {
                inheritedVelocity = groundSurface.GetSurfaceVelocity(groundPoint);
                accelerationMultiplier = groundSurface.AccelerationMultiplier;
            }
            else
            {
                inheritedVelocity = Vector3.zero;
            }
        }

        Vector3 externalVelocity = inheritedVelocity + pendingExternalVelocity;
        externalVelocity.y = 0f;
        pendingExternalVelocity = Vector3.zero;

        Vector3 targetVelocity = movement * moveSpeed + externalVelocity;

        Vector3 currentVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            rb.linearVelocity.z
        );

        Vector3 newVelocity = Vector3.MoveTowards(
            currentVelocity,
            targetVelocity,
            acceleration * accelerationMultiplier * Time.fixedDeltaTime
        );

        rb.linearVelocity = new Vector3(
            newVelocity.x,
            rb.linearVelocity.y,
            newVelocity.z
        );

        if (jumpRequested)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            jumpRequested = false;
            isGrounded = false;
        }
    }

    private void CheckGround()
    {
        // Ignora triggers: áreas de vento, checkpoints e a chegada não contam como chão.
        isGrounded = Physics.Raycast(
            transform.position,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        groundSurface = isGrounded ? hit.collider.GetComponentInParent<IPlayerSurface>() : null;
        groundPoint = isGrounded ? hit.point : Vector3.zero;
    }
}
