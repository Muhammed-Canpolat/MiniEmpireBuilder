using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Savaş havası. Bölge 4 (specialMechanic "Don"): yaklaşık 22 sn'de bir kar fırtınası gelir.
/// 2 sn önce uyarı çıkar; fırtına 6 sn sürer: kuleler %40 yavaş ateş eder, savaşçı %25 yavaş yürür,
/// ekran buzlanır. Çarpanlar statik: HeroController ve TowerController okur, fırtına yoksa 1.
/// </summary>
public sealed class BattleWeather : MonoBehaviour
{
    public const float Interval = 22f;
    public const float WarningSeconds = 2f;
    public const float StormSeconds = 6f;
    public const float StormTowerCooldown = 1.4f;   // kule bekleme süresi ×1.4 (= %40 yavaş)
    public const float StormHeroSpeed = 0.75f;

    public static bool StormActive { get; private set; }
    public static float TowerCooldownMultiplier => StormActive ? StormTowerCooldown : 1f;
    public static float HeroSpeedMultiplier => StormActive ? StormHeroSpeed : 1f;

    private CanvasGroup frost;
    private TextMeshProUGUI banner;

    private IEnumerator Start()
    {
        StormActive = false;
        BuildOverlay();
        yield return new WaitForSeconds(12f); // ilk fırtına savaş oturduktan sonra
        while (true)
        {
            var battle = BattleManager.Instance;
            if (battle != null && battle.IsBattleActive)
            {
                banner.text = L.T("KAR FIRTINASI GELİYOR!", "BLIZZARD INCOMING!");
                yield return Fade(0f, 0.25f, WarningSeconds);
                StormActive = true;
                banner.text = L.T("FIRTINA: kuleler ve savaşçı yavaş", "BLIZZARD: towers and warrior slowed");
                yield return Fade(0.25f, 0.6f, 0.5f);
                yield return new WaitForSeconds(StormSeconds - 0.5f);
                StormActive = false;
                yield return Fade(0.6f, 0f, 0.8f);
            }
            yield return new WaitForSeconds(Interval - WarningSeconds - StormSeconds);
        }
    }

    private void OnDestroy() => StormActive = false;

    private IEnumerator Fade(float from, float to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            if (frost != null) frost.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        if (frost != null) frost.alpha = to;
    }

    private void BuildOverlay()
    {
        var canvasObject = new GameObject("FrostCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20; // HUD'un altında
        ReferenceUiVisuals.ConfigureCanvas(canvasObject.GetComponent<CanvasScaler>());
        var root = ReferenceUiVisuals.Rect(canvas.transform, "Frost", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        frost = root.gameObject.AddComponent<CanvasGroup>();
        frost.alpha = 0f;
        frost.blocksRaycasts = false;
        frost.interactable = false;
        var wash = ReferenceUiVisuals.Image(root, "Wash", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.75f, 0.9f, 1f, 0.35f));
        wash.raycastTarget = false;
        // Kenarlarda koyu buz çerçevesi.
        foreach (var (min, max) in new[] {
            (new Vector2(0f, 0f), new Vector2(1f, 0.06f)), (new Vector2(0f, 0.94f), new Vector2(1f, 1f)),
            (new Vector2(0f, 0f), new Vector2(0.06f, 1f)), (new Vector2(0.94f, 0f), new Vector2(1f, 1f)) })
            ReferenceUiVisuals.Image(root, "Edge", min, max, Vector2.zero, Vector2.zero,
                new Color(0.85f, 0.95f, 1f, 0.55f)).raycastTarget = false;
        banner = ReferenceUiVisuals.Text(root, "Banner", "", 16, ReferenceUiVisuals.Hex(0x1E4A6B), TextAlignmentOptions.Center,
            new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.67f), Vector2.zero, Vector2.zero, true);
        banner.raycastTarget = false;
    }
}
