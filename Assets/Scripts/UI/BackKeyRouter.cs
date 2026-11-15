using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 9. oturum (teknik denetim E-1 / Y-6): Android geri tuşunun TEK yöneticisi. Geri tuşu yeni Input System'de Escape olarak gelir.
/// Kalıcı (DontDestroyOnLoad) tek bir nesne; derleme koşulu yok, yayın derlemesinde de çalışır. <see cref="HandleBack"/> sırası
/// (ilk "işledim" diyen kazanır, basış başına tek işlem):
///   a) sahne geçişi/kararma örtüsü sürüyorsa hiçbir şey yapma,
///   b) çıkış onayı açıksa kapat,
///   c) açık pencereler (en üstteki önce; her birinin mevcut kapatma yolu) ve kapatılamayan zorunlu akışlar (yutulur),
///   d) yerleştirme/taşıma kipi → mevcut İPTAL,
///   e) seçim → <see cref="SelectionManager.Clear"/>,
///   f) açık radyal menü → kapat,
///   g) sahneye göre varsayılan: Savaş/Ada akını → ayarlar (duraklatma), Sefer → bölge paneli/ÜSSE DÖN, Ada → ANA ÜS,
///      Ana Üs ve Ana Menü → çıkış onayı.
/// </summary>
public sealed class BackKeyRouter : MonoBehaviour
{
    private static BackKeyRouter instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var go = new GameObject("BackKeyRouter");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<BackKeyRouter>();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) HandleBack();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>Geri tuşuna basıldı. Bir şey işlendiyse (ya da bilerek yutulduysa) true.</summary>
    public static bool HandleBack()
    {
        try { return Route(); }
        catch (Exception e)
        {
            Debug.LogWarning("[BackKey] işleme hatası: " + e.Message);
            return true;
        }
    }

    /// <summary>Son geri tuşunu hangi basamak işledi (tanılama ve testler): transition / quit / window / placement / selection / radial / scene.</summary>
    public static string LastStep { get; private set; } = "";

    private static bool Step(string step) { LastStep = step; return true; }

    private static bool Route()
    {
        // a) Sahne geçişi sürüyor: bulut örtüsü ya da kararma (GameManager.FadeAndLoadScene "SceneFade" örtüsü).
        if (SceneCloudTransition.Active != null || GameObject.Find("SceneFade") != null) return Step("transition");

        // b) Çıkış onayı
        if (QuitConfirm.IsOpen) { QuitConfirm.Close(); return Step("quit"); }

        if (CloseWindow()) return Step("window");

        // d) Yerleştirme / taşıma kipi: mevcut İPTAL yolu
        var placement = PlacementController.Instance;
        if (placement != null && placement.IsPlacing) { placement.CancelPlacement(); return Step("placement"); }
        var decor = DecorPlacementController.Instance;
        if (decor != null && decor.IsPlacing) { decor.Cancel(); return Step("placement"); }
        var island = IslandHudOrNull();
        if (island != null && island.CancelPlacementForBack()) return Step("placement");

        // e) Seçim
        if (SelectionManager.Clear()) return Step("selection");

        // f) Radyal menü
        var radial = RadialDockMenu.Current;
        if (radial != null && radial.IsOpen) { radial.Toggle(); return Step("radial"); }

        LastStep = "scene";
        return SceneDefault();
    }

    /// <summary>c) Açık pencereler, en üstteki (tuval sıralaması yüksek) önce. Zorunlu akışlar tuşu yutar.</summary>
    private static bool CloseWindow()
    {
        // Arka plan duraklatması "DEVAM" ekranı: DEVAM düğmesinin yaptığı
        if (BattlePause.Has(BattlePause.Background)) { BattlePause.Resume(BattlePause.Background); return true; }
        // Ödüllü (deneme) reklam ekranı: kapatılamaz
        if (RewardedAds.IsShowing) return true;
        // Ayarlar (1200): PES ET onayı açıksa yalnız onu kapatır
        if (AudioSettingsUI.CloseIfOpen()) return true;
        // Ada ad penceresi (320): yeniden adlandırma kapanır, ilk ad verme/teklif yutulur
        if (IslandNameUi.CloseIfOpen()) return true;
        // Ada tanıtımı ve inşa öğreticisi (310): zorunlu
        if (IslandIntro.IsPlaying || IslandBuildTutorial.Current != null) return true;
        // "Ana Üs yıkıldı, devam et?" teklifi: zorunlu karar (GemConfirm olarak açılır; kapatmak yenilgidir)
        var battle = BattleManager.Instance;
        if (battle != null && battle.AwaitingContinue) return true;
        // Elmas onayı (300) ve kart kilidi diyaloğu (300)
        if (GemConfirm.IsOpen) { GemConfirm.Close(true); return true; }
        if (BattleCardUnlockDialog.Current != null) { BattleCardUnlockDialog.Close(); return true; }
        // Elmas dükkânı ve kart atölyesi (250)
        if (GemShopUI.IsOpen) { GemShopUI.Close(); return true; }
        if (CardWorkshopPanel.IsOpen) { CardWorkshopPanel.Close(); return true; }
        // Mod/aşama başlatma penceresi (240)
        if (ModeLaunchPanel.IsOpen) { ModeLaunchPanel.Close(); return true; }
        // Inşaatçı penceresi
        if (BuilderPanel.IsOpen) { BuilderPanel.Hide(); return true; }
        // Sen yokken / giriş takvimi / günlük görevler
        if (AwayReportUI.CloseIfOpen()) return true;
        if (DailyRewardsUI.CloseIfOpen()) return true;
        if (DailyQuestsUI.CloseIfOpen()) return true;
        // Ana Üs inşa menüsü (Yapılar / Dekor / Dükkân sekmeleri) — PanelController.OpenModal'ın kapatma yolu BuildMenuController.Hide
        var buildMenu = BuildMenuController.Instance;
        if (buildMenu != null && buildMenu.IsOpen) { buildMenu.Hide(); return true; }
        // Dünya eylem menüsü
        var actions = WorldActionMenu.Current;
        if (actions != null) { actions.Close(); return true; }
        // Ada: akın sonucu (yutulur), Akın panosu, inşa menüsü/paneli
        var island = IslandHudOrNull();
        if (island != null && island.CloseWindowForBack()) return true;
        return false;
    }

    private static IslandHud IslandHudOrNull()
    {
        if (!IslandSession.Active) return null;
        var setup = IslandSceneSetup.Instance;
        return setup != null ? setup.Hud : null;
    }

    /// <summary>g) Sahneye göre varsayılan davranış.</summary>
    private static bool SceneDefault()
    {
        string scene = SceneManager.GetActiveScene().name;
        switch (scene)
        {
            case "BattleScene":
            {
                var island = IslandHudOrNull();
                if (island != null)
                {
                    if (island.GoHomeForBack()) return true;   // ada (savaş dışı): ANA ÜS
                    AudioSettingsUI.OpenIfPossible();          // ada akını sürüyor: ayarlar
                    return true;
                }
                AudioSettingsUI.OpenIfPossible();              // savaş: ayarlar (BattlePause.Settings); sonuç ekranında açılmaz, tuş yutulur
                return true;
            }
            case "CampaignScene":
            {
                var campaign = FindAnyObjectByType<CampaignSceneSetup>();
                return campaign != null && campaign.HandleBackKey();
            }
            case "MainMenuScene":
            {
                var menu = FindAnyObjectByType<MainMenuUI>();
                if (menu != null && menu.HandleBackKey()) return true;
                QuitConfirm.Show();
                return true;
            }
            case "BaseScene":
                QuitConfirm.Show();
                return true;
            default:
                return false;
        }
    }
}

