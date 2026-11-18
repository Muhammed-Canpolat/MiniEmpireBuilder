using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Intro arka planı ekran oranına göre: referanstan (9:16) dar/uzun ekranlarda (9:19.5, 9:21) ekranı kaplar (Envelope, yanlar
/// kırpılır, logo tam kalır); daha geniş ekranlarda (tablet 3:4, 10:16) görselin tamamı sığdırılır (FitInParent) ve kalan
/// kenarlar arkadaki karartılmış kaplayan kopya (BackgroundFill) ile dolar: siyah bant yok, logo/çubuk alanı kırpılmaz.
/// </summary>
[RequireComponent(typeof(AspectRatioFitter))]
public sealed class IntroBackgroundFit : MonoBehaviour
{
    private AspectRatioFitter fitter;
    private RectTransform parent;
    private float lastAspect = -1f;

    private void Awake()
    {
        fitter = GetComponent<AspectRatioFitter>();
        parent = transform.parent as RectTransform;
        Apply();
    }

    private void Update() => Apply();

    private void Apply()
    {
        if (fitter == null || parent == null) return;
        var size = parent.rect.size;
        if (size.y <= 0f) return;
        float aspect = size.x / size.y;
        if (Mathf.Approximately(aspect, lastAspect)) return;
        lastAspect = aspect;
        fitter.aspectMode = aspect > fitter.aspectRatio + 0.005f
            ? AspectRatioFitter.AspectMode.FitInParent
            : AspectRatioFitter.AspectMode.EnvelopeParent;
    }
}
