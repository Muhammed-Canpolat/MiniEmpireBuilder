// PixelCastle klasörüne atılan PNG'leri otomatik olarak pixel-art sprite ayarlarıyla içe aktarır.
// Bu dosya bir "Editor" klasöründe kalmalı.
using UnityEditor;
using UnityEngine;

public class PixelCastleImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("PixelCastle")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 84;          // zemin karoları ≈84 px → 1 birim
        ti.filterMode = FilterMode.Point;     // keskin pikseller
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.BottomCenter; // binalar zemine otursun
        ti.SetTextureSettings(s);
    }
}
