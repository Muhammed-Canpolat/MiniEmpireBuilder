using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BaseShadowAssetGenerator
{
    public const string AssetPath = "Assets/Art/FX/blob_shadow.png";
    private const string PrefabPath = "Assets/Resources/BaseGroundShadow.prefab";

    static BaseShadowAssetGenerator()
    {
        EditorApplication.delayCall += EnsureAsset;
    }

    [MenuItem("Tools/Mini Empire/Generate Building Shadow")]
    public static void EnsureAsset()
    {
        if (!File.Exists(AssetPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float nx = ((x + 0.5f) / 64f - 0.5f) * 2f;
                float ny = ((y + 0.5f) / 64f - 0.5f) * 2f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            File.WriteAllBytes(AssetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(AssetPath);
        var importer = AssetImporter.GetAtPath(AssetPath) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType == TextureImporterType.Sprite &&
            importer.alphaIsTransparency && importer.textureCompression == TextureImporterCompression.Uncompressed &&
            importer.filterMode == FilterMode.Bilinear)
        {
            EnsurePrefab();
            EnsureMineImportSettings();
            return;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePixelsPerUnit = 64;
        importer.SaveAndReimport();
        EnsurePrefab();
        EnsureMineImportSettings();
    }

    private static void EnsurePrefab()
    {
        if (File.Exists(PrefabPath)) return;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath);
        if (sprite == null) return;
        var go = new GameObject("ZeminGolgesi");
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0f, 0f, 0f, 0.30f);
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
    }

    private static void EnsureMineImportSettings()
    {
        foreach (string folder in new[] { "Assets/Resources/CustomBaseArt", "Assets/Art/Buildings/Mine" })
        {
            if (!Directory.Exists(folder)) continue;
            foreach (string file in Directory.GetFiles(folder, "*.png"))
            {
                string path = file.Replace('\\', '/');
                var mineImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                if (mineImporter == null) continue;
                var settings = new TextureImporterSettings();
                mineImporter.ReadTextureSettings(settings);
                if (mineImporter.alphaSource == TextureImporterAlphaSource.FromInput &&
                    mineImporter.alphaIsTransparency &&
                    mineImporter.textureCompression == TextureImporterCompression.Uncompressed &&
                    mineImporter.isReadable &&
                    settings.spriteMeshType == SpriteMeshType.Tight) continue;
                mineImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                mineImporter.alphaIsTransparency = true;
                mineImporter.textureCompression = TextureImporterCompression.Uncompressed;
                mineImporter.isReadable = true;
                settings.spriteMeshType = SpriteMeshType.Tight;
                mineImporter.SetTextureSettings(settings);
                mineImporter.SaveAndReimport();
            }
        }
    }
}

public sealed class BuildingSpriteAlphaValidator : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
            !(assetPath.StartsWith("Assets/Art/Buildings/") ||
              assetPath.StartsWith("Assets/Resources/CustomBaseArt/") ||
              assetPath.StartsWith("Assets/Resources/Sprites/building_"))) return;
        var importer = (TextureImporter)assetImporter;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;
        var spriteSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(spriteSettings);
        spriteSettings.spriteMeshType = SpriteMeshType.Tight;
        importer.SetTextureSettings(spriteSettings);
    }

    private void OnPostprocessTexture(Texture2D texture)
    {
        if (!assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
            !(assetPath.StartsWith("Assets/Art/Buildings/") ||
              assetPath.StartsWith("Assets/Resources/CustomBaseArt/") ||
              assetPath.StartsWith("Assets/Resources/Sprites/building_"))) return;
        try
        {
            int lastX = texture.width - 1, lastY = texture.height - 1;
            if (texture.GetPixel(0, 0).a > 0.001f || texture.GetPixel(lastX, 0).a > 0.001f ||
                texture.GetPixel(0, lastY).a > 0.001f || texture.GetPixel(lastX, lastY).a > 0.001f)
                Debug.LogWarning($"Building sprite köşe alfası sıfır değil: {assetPath}");
        }
        catch (UnityException)
        {
            Debug.LogWarning($"Building sprite alpha doğrulanamadı (Read/Write aç): {assetPath}");
        }
    }
}
