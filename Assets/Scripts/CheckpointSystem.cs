
using System;
using UnityEngine;

public class CheckpointSystem : MonoBehaviour
{
    private Vector3 currentCheckpoint;

    // Avisado sempre que a bola volta para o checkpoint (ex.: a lava da fase 9 recomeça).
    public event Action Respawned;

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
        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Com interpolação ligada, mudar só o transform não move o corpo físico:
            // ele continuaria caindo e o FallDetector chamaria Respawn a cada quadro.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = currentCheckpoint;
        }

        transform.position = currentCheckpoint;
        Respawned?.Invoke();
    }
}
