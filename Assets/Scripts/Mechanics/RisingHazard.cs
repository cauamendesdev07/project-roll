using UnityEngine;

// Lava que sobe com o tempo. Se a bola encostar, volta ao checkpoint e a lava recomeça de baixo.
public class RisingHazard : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float riseSpeed = 0.3f;
    [SerializeField] private float maxHeight = 40f;
    [Tooltip("Segundos parada no começo e depois de cada respawn.")]
    [SerializeField] private float startDelay = 3f;

    private CheckpointSystem checkpoint;
    private float startHeight;
    private float elapsed;

    private void Start()
    {
        startHeight = transform.position.y;
        checkpoint = player.GetComponent<CheckpointSystem>();
        checkpoint.Respawned += ResetHeight;
    }

    private void OnDestroy()
    {
        if (checkpoint != null)
            checkpoint.Respawned -= ResetHeight;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float height = Mathf.Min(maxHeight, startHeight + Mathf.Max(0f, elapsed - startDelay) * riseSpeed);
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, height, p.z);

        if (player.position.y < height + 0.3f)
            checkpoint.Respawn();
    }

    private void ResetHeight()
    {
        elapsed = 0f;
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, startHeight, p.z);
    }
}
