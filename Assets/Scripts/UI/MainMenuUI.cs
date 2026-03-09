using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ana Menü ekranı — Yeni Oyun / Devam Et seçenekleri
/// Yeni oyunda silah seçimi yapılır
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Ana Menü Paneli")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;

    [Header("Silah Seçim Paneli")]
    [SerializeField] private GameObject weaponSelectPanel;
    [SerializeField] private Button axeButton;
    [SerializeField] private Button spearButton;
    [SerializeField] private Button bowButton;
    [SerializeField] private Button backButton;

    [Header("Bilgi Metinleri")]
    [SerializeField] private TextMeshProUGUI gameTitle;
    [SerializeField] private TextMeshProUGUI weaponInfoText;

    private void Start()
    {
        // GameManager yoksa oluştur
        if (GameManager.Instance == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            gmObj.AddComponent<GameManager>();
        }

        // Başlangıçta ana menü göster, silah seçimi gizle
        ShowMainMenu();

        // Devam et butonu — kayıt varsa aktif
        if (continueButton != null)
        {
            continueButton.interactable = SaveSystem.HasSave();
        }

        // Buton listener'ları
        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGameClicked);

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);

        if (axeButton != null)
            axeButton.onClick.AddListener(() => OnWeaponSelected(WeaponType.Axe));

        if (spearButton != null)
            spearButton.onClick.AddListener(() => OnWeaponSelected(WeaponType.Spear));

        if (bowButton != null)
            bowButton.onClick.AddListener(() => OnWeaponSelected(WeaponType.Bow));

        if (backButton != null)
            backButton.onClick.AddListener(ShowMainMenu);
    }

    private void ShowMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (weaponSelectPanel != null) weaponSelectPanel.SetActive(false);
    }

    private void ShowWeaponSelect()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (weaponSelectPanel != null) weaponSelectPanel.SetActive(true);
        UpdateWeaponInfo(WeaponType.Axe); // varsayılan bilgi
    }

    private void OnNewGameClicked()
    {
        // Önceki kaydı sil
        SaveSystem.DeleteSave();
        ShowWeaponSelect();
    }

    private void OnContinueClicked()
    {
        GameManager.Instance.LoadGame();
        GameManager.Instance.LoadBaseWorld();
    }

    private void OnWeaponSelected(WeaponType weapon)
    {
        Debug.Log($"[MainMenu] Silah seçildi: {weapon}");
        GameManager.Instance.StartNewGame(weapon);
    }

    private void UpdateWeaponInfo(WeaponType weapon)
    {
        if (weaponInfoText == null) return;

        switch (weapon)
        {
            case WeaponType.Axe:
                weaponInfoText.text = "BALTA\nYüksek hasar, kısa menzil\nYakın dövüş ustaları için!";
                break;
            case WeaponType.Spear:
                weaponInfoText.text = "MIZRAK\nSavunmacı, yüksek dayanıklılık\nPozisyon tut, düşmanı durdur!";
                break;
            case WeaponType.Bow:
                weaponInfoText.text = "OK\nOrta hasar, uzun menzil\nUzaktan saldırı severler için!";
                break;
        }
    }

    /// <summary>
    /// Silah butonlarının üzerine gelince bilgi göster (opsiyonel)
    /// </summary>
    public void ShowAxeInfo() => UpdateWeaponInfo(WeaponType.Axe);
    public void ShowSpearInfo() => UpdateWeaponInfo(WeaponType.Spear);
    public void ShowBowInfo() => UpdateWeaponInfo(WeaponType.Bow);
}
