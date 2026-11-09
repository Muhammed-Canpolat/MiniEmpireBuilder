using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// İnşaatçılar paneli: her inşaatçının durumu ve sıradaki inşaatçıyı kiralama.
/// 2. inşaatçı Ana Üs 5'te altınla; 3.–5. elmasla (100/200/300). Kulübeye ya da üst bardaki
/// inşaatçı göstergesine dokununca açılır.
/// </summary>
public sealed class BuilderPanel : MonoBehaviour
{
    private static BuilderPanel open;

    private readonly TextMeshProUGUI[] rowStatus = new TextMeshProUGUI[Progression.MaxBuilders];
    private readonly Image[] rowBackground = new Image[Progression.MaxBuilders];
    private TextMeshProUGUI summary;
    private TextMeshProUGUI hireLabel;
    private Button hireButton;
    private float refreshTimer;

    public static bool IsOpen => open != null;

    public static void Show()
    {
        if (open != null) { open.Refresh(); return; }
        var parent = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea")
            ?? GameObject.Find("BaseCanvas")?.transform;
        if (parent == null) return;
        var root = ReferenceUiVisuals.Rect(parent, "BuilderPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        open = root.gameObject.AddComponent<BuilderPanel>();
        open.Build(root);
        open.Refresh();
        AudioManager.Instance?.PlaySfx("click");
    }

    public static void Hide()
    {
        if (open != null) Destroy(open.gameObject);
        open = null;
    }

    private void OnDestroy()
    {
        if (open == this) open = null;
    }

    private void Build(RectTransform root)
    {
        // Karartma: dışarı dokununca kapanır.
        var dim = ReferenceUiVisuals.Image(root, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.55f));
        dim.raycastTarget = true;
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Hide);

        var card = ReferenceUiVisuals.Image(root, "Card", new Vector2(0.06f, 0.24f), new Vector2(0.94f, 0.76f),
            Vector2.zero, Vector2.zero, Color.white);
        card.sprite = ReferenceUiVisuals.ToastSprite();
        card.type = Image.Type.Sliced;
        card.raycastTarget = true;
        var cardRect = card.rectTransform;

        ReferenceUiVisuals.Text(cardRect, "Title", "İNŞAATÇILAR", 22, ReferenceUiVisuals.Gold,
            TextAlignmentOptions.Center, new Vector2(0f, 0.88f), new Vector2(1f, 0.98f),
            new Vector2(40f, 0f), new Vector2(-40f, 0f), title: true);
        summary = ReferenceUiVisuals.Text(cardRect, "Summary", "", 14, ReferenceUiVisuals.Muted,
            TextAlignmentOptions.Center, new Vector2(0f, 0.81f), new Vector2(1f, 0.88f),
            new Vector2(16f, 0f), new Vector2(-16f, 0f), bodyBold: true);

