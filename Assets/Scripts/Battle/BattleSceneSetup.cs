using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// Savaş sahnesini otomatik olarak kurar — test etmek için kolaylık
/// Bu script BattleScene'de boş bir GameObject'e eklenir
/// Tüm objeleri (Ana Üs, Kahraman, UI) kodla oluşturur
/// </summary>
public class BattleSceneSetup : MonoBehaviour
{
    private Canvas mainCanvas;
    private GameObject weaponSelectPanel;
    private bool battleStarted = false;

    private void Awake()
    {
        // GameManager yoksa oluştur (direkt BattleScene açıldığında test için)
        if (GameManager.Instance == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            gmObj.AddComponent<GameManager>();
        }

        // SpriteManager yoksa oluştur
        if (SpriteManager.Instance == null)
        {
            GameObject smObj = new GameObject("SpriteManager");
            smObj.AddComponent<SpriteManager>();
        }
    }

    private void Start()
    {
        // Kamerayı ayarla
        Camera.main.orthographic = true;
        Camera.main.orthographicSize = 8f;
        Camera.main.backgroundColor = new Color(0.15f, 0.15f, 0.2f);

        // EventSystem yoksa oluştur
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // Oyuncu verisi yoksa intro sahnesine gönder
        if (GameManager.Instance.PlayerData == null)
        {
            // Test modunda direkt açılmışsa — varsayılan silahla başlat
            Debug.LogWarning("[BattleSceneSetup] PlayerData yok! Test modu: varsayilan silahla baslatiliyor.");
            GameManager.Instance.StartNewGame(WeaponType.Spear);
            return; // StartNewGame BaseScene'e yönlendirir
        }

        // Normal akış — savaşı başlat
        StartBattle();

        // Sahne fade-in
        StartCoroutine(BattleFadeIn());
    }

    private IEnumerator BattleFadeIn()
    {
        GameObject fadeObj = new GameObject("FadePanel");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 999;

        GameObject panel = new GameObject("Black");
        panel.transform.SetParent(fadeObj.transform, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image fadeImg = panel.AddComponent<Image>();
        fadeImg.color = Color.black;
        fadeImg.raycastTarget = false;

        float t = 0;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            fadeImg.color = new Color(0, 0, 0, 1f - Mathf.Clamp01(t / 0.5f));
            yield return null;
        }
        Destroy(fadeObj);
    }

    // ==================== SİLAH SEÇİM EKRANI ====================

    private void ShowWeaponSelection()
    {
        // Canvas oluştur
        GameObject canvasObj = new GameObject("WeaponSelectCanvas");
        mainCanvas = canvasObj.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mainCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 1f; // Yüksekliğe göre ölçekle (dikey oyun)

        canvasObj.AddComponent<GraphicRaycaster>();

        // Arka plan — tam ekran
        weaponSelectPanel = CreatePanel(canvasObj.transform, "WeaponSelectBg",
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            new Color(0.08f, 0.08f, 0.12f, 1f));

        // ===== BAŞLIK — ekranın üst %6'sı =====
        // Anchor: y 0.94 → 1.00
        GameObject titleArea = new GameObject("TitleArea");
        titleArea.transform.SetParent(weaponSelectPanel.transform, false);
        RectTransform titleRt = titleArea.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0.94f);
        titleRt.anchorMax = new Vector2(1, 1f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        TextMeshProUGUI title = CreateText(titleArea.transform, "Title",
            new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f),
            Vector2.zero, new Vector2(600, 45),
            "SILAHINI SEC", 34, TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.85f, 0.2f);
        title.fontStyle = FontStyles.Bold;

