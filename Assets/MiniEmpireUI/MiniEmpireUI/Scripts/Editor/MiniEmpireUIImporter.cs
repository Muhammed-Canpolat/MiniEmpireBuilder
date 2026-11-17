using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MiniEmpire.UI.EditorTools
{
    /// <summary>
    /// Every PNG under a folder named "MiniEmpireUI" is imported as a UI sprite
    /// (no compression, no mipmaps, Full Rect mesh) and the stretchable ones get their 9-slice borders.
    /// Borders are in pixels: (left, bottom, right, top).
    /// </summary>
    public class MiniEmpireUIImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            // Top bar
            { "topbar_bg",                 new Vector4(24, 0, 23, 0) },
            { "res_pill_bg",               new Vector4(63, 0, 29, 0) },   // left border covers the round icon socket
            { "res_capsule_bg",            new Vector4(30, 0, 29, 0) },
            // Bottom bar
            { "bottombar_bg",              new Vector4(29, 0, 28, 0) },
            { "menu_button_frame_noplate", new Vector4(26, 26, 26, 40) },
            { "menu_button_frame",         new Vector4(26, 60, 26, 40) },
            { "menu_label_plate",          new Vector4(16, 0, 16, 0) },
            { "separator_banner",          new Vector4(0, 20, 0, 14) },
        };

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/MiniEmpireUI/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(s);

            // default 9-slice borders; only written while the sprite has none, so your own Sprite Editor tweaks are kept
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (Borders.TryGetValue(name, out var b) && ti.spriteBorder == Vector4.zero)
                ti.spriteBorder = b;
        }

        [MenuItem("Tools/MiniEmpire UI/Reapply Sprite Import Settings")]
        static void ReimportAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.Replace('\\', '/').Contains("/MiniEmpireUI/"))
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("MiniEmpire UI: sprite import settings reapplied.");
        }
    }
}
