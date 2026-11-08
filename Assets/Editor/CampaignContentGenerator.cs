using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CampaignContentGenerator
{
    [MenuItem("Mini Empire Builder/Generate Campaign Content")]
    public static void Generate()
    {
        const string folder = "Assets/Resources/Campaign";
        Directory.CreateDirectory(folder);
        string[] names = { "Yıkık Kale Yolu", "Sisli Orman", "Terk Edilmiş Maden", "Donmuş Geçit", "Karanlık Kale" };
        string[] tones = { "#8A5E38", "#2F6B3A", "#6A6A72", "#3E7FB0", "#7A2A2A" };
        string[] bosses = { "Kale Muhafızı", "Orman Cadısı", "Maden Gardiyanı", "Buz Hükümdarı", "Karanlık Kral" };
        string[] descriptions =
        {
            "Yıkılmış kalenin yolunu temizle. Düşman akınlarını durdurarak üsse güvenli bir geçit aç.",
            "Sis görüş alanını daraltır; yaratıklar ağaçların arasından yaklaşır.",
            "Terk edilmiş madendeki kaynakları düşman baskınlarından koru.",
            "Donmuş geçitte soğuk ve dar yollar savunmayı zorlaştırır.",
            "Karanlık kalenin ordusunu yen ve son bossla yüzleş."
        };
        CampaignLevelKind[] kinds =
        {
            CampaignLevelKind.Survive, CampaignLevelKind.DefendBase,
            CampaignLevelKind.DefendMine, CampaignLevelKind.TimeTrial, CampaignLevelKind.Boss
        };
        string[] levelDescriptions =
        {
            "Dalgalar bitene kadar hayatta kal ve Ana Üs'ü koru.",
            "Düşmanları Ana Üs'e ulaşmadan durdur.",
            "Madene yönelen düşman akınını püskürt.",
            "Süre dolmadan bütün düşmanları temizle.",
            "Bölgenin bossunu ve korumalarını yen."
        };
        int[][] enemies =
        {
            new[] { 7 }, new[] { 7, 5 }, new[] { 8, 6 },
            new[] { 9, 7 }, new[] { 8, 8, 1 }
        };
        Sprite battleTile = Resources.Load<Sprite>("Sprites/ground_battle_tile_01");
        Sprite forestTile = Resources.Load<Sprite>("Sprites/ground_buildable_01");
        for (int i = 0; i < 5; i++)
        {
            string path = $"{folder}/region_{i + 1:00}.asset";
            var region = AssetDatabase.LoadAssetAtPath<RegionData>(path);
            if (region == null)
            {
                region = ScriptableObject.CreateInstance<RegionData>();
                AssetDatabase.CreateAsset(region, path);
            }
            region.id = i + 1;
            region.displayName = names[i];
            region.requiredBaseLevel = i + 1;
            ColorUtility.TryParseHtmlString(tones[i], out region.colorTone);
            region.groundTile = i == 1 ? forestTile : battleTile;
            region.enemyTypes = i == 1 ? new[] { EnemyType.Wolf, EnemyType.ShadowWolf, EnemyType.Zombie }
                : i < 2 ? new[] { EnemyType.Wolf, EnemyType.Zombie, EnemyType.Orc }
                : new[] { EnemyType.Wolf, EnemyType.Zombie, EnemyType.Orc, EnemyType.Troll };
            region.bossType = i == 1 ? EnemyType.ForestWitch : EnemyType.Troll;
            region.specialMechanic = i == 1 ? "Sis" : i == 3 ? "Don" : "";
            region.bossName = bosses[i];
            region.description = descriptions[i];
            region.levels = new CampaignLevelData[5];
            for (int j = 0; j < 5; j++)
            {
                var counts = new int[enemies[j].Length];
                for (int wave = 0; wave < counts.Length; wave++) counts[wave] = enemies[j][wave] + i * 2;
                region.levels[j] = new CampaignLevelData
                {
                    number = j + 1,
                    kind = kinds[j],
                    waveCount = counts.Length,
                    enemyCounts = counts,
                    reward = 35 + i * 35 + j * 18,
                    modifier = i == 1 ? "SİS" : i == 3 ? "DON" : "",
                    description = levelDescriptions[j],
                    starRules = new[]
                    {
                        "Zafer kazan", "Ana Üs canı %50 üzerinde kalsın", "Kahraman canı %50 üzerinde kalsın"
                    }
                };
            }
            EditorUtility.SetDirty(region);
        }
        AssetDatabase.SaveAssets();

        const string scenePath = "Assets/Scenes/CampaignScene.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("CampaignSceneSetup").AddComponent<CampaignSceneSetup>();
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0, 0, -10);
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.backgroundColor = ReferenceUiVisuals.Background;
        EditorSceneManager.SaveScene(scene, scenePath);

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(item => item.path == scenePath))
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.Refresh();
        Debug.Log("[Campaign] Five regions and CampaignScene generated.");
    }
}
