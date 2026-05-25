using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// İnşaatçının fiziksel olarak haritada gezinmesi ve kulübesinde uyumasını yönetir.
/// </summary>
public class BuilderVisualController : MonoBehaviour
{
    private Transform hutTransform;
    private bool isSleeping = false;
    private GameObject zzzEffect;

    private void Start()
    {
        // 1. İnşaatçı Binasını Oluştur (Blue Buildings/House1.png = building_carpenter)
        GameObject hut = new GameObject("BuilderHut");
        hutTransform = hut.transform;

        // Ana üssün sol köşesine yerleştir
        float cx = BaseSceneSetup.Instance?.width / 2f ?? 12f;
        float cy = BaseSceneSetup.Instance?.height / 2f ?? 12f;
        hut.transform.position = new Vector3(cx - 4f, cy + 2f, 0f);
        hut.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

        SpriteRenderer sr = hut.AddComponent<SpriteRenderer>();
        // WallBuilder → building_carpenter → Blue Buildings/House1.png
        sr.sprite = SpriteManager.Instance?.WallBuilder;
        if (sr.sprite == null) sr.sprite = BaseMapManager.MakeSquareSprite(new Color(0.25f, 0.45f, 0.75f));
        sr.sortingOrder = 4;

        // Kulübenin altına etiket ekle
        AddHutLabel(hutTransform);

        // 2. İnşaatçı Karakter Avatarini Ayarla (Avatars_04.png = ui_builder_avatar)
        SpriteRenderer builderSr = gameObject.AddComponent<SpriteRenderer>();
        // Önce ui_builder_avatar, yoksa hero sprite kullan
        builderSr.sprite = SpriteManager.Instance?.GetSprite("ui_builder_avatar");
        if (builderSr.sprite == null)
            builderSr.sprite = SpriteManager.Instance?.HeroAxe;
        if (builderSr.sprite == null)
            builderSr.sprite = BaseMapManager.MakeCircleSprite(new Color(0.3f, 0.6f, 1f));

        builderSr.color = Color.white; // Orijinal renk
        builderSr.sortingOrder = 14;
        transform.localScale = new Vector3(0.65f, 0.65f, 1f);

        CreateZzZ();

        if (BuilderSystem.Instance != null)
        {
            BuilderSystem.Instance.OnStatusChanged += UpdateVisuals;
            UpdateVisuals();
        }
    }

    private void OnDestroy()
    {
        if (BuilderSystem.Instance != null)
        {
            BuilderSystem.Instance.OnStatusChanged -= UpdateVisuals;
        }
    }

    private void CreateZzZ()
    {
        zzzEffect = new GameObject("ZzzText");
        zzzEffect.transform.SetParent(hutTransform);
        // Bina üstünde, yakın konumda
        zzzEffect.transform.localPosition = new Vector3(0.3f, 0.55f, 0f);

        var canvas = zzzEffect.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;
        var crt = zzzEffect.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(1.5f, 0.6f);
        crt.localScale = Vector3.one * 0.09f;

        var txt = zzzEffect.AddComponent<TextMeshProUGUI>();
        txt.text = "zzZ";
        txt.color = new Color(0.85f, 0.9f, 1f);
        txt.fontSize = 5f;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;

        // Hafif yukarı sürükleme
        zzzEffect.transform.DOLocalMoveY(0.75f, 1.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        txt.DOFade(0.25f, 1.8f).SetLoops(-1, LoopType.Yoyo);
    }

    private void AddHutLabel(Transform parent)
    {
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent, false);
        labelObj.transform.localPosition = new Vector3(0f, -0.65f, 0f);

        Canvas c = labelObj.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 10;

        RectTransform crt = labelObj.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(160f, 30f);
        crt.localScale = Vector3.one * 0.008f;

        GameObject bgGO = new GameObject("BG");
        bgGO.transform.SetParent(labelObj.transform, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(labelObj.transform, false);
        RectTransform txtRT = textGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "İnşaatçı Binası";
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.9f, 0.9f, 0.9f);
    }


    private void UpdateVisuals()
    {
        if (BuilderSystem.Instance == null) return;

        var b = BuilderSystem.Instance.Builders[0];

        if (b.isAvailable)
        {
            // Müsaitse kulübeye dön ve uyu
            if (!isSleeping)
            {
                transform.DOKill(); // Önceki hareketi iptal et
                gameObject.GetComponent<SpriteRenderer>().enabled = true; // Yolda görünür ol

                transform.DOMove(hutTransform.position, 2.5f).SetSpeedBased().OnComplete(() =>
                {
                    // Kulübeye ulaştı, gizlen ve uyumaya başla
                    gameObject.GetComponent<SpriteRenderer>().enabled = false;
                    zzzEffect.SetActive(true);
                    isSleeping = true;
                });
            }
        }
        else
        {
            // Çalışıyorsa hedef konuma git
            gameObject.GetComponent<SpriteRenderer>().enabled = true;
            zzzEffect.SetActive(false);
            isSleeping = false;

            transform.DOKill();

            // Eğer hedef konum Vector3.zero ise (kayıttan yüklenmiş ve hedef bilinmiyorsa) 
            // şimdilik sadece kulübenin önünde çalışsın.
            Vector3 target = b.targetPosition != Vector3.zero ? b.targetPosition : hutTransform.position + Vector3.down;

            transform.DOMove(target, 2.5f).SetSpeedBased().OnComplete(() =>
            {
                // Hedefe ulaştı, çalışma animasyonu (ufak zıplamalar) yap
                transform.DOJump(target, 0.2f, 1, 0.5f).SetLoops(-1);
            });
        }
    }
}
