// [HERO CARD VISUAL] — Mini Empire Builder
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class HeroCardVisual : MonoBehaviour
{
    private const float SelectedScale = 1.04f;
    private const float UnselectedAlpha = 0.6f;

    [SerializeField] private Image borderImage;
    [SerializeField] private Image cardDimmer;
    [SerializeField] private Color selectedOutlineColor = new Color32(0xD4, 0xA0, 0x17, 0xFF);

    /// <summary>
    /// Sets selected state visuals for this hero card.
    /// </summary>
    public void SetSelected(bool selected)
    {
        transform.DOScale(selected ? SelectedScale : 1f, 0.2f).SetEase(Ease.OutQuad);

        if (cardDimmer != null)
        {
            Color c = cardDimmer.color;
            c.a = selected ? 0f : UnselectedAlpha;
            cardDimmer.DOColor(c, 0.2f);
        }

        if (borderImage == null)
            return;

        borderImage.DOKill();
        if (selected)
        {
            borderImage.DOColor(selectedOutlineColor, 0.4f).SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            borderImage.DOColor(Color.white, 0.15f);
        }
    }
}
