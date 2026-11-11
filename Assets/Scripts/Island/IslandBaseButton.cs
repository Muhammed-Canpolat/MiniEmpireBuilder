using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana Üs HUD'ında SEFER'in yanındaki ADA düğmesi (island_base.md §1). Ana Üs 5'ten önce kilitli görünür ("ADA · 5" yazar),
/// 5 olunca bir kez "Ufukta bir ada!" afişi çıkar. Basınca <see cref="GameManager.LoadIsland"/> ile yelken açılır.
/// </summary>
public sealed class IslandBaseButton : MonoBehaviour
{
    private Button button;
    private TextMeshProUGUI label;
    private Image face;
    private RectTransform hud;
    private TextMeshProUGUI toast;
    private float toastUntil, nextRefresh;
    private bool lastUnlocked;

    /// <summary>HUD köküne düğmeyi ekler (tekrar çağrılırsa bir şey yapmaz).</summary>
    public static void Attach(Transform hudRoot)
    {
        if (hudRoot == null || hudRoot.Find("IslandBtn") != null) return;
        var button = ReferenceUiVisuals.Button(hudRoot, "IslandBtn", L.T("ADA", "ISLE"), false,
            new Vector2(0.83f, 0f), new Vector2(0.83f, 0f), new Vector2(-46f, 92f), new Vector2(46f, 134f), 14);
        var component = button.gameObject.AddComponent<IslandBaseButton>();
        component.hud = hudRoot as RectTransform;
        component.button = button;
        component.face = button.GetComponent<Image>();
        component.label = button.GetComponentInChildren<TextMeshProUGUI>();
        button.onClick.AddListener(component.OnClick);
        component.Refresh(true);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.5f; Refresh(false); }
        if (toast != null && toast.gameObject.activeSelf && Time.unscaledTime > toastUntil) toast.gameObject.SetActive(false);
    }

    private bool Unlocked => IslandSession.Unlocked();

    private void Refresh(bool first)
    {
        bool unlocked = Unlocked;
        label.text = unlocked ? L.T("ADA", "ISLE") : L.T("ADA · 5", "ISLE · 5");
        face.color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.9f);
        if (unlocked && !lastUnlocked && !first) Pulse();
        lastUnlocked = unlocked;
        var save = IslandSession.Save;
        if (unlocked && save != null && !save.announced)
        {
            save.announced = true;
            GameManager.Instance?.SaveGame();
            Say(L.T("Ufukta bir ada!", "An island on the horizon!"), 4f, true);
        }
    }

    private void Pulse()
    {
        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.DOScale(1.15f, 0.4f).SetLoops(6, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
    }

    private void OnClick()
    {
        if (!Unlocked)
        {
            Say(L.T($"Ana Üs {Progression.Island.UnlockMainBaseLevel}'te Martı Adası açılır.",
                $"Gull Isle opens at Main Base {Progression.Island.UnlockMainBaseLevel}."), 2.5f, false);
            return;
        }
        GameManager.Instance?.LoadIsland();
    }

    private void Say(string text, float seconds, bool ceremony)
    {
        if (hud == null) return;
        if (toast == null)
        {
            toast = ReferenceUiVisuals.Text(hud, "IslandToast", "", 22, ReferenceUiVisuals.Gold, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.62f), new Vector2(0, -26), new Vector2(0, 26), true);
            toast.raycastTarget = false;
        }
        toast.text = text;
        toast.fontSize = ceremony ? 26 : 16;
        toast.gameObject.SetActive(true);
        toastUntil = Time.unscaledTime + seconds;
        toast.transform.DOKill();
        toast.transform.localScale = Vector3.one * 0.7f;
        toast.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(toast.gameObject);
    }
}
