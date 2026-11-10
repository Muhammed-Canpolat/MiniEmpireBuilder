using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>An empty battlefield socket. Entering its radius offers explicit build choices.</summary>
public class BattleBuildSlot : MonoBehaviour
{
    private BattleManager battleManager;
    private bool isBuilt;
    private int destroyCount;
    private GameObject ruin;

    /// <summary>Bu yuvada kaç yapı yıkıldı; yeniden kurma bedeli her yıkılışta +%50.</summary>
    public int DestroyCount => destroyCount;
    public float CostMultiplier => 1f + 0.5f * destroyCount;
    public int CostFor(BuildingType type) =>
        Mathf.RoundToInt(battleManager.GetBuildCost(type) * CostMultiplier);
    private BuildingType builtType;
    private GameObject visualMarker;
    private Vector3 markerRestScale;
    private GameObject choiceCanvas;
    private RectTransform choicePanel;
    private readonly Button[] choices = new Button[3];
    private readonly BuildingType[] types =
        { BuildingType.ArcherTower, BuildingType.CannonTower, BuildingType.Monastery };

    public bool IsBuilt => isBuilt;
    public BuildingType BuiltType => builtType;

    public void Setup(BattleManager manager, BuildingType initialType, int initialCost)
    {
        battleManager = manager;
    }

    private void Start()
    {
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = false;
        visualMarker = BattleWorldMarkers.DashedCircle(transform, 0.95f,
            new Color(0.94f, 0.76f, 0.29f, 0.9f), 22);
        markerRestScale = visualMarker.transform.localScale;
        BattleWorldMarkers.SlotLabel(visualMarker.transform, battleManager);
        var trigger = GetComponent<CircleCollider2D>();
        if (trigger != null) trigger.radius = 1.05f;
    }