/// <summary>
/// Çıkış onayı ("Oyundan çıkılsın mı?" ÇIK / KAL). Kendi tuvalinde (en üstte), projenin kâğıt pencere stiliyle; yeni sanat yok.
/// ÇIK: oyun kaydedilir, uygulama kapanır. Karartmaya dokunmak KAL ile aynıdır.
/// </summary>
public sealed class QuitConfirm : MonoBehaviour
{
    private static readonly Color Ink = ReferenceUiVisuals.Hex(0x3B2A1A);
    private static QuitConfirm open;

    public static bool IsOpen => open != null;

    public static void Show()
    {
        if (open != null) return;
        var root = new GameObject("QuitConfirm", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1300;   // ayarlar (1200) dahil her pencerenin üstünde
        ReferenceUiVisuals.ConfigureCanvas(root.GetComponent<CanvasScaler>());
        open = root.AddComponent<QuitConfirm>();
        var safe = ReferenceUiVisuals.SafeArea(canvas.transform);

        var dim = ReferenceUiVisuals.Image(safe, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.6f));
        dim.raycastTarget = true;
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Close);

        var center = new Vector2(0.5f, 0.5f);
        var panel = ReferenceUiVisuals.Image(safe, "Panel", center, center, new Vector2(-150, -90), new Vector2(150, 90), Color.white);
        panel.sprite = CampaignSceneSetup.Art("ui_paper", 22f);
        panel.type = Image.Type.Sliced;
        panel.raycastTarget = true;

        ReferenceUiVisuals.Text(panel.transform, "Title", L.T("Oyundan çıkılsın mı?", "Quit the game?"), 21, Ink,
            TextAlignmentOptions.Center, new Vector2(0, 0.42f), Vector2.one, new Vector2(24, 0), new Vector2(-24, -22), true);

        var stay = FancyInfoPanel.TsButton(panel.transform, "StayButton", L.T("KAL", "STAY"), false,
            new Vector2(0.05f, 0), new Vector2(0.49f, 0), new Vector2(0, 22), new Vector2(0, 76), 17, out _);
        stay.onClick.AddListener(Close);
        var quit = FancyInfoPanel.TsButton(panel.transform, "QuitButton", L.T("ÇIK", "QUIT"), true,
            new Vector2(0.51f, 0), new Vector2(0.95f, 0), new Vector2(0, 22), new Vector2(0, 76), 17, out _);
        quit.onClick.AddListener(Quit);
    }

    public static void Close()
    {
        if (open == null) return;
        var go = open.gameObject;
        open = null;
        Destroy(go);
    }

    private static void Quit()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.PlayerData != null) gm.SaveGame();
        Application.Quit();
    }

    private void OnDestroy()
    {
        if (open == this) open = null;
    }
}
