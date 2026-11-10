using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// İki adımlı ilk oturum rehberi; tamamlanma kayda yazılır.
/// Çerçeveli konuşma balonu + sallanan işaretçi; hedefte vurgu (madende dönen halka,
/// Savaşa Git düğmesinde nabız). Konum canvas birimleriyle hesaplanır, cihaz ölçeğinden bağımsızdır.
/// </summary>
public sealed class FirstBaseGuide : MonoBehaviour
{
    public static FirstBaseGuide Instance { get; private set; }

    private static string MineText => L.T("Madene dokun ve biriken <color=#F0C24A>altını topla</color>", "Tap the mine to <color=#F0C24A>collect the gold</color>");
    private static string BattleText => L.T("Hazırsın! <color=#F0C24A>Savaşa Git</color>'e bas", "Ready! Tap <color=#F0C24A>To Battle</color>");
    private const float BubbleWidth = 236f;
    private const float BubbleHeight = 58f;
    private const float PointerSize = 18f;
    private const float Gap = 6f;

    private TextMeshProUGUI hint;
    private RectTransform hintRect;
    private RectTransform pointerRect;
    private RectTransform safeArea;
    private GameObject mineRing;
    private Tween buttonPulse;
    private int step;

    private void Awake() => Instance = this;

    private void Start()
    {
        if (GameManager.Instance?.PlayerData == null ||
            GameManager.Instance.PlayerData.firstBaseGuideDone) return;
        safeArea = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea") as RectTransform;
        if (safeArea == null) return;

        // Balon (ad testler tarafından aranır).
        var bubble = new GameObject("FirstBaseGuideArrow", typeof(RectTransform), typeof(Image));
        bubble.transform.SetParent(safeArea, false);
        hintRect = bubble.GetComponent<RectTransform>();
        hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0.5f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(BubbleWidth, BubbleHeight);
        var background = bubble.GetComponent<Image>();
        background.sprite = ReferenceUiVisuals.ToastSprite();
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;
        var outline = bubble.AddComponent<Outline>();
        outline.effectColor = new Color(0.94f, 0.76f, 0.29f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        hint = ReferenceUiVisuals.Text(hintRect, "Text", MineText, 16, ReferenceUiVisuals.Light,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(12f, 4f),
            new Vector2(-12f, -4f), bodyBold: true);

        // Balonun altındaki üçgen işaretçi.
        var pointer = new GameObject("GuidePointer", typeof(RectTransform), typeof(Image));
        pointer.transform.SetParent(bubble.transform, false);
        pointerRect = pointer.GetComponent<RectTransform>();
        pointerRect.anchorMin = pointerRect.anchorMax = new Vector2(0.5f, 0f);
        pointerRect.pivot = new Vector2(0.5f, 1f);
        pointerRect.sizeDelta = new Vector2(PointerSize * 1.3f, PointerSize);
        var pointerImage = pointer.GetComponent<Image>();
        pointerImage.sprite = TriangleSprite();
        pointerImage.color = ReferenceUiVisuals.Gold;
        pointerImage.raycastTarget = false;

        // Balon hafifçe sallanır; dikkat çeker ama metni okunmaz hale getirmez.
        pointerRect.anchoredPosition = Vector2.zero;
        pointerRect.DOAnchorPosY(-5f, 0.55f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);

        bubble.transform.localScale = Vector3.one * 0.6f;
        bubble.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
        HighlightMine();
        Update();
    }

    private void HighlightMine()
    {
        var mine = GameObject.Find("Building_GoldMine_mine_1");
        if (mine == null) return;
        mineRing = BattleWorldMarkers.DashedCircle(mine.transform, 2.5f,
            new Color(0.94f, 0.76f, 0.29f, 0.95f), 26);
        mineRing.name = "GuideMineRing";
        foreach (var line in mineRing.GetComponentsInChildren<LineRenderer>())
        {
            line.startWidth = line.endWidth = 0.09f;
            line.sortingOrder = 400;
        }
        mineRing.transform.DOLocalRotate(new Vector3(0f, 0f, -360f), 9f, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear).SetLoops(-1);
    }

    private void Update()
    {
        if (hintRect == null || safeArea == null || Camera.main == null) return;
        var canvas = safeArea.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;

        Vector2 local;
        if (step == 0)
        {
            var mine = GameObject.Find("Building_GoldMine_mine_1");
            if (mine == null) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(mine.transform.position + Vector3.up * 2.2f);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea, screen, uiCamera, out local))
                return;
        }
        else
        {
            var button = BaseWorldUI.Instance?.BattleButton;
            if (button == null) return;
            var corners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners);
            Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, topCenter);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea, screen, uiCamera, out local))
                return;
        }

        // Balonun tabanı hedefin üstünde, işaretçi hedefi gösterecek şekilde.
        float half = BubbleWidth * 0.5f;
        float x = Mathf.Clamp(local.x, safeArea.rect.xMin + half + 8f, safeArea.rect.xMax - half - 8f);
        hintRect.anchoredPosition = new Vector2(x, local.y + PointerSize + Gap);
        if (pointerRect != null)
            pointerRect.anchoredPosition = new Vector2(
                Mathf.Clamp(local.x - x, -half + 20f, half - 20f), pointerRect.anchoredPosition.y);
    }

    public void OnMineTapped()
    {
        if (step != 0 || hint == null) return;
        step = 1;
        hint.text = BattleText;
        if (mineRing != null) Destroy(mineRing);
        hintRect.DOKill();
        hintRect.localScale = Vector3.one * 0.6f;
        hintRect.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
        var button = BaseWorldUI.Instance?.BattleButton;
        if (button != null)
            buttonPulse = button.transform.DOScale(1.06f, 0.5f).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
    }

    public void OnBattleTapped()
    {
        if (step != 1 || GameManager.Instance?.PlayerData == null) return;
        GameManager.Instance.PlayerData.firstBaseGuideDone = true;
        GameManager.Instance.SaveGame();
        StopPulse();
        if (hintRect != null) Destroy(hintRect.gameObject);
    }

    private void StopPulse()
    {
        if (buttonPulse == null) return;
        buttonPulse.Kill();
        buttonPulse = null;
        var button = BaseWorldUI.Instance?.BattleButton;
        if (button != null) button.transform.localScale = Vector3.one;
    }

    private void OnDestroy()
    {
        StopPulse();
        if (pointerRect != null) pointerRect.DOKill();
        if (hintRect != null) hintRect.DOKill();
        if (Instance == this) Instance = null;
    }

    private static Sprite triangle;

    private static Sprite TriangleSprite()
    {
        if (triangle != null) return triangle;
        const int w = 32, h = 24;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            // Aşağıyı gösteren üçgen: tepe y=0'da.
            float halfWidth = (y + 0.5f) / h * (w * 0.5f);
            float alpha = Mathf.Clamp01(halfWidth - Mathf.Abs(x + 0.5f - w * 0.5f) + 0.5f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();
        triangle = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return triangle;
    }
}
