using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Menu principal montado por código: Continuar, Novo jogo, Fases, Opções e Sair,
// mais a tela de seleção de fases com recordes.
public class MenuUI : MonoBehaviour
{
    private GameObject menu, levels;
    private Button continueButton;
    private TextMeshProUGUI continueLabel;
    private readonly Button[] levelButtons = new Button[SaveData.LevelCount];
    private readonly TextMeshProUGUI[] levelInfo = new TextMeshProUGUI[SaveData.LevelCount];
    private readonly GameObject[] levelLocks = new GameObject[SaveData.LevelCount];
    private RectTransform root, grid;
    private TextMeshProUGUI resetLabel;
    private bool resetArmed;

    public event System.Action OptionsRequested;

    public void Build(Transform root)
    {
        this.root = (RectTransform)root;
        BuildMenu(root);
        BuildLevels(root);
        Hide();
    }

    private void BuildMenu(Transform root)
    {
        menu = UiKit.Img("MainMenu", root, new Color(0.03f, 0.04f, 0.05f, 0.35f)).gameObject;
        UiKit.Stretch((RectTransform)menu.transform);

        var title = UiKit.Label("Title", menu.transform, "PROJECT ROLL", 110f, UiKit.Accent, TextAlignmentOptions.Center, true);
        UiKit.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(0, 140));
        title.outlineWidth = 0.25f;
        var sub = UiKit.Label("Subtitle", menu.transform, "10 fases por paisagens e cidades do Brasil", 30f, UiKit.Text, TextAlignmentOptions.Center);
        UiKit.Anchor(sub.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -260), new Vector2(0, 44));

        var col = UiKit.Rect("Buttons", menu.transform);
        UiKit.Anchor(col, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(460, 460));
        UiKit.Column(col, 18f);
        continueButton = UiKit.Btn("Continue", col, "Continuar", () => GameManager.Instance.LoadLevel(SaveData.Unlocked), 420f, 70f);
        continueLabel = continueButton.GetComponentInChildren<TextMeshProUGUI>();
        UiKit.Btn("NewGame", col, "Novo jogo", () => GameManager.Instance.LoadLevel(1), 420f, 70f);
        UiKit.Btn("Levels", col, "Fases", ShowLevels, 420f, 70f);
        UiKit.Btn("Options", col, "Opções", () => OptionsRequested?.Invoke(), 420f, 70f);
        var quit = UiKit.Btn("Quit", col, "Sair", Application.Quit, 420f, 70f);
        quit.gameObject.SetActive(!Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer);
    }

    private void BuildLevels(Transform root)
    {
        levels = UiKit.Img("LevelSelect", root, new Color(0.03f, 0.04f, 0.05f, 0.7f)).gameObject;
        UiKit.Stretch((RectTransform)levels.transform);
        var title = UiKit.Label("Title", levels.transform, "FASES", 70f, UiKit.Accent, TextAlignmentOptions.Center, true);
        UiKit.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(0, 90));

        grid = UiKit.Rect("Grid", levels.transform);
        UiKit.Anchor(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1600, 560));
        var g = grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(300, 260);
        g.spacing = new Vector2(25, 30);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 5;
        g.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < SaveData.LevelCount; i++)
        {
            int level = i + 1;
            var tile = UiKit.Img($"Level{level}", grid, UiKit.ButtonColor);
            var btn = tile.gameObject.AddComponent<Button>();
            btn.targetGraphic = tile;
            var colors = btn.colors; colors.highlightedColor = new Color(1.25f, 1.1f, 0.8f); colors.colorMultiplier = 1.4f;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f); btn.colors = colors;
            btn.onClick.AddListener(() => AudioManager.Play("click", 0.5f));
            btn.onClick.AddListener(() => GameManager.Instance.LoadLevel(level));
            levelButtons[i] = btn;

            var num = UiKit.Label("Number", tile.transform, level.ToString(), 90f, UiKit.Accent, TextAlignmentOptions.Center, true);
            UiKit.Anchor(num.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(0, 110));
            var name = UiKit.Label("Name", tile.transform, GameManager.LevelName(level), 22f, UiKit.Text, TextAlignmentOptions.Center, true);
            UiKit.Anchor(name.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(-20, 40));
            name.characterSpacing = 1f;
            name.textWrappingMode = TextWrappingModes.Normal;
            name.enableAutoSizing = true; name.fontSizeMin = 14f; name.fontSizeMax = 22f;
            var info = UiKit.Label("Best", tile.transform, "", 21f, UiKit.TextDim, TextAlignmentOptions.Center);
            UiKit.Anchor(info.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(-20, 60));
            info.textWrappingMode = TextWrappingModes.Normal;
            levelInfo[i] = info;
            var lockIcon = UiKit.Label("Lock", tile.transform, "BLOQUEADA", 22f, new Color(0.9f, 0.45f, 0.35f), TextAlignmentOptions.Center, true);
            UiKit.Anchor(lockIcon.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(0, 30));
            levelLocks[i] = lockIcon.gameObject;
        }

        var row = UiKit.Rect("Bottom", levels.transform);
        UiKit.Anchor(row, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(900, 70));
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 30; h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = false; h.childControlHeight = false; h.childForceExpandWidth = false;
        UiKit.Btn("Back", row, "Voltar", ShowMenu, 300f, 66f);
        var reset = UiKit.Btn("Reset", row, "Apagar progresso", OnResetClicked, 360f, 66f);
        resetLabel = reset.GetComponentInChildren<TextMeshProUGUI>();
    }

    public void ShowMenu()
    {
        resetArmed = false;
        levels.SetActive(false);
        menu.SetActive(true);
        bool progress = SaveData.HasProgress;
        continueButton.gameObject.SetActive(progress);
        continueLabel.text = $"Continuar · Fase {SaveData.Unlocked}";
    }

    public void ShowLevels()
    {
        resetArmed = false;
        resetLabel.text = "Apagar progresso";
        menu.SetActive(false);
        levels.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        menu.SetActive(false);
        levels.SetActive(false);
    }

    // A grade tem 1600 de largura: encolhe em janelas estreitas para não cortar as fases das pontas.
    private void Update()
    {
        if (levels.activeSelf)
            grid.localScale = Vector3.one * Mathf.Min(1f, root.rect.width * 0.94f / 1600f);
    }

    private void Refresh()
    {
        for (int i = 0; i < SaveData.LevelCount; i++)
        {
            int level = i + 1;
            bool unlocked = SaveData.IsUnlocked(level);
            levelButtons[i].interactable = unlocked;
            levelLocks[i].SetActive(!unlocked);
            levelInfo[i].gameObject.SetActive(unlocked);
            levelInfo[i].text = SaveData.HasBest(level)
                ? $"Recorde {GameManager.FormatTime(SaveData.BestTime(level))}\n{(SaveData.BestDeaths(level) == 1 ? "1 queda" : $"{SaveData.BestDeaths(level)} quedas")}"
                : "Ainda não concluída";
        }
    }

    private void OnResetClicked()
    {
        if (!resetArmed)
        {
            resetArmed = true;
            resetLabel.text = "Clique de novo para apagar";
            return;
        }
        SaveData.ResetProgress();
        resetArmed = false;
        resetLabel.text = "Progresso apagado";
        Refresh();
    }
}