        var close = ReferenceUiVisuals.Button(cardRect, "CloseBtn", "X", false,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-46f, -46f), new Vector2(-8f, -8f), 16);
        close.onClick.AddListener(Hide);

        const float top = 0.79f, rowHeight = 0.095f;
        for (int i = 0; i < Progression.MaxBuilders; i++)
        {
            float yMax = top - i * rowHeight;
            var row = ReferenceUiVisuals.Image(cardRect, "Row_" + (i + 1), new Vector2(0.05f, yMax - rowHeight + 0.012f),
                new Vector2(0.95f, yMax), Vector2.zero, Vector2.zero, Color.white);
            row.sprite = ReferenceUiVisuals.PillSprite();
            row.type = Image.Type.Sliced;
            rowBackground[i] = row;
            ReferenceUiVisuals.Text(row.rectTransform, "Name", $"{i + 1}. İnşaatçı", 14, ReferenceUiVisuals.Light,
                TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(0.42f, 1f),
                new Vector2(18f, 0f), Vector2.zero, bodyBold: true);
            rowStatus[i] = ReferenceUiVisuals.Text(row.rectTransform, "Status", "", 13, ReferenceUiVisuals.Muted,
                TextAlignmentOptions.Right, new Vector2(0.40f, 0f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(-18f, 0f));
        }

        hireButton = ReferenceUiVisuals.Button(cardRect, "HireBtn", "", true,
            new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.23f), Vector2.zero, Vector2.zero, 15);
        hireLabel = hireButton.GetComponentInChildren<TextMeshProUGUI>();
        hireButton.onClick.AddListener(OnHire);
        ReferenceUiVisuals.Text(cardRect, "Note",
            "Kulübeyi basılı tutup sürükleyerek taşıyabilirsin", 12, ReferenceUiVisuals.Muted,
            TextAlignmentOptions.Center, new Vector2(0f, 0.02f), new Vector2(1f, 0.11f),
            new Vector2(16f, 0f), new Vector2(-16f, 0f));
    }

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = 0.25f;
        Refresh();
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        int total = gm.BuilderCount;
        summary.text = $"Boşta {gm.FreeBuilderCount} / {total}  ·  Elmas: {gm.Gems}";

        for (int i = 0; i < Progression.MaxBuilders; i++)
        {
            var status = rowStatus[i];
            if (i < total)
            {
                var task = gm.TaskForBuilder(i);
                status.text = task == null ? "<color=#9FD37E>Boşta</color>"
                    : $"{ShortLabel(task)} · {Progression.FormatDuration(GameManager.SecondsRemaining(task))}";
                rowBackground[i].color = Color.white;
            }
            else
            {
                var cost = Progression.BuilderCost(i + 1);
                string price = cost.gold > 0 ? $"{cost.gold} altın" : $"{cost.gems} elmas";
                status.text = i + 1 == 2
                    ? $"Ana Üs Sev. {Progression.SecondBuilderMainBaseLevel} · {price}"
                    : $"Kilitli · {price}";
                rowBackground[i].color = new Color(1f, 1f, 1f, 0.55f);
            }
        }

        var block = gm.CanHireBuilder();
        var next = gm.NextBuilderCost;
        string nextPrice = next.gold > 0 ? $"{next.gold} ALTIN" : $"{next.gems} ELMAS";
        switch (block)
        {
            case GameManager.HireBlock.MaxReached:
                hireLabel.text = "TÜM İNŞAATÇILAR HAZIR";
                break;
            case GameManager.HireBlock.MainBaseLevel:
                hireLabel.text = $"ANA ÜS SEV. {Progression.SecondBuilderMainBaseLevel} GEREKLİ";
                break;
            case GameManager.HireBlock.NoGold:
            case GameManager.HireBlock.NoGems:
                hireLabel.text = $"{total + 1}. İNŞAATÇI · <color=#8A2A1A>{nextPrice}</color>";
                break;
            default:
                hireLabel.text = $"{total + 1}. İNŞAATÇI · {nextPrice}";
                break;
        }
        bool canPress = block == GameManager.HireBlock.None || block == GameManager.HireBlock.NoGold
            || block == GameManager.HireBlock.NoGems;
        hireButton.interactable = canPress;
        hireButton.GetComponent<Image>().color = block == GameManager.HireBlock.None
            ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.85f);
    }

    private static string ShortLabel(BuilderTaskSave task)
    {
        if (task.kind == "remove") return "Temizlik";
        return string.IsNullOrEmpty(task.label) ? "Çalışıyor" : task.label;
    }

    private void OnHire()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var block = gm.CanHireBuilder();
        var setup = Object.FindFirstObjectByType<BaseSceneSetup>();
        if (block == GameManager.HireBlock.NoGold)
        {
            setup?.ShowHudToast("Yeterli altın yok");
            return;
        }
        if (block == GameManager.HireBlock.NoGems)
        {
            setup?.ShowHudToast("Yeterli elmas yok: giriş ödülleri ve mağazadan elmas kazanılır");
            return;
        }
        if (!gm.HireBuilder()) return;
        setup?.ShowHudToast($"{gm.BuilderCount}. inşaatçı geldi! Kulübesini basılı tutarak taşıyabilirsin");
        Refresh();
    }
}
