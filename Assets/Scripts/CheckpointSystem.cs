
using UnityEngine;

public class CheckpointSystem : MonoBehaviour
{
    private Vector3 currentCheckpoint;

    private void Start()
    {
        currentCheckpoint = transform.position;
    }

    public void SetCheckpoint(Vector3 newPosition)
    {
        currentCheckpoint = newPosition;
        Debug.Log("Checkpoint atualizado!");
    }

    public void Respawn()
    {
        transform.position = currentCheckpoint;

        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
