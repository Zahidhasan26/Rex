using UnityEditor;

namespace BowGame
{
    // Photos dropped into Boss/FacePhotos are imported ready for BossFace: readable from scripts,
    // full quality (no compression), no mipmaps, up to 2048 px.
    public class FacePhotoImporter : AssetPostprocessor
    {
        const string Folder = "Assets/_Game/Dinosaurs/Boss/FacePhotos/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.isReadable = true;
            ti.mipmapEnabled = false;
            ti.sRGBTexture = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 2048;
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
