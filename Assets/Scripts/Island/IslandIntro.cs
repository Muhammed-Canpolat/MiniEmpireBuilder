using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 6. oturum (3.1): adaya İLK girişte kısa tanıtım. Ekranı kaplayan bulutlar iki yana açılır (ada görünür), kâğıt kart üzerinde birkaç
/// cümlelik hikâye ve "DEVAM"; ardından "Adanızın adı ne olsun?" (IslandNameUi.Mode.First), ad onaylanınca/geçilince öğretici (3.2).
/// Bayrak: <see cref="IslandSaveData.introSeen"/> (kayıt v13). Mevcut kayıtta adaya daha önce girmiş oyuncuya gösterilmez (göç).
/// </summary>
public sealed class IslandIntro : MonoBehaviour
{
    public static IslandIntro Current { get; private set; }
    public static bool IsPlaying => Current != null;

    private readonly List<RectTransform> clouds = new List<RectTransform>();
    private readonly List<Vector2> cloudTargets = new List<Vector2>();
    private Image sky;
    private CanvasGroup card;
    private Button continueButton;
    private Action finished;
    public Button ContinueButton => continueButton;
    public bool CardShown => card != null && card.alpha > 0.9f;

    /// <summary>İlk giriş mi: tanıtım henüz izlenmedi.</summary>
    public static bool ShouldPlay(IslandSaveData save) => save != null && !save.introSeen;

    public static IslandIntro Play(IslandSaveData save, Action onFinished)
    {
        if (Current != null) return Current;
        var root = new GameObject("IslandIntro", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 330;
        ReferenceUiVisuals.ConfigureCanvas(root.GetComponent<CanvasScaler>());
        var intro = root.AddComponent<IslandIntro>();
        Current = intro;
        intro.finished = () =>
        {
            if (save != null) { save.introSeen = true; GameManager.Instance?.SaveGame(); }
            onFinished?.Invoke();
        };
        intro.Build(ReferenceUiVisuals.SafeArea(canvas.transform), (RectTransform)canvas.transform);
        intro.StartCoroutine(intro.Run());
        return intro;
    }

    private void Build(RectTransform safe, RectTransform full)
    {
        sky = ReferenceUiVisuals.Image(full, "Sky", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.93f, 0.96f, 0.99f, 1f));
        sky.raycastTarget = true;
        var sprites = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.clouds : null;
        int n = 0;
        for (int row = 0; row < 5; row++)
            for (int col = 0; col < 3; col++, n++)
            {
                Sprite sprite = null;
                if (sprites != null && sprites.Length > 0)
                    for (int t = 0; sprite == null && t < sprites.Length; t++) sprite = sprites[(n + t) % sprites.Length];
                var img = ReferenceUiVisuals.Image(full, "Cloud" + n, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, Color.white);
                img.sprite = sprite; img.preserveAspect = true; img.enabled = sprite != null;
                var rt = img.rectTransform;
                float w = 300f + (n % 3) * 40f;
                rt.sizeDelta = new Vector2(w, w * 0.55f);
                Vector2 start = new Vector2((col - 1) * 150f, (row - 2) * 170f + (col % 2) * 50f);
                rt.anchoredPosition = start;
                clouds.Add(rt);
                // Sol sütun sola, sağ sütun sağa, ortadakiler üst/alta açılır.
                Vector2 dir = col == 0 ? Vector2.left : col == 2 ? Vector2.right : (row < 2 ? Vector2.up : Vector2.down) + (row == 2 ? Vector2.left : Vector2.zero);
                cloudTargets.Add(start + dir.normalized * 900f);
            }

        var cardRt = ReferenceUiVisuals.Rect(safe, "StoryCard", new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.18f), new Vector2(-170, 0), new Vector2(170, 210));
        card = cardRt.gameObject.AddComponent<CanvasGroup>();
        card.alpha = 0f; card.interactable = false; card.blocksRaycasts = false;
        var paper = FancyInfoPanel.NineSlice(cardRt, "Paper", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "ui_paper", Color.white, 2.4f);
        paper.raycastTarget = true;
        var ribbon = FancyInfoPanel.NineSlice(cardRt, "Ribbon", new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0, -26), new Vector2(0, 22), "ui_ribbon", Color.white, 3f);
        ReferenceUiVisuals.Text(ribbon.transform, "Title", L.T("UFUKTAKİ ADA", "THE ISLAND ON THE HORIZON"), 17, Color.white,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -2), true);
        var body = ReferenceUiVisuals.Text(cardRt, "Story",
            L.T("Fırtına dindi, bulutlar çekildi. Kıyıda terk edilmiş bir kale ve bakir topraklar seni bekliyor.\nAma denizden akıncılar geliyor: önce kıyıyı savunacak okçuları yetiştir, sonra adanı büyüt.",
                "The storm is over and the clouds part. An abandoned keep and untouched land await you on the shore.\nBut raiders come from the sea: first train archers to guard the coast, then grow your island."),
            14, FancyInfoPanel.Ink, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(20, 64), new Vector2(-20, -30), false, true);
        body.textWrappingMode = TextWrappingModes.Normal;
        continueButton = FancyInfoPanel.TsButton(cardRt, "ContinueButton", L.T("DEVAM", "CONTINUE"), false,
            new Vector2(0.25f, 0f), new Vector2(0.75f, 0f), new Vector2(0, 12), new Vector2(0, 56), 18, out _);
        continueButton.onClick.AddListener(Finish);
    }

    private IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(0.4f);
        float t = 0f;
        var starts = new List<Vector2>();
        foreach (var c in clouds) starts.Add(c.anchoredPosition);
        while (t < 1.6f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.6f));
            for (int i = 0; i < clouds.Count; i++) clouds[i].anchoredPosition = Vector2.Lerp(starts[i], cloudTargets[i], k);
            sky.color = new Color(sky.color.r, sky.color.g, sky.color.b, 1f - k);
            yield return null;
        }
        sky.raycastTarget = true;
        sky.color = new Color(0f, 0f, 0f, 0.25f);   // kart okunurken ada hafif karartılır, dokunuş geçmez
        t = 0f;
        while (t < 0.35f)
        {
            t += Time.unscaledDeltaTime;
            card.alpha = Mathf.Clamp01(t / 0.35f);
            yield return null;
        }
        card.alpha = 1f; card.interactable = true; card.blocksRaycasts = true;
    }

    /// <summary>DEVAM (testler de çağırır): tanıtım biter, ad penceresine geçilir.</summary>
    public void Finish()
    {
        var done = finished;
        finished = null;
        Destroy(gameObject);
        done?.Invoke();
    }

    private void OnDestroy() { if (Current == this) Current = null; }
}
