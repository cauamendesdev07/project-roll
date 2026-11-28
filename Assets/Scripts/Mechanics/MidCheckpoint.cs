using UnityEngine;

// Checkpoint no meio da fase: ao passar, a bola passa a renascer aqui.
// Fica num trigger filho da plataforma, como o FinishTrigger.
public class MidCheckpoint : MonoBehaviour
{
    [SerializeField] private Material activeMaterial;

    private bool activated;

    private void OnTriggerEnter(Collider other)
    {
        if (activated || !other.CompareTag("Player"))
            return;

        CheckpointSystem checkpoint = other.GetComponent<CheckpointSystem>();
        Transform platform = transform.parent;

        if (checkpoint == null || platform == null)
            return;

        Bounds bounds = platform.GetComponent<Collider>().bounds;
        checkpoint.SetCheckpoint(new Vector3(bounds.center.x, bounds.max.y + 0.6f, bounds.center.z));

        Renderer platformRenderer = platform.GetComponent<Renderer>();
        if (platformRenderer != null && activeMaterial != null)
            platformRenderer.sharedMaterial = activeMaterial;

        activated = true;
        AudioManager.Play("checkpoint", 0.8f);
    }
}
