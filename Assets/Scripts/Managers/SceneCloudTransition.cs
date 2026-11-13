using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ana Üs ↔ Ada bulut geçişi (FeatureConfig.cloudTransition). DontDestroyOnLoad, ekran-uzayı overlay: Tiny Swords bulutları
/// (IslandVisualCatalog.clouds) kenarlardan kayıp ekranı kapatır, sahne LoadSceneAsync ile yüklenir, kurulum birkaç kare
/// sürdükten sonra bulutlar açılır ve nesne kendini yok eder. DOTween kullanılmaz (yüklemeden önce KillAll çağrılır):
/// tüm hareket kendi coroutine'inde, unscaled zamanla. Bulut aralarını yumuşak bulut rengi bir zemin doldurur
/// (mavi/siyah parlaması olmaz).
/// </summary>
public sealed class SceneCloudTransition : MonoBehaviour
{
    private static readonly Color Backdrop = new Color(0.93f, 0.96f, 0.99f, 1f);
    private const int CloudCount = 18;

    public static SceneCloudTransition Active { get; private set; }
    /// <summary>Test/telemetri: tamamlanan geçiş sayısı.</summary>
    public static int CompletedCount { get; private set; }

    private struct Piece { public RectTransform rect; public Vector2 home, away; }
    private Image backdrop;
    private Piece[] pieces;

    /// <summary>Bayrak açıksa geçişi başlatır (true); kapalıysa ya da zaten çalışıyorsa false (çağıran eski kararmaya düşer).</summary>
    public static bool BeginIfEnabled(string sceneName)
    {
        if (!FeatureConfig.Instance.cloudTransition || Active != null) return false;
        var go = new GameObject("SceneCloudTransition");
        DontDestroyOnLoad(go);
        Active = go.AddComponent<SceneCloudTransition>();
        Active.StartCoroutine(Active.Run(sceneName));
        return true;
    }

    /// <summary>
    /// Sahne yüklemeden bulut geçişi (ör. Akın sonucu → ada görünümü): bulutlar örter, <paramref name="midpoint"/> çalışır,
    /// <paramref name="holdSeconds"/> (gerçek zaman) beklenir (kamera kayması örtünün altında biter), bulutlar açılır.
    /// Bayrak kapalıysa ya da başka geçiş sürüyorsa false (çağıran işi kendisi doğrudan yapar).
    /// </summary>
    public static bool BeginInPlace(System.Action midpoint, float holdSeconds)
    {
        if (!FeatureConfig.Instance.cloudTransition || Active != null) return false;
        var go = new GameObject("SceneCloudTransition");
        Active = go.AddComponent<SceneCloudTransition>();
        Active.StartCoroutine(Active.RunInPlace(midpoint, holdSeconds));
        return true;
    }

    private IEnumerator RunInPlace(System.Action midpoint, float holdSeconds)
    {
        var config = FeatureConfig.Instance;
        BuildOverlay();
        float cover = Mathf.Max(0.05f, config.cloudCoverSeconds);
        yield return Animate(cover, k =>
        {
            Place(k);
            backdrop.color = new Color(Backdrop.r, Backdrop.g, Backdrop.b, Mathf.Clamp01((k - 0.35f) / 0.5f));
        });
        Place(1f);
        backdrop.color = Backdrop;
        try { midpoint?.Invoke(); }
        catch (System.Exception e) { Debug.LogException(e); }
        float until = Time.unscaledTime + Mathf.Max(0f, holdSeconds);
        while (Time.unscaledTime < until) yield return null;
        yield return null;
        float reveal = Mathf.Max(0.05f, config.cloudRevealSeconds);
        yield return Animate(reveal, k =>
        {
            Place(1f - k);
            backdrop.color = new Color(Backdrop.r, Backdrop.g, Backdrop.b, 1f - Mathf.Clamp01((k - 0.1f) / 0.5f));
        });
        Finish();
    }

    private void OnDestroy() { if (Active == this) Active = null; }

