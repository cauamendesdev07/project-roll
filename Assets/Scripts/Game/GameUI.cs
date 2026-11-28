using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Interface do jogo (HUD, pausa, fase concluída e tela final), montada por código.
public class GameUI : MonoBehaviour
{
    private Canvas canvas;
    private EventSystem eventSystem;
    private GameObject hud, pause, levelComplete, gameComplete;
    private TextMeshProUGUI hudLevel, hudName, hudDeaths, hudTime;
    private TextMeshProUGUI completeTitle, completeStats, endStats;
    private CanvasGroup completeGroup;
    private Image respawnFlash;
    private float respawnFlashAt = -10f;
    private float completeShownAt;

    public Transform Root => canvas.transform;
    public MenuUI Menu { get; private set; }
    public OptionsUI Options { get; private set; }

    private void Awake()
    {
        BuildCanvas();
        BuildRespawnFlash();
        BuildHud();
        BuildPause();
        BuildLevelComplete();
        BuildGameComplete();
        Menu = gameObject.AddComponent<MenuUI>();
        Menu.Build(canvas.transform);
        Options = gameObject.AddComponent<OptionsUI>();
        Options.Build(canvas.transform);
        Menu.OptionsRequested += () => { Menu.Hide(); Options.Open(Menu.ShowMenu); };
    }

