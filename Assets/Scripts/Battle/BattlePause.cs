using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 6. oturum (1.5): savaş duraklatmasının TEK girişi. Birden çok neden aynı anda olabilir ("settings", "background"); hepsi kalkınca
/// oyun kaldığı yerden sürer. Duraklatma: <c>Time.timeScale = 0</c> (savaş zamanlayıcıları, dalga sayacı, süre, yetenek ve kule bekleme
/// süreleri, coroutine'ler ve DOTween ölçekli zamanla çalışır) + <c>AudioListener.pause</c> (müzik ve efektler).
/// Devam ederken duraklatma öncesi zaman ölçeği geri gelir (ör. "devam teklifi" penceresinin 0'ı ezilmez).
/// Oyun arka plana alınınca / odak kaybolunca (telefon, ekran kilidi) savaş otomatik duraklar ve dönüşte "DEVAM" düğmesi gösterilir.
/// </summary>
public static class BattlePause
{
    public const string Settings = "settings";
    public const string Background = "background";

    private static readonly HashSet<string> reasons = new HashSet<string>();
    private static float resumeScale = 1f;

    public static bool IsPaused => reasons.Count > 0;
    /// <summary>Uygulama arka planda/odaksızsa ses açılmaz (AudioManager.SetAppPaused/SetAppFocused).</summary>
    private static bool AppInBackground => AudioManager.Instance != null && AudioManager.Instance.InBackground;
    public static bool Has(string reason) => reasons.Contains(reason);
    public static event System.Action Changed;

    public static void Pause(string reason)
    {
        if (string.IsNullOrEmpty(reason) || reasons.Contains(reason)) return;
        if (reasons.Count == 0) resumeScale = Time.timeScale;
        reasons.Add(reason);
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Changed?.Invoke();
    }

    public static void Resume(string reason)
    {
        if (!reasons.Remove(reason)) return;
        if (reasons.Count == 0)
        {
            Time.timeScale = resumeScale;
            AudioListener.pause = AppInBackground;
        }
        Changed?.Invoke();
    }

    /// <summary>Sahne değişince / savaş bitince: tüm nedenler kalkar, zaman ve ses normale döner.</summary>
    public static void ResetAll()
    {
        if (reasons.Count == 0) return;
        reasons.Clear();
        Time.timeScale = resumeScale > 0f ? resumeScale : 1f;
        AudioListener.pause = AppInBackground;
        Changed?.Invoke();
    }

    /// <summary>Uygulama arka plana gitti / odak kaybetti (savaş sürüyorsa duraklat).</summary>
    public static void OnAppBackgrounded()
    {
        var battle = BattleManager.Instance;
        if (battle == null || !battle.IsBattleActive) return;
        Pause(Background);
    }
}

/// <summary>Savaş sahnesinde yaşar: uygulama yaşam döngüsünü dinler, arka plan duraklatmasında "DEVAM" penceresini gösterir.</summary>
public sealed class BattlePauseWatcher : MonoBehaviour
{
    private GameObject overlay;
    public GameObject Overlay => overlay;

    public static BattlePauseWatcher Install()
    {
        var existing = FindFirstObjectByType<BattlePauseWatcher>();
        if (existing != null) return existing;
        return new GameObject("BattlePauseWatcher").AddComponent<BattlePauseWatcher>();
    }

    private void OnEnable() => BattlePause.Changed += Refresh;
    private void OnDisable() => BattlePause.Changed -= Refresh;

    private void OnApplicationPause(bool paused) { if (paused) BattlePause.OnAppBackgrounded(); }
    private void OnApplicationFocus(bool focus)
    {
        // Editörde odak kaybı (başka pencereye geçmek) testleri bozmasın; cihazda telefon / bildirim perdesi odak kaybıdır.
        if (!focus && !Application.isEditor) BattlePause.OnAppBackgrounded();
    }

    private void OnDestroy() => BattlePause.ResetAll();

    private void Refresh()
    {
        bool show = BattlePause.Has(BattlePause.Background);
        if (show && overlay == null) Build();
        if (overlay != null) overlay.SetActive(show);
    }

    private void Build()
    {
        overlay = new GameObject("BattlePausedOverlay", typeof(RectTransform));
        var canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        ReferenceUiVisuals.ConfigureCanvas(overlay.AddComponent<CanvasScaler>());
        overlay.AddComponent<GraphicRaycaster>();
        var dim = ReferenceUiVisuals.Image(overlay.transform, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.03f, 0.02f, 0.01f, 0.72f));
        dim.raycastTarget = true;
        var safe = ReferenceUiVisuals.SafeArea(overlay.transform);
        var panel = FancyInfoPanel.NineSlice(safe, "PausePanel", new Vector2(0.12f, 0.40f), new Vector2(0.88f, 0.60f),
            Vector2.zero, Vector2.zero, "ui_paper", Color.white, 2.4f);
        panel.raycastTarget = true;
        var ribbon = FancyInfoPanel.NineSlice(panel.transform, "Ribbon", new Vector2(0.08f, 1f), new Vector2(0.92f, 1f),
            new Vector2(0, -30), new Vector2(0, 18), "ui_ribbon", Color.white, 3f);
        ReferenceUiVisuals.Text(ribbon.transform, "Title", L.T("DURAKLATILDI", "PAUSED"), 20, Color.white,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -2), true);
        ReferenceUiVisuals.Text(panel.transform, "Body", L.T("Savaş seni bekliyor.", "The battle is waiting for you."), 16,
            ReferenceUiVisuals.Hex(0x3A2814), TextAlignmentOptions.Center, new Vector2(0.06f, 0.44f), new Vector2(0.94f, 0.74f),
            Vector2.zero, Vector2.zero, false, true);
        var button = FancyInfoPanel.TsButton(panel.transform, "ResumeButton", L.T("DEVAM", "RESUME"), false,
            new Vector2(0.22f, 0.08f), new Vector2(0.78f, 0.40f), Vector2.zero, Vector2.zero, 20, out _);
        button.onClick.AddListener(() => BattlePause.Resume(BattlePause.Background));
    }
}
