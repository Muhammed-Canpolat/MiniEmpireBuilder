using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>An empty battlefield socket. Entering its radius offers explicit build choices.</summary>
public class BattleBuildSlot : MonoBehaviour
{
    private BattleManager battleManager;
    private bool isBuilt;
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
        UpdateChoices();
        var screen = Camera.main.WorldToScreenPoint(hero.transform.position + Vector3.up * 2.3f);
        var anchor = new Vector2(Mathf.Clamp(screen.x / Screen.width, 168f / 390f, 1f - 168f / 390f),
            Mathf.Clamp(screen.y / Screen.height, 85f / 844f, 1f - 85f / 844f));
        choicePanel.anchorMin = choicePanel.anchorMax = anchor;
        choicePanel.anchoredPosition = Vector2.zero;
    }

    private void CreateChoicePanel()
    {
        choiceCanvas = new GameObject("BattleSlotChoiceCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = choiceCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;
        ReferenceUiVisuals.ConfigureCanvas(choiceCanvas.GetComponent<CanvasScaler>());
        choicePanel = ReferenceUiVisuals.Rect(choiceCanvas.transform, "SlotChoicePanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-168, -62), new Vector2(168, 62));
        var background = choicePanel.gameObject.AddComponent<Image>();
        background.color = ReferenceUiVisuals.Panel;
        var outline = choicePanel.gameObject.AddComponent<Outline>();
        outline.effectColor = ReferenceUiVisuals.Gold;
        outline.effectDistance = new Vector2(2, -2);
        for (int i = 0; i < choices.Length; i++)
        {
            int captured = i;
            choices[i] = ReferenceUiVisuals.Button(choicePanel, "BuildChoice" + i, "",
                false,
                new Vector2(0.02f + i * 0.33f, 0.08f),
                new Vector2(0.32f + i * 0.33f, 0.92f), Vector2.zero, Vector2.zero, 12);
            choices[i].onClick.AddListener(() => TryBuild(types[captured]));
        }
    }

    private void UpdateChoices()
    {
        for (int i = 0; i < choices.Length; i++)
        {
            var type = types[i];
            int cost = battleManager.GetBuildCost(type);
            int owned = battleManager.GetBattleBuildAllowance(type);
            int remaining = battleManager.GetBattleBuildRemaining(type);
            bool unlocked = GameManager.Instance != null
                && GameManager.Instance.Buildings.TryGetValue(type, out var data) && data.isUnlocked;
            choices[i].interactable = battleManager.CanBuildFromSlot(type, cost);
            var group = choices[i].GetComponent<CanvasGroup>();
            if (group == null) group = choices[i].gameObject.AddComponent<CanvasGroup>();
            group.alpha = choices[i].interactable ? 1f : 0.5f;
            var text = choices[i].GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                string name = type == BuildingType.ArcherTower ? "OKÇU"
                    : type == BuildingType.CannonTower ? "TOPÇU" : "MANASTIR";
                int level = type == BuildingType.ArcherTower ? 2 : type == BuildingType.CannonTower ? 3 : 4;
                string instruction = owned == 0
                    ? $"Ana Üste {Name(type)} binası kur"
                    : remaining == 0 ? $"Ana Üste yeni {Name(type)} binası kur" : "";
                text.text = !unlocked ? $"SEV. {level}\n{remaining}/{owned}\n{instruction}"
                    : $"{name} {cost}\n{remaining}/{owned}\n{instruction}";
                text.fontSize = instruction.Length > 0 ? 10 : 13;
            }
        }
    }

    private static string Name(BuildingType type) => type == BuildingType.ArcherTower
        ? "Okçu" : type == BuildingType.CannonTower ? "Topçu" : "Manastır";

    private void TryBuild(BuildingType type)
    {
        int cost = battleManager.GetBuildCost(type);
        if (!battleManager.TryBuildFromSlot(this, type, transform.position, cost)) return;
        builtType = type;
        isBuilt = true;
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

    private void OnDestroy()
    {
        if (choiceCanvas != null) Destroy(choiceCanvas);
    }
}
