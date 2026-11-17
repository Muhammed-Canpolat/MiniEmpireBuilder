using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiniEmpire.UI
{
    /// <summary>
    /// A single bottom-menu button: frame (sliced) + icon + label plate (sliced) + label text.
    /// All four parts are separate, so any of them can be swapped independently.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MenuButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Image frame;
        public Image icon;
        public Image labelPlate;
        public TMP_Text label;
        [Tooltip("Optional red dot / counter object, toggled with SetBadge")]
        public GameObject badge;
        public TMP_Text badgeText;

        public float pressedScale = 0.94f;
        public Color lockedTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        public string Id { get; private set; }
        Button button;
        Vector3 baseScale = Vector3.one;

        void Awake()
        {
            button = GetComponent<Button>();
            baseScale = transform.localScale;
        }

        public void Setup(MenuItemData data, bool turkish, Action<string> onClick)
        {
            if (button == null) button = GetComponent<Button>();
            Id = data.id;
            gameObject.name = "Btn_" + data.id;
            SetIcon(data.icon);
            SetLabel(data.Label(turkish));
            SetLocked(data.locked);
            SetBadge(0);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(Id));
        }

        public void SetIcon(Sprite sprite)
        {
            if (icon == null) return;
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.enabled = sprite != null;
        }

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
            if (labelPlate != null) labelPlate.enabled = !string.IsNullOrEmpty(text);
        }

        public void SetLocked(bool locked)
        {
            if (button == null) button = GetComponent<Button>();
            button.interactable = !locked;
            Color c = locked ? lockedTint : Color.white;
            if (icon != null) icon.color = c;
            if (frame != null) frame.color = c;
        }

        public void SetBadge(int count)
        {
            if (badge == null) return;
            badge.SetActive(count > 0);
            if (badgeText != null) badgeText.text = count > 99 ? "99+" : count.ToString();
        }

        public void OnPointerDown(PointerEventData e) { if (button.interactable) transform.localScale = baseScale * pressedScale; }
        public void OnPointerUp(PointerEventData e) { transform.localScale = baseScale; }
        public void OnPointerExit(PointerEventData e) { transform.localScale = baseScale; }
    }
}
