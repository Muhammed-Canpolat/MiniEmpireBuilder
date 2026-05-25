// [PULSE ON UI] — Mini Empire Builder
using UnityEngine;

public class PulseOnUi : MonoBehaviour
{
    [SerializeField] private float duration = 1.5f;
    [SerializeField] private float maxScale = 1.05f;

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void Update()
    {
        float t = (Mathf.Sin((Time.time / duration) * Mathf.PI * 2f) + 1f) * 0.5f;
        transform.localScale = Vector3.Lerp(baseScale, baseScale * maxScale, t);
    }
}
