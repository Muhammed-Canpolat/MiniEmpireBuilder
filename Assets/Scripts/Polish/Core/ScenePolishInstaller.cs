// [SCENE POLISH INSTALLER] — Mini Empire Builder
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScenePolishInstaller : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null;
        yield return new WaitForSeconds(0.1f);

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        InstallGlobal();

        if (sceneName == "MainMenuScene")
        {
            InstallIntroPolish();
        }
        else if (sceneName == "BaseScene")
        {
            InstallBasePolish();
        }
        else if (sceneName == "BattleScene")
        {
            InstallBattlePolish();
        }
    }

    private void InstallGlobal()
    {
        if (FindFirstObjectByType<MobilePerformanceConfig>() == null)
        {
            new GameObject("MobilePerformanceConfig").AddComponent<MobilePerformanceConfig>();
        }

        if (FindFirstObjectByType<GlobalUiThemeApplier>() == null)
        {
            GameObject go = new GameObject("GlobalUiThemeApplier");
            go.AddComponent<GlobalUiThemeApplier>();
        }
    }

    private void InstallIntroPolish()
    {
        GameObject introCanvas = GameObject.Find("IntroCanvas");
        if (introCanvas == null)
        {
            return;
        }

        Transform[] children = introCanvas.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "ContinueBtn")
            {
                PulseOnUi pulse = children[i].gameObject.GetComponent<PulseOnUi>();
                if (pulse == null)
                {
                    children[i].gameObject.AddComponent<PulseOnUi>();
                }
            }

            if (children[i].name.EndsWith("Card"))
            {
                HeroCardVisual visual = children[i].gameObject.GetComponent<HeroCardVisual>();
                if (visual == null)
                {
                    visual = children[i].gameObject.AddComponent<HeroCardVisual>();
                }

                HeroStatBarFill statFill = children[i].gameObject.GetComponent<HeroStatBarFill>();
                if (statFill == null)
                {
                    statFill = children[i].gameObject.AddComponent<HeroStatBarFill>();
                }

                Image damage = FindImage(children[i], "HasarFill");
                Image range = FindImage(children[i], "MenzilFill");
                Image speed = FindImage(children[i], "HizFill");
                Image hp = FindImage(children[i], "CanFill");

                SetSerializedField(statFill, "damageFill", damage);
                SetSerializedField(statFill, "rangeFill", range);
                SetSerializedField(statFill, "speedFill", speed);
                SetSerializedField(statFill, "healthFill", hp);

                WeaponType w = WeaponType.Spear;
                if (children[i].name.Contains("Axe")) w = WeaponType.Axe;
                if (children[i].name.Contains("Bow")) w = WeaponType.Bow;
                SetSerializedField(statFill, "highlightedStatWeapon", w);

                statFill.Init(damage != null ? damage.fillAmount : 0.5f,
                    range != null ? range.fillAmount : 0.5f,
                    speed != null ? speed.fillAmount : 0.5f,
                    hp != null ? hp.fillAmount : 0.5f);
            }
        }
    }

    private void InstallBasePolish()
    {
        if (FindFirstObjectByType<ResourceHUD>() != null)
        {
            return;
        }

        TextMeshProUGUI goldText = FindText("GoldText");
        TextMeshProUGUI gpsText = FindText("GpsText");
        if (goldText == null || gpsText == null)
        {
            return;
        }

        GameObject hudObj = new GameObject("ResourceHUD");
        ResourceHUD hud = hudObj.AddComponent<ResourceHUD>();
        SetSerializedField(hud, "amountText", goldText);
        SetSerializedField(hud, "rateText", gpsText);

        GameObject popup = new GameObject("GoldDelta");
        popup.transform.SetParent(goldText.transform.parent, false);
        TextMeshProUGUI popupText = popup.AddComponent<TextMeshProUGUI>();
        popupText.alignment = TextAlignmentOptions.Left;
        popupText.fontSize = 22f;
        popupText.rectTransform.anchorMin = new Vector2(0.02f, 0.42f);
        popupText.rectTransform.anchorMax = new Vector2(0.3f, 0.78f);
        popupText.rectTransform.offsetMin = Vector2.zero;
        popupText.rectTransform.offsetMax = Vector2.zero;
        popupText.gameObject.SetActive(false);

        SetSerializedField(hud, "popupText", popupText);

        if (GameManager.Instance != null)
        {
            hud.SetResource(GameManager.Instance.Gold, GameManager.Instance.GetGoldPerSecond());
            StartCoroutine(UpdateResourceHudLoop(hud));
        }

        if (FindFirstObjectByType<TooltipSystem>() == null)
        {
            new GameObject("TooltipSystem").AddComponent<TooltipSystem>();
        }
    }

    private IEnumerator UpdateResourceHudLoop(ResourceHUD hud)
    {
        while (hud != null)
        {
            if (GameManager.Instance != null)
            {
                hud.SetResource(GameManager.Instance.Gold, GameManager.Instance.GetGoldPerSecond());
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    private void InstallBattlePolish()
    {
        if (FindFirstObjectByType<DamageNumberPool>() == null)
        {
            GameObject prefabObj = new GameObject("DamageNumberPrefab");
            TextMeshPro tmp = prefabObj.AddComponent<TextMeshPro>();
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            DamageNumber dmg = prefabObj.AddComponent<DamageNumber>();

            GameObject poolObj = new GameObject("DamageNumberPool");
            DamageNumberPool pool = poolObj.AddComponent<DamageNumberPool>();
            SetSerializedField(pool, "damageNumberPrefab", dmg);
            prefabObj.SetActive(false);
        }

        if (FindFirstObjectByType<WaveAnnouncer>() == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                GameObject announcerObj = new GameObject("WaveAnnouncer");
                announcerObj.transform.SetParent(canvas.transform, false);
                WaveAnnouncer wa = announcerObj.AddComponent<WaveAnnouncer>();

                GameObject bannerObj = new GameObject("WaveBanner");
                bannerObj.transform.SetParent(announcerObj.transform, false);
                Image bannerBg = bannerObj.AddComponent<Image>();
                bannerBg.color = new Color(0f, 0f, 0f, 0.75f);
                RectTransform br = bannerObj.GetComponent<RectTransform>();
                br.anchorMin = new Vector2(0.2f, 0.7f);
                br.anchorMax = new Vector2(0.8f, 0.82f);
                br.offsetMin = Vector2.zero;
                br.offsetMax = Vector2.zero;

                GameObject textObj = new GameObject("WaveBannerText");
                textObj.transform.SetParent(bannerObj.transform, false);
                TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
                txt.alignment = TextAlignmentOptions.Center;
                txt.fontSize = 44f;
                txt.color = new Color(1f, 0.85f, 0.2f);
                RectTransform tr = txt.rectTransform;
                tr.anchorMin = Vector2.zero;
                tr.anchorMax = Vector2.one;
                tr.offsetMin = Vector2.zero;
                tr.offsetMax = Vector2.zero;

                GameObject vignetteObj = new GameObject("Vignette");
                vignetteObj.transform.SetParent(announcerObj.transform, false);
                Image vignette = vignetteObj.AddComponent<Image>();
                vignette.color = new Color(0.6f, 0f, 0f, 0f);
                RectTransform vr = vignette.rectTransform;
                vr.anchorMin = Vector2.zero;
                vr.anchorMax = Vector2.one;
                vr.offsetMin = Vector2.zero;
                vr.offsetMax = Vector2.zero;

                SetSerializedField(wa, "banner", br);
                SetSerializedField(wa, "bannerText", txt);
                SetSerializedField(wa, "vignette", vignette);
            }
        }

        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i].GetComponent<EnemyVisuals>() == null)
            {
                enemies[i].gameObject.AddComponent<EnemyVisuals>();
            }
        }
    }

    private static TextMeshProUGUI FindText(string name)
    {
        TextMeshProUGUI[] texts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == name)
            {
                return texts[i];
            }
        }

        return null;
    }

    private static Image FindImage(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name)
            {
                return all[i].GetComponent<Image>();
            }
        }

        return null;
    }

    private static void SetSerializedField(object target, string fieldName, object value)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var field = target.GetType().GetField(fieldName, flags);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }
}
