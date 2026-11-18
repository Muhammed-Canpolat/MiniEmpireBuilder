using UnityEngine;
using TMPro;

namespace MiniEmpire.Intro
{
    /// <summary>Alttaki ipucu yazısını belirli aralıklarla değiştirir.</summary>
    public class TipRotator : MonoBehaviour
    {
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private float interval = 3f;
        // İpucu listesi TEK YER: burası (sahne kurulumu bu varsayılanları kullanır; Intro sahnesinde Inspector'dan da düzenlenebilir).
        [SerializeField] private string[] tips =
        {
            "Ordunu güçlendir, imparatorluğunu büyüt!",
            "Madenlerini yükselt, daha çok altın topla!",
            "Kulelerini yükselt, Ana Üs'ü koru!",
            "Seferde bölgeleri fethet, ödülleri topla!"
        };

        private float _timer;
        private int _index;

        private void OnEnable()
        {
            _index = 0; _timer = 0f;
            Show();
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < interval || tips.Length < 2) return;
            _timer = 0f;
            _index = (_index + 1) % tips.Length;
            Show();
        }

        private void Show() { if (tipText != null && tips.Length > 0) tipText.text = tips[_index]; }
    }
}
