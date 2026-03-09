using TMPro;
using UnityEngine;

/// <summary>
/// Madende biriken altin oldugunda ikon/metin gosterir.
/// </summary>
public class GoldMineCollectIndicator : MonoBehaviour
{
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private TextMeshPro amountText;

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoldMineStoredChanged += OnMineStoredChanged;
            OnMineStoredChanged(GameManager.Instance.GoldMineStored, GameManager.Instance.GetGoldMineStorageCapacity());
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGoldMineStoredChanged -= OnMineStoredChanged;
    }

    private void OnMineStoredChanged(int stored, int capacity)
    {
        bool active = stored > 0;

        if (iconRenderer != null)
            iconRenderer.enabled = active;

        if (amountText != null)
        {
            amountText.gameObject.SetActive(active);
            amountText.text = active ? $"{stored}" : "";
        }
    }

    public void SetReferences(SpriteRenderer icon, TextMeshPro text)
    {
        iconRenderer = icon;
        amountText = text;
    }
}
