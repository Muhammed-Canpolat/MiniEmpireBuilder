using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniEmpire.UI
{
    /// <summary>
    /// One resource pill on the top bar (gold, wood, meat, gem, builders...).
    /// The pill background is a 9-sliced sprite driven by a HorizontalLayoutGroup,
    /// so it grows/shrinks with the text. Long numbers switch to compact form automatically.
    /// </summary>
    public class ResourceCounter : MonoBehaviour
    {
        [Header("References")]
        public Image icon;
        public TMP_Text valueText;

        [Header("Formatting")]
        public NumberStyle style = NumberStyle.Auto;
        [Tooltip("Auto style: numbers longer than this (including separators) become compact, e.g. 123.456 -> 123B")]
        public int maxChars = 5;
        public bool turkish = true;

        [Header("Animation")]
        public bool animateChanges = true;
        public float animDuration = 0.35f;
        public bool punchOnChange = true;

        long shown;
        Coroutine routine;
        Vector3 baseScale = Vector3.one;

        void Awake() { baseScale = transform.localScale; }

        public void SetIcon(Sprite sprite)
        {
            if (icon == null) return;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
        }

        /// <summary>Plain resource value (e.g. gold).</summary>
        public void SetValue(long value, bool instant = false)
        {
            if (routine != null) StopCoroutine(routine);
            if (instant || !animateChanges || !isActiveAndEnabled || value == shown)
            {
                shown = value;
                Write(Format(value));
                return;
            }
            routine = StartCoroutine(Animate(shown, value));
        }

        /// <summary>"current/max" style, e.g. builders 2/2.</summary>
        public void SetValue(long current, long max)
        {
            if (routine != null) StopCoroutine(routine);
            shown = current;
            Write(Format(current) + "/" + Format(max));
        }

        /// <summary>Any custom text ("FULL", "∞", timers...).</summary>
        public void SetText(string text)
        {
            if (routine != null) StopCoroutine(routine);
            Write(text);
        }

        string Format(long v) => NumberFormatter.Format(v, style, maxChars, turkish);

        void Write(string s)
        {
            if (valueText == null) return;
            valueText.text = s;
            // let the layout group resize the pill this frame
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        IEnumerator Animate(long from, long to)
        {
            float t = 0f;
            while (t < animDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / animDuration);
                shown = (long)Mathf.Lerp(from, to, k);
                Write(Format(shown));
                if (punchOnChange) transform.localScale = baseScale * (1f + 0.08f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            shown = to;
            Write(Format(to));
            transform.localScale = baseScale;
            routine = null;
        }
    }
}
