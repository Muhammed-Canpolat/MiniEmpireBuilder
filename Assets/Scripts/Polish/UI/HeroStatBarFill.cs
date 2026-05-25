// [HERO STAT BAR FILL] — Mini Empire Builder
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class HeroStatBarFill : MonoBehaviour
{
    [SerializeField] private Image damageFill;
    [SerializeField] private Image rangeFill;
    [SerializeField] private Image speedFill;
    [SerializeField] private Image healthFill;
    [SerializeField] private Color highlightColor = new Color32(0xD4, 0xA0, 0x17, 0xFF);
    [SerializeField] private Color mutedColor = new Color32(0x66, 0x66, 0x66, 0xFF);
    [SerializeField] private WeaponType highlightedStatWeapon;

    /// <summary>
    /// Initializes stat bars from normalized values and animates fill from zero.
    /// </summary>
    public void Init(float damage, float range, float speed, float health)
    {
        SetFill(0f);
        AnimateFill(damageFill, damage);
        AnimateFill(rangeFill, range);
        AnimateFill(speedFill, speed);
        AnimateFill(healthFill, health);
        ApplyHighlight();
    }

    private void ApplyHighlight()
    {
        if (damageFill == null || rangeFill == null || speedFill == null)
            return;

        damageFill.color = mutedColor;
        rangeFill.color = mutedColor;
        speedFill.color = mutedColor;

        switch (highlightedStatWeapon)
        {
            case WeaponType.Axe: damageFill.color = highlightColor; break;
            case WeaponType.Spear: rangeFill.color = highlightColor; break;
            case WeaponType.Bow: speedFill.color = highlightColor; break;
        }
    }

    private void SetFill(float value)
    {
        if (damageFill != null) damageFill.fillAmount = value;
        if (rangeFill != null) rangeFill.fillAmount = value;
        if (speedFill != null) speedFill.fillAmount = value;
        if (healthFill != null) healthFill.fillAmount = value;
    }

    private static void AnimateFill(Image image, float target)
    {
        if (image == null)
            return;

        image.DOFillAmount(Mathf.Clamp01(target), 0.45f).SetEase(Ease.OutCubic);
    }
}
