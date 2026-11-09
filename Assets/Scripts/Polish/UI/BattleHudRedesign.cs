using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Savaş HUD'ının görsel dili: ahşap alt panel; Savaşçı / Ana Üs / Çırak için çerçeveli,
/// portreli can kartları; tehlikede kırmızıya dönen nabızlı çubuk; okunur dalga afişi.
/// Mevcut veri bağlantıları (Slider, Image fill, metinler) korunur.
/// </summary>
public static class BattleHudRedesign
{
    // Alt panel yüksekliği (canvas birimi): kartlar + ince kenar; eski boş bant kaldırıldı.
    public const float DockHeight = 104f;
    private const float DockFraction = DockHeight / 844f;

    public static void Apply(RectTransform safe)
    {
        if (safe == null) return;
        var dock = safe.Find("BattleBottomDock") as RectTransform;
        if (dock != null)
        {
            dock.anchorMin = Vector2.zero;
            dock.anchorMax = new Vector2(1f, DockFraction);
            dock.offsetMin = dock.offsetMax = Vector2.zero;
            BaseHudRedesign.SkinDock(dock);
        }

        var gm = GameManager.Instance;
        Sprite heroIcon = null;
        if (gm?.PlayerData != null && SpriteManager.Instance != null)
            heroIcon = HeroPortraitSprite.Get(SpriteManager.Instance.GetHeroSprite(gm.PlayerData.hero.weaponType));
        Sprite baseIcon = null;
        if (gm?.Buildings != null && gm.Buildings.TryGetValue(BuildingType.MainBase, out var mainBase))
            baseIcon = mainBase.VisualForLevel(mainBase.level)?.sprite;

        StyleCard(safe.Find("HeroBarBg") as RectTransform, 0, heroIcon, ReferenceUiVisuals.Hex(0x4FCF74));
        StyleCard(safe.Find("BaseBarBg") as RectTransform, 1, baseIcon, ReferenceUiVisuals.Gold);

        // İnşa ipucu artık panelin hemen üstünde, hap içinde.
        var hint = safe.Find("BattleBuildHint") as RectTransform;
        if (hint != null)
        {
            hint.anchorMin = new Vector2(0.1f, DockFraction + 0.006f);
            hint.anchorMax = new Vector2(0.9f, DockFraction + 0.036f);
            hint.offsetMin = hint.offsetMax = Vector2.zero;
            var pill = ReferenceUiVisuals.Image(hint.parent, "BattleBuildHintPill", hint.anchorMin, hint.anchorMax,
                Vector2.zero, Vector2.zero, Color.white);
            pill.sprite = ReferenceUiVisuals.PillSprite();
            pill.type = Image.Type.Sliced;
            pill.transform.SetSiblingIndex(hint.GetSiblingIndex());
            pill.gameObject.SetActive(hint.gameObject.activeSelf);
            hint.gameObject.AddComponent<FollowActive>().Target = pill.gameObject;
        }

        StyleIncoming(safe);
        var track = safe.Find("TopBar/WaveTrack") as RectTransform;
        if (track != null)
        {
            track.anchorMin = new Vector2(0.06f, 0.05f);
            track.anchorMax = new Vector2(0.94f, 0.13f);
            track.offsetMin = track.offsetMax = Vector2.zero;
            var trackImage = track.GetComponent<Image>();
            if (trackImage != null) trackImage.color = Color.clear;
        }
    }

