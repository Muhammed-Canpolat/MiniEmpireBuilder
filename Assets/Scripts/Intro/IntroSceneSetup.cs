using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;

/// <summary>
/// Oyunun giriş sahnesi — atmosferik intro, parallax arka plan, logo, typewriter hikaye, OYNA butonu
/// MainMenuScene'de boş bir GameObject'e eklenir.
///
/// Animasyon sırası:
/// 1. Parallax katmanlar fade-in (0.5s)
/// 2. Logo scale 0→1 (0.8s, OutBack)
/// 3. Kale sprite aşağıdan yukarı (0.5s, OutQuart)
/// 4. Typewriter metni başlar
/// 5. Metin bittikten sonra OYNA butonu fade-in
/// </summary>
public class IntroSceneSetup : MonoBehaviour
{
    // ==================== RENKLER ====================
    private static readonly Color Gold      = new Color32(0xFF, 0xD7, 0x00, 0xFF); // #FFD700
    private static readonly Color DarkBg   = new Color32(0x0A, 0x0A, 0x0F, 0xFF); // #0a0a0f
    private static readonly Color TextGray = new Color32(0x88, 0x88, 0x99, 0xFF);

    // ── Referanslar ─────────────────────────────────────────────────────────
    private Canvas        mainCanvas;
    private CanvasGroup   parallaxCG;
    private RectTransform logoRt;
    private RectTransform castleRt;
    private TextMeshProUGUI typewriterText;
    private CanvasGroup   playBtnCG;
    private CanvasGroup   newGameBtnCG;   // Sadece kayıt varsa dolu olur

    // Parallax katmanları
    private RectTransform[] parallaxLayers;
    private float[]          parallaxSpeeds;
    private float[]          parallaxStartX;

    // ==================== BOOTSTRAP ====================

    private void Awake()
    {
        if (GameManager.Instance == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            gmObj.AddComponent<GameManager>();
        }
        if (SpriteManager.Instance == null)
        {
            GameObject smObj = new GameObject("SpriteManager");
            smObj.AddComponent<SpriteManager>();
        }
    }

    private void Start()
    {
        // Kamera — koyu sade arka plan, ortografik
        Camera.main.orthographic    = true;
        Camera.main.orthographicSize = 6f;
        Camera.main.backgroundColor = DarkBg;

        // EventSystem
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        BuildUI();
        StartCoroutine(PlayIntroSequence());
    }

    // ==================== UI KURULUMU ====================

    private void BuildUI()
    {
        // ── Canvas ──────────────────────────────────────────────────────────
        GameObject canvasObj = new GameObject("IntroCanvas");
        mainCanvas = canvasObj.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mainCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight  = 1f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // ── Tam ekran koyu arka plan ─────────────────────────────────────────
        CreatePanel(canvasObj.transform, "DarkBg",
            Vector2.zero, Vector2.one, DarkBg).GetComponent<Image>().raycastTarget = false;

        // ── PARALLAX BÖLGE (üst %30) ─────────────────────────────────────────
        BuildParallaxSection(canvasObj.transform);

        // ── ORTA BÖLGE: Logo + Kale (y: %30 → %70) ─────────────────────────
        BuildLogoSection(canvasObj.transform);

        // ── HİKAYE METNİ (%20 → %38) ────────────────────────────────────────
        BuildNarrativeSection(canvasObj.transform);

        // ── OYNA BUTONU (alt %8) ─────────────────────────────────────────────
        BuildPlayButton(canvasObj.transform);

        // ── VERSİYON ETİKETİ (sağ alt) ──────────────────────────────────────
        BuildVersionLabel(canvasObj.transform);
    }

    // ──────────────────────────────────────────────────────────────────────
    // PARALLAX BÖLGE
    // ──────────────────────────────────────────────────────────────────────

    private void BuildParallaxSection(Transform canvasRoot)
    {
        // Çerçeve container (üst %30)
        GameObject container = new GameObject("ParallaxContainer");
        container.transform.SetParent(canvasRoot, false);
        RectTransform crt = container.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 0.70f);
        crt.anchorMax = Vector2.one;
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;

        // CanvasGroup — fade-in için
        parallaxCG = container.AddComponent<CanvasGroup>();
        parallaxCG.alpha = 0f;

