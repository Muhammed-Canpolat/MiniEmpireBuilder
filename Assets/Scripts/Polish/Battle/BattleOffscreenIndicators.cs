using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kamera dışındaki düşmanlar için yön okları. Aynı yöndeki (8 dilim) düşmanlar tek okta
/// toplanır ve sayısı "×3" gibi gösterilir; ekran kenarında üst üste yığılan oklar olmaz.
/// </summary>
public sealed class BattleOffscreenIndicators : MonoBehaviour
{
    private const int Sectors = 8;
    private readonly Image[] markers = new Image[Sectors];
    private readonly TextMeshProUGUI[] counts = new TextMeshProUGUI[Sectors];
    private readonly Vector2[] directionSum = new Vector2[Sectors];
    private readonly int[] enemyCount = new int[Sectors];
    private readonly float[] nearest = new float[Sectors];
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
        for (int i = 0; i < Sectors; i++)
        {
            directionSum[i] = Vector2.zero;
            enemyCount[i] = 0;
            nearest[i] = float.MaxValue;
        }
        bool active = BattleManager.Instance != null && BattleManager.Instance.IsBattleActive;
        var camera = Camera.main;
        if (active)
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy == null || enemy.IsDead) continue;
                Vector3 viewport = camera.WorldToViewportPoint(enemy.transform.position);
                if (viewport.z < 0f ||
                    (viewport.x >= 0.05f && viewport.x <= 0.95f && viewport.y >= 0.05f && viewport.y <= 0.95f)) continue;
                Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                if (direction.sqrMagnitude < 0.0001f) continue;
                float angle = Mathf.Repeat(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 22.5f, 360f);
                int sector = Mathf.Clamp((int)(angle / 45f), 0, Sectors - 1);
                directionSum[sector] += direction.normalized;
                enemyCount[sector]++;
                nearest[sector] = Mathf.Min(nearest[sector],
                    Vector2.Distance(camera.transform.position, enemy.transform.position));
            }

        for (int i = 0; i < Sectors; i++)
        {
            if (enemyCount[i] == 0)
            {
                if (markers[i] != null) markers[i].gameObject.SetActive(false);
                continue;
            }
            var marker = markers[i] != null ? markers[i] : (markers[i] = CreateMarker(i));
            Vector2 direction = directionSum[i] / enemyCount[i];
            float tx = Mathf.Abs(direction.x) > 0.001f ? 0.44f / Mathf.Abs(direction.x) : float.MaxValue;
            float ty = Mathf.Abs(direction.y) > 0.001f ? 0.43f / Mathf.Abs(direction.y) : float.MaxValue;
            Vector2 edge = new Vector2(0.5f, 0.5f) + direction * Mathf.Min(tx, ty);
            edge.x = Mathf.Clamp(edge.x, 0.12f, 0.88f);
            edge.y = Mathf.Clamp(edge.y, 0.2f, 0.82f);
            Vector2 screen = new Vector2(edge.x * Screen.width, edge.y * Screen.height);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
            marker.rectTransform.anchoredPosition = local;
            marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            float near = 1f - Mathf.Clamp01(nearest[i] / 30f);
            marker.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(48f, 64f, near);
            marker.color = new Color(0.95f, 0.16f, 0.10f, Mathf.Lerp(0.8f, 1f, near));
            marker.gameObject.SetActive(true);
            var count = counts[i];
            count.transform.parent.gameObject.SetActive(enemyCount[i] > 1);
            count.text = "×" + enemyCount[i];
            // Sayı rozeti okun ekran içi tarafında, dönmeden durur.
            var badge = (RectTransform)count.transform.parent;
            badge.anchoredPosition = local - direction.normalized * 34f;
        }
    }

    private Image CreateMarker(int sector)
    {
        var go = new GameObject("EnemyDirection_" + sector, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        var marker = go.GetComponent<Image>();
        if (arrowSprite == null) arrowSprite = Resources.Load<Sprite>("Battle/enemy_direction_arrow");
        marker.sprite = arrowSprite;
        marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        marker.raycastTarget = false;

        var badge = ReferenceUiVisuals.Image(root, "EnemyDirectionCount_" + sector, new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(-17f, -11f), new Vector2(17f, 11f), Color.white);
        badge.sprite = ReferenceUiVisuals.PillSprite();
        badge.type = Image.Type.Sliced;
        counts[sector] = ReferenceUiVisuals.Text(badge.rectTransform, "Count", "", 13,
            new Color(1f, 0.55f, 0.45f), TextAlignmentOptions.Center, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, bodyBold: true);
        badge.gameObject.SetActive(false);
        // Rozet işaretçiyle birlikte gizlenir.
        go.AddComponent<FollowActive>().Target = badge.gameObject;
        return marker;
    }
}
