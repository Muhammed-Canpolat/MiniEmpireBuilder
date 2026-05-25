// [DAMAGE NUMBER] — Mini Empire Builder
using System.Collections;
using TMPro;
using UnityEngine;

public class DamageNumber : MonoBehaviour
{
    [SerializeField] private TextMeshPro text;

    private Coroutine playRoutine;

    /// <summary>
    /// Plays floating damage number animation.
    /// </summary>
    public void Play(float damage, bool isCritical)
    {
        if (text == null)
        {
            text = GetComponent<TextMeshPro>();
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(PlayRoutine(damage, isCritical));
    }

    private IEnumerator PlayRoutine(float damage, bool isCritical)
    {
        text.text = Mathf.RoundToInt(damage).ToString();
        text.color = isCritical ? Color.yellow : Color.white;
        text.fontSize = isCritical ? 7f : 5f;

        Vector3 start = transform.position;
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.6f);
            transform.position = start + Vector3.up * p;

            Color c = text.color;
            c.a = 1f - p;
            text.color = c;

            if (isCritical)
            {
                transform.localPosition += (Vector3)Random.insideUnitCircle * 0.01f;
            }

            yield return null;
        }

        gameObject.SetActive(false);
    }
}
