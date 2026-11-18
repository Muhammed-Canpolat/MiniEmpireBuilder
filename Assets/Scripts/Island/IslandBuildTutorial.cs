using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 6. oturum (3.2): adanın ilk açılışında ZORUNLU Okçuluk Alanı öğreticisi. Adımlar <see cref="IslandSaveData.buildTutorial"/>'da:
/// 1 İNŞA ET'e bas → 2 Okçuluk Alanı'nı seç → 3 yerleştir → 4 inşa sürüyor → 5 bitti (okçu binadan çıkıp atış yerine yürür).
/// Karartma ekranın hedef dışındaki her yerini kapatır (diğer düğmeler basılamaz), hedefin çevresi açık kalır; ok + kısa açıklama.
/// Okçuluk Alanı'nın ilk yapımı ücretsizdir (<see cref="IslandEconomy.IsFirstBuildFree"/>). Eski (girilmiş) adada adım 5 (göç).
/// </summary>
public sealed class IslandBuildTutorial : MonoBehaviour
{
    public const int Done = 5;
    public static IslandBuildTutorial Current { get; private set; }

    private IslandHud hud;
    private Canvas canvas;
    private RectTransform root, arrow;
    private readonly Image[] dims = new Image[4];
    private Image fullDim;
    private TextMeshProUGUI text;
    private RectTransform textBox;
    private Button action;
    private TextMeshProUGUI actionLabel;
    private float revealAt = -1f;

    public static int Step => IslandSession.Save != null ? IslandSession.Save.buildTutorial : Done;
    public string Text => text != null ? text.text : "";
    public Button ActionButton => action;

