using UnityEditor;
using UnityEngine;

namespace Snapline.EditorTools
{
    /// <summary>
    /// Import settings for the authored art, applied automatically.
    ///
    /// Dropping a PNG into a Unity project imports it as a plain texture with mipmaps, aggressive
    /// compression and no sprite borders. None of that is right for UI, and every one of the
    /// symptoms — soft icons, banded gradients, a stretched button with warped rounded ends — looks
    /// like bad artwork rather than a wrong import setting, which is why this is enforced here
    /// instead of written down as a step someone has to remember.
    ///
    /// It applies to everything under <c>Resources/Snapline/</c> and nothing else.
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Snapline/Resources/Snapline/";

        /// <summary>
        /// Sprites that get stretched to a width the artwork was not drawn at, and so need
        /// nine-slice borders to keep their rounded caps intact.
        ///
        /// Only the horizontal borders are set. These are drawn as horizontal pills, so the caps are
        /// what must survive; leaving the vertical borders at zero means a sprite used at anything
        /// other than its native height scales cleanly rather than smearing the top highlight. Call
        /// sites size the pills at their native height for that reason.
        ///
        /// The border is expressed as a fraction of the sprite's own height, because the delivered
        /// art is not at a fixed size and a pixel value would silently stop matching on re-export.
        /// </summary>
        private static readonly string[] NineSliced =
        {
            "pill_blue", "pill_red", "pill_purple", "pill_teal", "pill_yellow",
        };

        /// <summary>A cap is a semicircle, so it is half the height — plus a little, so the
        /// stretched middle never eats into the curve itself.</summary>
        private const float CapFraction = 0.55f;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot, System.StringComparison.Ordinal)) return;

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;

            // These are large flat gradients with soft glows, which is the worst case for block
            // compression — it bands visibly. UI art is a small share of the atlas, so the memory
            // is worth spending.
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;

            // The background is full-screen; everything else is an element on it.
            importer.maxTextureSize = assetPath.EndsWith("candy_background.png", System.StringComparison.Ordinal)
                ? 2048
                : 1024;
        }

        /// <summary>
        /// Borders have to be set after the texture is read, because they are a fraction of its
        /// height and the height is not known at preprocess time.
        /// </summary>
        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!assetPath.StartsWith(ArtRoot, System.StringComparison.Ordinal)) return;

            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (System.Array.IndexOf(NineSliced, file) < 0) return;

            var importer = (TextureImporter)assetImporter;
            int cap = Mathf.RoundToInt(texture.height * CapFraction);
            importer.spriteBorder = new Vector4(cap, 0f, cap, 0f);
        }

        /// <summary>
        /// Forces every art asset back through the importer.
        ///
        /// Needed because these rules only run when an asset is imported, so anything already in the
        /// project when the rules changed keeps its old settings and looks wrong for no visible
        /// reason.
        /// </summary>
        [MenuItem("Snapline/Reimport Art")]
        public static void ReimportArt()
        {
            AssetDatabase.ImportAsset(
                ArtRoot.TrimEnd('/'),
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Snapline] reimported art under {ArtRoot}");
        }
    }
}
