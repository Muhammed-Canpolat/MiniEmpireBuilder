// [WAVE ANNOUNCER] — Mini Empire Builder
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WaveAnnouncer : MonoBehaviour
{
    [SerializeField] private RectTransform banner;
    [SerializeField] private TextMeshProUGUI bannerText;
    [SerializeField] private Image vignette;

    private readonly Vector2 hiddenTop = new Vector2(0f, 400f);
    private readonly Vector2 center = new Vector2(0f, 80f);
    private readonly Vector2 hiddenBottom = new Vector2(0f, -500f);

    /// <summary>
    /// Shows "new wave incoming" banner with red vignette flash.
    /// </summary>
    public void AnnounceIncomingWave(int waveNumber)
    {
        if (banner == null || bannerText == null)
            return;

        banner.DOKill();
        if (vignette != null)
            vignette.DOKill();

        bannerText.text = $"DALGA {waveNumber} GELIYOR!";
        banner.anchoredPosition = hiddenTop;

        Sequence seq = DOTween.Sequence();
        seq.Append(banner.DOAnchorPos(center, 0.35f).SetEase(Ease.OutCubic));

        if (vignette != null)
        {
            Color c = vignette.color;
            c.a = 0f;
            vignette.color = c;
            seq.Join(vignette.DOFade(0.45f, 0.15f).SetLoops(2, LoopType.Yoyo));
        }

        seq.AppendInterval(2f);
        seq.Append(banner.DOAnchorPos(hiddenBottom, 0.3f).SetEase(Ease.InCubic));
    }
}
