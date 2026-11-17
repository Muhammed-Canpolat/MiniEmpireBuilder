using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MiniEmpire.UI
{
    /// <summary>
    /// Data-driven bottom menu. Buttons are generated from MenuItemData assets,
    /// separators are inserted automatically, and the arrow tab collapses / expands the bar.
    ///
    /// Your code:  bottomMenu.ItemClicked += id => { if (id == "build") OpenBuildPanel(); };
    /// </summary>
    public class BottomMenuBar : MonoBehaviour
    {
        [Header("Content")]
        public List<MenuItemData> items = new List<MenuItemData>();
        public bool turkish = true;
        [Tooltip("Put a separator before the first and after the last button too")]
        public bool edgeSeparators = true;

        [Header("Templates (inactive children are fine)")]
        public MenuButton buttonTemplate;
        public GameObject separatorTemplate;
        public RectTransform buttonRow;

        [Header("Collapse / expand")]
        public RectTransform slidingPanel;     // everything that slides down
        public Button toggleButton;
        public Image toggleImage;
        public Sprite arrowDown;               // shown while open  (tap = close)
        public Sprite arrowUp;                 // shown while closed (tap = open)
        public float collapseDistance = 178f;
        public float animTime = 0.25f;
        public bool startCollapsed = false;

        [Header("Events")]
        public UnityEvent<string> onItemClicked;
        public event Action<string> ItemClicked;
        public event Action<bool> CollapsedChanged;

        public bool IsCollapsed { get; private set; }
        readonly Dictionary<string, MenuButton> buttons = new Dictionary<string, MenuButton>();
        readonly List<GameObject> spawned = new List<GameObject>();
        Vector2 openPos;
        Coroutine slide;

        void Awake()
        {
            if (slidingPanel != null) openPos = slidingPanel.anchoredPosition;
            if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
            if (buttonTemplate != null) buttonTemplate.gameObject.SetActive(false);
            if (separatorTemplate != null) separatorTemplate.SetActive(false);
        }

        void Start()
        {
            Rebuild();
            SetCollapsed(startCollapsed, true);
        }

        // ---------- building ----------
        public void Rebuild()
        {
            foreach (var go in spawned) if (go != null) Destroy(go);
            spawned.Clear();
            buttons.Clear();
            if (buttonTemplate == null || buttonRow == null) return;

            var list = items.Where(i => i != null && i.visible).OrderBy(i => i.order).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0 || edgeSeparators) AddSeparator();
                var b = Instantiate(buttonTemplate, buttonRow);
                b.gameObject.SetActive(true);
                b.Setup(list[i], turkish, HandleClick);
                buttons[list[i].id] = b;
                spawned.Add(b.gameObject);
            }
            if (edgeSeparators && list.Count > 0) AddSeparator();
            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonRow);
        }

        void AddSeparator()
        {
            if (separatorTemplate == null) return;
            var s = Instantiate(separatorTemplate, buttonRow);
            s.SetActive(true);
            spawned.Add(s);
        }

        void HandleClick(string id)
        {
            ItemClicked?.Invoke(id);
            onItemClicked?.Invoke(id);
        }

        // ---------- runtime API ----------
        public void AddItem(MenuItemData data) { if (!items.Contains(data)) items.Add(data); Rebuild(); }
        public void RemoveItem(string id) { items.RemoveAll(i => i != null && i.id == id); Rebuild(); }
        public void SetLanguage(bool tr) { turkish = tr; Rebuild(); }
        public MenuButton GetButton(string id) => buttons.TryGetValue(id, out var b) ? b : null;
        public void SetBadge(string id, int count) { GetButton(id)?.SetBadge(count); }
        public void SetLocked(string id, bool locked) { GetButton(id)?.SetLocked(locked); }

        public void Toggle() => SetCollapsed(!IsCollapsed, false);

        public void SetCollapsed(bool collapsed, bool instant)
        {
            IsCollapsed = collapsed;
            if (toggleImage != null)
            {
                toggleImage.sprite = collapsed ? arrowUp : arrowDown;
                toggleImage.SetNativeSize();
            }
            if (slidingPanel != null)
            {
                Vector2 target = openPos + (collapsed ? new Vector2(0f, -collapseDistance) : Vector2.zero);
                if (slide != null) StopCoroutine(slide);
                if (instant || !isActiveAndEnabled) slidingPanel.anchoredPosition = target;
                else slide = StartCoroutine(Slide(target));
            }
            CollapsedChanged?.Invoke(collapsed);
        }

        IEnumerator Slide(Vector2 target)
        {
            Vector2 from = slidingPanel.anchoredPosition;
            float t = 0f;
            while (t < animTime)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / animTime), 3f); // ease-out
                slidingPanel.anchoredPosition = Vector2.LerpUnclamped(from, target, k);
                yield return null;
            }
            slidingPanel.anchoredPosition = target;
            slide = null;
        }
    }
}
