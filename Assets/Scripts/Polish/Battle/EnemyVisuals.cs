// [ENEMY VISUALS] — Mini Empire Builder
using TMPro;
using UnityEngine;

public class EnemyVisuals : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private TextMeshPro nameLabel;

    /// <summary>
    /// Applies tier visual treatment to enemy.
    /// </summary>
    public void ApplyTier(bool isElite, bool isBoss)
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (isBoss)
        {
            transform.localScale *= 2f;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(1f, 0.7f, 0.7f);
            }

            if (nameLabel != null)
            {
                nameLabel.gameObject.SetActive(true);
                nameLabel.text = "BOSS";
            }

            return;
        }

        if (isElite)
        {
            transform.localScale *= 1.5f;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(1f, 0.5f, 0.5f);
            }
        }
    }
}
