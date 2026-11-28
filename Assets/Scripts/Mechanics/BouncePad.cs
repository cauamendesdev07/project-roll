using UnityEngine;

// Trampolim: lança a bola para cima ao ser tocado por cima.
public class BouncePad : MonoBehaviour
{
    [SerializeField] private float launchSpeed = 16f;

    private float lastBounceTime = -1f;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Player") || Time.time - lastBounceTime < 0.15f)
            return;

        if (collision.GetContact(0).normal.y > -0.5f)
            return; // só quando a bola chega por cima

        Rigidbody rb = collision.rigidbody;
        if (rb == null)
            return;

        lastBounceTime = Time.time;
        AudioManager.Play("bounce", 0.8f, 1f, 0.06f);
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, launchSpeed, rb.linearVelocity.z);
    }
}
