using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BaseLevelBanner : MonoBehaviour
{
    private RectTransform banner;
    private CanvasGroup group;
    private TextMeshProUGUI text;

    private void Start()
    {
        var canvas = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea");
        if (canvas == null) return;
        var root = new GameObject("MainBaseLevelBanner", typeof(RectTransform),
            typeof(Image), typeof(CanvasGroup));
        root.transform.SetParent(canvas, false);
        banner = root.GetComponent<RectTransform>();
        banner.anchorMin = new Vector2(0.05f, 1f);
        banner.anchorMax = new Vector2(0.95f, 1f);
        banner.pivot = new Vector2(0.5f, 1f);
        banner.anchoredPosition = new Vector2(0f, -72f);
        banner.sizeDelta = new Vector2(0f, 100f);
        root.GetComponent<Image>().color = new Color(0.085f, 0.065f, 0.045f, 0.94f);
        group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        var child = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        child.transform.SetParent(root.transform, false);
        var rect = child.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 8f);
        rect.offsetMax = new Vector2(-12f, -8f);
        text = child.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 27f;
        text.color = new Color(1f, 0.85f, 0.52f);
        root.SetActive(false);
        if (GameManager.Instance != null)
            GameManager.Instance.OnBuildingUpgraded += OnUpgraded;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnBuildingUpgraded -= OnUpgraded;
    }

    private void OnUpgraded(BuildingType type, int level)
    {
        if (type != BuildingType.MainBase || banner == null) return;
        var unlocked = new List<string>();
        if (GameManager.Instance?.Buildings != null)
            foreach (var entry in GameManager.Instance.Buildings)
                if (entry.Key != BuildingType.MainBase &&
                    entry.Value.GetRequiredMainBaseLevel() == level)
                    unlocked.Add(BaseMapManager.GetBuildingDisplayName(entry.Key));
        var before = BaseWorldLayout.CleanAtLevel(level - 1);
        var after = BaseWorldLayout.CleanAtLevel(level);
        if (after.width * after.height > before.width * before.height)
            unlocked.Add("Yeni arazi");
        text.text = (after.width * after.height > before.width * before.height
            ? "YENİ ARAZİ AÇILDI\n" : "") + $"ANA ÜS SEV. {level}!" +
            (unlocked.Count > 0 ? "\nAçıldı: " + string.Join(" · ", unlocked) : "");
        banner.gameObject.SetActive(true);
        banner.DOKill();
        group.DOKill();
        banner.localScale = Vector3.one * 0.9f;
        group.alpha = 0f;
        banner.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
        group.DOFade(1f, 0.2f).OnComplete(() =>
            group.DOFade(0f, 0.35f).SetDelay(2.2f)
                .OnComplete(() => banner.gameObject.SetActive(false)));
    }
}
