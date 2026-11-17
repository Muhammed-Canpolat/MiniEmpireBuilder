using UnityEngine;

namespace MiniEmpire.UI
{
    /// <summary>
    /// Keeps its RectTransform inside the device safe area (notch, rounded corners, home indicator).
    /// Put the top and bottom bars under an object that has this component.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        Vector2Int lastSize;

        void OnEnable() => Apply();
        void Update()
        {
            if (Screen.safeArea != last || lastSize.x != Screen.width || lastSize.y != Screen.height) Apply();
        }

        void Apply()
        {
            var rt = (RectTransform)transform;
            Rect r = Screen.safeArea;
            last = r;
            lastSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rt.anchorMin = new Vector2(r.xMin / Screen.width, r.yMin / Screen.height);
            rt.anchorMax = new Vector2(r.xMax / Screen.width, r.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
