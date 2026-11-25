using UnityEngine;

// Superfícies que alteram o movimento da bola enquanto ela está em cima delas.
public interface IPlayerSurface
{
    // Velocidade da superfície no ponto de contato (plataforma móvel, giratória, esteira).
    Vector3 GetSurfaceVelocity(Vector3 worldPoint);

    // Multiplica a aceleração da bola (1 = normal; gelo usa valores baixos).
    float AccelerationMultiplier { get; }
}
