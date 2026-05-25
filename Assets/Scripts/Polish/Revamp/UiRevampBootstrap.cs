// [UI REVAMP BOOTSTRAP] — Mini Empire Builder
using System.Collections;
using UnityEngine;

public class UiRevampBootstrap : MonoBehaviour
{
    private const string ThemeResourcePath = "Theme/RevampTheme";

    private IEnumerator Start()
    {
        yield return null;
        yield return new WaitForSeconds(0.08f);

        AutoTagKnownNodes();

        UiRevampApplier applier = FindFirstObjectByType<UiRevampApplier>();
        RevampTheme loaded = Resources.Load<RevampTheme>(ThemeResourcePath);
        if (loaded == null)
            loaded = ScriptableObject.CreateInstance<RevampTheme>();

        if (applier == null)
        {
            GameObject obj = new GameObject("UiRevampApplier");
            applier = obj.AddComponent<UiRevampApplier>();
        }

        applier.SetTheme(loaded);
        applier.ApplyNow();
    }

    private void AutoTagKnownNodes()
    {
        TagByName("TopBar", UiStyleKind.TopBar);
        TagByName("BuildingPanel", UiStyleKind.Panel);
        TagByName("HeroPanel", UiStyleKind.Panel);
        TagByName("BattleEndPanel", UiStyleKind.Panel);
        TagByName("EndInner", UiStyleKind.Panel);
        TagByName("HeroBarBg", UiStyleKind.Panel);
        TagByName("BaseBarBg", UiStyleKind.Panel);

        TagByName("BattleBtn", UiStyleKind.PrimaryButton);
        TagByName("ContinueBtn", UiStyleKind.PrimaryButton);
        TagByName("ReturnButton", UiStyleKind.PrimaryButton);
        TagByName("SelectBtn", UiStyleKind.PrimaryButton);
        TagByName("UpgradeBtn", UiStyleKind.PrimaryButton);
        TagByName("HeroUpBtn", UiStyleKind.PrimaryButton);
        TagByName("MenuBtn", UiStyleKind.DangerButton);
        TagByName("CloseBtn", UiStyleKind.DangerButton);
        TagByName("HCloseBtn", UiStyleKind.DangerButton);

        TagByName("Title", UiStyleKind.TitleText);
        TagByName("WTitle", UiStyleKind.TitleText);
        TagByName("ResultText", UiStyleKind.TitleText);
        TagByName("Subtitle", UiStyleKind.SubtitleText);

        TagByName("GoldText", UiStyleKind.AccentText);
        TagByName("GpsText", UiStyleKind.AccentText);
        TagByName("BaseLvlText", UiStyleKind.AccentText);
        TagByName("WaveText", UiStyleKind.BodyText);
        TagByName("EnemyText", UiStyleKind.BodyText);
        TagByName("LevelText", UiStyleKind.BodyText);
        TagByName("HeroLabel", UiStyleKind.BodyText);
        TagByName("BaseLabel", UiStyleKind.BodyText);
    }

    private static void TagByName(string nodeName, UiStyleKind kind)
    {
        Transform[] all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name != nodeName)
                continue;

            UiStyleToken token = all[i].GetComponent<UiStyleToken>();
            if (token == null)
                token = all[i].gameObject.AddComponent<UiStyleToken>();

            token.SetKind(kind);
        }
    }
}
