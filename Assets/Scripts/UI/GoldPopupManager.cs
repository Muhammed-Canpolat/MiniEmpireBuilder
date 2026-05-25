using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// Altın artış ve azalışlarında ekranda süzülen yazı (Float text) gösterir.
/// </summary>
public class GoldPopupManager : MonoBehaviour
{
    public static void ShowGoldChange(int amount)
    {
        if (amount == 0) return;

        // Rastgele bir pozisyona veya UI ekranının ortasına doğru çıkar
        Vector3 spawnPos = new Vector3(Screen.width / 2f + Random.Range(-50f, 50f), Screen.height / 2f + 100f, 0f);

        GameObject go = new GameObject("GoldPopup");
        go.transform.position = spawnPos;

        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100; // En üstte

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 50f);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 40f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        if (amount > 0)
        {
            tmp.text = $"+{amount}";
            tmp.color = new Color(1f, 0.85f, 0f); // Altın Sarısı
        }
        else
        {
            tmp.text = $"{amount}";
            tmp.color = new Color(1f, 0.2f, 0.2f); // Kırmızı
        }

        // Float up animasyonu
        go.transform.DOMoveY(spawnPos.y + 150f, 1.2f).SetEase(Ease.OutCubic);
        tmp.DOFade(0f, 1.2f).SetDelay(0.3f).OnComplete(() =>
        {
            if (go != null) Destroy(go);
        });
    }
}