        TextMeshProUGUI sub = CreateText(titleArea.transform, "Subtitle",
            new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f),
            Vector2.zero, new Vector2(700, 25),
            "Her silah farkli bir strateji sunar", 16, TextAlignmentOptions.Center);
        sub.color = new Color(0.6f, 0.6f, 0.7f);

        // ===== 3 KART — ekranın %8 - %93 arası, her biri eşit =====
        // Padding: üstten %7, alttan %7
        // Her kart arası %1.5 boşluk
        // Kart alanı: %93 - %8 = %85 → her kart %27, boşluk %2
        float topPad = 0.93f;   // başlık altı
        float botPad = 0.02f;   // alt boşluk
        float cardGap = 0.015f; // kartlar arası
        float totalSpace = topPad - botPad - 2 * cardGap; // %85
        float cardH = totalSpace / 3f; // her kart ~%28.3

        // Kart 1 — BALTA
        float c1Top = topPad;
        float c1Bot = c1Top - cardH;
        CreateWeaponCardAnchored(weaponSelectPanel.transform, "AxeCard",
            c1Bot, c1Top,
            "BALTA", new Color(0.9f, 0.3f, 0.2f),
            "Tank Savasci",
            "Dusmanlarin icine dal, sert vur!",
            new float[] { 1.0f, 0.2f, 0.3f, 1.0f },
            WeaponType.Axe);

        // Kart 2 — MIZRAK
        float c2Top = c1Bot - cardGap;
        float c2Bot = c2Top - cardH;
        CreateWeaponCardAnchored(weaponSelectPanel.transform, "SpearCard",
            c2Bot, c2Top,
            "MIZRAK", new Color(0.3f, 0.7f, 1f),
            "Savunma Savascisi",
            "Pozisyon tut, dusmanlarini durdur!",
            new float[] { 0.5f, 0.6f, 0.4f, 1.0f },
            WeaponType.Spear);

        // Kart 3 — OK
        float c3Top = c2Bot - cardGap;
        float c3Bot = c3Top - cardH;
        CreateWeaponCardAnchored(weaponSelectPanel.transform, "BowCard",
            c3Bot, c3Top,
            "OK", new Color(0.2f, 0.85f, 0.4f),
            "Okcu",
            "Guvenli mesafeden dusmanlarini vur!",
            new float[] { 0.35f, 1.0f, 0.8f, 0.35f },
            WeaponType.Bow);

        Debug.Log("[BattleSceneSetup] Silah secim ekrani gosterildi");
    }

    /// <summary>
    /// Anchor-bazlı silah kartı — ekran yüzdesine göre konumlanır
    /// </summary>
    private void CreateWeaponCardAnchored(Transform parent, string name,
        float anchorBot, float anchorTop,
        string weaponName, Color weaponColor,
        string className, string desc,
        float[] stats, WeaponType weapon)
    {
        // Kart — yatay: %5 - %95 arası, dikey: parametrelere göre
        GameObject card = new GameObject(name);
        card.transform.SetParent(parent, false);
        RectTransform cardRt = card.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.04f, anchorBot);
        cardRt.anchorMax = new Vector2(0.96f, anchorTop);
        cardRt.offsetMin = Vector2.zero;
        cardRt.offsetMax = Vector2.zero;
        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.14f, 0.14f, 0.2f, 0.95f);

        // Üst renkli şerit — kartın üst %2'si
        GameObject stripe = new GameObject("Stripe");
        stripe.transform.SetParent(card.transform, false);
        RectTransform stripeRt = stripe.AddComponent<RectTransform>();
        stripeRt.anchorMin = new Vector2(0, 0.98f);
        stripeRt.anchorMax = Vector2.one;
        stripeRt.offsetMin = Vector2.zero;
        stripeRt.offsetMax = Vector2.zero;
        stripe.AddComponent<Image>().color = weaponColor;

        // --- ÜST BÖLGE: İkon + İsim + Açıklama (%98 - %60 arası) ---

        // İkon — sol üst köşe
        GameObject icon = new GameObject("Icon");
        icon.transform.SetParent(card.transform, false);
        RectTransform iconRt = icon.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.02f, 0.72f);
        iconRt.anchorMax = new Vector2(0.12f, 0.95f);
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;
        icon.AddComponent<Image>().color = weaponColor;

        // İkon harfi
        TextMeshProUGUI iconLetter = CreateText(icon.transform, "Letter",
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            weaponName.Substring(0, 1), 28, TextAlignmentOptions.Center);
        iconLetter.rectTransform.anchorMin = Vector2.zero;
        iconLetter.rectTransform.anchorMax = Vector2.one;
        iconLetter.rectTransform.offsetMin = Vector2.zero;
        iconLetter.rectTransform.offsetMax = Vector2.zero;
        iconLetter.fontStyle = FontStyles.Bold;
        iconLetter.color = Color.white;

        // Silah adı
        TextMeshProUGUI nameText = CreateText(card.transform, "Name",
            new Vector2(0.14f, 0.88f), new Vector2(0.98f, 0.97f),
            Vector2.zero, Vector2.zero,
            weaponName + "  -  " + className, 22, TextAlignmentOptions.Left);
        nameText.rectTransform.anchorMin = new Vector2(0.14f, 0.88f);
        nameText.rectTransform.anchorMax = new Vector2(0.98f, 0.97f);
        nameText.rectTransform.offsetMin = Vector2.zero;
        nameText.rectTransform.offsetMax = Vector2.zero;
        nameText.color = weaponColor;
        nameText.fontStyle = FontStyles.Bold;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 14;
        nameText.fontSizeMax = 24;

        // Açıklama
        TextMeshProUGUI descText = CreateText(card.transform, "Desc",
            new Vector2(0.14f, 0.74f), new Vector2(0.98f, 0.88f),
            Vector2.zero, Vector2.zero,
            desc, 15, TextAlignmentOptions.Left);
        descText.rectTransform.anchorMin = new Vector2(0.14f, 0.74f);
        descText.rectTransform.anchorMax = new Vector2(0.98f, 0.88f);
        descText.rectTransform.offsetMin = Vector2.zero;
        descText.rectTransform.offsetMax = Vector2.zero;
        descText.color = new Color(0.6f, 0.6f, 0.65f);
        descText.enableAutoSizing = true;
        descText.fontSizeMin = 10;
        descText.fontSizeMax = 16;

        // --- STAT ÇUBUKLARI — %20 - %70 arası, 4 satır eşit dağıtım ---
        string[] statNames = { "Hasar", "Menzil", "Hiz", "Can" };
        Color[] statColors = {
            new Color(1f, 0.35f, 0.3f),
            new Color(0.3f, 0.8f, 1f),
            new Color(1f, 0.85f, 0.2f),
            new Color(0.3f, 0.9f, 0.4f)
        };

        float statTop = 0.70f;
        float statBot = 0.22f;
        float statH = (statTop - statBot) / 4f; // her stat ~%12
        float statGapY = 0.005f;

        for (int i = 0; i < 4; i++)
        {
            float sTop = statTop - i * statH;
            float sBot = sTop - statH + statGapY;
            CreateStatBarAnchored(card.transform, statNames[i], sBot, sTop, stats[i], statColors[i]);
        }

        // --- SEÇ BUTONU — alt %18 ---
        GameObject btn = new GameObject("SelectBtn");
        btn.transform.SetParent(card.transform, false);
        RectTransform btnRt = btn.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.04f, 0.03f);
        btnRt.anchorMax = new Vector2(0.96f, 0.18f);
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = Vector2.zero;
        Image btnImg = btn.AddComponent<Image>();
        btnImg.color = weaponColor;
        Button btnComp = btn.AddComponent<Button>();
        btnComp.targetGraphic = btnImg;

        TextMeshProUGUI btnText = CreateText(btn.transform, "BtnText",
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            "SEC", 20, TextAlignmentOptions.Center);
        btnText.rectTransform.anchorMin = Vector2.zero;
        btnText.rectTransform.anchorMax = Vector2.one;
        btnText.rectTransform.offsetMin = Vector2.zero;
        btnText.rectTransform.offsetMax = Vector2.zero;
        btnText.enableAutoSizing = true;
        btnText.fontSizeMin = 14;
        btnText.fontSizeMax = 24;

        btnComp.onClick.AddListener(() => OnWeaponSelected(weapon));
    }

    /// <summary>
    /// Anchor-bazlı stat çubuğu
    /// </summary>
    private void CreateStatBarAnchored(Transform parent, string label,
        float anchorBot, float anchorTop, float fillPercent, Color barColor)
    {
        // Etiket — sol %2 - %15
        TextMeshProUGUI labelText = CreateText(parent, label + "Label",
            new Vector2(0.02f, anchorBot), new Vector2(0.16f, anchorTop),
            Vector2.zero, Vector2.zero,
            label, 14, TextAlignmentOptions.Left);
        labelText.rectTransform.anchorMin = new Vector2(0.02f, anchorBot);
        labelText.rectTransform.anchorMax = new Vector2(0.16f, anchorTop);
        labelText.rectTransform.offsetMin = Vector2.zero;
        labelText.rectTransform.offsetMax = Vector2.zero;
        labelText.color = new Color(0.7f, 0.7f, 0.75f);
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 10;
        labelText.fontSizeMax = 16;

        // Çubuk arka plan — %17 - %98
        GameObject barBg = new GameObject(label + "BarBg");
        barBg.transform.SetParent(parent, false);
        RectTransform barBgRt = barBg.AddComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0.17f, anchorBot + 0.01f);
        barBgRt.anchorMax = new Vector2(0.98f, anchorTop - 0.01f);
        barBgRt.offsetMin = Vector2.zero;
        barBgRt.offsetMax = Vector2.zero;
        barBg.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

        // Dolu kısım
        GameObject fill = new GameObject(label + "Fill");
        fill.transform.SetParent(barBg.transform, false);
        RectTransform fillRt = fill.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(fillPercent, 1f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = barColor;
    }

    private void OnWeaponSelected(WeaponType weapon)
    {
        Debug.Log($"[BattleSceneSetup] Silah seçildi: {weapon}");

        // Yeni oyun başlat
        GameManager.Instance.StartNewGame(weapon);

        // Silah seçim ekranını kaldır
        if (weaponSelectPanel != null)
            Destroy(weaponSelectPanel.transform.root.gameObject);

        // Savaşı başlat
        StartBattle();
    }

    // ==================== SAVAŞ BAŞLATMA ====================

    private void StartBattle()
    {
        if (battleStarted) return;
        battleStarted = true;

        // BattleManager oluştur (sahneyi de kuracak)
        if (FindFirstObjectByType<BattleManager>() == null)
        {
            GameObject bmObj = new GameObject("BattleManager");
            bmObj.AddComponent<BattleManager>();
        }

        // Savaş UI oluştur
        SetupBattleUI();

        Debug.Log("[BattleSceneSetup] Savaş sahnesi hazır!");
    }

    private void SetupBattleUI()
    {
        // Canvas oluştur
        GameObject canvasObj = new GameObject("BattleCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 1f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // ===== ÜST BİLGİ ÇUBUĞU — %93 - %100 =====
        GameObject topBar = CreatePanel(canvasObj.transform, "TopBar",
            new Vector2(0, 0.93f), new Vector2(1, 1),
            Vector2.zero, Vector2.zero,
            new Color(0.05f, 0.05f, 0.1f, 0.85f));

        // Alt çizgi
        CreatePanel(topBar.transform, "BarLine",
            new Vector2(0, 0), new Vector2(1, 0.03f),
            Vector2.zero, Vector2.zero,
            new Color(0.8f, 0.2f, 0.2f, 0.8f));

        // Level yazısı — sol üst
        TextMeshProUGUI levelText = CreateText(topBar.transform, "LevelText",
            new Vector2(0.02f, 0.55f), new Vector2(0.35f, 0.95f),
            Vector2.zero, Vector2.zero,
            "Level 1", 24, TextAlignmentOptions.Left);

        // Dalga yazısı — orta
        TextMeshProUGUI waveText = CreateText(topBar.transform, "WaveText",
            new Vector2(0.35f, 0.55f), new Vector2(0.65f, 0.95f),
            Vector2.zero, Vector2.zero,
            "Dalga 1/3", 24, TextAlignmentOptions.Center);

        // Düşman sayısı — sağ üst
        TextMeshProUGUI enemyText = CreateText(topBar.transform, "EnemyText",
            new Vector2(0.65f, 0.55f), new Vector2(0.98f, 0.95f),
            Vector2.zero, Vector2.zero,
            "Dusman: 0", 22, TextAlignmentOptions.Right);

        // Altın — alt satır
        TextMeshProUGUI goldText = CreateText(topBar.transform, "GoldText",
            new Vector2(0.02f, 0.08f), new Vector2(0.98f, 0.50f),
            Vector2.zero, Vector2.zero,
            "Altin: +0", 18, TextAlignmentOptions.Center);
        goldText.color = new Color(1f, 0.85f, 0.2f);

        // ===== KAHRAMAN CAN BARI — sol alt =====
        GameObject heroBarBg = CreatePanel(canvasObj.transform, "HeroBarBg",
            new Vector2(0.02f, 0.02f), new Vector2(0.48f, 0.065f),
            Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0.7f));

        TextMeshProUGUI heroLabel = CreateText(heroBarBg.transform, "HeroLabel",
            new Vector2(0, 1), new Vector2(1, 1.6f),
            Vector2.zero, Vector2.zero,
            "Savasci", 14, TextAlignmentOptions.Left);
        heroLabel.color = Color.cyan;

        Slider heroHealthBar = CreateHealthBar(heroBarBg.transform, "HeroHealthBar",
            new Vector2(5, 5), new Vector2(280, 25),
            Color.green);

        TextMeshProUGUI heroHpText = CreateText(heroBarBg.transform, "HeroHpText",
            new Vector2(0, 0), new Vector2(1, 1),
            Vector2.zero, Vector2.zero,
            "100/100", 14, TextAlignmentOptions.Center);

        // ===== ANA ÜS CAN BARI — sağ alt =====
        GameObject baseBarBg = CreatePanel(canvasObj.transform, "BaseBarBg",
            new Vector2(0.52f, 0.02f), new Vector2(0.98f, 0.065f),
            Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0.7f));

        TextMeshProUGUI baseLabel = CreateText(baseBarBg.transform, "BaseLabel",
            new Vector2(0, 1), new Vector2(1, 1.6f),
            Vector2.zero, Vector2.zero,
            "Ana Us", 14, TextAlignmentOptions.Left);
        baseLabel.color = Color.yellow;

        Slider baseHealthBar = CreateHealthBar(baseBarBg.transform, "BaseHealthBar",
            new Vector2(5, 5), new Vector2(280, 25),
            Color.yellow);

        TextMeshProUGUI baseHpText = CreateText(baseBarBg.transform, "BaseHpText",
            new Vector2(0, 0), new Vector2(1, 1),
            Vector2.zero, Vector2.zero,
            "200/200", 14, TextAlignmentOptions.Center);

        // ===== JOYSTICK (sag alt) =====
        CreateVirtualJoystick(canvasObj.transform);

        // ===== SAVAŞ SONU PANELİ =====
        // Tam ekran koyu arka plan (savaş alanını gizler)
        GameObject endPanel = CreatePanel(canvasObj.transform, "BattleEndPanel",
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            new Color(0.03f, 0.03f, 0.06f, 0.85f));
        endPanel.SetActive(false);

        // İç panel (bilgi kutusu)
        GameObject endInner = CreatePanel(endPanel.transform, "EndInner",
            new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.72f),
            Vector2.zero, Vector2.zero,
            new Color(0.08f, 0.08f, 0.12f, 0.95f));

        // Üst kenar çizgi
        CreatePanel(endInner.transform, "EndBorder",
            new Vector2(0, 0.97f), new Vector2(1, 1),
            Vector2.zero, Vector2.zero,
            new Color(1f, 0.85f, 0.2f, 0.8f));

        TextMeshProUGUI resultText = CreateText(endInner.transform, "ResultText",
            new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.95f),
            Vector2.zero, Vector2.zero,
            "ZAFER!", 42, TextAlignmentOptions.Center);
        resultText.color = Color.yellow;

        TextMeshProUGUI rewardText = CreateText(endInner.transform, "RewardText",
            new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.60f),
            Vector2.zero, Vector2.zero,
            "Kazanilan Altin: 0", 24, TextAlignmentOptions.Center);

        // Devam butonu
        GameObject btnObj = CreateButton(endInner.transform, "ReturnButton",
            new Vector2(0.15f, 0.06f), new Vector2(0.85f, 0.30f),
            Vector2.zero, Vector2.zero,
            "Devam Et", new Color(0.15f, 0.65f, 0.25f));

        // ===== BattleUI bileşenini ekle ve referansları ata =====
        BattleUI battleUI = canvasObj.AddComponent<BattleUI>();
        battleUI.SetReferences(
            waveText, enemyText, goldText, levelText,
            heroHealthBar, heroHpText,
            baseHealthBar, baseHpText,
            endPanel, resultText, rewardText,
            btnObj.GetComponent<Button>()
        );
    }

    private void CreateVirtualJoystick(Transform parent)
    {
        GameObject joystickRoot = new GameObject("VirtualJoystick");
        joystickRoot.transform.SetParent(parent, false);
        RectTransform rootRt = joystickRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.74f, 0.08f);
        rootRt.anchorMax = new Vector2(0.96f, 0.20f);
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Image bg = joystickRoot.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.14f);
        bg.raycastTarget = true;

        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(joystickRoot.transform, false);
        RectTransform handleRt = handleObj.AddComponent<RectTransform>();
        handleRt.anchorMin = new Vector2(0.5f, 0.5f);
        handleRt.anchorMax = new Vector2(0.5f, 0.5f);
        handleRt.sizeDelta = new Vector2(70f, 70f);
        handleRt.anchoredPosition = Vector2.zero;

        Image handleImg = handleObj.AddComponent<Image>();
        handleImg.color = new Color(1f, 1f, 1f, 0.4f);
        handleImg.raycastTarget = false;

        VirtualJoystick joystick = joystickRoot.AddComponent<VirtualJoystick>();
        joystick.Setup(handleRt, 80f);
    }

    // ==================== UI YARDIMCI FONKSİYONLARI ====================

    private GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        Image img = panel.AddComponent<Image>();
        img.color = color;

        return panel;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 size,
        string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;

        return tmp;
    }

    private Slider CreateHealthBar(Transform parent, string name,
        Vector2 position, Vector2 size, Color fillColor)
    {
        // Slider objesi
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent, false);

        RectTransform sliderRt = sliderObj.AddComponent<RectTransform>();
        sliderRt.anchorMin = new Vector2(0, 0);
        sliderRt.anchorMax = new Vector2(0, 0);
        sliderRt.anchoredPosition = position + size / 2f;
        sliderRt.sizeDelta = size;

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = 1;
        slider.interactable = false;

        // Background
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f);

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRt = fillArea.AddComponent<RectTransform>();
        fillAreaRt.anchorMin = Vector2.zero;
        fillAreaRt.anchorMax = Vector2.one;
        fillAreaRt.offsetMin = Vector2.zero;
        fillAreaRt.offsetMax = Vector2.zero;

        // Fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRt = fill.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = fillColor;

        slider.fillRect = fillRt;

        return slider;
    }

    private GameObject CreateButton(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        string text, Color bgColor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        // Buton yazısı
        TextMeshProUGUI btnText = CreateText(btnObj.transform, "Text",
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            text, 20, TextAlignmentOptions.Center);
        btnText.rectTransform.anchorMin = Vector2.zero;
        btnText.rectTransform.anchorMax = Vector2.one;
        btnText.rectTransform.offsetMin = Vector2.zero;
        btnText.rectTransform.offsetMax = Vector2.zero;

        return btnObj;
    }
}
