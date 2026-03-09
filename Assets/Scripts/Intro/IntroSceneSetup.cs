using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Oyunun başlangıç sahnesi — atmosferik giriş + kahraman seçimi
/// MainMenuScene'de boş bir GameObject'e eklenir
/// 
/// Akış:
/// 1. Karanlık yıkık binalarla çevrili Ana Üs manzarası
/// 2. Hikaye metni yavaşça belirir
/// 3. "Kahramanını Seç" ekranı açılır (silah kartları)
/// 4. Seçim sonrası → StartNewGame → BaseScene
/// 5. Kayıtlı oyun varsa "Devam Et" butonu da gösterilir
/// </summary>
public class IntroSceneSetup : MonoBehaviour
{
    // Aşamalar
    private enum IntroPhase { AtmosphericIntro, WeaponSelection }
    private IntroPhase currentPhase = IntroPhase.AtmosphericIntro;

    // UI referansları
    private Canvas mainCanvas;
    private GameObject introPanel;
    private GameObject weaponPanel;
    private TextMeshProUGUI narrativeText;
    private CanvasGroup narrativeCG;

    // Zamanlama
    private float introTimer = 0f;
    private bool introComplete = false;

    private void Awake()
    {
        // GameManager yoksa oluştur
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
        // Kamera ayarları — karanlık, kasvetli
        Camera.main.orthographic = true;
        Camera.main.orthographicSize = 7f;
        Camera.main.backgroundColor = new Color(0.04f, 0.06f, 0.08f); // Çok koyu gece

        // EventSystem
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // Atmosferik sahneyi kur
        CreateAtmosphericScene();

        // UI oluştur
        CreateIntroUI();
    }

    // ==================== ATMOSFERİK SAHNE ====================