        // Maske: taşan parallax kısımlarını kesmek için
        container.AddComponent<Image>().color = Color.clear;
        container.AddComponent<Mask>().showMaskGraphic = false;

        // Katman renkleri ve hızları (uzaktan yakına)
        Color[] layerColors = {
            new Color(0.04f, 0.05f, 0.09f), // Uzak dağ silueti
            new Color(0.05f, 0.08f, 0.06f), // Orman
            new Color(0.07f, 0.06f, 0.05f), // Ön plan kayalar
        };
        float[] yAnchors   = { 0.35f, 0.10f, 0.00f }; // her katmanın alt yüzdesi
        float[] yTopAnchors = { 1.00f, 0.50f, 0.25f };

        // Hızlar (piksel/saniye cinsinden referans alanda)
        parallaxSpeeds = new float[] { 30f, 60f, 120f };
        parallaxLayers  = new RectTransform[layerColors.Length];
        parallaxStartX  = new float[layerColors.Length];

        for (int i = 0; i < layerColors.Length; i++)
        {
            string lName = i == 0 ? "Layer_Mountains" : i == 1 ? "Layer_Forest" : "Layer_Rocks";
            Sprite spr = GetParallaxSprite(i);

            GameObject layer = new GameObject(lName);
            layer.transform.SetParent(container.transform, false);
            RectTransform lrt = layer.AddComponent<RectTransform>();

            // Genişliği 120% (parallax kayması için taşacak)
            lrt.anchorMin = new Vector2(-0.10f, yAnchors[i]);
            lrt.anchorMax = new Vector2( 1.10f, yTopAnchors[i]);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            Image img = layer.AddComponent<Image>();
            img.color          = layerColors[i];
            img.sprite         = spr;
            img.type           = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = 0.15f;
            img.raycastTarget  = false;

            parallaxLayers[i] = lrt;
            parallaxStartX[i] = 0f;
        }

