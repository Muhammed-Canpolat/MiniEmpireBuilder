using System.Collections;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Sen yokken" penceresi (Ana Üs): madenin ürettiği altın, biten işler, dolan canlar.
/// "TOPLA" madendeki altını cüzdana taşır. Giriş takvimi bu pencere kapanınca açılır.
/// </summary>
public sealed class AwayReportUI : MonoBehaviour
{
    private static readonly Color Ink = ReferenceUiVisuals.Hex(0x3B2A1A);
    private static readonly Color InkMuted = ReferenceUiVisuals.Hex(0x6B5236);

    public static bool IsOpen { get; private set; }

    private RectTransform safe;
    private GameObject popup;

    private IEnumerator Start()
    {
        var canvasObject = new GameObject("AwayReportCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 47;
        ReferenceUiVisuals.ConfigureCanvas(canvasObject.GetComponent<CanvasScaler>());
        safe = ReferenceUiVisuals.SafeArea(canvas.transform);
        AwayReport.OnReady += HandleReady;
        // Üs kurulup maden üretimi uygulansın diye kısa bekleme.
        if (AwayReport.Pending != null) IsOpen = true; // takvim penceresi beklesin
        yield return new WaitForSeconds(0.7f);
        ShowPending();
    }

    private void OnDestroy()
    {
        AwayReport.OnReady -= HandleReady;
        IsOpen = false;
    }

    private void HandleReady() => StartCoroutine(ShowNextFrame());

    private IEnumerator ShowNextFrame()
    {
        IsOpen = true;
        yield return null; // dönüşte maden üretimi önce yazılsın
        ShowPending();
    }

    private void ShowPending()
    {
        var report = AwayReport.Take();
        var gm = GameManager.Instance;
        if (report == null || gm == null || gm.PlayerData == null) { IsOpen = popup != null; return; }

        int mined = Mathf.Max(0, gm.GoldMineStored - report.storedBefore);
        bool mineFull = gm.GoldMineStored >= gm.GetGoldMineStorageCapacity() && gm.GetGoldMineStorageCapacity() > 0;
        int livesGained = Mathf.Max(0, LivesSystem.Lives - report.livesBefore);
        if (mined <= 0 && report.finished.Count == 0 && livesGained <= 0) { IsOpen = popup != null; return; }

        var lines = new StringBuilder();
        if (mined > 0)
            lines.AppendLine(L.T($"<b>Maden:</b> +{mined} altın", $"<b>Mine:</b> +{mined} gold") +
                             (mineFull ? L.T(" (depo doldu)", " (storage full)") : ""));
        foreach (var item in report.finished)
            lines.AppendLine(L.T($"<b>Bitti:</b> {item}", $"<b>Finished:</b> {item}"));
        if (livesGained > 0)
            lines.AppendLine(L.T($"<b>Can:</b> +{livesGained} ({LivesSystem.Lives}/{LivesSystem.MaxLives})",
                $"<b>Lives:</b> +{livesGained} ({LivesSystem.Lives}/{LivesSystem.MaxLives})"));
        Build(Progression.FormatDuration(report.seconds), lines.ToString().TrimEnd(), gm.GoldMineStored > 0);
    }

    private void Build(string duration, string body, bool canCollect)
    {
        if (popup != null) Destroy(popup);
        IsOpen = true;
        var blocker = ReferenceUiVisuals.Image(safe, "AwayReportPopup", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.62f));
        blocker.raycastTarget = true;
        popup = blocker.gameObject;

        var center = new Vector2(0.5f, 0.5f);
        var panel = ReferenceUiVisuals.Image(popup.transform, "Panel", center, center, new Vector2(-160, -150), new Vector2(160, 150),
            Color.white);
        panel.sprite = CampaignSceneSetup.Art("ui_paper", 22f);
        panel.type = Image.Type.Sliced;
        panel.raycastTarget = true;
        panel.transform.localScale = Vector3.one * 0.85f;
        panel.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetLink(panel.gameObject);
        var p = panel.transform;
        ReferenceUiVisuals.Text(p, "Title", L.T("SEN YOKKEN", "WHILE YOU WERE AWAY"), 20, Ink, TextAlignmentOptions.Center,
            new Vector2(0, 1), Vector2.one, new Vector2(20, -50), new Vector2(-20, -16), true);
        ReferenceUiVisuals.Text(p, "Duration", duration, 12, InkMuted, TextAlignmentOptions.Center,
            new Vector2(0, 1), Vector2.one, new Vector2(20, -70), new Vector2(-20, -50), false, true);
        var bodyText = ReferenceUiVisuals.Text(p, "Body", body, 14, Ink, TextAlignmentOptions.TopLeft,
            Vector2.zero, Vector2.one, new Vector2(26, 84), new Vector2(-26, -78), false);
        bodyText.richText = true;

        var ok = ReferenceUiVisuals.Button(p, "Collect", canCollect ? L.T("TOPLA", "COLLECT") : L.T("TAMAM", "OK"), true,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(22, 20), new Vector2(-22, 72), 18);
        ok.onClick.AddListener(() =>
        {
            if (canCollect)
            {
                int collected = GameManager.Instance != null ? GameManager.Instance.CollectGoldFromMineStorage() : 0;
                if (collected > 0)
                {
                    AudioManager.Instance?.PlaySfx("goldCollect");
                    GemActions.Toast(L.T($"+{collected} altın", $"+{collected} gold"));
                }
            }
            Destroy(popup);
            popup = null;
            IsOpen = false;
        });
    }
}

