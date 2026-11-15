
using UnityEngine;

public class CheckpointTrigger : MonoBehaviour
{
    private bool activated;

    private void OnTriggerEnter(Collider other)
    {
        if (activated || !other.CompareTag("Player"))
            return;

        CheckpointSystem checkpoint = other.GetComponent<CheckpointSystem>();

        if (checkpoint == null)
            return;

        Transform platform = transform.parent;

        if (platform == null)
            return;

        // Define o ponto de respawn acima da plataforma.
        Vector3 respawnPosition = platform.position + Vector3.up * 1.1f;

        checkpoint.SetCheckpoint(respawnPosition);

        // Muda a cor da plataforma para verde.
        Renderer platformRenderer = platform.GetComponent<Renderer>();

        if (platformRenderer != null)
            platformRenderer.material.color = Color.green;

        activated = true;

        Debug.Log("Checkpoint final ativado!");
    }
}
