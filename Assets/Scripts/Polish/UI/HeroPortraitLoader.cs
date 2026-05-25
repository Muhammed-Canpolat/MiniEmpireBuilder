// [HERO PORTRAIT LOADER] — Mini Empire Builder
using UnityEngine;
using UnityEngine.UI;

public class HeroPortraitLoader : MonoBehaviour
{
    [SerializeField] private WeaponType weaponType;
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite placeholder;

    /// <summary>
    /// Loads hero sprite from Resources/Heroes and assigns placeholder if missing.
    /// </summary>
    public void LoadPortrait()
    {
        if (targetImage == null)
        {
            return;
        }

        string key = weaponType.ToString().ToLowerInvariant();
        Sprite loaded = Resources.Load<Sprite>("Heroes/" + key);
        targetImage.sprite = loaded != null ? loaded : placeholder;
    }

    private void Start()
    {
        LoadPortrait();
    }
}