    private void CreateAtmosphericScene()
    {
        var sm = SpriteManager.Instance;

        // Zemin — karanlık toprak
        CreateSceneSprite("Zemin", new Color(0.06f, 0.08f, 0.04f),
            new Vector3(0, -2.5f, 1), new Vector3(20f, 6f, 1));

        // === ANA ÜS — ortada, Tiny Swords Castle sprite ===
        if (sm != null)
        {
            Sprite castleSprite = sm.MainBase;
            GameObject mainBase = new GameObject("AnaUs");
            mainBase.transform.position = new Vector3(0, 0.5f, 0);
            mainBase.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            SpriteRenderer sr = mainBase.AddComponent<SpriteRenderer>();
            sr.sprite = castleSprite;
            sr.sortingOrder = 2;
            // Karanlık tint — harabe atmosferi
            sr.color = new Color(0.4f, 0.35f, 0.3f);
        }
        else
        {
            CreateSceneSprite("AnaUs", new Color(0.65f, 0.55f, 0.18f),
                new Vector3(0, 0, 0), new Vector3(1.8f, 2.0f, 1));
        }

        // Üs ışığı (parlama efekti — umut)
        CreateSceneSprite("UsIsiği", new Color(1f, 0.9f, 0.4f, 0.08f),
            new Vector3(0, 0.5f, 0.5f), new Vector3(4f, 4f, 1));

        // === YIKIK BİNALAR — gerçek sprite'larla karanlık harabe ===
        if (sm != null)
        {
            // Sol — yıkık kule (koyu, eğik)
            GameObject ruin1 = CreateDarkRuin(sm.ArcherTower, new Vector3(-4f, 1.5f, 0.1f), 0.35f, 12f);
            // Sağ — çökmüş yapı
            GameObject ruin2 = CreateDarkRuin(sm.CannonTower, new Vector3(4f, 1.0f, 0.1f), 0.3f, -8f);
            // Sol alt — yıkık ev
            GameObject ruin3 = CreateDarkRuin(sm.WallBuilder, new Vector3(-3f, -1.5f, 0.1f), 0.3f, 15f);
            // Sağ alt — yıkık ev
            GameObject ruin4 = CreateDarkRuin(sm.GoldMine, new Vector3(3.5f, -2f, 0.1f), 0.5f, -5f);
        }
        else
        {
            // Fallback — eski renkli kareler
            GameObject ruin1 = CreateSceneSprite("Yikik1", new Color(0.15f, 0.12f, 0.1f),
                new Vector3(-4.5f, 2.5f, 0), new Vector3(0.8f, 2f, 1));
            ruin1.transform.rotation = Quaternion.Euler(0, 0, 12f);
            GameObject ruin2 = CreateSceneSprite("Yikik2", new Color(0.13f, 0.11f, 0.1f),
                new Vector3(4f, 2f, 0), new Vector3(1.2f, 1.5f, 1));
            ruin2.transform.rotation = Quaternion.Euler(0, 0, -8f);
        }

        // Enkaz parçaları (küçük kareler — zemin dokusu)
        CreateSceneSprite("Enkaz1", new Color(0.1f, 0.08f, 0.06f),
            new Vector3(-3.8f, -0.3f, 0.05f), new Vector3(0.5f, 0.2f, 1));
        CreateSceneSprite("Enkaz2", new Color(0.08f, 0.07f, 0.05f),
            new Vector3(5f, -0.5f, 0.05f), new Vector3(0.6f, 0.25f, 1));
        CreateSceneSprite("Enkaz3", new Color(0.07f, 0.06f, 0.05f),
            new Vector3(1.5f, -2.8f, 0.05f), new Vector3(0.4f, 0.3f, 1));
        CreateSceneSprite("Enkaz4", new Color(0.09f, 0.07f, 0.06f),
            new Vector3(-1.5f, -3f, 0.05f), new Vector3(0.5f, 0.35f, 1));

        // Dekoratif ağaçlar — karanlık versiyonlar
        if (sm != null)
        {
            Sprite treeSpr = sm.Tree;
            GameObject t1 = CreateDarkDecor(treeSpr, new Vector3(-6f, 3f, 0.2f), 0.4f);
            GameObject t2 = CreateDarkDecor(treeSpr, new Vector3(6f, 2.5f, 0.2f), 0.35f);
            GameObject t3 = CreateDarkDecor(treeSpr, new Vector3(-5.5f, -1f, 0.2f), 0.3f);
            GameObject t4 = CreateDarkDecor(treeSpr, new Vector3(5.5f, -1.5f, 0.2f), 0.35f);

            // Kayalar
            Sprite rockSpr = sm.Rock;
            CreateDarkDecor(rockSpr, new Vector3(-2.5f, -2.5f, 0.15f), 0.6f);
            CreateDarkDecor(rockSpr, new Vector3(2f, -2.8f, 0.15f), 0.5f);
        }

        // === UZAK ARKAPLAN — sisli dağlar ===
        CreateSceneSprite("Dag1", new Color(0.04f, 0.06f, 0.08f),
            new Vector3(-3f, 5f, 2), new Vector3(6f, 4f, 1));
        CreateSceneSprite("Dag2", new Color(0.03f, 0.05f, 0.07f),
            new Vector3(3f, 5.5f, 2), new Vector3(5f, 3.5f, 1));

        // === PARLAYAN PARÇACIKLAR (basit ışık noktaları) ===
        for (int i = 0; i < 8; i++)
        {
            float x = Random.Range(-5f, 5f);
            float y = Random.Range(-1f, 4f);
            float size = Random.Range(0.05f, 0.12f);
            float alpha = Random.Range(0.15f, 0.4f);
            GameObject particle = CreateSceneSprite($"Isik{i}",
                new Color(1f, 0.9f, 0.5f, alpha),
                new Vector3(x, y, -0.5f), Vector3.one * size);
            StartCoroutine(FloatParticle(particle, Random.Range(0.2f, 0.5f)));
        }
    }

