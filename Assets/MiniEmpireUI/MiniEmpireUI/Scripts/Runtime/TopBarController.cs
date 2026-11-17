using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MiniEmpire.UI
{
    /// <summary>
    /// Convenience access to the top bar:  topBar.Set("gold", 2581);  topBar.SetCurrentMax("builder", 2, 2);
    /// New resource = duplicate a pill under LeftGroup/RightGroup, give it an id here, change its icon.
    /// </summary>
    public class TopBarController : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string id;
            public ResourceCounter counter;
        }

        public List<Entry> counters = new List<Entry>();
        public LevelBadge levelBadge;
        public Button settingsButton;
        public event Action SettingsClicked;

        void Awake()
        {
            if (settingsButton != null) settingsButton.onClick.AddListener(() => SettingsClicked?.Invoke());
        }

        public ResourceCounter Get(string id)
        {
            foreach (var e in counters) if (e.id == id) return e.counter;
            Debug.LogWarning("TopBar: no counter with id " + id);
            return null;
        }

        public void Set(string id, long value, bool instant = false) => Get(id)?.SetValue(value, instant);
        public void SetCurrentMax(string id, long current, long max) => Get(id)?.SetValue(current, max);
        public void SetLevel(int level) => levelBadge?.SetLevel(level);

        public void SetLanguage(bool turkish)
        {
            foreach (var e in counters) if (e.counter != null) e.counter.turkish = turkish;
        }
    }
}
