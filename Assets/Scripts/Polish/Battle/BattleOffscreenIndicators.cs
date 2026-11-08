using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows a direction marker only for enemies outside the camera view.</summary>
public sealed class BattleOffscreenIndicators : MonoBehaviour
{
    private readonly Dictionary<int, Image> markers = new Dictionary<int, Image>();
    private RectTransform root;
    private static Sprite arrowSprite;

    private void Start()
    {
        root = GameObject.Find("BattleCanvas")?.transform.Find("SafeArea") as RectTransform;
        if (root == null) root = transform as RectTransform;
    }

    private void LateUpdate()
    {
        if (root == null || Camera.main == null) return;
        foreach (var marker in markers.Values)
            if (marker != null) marker.gameObject.SetActive(false);
        if (BattleManager.Instance == null || !BattleManager.Instance.IsBattleActive) return;

        var camera = Camera.main;
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (enemy == null || enemy.IsDead) continue;
            Vector3 viewport = camera.WorldToViewportPoint(enemy.transform.position);
            if (viewport.z < 0f ||
                (viewport.x >= 0.05f && viewport.x <= 0.95f &&
                 viewport.y >= 0.05f && viewport.y <= 0.95f)) continue;

            int id = enemy.GetInstanceID();
            if (!markers.TryGetValue(id, out var marker) || marker == null)
            {
                var go = new GameObject("EnemyDirection_" + id, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root, false);
                marker = go.GetComponent<Image>();
                if (arrowSprite == null)
                    arrowSprite = Resources.Load<Sprite>("Battle/enemy_direction_arrow");
                marker.sprite = arrowSprite;
                marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                marker.rectTransform.sizeDelta = new Vector2(52f, 52f);
                marker.raycastTarget = false;
                markers[id] = marker;
            }
            Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (direction.sqrMagnitude < 0.0001f) continue;
            float tx = Mathf.Abs(direction.x) > 0.001f ? 0.44f / Mathf.Abs(direction.x) : float.MaxValue;
            float ty = Mathf.Abs(direction.y) > 0.001f ? 0.43f / Mathf.Abs(direction.y) : float.MaxValue;
            Vector2 edge = new Vector2(0.5f, 0.5f) + direction * Mathf.Min(tx, ty);
            edge.x = Mathf.Clamp(edge.x, 0.14f, 0.86f);
            edge.y = Mathf.Clamp(edge.y, 0.21f, 0.82f);
            Vector2 screen = new Vector2(edge.x * Screen.width, edge.y * Screen.height);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
            marker.rectTransform.anchoredPosition = local;
            marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            float distance = Vector2.Distance(camera.transform.position, enemy.transform.position);
            float near = 1f - Mathf.Clamp01(distance / 30f);
            marker.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(50f, 66f, near);
            marker.color = new Color(0.95f, 0.16f, 0.10f, Mathf.Lerp(0.78f, 1f, near));
            marker.gameObject.SetActive(true);
        }
    }

}
