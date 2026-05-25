using UnityEngine;
using System.Collections;
using DG.Tweening;
using TMPro;

/// <summary>
/// Savaş sahnesi görsel efekt yöneticisi.
/// Hasar sayıları, dalga banner, parçacıklar, projectile, screen shake.
/// </summary>
public class BattleVfxManager : MonoBehaviour
{
    public static BattleVfxManager Instance { get; private set; }

    // Hasar eşiği — bu üstü screen shake tetikler
    private const float ShakeThreshold = 20f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ══════════════════════════════════════════════════════════════
    // HASAR SAYILARI
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Düşman üzerinde hasar sayısı göster.
    /// baseDamage: normal hasar referansı (kritik hesabı için)
    /// </summary>
    public void ShowDamageNumber(Vector3 worldPos, float damage, float baseDamage, bool miss = false)
    {
        GameObject obj = new GameObject("DmgNum");
        obj.transform.position = worldPos + Vector3.up * 0.3f;

        // Canvas — World Space
        Canvas c = obj.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 50;
        obj.AddComponent<UnityEngine.UI.CanvasScaler>();

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(2f, 0.8f);
        rt.localScale = Vector3.one * 0.012f;

        // TMP metni
        GameObject txtObj = new GameObject("Txt");
        txtObj.transform.SetParent(obj.transform, false);
        RectTransform trt = txtObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;

        if (miss)
        {
            tmp.text = "MISS";
            tmp.fontSize = 28f;
            tmp.color = new Color(0.6f, 0.6f, 0.6f);
        }
        else
        {
            bool isCrit = damage >= baseDamage * 1.5f;
            if (isCrit)
            {
                tmp.text = Mathf.RoundToInt(damage).ToString();
                tmp.fontSize = 34f;
                tmp.color = new Color32(0xFF, 0x8C, 0x00, 0xFF); // #FF8C00
                tmp.fontStyle = FontStyles.Bold;
            }
            else
            {
                tmp.text = Mathf.RoundToInt(damage).ToString();
                tmp.fontSize = 28f;
                tmp.color = Color.white;
            }
        }

        // Yukarı float + fade out
        obj.transform.DOMove(worldPos + Vector3.up * 1.2f, 1f).SetEase(Ease.OutCubic);
        CanvasGroup cg = obj.AddComponent<CanvasGroup>();
        cg.DOFade(0f, 0.7f).SetDelay(0.4f);
        Destroy(obj, 1.2f);
    }

    // ══════════════════════════════════════════════════════════════
    // ÖLÜM EFEKTİ (parçacık)
    // ══════════════════════════════════════════════════════════════

    public void SpawnDeathParticles(Vector3 worldPos, Color baseColor)
    {
        int count = 5;
        for (int i = 0; i < count; i++)
        {
            GameObject p = new GameObject("DeathParticle");
            p.transform.position = worldPos;

            SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = MakeCircleSprite(8);
            sr.color = (i % 2 == 0) ? baseColor : new Color(0.4f, 0.15f, 0.05f);
            sr.sortingOrder = 10;

            float scale = Random.Range(0.06f, 0.14f);
            p.transform.localScale = Vector3.one * scale;

            Vector2 dir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(0.4f, 0.9f);
            Vector3 target = worldPos + new Vector3(dir.x * dist, dir.y * dist, 0);

            float dur = Random.Range(0.3f, 0.55f);
            p.transform.DOMove(target, dur).SetEase(Ease.OutQuad);
            sr.DOFade(0f, dur).SetDelay(0.1f);
            Destroy(p, dur + 0.15f);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // DALGA BANNER
    // ══════════════════════════════════════════════════════════════

    public void ShowWaveBanner(int waveNumber)
    {
        StartCoroutine(WaveBannerRoutine(waveNumber));
    }

    private IEnumerator WaveBannerRoutine(int waveNumber)
    {
        // Canvas oluştur
        GameObject canvasObj = new GameObject("WaveBanner");
        Canvas cv = canvasObj.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 200;
        UnityEngine.UI.CanvasScaler cs = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        cs.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080, 1920);
        cs.matchWidthOrHeight = 1f;
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Koyu panel
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(canvasObj.transform, false);
        RectTransform prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.05f, 0.42f);
        prt.anchorMax = new Vector2(0.95f, 0.58f);
        prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
        UnityEngine.UI.Image pImg = panel.AddComponent<UnityEngine.UI.Image>();
        pImg.color = new Color(0f, 0f, 0f, 0.75f);

        // Altın çizgi üst
        GameObject line = new GameObject("GoldLine");
        line.transform.SetParent(panel.transform, false);
        RectTransform lrt = line.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0.94f); lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        line.AddComponent<UnityEngine.UI.Image>().color = new Color(1f, 0.85f, 0.2f, 0.9f);

        // Altın çizgi alt
        GameObject line2 = new GameObject("GoldLine2");
        line2.transform.SetParent(panel.transform, false);
        RectTransform l2rt = line2.AddComponent<RectTransform>();
        l2rt.anchorMin = Vector2.zero; l2rt.anchorMax = new Vector2(1f, 0.06f);
        l2rt.offsetMin = Vector2.zero; l2rt.offsetMax = Vector2.zero;
        line2.AddComponent<UnityEngine.UI.Image>().color = new Color(1f, 0.85f, 0.2f, 0.9f);

