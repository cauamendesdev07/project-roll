using UnityEngine;

// Gelo: a bola demora muito mais para acelerar e para frear.
public class IceSurface : MonoBehaviour, IPlayerSurface
{
    [Range(0.02f, 1f)] [SerializeField] private float accelerationMultiplier = 0.12f;

    public float AccelerationMultiplier => accelerationMultiplier;

    public Vector3 GetSurfaceVelocity(Vector3 worldPoint) => Vector3.zero;
}
