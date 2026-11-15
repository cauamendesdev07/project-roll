
using UnityEngine;

public class FallDetector : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float fallLimit = -7f;

    private CheckpointSystem checkpointSystem;

    private void Start()
    {
        checkpointSystem = player.GetComponent<CheckpointSystem>();
    }

    private void Update()
    {
        if (player.position.y < fallLimit)
        {
            checkpointSystem.Respawn();
        }
    }
}
