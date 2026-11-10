#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Geliştirici test menüsü: yalnız editörde ve Development APK'da derlenir (yayın sürümünde yok).
/// Sol kenardaki küçük "DEV" düğmesi; altın/elmas, Ana Üs seviyesi, bölge açma, zamanı ileri alma,
/// can ve giriş takvimi denemeleri, savaşta düşmanları temizleme.
/// </summary>
public sealed class DevMenu : MonoBehaviour
{
    private RectTransform safe;
    private GameObject panel;
    private TextMeshProUGUI info;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<DevMenu>() != null) return;
        var go = new GameObject("DevMenu");
        DontDestroyOnLoad(go);
        go.AddComponent<DevMenu>();
    }

    private void Start()
    {
        var canvasObject = new GameObject("DevMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 280; // dükkânın (250) üstünde, onay penceresinin (300) altında
        ReferenceUiVisuals.ConfigureCanvas(canvasObject.GetComponent<CanvasScaler>());
        safe = ReferenceUiVisuals.SafeArea(canvas.transform);

        var anchor = new Vector2(0f, 0.55f);
        var toggle = ReferenceUiVisuals.Button(safe, "DevToggle", "DEV", false, anchor, anchor,
            new Vector2(6, -14), new Vector2(50, 14), 11);
        toggle.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.55f);
        toggle.onClick.AddListener(() => { if (panel != null) Close(); else Open(); });
    }

    private void Close()
    {
        if (panel != null) Destroy(panel);
        panel = null;
    }

    private void Open()
    {
        Close();
        var bg = ReferenceUiVisuals.Image(safe, "DevPanel", new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.82f),
            Vector2.zero, Vector2.zero, new Color(0.08f, 0.06f, 0.05f, 0.95f));
        bg.raycastTarget = true;
        panel = bg.gameObject;
        var p = bg.rectTransform;
        ReferenceUiVisuals.Text(p, "Title", "TEST MENÜSÜ (yalnız Development)", 16, ReferenceUiVisuals.Gold,
            TextAlignmentOptions.Center, new Vector2(0, 0.93f), Vector2.one, new Vector2(10, 0), new Vector2(-50, -4), true);
        var close = ReferenceUiVisuals.Button(p, "Close", "X", false, Vector2.one, Vector2.one,
            new Vector2(-44, -40), new Vector2(-6, -4), 14);
        close.onClick.AddListener(Close);
        info = ReferenceUiVisuals.Text(p, "Info", "", 12, ReferenceUiVisuals.Muted, TextAlignmentOptions.Center,
            new Vector2(0, 0.86f), new Vector2(1, 0.93f), new Vector2(10, 0), new Vector2(-10, 0), false, true);

        var gm = GameManager.Instance;
        bool battle = BattleManager.Instance != null;
        var actions = new (string label, Action action, bool reload)[]
        {
            ("+1000 ALTIN", () => gm?.AddGold(1000), false),
            ("+100 ELMAS", () => gm?.AddGems(100), false),
            ("ALTIN = 0", () => { if (gm != null) { gm.PlayerData.gold = 0; gm.SaveGame(); } }, true),
            ("ELMAS = 0", () => { if (gm != null) { gm.PlayerData.gems = 0; gm.SaveGame(); } }, true),
            ("ANA ÜS +1", () => gm?.DevSetMainBaseLevel(gm.GetMainBaseLevel() + 1), true),
            ("ANA ÜS −1", () => gm?.DevSetMainBaseLevel(gm.GetMainBaseLevel() - 1), true),
            ("SONRAKİ BÖLGEYİ AÇ", OpenNextRegion, true),
            ("ZAMAN +1 SAAT", () => gm?.DevAdvanceTime(TimeSpan.FromHours(1)), false),
            ("ZAMAN +8 SAAT", () => gm?.DevAdvanceTime(TimeSpan.FromHours(8)), false),
            ("YARINA GEÇ (TAKVİM)", NextDay, true),
            ("CANLAR = 0", () => { if (gm != null) { gm.PlayerData.lives = 0; gm.PlayerData.livesLastRegenTicks = DateTime.UtcNow.Ticks; gm.PlayerData.unlimitedLivesUntilTicks = 0; gm.SaveGame(); } }, true),
            ("CANLARI DOLDUR", () => LivesSystem.AddLives(LivesSystem.MaxLives), true),
            (battle ? "DÜŞMANLARI ÖLDÜR" : "SAHNEYİ YENİLE", battle ? (Action)KillEnemies : () => { }, !battle),
            ("KRALİYET AÇ/KAPAT", () => { if (gm != null) { gm.PlayerData.royalUntilTicks = BattleCards.IsRoyal ? 0 : DateTime.UtcNow.AddDays(1).Ticks; gm.PlayerData.royalGemDay = 0; gm.SaveGame(); } }, true),
            ("REKLAM HAKLARINI SIFIRLA", () => { if (gm != null) { var d = gm.PlayerData; d.adSpeedupDay = d.adMineDay = d.adChestDay = d.gemAdDay = d.adLivesDay = 0; gm.SaveGame(); } }, false),
            ("TAKVİMİ SIFIRLA", () => { if (gm != null) { gm.PlayerData.dailyStreak = 0; gm.PlayerData.dailyLastClaimDay = 0; DailyRewards.TodayOverride = null; gm.SaveGame(); } }, true),
        };

        const int columns = 2;
        float rowHeight = 0.86f / Mathf.Ceil(actions.Length / (float)columns);
        for (int i = 0; i < actions.Length; i++)
        {
            int col = i % columns, row = i / columns;
            float top = 0.86f - row * rowHeight;
            var entry = actions[i];
            var button = ReferenceUiVisuals.Button(p, "Dev_" + i, entry.label, false,
                new Vector2(col * 0.5f, top - rowHeight), new Vector2((col + 1) * 0.5f, top),
                new Vector2(8, 4), new Vector2(-8, -4), 13);
            button.onClick.AddListener(() =>
            {
                try { entry.action?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
                Refresh();
                // Görünümü etkileyen değişikliklerde sahne baştan kurulur (savaş hariç).
                if (entry.reload && BattleManager.Instance == null)
                {
                    Close();
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                }
            });
        }
        Refresh();
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (info == null || gm?.PlayerData == null) return;
        info.text = $"Ana Üs {gm.GetMainBaseLevel()} · Altın {gm.Gold} · Elmas {gm.Gems} · Can {LivesSystem.Lives}" +
                    (DailyRewards.TodayOverride.HasValue ? $" · Gün {DailyRewards.TodayOverride.Value:dd.MM}" : "");
    }

    /// <summary>Henüz bossu yenilmemiş ilk bölgenin 5 aşamasını 3 yıldızla bitirir; sonraki bölgenin Ana Üs şartını sağlar.</summary>
    private static void OpenNextRegion()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var regions = CampaignCatalog.Regions;
        for (int i = 0; i < regions.Length; i++)
        {
            var region = regions[i];
            if (CampaignProgress.Stars(region.id, 5) > 0) continue;
            for (int level = 1; level <= 5; level++) CampaignProgress.RecordVictory(region.id, level, 3);
            int needed = i + 1 < regions.Length ? regions[i + 1].requiredBaseLevel : region.requiredBaseLevel;
            if (gm.GetMainBaseLevel() < needed) gm.DevSetMainBaseLevel(needed);
            return;
        }
    }

    private static void NextDay()
    {
        DailyRewards.TodayOverride = (DailyRewards.TodayOverride ?? DateTime.Now).Date.AddDays(1);
    }

    private static void KillEnemies()
    {
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            if (enemy != null && !enemy.IsDead) enemy.TakeDamage(999999f);
    }
}
#endif