    private IEnumerator Run(string sceneName)
    {
        var config = FeatureConfig.Instance;
        BuildOverlay();
        float cover = Mathf.Max(0.05f, config.cloudCoverSeconds);
        yield return Animate(cover, k =>
        {
            Place(k);
            backdrop.color = new Color(Backdrop.r, Backdrop.g, Backdrop.b, Mathf.Clamp01((k - 0.35f) / 0.5f));
        });
        Place(1f);
        backdrop.color = Backdrop;

        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null) { Finish(); yield break; }
        op.allowSceneActivation = false;
        float started = Time.unscaledTime;
        while (op.progress < 0.9f && Time.unscaledTime - started < config.cloudMaxWaitSeconds) yield return null;
        DG.Tweening.DOTween.KillAll();   // eski sahnenin tween'leri yok edilen nesnelere dokunmasın
        op.allowSceneActivation = true;
        while (!op.isDone && Time.unscaledTime - started < config.cloudMaxWaitSeconds) yield return null;
        for (int i = 0; i < Mathf.Max(1, config.cloudPostLoadFrames); i++) yield return null;
        // Ada kurulumu (Start + bir kare bekleme) bitene dek bekle
        while (IslandSession.Active && sceneName == "BattleScene"
            && (IslandSceneSetup.Instance == null || !IslandSceneSetup.Instance.Ready)
            && Time.unscaledTime - started < config.cloudMaxWaitSeconds) yield return null;
        yield return null;

        float reveal = Mathf.Max(0.05f, config.cloudRevealSeconds);
        yield return Animate(reveal, k =>
        {
            Place(1f - k);
            backdrop.color = new Color(Backdrop.r, Backdrop.g, Backdrop.b, 1f - Mathf.Clamp01((k - 0.1f) / 0.5f));
        });
        Finish();
    }

    private void Finish()
    {
        CompletedCount++;
        Destroy(gameObject);
    }

    private static IEnumerator Animate(float seconds, System.Action<float> step)
    {
        for (float t = 0f; t < seconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
        {
            step(Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        step(1f);
    }

    /// <summary>k=0: bulutlar ekran dışında; k=1: yerlerinde (ekranı kapatır).</summary>
    private void Place(float k)
    {
        foreach (var piece in pieces)
            if (piece.rect != null) piece.rect.anchoredPosition = Vector2.LerpUnclamped(piece.away, piece.home, k);
    }

    private void BuildOverlay()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        var backdropObject = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdropObject.transform.SetParent(transform, false);
        var rect = (RectTransform)backdropObject.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        backdrop = backdropObject.GetComponent<Image>();
        backdrop.color = new Color(Backdrop.r, Backdrop.g, Backdrop.b, 0f);
        backdrop.raycastTarget = true;   // geçiş sırasında dokunuşları yut

        float w = Mathf.Max(1f, Screen.width), h = Mathf.Max(1f, Screen.height);
        var sprites = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.clouds : null;
        int usable = 0;
        if (sprites != null) foreach (var s in sprites) if (s != null) usable++;
        if (usable == 0) { pieces = new Piece[0]; return; }

        // 3 sütun × 6 satır ızgara + sapma; yan sütunlar yandan, orta sütun alttan/üstten girer.
        var list = new System.Collections.Generic.List<Piece>();
        var rng = new System.Random(7);
        int cols = 3, rows = 6, index = 0;
        float cellW = w / cols, cellH = h / rows;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++, index++)
            {
                if (list.Count >= CloudCount) break;
                Sprite sprite = null;
                for (int tries = 0; sprite == null && tries < sprites.Length; tries++) sprite = sprites[(index + tries) % sprites.Length];
                var go = new GameObject("Cloud" + index, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var image = go.GetComponent<Image>();
                image.sprite = sprite; image.raycastTarget = false; image.preserveAspect = true;
                float width = cellW * (1.9f + (float)rng.NextDouble() * 0.5f);
                float height = width * sprite.rect.height / Mathf.Max(1f, sprite.rect.width);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(width, height);
                var home = new Vector2((c + 0.5f) * cellW - w * 0.5f + ((float)rng.NextDouble() - 0.5f) * cellW * 0.4f,
                                       (r + 0.5f) * cellH - h * 0.5f + ((float)rng.NextDouble() - 0.5f) * cellH * 0.4f);
                Vector2 away;
                if (c == 0) away = new Vector2(-w * 0.5f - width, home.y);
                else if (c == cols - 1) away = new Vector2(w * 0.5f + width, home.y);
                else away = new Vector2(home.x, r < rows / 2 ? -h * 0.5f - height : h * 0.5f + height);
                list.Add(new Piece { rect = rt, home = home, away = away });
            }
        pieces = list.ToArray();
    }
}
