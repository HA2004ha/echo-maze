using UnityEditor;
using UnityEngine;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// The victory emote's art, built out of the seven delivery sheets in <c>Victory-emote/</c> at the repo
    /// root. Those are paintings, not sprites: the glow sheets are drawn on black and the two character
    /// sheets on white, and none of them can be dropped into an Image as they are.
    ///
    /// Two ways out, one per background, and the difference is deliberate:
    ///
    /// - Sheets painted on black (the swirls, the confetti, the crown and the word) keep their pixels and are
    ///   drawn with <c>EchoMaze/UI Additive</c>. Black adds nothing, so there is no matte to make a mistake
    ///   in and the soft glow survives instead of being cut away by a luminance threshold.
    /// - Sheets painted on white (the ghost, its pedestal) are matted here: everything that is paper becomes
    ///   transparent, the white spill is un-multiplied back out of the edge pixels, and the neutral grey drop
    ///   shadow under the pedestal is dropped rather than shipped as a lit grey shape on a black panel.
    ///
    /// The two hero pieces are the same painting at different rows: <c>Asset6</c> is the ghost standing on the
    /// pedestal and <c>Asset7</c> is the pedestal alone, and the two images agree pixel for pixel below the
    /// rim (measured: identical under row 709). The ghost is therefore cut at row 711 and the pedestal is
    /// kept from row 680 down, so the ghost can be animated onto a pedestal that is already standing — and
    /// the rim it is cut at is the rim that hides the cut.
    ///
    /// Every generated sprite keeps the source's full 1024 x 1024 frame, so the layers stay registered to
    /// each other and to the composition in <c>Final-Shot.jpeg</c>: the panel stacks them in one rect.
    /// </summary>
    public static class VictoryEmoteArt
    {
        public const string SourceFolder = "Victory-emote";
        public const string SpriteFolder = "Assets/Art/Sprites/Victory";
        public const string GlowMaterialPath = "Assets/Art/Materials/UI_Additive.mat";

        // Source sheet names as delivered, including the two misspellings, because renaming the designer's
        // files in place is how a folder of references stops matching the folder of exports.
        const string SwirlsSource = "Asset01.jpeg";
        const string ConfettiSource = "Asset02.jpeg";
        const string TitleSource = "Asseet03.jpeg";
        const string GhostSource = "Asset6.jpeg";
        const string PedestalSource = "Asset7.jpeg";

        /// <summary>The two hero pieces, in source rows. Kept as numbers because the seam is a measurement.</summary>
        const int GhostCutRow = 711;
        const int PedestalTopRow = 680;

        /// <summary>The white-background matte. Values chosen against the delivered sheets, not by taste.</summary>
        const float PaperLevel = 245f;
        const float PaperBand = 12f;
        const float NeutralChroma = 14f;
        const float NeutralMin = 130f;

        public struct LayerSet
        {
            public Material Glow;
            public Sprite Flash;
            public Sprite Swirls;
            public Sprite Confetti;
            public Sprite Title;
            public Sprite Pedestal;
            public Sprite Ghost;
        }

        /// <summary>Every sprite the victory panel needs, generated once and re-generated when a sheet changes.</summary>
        public static LayerSet EnsureAll()
        {
            return new LayerSet
            {
                Glow = EnsureGlowMaterial(),
                Flash = EnsureFlashSprite(),
                Swirls = EnsureGlowSprite("Victory_Swirls", SwirlsSource),
                Confetti = EnsureGlowSprite("Victory_Confetti", ConfettiSource),
                Title = EnsureGlowSprite("Victory_Title", TitleSource, keepRows: 310),
                Pedestal = EnsureCutoutSprite("Victory_Pedestal", PedestalSource, PedestalTopRow, 1024),
                Ghost = EnsureCutoutSprite("Victory_Ghost", GhostSource, 0, GhostCutRow)
            };
        }

        public static Material EnsureGlowMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            Shader shader = Shader.Find("EchoMaze/UI Additive");
            if (shader == null)
            {
                Debug.LogError("EchoMaze/UI Additive not found. Is Assets/Art/Shaders/UiAdditive.shader imported? " +
                               "The victory emote's glow layers will draw as black squares without it.");
                return null;
            }

            if (existing == null)
            {
                SceneKit.EnsureFolder("Assets/Art/Materials");
                existing = new Material(shader) { name = "UI_Additive" };
                AssetDatabase.CreateAsset(existing, GlowMaterialPath);
            }

            existing.shader = shader;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static string SpritePath(string name) => SpriteFolder + "/" + name + ".png";

        static Sprite EnsureGlowSprite(string name, string source, int keepRows = 1024)
        {
            string path = SpritePath(name);
            if (NeedsGeneration(path, source))
            {
                Texture2D sheet = LoadSheet(source);
                if (sheet == null)
                {
                    return null;
                }

                Color32[] pixels = sheet.GetPixels32();
                int width = sheet.width;
                int height = sheet.height;
                Object.DestroyImmediate(sheet);

                if (keepRows < height)
                {
                    KeepRows(pixels, width, height, 0, keepRows);
                }

                for (int i = 0; i < pixels.Length; i++)
                {
                    // Colour only: what is not light is black, and black is what the additive shader
                    // discards. Writing 255 into the alpha keeps the sheet usable by a plain Image too.
                    pixels[i].a = 255;
                }

                WriteSprite(path, width, height, pixels);
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite EnsureCutoutSprite(string name, string source, int topRow, int bottomRow)
        {
            string path = SpritePath(name);
            if (NeedsGeneration(path, source))
            {
                Texture2D sheet = LoadSheet(source);
                if (sheet == null)
                {
                    return null;
                }

                Color32[] pixels = sheet.GetPixels32();
                int width = sheet.width;
                int height = sheet.height;
                Object.DestroyImmediate(sheet);

                // Key first, then crop: the key reads "is this paper", and a row already blanked to
                // transparent black would answer no — it would come back as opaque black.
                KeyPaper(pixels);
                KeepRows(pixels, width, height, topRow, bottomRow);
                WriteSprite(path, width, height, pixels);
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// The paper key. A pixel is as opaque as it is far from the paper colour, and its colour is
        /// un-multiplied back out of the white it was composited on — without that step every edge keeps a
        /// pale fringe, which is exactly what a black panel shows up most.
        ///
        /// The neutral-grey rule exists for the pedestal's drop shadow: it is a grey wash on white, so it is
        /// near-paper in saturation and would otherwise ship as an opaque grey cloud floating on the panel.
        /// </summary>
        static void KeyPaper(Color32[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                int min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                int max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));

                float alpha = Mathf.Clamp01((PaperLevel - min) / PaperBand);
                if (max - min <= NeutralChroma && min >= NeutralMin)
                {
                    alpha = 0f;
                }

                if (alpha <= 0.004f)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                // Un-multiply the matte from white: c = (source - (1 - a) * paper) / a, clamped.
                float inverse = 1f - alpha;
                pixels[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt((pixel.r - inverse * 255f) / alpha), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt((pixel.g - inverse * 255f) / alpha), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt((pixel.b - inverse * 255f) / alpha), 0, 255),
                    (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        /// <summary>
        /// The pop the emote opens with: a soft core with one thin ring in it, white so the Image's own
        /// colour decides whether it reads as an echo or a spark. Additive, like the other light layers.
        /// </summary>
        static Sprite EnsureFlashSprite()
        {
            const string name = "Victory_Flash";
            string path = SpritePath(name);
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null)
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            const int size = 512;
            var pixels = new Color32[size * size];
            float centre = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);

                    float core = Mathf.Exp(-radius * radius * 7f) * 0.55f;
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.52f) / 0.035f);
                    float alpha = Mathf.Clamp01(core + ring * 0.9f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            WriteSprite(path, size, size, pixels);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Blank every row outside <paramref name="topRow"/>..<paramref name="bottomRow"/>, measured from the
        /// top of the picture the way a person reads the sheet. A texture's row zero is its bottom, which is
        /// the one conversion in this file that is easy to get backwards and invisible until the emote plays.
        /// </summary>
        static void KeepRows(Color32[] pixels, int width, int height, int topRow, int bottomRow)
        {
            for (int row = 0; row < height; row++)
            {
                int pictureRow = height - 1 - row;
                if (pictureRow >= topRow && pictureRow < bottomRow)
                {
                    continue;
                }

                int start = row * width;
                for (int x = 0; x < width; x++)
                {
                    pixels[start + x] = new Color32(0, 0, 0, 0);
                }
            }
        }

        static bool NeedsGeneration(string path, string source)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
            {
                return true;
            }

            string sheet = SourcePath(source);
            if (!System.IO.File.Exists(sheet))
            {
                return false;
            }

            return System.IO.File.GetLastWriteTimeUtc(sheet) > System.IO.File.GetLastWriteTimeUtc(path);
        }

        static string SourcePath(string file) =>
            System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", SourceFolder, file));

        static Texture2D LoadSheet(string file)
        {
            string path = SourcePath(file);
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError("VictoryEmoteArt: " + path + " is missing. The victory emote cannot be built " +
                               "from it; restore the delivery sheet in " + SourceFolder + "/.");
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(System.IO.File.ReadAllBytes(path)))
            {
                Debug.LogError("VictoryEmoteArt: " + path + " could not be decoded as an image.");
                Object.DestroyImmediate(texture);
                return null;
            }

            return texture;
        }

        static void WriteSprite(string path, int width, int height, Color32[] pixels)
        {
            SceneKit.EnsureFolder(SpriteFolder);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            // The glow sheets are gradients over a black field and the matted ones are line art: both are the
            // first thing block compression ruins, and there are only six of them.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }
}
