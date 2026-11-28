using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Peças de interface montadas por código, todas no mesmo estilo.
public static class UiKit
{
    public static readonly Color Panel = new Color(0.07f, 0.09f, 0.11f, 0.82f);
    public static readonly Color PanelSoft = new Color(0.07f, 0.09f, 0.11f, 0.55f);
    public static readonly Color Text = new Color(0.96f, 0.93f, 0.86f);
    public static readonly Color TextDim = new Color(0.78f, 0.75f, 0.68f);
    public static readonly Color Accent = new Color(0.95f, 0.69f, 0.2f);
    public static readonly Color ButtonColor = new Color(0.16f, 0.2f, 0.24f, 0.95f);
    public static readonly Color ButtonHover = new Color(0.95f, 0.69f, 0.2f, 1f);

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static RectTransform Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static UnityEngine.UI.Image Img(string name, Transform parent, Color color)
    {
        var rt = Rect(name, parent);
        var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
        img.color = color;
        return img;
    }

    public static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
    {
        var rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.characterSpacing = 3f;
        t.outlineWidth = 0.18f;
        t.outlineColor = new Color32(10, 14, 18, 200);
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    public static UnityEngine.UI.Button Btn(string name, Transform parent, string text, UnityAction onClick, float width = 360f, float height = 64f)
    {
        var img = Img(name, parent, ButtonColor);
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(width, height);
        var layout = rt.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width; layout.preferredHeight = height;
        var btn = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.1f, 0.8f, 1f);
        colors.selectedColor = new Color(1.15f, 1.05f, 0.85f, 1f);
        colors.pressedColor = new Color(0.8f, 0.75f, 0.6f, 1f);
        colors.colorMultiplier = 1.4f;
        btn.colors = colors;
        btn.onClick.AddListener(() => AudioManager.Play("click", 0.5f));
        btn.onClick.AddListener(onClick);
        var label = Label("Label", rt, text, 28f, Text, TextAlignmentOptions.Center, true);
        Stretch(label.rectTransform);
        return btn;
    }

    public static VerticalLayoutGroup Column(RectTransform rt, float spacing)
    {
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.childAlignment = TextAnchor.MiddleCenter;
        v.childControlWidth = false; v.childControlHeight = false;
        v.childForceExpandWidth = false; v.childForceExpandHeight = false;
        return v;
    }

    // Barra deslizante no estilo da interface.
    public static Slider Bar(string name, Transform parent, float min, float max, float value, UnityAction<float> onChange, float width = 380f)
    {
        var root = Rect(name, parent);
        root.sizeDelta = new Vector2(width, 30f);
        var bg = Img("Background", root, ButtonColor);
        Anchor(bg.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 12));

        var fillArea = Rect("Fill Area", root);
        Anchor(fillArea, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 12));
        var fill = Img("Fill", fillArea, Accent);
        fill.rectTransform.anchorMin = new Vector2(0, 0);
        fill.rectTransform.anchorMax = new Vector2(0, 1);
        fill.rectTransform.sizeDelta = Vector2.zero;

        var handleArea = Rect("Handle Slide Area", root);
        Stretch(handleArea);
        handleArea.sizeDelta = new Vector2(-24, 0);
        var handle = Img("Handle", handleArea, Text);
        handle.rectTransform.anchorMin = new Vector2(0, 0);
        handle.rectTransform.anchorMax = new Vector2(0, 1);
        handle.rectTransform.sizeDelta = new Vector2(24, 0);

        var s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        var colors = s.colors;
        colors.highlightedColor = new Color(1f, 0.85f, 0.55f);
        colors.pressedColor = Accent;
        s.colors = colors;
        s.minValue = min;
        s.maxValue = max;
        s.SetValueWithoutNotify(value);
        s.onValueChanged.AddListener(onChange);
        return s;
    }
}
