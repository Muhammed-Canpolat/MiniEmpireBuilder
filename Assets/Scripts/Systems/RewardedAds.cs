using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ödüllü reklam girişi. Gerçek reklam SDK'sı (yol haritası 6. adım) bağlanana kadar
/// "deneme reklamı" gösterir: 3 sn'lik bir ekran, sonunda ödül verilir.
/// Tüm ödüllü reklam yerleri (kart, can, maden, inşaat) yalnız bu sınıfı çağırır;
/// SDK gelince yalnız <see cref="Show"/> değişir.
/// </summary>
public static class RewardedAds
{
    /// <summary>Gerçek SDK bağlı değil: deneme ekranı gösterilir.</summary>
    public static bool IsSimulated => true;
    public const float SimulatedSeconds = 3f;

    private static bool showing;

    /// <summary>Reklam gösterir; izlenip bitince done(true), kapatılır/başarısızsa done(false).</summary>
    public static void Show(string placement, Action<bool> done)
    {
        if (showing) return;
        showing = true;
        var runner = new GameObject("RewardedAd_" + placement).AddComponent<SimulatedAdScreen>();
        runner.Begin(placement, ok =>
        {
            showing = false;
            done?.Invoke(ok);
        });
    }

    private sealed class SimulatedAdScreen : MonoBehaviour
    {
        public void Begin(string placement, Action<bool> done) => StartCoroutine(Run(placement, done));

        private IEnumerator Run(string placement, Action<bool> done)
        {
            var canvasObject = new GameObject("AdCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            ReferenceUiVisuals.ConfigureCanvas(canvasObject.GetComponent<CanvasScaler>());
            var blocker = ReferenceUiVisuals.Image(canvas.transform, "AdBlocker", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0.03f, 0.02f, 0.02f, 0.94f));
            blocker.raycastTarget = true;
            ReferenceUiVisuals.Text(canvas.transform, "AdTitle", L.T("REKLAM", "AD"), 26, ReferenceUiVisuals.Gold,
                TextAlignmentOptions.Center, new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.62f), Vector2.zero, Vector2.zero, true);
            ReferenceUiVisuals.Text(canvas.transform, "AdNote", L.T("Deneme reklamı (gerçek reklam henüz bağlı değil)", "Test ad (real ads not connected yet)"), 13,
                ReferenceUiVisuals.Muted, TextAlignmentOptions.Center, new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.55f),
                Vector2.zero, Vector2.zero);
            var counter = ReferenceUiVisuals.Text(canvas.transform, "AdCounter", "", 20, ReferenceUiVisuals.Light,
                TextAlignmentOptions.Center, new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.48f), Vector2.zero, Vector2.zero, true);
            float end = Time.unscaledTime + SimulatedSeconds;
            while (Time.unscaledTime < end)
            {
                int left = Mathf.CeilToInt(end - Time.unscaledTime);
                counter.text = L.T($"Ödül {left} sn sonra", $"Reward in {left} s");
                yield return null;
            }
            Debug.Log($"[RewardedAds] Deneme reklamı tamamlandı: {placement}");
            done?.Invoke(true);
            Destroy(gameObject);
        }
    }
}