    private void Update()
    {
        if (isBuilt || AudioSettingsUI.BattleModalOpen || battleManager == null || !battleManager.IsBattleActive)
        {
            if (choiceCanvas != null) choiceCanvas.SetActive(false);
            return;
        }
        var hero = FindFirstObjectByType<HeroController>();
        if (hero != null && visualMarker != null)
        {
            float distance = Vector2.Distance(hero.transform.position, transform.position);
            float pulse = distance <= 3f
                ? 1f + 0.06f * (1f + Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 0.6f))
                : 1f;
            visualMarker.transform.localScale = markerRestScale * pulse;
        }
        bool near = hero != null && Vector2.Distance(hero.transform.position, transform.position) <= 1.35f;
        if (!near)
        {
            if (choiceCanvas != null) choiceCanvas.SetActive(false);
            return;
        }
        if (choiceCanvas == null) CreateChoicePanel();
        choiceCanvas.SetActive(true);
        int visible = UpdateChoices();
        bool infoOnly = visible == 0;
        choicePanel.gameObject.SetActive(!infoOnly);
        infoPill.gameObject.SetActive(infoOnly);
        if (infoOnly) infoText.text = InfoMessage();
        // Menü yuvanın üstünde durur; kahramanı ve yaklaşan düşmanı kapatmaz.
        RectTransform target = infoOnly ? infoPill : choicePanel;
        Vector2 half = target.sizeDelta * 0.5f;
        var screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.7f);
        var scaler = choiceCanvas.GetComponent<RectTransform>();
        float w = Mathf.Max(1f, scaler.rect.width), h = Mathf.Max(1f, scaler.rect.height);
        var anchor = new Vector2(
            Mathf.Clamp(screen.x / Screen.width, (half.x + 8f) / w, 1f - (half.x + 8f) / w),
            // Alt panel (~150) ve üst HUD (~160) ile çakışmasın; panelin tabanı çapada durur.
            Mathf.Clamp(screen.y / Screen.height, 150f / h, 1f - (half.y * 2f + 160f) / h));
        target.anchorMin = target.anchorMax = anchor;
        target.anchoredPosition = new Vector2(0f, half.y);
    }

    private const float ChoiceWidth = 104f;
    private const float ChoiceHeight = 84f;
    private const float ChoiceGap = 6f;
    private RectTransform infoPill;
    private TextMeshProUGUI infoText;
    private readonly TextMeshProUGUI[] choiceTexts = new TextMeshProUGUI[3];

    private void CreateChoicePanel()
    {
        choiceCanvas = new GameObject("BattleSlotChoiceCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = choiceCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;
        ReferenceUiVisuals.ConfigureCanvas(choiceCanvas.GetComponent<CanvasScaler>());
        choicePanel = ReferenceUiVisuals.Rect(choiceCanvas.transform, "SlotChoicePanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var background = choicePanel.gameObject.AddComponent<Image>();
        background.sprite = ReferenceUiVisuals.ToastSprite();
        background.type = Image.Type.Sliced;
        for (int i = 0; i < choices.Length; i++)
        {
            int captured = i;
            choices[i] = ReferenceUiVisuals.Button(choicePanel, "BuildChoice" + i, "",
                true, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, 13);
            choices[i].onClick.AddListener(() => TryBuild(types[captured]));
            choiceTexts[i] = choices[i].GetComponentInChildren<TextMeshProUGUI>();
            choiceTexts[i].characterSpacing = 0.5f;
            choiceTexts[i].lineSpacing = -8f;
        }

        // Kurulabilecek yapı yoksa: ekranı kapatmayan, dokunmayı engellemeyen bilgi balonu.
        infoPill = ReferenceUiVisuals.Rect(choiceCanvas.transform, "SlotInfoPill",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        infoPill.sizeDelta = new Vector2(250f, 50f);
        var pillImage = infoPill.gameObject.AddComponent<Image>();
        pillImage.sprite = ReferenceUiVisuals.ToastSprite();
        pillImage.type = Image.Type.Sliced;
        pillImage.raycastTarget = false;
        infoText = ReferenceUiVisuals.Text(infoPill, "Text", "",
            13, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -4f));
        infoPill.gameObject.SetActive(false);
    }

    /// <summary>Yalnız Ana Üs'te sahip olunan yapıları gösterir; görünen seçenek sayısını döndürür.</summary>
    private int UpdateChoices()
    {
        int visible = 0;
        for (int i = 0; i < choices.Length; i++)
        {
            var type = types[i];
            int owned = battleManager.GetBattleBuildAllowance(type);
            bool show = owned > 0;
            choices[i].gameObject.SetActive(show);
            if (!show) continue;

            int cost = CostFor(type);
            int remaining = battleManager.GetBattleBuildRemaining(type);
            bool canBuild = battleManager.CanBuildFromSlot(type, cost);
            choices[i].interactable = canBuild;
            var group = choices[i].GetComponent<CanvasGroup>();
            if (group == null) group = choices[i].gameObject.AddComponent<CanvasGroup>();
            group.alpha = canBuild ? 1f : 0.55f;

            string status = remaining <= 0 ? L.T("Hak kalmadı", "No builds left")
                : battleManager.GoldEarned < cost ? L.T("Altın yetersiz", "Not enough gold")
                : destroyCount > 0 ? "Yeniden kur"
                : $"{remaining}/{owned} hak";
            choiceTexts[i].text = UiStrings.Upper(Name(type)) +
                $"\n<size=16>{cost}</size> <size=11>ALTIN</size>\n<size=10>{UiStrings.Upper(status)}</size>";

            var rect = (RectTransform)choices[i].transform;
            float x = 8f + visible * (ChoiceWidth + ChoiceGap);
            rect.offsetMin = new Vector2(x, -ChoiceHeight * 0.5f);
            rect.offsetMax = new Vector2(x + ChoiceWidth, ChoiceHeight * 0.5f);
            visible++;
        }
        choicePanel.sizeDelta = new Vector2(16f + visible * ChoiceWidth + Mathf.Max(0, visible - 1) * ChoiceGap,
            ChoiceHeight + 16f);
        return visible;
    }

    private static string InfoMessage()
    {
        bool archerUnlocked = GameManager.Instance != null &&
            GameManager.Instance.Buildings.TryGetValue(BuildingType.ArcherTower, out var archer) &&
            archer.isUnlocked;
        return archerUnlocked
            ? L.T("Kule kurmak için önce\n<color=#F0C24A>Ana Üs'te Okçu binası</color> inşa et", "To build towers, first build\n<color=#F0C24A>an Archer at the Main Base</color>")
            : L.T("Kuleler <color=#F0C24A>Ana Üs Sev. 2</color> ile açılır", "Towers unlock at <color=#F0C24A>Main Base Lv. 2</color>");
    }

    private static string Name(BuildingType type) => type == BuildingType.ArcherTower
        ? L.T("Okçu", "Archer") : type == BuildingType.CannonTower ? L.T("Topçu", "Cannon") : L.T("Manastır", "Monastery");

    private void TryBuild(BuildingType type)
    {
        int cost = CostFor(type);
        if (!battleManager.TryBuildFromSlot(this, type, transform.position, cost)) return;
        builtType = type;
        isBuilt = true;
        if (ruin != null) Destroy(ruin);
        if (GameManager.Instance?.PlayerData != null &&
            !GameManager.Instance.PlayerData.firstBattleTowerBuilt)
        {
            GameManager.Instance.PlayerData.firstBattleTowerBuilt = true;
            GameManager.Instance.SaveGame();
            var hint = GameObject.Find("BattleBuildHint");
            if (hint != null) hint.SetActive(false);
        }
        if (visualMarker != null) visualMarker.SetActive(false);
        if (choiceCanvas != null) Destroy(choiceCanvas);
    }

    /// <summary>Üstündeki yapı yıkıldı: yuva yeniden kurulabilir, yerinde yıkıntı kalır.</summary>
    public void OnStructureDestroyed()
    {
        isBuilt = false;
        destroyCount++;
        if (visualMarker != null) visualMarker.SetActive(true);
        if (ruin != null) Destroy(ruin);
        ruin = CreateRuin(transform.position);
        // Enkazda ateş yanar; yapı yeniden kurulunca enkazla birlikte söner.
        TinyFx.Fire(ruin.transform.position + Vector3.up * 0.35f, ruin.transform);
    }

    /// <summary>Koyu yanık iz ve birkaç kırık taş.</summary>
    private static GameObject CreateRuin(Vector3 center)
    {
        var root = new GameObject("StructureRuin");
        root.transform.position = new Vector3(center.x, center.y - 0.35f, 0f);
        var scorch = new GameObject("Scorch");
        scorch.transform.SetParent(root.transform, false);
        var scorchRenderer = scorch.AddComponent<SpriteRenderer>();
        scorchRenderer.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        scorchRenderer.color = new Color(0.08f, 0.06f, 0.05f, 0.55f);
        scorchRenderer.sortingOrder = 2;
        Vector2 circle = scorchRenderer.sprite.bounds.size;
        scorch.transform.localScale = new Vector3(1.7f / circle.x, 0.7f / circle.y, 1f);
        var rock = Resources.Load<Sprite>("BaseDecor/rock");
        if (rock == null) return root;
        Vector3[] pieces = { new Vector3(-0.42f, 0.05f), new Vector3(0.36f, 0.12f), new Vector3(0.02f, -0.12f) };
        float[] sizes = { 0.42f, 0.34f, 0.28f };
        for (int i = 0; i < pieces.Length; i++)
        {
            var piece = new GameObject("Rubble");
            piece.transform.SetParent(root.transform, false);
            piece.transform.localPosition = pieces[i];
            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = rock;
            renderer.color = new Color(0.55f, 0.5f, 0.46f);
            renderer.sortingOrder = 3;
            piece.transform.localScale = Vector3.one * (sizes[i] / Mathf.Max(0.01f, rock.bounds.size.x));
        }
        return root;
    }

    private void OnDestroy()
    {
        if (choiceCanvas != null) Destroy(choiceCanvas);
        if (ruin != null) Destroy(ruin);
    }
}
