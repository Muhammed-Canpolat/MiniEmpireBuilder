using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniEmpire.UI
{
    /// <summary>
    /// The red shield in the middle of the top bar. Number and caption are text, not baked into the sprite,
    /// so 1, 2 or 3 digit levels and any language work.
    /// </summary>
    public class LevelBadge : MonoBehaviour
    {
        public Image shield;
        public TMP_Text levelText;
        public TMP_Text captionText;

        [Tooltip("Font size per digit count: index 0 = 1 digit, 1 = 2 digits, 2 = 3+ digits")]
        public float[] fontSizeByDigits = { 54f, 46f, 36f };

        public void SetLevel(int level)
        {
            if (levelText == null) return;
            string s = level.ToString();
            int i = Mathf.Clamp(s.Length - 1, 0, fontSizeByDigits.Length - 1);
            levelText.enableAutoSizing = false;
            levelText.fontSize = fontSizeByDigits[i];
            levelText.text = s;
        }

        public void SetCaption(string caption)
        {
            if (captionText != null) captionText.text = caption;
        }

        public void SetShieldSprite(Sprite sprite)
        {
            if (shield != null) shield.sprite = sprite;
        }
    }
}
