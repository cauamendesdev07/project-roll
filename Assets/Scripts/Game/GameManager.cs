using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Gerenciador do jogo: criado automaticamente, persiste entre as cenas.
// Conta tempo e quedas, controla pausa, a transição entre fases e a tela final.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static readonly string[] LevelNames =
    {
        "Amazônia", "Caatinga", "Cerrado", "Pantanal", "Fernando de Noronha",
        "Lençóis Maranhenses", "Gruta do Lago Azul", "Capital Nacional do Frio", "Terra da Garoa", "Cidade Maravilhosa",
    };

    public const string MainMenuScene = "MainMenu";

    // Fase atual (1 a 10); 0 fora das fases (menu).
    public int CurrentLevel { get; private set; }
    public float LevelTime { get; private set; }
    public int LevelDeaths { get; private set; }
    public float RunTime { get; private set; }
    public int RunDeaths { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsTransitioning { get; private set; }
    public bool IsGameOverScreen { get; private set; }

    // Menu com o jogo rodando ao fundo: o robozinho joga as fases em sequência.
    public bool IsDemo { get; private set; }
    public int DemoLevel { get; private set; }
    // Fase cujo visual/áudio está na tela (a jogada ou a da demonstração).
    public int VisualLevel => CurrentLevel > 0 ? CurrentLevel : DemoLevel;
    private const float DemoDuration = 14f;
    private bool demoWanted;
    private int demoNext;
    private Coroutine demoRoutine;
    private AsyncOperation demoLoad;

    // Movimento e câmera ficam travados durante a pausa e a tela final.
    public static bool InputBlocked => Instance != null && (Instance.IsPaused || Instance.IsGameOverScreen);

    // Avisos para outros sistemas (progresso salvo, áudio, efeitos).
    public event Action<int, float, int> LevelCompleted;   // fase, tempo, quedas
    public event Action Respawned;
    public event Action<int> LevelStarted;                 // fase (0 = menu)

    private GameUI ui;
    private AudioManager audioManager;
    private CheckpointSystem player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        new GameObject("GameManager").AddComponent<GameManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        GameSettings.Load();
        ui = gameObject.AddComponent<GameUI>();
        audioManager = gameObject.AddComponent<AudioManager>();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    public static int LevelFromScene(string sceneName)
    {
        if (sceneName != null && sceneName.StartsWith("Level_") && int.TryParse(sceneName.Substring(6), out int n))
            return n;
        return 0;
    }

    public static string LevelName(int level) =>
        level >= 1 && level <= LevelNames.Length ? LevelNames[level - 1] : "";

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        int previous = CurrentLevel;
        Time.timeScale = 1f;
        IsPaused = false;
        IsTransitioning = false;
        IsGameOverScreen = false;
        int sceneLevel = LevelFromScene(scene.name);
        IsDemo = demoWanted && sceneLevel > 0;
        DemoLevel = IsDemo ? sceneLevel : 0;
        CurrentLevel = IsDemo ? 0 : sceneLevel;
        LevelTime = 0f;
        LevelDeaths = 0;

        // Saiu do menu para uma fase: começa uma nova sessão de jogo.
        if (CurrentLevel > 0 && previous == 0)
        {
            RunTime = 0f;
            RunDeaths = 0;
        }

        if (player != null)
            player.Respawned -= OnPlayerRespawned;

        player = sceneLevel > 0 ? FindAnyObjectByType<CheckpointSystem>() : null;

        if (player != null)
        {
            player.Respawned += OnPlayerRespawned;
            if (player.GetComponent<BallVisuals>() == null)
                player.gameObject.AddComponent<BallVisuals>();
            if (IsDemo) SetupDemo(player);
        }

        if (CurrentLevel == 0)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        GameSettings.ApplyCameras();
        audioManager.SetLevel(VisualLevel);
        ui.OnSceneChanged(CurrentLevel, IsDemo);
        LevelStarted?.Invoke(CurrentLevel);

        // menu principal: começa a demonstração ao fundo
        if (scene.name == MainMenuScene && demoRoutine == null)
            demoRoutine = StartCoroutine(DemoLoop());
    }

    // ---------- Demonstração no menu ----------

    private void SetupDemo(CheckpointSystem ball)
    {
        var cam = Camera.main;
        if (cam != null)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow != null) follow.enabled = false;   // antes do Start: não trava o cursor
            var demoCam = cam.GetComponent<DemoCamera>();
            if (demoCam == null) demoCam = cam.gameObject.AddComponent<DemoCamera>();
            var fwd = cam.transform.forward; fwd.y = 0f;
            demoCam.Init(ball.transform, fwd.sqrMagnitude > 0.01f ? Quaternion.LookRotation(fwd).eulerAngles.y : 0f);
        }
        if (ball.GetComponent<DemoPilot>() == null)
            ball.gameObject.AddComponent<DemoPilot>();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private IEnumerator DemoLoop()
    {
        demoWanted = true;
        demoNext = UnityEngine.Random.Range(1, LevelNames.Length + 1);
        while (demoWanted)
        {
            yield return ui.FadeDemo(1f, 0.45f);
            if (!demoWanted) break;
            demoLoad = SceneManager.LoadSceneAsync($"Level_{demoNext:00}");
            while (demoLoad != null && !demoLoad.isDone) yield return null;
            demoLoad = null;
            demoNext = demoNext % LevelNames.Length + 1;
            if (!demoWanted) break;
            yield return ui.FadeDemo(0f, 0.6f);
            float until = Time.realtimeSinceStartup + DemoDuration;
            while (demoWanted && Time.realtimeSinceStartup < until) yield return null;
        }
        demoRoutine = null;
    }

    private void StopDemo()
    {
        demoWanted = false;
        if (demoRoutine != null) { StopCoroutine(demoRoutine); demoRoutine = null; }
        ui.FadeDemoInstant(0f);
    }

    // Se uma fase da demonstração ainda está carregando, espera terminar antes de trocar de cena.
    private IEnumerator LoadWhenFree(string sceneName)
    {
        while (demoLoad != null && !demoLoad.isDone) yield return null;
        demoLoad = null;
        SceneManager.LoadScene(sceneName);
    }

    private void OnPlayerRespawned()
    {
        if (IsDemo) return;   // o robozinho cai à vontade: sem contar, sem som, sem flash
        LevelDeaths++;
        AudioManager.Play("respawn", 0.7f);
        ui.FlashRespawn();
        Respawned?.Invoke();
    }

    private void Update()
    {
        // Esc com as opções abertas só fecha as opções (volta à pausa ou ao menu).
        if (ui.Options.IsOpen)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ui.Options.Close();
            return;
        }

        if (CurrentLevel <= 0)
            return;

        if (!IsPaused && !IsTransitioning)
            LevelTime += Time.deltaTime;

        if (!IsTransitioning && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    public void Pause()
    {
        if (CurrentLevel <= 0 || IsTransitioning)
            return;

        IsPaused = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ui.ShowPause(true);
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        ui.ShowPause(false);
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadLevel(int level)
    {
        Time.timeScale = 1f;
        bool loading = demoLoad != null && !demoLoad.isDone;
        StopDemo();
        if (loading) StartCoroutine(LoadWhenFree($"Level_{level:00}"));
        else SceneManager.LoadScene($"Level_{level:00}");
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(MainMenuScene);
    }

    // Chamado pela chegada. nextScene vazio = era a última fase.
    public void CompleteLevel(string nextScene)
    {
        if (IsTransitioning || IsDemo)
            return;

        StartCoroutine(CompleteRoutine(nextScene));
    }

    private IEnumerator CompleteRoutine(string nextScene)
    {
        IsTransitioning = true;
        RunTime += LevelTime;
        RunDeaths += LevelDeaths;
        SaveData.RecordCompletion(CurrentLevel, LevelTime, LevelDeaths);
        LevelCompleted?.Invoke(CurrentLevel, LevelTime, LevelDeaths);

        AudioManager.Play(string.IsNullOrEmpty(nextScene) ? "victory" : "finish", 0.85f);

        if (string.IsNullOrEmpty(nextScene))
        {
            IsGameOverScreen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ui.ShowGameComplete(RunTime, RunDeaths);
            yield break;
        }

        ui.ShowLevelComplete(CurrentLevel, LevelTime, LevelDeaths);
        yield return new WaitForSecondsRealtime(1.8f);
        SceneManager.LoadScene(nextScene);
    }

    public static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        float s = seconds - m * 60f;
        return $"{m:00}:{s:00.0}";
    }
}