        // Metin
        GameObject txtObj = new GameObject("WaveText");
        txtObj.transform.SetParent(panel.transform, false);
        RectTransform trt = txtObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = $"DALGA {waveNumber}";
        tmp.fontSize = 72f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 6f;

        // Giriş animasyonu: scale 0 → 1.2 → 1
        panel.transform.localScale = Vector3.zero;
        panel.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);

        yield return new WaitForSeconds(2f);

        // Çıkış: yukarı kayarak
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.DOAnchorPosY(panelRt.anchoredPosition.y + 300f, 0.4f).SetEase(Ease.InCubic);
        CanvasGroup cg = panel.AddComponent<CanvasGroup>();
        cg.DOFade(0f, 0.4f);

        yield return new WaitForSeconds(0.45f);
        Destroy(canvasObj);
    }

    // ══════════════════════════════════════════════════════════════
    // SCREEN SHAKE
    // ══════════════════════════════════════════════════════════════

    public void TryScreenShake(float damage)
    {
        if (damage < ShakeThreshold) return;
        if (Camera.main == null) return;

        Camera.main.transform.DOShakePosition(0.2f, strength: 0.15f, vibrato: 10, randomness: 90f);
    }

    // ══════════════════════════════════════════════════════════════
    // SLASH EFEKTİ (Baltacı)
    // ══════════════════════════════════════════════════════════════

    public void SpawnSlash(Vector3 pos)
    {
        GameObject slash = new GameObject("SlashFx");
        slash.transform.position = pos;
        slash.transform.localScale = new Vector3(0.6f, 0.2f, 1f);
        slash.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-30f, 30f));

        SpriteRenderer sr = slash.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite(16);
        sr.color = new Color(0.85f, 0.85f, 0.85f, 0.9f); // #cccccc
        sr.sortingOrder = 15;

        sr.DOFade(0f, 0.15f).SetEase(Ease.InQuad);
        Destroy(slash, 0.2f);
    }

    // ══════════════════════════════════════════════════════════════
    // KIVILCIM (SPARK) EFEKTİ (Yakın Saldırı / Balta)
    // ══════════════════════════════════════════════════════════════

    public void SpawnSpark(Vector3 pos)
    {
        for (int i = 0; i < 4; i++)
        {
            GameObject p = new GameObject("SparkParticle");
            p.transform.position = pos;

            SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSquareSprite(); // İnce uzun çizgi yapmak için kareyi scale edeceğiz
            sr.color = Color.white;
            sr.sortingOrder = 25;

            // Çizgi şekli
            p.transform.localScale = new Vector3(0.05f, 0.2f, 1f);

            // Rastgele fırlama yönü ve dönüşü
            Vector2 dir = Random.insideUnitCircle.normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            p.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

            float dist = Random.Range(0.2f, 0.4f);
            Vector3 target = pos + new Vector3(dir.x * dist, dir.y * dist, 0);

            p.transform.DOMove(target, 0.15f).SetEase(Ease.OutExpo);
            sr.DOFade(0f, 0.15f);
            Destroy(p, 0.2f);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // KÜÇÜK TOZ EFEKTİ (Uzak Saldırı İsabet / Mızrak / Ok)
    // ══════════════════════════════════════════════════════════════

    public void SpawnSmallDust(Vector3 pos)
    {
        for (int i = 0; i < 3; i++)
        {
            GameObject p = new GameObject("SmallDust");
            p.transform.position = pos;

            SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = MakeCircleSprite(8);
            sr.color = new Color(0.7f, 0.65f, 0.55f, 0.8f);
            sr.sortingOrder = 22;

            float s = Random.Range(0.08f, 0.15f);
            p.transform.localScale = Vector3.one * s;

            Vector2 dir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(0.1f, 0.25f);
            p.transform.DOMove(pos + new Vector3(dir.x * dist, dir.y * dist, 0), 0.25f).SetEase(Ease.OutQuad);
            sr.DOFade(0f, 0.25f);
            Destroy(p, 0.3f);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // ESKI API (geriye uyumluluk — BattleManager ve EnemyController kullanıyor olabilir)
    // ══════════════════════════════════════════════════════════════

    public void SpawnHit(Vector3 position, float scale = 0.7f)
    {
        SpawnSpark(position);
    }

    public void SpawnDeath(Vector3 position, float scale = 0.9f)
    {
        SpawnDeathParticles(position, new Color(0.8f, 0.15f, 0.1f));
    }

    public void SpawnBuild(Vector3 position, float scale = 0.6f)
    {
        SpawnSmallDust(position);
    }

    // ══════════════════════════════════════════════════════════════
    // YARDIMCI — Sprite oluşturma
    // ══════════════════════════════════════════════════════════════

    private Sprite MakeCircleSprite(int radius)
    {
        int size = radius * 2;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[size * size];
        Vector2 center = new Vector2(radius - 0.5f, radius - 0.5f);
        for (int i = 0; i < px.Length; i++)
        {
            int x = i % size, y = i / size;
            float dist = Vector2.Distance(new Vector2(x, y), center);
            px[i] = dist <= radius ? Color.white : Color.clear;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Sprite MakeSquareSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }
}