    /// <summary>Ada açılınca (tanıtım ve ad sorusundan sonra) çağrılır; gerekiyorsa öğreticiyi başlatır/sürdürür.</summary>
    public static void Resume(IslandHud hud)
    {
        var save = IslandSession.Save;
        if (save == null) return;
        if (save.buildTutorial >= Done) return;
        // Okçuluk Alanı zaten tamamlanmışsa (hazır köy, eski kayıt) öğretici gerekmez.
        if (IslandEconomy.HasArcheryReady(save)) { save.buildTutorial = Done; return; }
        if (save.buildTutorial == 0) save.buildTutorial = 1;
        if (Current != null) return;
        var go = new GameObject("IslandBuildTutorial", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var t = go.AddComponent<IslandBuildTutorial>();
        Current = t;
        t.hud = hud;
        t.Build();
    }

    private void Build()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 310;   // ada HUD'ının ve inşa menüsünün üstünde, ad/elmas pencerelerinin altında
        ReferenceUiVisuals.ConfigureCanvas(GetComponent<CanvasScaler>());
        root = (RectTransform)canvas.transform;
        var dimColor = new Color(0f, 0f, 0f, 0.62f);
        fullDim = ReferenceUiVisuals.Image(root, "FullDim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, dimColor);
        fullDim.raycastTarget = true;
        for (int i = 0; i < 4; i++)
        {
            dims[i] = ReferenceUiVisuals.Image(root, "Dim" + i, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, dimColor);
            dims[i].raycastTarget = true;
        }
        textBox = FancyInfoPanel.NineSlice(root, "TutorialText", new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.62f),
            new Vector2(0, 0), new Vector2(0, 112), "ui_paper", Color.white, 2.4f).rectTransform;
        text = ReferenceUiVisuals.Text(textBox, "Text", "", 15, FancyInfoPanel.Ink, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(18, 10), new Vector2(-18, -10), false, true);
        text.textWrappingMode = TextWrappingModes.Normal;
        arrow = TutorialPointer.CreateArrow(root);
        action = FancyInfoPanel.TsButton(root, "TutorialAction", "", false, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-90, -30), new Vector2(90, 30), 18, out actionLabel);
        action.onClick.AddListener(OnAction);
    }

    private void OnAction()
    {
        var save = IslandSession.Save;
        if (save == null) return;
        if (save.buildTutorial == 1 && hud != null) { hud.OpenBuildMenu(); save.buildTutorial = 2; }
        else if (save.buildTutorial == Done) Destroy(gameObject);
    }

    private void Update() => Tick();

    /// <summary>Adımı oyun durumundan ilerletir ve görünümü kurar (testler/ekran görüntüsü öncesi de çağrılır).</summary>
    public void Tick()
    {
        var save = IslandSession.Save;
        if (save == null) { Destroy(gameObject); return; }
        bool archeryPlaced = IslandEconomy.CountOf(IslandLayout.BuildingKind.Archery) > 0;
        bool archeryReady = IslandEconomy.HasArcheryReady(save);
        var ui = hud != null ? hud.BuildUi : null;
        // Adım ilerletme (oyun durumundan)
        if (save.buildTutorial < 4 && archeryPlaced) save.buildTutorial = 4;
        if (save.buildTutorial == 4 && archeryReady)
        {
            save.buildTutorial = Done;
            GameManager.Instance?.SaveGame();
            IslandSceneSetup.Instance?.RevealArcher();
            revealAt = Time.unscaledTime;
        }
        if (save.buildTutorial == 2 && !(ui != null && ui.MenuOpen) && !(hud != null && hud.Placing)) save.buildTutorial = 1;   // menü kapatıldı
        if (save.buildTutorial == 2 && hud != null && hud.Placing) save.buildTutorial = 3;
        if (save.buildTutorial == 3 && hud != null && !hud.Placing && !archeryPlaced) save.buildTutorial = 1;   // yerleştirme iptal

        // Görünüm
        switch (save.buildTutorial)
        {
            case 1:
                Show(true, null, L.T("Akıncılar kıyıya çıkmadan okçu yetiştirmelisin. İlk iş: Okçuluk Alanı kur (ilk yapım ÜCRETSİZ).",
                    "Train archers before raiders land. First task: build an Archery Range (first build is FREE)."), L.T("İNŞA ET", "BUILD"));
                break;
            case 2:
                var rect = ui != null ? ui.MenuCardRect(IslandLayout.BuildingKind.Archery) : null;
                Show(false, rect, L.T("Okçuluk Alanı'nı seç.", "Pick the Archery Range."), null);
                break;
            case 3:
                Show(false, null, L.T("Okçuluk Alanı'nı kalenin yakınına yerleştir ve yeşil onay düğmesiyle onayla.", "Place the Archery Range near the keep and confirm with the green tick button."), null, passThrough: true);
                break;
            case 4:
                Show(false, null, L.T("Ada Ustası Okçuluk Alanı'nı kuruyor…", "The Island Builder is building the Archery Range…"), null, passThrough: true);
                break;
            default:
                if (revealAt < 0f) { Destroy(gameObject); return; }
                Show(false, null, L.T("Okçu Kaptan hazır! Akın'larda atış yerinden kıyıyı savun.", "Your Archer Captain is ready! Defend the shore from the firing posts in raids."),
                    L.T("TAMAM", "OK"), passThrough: true);
                if (Time.unscaledTime - revealAt > 8f) Destroy(gameObject);
                break;
        }
    }

    /// <summary>
    /// <paramref name="dimAll"/>: tüm ekran kararır (yalnız öğretici düğmesi basılır). <paramref name="hole"/>: hedef dışındaki her yer kararır.
    /// <paramref name="passThrough"/>: karartma yok, yalnız metin (yerleştirme / bekleme).
    /// </summary>
    private void Show(bool dimAll, RectTransform hole, string message, string actionText, bool passThrough = false)
    {
        text.text = message;
        fullDim.gameObject.SetActive(dimAll);
        bool holeOn = !dimAll && !passThrough && hole != null;
        foreach (var d in dims) d.gameObject.SetActive(holeOn);
        action.gameObject.SetActive(actionText != null);
        if (actionText != null) actionLabel.text = actionText;
        arrow.gameObject.SetActive(holeOn || dimAll);
        float bob = TutorialPointer.Bob();
        textBox.anchorMin = new Vector2(0.06f, passThrough ? 0.80f : 0.62f);
        textBox.anchorMax = new Vector2(0.94f, passThrough ? 0.80f : 0.62f);
        if (dimAll)
        {
            ((RectTransform)action.transform).anchoredPosition = new Vector2(0f, -40f);
            arrow.anchoredPosition = new Vector2(0f, 20f + bob);
        }
        else if (actionText != null) ((RectTransform)action.transform).anchoredPosition = new Vector2(0f, root.rect.height * 0.25f);
        if (!holeOn) return;
        // Hedefin ekran dikdörtgeni → kök yerel koordinatları; dört karartma şeridi deliği çevreler.
        var corners = new Vector3[4];
        hole.GetWorldCorners(corners);
        var holeCanvas = hole.GetComponentInParent<Canvas>()?.rootCanvas;
        var cam = holeCanvas == null || holeCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : holeCanvas.worldCamera;
        var myCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, a, myCam, out var la);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, b, myCam, out var lb);
        var r = root.rect;
        float pad = 6f;
        float x0 = la.x - pad, y0 = la.y - pad, x1 = lb.x + pad, y1 = lb.y + pad;
        Place(dims[0], r.xMin, y1, r.xMax, r.yMax);   // üst
        Place(dims[1], r.xMin, r.yMin, r.xMax, y0);   // alt
        Place(dims[2], r.xMin, y0, x0, y1);           // sol
        Place(dims[3], x1, y0, r.xMax, y1);           // sağ
        arrow.anchoredPosition = new Vector2((x0 + x1) * 0.5f, y1 + 26f + bob);
    }

    private void Place(Image img, float x0, float y0, float x1, float y1)
    {
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(x0, y0);
        rt.offsetMax = new Vector2(Mathf.Max(x0, x1), Mathf.Max(y0, y1));
    }

    private void OnDestroy() { if (Current == this) Current = null; }
}
