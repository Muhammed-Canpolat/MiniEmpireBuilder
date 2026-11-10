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
    private readonly TextMeshProUGUI[] rowName = new TextMeshProUGUI[Progression.MaxBuilders];
    private readonly Button[] rowFinish = new Button[Progression.MaxBuilders];
    private readonly TextMeshProUGUI[] rowFinishLabel = new TextMeshProUGUI[Progression.MaxBuilders];
    private readonly BuilderTaskSave[] rowTask = new BuilderTaskSave[Progression.MaxBuilders];
    private TextMeshProUGUI potionLabel;
    private TextMeshProUGUI guestLabel;
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
        FirstTimeTips.Show("builders", L.T(
            "Her inşaatçı aynı anda bir iş yapar. Süren işi elmasla ya da reklamla hızlandırabilir, İnşaat İksiri ile herkesi 4 kat hızlandırabilirsin.",
            "Each builder does one job at a time. Speed up jobs with gems or ads, or use a Construction Potion to make everyone 4x faster."));
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

        ReferenceUiVisuals.Text(cardRect, "Title", L.T("İNŞAATÇILAR", "BUILDERS"), 22, ReferenceUiVisuals.Gold,
            TextAlignmentOptions.Center, new Vector2(0f, 0.88f), new Vector2(1f, 0.98f),
            new Vector2(40f, 0f), new Vector2(-40f, 0f), title: true);
        summary = ReferenceUiVisuals.Text(cardRect, "Summary", "", 14, ReferenceUiVisuals.Muted,
            TextAlignmentOptions.Center, new Vector2(0f, 0.81f), new Vector2(1f, 0.88f),
            new Vector2(16f, 0f), new Vector2(-16f, 0f), bodyBold: true);

        var close = ReferenceUiVisuals.Button(cardRect, "CloseBtn", "X", false,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-46f, -46f), new Vector2(-8f, -8f), 16);
        close.onClick.AddListener(Hide);

        const float top = 0.79f, rowHeight = 0.085f;
        for (int i = 0; i < Progression.MaxBuilders; i++)
        {
            float yMax = top - i * rowHeight;
            var row = ReferenceUiVisuals.Image(cardRect, "Row_" + (i + 1), new Vector2(0.05f, yMax - rowHeight + 0.012f),
                new Vector2(0.95f, yMax), Vector2.zero, Vector2.zero, Color.white);
            row.sprite = ReferenceUiVisuals.PillSprite();
            row.type = Image.Type.Sliced;
            rowBackground[i] = row;
            rowName[i] = ReferenceUiVisuals.Text(row.rectTransform, "Name", L.T($"{i + 1}. İnşaatçı", $"Builder {i + 1}"), 14, ReferenceUiVisuals.Light,
                TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(0.36f, 1f),
                new Vector2(18f, 0f), Vector2.zero, bodyBold: true);
            rowStatus[i] = ReferenceUiVisuals.Text(row.rectTransform, "Status", "", 13, ReferenceUiVisuals.Muted,
                TextAlignmentOptions.Right, new Vector2(0.34f, 0f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(-18f, 0f));
            // Süren işi elmasla hemen bitir.
            int index = i;
            rowFinish[i] = ReferenceUiVisuals.Button(row.rectTransform, "Finish", "", true,
                new Vector2(0.74f, 0.1f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero, 12);
            rowFinishLabel[i] = rowFinish[i].GetComponentInChildren<TextMeshProUGUI>();
            rowFinish[i].onClick.AddListener(() => GemActions.OfferFinish(rowTask[index], Refresh));
            rowFinish[i].gameObject.SetActive(false);
        }

        // Elmas güçlendirmeleri: İnşaat İksiri ve misafir inşaatçı.
        var potion = ReferenceUiVisuals.Button(cardRect, "PotionBtn", "", false,
            new Vector2(0.05f, 0.255f), new Vector2(0.495f, 0.345f), Vector2.zero, Vector2.zero, 13);
        potionLabel = potion.GetComponentInChildren<TextMeshProUGUI>();
        potion.onClick.AddListener(() => GemActions.OfferPotion(Refresh));
        GemConfirm.AddGemIcon(potion.transform);
        var guestButton = ReferenceUiVisuals.Button(cardRect, "GuestBtn", "", false,
            new Vector2(0.505f, 0.255f), new Vector2(0.95f, 0.345f), Vector2.zero, Vector2.zero, 13);
        guestLabel = guestButton.GetComponentInChildren<TextMeshProUGUI>();
        guestButton.onClick.AddListener(() => GemActions.OfferTempBuilder(Refresh));
        GemConfirm.AddGemIcon(guestButton.transform);

        hireButton = ReferenceUiVisuals.Button(cardRect, "HireBtn", "", true,
            new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.23f), Vector2.zero, Vector2.zero, 15);
        hireLabel = hireButton.GetComponentInChildren<TextMeshProUGUI>();
        hireButton.onClick.AddListener(OnHire);
        ReferenceUiVisuals.Text(cardRect, "Note",
            L.T("Kulübeyi basılı tutup sürükleyerek taşıyabilirsin", "Press and drag a hut to move it"), 12, ReferenceUiVisuals.Muted,
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
        int working = gm.WorkingBuilderCount;
        summary.text = L.T($"Boşta {gm.FreeBuilderCount} / {working}  ·  Elmas: {gm.Gems}", $"Free {gm.FreeBuilderCount} / {working}  ·  Gems: {gm.Gems}");

        potionLabel.text = gm.BuildPotionActive
            ? L.T("   İKSİR · ", "   POTION · ") + GemEconomy.FormatRemaining(gm.PlayerData.buildPotionUntilTicks)
            : L.T($"   İKSİR {GemEconomy.PotionSpeed}x · {GemEconomy.PotionCost}", $"   POTION {GemEconomy.PotionSpeed}x · {GemEconomy.PotionCost}");
        guestLabel.text = gm.TempBuilderActive
            ? L.T("   MİSAFİR · ", "   GUEST · ") + GemEconomy.FormatRemaining(gm.PlayerData.tempBuilderUntilTicks)
            : L.T($"   MİSAFİR {GemEconomy.TempBuilderHours}sa · {GemEconomy.TempBuilderCost}", $"   GUEST {GemEconomy.TempBuilderHours}h · {GemEconomy.TempBuilderCost}");

        for (int i = 0; i < Progression.MaxBuilders; i++)
        {
            var status = rowStatus[i];
            var task = i < working ? gm.TaskForBuilder(i) : null;
            rowTask[i] = task;
            rowFinish[i].gameObject.SetActive(task != null);
            var statusRect = status.rectTransform;
            statusRect.anchorMax = new Vector2(task != null ? 0.73f : 1f, 1f);
            statusRect.offsetMax = new Vector2(task != null ? -4f : -18f, 0f);
            if (task != null)
            {
                int cost = GameManager.FinishNowCost(task);
                rowFinishLabel[i].text = cost > 0 ? L.T("BİTİR ", "FINISH ") + cost : L.T("BİTİR", "FINISH");
            }
            rowName[i].text = i >= total && i < working ? L.T("Misafir", "Guest") : L.T($"{i + 1}. İnşaatçı", $"Builder {i + 1}");
            if (i < working)
            {
                status.text = task == null ? L.T("<color=#9FD37E>Boşta</color>", "<color=#9FD37E>Idle</color>")
                    : $"{ShortLabel(task)} · {Progression.FormatDuration(GameManager.SecondsRemaining(task))}";
                rowBackground[i].color = Color.white;
            }
            else
            {
                var cost = Progression.BuilderCost(i + 1);
                string price = cost.gold > 0 ? L.T($"{cost.gold} altın", $"{cost.gold} gold") : L.T($"{cost.gems} elmas", $"{cost.gems} gems");
                status.text = i + 1 == 2
                    ? L.T("Ana Üs Sev. ", "Main Base Lv. ") + $"{Progression.SecondBuilderMainBaseLevel} · {price}"
                    : L.T("Kilitli · ", "Locked · ") + price;
                rowBackground[i].color = new Color(1f, 1f, 1f, 0.55f);
            }
        }

        var block = gm.CanHireBuilder();
        var next = gm.NextBuilderCost;
        string nextPrice = next.gold > 0 ? L.T($"{next.gold} ALTIN", $"{next.gold} GOLD") : L.T($"{next.gems} ELMAS", $"{next.gems} GEMS");
        switch (block)
        {
            case GameManager.HireBlock.MaxReached:
                hireLabel.text = L.T("TÜM İNŞAATÇILAR HAZIR", "ALL BUILDERS HIRED");
                break;
            case GameManager.HireBlock.MainBaseLevel:
                hireLabel.text = L.T($"ANA ÜS SEV. {Progression.SecondBuilderMainBaseLevel} GEREKLİ", $"REQUIRES MAIN BASE LV. {Progression.SecondBuilderMainBaseLevel}");
                break;
            case GameManager.HireBlock.NoGold:
            case GameManager.HireBlock.NoGems:
                hireLabel.text = L.T($"{total + 1}. İNŞAATÇI", $"BUILDER {total + 1}") + $" · <color=#8A2A1A>{nextPrice}</color>";
                break;
            default:
                hireLabel.text = L.T($"{total + 1}. İNŞAATÇI", $"BUILDER {total + 1}") + $" · {nextPrice}";
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
        if (task.kind == "remove") return L.T("Temizlik", "Clearing");
        return string.IsNullOrEmpty(task.label) ? L.T("Çalışıyor", "Working") : task.label;
    }

    private void OnHire()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var block = gm.CanHireBuilder();
        var setup = Object.FindFirstObjectByType<BaseSceneSetup>();
        if (block == GameManager.HireBlock.NoGold)
        {
            setup?.ShowHudToast(L.T("Yeterli altın yok", "Not enough gold"));
            return;
        }
        if (block == GameManager.HireBlock.NoGems)
        {
            GemShopUI.Open();
            return;
        }
        if (!gm.HireBuilder()) return;
        setup?.ShowHudToast(L.T($"{gm.BuilderCount}. inşaatçı geldi! Kulübesini basılı tutarak taşıyabilirsin", $"Builder {gm.BuilderCount} arrived! Press and drag the hut to move it"));
        Refresh();
    }
}
