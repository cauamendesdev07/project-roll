using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tela de opções (aberta pelo menu principal e pela pausa).
public class OptionsUI : MonoBehaviour
{
    private const float RowWidth = 900f;
    private const float RowHeight = 48f;

    private GameObject panel;
    private Action onClose;
    private readonly System.Collections.Generic.List<Action> refreshers = new();

    public bool IsOpen => panel != null && panel.activeSelf;

    public void Build(Transform root)
    {
        panel = UiKit.Img("Options", root, new Color(0.03f, 0.04f, 0.05f, 0.78f)).gameObject;
        UiKit.Stretch((RectTransform)panel.transform);
        var box = UiKit.Img("Box", panel.transform, UiKit.Panel).rectTransform;
        UiKit.Anchor(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 940));
        var title = UiKit.Label("Title", box, "OPÇÕES", 56f, UiKit.Accent, TextAlignmentOptions.Center, true);
        UiKit.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(0, 70));

        var col = UiKit.Rect("Rows", box);
        UiKit.Anchor(col, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(RowWidth, 700));
        var layout = UiKit.Column(col, 6f);
        layout.childAlignment = TextAnchor.UpperCenter;

        Header(col, "CONTROLES");
        SliderRow(col, "Sensibilidade do mouse", GameSettings.MinSensitivity, GameSettings.MaxSensitivity,
            () => GameSettings.MouseSensitivity, GameSettings.SetMouseSensitivity,
            v => $"{v / GameSettings.DefaultSensitivity:0.0}x");
        ToggleRow(col, "Inverter eixo vertical", () => GameSettings.InvertY, GameSettings.SetInvertY);

        Header(col, "ÁUDIO");
        SliderRow(col, "Volume geral", 0f, 1f, () => GameSettings.MasterVolume, GameSettings.SetMasterVolume, Percent);
        SliderRow(col, "Música", 0f, 1f, () => GameSettings.MusicVolume, GameSettings.SetMusicVolume, Percent);
        SliderRow(col, "Efeitos", 0f, 1f, () => GameSettings.SfxVolume, GameSettings.SetSfxVolume, Percent);

        Header(col, "GRÁFICOS");
        ToggleRow(col, "Tela cheia", () => GameSettings.Fullscreen, GameSettings.SetFullscreen);
        ResolutionRow(col);
        ToggleRow(col, "VSync", () => GameSettings.VSync, GameSettings.SetVSync);
        ToggleRow(col, "Sombras", () => GameSettings.Shadows, GameSettings.SetShadows);
        ToggleRow(col, "Pós-processamento", () => GameSettings.PostProcessing, GameSettings.SetPostProcessing);

        var back = UiKit.Btn("Back", box, "Voltar", Close, 320f, 66f);
        UiKit.Anchor((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(320, 66));

        panel.SetActive(false);
    }

    private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";

    // A caixa tem 940 de altura: encolhe em janelas baixas.
    private void Update()
    {
        if (!IsOpen) return;
        var root = (RectTransform)panel.transform;
        var box = root.GetChild(0);
        box.localScale = Vector3.one * Mathf.Min(1f, root.rect.height * 0.96f / 940f);
    }

    public void Open(Action closed)
    {
        onClose = closed;
        foreach (var r in refreshers) r();
        panel.SetActive(true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        panel.SetActive(false);
        var cb = onClose;
        onClose = null;
        cb?.Invoke();
    }

    // ---------- Linhas ----------

    private static RectTransform Row(Transform parent, string label)
    {
        var row = UiKit.Rect(label, parent);
        row.sizeDelta = new Vector2(RowWidth, RowHeight);
        var text = UiKit.Label("Label", row, label, 28f, UiKit.Text, TextAlignmentOptions.Left);
        UiKit.Anchor(text.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(400, 0));
        return row;
    }

    private static void Header(Transform parent, string text)
    {
        var row = UiKit.Rect(text, parent);
        row.sizeDelta = new Vector2(RowWidth, 46f);
        var label = UiKit.Label("Header", row, text, 22f, UiKit.Accent, TextAlignmentOptions.BottomLeft, true);
        UiKit.Stretch(label.rectTransform);
        label.margin = new Vector4(30, 0, 0, 4);
    }

    // Controle à direita da linha (área de 440 de largura).
    private static void PlaceRight(RectTransform control, RectTransform row, float width, float height, float x = -30f)
    {
        control.SetParent(row, false);
        UiKit.Anchor(control, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(x, 0), new Vector2(width, height));
    }

    private void SliderRow(Transform parent, string label, float min, float max, Func<float> get, Action<float> set, Func<float, string> format)
    {
        var row = Row(parent, label);
        var value = UiKit.Label("Value", row, "", 26f, UiKit.TextDim, TextAlignmentOptions.Right);
        PlaceRight(value.rectTransform, row, 90f, RowHeight);
        Slider slider = null;
        slider = UiKit.Bar("Slider", row, min, max, get(), v => { set(v); value.text = format(v); }, 330f);
        PlaceRight((RectTransform)slider.transform, row, 330f, 30f, -130f);
        refreshers.Add(() => { slider.SetValueWithoutNotify(get()); value.text = format(get()); });
    }

    private void ToggleRow(Transform parent, string label, Func<bool> get, Action<bool> set)
    {
        var row = Row(parent, label);
        TextMeshProUGUI text = null;
        var btn = UiKit.Btn("Toggle", row, "", () => { set(!get()); text.text = get() ? "Ligado" : "Desligado"; }, 220f, 42f);
        text = btn.GetComponentInChildren<TextMeshProUGUI>();
        text.fontSize = 24f;
        PlaceRight((RectTransform)btn.transform, row, 220f, 42f);
        refreshers.Add(() => text.text = get() ? "Ligado" : "Desligado");
    }

    private void ResolutionRow(Transform parent)
    {
        var row = Row(parent, "Resolução");
        var value = UiKit.Label("Value", row, "", 26f, UiKit.Text, TextAlignmentOptions.Center, true);
        PlaceRight(value.rectTransform, row, 230f, RowHeight, -90f);

        void Show() { var r = GameSettings.CurrentResolution; value.text = $"{r.x} × {r.y}"; }
        void Step(int dir)
        {
            var list = GameSettings.Resolutions();
            var cur = GameSettings.CurrentResolution;
            int i = list.IndexOf(cur);
            if (i < 0)
            {
                // resolução atual fora da lista: começa da mais próxima
                i = 0;
                for (int k = 0; k < list.Count; k++) if (list[k].x * list[k].y <= cur.x * cur.y) i = k;
            }
            else
            {
                i = Mathf.Clamp(i + dir, 0, list.Count - 1);
            }
            GameSettings.SetResolution(list[i]);
            Show();
        }

        var next = UiKit.Btn("Next", row, ">", () => Step(1), 52f, 42f);
        PlaceRight((RectTransform)next.transform, row, 52f, 42f);
        var prev = UiKit.Btn("Prev", row, "<", () => Step(-1), 52f, 42f);
        PlaceRight((RectTransform)prev.transform, row, 52f, 42f, -328f);
        refreshers.Add(Show);
    }
}