        // Parallax silüet şekil detayları (çentikli profil — üçgenimsi)
        BuildMountainSilhouette(container.transform);
    }

    /// <summary>
    /// Parallax katmanı için uygun sprite seç ya da düz renk kullan
    /// </summary>
    private Sprite GetParallaxSprite(int layerIndex)
    {
        var sm = SpriteManager.Instance;
        if (sm == null) return null;
        // Mevcut sprite'larla dağ/orman/kaya temsil edilir
        switch (layerIndex)
        {
            case 0: return sm.Rock;
            case 1: return sm.Tree;
            case 2: return sm.Rock;
            default: return null;
        }
    }

    /// <summary>
    /// Dağ silueti efekti için birkaç karanlık dikdörtgen katmanlar üzerine eklenir
    /// </summary>
    private void BuildMountainSilhouette(Transform parent)
    {
        // Basit üçgen profil simülasyonu — yatay bantlar
        Color[] peakColors = {
            new Color(0.03f, 0.04f, 0.07f, 0.9f),
            new Color(0.04f, 0.06f, 0.05f, 0.85f),
            new Color(0.06f, 0.05f, 0.04f, 0.8f),
        };
        float[] peakBots = { 0.55f, 0.25f, 0.05f };
        float[] peakTops = { 1.00f, 0.60f, 0.30f };
        float[] xOffsets = { -0.05f, 0.02f, -0.03f };

        for (int i = 0; i < peakColors.Length; i++)
        {
            GameObject peak = new GameObject($"Peak{i}");
            peak.transform.SetParent(parent, false);
            RectTransform prt = peak.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(xOffsets[i], peakBots[i]);
            prt.anchorMax = new Vector2(1 + xOffsets[i], peakTops[i]);
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            Image pImg = peak.AddComponent<Image>();
            pImg.color = peakColors[i];
            pImg.raycastTarget = false;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // LOGO + KALE
    // ──────────────────────────────────────────────────────────────────────

    private void BuildLogoSection(Transform canvasRoot)
    {
        // Container: orta %40 (y: 0.30 → 0.70)
        GameObject mid = new GameObject("MidSection");
        mid.transform.SetParent(canvasRoot, false);
        RectTransform mrt = mid.AddComponent<RectTransform>();
        mrt.anchorMin = new Vector2(0, 0.42f);
        mrt.anchorMax = new Vector2(1, 0.72f);
        mrt.offsetMin = Vector2.zero;
        mrt.offsetMax = Vector2.zero;

        // ── Logo metni (üst %45) ────────────────────────────────
        GameObject logoObj = new GameObject("Logo");
        logoObj.transform.SetParent(mid.transform, false);
        logoRt = logoObj.AddComponent<RectTransform>();
        logoRt.anchorMin = new Vector2(0.05f, 0.62f);
        logoRt.anchorMax = new Vector2(0.95f, 1.00f);
        logoRt.offsetMin = Vector2.zero;
        logoRt.offsetMax = Vector2.zero;
        logoRt.localScale = Vector3.zero; // Başlangıç: gizli (scale animasyonu)

        TextMeshProUGUI logoTmp = logoObj.AddComponent<TextMeshProUGUI>();
        logoTmp.text              = "MINI EMPIRE\nBUILDER";
        logoTmp.fontSize          = 72f;
        logoTmp.alignment         = TextAlignmentOptions.Center;
        logoTmp.color             = Gold;
        logoTmp.fontStyle         = FontStyles.Bold;
        logoTmp.characterSpacing  = 8f;  // Geniş letter spacing
        logoTmp.enableAutoSizing  = true;
        logoTmp.fontSizeMin       = 36f;
        logoTmp.fontSizeMax       = 80f;

        // Glow efekti — Outline component
        Outline logoOutline = logoObj.AddComponent<Outline>();
        logoOutline.effectColor    = new Color(1f, 0.85f, 0f, 0.7f);
        logoOutline.effectDistance = new Vector2(3f, -3f);

        // ── Kale sprite (alt %55) ───────────────────────────────
        GameObject castleObj = new GameObject("Castle");
        castleObj.transform.SetParent(mid.transform, false);
        castleRt = castleObj.AddComponent<RectTransform>();
        castleRt.anchorMin = new Vector2(0.30f, 0.00f);
        castleRt.anchorMax = new Vector2(0.70f, 0.55f);
        castleRt.offsetMin = Vector2.zero;
        castleRt.offsetMax = Vector2.zero;

        Image castleImg = castleObj.AddComponent<Image>();
        var sm = SpriteManager.Instance;
        if (sm != null && sm.MainBase != null)
        {
            castleImg.sprite            = sm.MainBase;
            castleImg.color             = new Color(0.9f, 0.85f, 0.7f, 1f);
            castleImg.preserveAspect    = true;
        }
        else
        {
            castleImg.color = new Color(0.7f, 0.65f, 0.3f, 1f);
        }

        // Kale başlangıçta ekran dışı aşağıda (animasyon için)
        castleRt.anchoredPosition = new Vector2(0f, -120f);

        // ── Altın dekoratif çizgi ───────────────────────────────
        GameObject divider = new GameObject("GoldDivider");
        divider.transform.SetParent(mid.transform, false);
        RectTransform drt = divider.AddComponent<RectTransform>();
        drt.anchorMin = new Vector2(0.15f, 0.585f);
        drt.anchorMax = new Vector2(0.85f, 0.600f);
        drt.offsetMin = Vector2.zero;
        drt.offsetMax = Vector2.zero;
        divider.AddComponent<Image>().color = new Color(1f, 0.85f, 0.2f, 0.6f);
    }

    // ──────────────────────────────────────────────────────────────────────
    // HİKAYE / TYPEWRITER
    // ──────────────────────────────────────────────────────────────────────

    private void BuildNarrativeSection(Transform canvasRoot)
    {
        // Hafif karartılmış kutu
        GameObject narrativeBg = CreatePanel(canvasRoot, "NarrativeBg",
            new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.42f),
            new Color(0f, 0f, 0f, 0.45f));
        narrativeBg.GetComponent<Image>().raycastTarget = false;

        // Metin
        GameObject txtObj = new GameObject("TypewriterText");
        txtObj.transform.SetParent(canvasRoot, false);
        RectTransform trt = txtObj.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.08f, 0.23f);
        trt.anchorMax = new Vector2(0.92f, 0.41f);
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        typewriterText = txtObj.AddComponent<TextMeshProUGUI>();
        typewriterText.text            = "";
        typewriterText.fontSize        = 28f;
        typewriterText.alignment       = TextAlignmentOptions.Top;
        typewriterText.color           = new Color(0.88f, 0.88f, 0.94f, 1f);
        typewriterText.enableAutoSizing = true;
        typewriterText.fontSizeMin     = 14f;
        typewriterText.fontSizeMax     = 30f;
    }

    // ──────────────────────────────────────────────────────────────────────
    // OYNA BUTONU
    // ──────────────────────────────────────────────────────────────────────

    private void BuildPlayButton(Transform canvasRoot)
    {
        bool hasSave = SaveSystem.HasSave();

        // Kayıt varsa: OYNA (devam) y:0.09→0.17 + YENİ OYUN y:0.01→0.08
        // Kayıt yoksa: sadece OYNA (hero select) y:0.04→0.16
        float playTop = hasSave ? 0.17f : 0.16f;
        float playBot = hasSave ? 0.09f : 0.04f;

        // ── OYNA Butonu ────────────────────────────────────────────────────
        GameObject btnContainer = new GameObject("PlayBtnContainer");
        btnContainer.transform.SetParent(canvasRoot, false);
        RectTransform bcrt = btnContainer.AddComponent<RectTransform>();
        bcrt.anchorMin = new Vector2(0.12f, playBot);
        bcrt.anchorMax = new Vector2(0.88f, playTop);
        bcrt.offsetMin = Vector2.zero;
        bcrt.offsetMax = Vector2.zero;

        playBtnCG             = btnContainer.AddComponent<CanvasGroup>();
        playBtnCG.alpha       = 0f;
        playBtnCG.interactable   = false;
        playBtnCG.blocksRaycasts = false;

        // Dış border
        GameObject border = new GameObject("Border");
        border.transform.SetParent(btnContainer.transform, false);
        RectTransform brdRt = border.AddComponent<RectTransform>();
        brdRt.anchorMin = new Vector2(-0.01f, -0.05f);
        brdRt.anchorMax = new Vector2( 1.01f,  1.05f);
        brdRt.offsetMin = Vector2.zero;
        brdRt.offsetMax = Vector2.zero;
        border.AddComponent<Image>().color = new Color(0.4f, 0.3f, 0.0f, 1f);

        // Ana buton
        GameObject btnObj = new GameObject("PlayBtn");
        btnObj.transform.SetParent(btnContainer.transform, false);
        RectTransform btnRt = btnObj.AddComponent<RectTransform>();
        btnRt.anchorMin = Vector2.zero;
        btnRt.anchorMax = Vector2.one;
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = Vector2.zero;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = Gold;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        ColorBlock cb = btn.colors;
        cb.normalColor      = Gold;
        cb.highlightedColor = Gold * 1.1f;
        cb.pressedColor     = Gold * 0.8f;
        cb.selectedColor    = Gold;
        btn.colors = cb;
        // Kayıt varsa DEVAM ET, yoksa HeroSelect
        btn.onClick.AddListener(hasSave ? (UnityEngine.Events.UnityAction)OnContinueClicked
                                        : (UnityEngine.Events.UnityAction)OnNewGameClicked);

        // Buton metni
        GameObject txtObj = new GameObject("BtnText");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform trt = txtObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        TextMeshProUGUI btnTmp = txtObj.AddComponent<TextMeshProUGUI>();
        btnTmp.text             = hasSave ? "DEVAM ET" : "OYNA";
        btnTmp.fontSize         = 50f;
        btnTmp.alignment        = TextAlignmentOptions.Center;
        btnTmp.color            = DarkBg;
        btnTmp.fontStyle        = FontStyles.Bold;
        btnTmp.characterSpacing = 5f;

        // Pulse animasyonu
        btnObj.transform.DOScale(1.05f, 0.7f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetDelay(0.5f);

        // ── YENİ OYUN Butonu (sadece kayıt varsa göster) ───────────────────
        if (hasSave)
        {
            GameObject newContainer = new GameObject("NewGameBtnContainer");
            newContainer.transform.SetParent(canvasRoot, false);
            RectTransform ncrt = newContainer.AddComponent<RectTransform>();
            ncrt.anchorMin = new Vector2(0.20f, 0.01f);
            ncrt.anchorMax = new Vector2(0.80f, 0.08f);
            ncrt.offsetMin = Vector2.zero;
            ncrt.offsetMax = Vector2.zero;

            // Bu container da aynı playBtnCG ile fade-in yapacak ama
            // kendi CanvasGroup'u olmadığı için playBtnCG parent değil —
            // ayrı CanvasGroup ekle ve aynı anda açılsın
            CanvasGroup newCG = newContainer.AddComponent<CanvasGroup>();
            newCG.alpha          = 0f;
            newCG.interactable   = false;
            newCG.blocksRaycasts = false;

            // Referansı kaydet (PlayIntroSequence'de birlikte açılacak)
            newGameBtnCG = newCG;

            GameObject newBtnObj = new GameObject("NewGameBtn");
            newBtnObj.transform.SetParent(newContainer.transform, false);
            RectTransform nbrt = newBtnObj.AddComponent<RectTransform>();
            nbrt.anchorMin = Vector2.zero;
            nbrt.anchorMax = Vector2.one;
            nbrt.offsetMin = Vector2.zero;
            nbrt.offsetMax = Vector2.zero;

            Image newImg = newBtnObj.AddComponent<Image>();
            Color newBtnColor = new Color(0.22f, 0.22f, 0.30f, 1f);
            newImg.color = newBtnColor;

            Button newBtn = newBtnObj.AddComponent<Button>();
            newBtn.targetGraphic = newImg;
            ColorBlock ncb = newBtn.colors;
            ncb.normalColor      = newBtnColor;
            ncb.highlightedColor = newBtnColor + new Color(0.08f, 0.08f, 0.08f);
            ncb.pressedColor     = newBtnColor - new Color(0.05f, 0.05f, 0.05f);
            ncb.selectedColor    = newBtnColor;
            newBtn.colors = ncb;
            newBtn.onClick.AddListener(OnNewGameClicked);

            // İnce üst çizgi (altın)
            GameObject stripe = new GameObject("Stripe");
            stripe.transform.SetParent(newBtnObj.transform, false);
            RectTransform srt = stripe.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0.88f);
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            stripe.AddComponent<Image>().color = new Color(0.5f, 0.42f, 0.0f, 0.7f);

            GameObject newTxtObj = new GameObject("NewBtnText");
            newTxtObj.transform.SetParent(newBtnObj.transform, false);
            RectTransform ntrt = newTxtObj.AddComponent<RectTransform>();
            ntrt.anchorMin = Vector2.zero;
            ntrt.anchorMax = Vector2.one;
            ntrt.offsetMin = Vector2.zero;
            ntrt.offsetMax = Vector2.zero;

            TextMeshProUGUI newTmp = newTxtObj.AddComponent<TextMeshProUGUI>();
            newTmp.text             = "YENİ OYUN";
            newTmp.fontSize         = 26f;
            newTmp.alignment        = TextAlignmentOptions.Center;
            newTmp.color            = new Color(0.75f, 0.75f, 0.85f, 1f);
            newTmp.fontStyle        = FontStyles.Bold;
            newTmp.characterSpacing = 3f;
            newTmp.enableAutoSizing = true;
            newTmp.fontSizeMin      = 14f;
            newTmp.fontSizeMax      = 28f;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // VERSİYON ETİKETİ
    // ──────────────────────────────────────────────────────────────────────

    private void BuildVersionLabel(Transform canvasRoot)
    {
        GameObject vObj = new GameObject("VersionLabel");
        vObj.transform.SetParent(canvasRoot, false);
        RectTransform vrt = vObj.AddComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.75f, 0.005f);
        vrt.anchorMax = new Vector2(0.99f, 0.040f);
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;

        TextMeshProUGUI vTmp = vObj.AddComponent<TextMeshProUGUI>();
        vTmp.text      = "v0.1";
        vTmp.fontSize  = 18f;
        vTmp.alignment = TextAlignmentOptions.Right;
        vTmp.color     = TextGray;
    }

    // ──────────────────────────────────────────────────────────────────────
    // ANİMASYON SIRASI
    // ──────────────────────────────────────────────────────────────────────

    private IEnumerator PlayIntroSequence()
    {
        // 1. Arka plan katmanları fade-in (0.5s)
        yield return parallaxCG.DOFade(1f, 0.5f).WaitForCompletion();

        // 2. Logo scale 0 → 1 (0.8s, OutBack)
        yield return logoRt.DOScale(Vector3.one, 0.8f)
            .SetEase(Ease.OutBack)
            .WaitForCompletion();

        // 3. Kale aşağıdan yukarı (0.5s, OutQuart)
        yield return castleRt.DOAnchorPosY(0f, 0.5f)
            .SetEase(Ease.OutQuart)
            .WaitForCompletion();

        // 4. Typewriter metni
        string fullText = "Karanlik bir cagda imparatorlugun yikildi...\n\nYikintilar arasinda tek umut: sen.";
        yield return StartCoroutine(TypewriterEffect(fullText, 0.05f));

        // 5. OYNA / DEVAM ET butonu fade-in (0.6s)
        playBtnCG.interactable   = true;
        playBtnCG.blocksRaycasts = true;
        playBtnCG.DOFade(1f, 0.6f);

        // YENİ OYUN butonu da aynı anda açılsın (kayıt varsa)
        if (newGameBtnCG != null)
        {
            newGameBtnCG.interactable   = true;
            newGameBtnCG.blocksRaycasts = true;
            newGameBtnCG.DOFade(1f, 0.6f);
        }

        yield return new WaitForSeconds(0.6f);
    }

    private IEnumerator TypewriterEffect(string text, float delay)
    {
        typewriterText.text = "";
        foreach (char c in text)
        {
            typewriterText.text += c;
            yield return new WaitForSeconds(delay);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // PARALLAX UPDATE
    // ──────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (parallaxLayers == null) return;

        float dt = Time.deltaTime;
        for (int i = 0; i < parallaxLayers.Length; i++)
        {
            if (parallaxLayers[i] == null) continue;

            // Yatay kayış
            parallaxStartX[i] -= parallaxSpeeds[i] * dt;

            // Sonsuz loop: referans genişlik kadar kayınca sıfırla
            if (parallaxStartX[i] < -200f)
                parallaxStartX[i] = 0f;

            Vector2 cur = parallaxLayers[i].anchoredPosition;
            parallaxLayers[i].anchoredPosition = new Vector2(parallaxStartX[i], cur.y);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // BUTON DAVRANIŞI
    // ──────────────────────────────────────────────────────────────────────

    private void OnContinueClicked()
    {
        Debug.Log("[Intro] Devam → BaseScene");
        GameManager.Instance.LoadGame();
        GameManager.Instance.LoadBaseWorld();
    }

    private void OnNewGameClicked()
    {
        Debug.Log("[Intro] Yeni Oyun → Kahraman Seçim Paneli (Aynı Sahnede)");
        ShowWeaponSelection();
    }

    // ──────────────────────────────────────────────────────────────────────
    // SİLAH SEÇİM (OYNA → kayıt yoksa)
    // ──────────────────────────────────────────────────────────────────────

    private bool weaponSelectionLocked = false;
    private GameObject weaponPanel;
    private Image bgImage;
    private ScrollRect scrollRect;
    private RectTransform contentRt;

    private void ShowWeaponSelection()
    {
        if (playBtnCG != null)
        {
            playBtnCG.DOFade(0f, 0.25f);
            playBtnCG.interactable   = false;
            playBtnCG.blocksRaycasts = false;
        }

        BuildWeaponPanel();
        CanvasGroup wpCG = weaponPanel.GetComponent<CanvasGroup>();
        wpCG.alpha = 0f;
        weaponPanel.SetActive(true);
        wpCG.DOFade(1f, 0.35f);
    }

    private void BuildWeaponPanel()
    {
        weaponPanel = new GameObject("WeaponSelectPanel");
        weaponPanel.transform.SetParent(mainCanvas.transform, false);

        RectTransform wpRt = weaponPanel.AddComponent<RectTransform>();
        wpRt.anchorMin = Vector2.zero;
        wpRt.anchorMax = Vector2.one;
        wpRt.offsetMin = Vector2.zero;
        wpRt.offsetMax = Vector2.zero;

        bgImage = weaponPanel.AddComponent<Image>();
        bgImage.color = new Color32(0x0d, 0x0d, 0x1a, 0xFF); // #0d0d1a
        weaponPanel.AddComponent<CanvasGroup>();

        // Üst Altın Border
        GameObject border = new GameObject("TopBorder");
        border.transform.SetParent(weaponPanel.transform, false);
        RectTransform brt = border.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0, 0.99f);
        brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        border.AddComponent<Image>().color = Gold;

        // Başlık
        TextMeshProUGUI title = CreateText(weaponPanel.transform, "WTitle",
            new Vector2(0.05f, 0.92f), new Vector2(0.95f, 0.97f),
            "KAHRAMANINI SEÇ", 48, TextAlignmentOptions.Center, Gold);
        title.fontStyle = FontStyles.Bold;

        // Açıklama
        CreateText(weaponPanel.transform, "Subtitle",
            new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.92f),
            "Her savaşçı farklı bir strateji sunar", 24, TextAlignmentOptions.Center, TextGray);

        // Scroll View Container
        GameObject scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(weaponPanel.transform, false);
        RectTransform srt = scrollObj.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.05f, 0.05f);
        srt.anchorMax = new Vector2(0.95f, 0.86f);
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;
        scrollObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        scrollObj.AddComponent<Mask>().showMaskGraphic = false;

        scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 50f;

        // Content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollObj.transform, false);
        contentRt = contentObj.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1f);
        
        // Kart boyutu arttırıldı (550px)
        float cardHeight = 550f;
        float spacing = 40f;
        contentRt.sizeDelta = new Vector2(0, (cardHeight * 3) + (spacing * 4));
        scrollRect.content = contentRt;

        // Layout Grubu (childControlHeight = true EKLENDİ)
        UnityEngine.UI.VerticalLayoutGroup vlg = contentObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = spacing;
        vlg.padding = new RectOffset(20, 20, 20, 40);
        vlg.childControlHeight = true;  // <--- BURASI ÇOK ÖNEMLİ (Kartların ezilmesini önler)
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        // Kartları Oluştur
        CreateRichCard("AxeCard",   "BALTA",  "Tank",    new Color(0.9f, 0.3f, 0.2f), "Düşmanların arasına dalar, sert vurur!", new float[]{1f, 0.2f, 0.3f, 1f}, WeaponType.Axe, cardHeight);
        CreateRichCard("SpearCard", "MIZRAK", "Savunma", new Color(0.3f, 0.7f, 1f),   "Pozisyonunu korur, düşmanlarını engeller!", new float[]{0.5f,0.6f,0.4f,1f}, WeaponType.Spear, cardHeight);
        CreateRichCard("BowCard",   "OKÇU",   "Menzil",  new Color(0.2f, 0.85f, 0.4f), "Güvenli mesafeden yüksek hasar verir!", new float[]{0.35f,1f,0.8f,0.35f}, WeaponType.Bow, cardHeight);
    }

    private void CreateRichCard(string name, string heroName, string className, Color themeColor, string desc, float[] stats, WeaponType weapon, float height)
    {
        GameObject card = new GameObject(name);
        card.transform.SetParent(contentRt, false);
        RectTransform rt = card.AddComponent<RectTransform>();
        LayoutElement le = card.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;

        Image bg = card.AddComponent<Image>();
        bg.color = new Color32(0x1a, 0x1a, 0x2e, 0xFF); // #1a1a2e
        
        // Border efekti
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0); // Başta görünmez
        outline.effectDistance = new Vector2(4, -4);

        // ── Sol: Sprite (Daha büyük ve ortalanmış) ─────────────────────────
        GameObject spriteObj = new GameObject("HeroSprite");
        spriteObj.transform.SetParent(card.transform, false);
        RectTransform srt = spriteObj.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.02f, 0.20f);
        srt.anchorMax = new Vector2(0.35f, 0.90f);
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;

        Image img = spriteObj.AddComponent<Image>();
        img.preserveAspect = true;
        
        var sm = SpriteManager.Instance;
        Sprite hSprite = null;
        if (weapon == WeaponType.Axe) hSprite = sm.HeroAxe;
        else if (weapon == WeaponType.Spear) hSprite = sm.HeroSpear;
        else if (weapon == WeaponType.Bow) hSprite = sm.HeroBow;
        
        if (hSprite != null) {
            img.sprite = hSprite;
        } else {
            img.color = themeColor; // Placeholder
        }

        spriteObj.transform.DOScale(1.05f, 2f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

        // ── Sağ: Metinler ──────────────────────────────────────────────────────
        CreateText(card.transform, "Name", new Vector2(0.38f, 0.82f), new Vector2(0.95f, 0.95f), heroName, 42, TextAlignmentOptions.BottomLeft, Color.white).fontStyle = FontStyles.Bold;
        CreateText(card.transform, "Class", new Vector2(0.38f, 0.70f), new Vector2(0.95f, 0.82f), className, 28, TextAlignmentOptions.TopLeft, themeColor).fontStyle = FontStyles.Bold;
        CreateText(card.transform, "Desc", new Vector2(0.38f, 0.55f), new Vector2(0.95f, 0.70f), desc, 22, TextAlignmentOptions.TopLeft, TextGray);

        // ── 4 Stat Barı ──────────────────────────────────────────────────────
        string[] statNames = { "Hasar", "Menzil", "Hız", "Can" };
        float barStart = 0.52f;
        float barH = 0.08f;
        for (int i = 0; i < 4; i++)
        {
            float top = barStart - (i * barH);
            float bot = top - (barH * 0.75f);
            
            CreateText(card.transform, "Stat" + i, new Vector2(0.38f, bot), new Vector2(0.55f, top), statNames[i], 18, TextAlignmentOptions.Left, TextGray);
            GameObject barBg = CreatePanel(card.transform, "BarBg", new Vector2(0.55f, bot + 0.01f), new Vector2(0.85f, top - 0.01f), new Color(0.1f, 0.1f, 0.15f));
            CreatePanel(barBg.transform, "BarFill", Vector2.zero, new Vector2(stats[i], 1f), themeColor);
            CreateText(card.transform, "StatVal", new Vector2(0.87f, bot), new Vector2(0.98f, top), Mathf.RoundToInt(stats[i] * 10).ToString(), 18, TextAlignmentOptions.Right, Color.white);
        }

        // ── Alt: SEÇ Butonu ──────────────────────────────────────────────────
        GameObject btnObj = new GameObject("SelectBtn");
        btnObj.transform.SetParent(card.transform, false);
        RectTransform brt = btnObj.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.05f, 0.04f);
        brt.anchorMax = new Vector2(0.95f, 0.16f);
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = themeColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        
        CreateText(btnObj.transform, "BtnTxt", Vector2.zero, Vector2.one, "SEÇ", 36, TextAlignmentOptions.Center, Color.white).fontStyle = FontStyles.Bold;

        btn.onClick.AddListener(() => OnHeroCardClicked(card.transform, weapon, outline, themeColor, btnObj.transform));
    }

    private void OnHeroCardClicked(Transform cardTransform, WeaponType weapon, Outline outline, Color themeColor, Transform btnTransform)
    {
        if (weaponSelectionLocked) return;
        weaponSelectionLocked = true;

        // Arka Plan Rengi Geçişi
        Color bgColor = new Color(0.05f, 0.05f, 0.1f);
        if (weapon == WeaponType.Axe) bgColor = new Color32(0x1a, 0x0a, 0x0a, 0xFF); // #1a0a0a
        else if (weapon == WeaponType.Spear) bgColor = new Color32(0x0a, 0x0a, 0x1a, 0xFF); // #0a0a1a
        else if (weapon == WeaponType.Bow) bgColor = new Color32(0x0a, 0x1a, 0x0a, 0xFF); // #0a1a0a

        bgImage.DOColor(bgColor, 0.5f);

        // Diğer kartları karart
        foreach (Transform child in contentRt)
        {
            if (child != cardTransform)
            {
                CanvasGroup cg = child.gameObject.GetComponent<CanvasGroup>();
                if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();
                cg.DOFade(0.4f, 0.3f);
            }
        }

        // Seçilen kart animasyonları
        cardTransform.DOScale(1.03f, 0.3f);
        outline.effectColor = themeColor; // Border parlar
        
        // Buton animasyonu
        btnTransform.DOPunchScale(new Vector3(0.1f, 0.1f, 0f), 0.3f, 5, 0.5f);

        StartCoroutine(ConfirmWeapon(weapon));
    }

    private IEnumerator ConfirmWeapon(WeaponType weapon)
    {
        yield return new WaitForSeconds(0.6f);
        SaveSystem.DeleteSave();
        GameManager.Instance.StartNewGame(weapon);
    }

    // ──────────────────────────────────────────────────────────────────────
    // YARDIMCI METODLAR
    // ──────────────────────────────────────────────────────────────────────

    private GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        obj.AddComponent<Image>().color = color;
        return obj;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        string text, float fontSize, TextAlignmentOptions align, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text             = text;
        tmp.fontSize         = fontSize;
        tmp.alignment        = align;
        tmp.color            = color;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin      = 10f;
        tmp.fontSizeMax      = fontSize;
        return tmp;
    }

    private Sprite CreateSquareSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[]   px  = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }
}
