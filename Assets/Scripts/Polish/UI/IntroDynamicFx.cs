// [INTRO DYNAMIC FX] — Mini Empire Builder
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class IntroDynamicFx : MonoBehaviour
{
    private const float IntroFadeDuration = 2f;
    private const float SkipTouchWindow = 2f;

    [SerializeField] private CanvasGroup blackFade;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI narrativeText;
    [SerializeField] private RectTransform continueButton;
    [SerializeField] private Transform parallaxBackLayer;
    [SerializeField] private Transform parallaxFrontLayer;
    [SerializeField] private string nextSceneName = "BaseScene";

    private Vector3 backStart;
    private Vector3 frontStart;
    private float elapsed;
    private bool skipTriggered;

    private void Start()
    {
        if (parallaxBackLayer != null) backStart = parallaxBackLayer.localPosition;
        if (parallaxFrontLayer != null) frontStart = parallaxFrontLayer.localPosition;

        PlayIntro();
        PlayPulse();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        Vector2 look = ReadLookInput();
        if (parallaxBackLayer != null)
            parallaxBackLayer.localPosition = backStart + new Vector3(look.x, look.y, 0f) * 4f;

        if (parallaxFrontLayer != null)
            parallaxFrontLayer.localPosition = frontStart + new Vector3(look.x, look.y, 0f) * 8f;

        if (!skipTriggered && elapsed < SkipTouchWindow && IsPrimaryPressed())
        {
            skipTriggered = true;
            SceneManager.LoadScene(nextSceneName);
        }
    }

    /// <summary>
    /// Handles continue action for UI button click.
    /// </summary>
    public void ContinueToNextScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }

    private void PlayIntro()
    {
        Sequence seq = DOTween.Sequence();

        if (blackFade != null)
        {
            blackFade.alpha = 1f;
            seq.Append(blackFade.DOFade(0f, IntroFadeDuration));
        }

        if (titleText != null)
        {
            titleText.transform.localScale = Vector3.one * 0.8f;
            seq.Append(titleText.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
        }

        if (narrativeText != null)
        {
            string full = narrativeText.text;
            narrativeText.text = string.Empty;
            float duration = Mathf.Max(0.4f, full.Length * 0.04f);
            int charCount = 0;
            seq.Append(DOTween.To(() => charCount, x =>
            {
                charCount = x;
                int safeCount = Mathf.Clamp(charCount, 0, full.Length);
                narrativeText.text = full.Substring(0, safeCount);
            }, full.Length, duration).SetEase(Ease.Linear));
        }
    }

    private void PlayPulse()
    {
        if (continueButton == null)
            return;

        continueButton.DOScale(1.05f, 0.75f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private static bool IsPrimaryPressed()
    {
        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool touchPressed = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        return mousePressed || touchPressed;
    }

    private static Vector2 ReadLookInput()
    {
        if (Touchscreen.current != null)
        {
            Vector2 pos = Touchscreen.current.primaryTouch.position.ReadValue();
            return new Vector2((pos.x / Screen.width) - 0.5f, (pos.y / Screen.height) - 0.5f);
        }

        Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        return new Vector2((mouse.x / Screen.width) - 0.5f, (mouse.y / Screen.height) - 0.5f);
    }
}
