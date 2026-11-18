using UnityEngine;

namespace MiniEmpire.Intro
{
    /// <summary>Logo/şövalye gibi UI öğelerine hafif nefes alma (pulse) ve süzülme animasyonu verir.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIIdleAnimation : MonoBehaviour
    {
        [SerializeField] private float pulseAmount = 0.03f;
        [SerializeField] private float pulseSpeed = 1.6f;
        [SerializeField] private float floatPixels = 6f;
        [SerializeField] private float floatSpeed = 1.2f;

        private RectTransform _rt;
        private Vector3 _baseScale;
        private Vector2 _basePos;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _baseScale = _rt.localScale;
            _basePos = _rt.anchoredPosition;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            _rt.localScale = _baseScale * (1f + Mathf.Sin(t * pulseSpeed) * pulseAmount);
            _rt.anchoredPosition = _basePos + new Vector2(0f, Mathf.Sin(t * floatSpeed) * floatPixels);
        }
    }
}