    private void BuildCanvas()
    {
        var go = new GameObject("GameUI", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        var es = new GameObject("EventSystem");
        es.transform.SetParent(transform, false);
        eventSystem = es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    // Escurecida rápida no respawn (abaixo da HUD; não bloqueia cliques).
    private void BuildRespawnFlash()
    {
        respawnFlash = UiKit.Img("RespawnFlash", canvas.transform, new Color(0.03f, 0.04f, 0.05f, 0f));
        UiKit.Stretch(respawnFlash.rectTransform);
        respawnFlash.raycastTarget = false;
        respawnFlash.gameObject.SetActive(false);
    }

    public void FlashRespawn()
    {
        respawnFlashAt = Time.unscaledTime;
        respawnFlash.gameObject.SetActive(true);
    }

    private void BuildHud()
    {
        hud = UiKit.Rect("HUD", canvas.transform).gameObject;
        UiKit.Stretch((RectTransform)hud.transform);

        var left = UiKit.Img("LevelPanel", hud.transform, UiKit.PanelSoft).rectTransform;
        UiKit.Anchor(left, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -24), new Vector2(460, 92));
        hudLevel = UiKit.Label("Level", left, "FASE 1", 22f, UiKit.Accent, TextAlignmentOptions.Left, true);
        UiKit.Anchor(hudLevel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(22, -10), new Vector2(-44, 30));
        hudName = UiKit.Label("Name", left, "Amazônia", 34f, UiKit.Text, TextAlignmentOptions.Left, true);
        hudName.enableAutoSizing = true; hudName.fontSizeMin = 20f; hudName.fontSizeMax = 34f;
        UiKit.Anchor(hudName.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(22, 10), new Vector2(-44, 44));

        var right = UiKit.Img("StatsPanel", hud.transform, UiKit.PanelSoft).rectTransform;
        UiKit.Anchor(right, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -24), new Vector2(300, 92));
        hudTime = UiKit.Label("Time", right, "00:00.0", 34f, UiKit.Text, TextAlignmentOptions.Right, true);
        UiKit.Anchor(hudTime.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -8), new Vector2(-44, 44));
        hudDeaths = UiKit.Label("Deaths", right, "Quedas: 0", 22f, UiKit.TextDim, TextAlignmentOptions.Right, false);
        UiKit.Anchor(hudDeaths.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-22, 10), new Vector2(-44, 30));
    }

    private void BuildPause()
    {
        pause = UiKit.Img("Pause", canvas.transform, new Color(0.03f, 0.04f, 0.05f, 0.62f)).gameObject;
        UiKit.Stretch((RectTransform)pause.transform);
        var box = UiKit.Img("Box", pause.transform, UiKit.Panel).rectTransform;
        UiKit.Anchor(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 560));
        var title = UiKit.Label("Title", box, "PAUSADO", 52f, UiKit.Accent, TextAlignmentOptions.Center, true);
        UiKit.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(0, 70));
        var col = UiKit.Rect("Buttons", box);
        UiKit.Anchor(col, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(400, 380));
        UiKit.Column(col, 18f);
        UiKit.Btn("Resume", col, "Continuar", () => GameManager.Instance.Resume());
        UiKit.Btn("Restart", col, "Reiniciar fase", () => GameManager.Instance.RestartLevel());
        UiKit.Btn("Options", col, "Opções", OpenOptionsFromPause);
        UiKit.Btn("Menu", col, "Menu principal", () => GameManager.Instance.GoToMainMenu());
        pause.SetActive(false);
    }

    private void OpenOptionsFromPause()
    {
        pause.SetActive(false);
        Options.Open(() => pause.SetActive(GameManager.Instance != null && GameManager.Instance.IsPaused));
    }

    private void BuildLevelComplete()
    {
        levelComplete = UiKit.Rect("LevelComplete", canvas.transform).gameObject;
        UiKit.Stretch((RectTransform)levelComplete.transform);
        completeGroup = levelComplete.AddComponent<CanvasGroup>();
        var band = UiKit.Img("Band", levelComplete.transform, UiKit.Panel).rectTransform;
        UiKit.Anchor(band, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 220));
        completeTitle = UiKit.Label("Title", band, "FASE CONCLUÍDA!", 64f, UiKit.Accent, TextAlignmentOptions.Center, true);
        UiKit.Anchor(completeTitle.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(0, 80));
        completeStats = UiKit.Label("Stats", band, "", 30f, UiKit.Text, TextAlignmentOptions.Center);
        UiKit.Anchor(completeStats.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -45), new Vector2(0, 44));
        levelComplete.SetActive(false);
    }

    private void BuildGameComplete()
    {
        gameComplete = UiKit.Img("GameComplete", canvas.transform, new Color(0.03f, 0.04f, 0.05f, 0.78f)).gameObject;
        UiKit.Stretch((RectTransform)gameComplete.transform);
        var box = UiKit.Img("Box", gameComplete.transform, UiKit.Panel).rectTransform;
        UiKit.Anchor(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 520));
        var title = UiKit.Label("Title", box, "PARABÉNS!", 76f, UiKit.Accent, TextAlignmentOptions.Center, true);
        UiKit.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(0, 90));
        var sub = UiKit.Label("Subtitle", box, "Você chegou ao fim de Project Roll.", 32f, UiKit.Text, TextAlignmentOptions.Center);
        UiKit.Anchor(sub.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(0, 44));
        endStats = UiKit.Label("Stats", box, "", 30f, UiKit.TextDim, TextAlignmentOptions.Center);
        UiKit.Anchor(endStats.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(0, 90));
        endStats.textWrappingMode = TextWrappingModes.Normal;
        var btn = UiKit.Btn("Menu", box, "Voltar ao menu", () => GameManager.Instance.GoToMainMenu(), 380f, 70f);
        UiKit.Anchor((RectTransform)btn.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(380, 70));
        gameComplete.SetActive(false);
    }

    public void OnSceneChanged(int level)
    {
        Options.Close();
        // usa o EventSystem da cena, se ela tiver um (ex.: menu principal)
        var others = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude);
        bool sceneHasOwn = false;
        foreach (var e in others) if (e != eventSystem) sceneHasOwn = true;
        eventSystem.gameObject.SetActive(!sceneHasOwn);

        hud.SetActive(level > 0);
        pause.SetActive(false);
        levelComplete.SetActive(false);
        gameComplete.SetActive(false);
        respawnFlash.gameObject.SetActive(false);

        // Menu principal: o menu montado por código substitui o Canvas antigo da cena.
        if (level == 0 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameManager.MainMenuScene)
        {
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                if (c != canvas && c.isRootCanvas) c.gameObject.SetActive(false);
            Menu.ShowMenu();
        }
        else
        {
            Menu.Hide();
        }

        if (level > 0)
        {
            hudLevel.text = $"FASE {level}";
            hudName.text = GameManager.LevelName(level);
        }
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;

        if (hud.activeSelf)
        {
            hudTime.text = GameManager.FormatTime(gm.LevelTime);
            hudDeaths.text = gm.LevelDeaths == 1 ? "1 queda" : $"{gm.LevelDeaths} quedas";
        }

        if (respawnFlash.gameObject.activeSelf)
        {
            // começa forte e some em ~0,3 s; o jogo segue normalmente por baixo
            float t = (Time.unscaledTime - respawnFlashAt) / 0.3f;
            var c = respawnFlash.color;
            c.a = 0.6f * (1f - t) * (1f - t);
            respawnFlash.color = c;
            if (t >= 1f) respawnFlash.gameObject.SetActive(false);
        }

        if (levelComplete.activeSelf)
        {
            float t = Time.unscaledTime - completeShownAt;
            completeGroup.alpha = Mathf.Clamp01(t / 0.25f);
            completeTitle.transform.localScale = Vector3.one * (1f + 0.15f * Mathf.Exp(-t * 8f));
        }
    }

    public void ShowPause(bool show)
    {
        pause.SetActive(show);
        if (show) eventSystem.SetSelectedGameObject(null);
    }

    public void ShowLevelComplete(int level, float time, int deaths)
    {
        completeTitle.text = "FASE CONCLUÍDA!";
        completeStats.text = $"Tempo {GameManager.FormatTime(time)}   ·   {(deaths == 1 ? "1 queda" : $"{deaths} quedas")}";
        completeShownAt = Time.unscaledTime;
        levelComplete.SetActive(true);
    }

    public void ShowGameComplete(float totalTime, int totalDeaths)
    {
        hud.SetActive(false);
        endStats.text = $"Tempo total {GameManager.FormatTime(totalTime)}\n{(totalDeaths == 1 ? "1 queda" : $"{totalDeaths} quedas")}";
        gameComplete.SetActive(true);
    }
}
