using TMPro;
using UnityEngine;

/// <summary>Two-step first-session guide; completion is stored with the player save.</summary>
public sealed class FirstBaseGuide : MonoBehaviour
{
    public static FirstBaseGuide Instance { get; private set; }
    private TextMeshProUGUI hint;
    private RectTransform hintRect;
    private RectTransform pointerRect;
    private RectTransform safeArea;
    private int step;

    private void Awake() => Instance = this;

    private void Start()
    {
        if (GameManager.Instance?.PlayerData == null ||
            GameManager.Instance.PlayerData.firstBaseGuideDone) return;
        safeArea = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea") as RectTransform;
        if (safeArea == null) return;
        var go = new GameObject("FirstBaseGuideArrow", typeof(RectTransform),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(safeArea, false);
        hintRect = go.GetComponent<RectTransform>();
        hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0.5f);
        hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.sizeDelta = new Vector2(215f, 65f);
        hint = go.GetComponent<TextMeshProUGUI>();
        hint.fontSize = 21f;
        hint.fontStyle = FontStyles.Bold;
        hint.alignment = TextAlignmentOptions.Center;
        hint.color = new Color(1f, 0.83f, 0.35f);
        hint.raycastTarget = false;
        hint.text = "MADEN'E DOKUN";
        var pointer = new GameObject("GuidePointer", typeof(RectTransform),
            typeof(TextMeshProUGUI));
        pointer.transform.SetParent(go.transform, false);
        pointerRect = pointer.GetComponent<RectTransform>();
        pointerRect.anchorMin = pointerRect.anchorMax = new Vector2(0.5f, 0f);
        pointerRect.sizeDelta = new Vector2(34f, 32f);
        var pointerText = pointer.GetComponent<TextMeshProUGUI>();
        pointerText.text = "▼";
        pointerText.fontSize = 28f;
        pointerText.alignment = TextAlignmentOptions.Center;
        pointerText.color = hint.color;
        pointerText.raycastTarget = false;
        Update();
    }

    private void Update()
    {
        if (hintRect == null || safeArea == null || Camera.main == null) return;
        Vector3 screen;
        if (step == 0)
        {
            var mine = GameObject.Find("Building_GoldMine_mine_1");
            if (mine == null) return;
            screen = Camera.main.WorldToScreenPoint(mine.transform.position + Vector3.up * 2.3f);
        }
        else
        {
            var button = BaseWorldUI.Instance?.BattleButton;
            if (button == null) return;
            screen = button.transform.position + Vector3.up * 105f;
        }
        var canvas = safeArea.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea,
            screen, uiCamera, out var local))
        {
            float half = hintRect.sizeDelta.x * 0.5f;
            float x = Mathf.Clamp(local.x, safeArea.rect.xMin + half + 8f,
                safeArea.rect.xMax - half - 8f);
            hintRect.anchoredPosition = new Vector2(x, local.y);
            if (pointerRect != null)
                pointerRect.anchoredPosition = new Vector2(local.x - x, -5f);
        }
    }

    public void OnMineTapped()
    {
        if (step != 0 || hint == null) return;
        step = 1;
        hint.text = "SAVAŞA GİT'E BAS";
    }

    public void OnBattleTapped()
    {
        if (step != 1 || GameManager.Instance?.PlayerData == null) return;
        GameManager.Instance.PlayerData.firstBaseGuideDone = true;
        GameManager.Instance.SaveGame();
        if (hint != null) Destroy(hint.gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
