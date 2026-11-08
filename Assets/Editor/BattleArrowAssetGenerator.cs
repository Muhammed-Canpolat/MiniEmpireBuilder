using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BattleArrowAssetGenerator
{
    private const string Path = "Assets/Resources/Battle/enemy_direction_arrow.png";

    static BattleArrowAssetGenerator() => EditorApplication.delayCall += EnsureAsset;

    [MenuItem("Tools/Mini Empire/Generate Enemy Direction Arrow")]
    public static void EnsureAsset()
    {
        if (!File.Exists(Path))
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool body = x >= 3 && x < 18 && y >= 12 && y <= 19;
                bool head = x >= 15 && x <= 29 && Mathf.Abs(y - 15.5f) <= (29 - x) * 0.9f;
                texture.SetPixel(x, y, body || head ? Color.white : Color.clear);
            }
            texture.Apply();
            File.WriteAllBytes(Path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(Path);
        var importer = AssetImporter.GetAtPath(Path) as TextureImporter;
        if (importer == null ||
            (importer.textureType == TextureImporterType.Sprite &&
             importer.alphaIsTransparency &&
             importer.textureCompression == TextureImporterCompression.Uncompressed)) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePixelsPerUnit = 32;
        importer.SaveAndReimport();
    }
}
