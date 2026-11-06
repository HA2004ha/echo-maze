using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using EchoMaze.Core;
using EchoMaze.Gameplay;
using EchoMaze.UI;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// The canvas pieces every screen in the game is made of, with the sizes the 1080p target is measured in.
    ///
    /// Written as editor code rather than placed in the scenes for the same reason the geometry is: a canvas
    /// built by hand in the editor is a merge conflict waiting to happen in a text file, and a rebuilt one is
    /// a menu item. All of it is idempotent, and every element is named, so re-running a bootstrap re-wires
    /// the screen it made last time instead of putting a second copy of the HUD on top of the first.
    ///
    /// The style follows the GDD's visual identity (line 342): pure black, thin bright cyan lines, Share
    /// Tech Mono, and glow faked on the canvas because a Screen Space - Overlay canvas never passes through
    /// the camera's Bloom. The doctrine of the whole file is re-assertion: every Ensure* path applies the
    /// complete style — font, color, sprite, rect, transition — to existing objects too, because a rebuild
    /// that only repairs the wiring is how the scenes kept last week's look after the generator changed.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Cyan = new Color(0.498f, 0.914f, 0.941f, 1f);
        public static readonly Color CyanMild = new Color(0.498f, 0.914f, 0.941f, 0.72f);
        public static readonly Color CyanFaint = new Color(0.498f, 0.914f, 0.941f, 0.45f);
        public static readonly Color Warm = new Color(1.0f, 0.71f, 0.42f, 1f);
        public static readonly Color Grey = new Color(0.54f, 0.63f, 0.72f, 1f);

        const int HudSortingOrder = 10;
        const int PanelSortingOrder = 100;

        const string SolidPath = "Assets/Art/Sprites/UI_solid.png";
        const string FramePanelPath = "Assets/Art/Sprites/UI/UI_frame_panel.png";
        const string FrameButtonPath = "Assets/Art/Sprites/UI/UI_frame_button.png";
        const string VignettePath = "Assets/Art/Sprites/UI/UI_vignette.png";
        const string RingPath = "Assets/Art/Sprites/UI/UI_ring_thin.png";
        const string FontPath = "Assets/Art/Fonts/ShareTechMono-Regular.ttf";
        const int SolidTexels = 4;

        // One const so the crispness experiment (HintedSmooth versus Smooth) is one line, not a hunt.
        const FontRenderingMode FontMode = FontRenderingMode.HintedSmooth;

        // The frame sprite is white so the button's ColorBlock can tint it, and the idle tint starts
        // below 1 so the hover tint can lift the hairline toward full brightness — ColorTint against a
        // white base can brighten, against a cyan base it could only muddy.
        static readonly Color ButtonIdle = new Color(0.55f, 0.85f, 0.92f, 1f);
        static readonly Color ButtonHover = new Color(0.85f, 1.0f, 1.0f, 1f);
        static readonly Color ButtonPressed = new Color(0.38f, 0.72f, 0.80f, 1f);
        static readonly Color ButtonDisabled = new Color(0.22f, 0.28f, 0.30f, 1f);

        public static Canvas EnsureCanvas(string name, int sortingOrder)
        {
            Canvas canvas = FindCanvas(name);
            if (canvas == null)
            {
                var go = new GameObject(name);
                canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            canvas.sortingOrder = sortingOrder;

            var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // A canvas without a graphic raycaster cannot be clicked, and the buttons are added as children
            // after this returns: adding the raycaster here is what makes the scene interactive on first Play.
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            return canvas;
        }

        static Canvas FindCanvas(string name)
        {
            foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (candidate.gameObject.name == name)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// The input module the project's own: the whole game runs on the Input System, and the legacy
        /// <c>StandaloneInputModule</c> that Unity puts on a new EventSystem reads a backend the player build
        /// does not have. Re-running this replaces a legacy module rather than leaving two.
        /// </summary>
        public static void EnsureEventSystem()
        {
            var system = Object.FindAnyObjectByType<EventSystem>();
            if (system == null)
            {
                system = new GameObject("Event System").AddComponent<EventSystem>();
            }

            if (system.GetComponent<InputSystemUIInputModule>() == null)
            {
                foreach (BaseInputModule legacy in system.GetComponents<BaseInputModule>())
                {
                    Object.DestroyImmediate(legacy);
                }

                system.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        /// <summary>
        /// The GDD-named monospace. The TTF is a committed asset, not a download this method performs:
        /// editor code that reaches the network on import would make a headless rebuild depend on the
        /// machine it runs on, and the visible fallback keeps the build working — in the wrong font —
        /// when the asset is missing rather than stopping for nobody's benefit.
        /// </summary>
        public static Font EnsureFont()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null && !System.IO.File.Exists(FontPath))
            {
                // The .meta beside the TTF carries the GUID every scene's Text references; a re-download
                // on a fresh clone is fine, deleting or regenerating that .meta is not — legacy Text
                // answers a missing font reference by falling back with no error at all.
                Debug.LogError($"UiKit: {FontPath} is missing. Restore it (it is a committed asset):\n" +
                               "  curl -fL -o Assets/Art/Fonts/ShareTechMono-Regular.ttf " +
                               "https://raw.githubusercontent.com/google/fonts/main/ofl/sharetechmono/ShareTechMono-Regular.ttf\n" +
                               "Falling back to the built-in font, so the interface will look generic.");
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            var importer = AssetImporter.GetAtPath(FontPath) as TrueTypeFontImporter;
            if (importer != null && (importer.fontRenderingMode != FontMode ||
                                     importer.ascentCalculationMode != AscentCalculationMode.FaceAscender ||
                                     !importer.includeFontData))
            {
                // HintedSmooth pins the stems of the monospace to pixel rows at the sizes the interface
                // actually uses; FaceAscender keeps the legacy Text line box on the font's own ascent,
                // which is what makes MiddleCenter single-line labels sit level.
                importer.includeFontData = true;
                importer.fontRenderingMode = FontMode;
                importer.ascentCalculationMode = AscentCalculationMode.FaceAscender;
                importer.SaveAndReimport();
            }

            font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>
        /// Decorative letter-spacing for static display headings, by inserting an ASCII space between
        /// every pair of letters: legacy Text has no tracking API, and a hair- or thin-space would put
        /// the result through the dynamic font's glyph fallback on a font with no such characters —
        /// machine-dependent spacing is exactly what the player-build screenshots cannot tolerate.
        /// Static display copy only; every label a runtime script fills is information, not decoration,
        /// and must carry its real characters.
        /// </summary>
        public static string Track(string value)
        {
            var text = new System.Text.StringBuilder(value.Length * 2);
            foreach (char c in value)
            {
                text.Append(c);
                if (!char.IsWhiteSpace(c))
                {
                    text.Append(' ');
                }
            }

            text.Length--;
            return text.ToString();
        }

        public static Text AddText(Transform parent, string name, string value, int fontSize,
                                   Color color, Vector2 anchored, Vector2 size, bool center = true)
        {
            Text label = AddTextRaw(parent, name);
            ApplyText(label, value, fontSize, color, anchored, size,
                      center ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, false);
            return label;
        }

        /// <summary>
        /// The complete style of a label in one place, because every path — created now or rebuilt from
        /// the saved scene — has to arrive at the same screen.
        /// </summary>
        public static void ApplyText(Text label, string value, int fontSize, Color color,
                                     Vector2 anchored, Vector2 size, TextAnchor align, bool glow)
        {
            label.font = EnsureFont();
            label.text = value;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;

            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchored;
            rect.sizeDelta = size;

            // The Bloom substitute: one downward shadow, never a four-direction outline — at display
            // sizes an outline stacks into a fat halo around a monospace instead of reading as glow.
            Shadow echo = label.GetComponent<Shadow>();
            if (glow)
            {
                if (echo == null)
                {
                    echo = label.gameObject.AddComponent<Shadow>();
                }

                echo.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f);
                echo.effectDistance = new Vector2(0f, -2f);
            }
            else if (echo != null)
            {
                Object.DestroyImmediate(echo);
            }
        }

        /// <summary>
        /// The find-or-create-and-style path every label goes through now. A named child that exists but
        /// has no Text on it is not trusted — it is replaced, the way <see cref="EnsureEventSystem"/>
        /// replaces a legacy input module instead of hoping it is the right one.
        /// </summary>
        public static Text EnsureLabel(Transform parent, string name, string value, int fontSize,
                                       Color color, Vector2 anchored, Vector2 size,
                                       TextAnchor align = TextAnchor.MiddleCenter, bool glow = false)
        {
            Transform existing = parent.Find(name);
            Text label = existing != null ? existing.GetComponent<Text>() : null;
            if (label == null)
            {
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                label = AddTextRaw(parent, name);
            }

            ApplyText(label, value, fontSize, color, anchored, size, align, glow);
            return label;
        }

        static Text AddTextRaw(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<Text>();
        }

        /// <summary>A full-screen wash: the panels' background, and the black the death notice is drawn on.</summary>
        public static Image AddBackground(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = EnsureSolidSprite();

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        /// <summary>
        /// Find-or-create a named Image and re-assert its whole visual state, including whether it
        /// stretches with the canvas or sits at an anchored position with a size.
        /// </summary>
        public static Image EnsureImage(Transform parent, string name, Sprite sprite, Image.Type type,
                                        Vector2 anchored, Vector2 size, Color color,
                                        bool stretch = false, int sibling = -1)
        {
            Transform existing = parent.Find(name);
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                image = go.AddComponent<Image>();
            }

            image.sprite = sprite;
            image.type = type;
            image.color = color;
            image.raycastTarget = false;

            var rect = image.rectTransform;
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchored;
                rect.sizeDelta = size;
            }

            if (sibling >= 0)
            {
                rect.SetSiblingIndex(sibling);
            }

            return image;
        }

        public static Image AddBar(Transform parent, string name, Color color, Vector2 anchored, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Image bar = go.AddComponent<Image>();
            bar.color = color;
            bar.sprite = EnsureSolidSprite();
            // A filled bar rather than a scaled one: the value is a fraction of a cost, and scaling a
            // rectangle would move its edge instead of emptying it.
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Vertical;
            bar.fillOrigin = (int)Image.OriginVertical.Bottom;
            bar.fillAmount = 0f;
            bar.raycastTarget = false;
            bar.rectTransform.anchoredPosition = anchored;
            bar.rectTransform.sizeDelta = size;
            return bar;
        }

        public static Image EnsureBar(Transform parent, string name, Color color, Vector2 anchored,
                                      Vector2 size)
        {
            Transform existing = parent.Find(name);
            Image bar = existing != null ? existing.GetComponent<Image>() : null;
            if (bar == null)
            {
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                return AddBar(parent, name, color, anchored, size);
            }

            bar.sprite = EnsureSolidSprite();
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Vertical;
            bar.fillOrigin = (int)Image.OriginVertical.Bottom;
            bar.fillAmount = 0f;
            bar.color = color;
            bar.raycastTarget = false;
            bar.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            bar.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bar.rectTransform.anchoredPosition = anchored;
            bar.rectTransform.sizeDelta = size;
            return bar;
        }

        public static Button AddButton(Transform parent, string name, string label, Vector2 anchored,
                                       Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;

            go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            ApplyButton(button, label, anchored, size);
            return button;
        }

        static void ApplyButton(Button button, string label, Vector2 anchored, Vector2 size)
        {
            Image background = button.GetComponent<Image>();
            // The line-drawn frame: a dark interior wash, a hairline and corner brackets, white so the
            // ColorBlock can tint the whole thing, and 9-sliced so the brackets keep their shape at any
            // button size instead of stretching the way a scaled solid rectangle stretches. The interface
            // stays a line drawing on purpose: a filled panel behind a label is the brightest thing on
            // the screen, and there is no Bloom on an overlay canvas to soften anything anyway.
            background.sprite = EnsureFrameButtonSprite();
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            background.raycastTarget = true;

            var rect = background.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchored;
            rect.sizeDelta = size;

            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonIdle;
            colors.highlightedColor = ButtonHover;
            colors.pressedColor = ButtonPressed;
            colors.selectedColor = ButtonHover;
            colors.disabledColor = ButtonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Transform labelObject = button.transform.Find("Label");
            Text text = labelObject != null ? labelObject.GetComponent<Text>() : null;
            if (text == null)
            {
                if (labelObject != null)
                {
                    Object.DestroyImmediate(labelObject.gameObject);
                }

                text = AddTextRaw(button.transform, "Label");
            }

            ApplyText(text, label, 28, Cyan, Vector2.zero, size, TextAnchor.MiddleCenter, false);
        }

        public static Button EnsureButton(Transform parent, string name, string label, Vector2 anchored,
                                          Vector2 size)
        {
            Transform existing = parent.Find(name);
            Button button = existing != null ? existing.GetComponent<Button>() : null;
            if (button == null)
            {
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                return AddButton(parent, name, label, anchored, size);
            }

            // Re-assert the copy as well as the whole style: a bootstrap that only repairs the wiring
            // leaves a scene printing last week's controls — which is exactly what happened when the
            // menu still said "E touch" after the action became a door/exit interact — and a bootstrap
            // that repairs the copy but not the style leaves last week's look just as quietly.
            ApplyButton(button, label, anchored, size);
            return button;
        }

        /// <summary>
        /// The one texture every filled rectangle in the interface is stretched from. It is a written asset
        /// rather than a <c>Sprite.Create</c> runtime texture because a scene's <c>Image</c> can only point at
        /// something with a GUID: a runtime sprite saves as a hole, so the bar would come back next boot with
        /// no reference and no way to tell that the bootstrap had quietly changed the screen.
        /// </summary>
        public static Sprite EnsureSolidSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SolidPath);
            if (existing != null)
            {
                return existing;
            }

            SceneKit.EnsureFolder("Assets/Art/Sprites");
            var texture = new Texture2D(SolidTexels, SolidTexels, TextureFormat.RGBA32, false);
            var white = new Color32[SolidTexels * SolidTexels];
            for (int i = 0; i < white.Length; i++)
            {
                white[i] = new Color32(255, 255, 255, 255);
            }

            texture.SetPixels32(white);
            texture.Apply();
            System.IO.File.WriteAllBytes(SolidPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SolidPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(SolidPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(SolidPath);
        }

        static Sprite EnsureFramePanelSprite() => EnsureFrameSprite(FramePanelPath, 64, 20, 2, 0.28f, 14, 2);

        static Sprite EnsureFrameButtonSprite() => EnsureFrameSprite(FrameButtonPath, 32, 12, 2, 0.22f, 8, 2);

        /// <summary>
        /// A 9-slice line frame: dark interior wash, one-texel hairline at the inset, and bright L-shaped
        /// corner brackets. The brackets stop inside the border — <c>inset + length &lt; border</c> is the
        /// whole arithmetic of why they never stretch — and everything is white so the Image color and the
        /// button ColorBlock tint it instead of fighting it.
        /// </summary>
        static Sprite EnsureFrameSprite(string path, int size, int border, int inset, float ringAlpha,
                                        int bracketLength, int bracketThickness)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            SceneKit.EnsureFolder(System.IO.Path.GetDirectoryName(path));
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var wash = new Color32(0, 0, 0, (byte)(0.35f * 255f));
            var ring = new Color32(255, 255, 255, (byte)(ringAlpha * 255f));
            var bracket = new Color32(255, 255, 255, 255);

            int last = size - 1;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color32 pixel = wash;
                    bool onRingLine = (x == inset && y >= inset && y <= last - inset) ||
                                      (x == last - inset && y >= inset && y <= last - inset) ||
                                      (y == inset && x >= inset && x <= last - inset) ||
                                      (y == last - inset && x >= inset && x <= last - inset);
                    if (onRingLine)
                    {
                        pixel = ring;
                    }

                    // Four L corners: a thick run along the top and bottom edges and the same run up the
                    // left and right sides, each measured from the nearest corner. Either arm lights the
                    // pixel; the join is covered by both.
                    bool alongTop = y > last - inset - bracketThickness && y <= last - inset;
                    bool alongBottom = y >= inset && y < inset + bracketThickness;
                    bool alongLeft = x >= inset && x < inset + bracketThickness;
                    bool alongRight = x > last - inset - bracketThickness && x <= last - inset;
                    bool fromLeftCorner = x >= inset && x < inset + bracketLength;
                    bool fromRightCorner = x > last - inset - bracketLength && x <= last - inset;
                    bool fromBottomCorner = y >= inset && y < inset + bracketLength;
                    bool fromTopCorner = y > last - inset - bracketLength && y <= last - inset;

                    bool onBracket = (alongTop || alongBottom) && (fromLeftCorner || fromRightCorner) ||
                                     (alongLeft || alongRight) && (fromBottomCorner || fromTopCorner);
                    if (onBracket)
                    {
                        pixel = bracket;
                    }

                    pixels[y * size + x] = pixel;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return ImportUiSprite(path, border);
        }

        public static Sprite EnsureVignetteSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(VignettePath);
            if (existing != null)
            {
                return existing;
            }

            SceneKit.EnsureFolder(System.IO.Path.GetDirectoryName(VignettePath));
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float centre = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Sqrt(2f);
                    // The room has to keep reading through the middle of the panel: the wash only
                    // gathers toward the edges, squared so the falloff starts softly.
                    float t = Mathf.Clamp01((radius - 0.30f) / 0.70f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(t * t * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(VignettePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return ImportUiSprite(VignettePath, 0);
        }

        static Sprite EnsureRingSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
            if (existing != null)
            {
                return existing;
            }

            SceneKit.EnsureFolder(System.IO.Path.GetDirectoryName(RingPath));
            const int size = 512;
            const float radius = 220f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float centre = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    // The sonar ring the whole game is drawn from: one wavefront of a line, two texels
                    // of falloff on each side so it stays a hairline when the Image scales it down.
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - radius) / 2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(RingPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return ImportUiSprite(RingPath, 0);
        }

        static Sprite ImportUiSprite(string path, int border)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // The border order is Left, Bottom, Right, Top — not the clockwise guess the naming invites,
            // and getting it wrong only shows up as a frame that stretches its own corners on two sides.
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.mipmapEnabled = false;
            // The default compression smears one-texel lines and bands a soft gradient, and both are
            // the entire content of these sprites.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// The HUD canvas for a level: the cooldown bar, the chapter title, the prompt line and the notice.
        /// Re-running keeps one canvas and re-wires the same four elements, so a bootstrap cannot leave a
        /// second bar on screen.
        /// </summary>
        public static HudView EnsureHud(EchoDirector director, Player player, LevelSession session)
        {
            Canvas canvas = EnsureCanvas("HUD", HudSortingOrder);
            Transform root = canvas.transform;

            // The GDD asks the HUD to be dimmer than the waves; CyanMild is how, rather than smaller type.
            Image meter = EnsureBar(root, "Cooldown", CyanMild, new Vector2(-860f, 420f),
                                    new Vector2(16f, 220f));
            Text title = EnsureLabel(root, "Title", "", 40, CyanMild, new Vector2(0f, 430f),
                                     new Vector2(1100f, 90f));
            Text prompt = EnsureLabel(root, "Prompt", "", 26, CyanMild, new Vector2(0f, -470f),
                                      new Vector2(1100f, 70f));
            Text notice = EnsureLabel(root, "Notice", "", 26, CyanMild, new Vector2(0f, 340f),
                                      new Vector2(1100f, 60f));

            var view = canvas.GetComponent<HudView>() ?? canvas.gameObject.AddComponent<HudView>();
            SceneKit.SetReference(view, "director", director);
            SceneKit.SetReference(view, "player", player);
            SceneKit.SetReference(view, "session", session);
            SceneKit.SetReference(view, "cooldownMeter", meter);
            SceneKit.SetReference(view, "titleLabel", title);
            SceneKit.SetReference(view, "promptLabel", prompt);
            SceneKit.SetReference(view, "noticeLabel", notice);
            return view;
        }

        /// <summary>
        /// A panel canvas with a vignette wash and a titled column of buttons. Shared by pause, victory and
        /// calibration because three screens with the same four words set five pixels apart is exactly the
        /// kind of drift a generated UI is supposed to prevent.
        ///
        /// The component lives on the canvas and everything visible lives inside the <c>content</c> child,
        /// because a panel that switches itself off also unsubscribes itself from the event that would
        /// switch it back on.
        /// </summary>
        public static Canvas EnsurePanel(string name, out Transform content, out Text heading,
                                         float washAlpha = 0.82f)
        {
            Canvas canvas = EnsureCanvas(name, PanelSortingOrder);

            GameObject contentObject = FindOrCreateChild(canvas.transform, "Content");
            content = contentObject.transform;

            // Find-or-create plus re-assert: the guard that used to skip an existing wash is how a
            // flat-black scene survived every style change the generator went through after it.
            EnsureImage(content, "wash", EnsureVignetteSprite(), Image.Type.Simple,
                        Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, washAlpha), true, 0);
            heading = EnsureLabel(content, "Heading", "", 44, Cyan,
                                  new Vector2(0f, 250f), new Vector2(900f, 90f),
                                  TextAnchor.MiddleCenter, true);

            return canvas;
        }

        static GameObject FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        /// <summary>The card the pause, victory and calibration screens are printed on, kept behind everything else.</summary>
        static Image EnsureCard(Transform content, Vector2 anchored, Vector2 size)
        {
            // White color, not cyan: the card is furniture, and its hairline reads as the GDD's line
            // drawing at the sprite's own alpha without competing with the buttons sitting on it.
            return EnsureImage(content, "Card", EnsureFramePanelSprite(), Image.Type.Sliced,
                               anchored, size, Color.white, false, 1);
        }

        static Image EnsureDivider(Transform content, string name, Vector2 anchored, Vector2 width)
        {
            return EnsureImage(content, name, EnsureSolidSprite(), Image.Type.Simple,
                               anchored, width, CyanFaint);
        }

        /// <summary>
        /// The pause screen. Every button on it does one thing that a keyboard cannot do comfortably: quit,
        /// and go back to a menu the player has already left.
        /// </summary>
        public static PausePanel EnsurePausePanel(LevelSession session)
        {
            Canvas canvas = EnsurePanel("Pause Panel", out Transform content, out Text heading);
            heading.text = Track("HELD YOUR BREATH");

            EnsureCard(content, new Vector2(0f, -20f), new Vector2(760f, 700f));
            EnsureDivider(content, "Divider", new Vector2(0f, 185f), new Vector2(620f, 2f));

            Button resume = EnsureButton(content, "Resume", "resume  (Esc)", new Vector2(0f, 80f),
                                         new Vector2(520f, 74f));
            Button restart = EnsureButton(content, "Restart", "restart this room", new Vector2(0f, -20f),
                                          new Vector2(520f, 74f));
            Button menu = EnsureButton(content, "Menu", "back to the menu", new Vector2(0f, -120f),
                                       new Vector2(520f, 74f));
            Button quit = EnsureButton(content, "Quit", "quit", new Vector2(0f, -220f), new Vector2(520f, 74f));
            Text room = EnsureLabel(content, "Room", "", 22, CyanFaint, new Vector2(0f, -310f),
                                    new Vector2(700f, 40f));

            var panel = canvas.GetComponent<PausePanel>() ?? canvas.gameObject.AddComponent<PausePanel>();
            SceneKit.SetReference(panel, "session", session);
            SceneKit.SetReference(panel, "content", content.gameObject);
            SceneKit.SetReference(panel, "resumeButton", resume);
            SceneKit.SetReference(panel, "restartButton", restart);
            SceneKit.SetReference(panel, "menuButton", menu);
            SceneKit.SetReference(panel, "quitButton", quit);
            SceneKit.SetReference(panel, "chapterLabel", room);
            return panel;
        }

        public static VictoryPanel EnsureVictoryPanel()
        {
            Canvas canvas = EnsurePanel("Victory Panel", out Transform content, out Text heading);
            heading.text = Track("YOU WERE NOT HEARD");

            EnsureCard(content, new Vector2(0f, 0f), new Vector2(980f, 620f));
            EnsureDivider(content, "Divider", new Vector2(0f, 150f), new Vector2(820f, 2f));

            Button menu = EnsureButton(content, "Menu", "back to the menu", new Vector2(0f, -190f),
                                       new Vector2(520f, 74f));
            Text summary = EnsureLabel(content, "Summary", "", 28, Cyan, new Vector2(0f, 10f),
                                       new Vector2(820f, 260f));
            summary.lineSpacing = 1.35f;
            EnsureLabel(content, "Footer", "the maze never learned your name", 20, CyanFaint,
                        new Vector2(0f, -270f), new Vector2(900f, 40f));

            var panel = canvas.GetComponent<VictoryPanel>() ?? canvas.gameObject.AddComponent<VictoryPanel>();
            SceneKit.SetReference(panel, "content", content.gameObject);
            SceneKit.SetReference(panel, "summaryLabel", summary);
            SceneKit.SetReference(panel, "menuButton", menu);

            // Hidden until the run ends: the component would otherwise cover the room it is standing in.
            content.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>
        /// The page the game opens on. The title is the only text in the project that is decoration rather
        /// than information, so it is the one label sized for looking at instead of for reading at a glance
        /// — tracked, and lit by the echo rings it is meant to be the source of.
        /// </summary>
        public static MainMenu EnsureMainMenu(EchoDirector director, VoiceCalibration calibration)
        {
            Canvas canvas = EnsurePanel("Main Menu", out Transform content, out Text heading, 0.88f);

            heading = EnsureLabel(content, "Heading", Track("ECHO MAZE"), 96, Cyan,
                                  new Vector2(0f, 320f), new Vector2(1200f, 160f),
                                  TextAnchor.MiddleCenter, true);

            // The rings go behind the title in sibling order, so they read as the title's own echo rather
            // than as wallpaper in front of it.
            Sprite ring = EnsureRingSprite();
            EnsureImage(content, "Ring1", ring, Image.Type.Simple, new Vector2(0f, 320f), new Vector2(300f, 300f),
                        new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f), false, 2);
            EnsureImage(content, "Ring2", ring, Image.Type.Simple, new Vector2(0f, 320f), new Vector2(440f, 440f),
                        new Color(Cyan.r, Cyan.g, Cyan.b, 0.20f), false, 3);
            EnsureImage(content, "Ring3", ring, Image.Type.Simple, new Vector2(0f, 320f), new Vector2(580f, 580f),
                        new Color(Cyan.r, Cyan.g, Cyan.b, 0.10f), false, 4);

            EnsureLabel(content, "Subtitle", "a maze you see by sound", 24, CyanFaint,
                        new Vector2(0f, 205f), new Vector2(1000f, 50f));

            Button start = EnsureButton(content, "Start", "begin", new Vector2(0f, 40f), new Vector2(520f, 84f));
            Button calibrate = EnsureButton(content, "Calibrate", "tune your voice", new Vector2(0f, -64f),
                                            new Vector2(520f, 68f));
            Button quit = EnsureButton(content, "Quit", "quit", new Vector2(0f, -150f), new Vector2(520f, 68f));
            Text status = EnsureLabel(content, "Status", "", 24, CyanMild, new Vector2(0f, -280f),
                                      new Vector2(1400f, 44f));
            EnsureLabel(content, "Legend",
                        "WASD move   mouse aim   Space shout (hold to charge)   F pulse\n" +
                        "Shift silent   E door / exit   Esc pause",
                        20, CyanFaint, new Vector2(0f, -360f), new Vector2(1600f, 80f));

            var menu = canvas.GetComponent<MainMenu>() ?? canvas.gameObject.AddComponent<MainMenu>();
            SceneKit.SetReference(menu, "director", director);
            SceneKit.SetReference(menu, "startButton", start);
            SceneKit.SetReference(menu, "calibrateButton", calibrate);
            SceneKit.SetReference(menu, "quitButton", quit);
            SceneKit.SetReference(menu, "statusLabel", status);
            // The calibration page starts folded away and the menu opens it; both are children of the same
            // canvas so the panel's buttons are still under the raycaster that can reach them. Found before
            // created because a folded-away duplicate is invisible on screen and only shows up as N panels in
            // the saved scene — which is what five of them in Boot.unity were.
            GameObject calibrationPanel = FindOrCreateChild(canvas.transform, "Calibration");
            calibrationPanel.SetActive(false);

            SceneKit.SetReference(menu, "calibrationPanel", calibrationPanel);

            var calibrationView = EnsureCalibration(calibration, calibrationPanel);
            SceneKit.SetReference(calibrationView, "calibration", calibration);
            return menu;
        }

        static CalibrationPanel EnsureCalibration(VoiceCalibration calibration, GameObject root)
        {
            Transform content = root.transform;

            // The menu is a bright page, not a dark room: unlike pause and victory, this panel has to blank
            // out live UI sitting under it in the same canvas, so it carries its own near-opaque dim rather
            // than relying on the vignette wash — whose whole point is an empty centre.
            EnsureImage(content, "dim", EnsureSolidSprite(), Image.Type.Simple, Vector2.zero, Vector2.zero,
                        new Color(0f, 0f, 0f, 0.96f), true, 0);

            EnsureCard(content, new Vector2(0f, 0f), new Vector2(1100f, 760f));
            EnsureDivider(content, "Divider", new Vector2(0f, 275f), new Vector2(900f, 2f));

            Text instruction = EnsureLabel(content, "Instruction", "", 26, Cyan, new Vector2(0f, 320f),
                                           new Vector2(1200f, 60f));

            // The track behind the meter is the cost, shown even when the meter itself has emptied:
            // an absolute bar with nothing to compare it to reads as a glitch, not a measurement.
            EnsureImage(content, "Track", EnsureSolidSprite(), Image.Type.Simple, new Vector2(0f, 180f),
                        new Vector2(904f, 30f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.12f), false, 2);
            Image meter = EnsureBar(content, "Meter", CyanMild, new Vector2(0f, 180f), new Vector2(900f, 26f));
            Text result = EnsureLabel(content, "Result", "", 24, CyanMild, new Vector2(0f, 20f),
                                      new Vector2(1200f, 60f));

            Button begin = EnsureButton(content, "Begin", "listen", new Vector2(0f, -120f), new Vector2(360f, 64f));
            Button close = EnsureButton(content, "Close", "back", new Vector2(0f, -210f), new Vector2(360f, 64f));

            // The input choice is a step button rather than a Dropdown: a generated dropdown needs a template,
            // a mask, a scrollbar and an item prefab, all of it built by hand in editor code and none of it
            // visible to anybody who runs the bootstrap headless. One button that names what the game is
            // actually hearing and applies on the press does the same job with parts this file already knows
            // how to make. See Docs/DESIGN_DECISIONS.md, 2026-10-29.
            Button device = EnsureButton(content, "Device", "input: auto", new Vector2(0f, -300f),
                                         new Vector2(620f, 56f));
            Text deviceInfo = EnsureLabel(content, "DeviceInfo", "", 20, CyanFaint, new Vector2(0f, -360f),
                                          new Vector2(1200f, 44f));

            var view = root.GetComponent<CalibrationPanel>() ?? root.AddComponent<CalibrationPanel>();
            SceneKit.SetReference(view, "startButton", begin);
            SceneKit.SetReference(view, "closeButton", close);
            SceneKit.SetReference(view, "meter", meter);
            SceneKit.SetReference(view, "instructionLabel", instruction);
            SceneKit.SetReference(view, "resultLabel", result);
            SceneKit.SetReference(view, "deviceButton", device);
            SceneKit.SetReference(view, "deviceLabel", deviceInfo);
            return view;
        }
    }
}