    /// <summary>Kart: çerçeve, solda portre yuvası, üstte ad + değer, altta yuvarlak can çubuğu.</summary>
    public static void StyleCard(RectTransform root, int column, Sprite icon, Color accent)
    {
        if (root == null || root.Find("CardFrame") != null) return;
        float left = 0.02f + column * 0.327f;
        root.anchorMin = new Vector2(left, 0.016f);
        root.anchorMax = new Vector2(left + 0.31f, DockFraction - 0.02f);
        root.offsetMin = root.offsetMax = Vector2.zero;

        var bg = root.GetComponent<Image>();
        if (bg != null)
        {
            bg.sprite = ReferenceUiVisuals.ToastSprite();
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
        }
        var frame = root.gameObject.AddComponent<Outline>();
        frame.effectColor = new Color(accent.r, accent.g, accent.b, 0.45f);
        frame.effectDistance = new Vector2(1f, -1f);
        ReferenceUiVisuals.Rect(root, "CardFrame", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Portre yuvası
        var well = ReferenceUiVisuals.Image(root, "PortraitWell", new Vector2(0.04f, 0.14f), new Vector2(0.30f, 0.86f),
            Vector2.zero, Vector2.zero, ReferenceUiVisuals.Hex(0x2A2118));
        well.sprite = ReferenceUiVisuals.SolidPillSprite();
        well.type = Image.Type.Sliced;
        var ring = well.gameObject.AddComponent<Outline>();
        ring.effectColor = accent;
        ring.effectDistance = new Vector2(1f, -1f);
        if (icon != null)
        {
            var portrait = ReferenceUiVisuals.Image(well.rectTransform, "Portrait", Vector2.zero, Vector2.one,
                new Vector2(3f, 3f), new Vector2(-3f, -3f), Color.white);
            portrait.sprite = icon;
            portrait.preserveAspect = true;
        }

        // Metinler ve çubuk portrenin sağında.
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            bool isValue = text.text.Contains("/") || text.name.Contains("Hp") || text.name == "Label";
            var rt = text.rectTransform;
            rt.anchorMin = isValue ? new Vector2(0.34f, 0.50f) : new Vector2(0.34f, 0.50f);
            rt.anchorMax = new Vector2(0.97f, 0.94f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            ReferenceUiVisuals.ApplyFont(text, isValue ? 12f : 13f,
                isValue ? ReferenceUiVisuals.Muted : ReferenceUiVisuals.Light, false, !isValue);
            text.alignment = isValue ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = false;
        }

        // Slider (Savaşçı, Ana Üs) ya da Image fill (Çırak) – ikisi de aynı görünür.
        var slider = root.GetComponentInChildren<Slider>(true);
        RectTransform bar = slider != null ? (RectTransform)slider.transform : root.Find("Bar") as RectTransform;
        if (bar != null)
        {
            bar.anchorMin = new Vector2(0.34f, 0.18f);
            bar.anchorMax = new Vector2(0.96f, 0.44f);
            bar.offsetMin = bar.offsetMax = Vector2.zero;
            Image track = slider != null ? bar.Find("Background")?.GetComponent<Image>() : bar.GetComponent<Image>();
            Image fill = slider != null ? bar.Find("Fill Area/Fill")?.GetComponent<Image>() : bar.Find("Fill")?.GetComponent<Image>();
            if (track != null)
            {
                track.sprite = ReferenceUiVisuals.SolidPillSprite();
                track.type = Image.Type.Sliced;
                track.color = ReferenceUiVisuals.Hex(0x120D08);
            }
            if (fill != null)
            {
                if (slider != null)
                {
                    fill.sprite = ReferenceUiVisuals.SolidPillSprite();
                    fill.type = Image.Type.Sliced;
                }
                fill.color = accent;
                var shine = ReferenceUiVisuals.Image(fill.rectTransform, "Shine", new Vector2(0f, 0.55f), Vector2.one,
                    new Vector2(2f, 0f), new Vector2(-2f, -1f), new Color(1f, 1f, 1f, 0.22f));
                shine.sprite = ReferenceUiVisuals.SolidPillSprite();
                shine.type = Image.Type.Sliced;
                fill.gameObject.AddComponent<DangerPulse>().Initialize(fill, accent, slider);
            }
        }
    }

    private static void StyleIncoming(RectTransform safe)
    {
        var incoming = safe.Find("IncomingChip") as RectTransform;
        if (incoming != null)
        {
            incoming.anchorMin = new Vector2(0.18f, 0.835f);
            incoming.anchorMax = new Vector2(0.82f, 0.878f);
            incoming.offsetMin = incoming.offsetMax = Vector2.zero;
            var image = incoming.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = ReferenceUiVisuals.ToastSprite();
                image.type = Image.Type.Sliced;
                image.color = new Color(1f, 0.55f, 0.5f);
            }
            var outline = incoming.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.94f, 0.3f, 0.2f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var label = incoming.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) ReferenceUiVisuals.ApplyFont(label, 17f, Color.white, true);
        }
        var countdown = safe.Find("NextWaveCountdown") as RectTransform;
        if (countdown != null)
        {
            countdown.anchorMin = new Vector2(0.24f, 0.805f);
            countdown.anchorMax = new Vector2(0.76f, 0.833f);
            countdown.offsetMin = countdown.offsetMax = Vector2.zero;
            var text = countdown.GetComponent<TextMeshProUGUI>();
            if (text != null) ReferenceUiVisuals.ApplyFont(text, 15f, ReferenceUiVisuals.Gold, false, true);
        }
    }
}

/// <summary>Can %30'un altına inince çubuk kırmızıya döner ve nabız gibi atar.</summary>
public sealed class DangerPulse : MonoBehaviour
{
    private Image fill;
    private Color normal;
    private Slider slider;

    public void Initialize(Image target, Color accent, Slider source)
    {
        fill = target;
        normal = accent;
        slider = source;
    }

    private void Update()
    {
        if (fill == null) return;
        float ratio = slider != null ? slider.normalizedValue : fill.fillAmount;
        if (ratio > 0.3f)
        {
            fill.color = normal;
            return;
        }
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
        fill.color = Color.Lerp(ReferenceUiVisuals.Hex(0xC2452D), ReferenceUiVisuals.Hex(0xFF7A5C), pulse);
    }
}

/// <summary>Hedef nesnenin görünürlüğü bu nesneninkini izler (ipucu ve hap arka planı).</summary>
public sealed class FollowActive : MonoBehaviour
{
    public GameObject Target;
    private void OnEnable() { if (Target != null) Target.SetActive(true); }
    private void OnDisable() { if (Target != null) Target.SetActive(false); }
}
