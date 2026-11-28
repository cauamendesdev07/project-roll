
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
    [Tooltip("Ainda dá para pular por este tempo depois de sair da beirada.")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("Pulo apertado até este tempo antes de pousar é feito ao pousar.")]
    [SerializeField] private float jumpBuffer = 0.1f;
    [Tooltip("Raio do teste de chão, em fração do raio da bola.")]
    [SerializeField] private float groundProbeScale = 0.9f;

    private static int groundMask;
    private static int GroundMask
    {
        get
        {
            if (groundMask == 0)
            {
                groundMask = Physics.DefaultRaycastLayers;
                int cameraBlock = LayerMask.NameToLayer("CameraBlock");
                if (cameraBlock >= 0) groundMask &= ~(1 << cameraBlock);
            }
            return groundMask;
        }
    }

    // Pouso (velocidade de queda, ponto no chão): usado por efeitos visuais.
    public event System.Action<float, Vector3> Landed;

    private Rigidbody rb;
    private Vector2 movementInput;
    private bool isGrounded;

    // Tolerâncias do pulo (tempos de física).
    private float lastGroundedTime = -10f;
    private float lastJumpPressTime = -10f;
    private float jumpLockUntil;
    private float ballRadius = 0.5f;

    // Superfície sob a bola (plataforma móvel, esteira, gelo...) e velocidades externas (vento).
    private IPlayerSurface groundSurface;
    private Vector3 groundPoint;
    private Vector3 inheritedVelocity;
    private Vector3 pendingExternalVelocity;

    // Som: queda acumulada no ar para o volume do pouso.
    private int airSteps;
    private float maxFallSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        var sphere = GetComponent<SphereCollider>();
        if (sphere != null)
            ballRadius = sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

        // Depois de renascer, a queda no vazio não conta como pouso.
        var checkpoint = GetComponent<CheckpointSystem>();
        if (checkpoint != null)
            checkpoint.Respawned += () => { airSteps = 0; maxFallSpeed = 0f; };
    }

    private void Update()
    {
        movementInput = Vector2.zero;

        if (Keyboard.current == null || GameManager.InputBlocked)
            return;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            movementInput.y += 1;

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            movementInput.y -= 1;

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            movementInput.x += 1;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            movementInput.x -= 1;

        // Guarda o momento do aperto; o FixedUpdate decide se pula (buffer + coyote time).
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            lastJumpPressTime = Time.time;
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

        TryJump();
    }

    private void TryJump()
    {
        float now = Time.time;
        bool pressed = now - lastJumpPressTime <= jumpBuffer;
        bool canJump = now >= jumpLockUntil && now - lastGroundedTime <= coyoteTime;
        if (!pressed || !canJump)
            return;

        // Pulando no coyote time a bola já pode estar caindo: zera a queda para o pulo ter a altura normal.
        Vector3 v = rb.linearVelocity;
        if (v.y < 0f)
            rb.linearVelocity = new Vector3(v.x, 0f, v.z);

        AudioManager.Play("jump", 0.55f, 1f, 0.05f);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        // Sem pulo duplo: consome o aperto e o chão, e ignora o chão logo abaixo por um instante.
        lastJumpPressTime = -10f;
        lastGroundedTime = -10f;
        jumpLockUntil = now + 0.15f;
        isGrounded = false;
    }

    private void CheckGround()
    {
        bool wasGrounded = isGrounded;

        // Esfera um pouco menor que a bola, descendo até o mesmo alcance do antigo raio: pega a quina
        // de vigas e blocos finos mesmo quando o centro da bola já passou da beirada.
        // Ignora triggers (vento, checkpoints, chegada) e os colisores só da câmera (CameraBlock).
        float probe = ballRadius * groundProbeScale;
        isGrounded = Physics.SphereCast(
            transform.position,
            probe,
            Vector3.down,
            out RaycastHit hit,
            Mathf.Max(0.01f, groundCheckDistance - probe),
            GroundMask,
            QueryTriggerInteraction.Ignore
        ) && hit.normal.y > 0.2f;

        // Reserva: o raio antigo (se a esfera começou encostada em algo, ela não acusa).
        if (!isGrounded)
            isGrounded = Physics.Raycast(transform.position, Vector3.down, out hit, groundCheckDistance, GroundMask, QueryTriggerInteraction.Ignore);

        if (isGrounded && Time.time >= jumpLockUntil)
            lastGroundedTime = Time.time;

        groundSurface = isGrounded ? hit.collider.GetComponentInParent<IPlayerSurface>() : null;
        groundPoint = isGrounded ? hit.point : Vector3.zero;

        UpdateSounds(wasGrounded);
    }

    // Só áudio: pouso com volume pelo impacto e rolamento pela velocidade.
    private void UpdateSounds(bool wasGrounded)
    {
        Vector3 v = rb.linearVelocity;

        if (!isGrounded)
        {
            airSteps++;
            maxFallSpeed = Mathf.Max(maxFallSpeed, -v.y);
        }
        else
        {
            if (!wasGrounded && airSteps > 6 && maxFallSpeed > 2.5f)
            {
                AudioManager.Play("land", Mathf.Clamp01((maxFallSpeed - 2f) / 12f) * 0.9f + 0.1f, 1f, 0.06f);
                Landed?.Invoke(maxFallSpeed, groundPoint);
            }
            airSteps = 0;
            maxFallSpeed = 0f;
        }

        // velocidade em relação à superfície (parado numa plataforma móvel não "rola")
        AudioManager.SetRolling(new Vector2(v.x - inheritedVelocity.x, v.z - inheritedVelocity.z).magnitude, isGrounded);
    }
}
