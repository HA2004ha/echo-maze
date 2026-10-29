using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using EchoMaze.Core;
using EchoMaze.Data;
using EchoMaze.Gameplay;
using EchoMaze.UI;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// The building blocks every scene in this project is made of, written with editor APIs instead of
    /// hand-edited YAML so the .meta files, GUIDs and serialization versions are always the ones this
    /// editor expects.
    ///
    /// All of it is idempotent: an object that already exists is reconfigured, not duplicated, so a
    /// builder can be re-run after a reset or from batchmode. Anything the designer is expected to touch
    /// is set through SerializedObject on the real serialized field, so the scene and the code agree about
    /// which references are required (see Docs/TEAM_SETUP.md, the objectReference: {fileID: 0} note).
    /// </summary>
    public static class SceneKit
    {
        const string TuningPath = "Assets/Data/Echo/EchoTuning.asset";
        const string VolumePath = "Assets/Settings/EchoVolumeProfile.asset";
        const string MaterialPath = "Assets/Art/Materials/Arc_Cyan.mat";
        const string PlayerDotPath = "Assets/Art/Sprites/Player_dot.png";

        const string PoseFolder = "Assets/Art/Sprites/Player";

        /// <summary>
        /// The pose sheet, in the order <c>EnsurePlayer</c> wires it: the three cardinals and the two
        /// diagonals <c>Docs/ISSUES.md</c> #2 was written for. All five are required — the fallback is the
        /// placeholder dot rather than a half-dressed character who freezes on a diagonal.
        ///
        /// There is deliberately no left-profile file: the two profiles were bit-exact mirrors (measured
        /// again on the 4096 delivery, after one non-mirrored export went round and came back — issue #5), so
        /// facing left is one <c>flipX</c>. The two diagonals are flipped the same way, which takes the set
        /// from five files to eight poses.
        /// </summary>
        static readonly string[] PosePaths =
        {
            PoseFolder + "/Player_front.png",
            PoseFolder + "/Player_back.png",
            PoseFolder + "/Player_side.png",
            PoseFolder + "/Player_34_front.png",
            PoseFolder + "/Player_34_back.png"
        };

        /// <summary>
        /// The canvas is 4096 source pixels and must land at 1.6 world units, so 2560. A drawn figure of
        /// 2528 px at this PPU is 0.99 world units, which is 89 screen pixels at the 1080p target and
        /// deliberately the same footprint the placeholder dot already had (16 px at PPU 16), so no camera
        /// framing or readability number has to be re-read because of the art.
        ///
        /// The number is tied to the *source* resolution, not to what is uploaded: <c>maxTextureSize</c>
        /// does not change a sprite's world size, Unity rescales the effective PPU instead (measured on the
        /// 4096 originals — imported at max 1024 the sprite still reports 6.4 units at PPU 640, because its
        /// live pixelsPerUnit became 160). So re-deriving this constant is the first thing to do if the art
        /// is ever delivered at another canvas size, and <c>PlayerPoseImportTests</c> is what fails when
        /// someone forgets.
        /// </summary>
        const float PosePixelsPerUnit = 2560f;

        const string CursorRingPath = "Assets/Art/Sprites/Cursor_ring.png";
        const string CursorDotPath = "Assets/Art/Sprites/Cursor_dot.png";
        const string CursorHeadPath = "Assets/Art/Sprites/Cursor_head.png";

        /// <summary>
        /// The aim cursor is drawn 1:1 with screen pixels, so its two constants are the same number read two
        /// ways: 48 texels at 90 texels-per-unit is 0.533 world units, and the blockout camera puts one world
        /// unit at ~90 screen pixels (Docs/TUNING.md), so 48 texels is 48 pixels at the 1080p target. That is
        /// what makes an anti-aliased circle here a crisp one on screen, and it is the same reasoning that
        /// gives the placeholder dot PPU 16 rather than a downscaled sheet.
        /// </summary>
        const int CursorTexels = 48;

        const float CursorPixelsPerUnit = 90f;

        // Strokes, in texels, measured from the canvas centre (24, 24). The weight is one number on
        // purpose: the game is drawn entirely in thin lines and a reticle with a different line weight to
        // the arcs would look like it belongs to another game.
        const float RingRadius = 15f;
        const float RingHalfWidth = 0.7f;

        /// <summary>
        /// 1.4 and not 1.0, which is the "two pixels" the design asks for: analytic anti-aliasing is honest
        /// about area, and a disc of radius 1 covers 3.14 texels spread over four pixels, so its brightest
        /// pixel is 0.76 and the dot's carefully budgeted brightness never arrives. At 1.4 a whole pixel
        /// fits inside the disc, the peak is 1, and the mark is still under three screen pixels across.
        /// </summary>
        const float DotRadius = 1.4f;

        const float HeadArcRadius = 18.5f;
        const float HeadArcHalfWidth = 0.8f;
        const float HeadArcHalfAngleDegrees = 18f;
        const float HeadArcAngleFeather = 5f;
        const float HeadTip = 21.5f;
        const float HeadWingDepth = 3.3f;
        const float HeadWingHalf = 3.4f;

        /// <summary>
        /// The canvas centre in texel coordinates, which is the geometric middle rather than the average of
        /// the pixel indices: the sprite pivot lands here, and every element is measured from it.
        /// </summary>
        const float CursorCentre = CursorTexels * 0.5f;

        /// <summary>
        /// The head's arc sits at r18.5 and not on the ring at r15, which is the obvious place for it, because
        /// two SpriteRenderers stack: an arc drawn over the ring composites to 1 - (1-ring)(1-head), which at
        /// a full charge would be 0.75 alpha and land at 0.62 luminance — over Bloom's 0.55 threshold, so the
        /// interface would glow exactly when the player is loudest, which is the one thing GDD 3.0 forbids.
        /// The ring's ink stops at r16 and the head's starts at r17.4, so the brightest pixel on screen is one
        /// element's alpha and AimCursorMath.AlphaCeiling really is the whole budget. The arc and the chevron
        /// may overlap each other freely: they are one sprite, and one sprite takes one tint.
        ///
        /// Feather, likewise, is one number for every edge in the set: the ramp is what makes a 1.4-texel
        /// stroke survive being drawn at one texel per screen pixel, and two softnesses in one glyph reads as
        /// one of them being out of focus.
        /// </summary>
        const float StrokeFeather = 0.55f;

        const int CursorSupersample = 4;

        public static EchoTuningProfile EnsureTuning()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EchoTuningProfile>(TuningPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder("Assets/Data/Echo");
            var profile = ScriptableObject.CreateInstance<EchoTuningProfile>();
            profile.name = "EchoTuning";
            AssetDatabase.CreateAsset(profile, TuningPath);
            Debug.Log("Created " + TuningPath);
            return profile;
        }

        public static VolumeProfile EnsureVolumeProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (existing != null)
            {
                return existing;
            }

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(0.55f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.65f);
            bloom.clamp.Override(20f);
            bloom.tint.Override(new Color(0.498f, 0.914f, 0.941f, 1f));
            bloom.filter.Override(BloomFilterMode.Gaussian);
            AssetDatabase.CreateAsset(profile, VolumePath);
            Debug.Log("Created " + VolumePath);
            return profile;
        }

        public static Material EnsureMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("EchoMaze/EchoArc");
            if (shader == null)
            {
                Debug.LogError("EchoMaze/EchoArc not found. Is Assets/Art/Shaders/EchoArc.shader imported?");
                return null;
            }

            EnsureFolder("Assets/Art/Materials");
            var material = new Material(shader) { name = "Arc_Cyan" };
            material.SetFloat("_Hdr", 2.5f);
            material.SetFloat("_Core", 0.22f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            Debug.Log("Created " + MaterialPath);
            return material;
        }

        public static Sprite EnsurePlayerDot()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerDotPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder("Assets/Art/Sprites");
            var texture = new Texture2D(16, 16);
            var centre = new Vector2(7.5f, 7.5f);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float d = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), centre) / 7f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, d * d));
                }
            }

            texture.Apply();
            System.IO.File.WriteAllBytes(PlayerDotPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(PlayerDotPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(PlayerDotPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 16f;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(PlayerDotPath);
        }

        /// <summary>
        /// A pose sprite from disk, imported the way the game assumes, or null if the file is not there.
        /// Null is a warning and never an error: a bootstrap that shouts in the console every time the
        /// art team renames a file trains people to ignore the console.
        /// </summary>
        public static Sprite EnsurePoseSprite(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            {
                return null;
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && ConfigurePoseImporter(importer))
            {
                importer.SaveAndReimport();
            }

            // Reload: a reimport destroys the object this call may have returned a moment ago.
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Returns whether anything actually changed, so a bootstrap re-run does not reimport six
        /// textures and rewrite six .meta files to set values they already have.
        ///
        /// Mipmaps are the one place this deliberately diverges from <see cref="EnsurePlayerDot"/>:
        /// the dot is 16 source pixels drawn 1:1, this art is 2528 drawn pixels uploaded as 1024 and
        /// squeezed into 89 on screen, and a downscale that deep without a mip chain shimmers as the
        /// character walks and breaks the rim light into noise.
        /// </summary>
        static bool ConfigurePoseImporter(TextureImporter importer)
        {
            bool changed = false;

            void Set(bool current, bool value, System.Action apply)
            {
                if (current != value)
                {
                    apply();
                    changed = true;
                }
            }

            Set(importer.textureType == TextureImporterType.Sprite, true,
                () => importer.textureType = TextureImporterType.Sprite);
            Set(importer.spriteImportMode == SpriteImportMode.Single, true,
                () => importer.spriteImportMode = SpriteImportMode.Single);
            Set(importer.sRGBTexture, true, () => importer.sRGBTexture = true);
            Set(importer.alphaSource == TextureImporterAlphaSource.FromInput, true,
                () => importer.alphaSource = TextureImporterAlphaSource.FromInput);
            Set(importer.alphaIsTransparency, true, () => importer.alphaIsTransparency = true);
            Set(importer.mipmapEnabled, true, () => importer.mipmapEnabled = true);

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, PosePixelsPerUnit))
            {
                importer.spritePixelsPerUnit = PosePixelsPerUnit;
                changed = true;
            }

            // The drawn figure sits within 36 canvas pixels of (2048, 2048) in every file, which at this PPU
            // is 1.3 screen pixels: a centre pivot is the character standing where the transform is.
            if (importer.spritePivot != new Vector2(0.5f, 0.5f))
            {
                importer.spritePivot = new Vector2(0.5f, 0.5f);
                changed = true;
            }

            if (importer.filterMode != FilterMode.Trilinear)
            {
                importer.filterMode = FilterMode.Trilinear;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }

            // A 4x downscale of a 4096 canvas. The world size is unaffected (see PosePixelsPerUnit), so
            // this buys texture memory against art that is drawn 89 pixels tall and will never be zoomed.
            if (importer.maxTextureSize != 1024)
            {
                importer.maxTextureSize = 1024;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// One of the three aim-cursor files: drawn here, written to disk as a PNG, imported with the settings
        /// the runtime expects, and returned as a sprite. The bytes are the artifact and this code is their
        /// source, which is why the file is rewritten whenever it stops matching what the drawer produces —
        /// a committed PNG that the generator has silently moved away from is the worst kind of drift, because
        /// the art looks right in the editor and only the numbers in the code have changed.
        ///
        /// The shape is drawn analytically at four samples per texel and averaged, rather than rasterised at
        /// 48x48: the whole look of this game is thin lines, and a jagged circle is the one failure an asset
        /// class like this cannot survive. White RGB with the alpha carrying coverage is the same contract
        /// <see cref="TouchMarks"/> builds its dashes on, so the tint on the renderer is the only place colour
        /// lives.
        /// </summary>
        static Sprite EnsureCursorSprite(string path, System.Func<float, float, float> coverage)
        {
            EnsureFolder("Assets/Art/Sprites");

            float step = 1f / CursorSupersample;
            float weight = 1f / (CursorSupersample * CursorSupersample);
            var pixels = new Color32[CursorTexels * CursorTexels];
            for (int py = 0; py < CursorTexels; py++)
            {
                for (int px = 0; px < CursorTexels; px++)
                {
                    float sum = 0f;
                    for (int sy = 0; sy < CursorSupersample; sy++)
                    {
                        for (int sx = 0; sx < CursorSupersample; sx++)
                        {
                            sum += coverage(px + (sx + 0.5f) * step, py + (sy + 0.5f) * step);
                        }
                    }

                    pixels[py * CursorTexels + px] = new Color32(
                        255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(sum * weight) * 255f));
                }
            }

            var texture = new Texture2D(CursorTexels, CursorTexels, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            byte[] bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            bool rewrote = !BytesEqual(System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null,
                                       bytes);
            if (rewrote)
            {
                System.IO.File.WriteAllBytes(path, bytes);
            }

            // ForceUpdate on a rewritten file, plain import on one that already matched: the second case is
            // the bootstrap re-run, and it must not churn a .meta it has nothing to change.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && ConfigureCursorImporter(importer))
            {
                importer.SaveAndReimport();
            }

            // Reload: a reimport destroys the object a previous call may have returned.
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The one anti-aliasing primitive: 1 well inside the boundary of a shape, 0 well outside it, ramping
        /// between the two over <c>feather</c> texels. Every element of the cursor is one call to this, so the
        /// ring, the dot and the head cannot disagree about how soft an edge is.
        /// </summary>
        static float Edge(float signedDistance, float feather)
        {
            return Mathf.Clamp01(0.5f - signedDistance / feather);
        }

        /// <summary>
        /// Canvas coordinates with (0, 0) at the bottom-left, the way a texture row is numbered, so y grows
        /// upward and an angle measured with Atan2 turns the same way <c>Quaternion.Euler(0, 0, degrees)</c>
        /// turns. The centre is the geometric 24 and not the 23.5 the pixel indices average to, because 24 is
        /// where the sprite pivot is: a head drawn half a texel off the pivot would orbit the wrong point.
        /// </summary>
        static float DistanceFromCentre(float x, float y)
        {
            float dx = x - CursorCentre;
            float dy = y - CursorCentre;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float RingCoverage(float x, float y)
        {
            return Edge(Mathf.Abs(DistanceFromCentre(x, y) - RingRadius) - RingHalfWidth, StrokeFeather);
        }

        static float DotCoverage(float x, float y)
        {
            return Edge(DistanceFromCentre(x, y) - DotRadius, StrokeFeather);
        }

        /// <summary>
        /// The direction head: a 36-degree bracket curved along the ring, and a 90-degree chevron that grows
        /// out of the middle of it with its tip at r21.5 — the farthest thing any cursor sprite draws.
        /// Authored pointing along +x, which is atan2's zero, so the rotation the runtime applies is the aim
        /// angle itself and not the aim angle plus an offset somebody has to remember when the sprite is
        /// redrawn.
        ///
        /// The arc is wide enough to be read as a bracket rather than as part of the arrow, and its ends are
        /// outside the chevron; a 24-degree span at this radius sat inside the arrowhead entirely and drew the
        /// glyph twice in the same four pixels (caught by previewing the generated PNG, not by looking at the
        /// numbers).
        ///
        /// The arc and the chevron are maxed, never added: two strokes of one sprite crossing is still one
        /// sprite, and a summed alpha would brighten the crossing above every state AimCursorMath budgets.
        /// </summary>
        static float HeadCoverage(float x, float y)
        {
            float dx = x - CursorCentre;
            float dy = y - CursorCentre;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float fromPlusX = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);

            float arc = Edge(Mathf.Abs(distance - HeadArcRadius) - HeadArcHalfWidth, StrokeFeather) *
                        Edge(fromPlusX - HeadArcHalfAngleDegrees, HeadArcAngleFeather);

            float wing = HeadTip - HeadWingDepth;
            float upper = SegmentCoverage(dx, dy, HeadTip, 0f, wing, HeadWingHalf, RingHalfWidth);
            float lower = SegmentCoverage(dx, dy, HeadTip, 0f, wing, -HeadWingHalf, RingHalfWidth);

            return Mathf.Max(arc, Mathf.Max(upper, lower));
        }

        static float SegmentCoverage(float px, float py, float ax, float ay, float bx, float by,
                                     float halfWidth)
        {
            float vx = bx - ax;
            float vy = by - ay;
            float wx = px - ax;
            float wy = py - ay;
            float t = Mathf.Clamp01((wx * vx + wy * vy) / (vx * vx + vy * vy));
            float nx = wx - t * vx;
            float ny = wy - t * vy;

            return Edge(Mathf.Sqrt(nx * nx + ny * ny) - halfWidth, StrokeFeather);
        }

        /// <summary>
        /// Returns whether anything changed, for the same reason <see cref="ConfigurePoseImporter"/> does.
        ///
        /// Mipmaps off and bilinear is the deliberate split from the poses, and it is the same split the
        /// placeholder dot already makes: this art is authored at the size it is drawn, one texel to one
        /// screen pixel at the 1080p target, so there is no downscale for a mip chain to rescue and a chain
        /// would only blur a 1.4-texel stroke the moment the screen is not exactly 1080p.
        /// </summary>
        static bool ConfigureCursorImporter(TextureImporter importer)
        {
            bool changed = false;

            void Set(bool current, bool value, System.Action apply)
            {
                if (current != value)
                {
                    apply();
                    changed = true;
                }
            }

            Set(importer.textureType == TextureImporterType.Sprite, true,
                () => importer.textureType = TextureImporterType.Sprite);
            Set(importer.spriteImportMode == SpriteImportMode.Single, true,
                () => importer.spriteImportMode = SpriteImportMode.Single);
            Set(importer.sRGBTexture, true, () => importer.sRGBTexture = true);
            Set(importer.alphaSource == TextureImporterAlphaSource.FromInput, true,
                () => importer.alphaSource = TextureImporterAlphaSource.FromInput);
            Set(importer.alphaIsTransparency, true, () => importer.alphaIsTransparency = true);
            Set(importer.mipmapEnabled, false, () => importer.mipmapEnabled = false);

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, CursorPixelsPerUnit))
            {
                importer.spritePixelsPerUnit = CursorPixelsPerUnit;
                changed = true;
            }

            // Concentric with the canvas, which is the aim point: the ring, the dot and the head are three
            // sprites that must land on each other exactly, and the head rotates about this point.
            if (importer.spritePivot != new Vector2(0.5f, 0.5f))
            {
                importer.spritePivot = new Vector2(0.5f, 0.5f);
                changed = true;
            }

            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }

            if (importer.maxTextureSize < CursorTexels)
            {
                importer.maxTextureSize = CursorTexels;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// The camera the wave was tuned against: orthographic, pure black, HDR on so Bloom can catch the
        /// cyan, and the near/far planes wide because the ribbon is drawn in the z = 0 plane while the
        /// capture code sometimes sits elsewhere.
        /// </summary>
        public static void ConfigureCamera(float orthographicSize = 6f)
        {
            Camera camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            // Unity 6 has no Camera.renderPostProcessing; the checkbox lives on URP's camera data.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.nearClipPlane = -100f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        public static void EnsureVolume(VolumeProfile profile)
        {
            Volume volume = Object.FindAnyObjectByType<Volume>();
            if (volume == null)
            {
                volume = new GameObject("Echo Volume").AddComponent<Volume>();
            }

            volume.isGlobal = true;
            volume.priority = 100;
            volume.sharedProfile = profile;
        }

        public static EchoRenderer EnsureEchoField(Material material)
        {
            var field = Object.FindAnyObjectByType<EchoRenderer>();
            if (field == null)
            {
                field = new GameObject("EchoField").AddComponent<EchoRenderer>();
            }

            var renderer = field.GetComponent<MeshRenderer>();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            renderer.sortingLayerName = "Echo";
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return field;
        }

        public static void RemoveGlobalLights2D()
        {
            foreach (Light2D stray in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Exclude))
            {
                // The game is unlit; a Global Light 2D only adds a lighting pass and a chance of the
                // wave being multiplied by an ambient colour instead of glowing.
                Object.DestroyImmediate(stray.gameObject);
            }
        }

        public static GameObject EnsureShape(string name, Vector2 position, Shape shape,
                                            SurfaceKind kind, float reflectivity = 1f)
        {
            GameObject go = FindOrCreate(name);
            go.transform.position = position;
            go.layer = shape.trigger ? LayerMask.NameToLayer("Trigger") : LayerMask.NameToLayer("EchoTarget");

            foreach (Collider2D old in go.GetComponents<Collider2D>())
            {
                Object.DestroyImmediate(old);
            }

            shape.Assign(go);

            var surface = go.GetComponent<EchoSurface>();
            if (surface == null)
            {
                surface = go.AddComponent<EchoSurface>();
            }

            surface.kind = kind;
            surface.reflectivity = reflectivity;
            return go;
        }

        /// <summary>
        /// A wall the eye cannot see and the wave can: box collider, EchoTarget layer, Plain signature and
        /// reflectivity 1, which means "read your brightness from your own curvature" after GDD 3.0.
        /// </summary>
        public static GameObject EnsureWall(string name, Vector2 centre, Vector2 size) =>
            EnsureShape(name, centre, Shape.Box(size.x, size.y), SurfaceKind.Plain, 1f);

        public static GameObject EnsurePlayer(Vector2 spawn)
        {
            GameObject player = FindOrCreate("Player");
            player.transform.position = spawn;
            player.layer = LayerMask.NameToLayer("Player");

            // A PlayerMock from week 1 leaves a missing script behind when the class is deleted, and a
            // missing script cannot be destroyed by hand. Rather than carry one into the blockout, the
            // object that still has one is rebuilt from scratch.
            if (HasMissingScript(player))
            {
                Object.DestroyImmediate(player);
                player = new GameObject("Player");
            }

            var body = EnsureComponent<Rigidbody2D>(player);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = EnsureComponent<CircleCollider2D>(player);
            collider.radius = 0.25f;

            EnsureComponent<Player>(player);

            // Configure every pose file, wired or not, so the ones a later change un-wires cannot be
            // committed with someone else's defaults.
            Sprite[] poses = new Sprite[PosePaths.Length];
            bool complete = true;
            for (int i = 0; i < PosePaths.Length; i++)
            {
                poses[i] = EnsurePoseSprite(PosePaths[i]);
                complete &= poses[i] != null;
            }

            Transform visuals = EnsureVisualsChild(player);
            var sprite = EnsureComponent<SpriteRenderer>(visuals.gameObject);
            sprite.sortingLayerName = "Default";

            if (complete)
            {
                // Full opacity, no tint: the art is a near-black body with a cyan rim and glowing eyes, so
                // in a black room only the outline and the eyes read anyway. The old faint disc was a
                // placeholder, not a look. It has to be a real asset either way — a runtime Sprite.Create
                // reference cannot survive a scene save.
                sprite.sprite = poses[0];
                sprite.color = Color.white;

                var visual = EnsureComponent<PlayerVisual>(player);
                SetReference(visual, "frontSprite", poses[0]);
                SetReference(visual, "backSprite", poses[1]);
                SetReference(visual, "sideSprite", poses[2]);
                SetReference(visual, "diagonalDownSprite", poses[3]);
                SetReference(visual, "diagonalUpSprite", poses[4]);

                // The hysteresis band is a fraction of a sector, so it is a function of the pose count and
                // not a taste: a scene generated while the fan had four poses still has the old 10 degrees
                // serialised in it, and running the bootstrap is the thing that has to correct that rather
                // than an inspector edit nobody remembers to make.
                SetFloat(visual, "boundaryDegrees", 5f);
            }
            else
            {
                Debug.LogWarning("Player pose art missing under " + PoseFolder + " (need all of " +
                                 string.Join(", ", PosePaths) + "); falling back to the placeholder dot.",
                                 player);

                Sprite dot = EnsurePlayerDot();
                if (dot != null)
                {
                    sprite.sprite = dot;
                }

                sprite.color = new Color(0.25f, 0.45f, 0.47f, 0.35f);
            }

            return player;
        }

        /// <summary>
        /// The pointer the game draws in place of the one the operating system draws: a ring, a dot on the
        /// exact aim point, and a head that turns to the angle the face and the cone are already using.
        ///
        /// Flat and world-space, deliberately not a child of the player. The root is placed by projecting the
        /// screen pointer, so parenting it to the body would add the body's position to a coordinate that is
        /// already absolute — and the body is also what the pointer is measured against for the parked state.
        ///
        /// Returns null with a warning, never an error, when the art could not be written: the component
        /// disables itself on an incomplete setup and <c>Cursor.visible</c> is left alone, so a machine that
        /// cannot have the reticle keeps the pointer it already has.
        /// </summary>
        public static GameObject EnsureAimCursor(GameObject player)
        {
            Sprite ring = EnsureCursorSprite(CursorRingPath, RingCoverage);
            Sprite dot = EnsureCursorSprite(CursorDotPath, DotCoverage);
            Sprite head = EnsureCursorSprite(CursorHeadPath, HeadCoverage);
            if (ring == null || dot == null || head == null)
            {
                Debug.LogWarning("Aim cursor art unavailable (" + CursorRingPath + ", " + CursorDotPath +
                                 ", " + CursorHeadPath + "); the system cursor stays and the aim is unchanged.");
                return null;
            }

            GameObject cursorObject = FindOrCreate("Aim Cursor");
            cursorObject.layer = 0;

            // Where the pointer happened to be when someone saved the scene is not scene state. LateUpdate
            // overwrites this every frame; the reset is so the editor view does not open with a reticle
            // sitting on the player.
            cursorObject.transform.position = Vector3.zero;
            cursorObject.transform.rotation = Quaternion.identity;
            cursorObject.transform.localScale = Vector3.one;

            var cursor = EnsureComponent<AimCursor>(cursorObject);
            SetReference(cursor, "player", player.GetComponent<Player>());
            SetReference(cursor, "ringSprite", ring);
            SetReference(cursor, "dotSprite", dot);
            SetReference(cursor, "headSprite", head);

            // The radius that decides when the head goes slack is PlayerVisual's number, not a second opinion
            // on it. Read off the component this scene already has and written here, so an inspector tweak is
            // carried over by the next bootstrap run rather than leaving two fields that quietly disagree
            // about where the aim stops meaning anything.
            var visual = player.GetComponent<PlayerVisual>();
            if (visual != null)
            {
                SerializedProperty noise = SerializedPropertyOf(visual, "aimNoiseRadius");
                if (noise != null)
                {
                    SetFloat(cursor, "parkRadius", noise.floatValue);
                }
            }

            // The layer and the order are also set at runtime by AimCursor.Awake; setting them here is what
            // makes the scene correct in the editor and in a capture taken before the first LateUpdate.
            EnsureCursorChild(cursorObject, "Ring", ring, 0);
            EnsureCursorChild(cursorObject, "Dot", dot, 1);
            EnsureCursorChild(cursorObject, "Head", head, 2);

            return cursorObject;
        }

        static Transform EnsureCursorChild(GameObject root, string name, Sprite sprite, int order)
        {
            Transform child = root.transform.Find(name);
            if (child == null)
            {
                GameObject created = new GameObject(name);
                created.transform.SetParent(root.transform, false);
                child = created.transform;
            }

            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;

            var renderer = EnsureComponent<SpriteRenderer>(child.gameObject);
            renderer.sprite = sprite;
            renderer.sortingLayerName = "HUD";
            renderer.sortingOrder = order;

            return child;
        }

        /// <summary>
        /// The sprite lives on a child, not on the player. The root carries the Rigidbody2D and the
        /// collider, so squash, bob and lean written there would resize what collides with walls and
        /// fight the body's interpolation. Resetting the local transform also un-does any animation
        /// pose that happened to be baked into the scene when it was saved.
        /// </summary>
        static Transform EnsureVisualsChild(GameObject player)
        {
            foreach (SpriteRenderer stray in player.GetComponents<SpriteRenderer>())
            {
                Object.DestroyImmediate(stray);
            }

            Transform child = player.transform.Find("Visuals");
            if (child == null)
            {
                var go = new GameObject("Visuals");
                go.transform.SetParent(player.transform, false);
                child = go.transform;
            }

            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        public static TouchMarks EnsureTouchMarks()
        {
            GameObject go = FindOrCreate("Touch Marks");
            return EnsureComponent<TouchMarks>(go);
        }

        /// <summary>
        /// The Crawler's *body*, which is the whole point of it: two circle colliders with a gap, so the
        /// wave reads two bumps where a wall reads one line. Nothing here knows it is an enemy.
        /// </summary>
        public static Crawler EnsureCrawler(Vector2 start, Vector2[] patrol, EchoDirector director, Player player)
        {
            GameObject go = FindOrCreate("Crawler");
            go.transform.position = start;
            go.layer = LayerMask.NameToLayer("Enemy");

            foreach (Collider2D old in go.GetComponents<Collider2D>())
            {
                Object.DestroyImmediate(old);
            }

            var body = EnsureComponent<Rigidbody2D>(go);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            const float bumpRadius = 0.32f;
            const float bumpSeparation = 0.42f;
            CircleCollider2D left = go.AddComponent<CircleCollider2D>();
            left.radius = bumpRadius;
            left.offset = new Vector2(-bumpSeparation, 0f);
            CircleCollider2D right = go.AddComponent<CircleCollider2D>();
            right.radius = bumpRadius;
            right.offset = new Vector2(bumpSeparation, 0f);

            var surface = EnsureComponent<EchoSurface>(go);
            surface.kind = SurfaceKind.Crawler;
            // 1, not a per-object amplitude: the creature's brightness comes from its own curvature and its
            // heartbeat comes from the shimmer, which is keyed to this kind and nothing else.
            surface.reflectivity = 1f;

            var crawler = EnsureComponent<Crawler>(go);
            SetReference(crawler, "director", director);
            SetReference(crawler, "player", player);
            SetVector2Array(crawler, "patrolPoints", patrol);
            return crawler;
        }

        public static EchoDirector EnsureDirector(EchoTuningProfile tuning, EchoRenderer field,
                                                  EchoDirector.SourceMode sourceMode
                                                  = EchoDirector.SourceMode.Microphone)
        {
            var director = Object.FindAnyObjectByType<EchoDirector>();
            if (director == null)
            {
                director = new GameObject("Echo System").AddComponent<EchoDirector>();
            }

            int mask = 0;
            foreach (string layer in new[] { "World", "EchoTarget", "Absorber", "Enemy" })
            {
                int index = LayerMask.NameToLayer(layer);
                if (index >= 0)
                {
                    mask |= 1 << index;
                }
            }

            SetReference(director, "profile", tuning);
            SetInt(director, "echoLayerMask", mask);
            SetInt(director, "sourceMode", (int)sourceMode);
            SetReference(director, "fieldRenderer", field);

            if (tuning == null)
            {
                // Without the profile the director falls back to code defaults and logs a warning on every
                // Play, which is exactly the failure this bootstrap is meant to prevent. A profile that was
                // loaded before OpenScene comes back as a destroyed object, which == null catches.
                Debug.LogError("EchoDirector was saved without " + TuningPath + "; the scene ships untuned.");
            }

            return director;
        }

        public static RunController EnsureRunController(Player player, GameObject deathNotice)
        {
            GameObject go = FindOrCreate("Run Controller");
            var controller = EnsureComponent<RunController>(go);
            SetReference(controller, "player", player);
            SetReference(controller, "deathNotice", deathNotice);
            return controller;
        }

        /// <summary>
        /// The lose screen: a black overlay with the GDD's wording. Screen Space - Overlay draws above the
        /// wave whatever the sorting layers do, and no EventSystem is needed because nothing is clickable:
        /// the retry is on a timer.
        /// </summary>
        public static GameObject EnsureDeathNotice()
        {
            GameObject existing = FindIncludingInactive("Death Notice");
            if (existing != null)
            {
                return existing;
            }

            var canvasObject = new GameObject("Death Notice");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var background = new GameObject("Blackout");
            background.transform.SetParent(canvasObject.transform, false);
            var backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = Color.black;
            Stretch(background);

            var label = new GameObject("Label");
            label.transform.SetParent(canvasObject.transform, false);
            var text = label.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "YOU WERE HEARD";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.498f, 0.914f, 0.941f, 1f);
            text.fontSize = 44;
            Stretch(label);

            canvasObject.SetActive(false);
            return canvasObject;
        }

        public class Shape
        {
            public bool circle;
            public float radius;
            public Vector2 size = Vector2.one;
            public Vector2 offset;
            public bool trigger;

            public static Shape Circle(float r, Vector2 offset = default(Vector2)) =>
                new Shape { circle = true, radius = r, offset = offset };

            public static Shape Box(float width, float height) =>
                new Shape { size = new Vector2(width, height) };

            public static Shape TriggerBox(float width, float height) =>
                new Shape { size = new Vector2(width, height), trigger = true };

            public void Assign(GameObject go)
            {
                if (circle)
                {
                    var c = go.AddComponent<CircleCollider2D>();
                    c.radius = radius;
                    c.offset = offset;
                    c.isTrigger = trigger;
                }
                else
                {
                    var c = go.AddComponent<BoxCollider2D>();
                    c.size = size;
                    c.isTrigger = trigger;
                }
            }
        }

        public static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            SerializedProperty property = SerializedPropertyOf(target, field);
            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Writes a serialised float on an existing component. Used for the one number whose correct value
        /// depends on something outside the scene, so that re-running the bootstrap rather than opening the
        /// inspector is what keeps an old scene honest.
        /// </summary>
        public static void SetFloat(UnityEngine.Object target, string field, float value)
        {
            SerializedProperty property = SerializedPropertyOf(target, field);
            if (property == null)
            {
                return;
            }

            property.floatValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(UnityEngine.Object target, string field, int value)
        {
            SerializedProperty property = SerializedPropertyOf(target, field);
            if (property == null)
            {
                return;
            }

            property.intValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetVector2Array(UnityEngine.Object target, string field, Vector2[] values)
        {
            SerializedProperty property = SerializedPropertyOf(target, field);
            if (property == null)
            {
                return;
            }

            property.ClearArray();
            for (int i = 0; i < values.Length; i++)
            {
                property.InsertArrayElementAtIndex(i);
                property.GetArrayElementAtIndex(i).vector2Value = values[i];
            }

            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        static SerializedProperty SerializedPropertyOf(UnityEngine.Object target, string field)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError(target.GetType().Name + " has no serialized field \"" + field +
                               "\": the scene would ship with a hole in it. Check the class.");
                return null;
            }

            return property;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static GameObject FindOrCreate(string name)
        {
            GameObject existing = GameObject.Find(name);
            return existing != null ? existing : new GameObject(name);
        }

        /// <summary>
        /// <see cref="GameObject.Find"/> only returns <b>active</b> objects, so an Ensure whose target is
        /// switched off before it is saved can never find what it made last time and appends a second copy
        /// on every run. The death notice is exactly that — it starts hidden, and the lose screen reads its
        /// canvas from an inactive GameObject — so it is looked up here instead.
        /// </summary>
        static GameObject FindIncludingInactive(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindInHierarchy(root.transform, name);
                if (found != null)
                {
                    return found.gameObject;
                }
            }

            return null;
        }

        static Transform FindInHierarchy(Transform candidate, string name)
        {
            if (candidate.name == name)
            {
                return candidate;
            }

            for (int i = 0; i < candidate.childCount; i++)
            {
                Transform found = FindInHierarchy(candidate.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        static bool HasMissingScript(GameObject go)
        {
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null)
                {
                    return true;
                }
            }

            return false;
        }

        static void Stretch(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
