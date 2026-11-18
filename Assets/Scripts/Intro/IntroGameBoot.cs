using System.Collections;
using MiniEmpire.Intro;
using UnityEngine;

/// <summary>
/// Açılış intro'sunda oyunu hazırlar (eski MainMenuScene'in görevleri; o sahne artık gösterilmez):
/// GameManager/SpriteManager kurulur, GameManager.Start kaydı yükler; kayıt yoksa Savaşçı ile yeni oyun kurulur
/// (eski menüdeki OYNA ile aynı). Kayıt açılamadıysa (K-1) kayıt korunur ve oyuncuya sorulur: YENİ OYUN (eski kayıt
/// arşivlenir) ya da ÇIK. Çubuk %100 olsa bile oyun, hazır olana dek (IntroSceneController.CanFinish) açılmaz.
/// </summary>
public sealed class IntroGameBoot : MonoBehaviour
{
    public static bool Ready { get; private set; }

    private void Awake()
    {
        Ready = false;
        IntroSceneController.CanFinish = () => Ready;
        if (GameManager.Instance == null) new GameObject("GameManager").AddComponent<GameManager>();
        if (SpriteManager.Instance == null) new GameObject("SpriteManager").AddComponent<SpriteManager>();
    }

    private IEnumerator Start()
    {
        yield return null;   // GameManager.Start kaydı yüklesin
        var gm = GameManager.Instance;
        if (gm == null) { Ready = true; yield break; }
        if (gm.PlayerData == null && SaveSystem.HasSave() && !gm.SaveLoadFailed) gm.LoadGame();
        if (gm.PlayerData != null) { Ready = true; yield break; }
        if (gm.SaveLoadFailed) { AskAboutBrokenSave(); yield break; }
        gm.PrepareNewGame(WeaponType.Axe);   // ilk açılış: tek kahraman (Savaşçı), silah seçimi yok
        Ready = true;
    }

    private static void AskAboutBrokenSave()
    {
        ConfirmDialog.Show("BrokenSaveDialog", L.T("Kayıt açılamadı", "Save could not be opened"),
            L.T("Kaydın silinmedi, korunuyor. Oyunu güncellemeyi dene. Yeni oyun başlatırsan eski kayıt arşivlenir.",
                "Your save was not deleted. Try updating the game. Starting a new game archives the old save."),
            L.T("ÇIK", "QUIT"), L.T("YENİ OYUN", "NEW GAME"),
            () => { GameManager.Instance.ResetProgressForNewGame(); Ready = true; },
            Application.Quit, false);
    }

    private void OnDestroy()
    {
        if (IntroSceneController.CanFinish != null) IntroSceneController.CanFinish = null;
    }
}
