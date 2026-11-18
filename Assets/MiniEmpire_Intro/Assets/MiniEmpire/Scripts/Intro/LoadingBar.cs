using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MiniEmpire.Intro
{
    /// <summary>
    /// Yükleme çubuğu: fill görselini (Image Type = Filled, Horizontal) ve "%75" yazısını günceller.
    /// Gerçek ilerleme değerine doğru yumuşak (smooth) şekilde yaklaşır.
    /// </summary>
    public class LoadingBar : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private Image fillImage;      // loading_bar_fill
        [SerializeField] private TMP_Text percentText; // "%75"

        [Header("Ayarlar")]
        [SerializeField, Min(0.1f)] private float smoothSpeed = 1.5f; // saniyede kaç birim (0-1)

        public float DisplayedProgress { get; private set; }
        public bool IsFull => DisplayedProgress >= 0.999f;

        private float _target;

        private void Awake()
        {
            if (fillImage != null)
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
            Apply(0f);
        }

        /// <summary>Hedef ilerleme (0..1).</summary>
        public void SetTarget(float value01) => _target = Mathf.Clamp01(value01);

        private void Update()
        {
            if (Mathf.Approximately(DisplayedProgress, _target)) return;
            DisplayedProgress = Mathf.MoveTowards(DisplayedProgress, _target, smoothSpeed * Time.unscaledDeltaTime);
            Apply(DisplayedProgress);
        }

        private void Apply(float v)
        {
            if (fillImage != null) fillImage.fillAmount = v;
            if (percentText != null) percentText.text = "%" + Mathf.RoundToInt(v * 100f);
        }
    }
}
