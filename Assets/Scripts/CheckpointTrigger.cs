
using UnityEngine;
using UnityEngine.SceneManagement;

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

        // Identifica a fase atual.
        string currentScene = SceneManager.GetActiveScene().name;

        // Extrai o número da fase.
        string levelNumberText = currentScene.Replace("Level_", "");

        if (!int.TryParse(levelNumberText, out int currentLevel))
        {
            Debug.LogError("Não foi possível identificar a fase atual: " + currentScene);
            return;
        }

        // Calcula a próxima fase.
        int nextLevel = currentLevel + 1;

        // Monta o nome da próxima cena.
        string nextScene = "Level_" + nextLevel.ToString("00");

        // Verifica se a próxima fase existe no Build Profiles.
        if (Application.CanStreamedLevelBeLoaded(nextScene))
        {
            Debug.Log("Fase " + currentLevel + " concluída! Carregando " + nextScene + "...");
            SceneManager.LoadScene(nextScene);
        }
        else
        {
            Debug.Log("Parabéns! Você concluiu todas as fases!");
        }
    }
}
