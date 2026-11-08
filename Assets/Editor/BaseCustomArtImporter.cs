using UnityEditor;
using UnityEngine;

/// <summary>Keep generated source art crisp and readable for a tight runtime sprite rect.</summary>
public sealed class BaseCustomArtImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/CustomBaseArt/")) return;
        var texture = (TextureImporter)assetImporter;
        texture.textureType = TextureImporterType.Default;
        texture.isReadable = true;
        texture.alphaIsTransparency = true;
        texture.mipmapEnabled = false;
        texture.filterMode = FilterMode.Point;
        texture.textureCompression = TextureImporterCompression.Uncompressed;
        texture.maxTextureSize = assetPath.Contains("mine_stage_") ||
            assetPath.Contains("mine_lift_box") ? 512 : 1024;
    }
}
