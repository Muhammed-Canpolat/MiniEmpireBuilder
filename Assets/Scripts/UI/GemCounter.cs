using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>Üst bardaki elmas sayısını GameManager.OnGemsChanged ile güncel tutar.</summary>
public sealed class GemCounter : MonoBehaviour
{
    private TextMeshProUGUI label;
    private int shown = -1;

    public void Bind(TextMeshProUGUI text)
    {
        label = text;
        if (GameManager.Instance != null) GameManager.Instance.OnGemsChanged += Show;
        Show(GameManager.Instance != null ? GameManager.Instance.Gems : 0);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnGemsChanged -= Show;
        if (label != null) label.transform.DOKill();
    }

    private int tapCount;
    private float lastTap;

    /// <summary>
    /// Yalnız editör ve Development derlemesinde: 1,5 sn içinde 5 dokunuş +100 elmas verir (test için).
    /// Yayın derlemesinde her zaman false döner.
    /// </summary>
    public bool RegisterTestTap()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        tapCount = Time.unscaledTime - lastTap < 0.4f ? tapCount + 1 : 1;
        lastTap = Time.unscaledTime;
        if (tapCount < 5) return false;
        tapCount = 0;
        GameManager.Instance?.AddGems(100);
        return true;
#else
        return false;
#endif
    }

    private void Show(int gems)
    {
        if (label == null) return;
        bool changed = shown >= 0 && gems != shown;
        shown = gems;
        label.text = gems.ToString();
        if (!changed) return;
        label.transform.DOKill();
        label.transform.localScale = Vector3.one;
        label.transform.DOPunchScale(Vector3.one * 0.25f, 0.3f);
    }
}