    /// <summary>
    /// Karanlık harabe sprite oluştur (koyu tintli, eğik)
    /// </summary>
    private GameObject CreateDarkRuin(Sprite sprite, Vector3 pos, float scale, float rotZ)
    {
        GameObject obj = new GameObject("Yikik");
        obj.transform.position = pos;
        obj.transform.localScale = new Vector3(scale, scale, 1f);
        obj.transform.rotation = Quaternion.Euler(0, 0, rotZ);
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0.2f, 0.18f, 0.15f); // Çok karanlık tint
        sr.sortingOrder = 1;
        return obj;
    }

    /// <summary>
    /// Karanlık dekorasyon sprite (ağaç, kaya — atmosfer için)
    /// </summary>
    private GameObject CreateDarkDecor(Sprite sprite, Vector3 pos, float scale)
    {
        GameObject obj = new GameObject("Decor");
        obj.transform.position = pos;
        obj.transform.localScale = new Vector3(scale, scale, 1f);
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0.15f, 0.15f, 0.12f); // Gece karanlığında siluet
        sr.sortingOrder = 0;
        return obj;
    }

    private IEnumerator FloatParticle(GameObject obj, float speed)
    {
        if (obj == null) yield break;

        float startY = obj.transform.position.y;
        float time = Random.Range(0f, 5f); // Farklı başlangıç fazları

        while (obj != null)
        {
            time += Time.deltaTime * speed;
            float newY = startY + Mathf.Sin(time) * 0.5f;
            float newX = obj.transform.position.x + Mathf.Cos(time * 0.7f) * Time.deltaTime * 0.1f;
            obj.transform.position = new Vector3(newX, newY, obj.transform.position.z);

            // Alpha animasyonu
            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Abs(Mathf.Sin(time * 0.5f)) * 0.4f + 0.1f;
                sr.color = c;
            }

            yield return null;
        }
    }

    // ==================== INTRO UI ====================

    private void CreateIntroUI()
    {
        // Canvas
        GameObject canvasObj = new GameObject("IntroCanvas");
        mainCanvas = canvasObj.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mainCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 1f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // ===== İNTRO PANELİ (atmosferik giriş metinleri) =====
        introPanel = new GameObject("IntroPanel");
        introPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform introPanelRt = introPanel.AddComponent<RectTransform>();
        introPanelRt.anchorMin = Vector2.zero;
        introPanelRt.anchorMax = Vector2.one;
        introPanelRt.offsetMin = Vector2.zero;
        introPanelRt.offsetMax = Vector2.zero;

        // Yarı saydam üst katman (metnin okunması için)
        Image introOverlay = introPanel.AddComponent<Image>();
        introOverlay.color = new Color(0, 0, 0, 0.4f);
        introOverlay.raycastTarget = false; // Tıklamaları engellemesin

        // Oyun başlığı — üst
        TextMeshProUGUI titleText = CreateAnchoredText(introPanel.transform, "Title",
            new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.92f),
            "MINI EMPIRE BUILDER", 38, TextAlignmentOptions.Center,
            new Color(1f, 0.85f, 0.2f));
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 20;
        titleText.fontSizeMax = 42;

        // Alt başlık
        CreateAnchoredText(introPanel.transform, "Subtitle",
            new Vector2(0.1f, 0.77f), new Vector2(0.9f, 0.82f),
            "Imparatorlugunu yeniden insa et", 18, TextAlignmentOptions.Center,
            new Color(0.6f, 0.65f, 0.7f));

        // Hikaye metni — ortada, yavaşça belirec
        narrativeText = CreateAnchoredText(introPanel.transform, "Narrative",
            new Vector2(0.08f, 0.45f), new Vector2(0.92f, 0.7f),
            "", 20, TextAlignmentOptions.Center,
            new Color(0.85f, 0.85f, 0.9f));
        narrativeText.enableAutoSizing = true;
        narrativeText.fontSizeMin = 14;
        narrativeText.fontSizeMax = 22;

        // CanvasGroup ile fade-in
        narrativeCG = narrativeText.gameObject.AddComponent<CanvasGroup>();
        narrativeCG.alpha = 0f;

        // "Dokunarak Basla" metni — altta, yanıp söner
        TextMeshProUGUI tapText = CreateAnchoredText(introPanel.transform, "TapToStart",
            new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.14f),
            "Dokunarak Basla", 22, TextAlignmentOptions.Center,
            new Color(0.8f, 0.8f, 0.85f));
        StartCoroutine(BlinkText(tapText));

        // Kayıtlı oyun varsa "Devam Et" butonu göster
        if (SaveSystem.HasSave())
        {
            GameObject continueBtn = CreateAnchoredButton(introPanel.transform, "ContinueBtn",
                new Vector2(0.25f, 0.15f), new Vector2(0.75f, 0.22f),
                "DEVAM ET", new Color(0.2f, 0.5f, 0.8f));

            continueBtn.GetComponent<Button>().onClick.AddListener(OnContinueClicked);

            // "Dokunarak Basla" metnini biraz aşağı al
            tapText.rectTransform.anchorMin = new Vector2(0.15f, 0.03f);
            tapText.rectTransform.anchorMax = new Vector2(0.85f, 0.09f);

            // "Yeni Oyun" etiketi ekle
            CreateAnchoredText(introPanel.transform, "NewGameLabel",
                new Vector2(0.15f, 0.09f), new Vector2(0.85f, 0.14f),
                "(Yeni Oyun icin dokun)", 15, TextAlignmentOptions.Center,
                new Color(0.5f, 0.5f, 0.55f));
        }

        // Hikaye anlatımını başlat
        StartCoroutine(PlayNarrative());

        // ===== SİLAH SEÇİM PANELİ (başlangıçta gizli) =====
        CreateWeaponSelectionPanel(canvasObj.transform);
    }

    // ==================== HİKAYE ANLATIMI ====================

    private IEnumerator PlayNarrative()
    {
        yield return new WaitForSeconds(0.5f);

        string[] lines = {
            "Karanlik bir cagda...",
            "Imparatorluk dusmanlar tarafindan\nyerle bir edildi.",
            "Yikintilarin arasinda\ntek bir yer ayakta kaldi...\n\nANA US.",
            "Simdi, bir kurtarici geldi.\n\nSen.",
            "Silahini sec ve\nimparatorlugunu yeniden kur!"
        };

        float[] durations = { 2.5f, 3f, 3.5f, 3f, 3f };

        for (int i = 0; i < lines.Length; i++)
        {
            narrativeText.text = lines[i];

            // Fade in
            float t = 0;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                narrativeCG.alpha = Mathf.Clamp01(t / 0.8f);
                yield return null;
            }
            narrativeCG.alpha = 1f;

            yield return new WaitForSeconds(durations[i]);

            // Fade out (son satır hariç)
            if (i < lines.Length - 1)
            {
                t = 0;
                while (t < 0.5f)
                {
                    t += Time.deltaTime;
                    narrativeCG.alpha = 1f - Mathf.Clamp01(t / 0.5f);
                    yield return null;
                }
                narrativeCG.alpha = 0f;
                yield return new WaitForSeconds(0.3f);
            }
        }

        // Otomatik olarak karakter seçme ekranına geçiş
        TransitionToWeaponSelect();
    }

    private IEnumerator BlinkText(TextMeshProUGUI text)
    {
        while (text != null)
        {
            float alpha = (Mathf.Sin(Time.time * 2f) + 1f) / 2f;
            alpha = Mathf.Lerp(0.3f, 1f, alpha);
            text.color = new Color(text.color.r, text.color.g, text.color.b, alpha);
            yield return null;
        }
    }

    // ==================== DOKUNMA / TIKKLAMA ====================

    private void Update()
    {
        if (currentPhase != IntroPhase.AtmosphericIntro) return;

        bool tapped = false;

        if (UnityEngine.InputSystem.Touchscreen.current != null &&
            UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            tapped = true;
        }
        else if (UnityEngine.InputSystem.Mouse.current != null &&
                 UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            tapped = true;
        }

        if (tapped && !introComplete)
        {
            TransitionToWeaponSelect();
        }
    }

    private void TransitionToWeaponSelect()
    {
        introComplete = true;
        currentPhase = IntroPhase.WeaponSelection;
        StartCoroutine(FadeToWeaponSelect());
    }

    private IEnumerator FadeToWeaponSelect()
    {
        // Intro panelini fade out
        CanvasGroup introCG = introPanel.GetComponent<CanvasGroup>();
        if (introCG == null) introCG = introPanel.AddComponent<CanvasGroup>();

        float t = 0;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            introCG.alpha = 1f - Mathf.Clamp01(t / 0.5f);
            yield return null;
        }
        introPanel.SetActive(false);

        // Silah seçim panelini göster
        weaponPanel.SetActive(true);
        CanvasGroup wpCG = weaponPanel.GetComponent<CanvasGroup>();
        if (wpCG == null) wpCG = weaponPanel.AddComponent<CanvasGroup>();
        wpCG.alpha = 0f;

        t = 0;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            wpCG.alpha = Mathf.Clamp01(t / 0.5f);
            yield return null;
        }
        wpCG.alpha = 1f;
    }

    // ==================== SİLAH SEÇİM PANELİ ====================

    private void CreateWeaponSelectionPanel(Transform canvasTransform)
    {
        weaponPanel = new GameObject("WeaponSelectPanel");
        weaponPanel.transform.SetParent(canvasTransform, false);
        RectTransform wpRt = weaponPanel.AddComponent<RectTransform>();
        wpRt.anchorMin = Vector2.zero;
        wpRt.anchorMax = Vector2.one;
        wpRt.offsetMin = Vector2.zero;
        wpRt.offsetMax = Vector2.zero;

        // Arka plan
        Image wpBg = weaponPanel.AddComponent<Image>();
        wpBg.color = new Color(0.06f, 0.07f, 0.1f, 0.95f);

        weaponPanel.SetActive(false); // Başlangıçta gizli

        // ===== BAŞLIK =====
        TextMeshProUGUI title = CreateAnchoredText(weaponPanel.transform, "WTitle",
            new Vector2(0.05f, 0.93f), new Vector2(0.95f, 0.98f),
            "KAHRAMANINI SEC", 36, TextAlignmentOptions.Center,
            new Color(1f, 0.85f, 0.2f));
        title.fontStyle = FontStyles.Bold;
        title.enableAutoSizing = true;
        title.fontSizeMin = 22;
        title.fontSizeMax = 38;

        CreateAnchoredText(weaponPanel.transform, "WSubtitle",
            new Vector2(0.1f, 0.90f), new Vector2(0.9f, 0.93f),
            "Her silah farkli bir strateji sunar", 16, TextAlignmentOptions.Center,
            new Color(0.5f, 0.5f, 0.6f));

        // ===== 3 SİLAH KARTI =====
        // Kartlar: %89 - %3 alan, 3 eşit bölüm, %1.5 ara boşluk
        float topPad = 0.89f;
        float botPad = 0.03f;
        float cardGap = 0.015f;
        float totalSpace = topPad - botPad - 2 * cardGap;
        float cardH = totalSpace / 3f;

        // Kart 1 — BALTA
        float c1Top = topPad;
        float c1Bot = c1Top - cardH;
        CreateWeaponCard(weaponPanel.transform, "AxeCard",
            c1Bot, c1Top,
            "BALTA", new Color(0.9f, 0.3f, 0.2f),
            "Tank Savasci",
            "Dusmanlarin icine dal, sert vur!\nYuksek hasar ve dayaniklilik.",
            new float[] { 1.0f, 0.2f, 0.3f, 1.0f },
            WeaponType.Axe);

        // Kart 2 — MIZRAK
        float c2Top = c1Bot - cardGap;
        float c2Bot = c2Top - cardH;
        CreateWeaponCard(weaponPanel.transform, "SpearCard",
            c2Bot, c2Top,
            "MIZRAK", new Color(0.3f, 0.7f, 1f),
            "Savunma Savascisi",
            "Pozisyon tut, dusmanlarini durdur!\nYuksek dayaniklilik.",
            new float[] { 0.5f, 0.6f, 0.4f, 1.0f },
            WeaponType.Spear);

        // Kart 3 — OK
        float c3Top = c2Bot - cardGap;
        float c3Bot = c3Top - cardH;
        CreateWeaponCard(weaponPanel.transform, "BowCard",
            c3Bot, c3Top,
            "OK", new Color(0.2f, 0.85f, 0.4f),
            "Okcu",
            "Guvenli mesafeden dusmanlarini vur!\nUzun menzil ve hiz.",
            new float[] { 0.35f, 1.0f, 0.8f, 0.35f },
            WeaponType.Bow);
    }

    private void CreateWeaponCard(Transform parent, string name,
        float anchorBot, float anchorTop,
        string weaponName, Color weaponColor,
        string className, string desc,
        float[] stats, WeaponType weapon)
    {
        // Kart arka planı — yatay %4 - %96
        GameObject card = new GameObject(name);
        card.transform.SetParent(parent, false);
        RectTransform cardRt = card.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.04f, anchorBot);
        cardRt.anchorMax = new Vector2(0.96f, anchorTop);
        cardRt.offsetMin = Vector2.zero;
        cardRt.offsetMax = Vector2.zero;
        card.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 0.95f);

        // Üst renkli şerit
        GameObject stripe = new GameObject("Stripe");
        stripe.transform.SetParent(card.transform, false);
        RectTransform stripeRt = stripe.AddComponent<RectTransform>();
        stripeRt.anchorMin = new Vector2(0, 0.98f);
        stripeRt.anchorMax = Vector2.one;
        stripeRt.offsetMin = Vector2.zero;
        stripeRt.offsetMax = Vector2.zero;
        stripe.AddComponent<Image>().color = weaponColor;

        // İkon — sol üst
        GameObject icon = new GameObject("Icon");
        icon.transform.SetParent(card.transform, false);
        RectTransform iconRt = icon.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.02f, 0.72f);
        iconRt.anchorMax = new Vector2(0.12f, 0.95f);
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;
        icon.AddComponent<Image>().color = weaponColor;

        // İkon harfi
        TextMeshProUGUI iconLetter = CreateAnchoredText(icon.transform, "Letter",
            Vector2.zero, Vector2.one,
            weaponName.Substring(0, 1), 28, TextAlignmentOptions.Center, Color.white);
        iconLetter.fontStyle = FontStyles.Bold;

        // Silah adı + sınıf
        TextMeshProUGUI nameText = CreateAnchoredText(card.transform, "Name",
            new Vector2(0.14f, 0.88f), new Vector2(0.98f, 0.97f),
            weaponName + "  -  " + className, 22, TextAlignmentOptions.Left, weaponColor);
        nameText.fontStyle = FontStyles.Bold;

        // Açıklama
        CreateAnchoredText(card.transform, "Desc",
            new Vector2(0.14f, 0.72f), new Vector2(0.98f, 0.88f),
            desc, 14, TextAlignmentOptions.Left, new Color(0.6f, 0.6f, 0.65f));

        // Stat çubukları — %22 - %70 arası, 4 satır
        string[] statNames = { "Hasar", "Menzil", "Hiz", "Can" };
        Color[] statColors = {
            new Color(1f, 0.35f, 0.3f),
            new Color(0.3f, 0.8f, 1f),
            new Color(1f, 0.85f, 0.2f),
            new Color(0.3f, 0.9f, 0.4f)
        };

        float statTop = 0.70f;
        float statBot = 0.22f;
        float statH = (statTop - statBot) / 4f;
        float statGapY = 0.005f;

        for (int i = 0; i < 4; i++)
        {
            float sTop = statTop - i * statH;
            float sBot = sTop - statH + statGapY;
            CreateStatBar(card.transform, statNames[i], sBot, sTop, stats[i], statColors[i]);
        }

        // SEÇ BUTONU — alt %18
        GameObject btn = CreateAnchoredButton(card.transform, "SelectBtn",
            new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.18f),
            "SEC", weaponColor);

        btn.GetComponent<Button>().onClick.AddListener(() => OnWeaponSelected(weapon));
    }

    private void CreateStatBar(Transform parent, string label,
        float anchorBot, float anchorTop, float fillPercent, Color barColor)
    {
        // Etiket — sol
        CreateAnchoredText(parent, label + "Label",
            new Vector2(0.02f, anchorBot), new Vector2(0.16f, anchorTop),
            label, 14, TextAlignmentOptions.Left, new Color(0.7f, 0.7f, 0.75f));

        // Çubuk arka plan
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

    // ==================== SİLAH SEÇİMİ ====================

    private void OnWeaponSelected(WeaponType weapon)
    {
        Debug.Log($"[IntroScene] Silah secildi: {weapon}");

        // Önceki kaydı sil (yeni oyun)
        SaveSystem.DeleteSave();

        // Yeni oyun başlat — bu otomatik olarak BaseScene'e geçer
        GameManager.Instance.StartNewGame(weapon);
    }

    private void OnContinueClicked()
    {
        Debug.Log("[IntroScene] Devam ediliyor...");
        GameManager.Instance.LoadGame();
        GameManager.Instance.LoadBaseWorld();
    }

    // ==================== YARDIMCI ====================

    private GameObject CreateSceneSprite(string name, Color color, Vector3 pos, Vector3 scale)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = pos;
        obj.transform.localScale = scale;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color = color;
        sr.sortingOrder = pos.z < 0 ? 5 : (pos.z > 1 ? -1 : 0); // z'ye göre katman

        return obj;
    }

    private Sprite CreateSquareSprite()
    {
        Texture2D tex = new Texture2D(32, 32);
        Color[] colors = new Color[32 * 32];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    private TextMeshProUGUI CreateAnchoredText(Transform parent, string name,
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
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 10;
        tmp.fontSizeMax = fontSize;
        return tmp;
    }

    private GameObject CreateAnchoredButton(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, string text, Color bgColor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        TextMeshProUGUI btnText = CreateAnchoredText(btnObj.transform, "Text",
            Vector2.zero, Vector2.one,
            text, 22, TextAlignmentOptions.Center, Color.white);
        btnText.fontStyle = FontStyles.Bold;

        return btnObj;
    }
}