/// <summary>
/// Yeni sistemler için ilk görüşte bir kez çıkan kısa ipucu (alt kısımda balon, "ANLADIM").
/// Gösterilenler PlayerPrefs'te; aynı anda birden çok gelirse sırayla gösterilir.
/// </summary>
public sealed class FirstTimeTips : MonoBehaviour
{
    private const string Prefix = "MiniEmpireBuilder_Tip_";
    private static FirstTimeTips instance;
    private readonly System.Collections.Generic.Queue<string> queue = new System.Collections.Generic.Queue<string>();
    private GameObject bubble;

    public static bool Seen(string key) => PlayerPrefs.GetInt(Prefix + key, 0) == 1;

    public static void Show(string key, string text)
    {
        if (string.IsNullOrEmpty(key) || Seen(key)) return;
        PlayerPrefs.SetInt(Prefix + key, 1);
        PlayerPrefs.Save();
        if (instance == null)
        {
            var go = new GameObject("FirstTimeTips");
            instance = go.AddComponent<FirstTimeTips>();
        }
        instance.queue.Enqueue(text);
        if (instance.bubble == null) instance.Next();
    }

    /// <summary>Testler ve "ipuçlarını yeniden göster" için.</summary>
    public static void Reset(params string[] keys)
    {
        foreach (var key in keys) PlayerPrefs.DeleteKey(Prefix + key);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Next()
    {
        if (queue.Count == 0) { Destroy(gameObject); return; }
        string text = queue.Dequeue();
        bubble = new GameObject("TipCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        bubble.transform.SetParent(transform, false);
        var canvas = bubble.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 260;
        ReferenceUiVisuals.ConfigureCanvas(bubble.GetComponent<CanvasScaler>());
        var safe = ReferenceUiVisuals.SafeArea(canvas.transform);
        var panel = ReferenceUiVisuals.Image(safe, "Tip", new Vector2(0.05f, 0.17f), new Vector2(0.95f, 0.17f),
            new Vector2(0, 0), new Vector2(0, 120), Color.white);
        panel.sprite = CampaignSceneSetup.Art("ui_paper", 18f);
        panel.type = Image.Type.Sliced;
        panel.raycastTarget = true;
        panel.transform.localScale = Vector3.one * 0.9f;
        panel.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(panel.gameObject);
        var icon = ReferenceUiVisuals.Image(panel.rectTransform, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(12, -24), new Vector2(56, 24), Color.white);
        icon.sprite = CampaignSceneSetup.Art("ui_hero_marker");
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        ReferenceUiVisuals.Text(panel.rectTransform, "Text", text, 14, ReferenceUiVisuals.Hex(0x3B2A1A),
            TextAlignmentOptions.Left, Vector2.zero, Vector2.one, new Vector2(64, 46), new Vector2(-14, -10), false, true);
        var ok = ReferenceUiVisuals.Button(panel.rectTransform, "Ok", L.T("ANLADIM", "GOT IT"), true,
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-132, 8), new Vector2(-12, 42), 13);
        ok.onClick.AddListener(() =>
        {
            Destroy(bubble);
            bubble = null;
            Next();
        });
    }
}
