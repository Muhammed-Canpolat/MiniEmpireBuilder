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
    private Animator workerAnimator;

    private void Start()
    {
        // 1. İnşaatçı Binasını Oluştur (Blue Buildings/House1.png = building_carpenter)
        GameObject hut = new GameObject("BuilderHut");
        hutTransform = hut.transform;

        // Ana üssün sol köşesine yerleştir
        hut.transform.position = BaseWorldLayout.Get("builder").center;

        SpriteRenderer sr = hut.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteManager.Instance?.BuilderHut;
        if (sr.sprite == null) sr.sprite = BaseMapManager.MakeSquareSprite(new Color(0.25f, 0.45f, 0.75f));
        sr.sortingOrder = 100 - Mathf.RoundToInt(hut.transform.position.y * 2f);
        float scale = 2.6f / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
        hut.transform.localScale = new Vector3(scale, scale, 1f);
        BaseMapManager.CreateBuildingShadow(hut.transform, new Vector2(2f, 2f),
            hut.transform.localScale, sr.sortingOrder - 1);
        var hutCollider = hut.AddComponent<BoxCollider2D>();
        hutCollider.size = sr.sprite.bounds.size;

        // Kulübenin altına etiket ekle
        BaseMapManager.CreateReadableLabel(hutTransform, "İnşaatçı", false,
            sr.sprite.bounds.extents.y * scale + 0.34f, scale, "Sev. 1");
        hutTransform.Find("ReadableLabel")?.gameObject.SetActive(false);

        // 2. İnşaatçı Karakter Avatarini Ayarla (Avatars_04.png = ui_builder_avatar)
        SpriteRenderer builderSr = gameObject.AddComponent<SpriteRenderer>();
        // Önce ui_builder_avatar, yoksa hero sprite kullan
        builderSr.sprite = SpriteManager.Instance?.GetSprite("ui_builder_avatar");
        if (builderSr.sprite == null)
            builderSr.sprite = SpriteManager.Instance?.HeroAxe;
        if (builderSr.sprite == null)
            builderSr.sprite = BaseMapManager.MakeCircleSprite(new Color(0.3f, 0.6f, 1f));

        builderSr.color = Color.white; // Orijinal renk
        builderSr.sortingOrder = 101 - Mathf.RoundToInt(transform.position.y * 2f);
        transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        var pawnController = BaseVisualCatalog.Instance?.pawnControllers?[0];
        if (pawnController != null)
        {
            workerAnimator = gameObject.AddComponent<Animator>();
            workerAnimator.runtimeAnimatorController = pawnController;
            workerAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            workerAnimator.Play("Pawn_Idle Hammer_Blue");
        }

        CreateZzZ();

        if (BuilderSystem.Instance != null)
        {
            BuilderSystem.Instance.OnStatusChanged += UpdateVisuals;
            UpdateVisuals();
        }
    }

    public void ShowHutLabel()
    {
        if (hutTransform == null) return;
        var label = hutTransform.Find("ReadableLabel");
        if (label == null) return;
        label.gameObject.SetActive(true);
        StartCoroutine(HideHutLabel(label.gameObject));
    }

    private System.Collections.IEnumerator HideHutLabel(GameObject label)
    {
        yield return new WaitForSeconds(2f);
        if (label != null) label.SetActive(false);
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
        ReferenceUiVisuals.ApplyFont(txt, 5f, new Color(0.85f, 0.9f, 1f, 0.9f), bodyBold: true);
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
                workerAnimator?.Play("Pawn_Run Hammer_Blue");
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
                workerAnimator?.Play("Pawn_Interact Hammer_Blue");
            });
            workerAnimator?.Play("Pawn_Run Hammer_Blue");
        }
    }
}
