using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildDecorAssets
{
    private const string Folder = "Assets/Resources/Decor";
    private const string Tiny = "Assets/Tiny Swords";

    [MenuItem("Tools/Base Review/Build Decor Assets")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        string[] ids = { "tree", "bush", "rock", "flowers", "lantern",
            "stone_path", "fountain", "flag" };
        string[] names = { "Ağaç", "Çalı", "Kaya", "Çiçek tarlası", "Fener",
            "Taş yol", "Fıskiye", "Bayrak" };
        Vector2Int[] sizes = { new Vector2Int(2, 2), Vector2Int.one, Vector2Int.one,
            new Vector2Int(2, 1), Vector2Int.one, Vector2Int.one,
            new Vector2Int(3, 3), Vector2Int.one };
        int[] costs = { 15, 5, 8, 10, 20, 3, 80, 12 };
        string[] tinyPaths = {
            $"{Tiny}/Pawn and Resources/Wood/Trees/Tree1.png",
            $"{Tiny}/Terrain/Decorations/Bushes/Bush 1.png",
            $"{Tiny}/Terrain/Decorations/Rocks/Rock1.png"
        };
        for (int i = 0; i < ids.Length; i++)
        {
            string path = $"{Folder}/{ids[i]}.asset";
            var data = AssetDatabase.LoadAssetAtPath<DecorData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<DecorData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.id = ids[i];
            data.displayName = names[i];
            data.footprint = sizes[i];
            data.cost = costs[i];
            data.unlockMainBaseLevel = ids[i] == "fountain" ? 3 : 1;
            data.animated = ids[i] == "bush";
            data.animatorController = ids[i] == "bush" ?
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    $"{Tiny}/Terrain/Decorations/Bushes/Bush 1 Animation/Bush 1.controller") : null;
            data.sprite = i < tinyPaths.Length
                ? AssetDatabase.LoadAllAssetsAtPath(tinyPaths[i]).OfType<Sprite>().FirstOrDefault()
                : MakePlaceholder(ids[i]);
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Decor] Eight data assets and placeholder sprites created.");
    }

    private static Sprite MakePlaceholder(string id)
    {
        string path = $"{Folder}/{id}_placeholder.png";
        if (!File.Exists(path))
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            var clear = new Color32[32 * 32];
            texture.SetPixels32(clear);
            Color main = id == "fountain" ? new Color(0.2f, 0.75f, 0.95f) :
                id == "flowers" ? new Color(0.95f, 0.45f, 0.65f) :
                id == "lantern" ? new Color(1f, 0.79f, 0.28f) :
                id == "flag" ? new Color(0.72f, 0.3f, 0.28f) :
                new Color(0.54f, 0.55f, 0.57f);
            for (int y = 3; y < 29; y++)
            for (int x = 3; x < 29; x++)
            {
                bool draw = id == "stone_path" ? x > 4 && x < 27 && y > 5 && y < 26 :
                    id == "flowers" ? (x % 8 < 4 && y % 8 < 4) :
                    id == "lantern" ? (x >= 14 && x <= 17 && y < 22) ||
                        (x >= 10 && x <= 21 && y >= 19 && y <= 26) :
                    id == "flag" ? (x >= 8 && x <= 10) ||
                        (x >= 11 && x <= 26 && y >= 16 && y <= 25) :
                    (x - 16) * (x - 16) + (y - 16) * (y - 16) < 135;
                if (draw) texture.SetPixel(x, y, main);
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
