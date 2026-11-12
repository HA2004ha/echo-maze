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
        const string DimPath = "Assets/Art/Sprites/UI/UI_dim.png";
        const string LegendBandPath = "Assets/Art/Sprites/UI/UI_legend_band.png";
        const string RingPath = "Assets/Art/Sprites/UI/UI_ring_thin.png";
        const string NodePath = "Assets/Art/Sprites/UI/UI_node_marker.png";
        const string FontPath = "Assets/Resources/Fonts/ShareTechMono-Regular.ttf";
        const int SolidTexels = 4;

        // One const so the crispness experiment (HintedSmooth versus Smooth) is one line, not a hunt.
        const FontRenderingMode FontMode = FontRenderingMode.HintedSmooth;

        // The frame sprite is white so the button's ColorBlock can tint it, and the idle tint starts
        // below 1 so the hover tint can lift the hairline toward full brightness — ColorTint against a
        // white base can brighten, against a cyan base it could only muddy.
        // Slightly cooler than it was before the motion pass: with a real hover glow underneath it, the
        // idle colour no longer has to suggest availability on its own and can go back to being furniture.
        // (The old ButtonHover constant died here — brightening is UIButtonFeedback's job now, and two
        // systems writing one Image.color is a flicker, not a highlight.)
        static readonly Color ButtonIdle = new Color(0.50f, 0.83f, 0.92f, 1f);
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
        /// A dark drop shadow on a label that has to stay readable over something we do not control.
        /// This is the opposite job from <see cref="ApplyText"/>'s glow, which is a cyan Bloom
        /// substitute: here the effect colour is near black, so the shadow is not decoration but the
        /// glyphs' own ground — it travels with the text, which a painted band behind it cannot do.
        /// Call it after EnsureLabel, never before: a rebuild with <c>glow: false</c> deletes any
        /// Shadow it finds, and that is the behaviour that keeps the two effects from ever coexisting.
        /// </summary>
        public static Shadow EnsureTextShadow(Text label, Color effectColor, Vector2 effectDistance)
        {
            Shadow shadow = label.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = label.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = effectColor;
            shadow.effectDistance = effectDistance;
            return shadow;
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
            // Hover and selection are no longer the ColorBlock's job: the <see cref="UIButtonFeedback"/>
            // attached below writes the frame colour every frame it is ramping, and two writers on one
            // colour is a flicker. The block keeps what feedback cannot do — the pressed dip and the
            // disabled fade.
            colors.highlightedColor = ButtonIdle;
            colors.pressedColor = ButtonPressed;
            colors.selectedColor = ButtonIdle;
            colors.disabledColor = ButtonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            // Colour alone only speaks while the pointer is down, which is after the decision and not
            // during it. The hover-and-focus grow is attached to every generated button from here so
            // there is no button in the game that can forget it has hands.
            var feedback = SceneKit.EnsureComponent<UIButtonFeedback>(button.gameObject);
            SceneKit.SetReference(feedback, "frame", background);

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

        /// <summary>
        /// The vignette's inverse: darkest at the centre, gone at the edges. The wash was written to
        /// keep the room readable through the middle of a panel, and that empty centre is exactly where
        /// the diorama plays — behind the menu's text column, the brightest thing on screen outshines
        /// the glyphs. Every panel screen therefore dims its own middle with this, so legibility is a
        /// painted property of the panel rather than a mercy of whatever happens to be behind it.
        /// </summary>
        public static Sprite EnsureDimSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(DimPath);
            if (existing != null)
            {
                return existing;
            }

            SceneKit.EnsureFolder(System.IO.Path.GetDirectoryName(DimPath));
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
                    // Fully opaque through the middle third, squared falloff to nothing: the same curve
                    // as the wash, reversed, so the two gradients agree about where the soft part lives.
                    float t = Mathf.Clamp01((0.70f - radius) / 0.70f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(t * t * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(DimPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return ImportUiSprite(DimPath, 0);
        }

        /// <summary>
        /// A vertical fade, opaque at its own bottom edge: the band the menu's controls legend sits on.
        /// The dim column is an ellipse and its falloff is spent before it reaches y −360, while the
        /// diorama's trim glow sweeps straight through that line as the model turns — a faint glyph
        /// over a Bloom-lit bar is invisible, not low-contrast. The band fades up rather than covering
        /// the bottom of the screen, so the glowing frame stays part of the picture and only the row
        /// of words owns a piece of dark.
        /// </summary>
        public static Sprite EnsureLegendBandSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(LegendBandPath);
            if (existing != null)
            {
                return existing;
            }

            SceneKit.EnsureFolder(System.IO.Path.GetDirectoryName(LegendBandPath));
            const int width = 64;
            const int height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                // Smoothstep from the bottom edge: full dark under the words, gone by the top of the
                // band, with no hard line either way.
                float t = 1f - v;
                float a = t * t * (3f - 2f * t);
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(LegendBandPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return ImportUiSprite(LegendBandPath, 0);
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
        public static HudView EnsureHud(EchoDirector director, Player player, LevelSession session,
                                        LanternKit lanternKit = null)
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

            // The lamp count sits under the cooldown sweep, at the edge the player already looks to when
            // they want to know what the next press costs: one column, two debts, cooldown and light.
            Text lamps = EnsureLabel(root, "Lamps", "", 22, CyanMild, new Vector2(-860f, 296f),
                                     new Vector2(280f, 40f));

            var view = canvas.GetComponent<HudView>() ?? canvas.gameObject.AddComponent<HudView>();
            SceneKit.SetReference(view, "director", director);
            SceneKit.SetReference(view, "player", player);
            SceneKit.SetReference(view, "session", session);
            SceneKit.SetReference(view, "cooldownMeter", meter);
            SceneKit.SetReference(view, "titleLabel", title);
            SceneKit.SetReference(view, "promptLabel", prompt);
            SceneKit.SetReference(view, "noticeLabel", notice);
            SceneKit.SetReference(view, "lampsLabel", lamps);
            SceneKit.SetReference(view, "lanterns", lanternKit);
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

            // The fade surface: an alpha the panels can ramp without deactivating anything, so the panel
            // components that subscribe from the canvas keep their events through a hide. Re-asserted at
            // full alpha because a scene must never save mid-fade and ship an invisible menu. The panel's
            // Content child is saved deactivated on some screens, and AddComponent on an inactive object
            // returns a reference the editor will not evaluate — hence SceneKit.EnsureComponent.
            CanvasGroup group = SceneKit.EnsureComponent<CanvasGroup>(contentObject);
            // Active, always, at build time: the old doctrine saved this child switched off, and a fade
            // ramping the CanvasGroup of an inactive object fades nothing a player will ever see. Under
            // the new doctrine hidden is alpha zero on an active object — which is also why the panel
            // components on the canvas keep their event subscriptions through a hide.
            contentObject.SetActive(true);
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            // Find-or-create plus re-assert: the guard that used to skip an existing wash is how a
            // flat-black scene survived every style change the generator went through after it.
            EnsureImage(content, "wash", EnsureVignetteSprite(), Image.Type.Simple,
                        Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, washAlpha), true, 0);

            // The text column sits on its own dim, not on the wash's empty centre: over the 3D
            // diorama the wave crest is brighter than Cyan, and a menu whose buttons blink in and
            // out with the background is a menu nobody reads. One column size for every panel
            // screen so the dim belongs to the layout, not to whichever page happens to need it.
            EnsureImage(content, "textdim", EnsureDimSprite(), Image.Type.Simple,
                        Vector2.zero, new Vector2(1500f, 1020f),
                        new Color(0f, 0f, 0f, 0.55f), false, 1);

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

            var panel = SceneKit.EnsureComponent<PausePanel>(canvas.gameObject);
            var fade = SceneKit.EnsureComponent<UIPanelFade>(canvas.gameObject);
            SceneKit.SetReference(fade, "group", content.GetComponent<CanvasGroup>());
            SceneKit.SetReference(panel, "session", session);
            SceneKit.SetReference(panel, "content", content.gameObject);
            SceneKit.SetReference(panel, "fade", fade);
            SceneKit.SetReference(panel, "resumeButton", resume);
            SceneKit.SetReference(panel, "restartButton", restart);
            SceneKit.SetReference(panel, "menuButton", menu);
            SceneKit.SetReference(panel, "quitButton", quit);
            SceneKit.SetReference(panel, "chapterLabel", room);
            return panel;
        }

        // The victory page is two columns. The emote is square, so the run's numbers get a column beside it
        // rather than being printed over the ghost: 860 of art centred at -455 and an 880 card centred at
        // +475, which lands both columns inside the 1920 x 1080 frame with a margin at each edge.
        const float VictoryArtCentre = -455f;
        const float VictoryArtSize = 860f;
        const float VictoryColumn = 475f;
        const float VictoryCardWidth = 880f;
        const float VictoryCardHeight = 720f;

        public static VictoryPanel EnsureVictoryPanel()
        {
            Canvas canvas = EnsurePanel("Victory Panel", out Transform content, out Text heading);
            heading.text = Track("YOU WERE NOT HEARD");
            // The heading is one line of tracked capitals: 840 of column leaves it the same 56 px of air on
            // either side it had on the centred card.
            heading.rectTransform.anchoredPosition = new Vector2(VictoryColumn, 265f);
            heading.rectTransform.sizeDelta = new Vector2(VictoryCardWidth - 40f, 90f);

            EnsureCard(content, new Vector2(VictoryColumn, 0f),
                       new Vector2(VictoryCardWidth, VictoryCardHeight));
            EnsureDivider(content, "Divider", new Vector2(VictoryColumn, 175f), new Vector2(740f, 2f));

            Button menu = EnsureButton(content, "Menu", "back to the menu", new Vector2(VictoryColumn, -205f),
                                       new Vector2(520f, 74f));
            Text summary = EnsureLabel(content, "Summary", "", 28, Cyan, new Vector2(VictoryColumn, 15f),
                                       new Vector2(760f, 240f));
            summary.lineSpacing = 1.35f;
            EnsureLabel(content, "Footer", "the maze never learned your name", 20, CyanFaint,
                        new Vector2(VictoryColumn, -300f), new Vector2(800f, 40f));

            VictoryEmote emote = EnsureVictoryEmote(content);

            var panel = SceneKit.EnsureComponent<VictoryPanel>(canvas.gameObject);
            var fade = SceneKit.EnsureComponent<UIPanelFade>(canvas.gameObject);
            SceneKit.SetReference(fade, "group", content.GetComponent<CanvasGroup>());
            var typewriter = SceneKit.EnsureComponent<UITypewriter>(canvas.gameObject);
            SceneKit.SetReference(typewriter, "label", summary);
            SceneKit.SetReference(panel, "content", content.gameObject);
            SceneKit.SetReference(panel, "fade", fade);
            SceneKit.SetReference(panel, "typewriter", typewriter);
            SceneKit.SetReference(panel, "summaryLabel", summary);
            SceneKit.SetReference(panel, "menuButton", menu);
            SceneKit.SetReference(panel, "emote", emote);

            // Hidden until the run ends by alpha rather than by deactivation: the fade and the typing
            // both run on this canvas, and the summary is the thing the player came to read.
            CanvasGroup group = content.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return panel;
        }

        /// <summary>
        /// The emote's own root: one rect and six layers, every layer the same size and registered to the
        /// same 1024 frame as the delivery sheets, so the animation's offsets are the only thing that moves
        /// them and the composition can never drift apart between rebuilds.
        ///
        /// The driver sits on this root rather than on the canvas because the art is what it animates; the
        /// root is a child of the panel's content, which this panel hides by canvas alpha and never by
        /// deactivation, so <c>Update</c> keeps running while the page is on screen.
        /// </summary>
        static VictoryEmote EnsureVictoryEmote(Transform content)
        {
            VictoryEmoteArt.LayerSet art = VictoryEmoteArt.EnsureAll();

            GameObject root = FindOrCreateChild(content, "Emote");
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(VictoryArtCentre, 0f);
            rect.sizeDelta = new Vector2(VictoryArtSize, VictoryArtSize);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            // Back to front: the pop, the light, the confetti in it, the word, then the two pieces the
            // ghost is actually standing on.
            Image flash = EnsureEmoteLayer(root.transform, "Flash", art.Flash, art.Glow, 0);
            Image swirls = EnsureEmoteLayer(root.transform, "Swirls", art.Swirls, art.Glow, 1);
            Image confetti = EnsureEmoteLayer(root.transform, "Confetti", art.Confetti, art.Glow, 2);
            Image title = EnsureEmoteLayer(root.transform, "Title", art.Title, art.Glow, 3);
            Image pedestal = EnsureEmoteLayer(root.transform, "Pedestal", art.Pedestal, null, 4);
            Image ghost = EnsureEmoteLayer(root.transform, "Ghost", art.Ghost, null, 5);

            var emote = SceneKit.EnsureComponent<VictoryEmote>(root);
            SceneKit.SetReference(emote, "flash", flash);
            SceneKit.SetReference(emote, "swirls", swirls);
            SceneKit.SetReference(emote, "confetti", confetti);
            SceneKit.SetReference(emote, "title", title);
            SceneKit.SetReference(emote, "pedestal", pedestal);
            SceneKit.SetReference(emote, "ghost", ghost);
            return emote;
        }

        /// <summary>
        /// One emote layer, stretched over the emote's square. The material is the whole reason these are
        /// split into two kinds: the light sheets add (their black becomes nothing, their glow survives),
        /// and the two matted pieces stay on the canvas's own alpha-blended material.
        /// </summary>
        static Image EnsureEmoteLayer(Transform parent, string name, Sprite sprite, Material material, int sibling)
        {
            Image image = EnsureImage(parent, name, sprite, Image.Type.Simple, Vector2.zero, Vector2.zero,
                                      Color.white, true, sibling);
            image.material = material;
            image.preserveAspect = false;
            image.rectTransform.localPosition = Vector3.zero;
            image.rectTransform.localRotation = Quaternion.identity;
            image.rectTransform.localScale = Vector3.one;
            return image;
        }

        /// <summary>
        /// The page the game opens on. The title is the only text in the project that is decoration rather
        /// than information, so it is the one label sized for looking at instead of for reading at a glance
        /// — tracked, and lit by the echo rings it is meant to be the source of.
        /// </summary>
        public static MainMenu EnsureMainMenu(EchoDirector director, VoiceCalibration calibration)
        {
            // 0.80 and not 0.88: the room behind this canvas now has moving light in it, and the wash's
            // job is to hold the text off that light, not to bury it. The centre was always empty on
            // purpose; now there is something worth seeing in it.
            Canvas canvas = EnsurePanel("Main Menu", out Transform content, out Text heading, 0.80f);

            heading = EnsureLabel(content, "Heading", Track("ECHO MAZE"), 96,
                                  Color.Lerp(Cyan, Color.white, 0.45f),
                                  new Vector2(0f, 320f), new Vector2(1200f, 160f),
                                  TextAnchor.MiddleCenter, true);

            // The nearest a legacy Text can come to a gradient: the glyph colour lifted toward white and
            // the glow Shadow left full cyan, so the bright part sits in the letters and the cyan bleeds
            // around them, which is the way a lit screen actually washes its own edges. A vertex gradient
            // would need TextMeshPro, and the whole interface is one font deep on legacy Text for the
            // sake of the rebuild doctrine above.

            // The rings go behind the title in sibling order, so they read as the title's own echo rather
            // than as wallpaper in front of it.
            Sprite ring = EnsureRingSprite();
            Image ring1 = EnsureImage(content, "Ring1", ring, Image.Type.Simple, new Vector2(0f, 320f),
                                      new Vector2(300f, 300f),
                                      new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f), false, 2);
            Image ring2 = EnsureImage(content, "Ring2", ring, Image.Type.Simple, new Vector2(0f, 320f),
                                      new Vector2(440f, 440f),
                                      new Color(Cyan.r, Cyan.g, Cyan.b, 0.20f), false, 3);
            Image ring3 = EnsureImage(content, "Ring3", ring, Image.Type.Simple, new Vector2(0f, 320f),
                                      new Vector2(580f, 580f),
                                      new Color(Cyan.r, Cyan.g, Cyan.b, 0.10f), false, 4);

            Text subtitle = EnsureLabel(content, "Subtitle", "a maze you see by sound", 24, CyanFaint,
                                        new Vector2(0f, 205f), new Vector2(1000f, 50f));

            // The column's own ground: the card the pause, victory and chapters pages are already printed
            // on, so the menu stops being the one screen whose buttons sit on geometry.
            //
            // A solid plate under the frame, not the panel's radial dim: that sprite is transparent in the
            // middle by design (it is a vignette), and the first attempt at this used it and darkened
            // nothing at all where the buttons are. The plate is opaque enough to take the table's wall
            // edges out of the button interiors, and the frame on top of it keeps the page language — a
            // hairline and corner brackets rather than a black rectangle. Both go in at index 2 and 3:
            // above the wash and the text column, below every button that is made after them.
            // Sized to the buttons and not a pixel more: the first version was 600 tall and its bottom
            // hairline struck straight through the controls legend at y −360, which is the one line on this
            // page that already has to fight a moving light. Top 168, bottom −310: clear of the subtitle at
            // 205 and of the legend below it, with 22 px of air above the first button.
            EnsureImage(content, "columnplate", EnsureSolidSprite(), Image.Type.Simple,
                        new Vector2(0f, -71f), new Vector2(720f, 478f),
                        new Color(0f, 0f, 0f, 0.62f), false, 2);
            EnsureImage(content, "columnframe", EnsureFramePanelSprite(), Image.Type.Sliced,
                        new Vector2(0f, -71f), new Vector2(720f, 478f), Color.white, false, 3);

            // The subtitle sits exactly where the table's far rim is brightest. The plate does not reach it
            // (it starts below the title), so the line gets the one ground that travels with it.
            EnsureTextShadow(subtitle, new Color(0f, 0f, 0f, 0.9f), new Vector2(0f, -1f));

            // Five entries now, so the column starts higher and every gap is even: the start button is
            // the run, "chapters" is the story, and the last three are the room's own furniture.
            Button start = EnsureButton(content, "Start", "begin", new Vector2(0f, 104f), new Vector2(520f, 84f));
            Button chapters = EnsureButton(content, "Chapters", "chapters", new Vector2(0f, 16f),
                                           new Vector2(520f, 68f));
            Button guide = EnsureButton(content, "Guide", "how to play", new Vector2(0f, -62f),
                                        new Vector2(520f, 68f));
            Button calibrate = EnsureButton(content, "Calibrate", "tune your voice", new Vector2(0f, -140f),
                                            new Vector2(520f, 68f));
            Button quit = EnsureButton(content, "Quit", "quit", new Vector2(0f, -218f), new Vector2(520f, 68f));
            // Left where it was: the status line sits over the diorama's brightest trim at the bottom of
            // the frame, and one row lower is the difference between a faint line and no line at all.
            // 21pt inside the plate's width rather than 24pt across the whole frame: the line is one long
            // device name, and a name that runs off the plate is a name sitting back on the lit table.
            Text status = EnsureLabel(content, "Status", "", 21, CyanMild, new Vector2(0f, -282f),
                                      new Vector2(800f, 40f));
            EnsureTextShadow(status, new Color(0f, 0f, 0f, 0.9f), new Vector2(0f, -1f));
            // Full alpha, not CyanFaint: the legend is the one label that states information nobody may
            // guess, and it sits over the frame that never stops glowing. A 45%-transparent glyph lets
            // that light through the letters themselves, which no shadow behind them can take back.
            Text legend = EnsureLabel(content, "Legend",
                                      "WASD move   mouse aim   Space shout (hold to charge)   F pulse\n" +
                                      "Shift silent   E door / exit   Esc pause",
                                      20, Cyan, new Vector2(0f, -360f), new Vector2(1600f, 80f));

            // The band goes in after the labels exist so its position in the draw order is the last
            // slot before them; it dims everything above it, which is why nothing visible is on it.
            EnsureImage(content, "legendband", EnsureLegendBandSprite(), Image.Type.Simple,
                        new Vector2(0f, -350f), new Vector2(1700f, 340f),
                        new Color(0f, 0f, 0f, 0.82f), false, 5);

            // The band darkens what the legend sits on; it cannot darken the glyph strokes themselves,
            // and a 20pt monospace over a wall that is lit is exactly the case where that matters. The
            // shadow is the part of the ground that moves with the text.
            EnsureTextShadow(legend, new Color(0f, 0f, 0f, 0.92f), new Vector2(0f, -2f));

            // The rings breathe and the page writes itself: both drivers sit on the canvas rather than
            // on the Content child so a hide of the menu never freezes a fade halfway through its own
            // face. The arrays are re-asserted in reading order on every rebuild.
            var pulse = SceneKit.EnsureComponent<UITitlePulse>(canvas.gameObject);
            SceneKit.SetReferenceArray(pulse, "rings", new UnityEngine.Object[] { ring1, ring2, ring3 });
            SceneKit.SetReference(pulse, "headingShadow", heading.GetComponent<Shadow>());

            var entrance = SceneKit.EnsureComponent<MenuEntrance>(canvas.gameObject);
            SceneKit.SetReferenceArray(entrance, "elements", new UnityEngine.Object[]
            {
                heading.transform, subtitle.transform, start.transform, chapters.transform,
                guide.transform, calibrate.transform, quit.transform, status.transform, legend.transform
            });

            var menu = canvas.GetComponent<MainMenu>() ?? canvas.gameObject.AddComponent<MainMenu>();
            SceneKit.SetReference(menu, "director", director);
            SceneKit.SetReference(menu, "startButton", start);
            SceneKit.SetReference(menu, "chaptersButton", chapters);
            SceneKit.SetReference(menu, "startLabel", start.transform.Find("Label") != null
                                  ? start.transform.Find("Label").GetComponent<Text>()
                                  : null);
            SceneKit.SetReference(menu, "guideButton", guide);
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

            // The guide is made after the calibration page so that it draws over it: "how do I play" is the
            // question a player asks while the meter is on the screen, and a page that let the live mic meter
            // blink through the copy would be a page nobody finishes reading.
            // Named "Guide" like the button that opens it, the way "Card" and "Close" are reused across the
            // panels of this file: they sit under different parents, and renaming one here would leave the
            // object the last rebuild made in the scene with nobody left to look for it.
            GameObject guidePage = FindOrCreateChild(canvas.transform, "Guide");
            guidePage.SetActive(false);
            SceneKit.SetReference(menu, "guidePanel", EnsureGuide(guidePage));

            // The story page last of all, so it draws over the guide and the calibration meter: a player
            // who opens "chapters" from a menu that still has a page folded away behind it is asking for
            // the top page, not for whichever one was made first.
            GameObject chapterPage = FindOrCreateChild(canvas.transform, "Chapters");
            chapterPage.SetActive(false);
            SceneKit.SetReference(menu, "chapterPanel", EnsureChapterSelect(chapterPage));
            return menu;
        }

        /// <summary>
        /// The story page: the chapters as nodes on one descending thread.
        ///
        /// The building code draws the thread and an empty node per row; every word that depends on the
        /// player — complete, open, sealed, the best run, which row is lit — is written by
        /// <see cref="ChapterSelectPanel"/> at runtime, because a scene that stored a save file would be a
        /// scene that ships somebody's progress. What is authored here is the layout and the prose that is
        /// true regardless of the player: the numerals, the chapter names, and the sentence joining each
        /// room to the next.
        /// </summary>
        /// <summary>
        /// The story page: ten chapters as one rail, and the selected one read out beside it.
        ///
        /// A rail rather than a list of rows, because the story is ten rooms long and ten rows cannot be
        /// seen at once — and seeing the whole descent, sealed rooms included, is the reason the page
        /// exists. The nodes are generated from <see cref="ChapterCatalog.Count"/> rather than from fixed
        /// slots, so a chapter added to the catalogue gets a node by re-running the bootstrap.
        ///
        /// Every word that depends on the player — walked, open, sealed, the record, which node is lit —
        /// is written by <see cref="ChapterSelectPanel"/> at runtime, because a scene that stored a save
        /// file would be a scene that ships somebody's progress.
        /// </summary>
        static ChapterSelectPanel EnsureChapterSelect(GameObject root)
        {
            Transform content = root.transform;

            // The page used to be rows of prose between two nodes. Find-or-create keeps anything it is not
            // told about, so those thread segments and their captions were still drawn underneath the rail —
            // the one kind of stale object a generated scene cannot repair by rebuilding its own layout.
            for (int i = 1; i <= 40; i++)
            {
                foreach (string stale in new[] { "Thread " + i, "ThreadText " + i })
                {
                    Transform child = content.Find(stale);
                    if (child != null)
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }
            }

            const float railX = -520f;
            const float firstRow = 236f;
            const float rowSpacing = 60f;
            const float dossierX = 150f;

            var pageGroup = SceneKit.EnsureComponent<CanvasGroup>(root);
            pageGroup.alpha = 1f;
            pageGroup.interactable = true;
            pageGroup.blocksRaycasts = true;

            // Opaque, not 0.96: at the diorama's brightest the menu's own title and buttons still read
            // through a four-percent window as grey ghosts under the card, and a reading page whose
            // background is somebody else's menu is a page nobody trusts.
            Image dim = EnsureImage(content, "dim", EnsureSolidSprite(), Image.Type.Simple, Vector2.zero,
                                    Vector2.zero, Color.black, true, 0);
            dim.raycastTarget = true;

            EnsureCard(content, new Vector2(0f, -8f), new Vector2(1460f, 830f));
            EnsureLabel(content, "Heading", Track("THE DESCENT"), 40, Cyan, new Vector2(0f, 354f),
                        new Vector2(1200f, 70f), TextAnchor.MiddleCenter, true);
            Text subtitle = EnsureLabel(content, "Subtitle", "", 22, CyanFaint, new Vector2(0f, 310f),
                                        new Vector2(1240f, 34f));
            EnsureDivider(content, "Divider", new Vector2(0f, 282f), new Vector2(1240f, 2f));

            int count = Mathf.Max(1, ChapterCatalog.Count);
            float lastRow = firstRow - rowSpacing * (count - 1);

            // The thread goes in first so the node markers cover it: a line drawn over the nodes reads as
            // a table, and a line behind them reads as a route.
            EnsureImage(content, "Rail", EnsureSolidSprite(), Image.Type.Simple,
                        new Vector2(railX, (firstRow + lastRow) * 0.5f),
                        new Vector2(2f, firstRow - lastRow + 22f), CyanFaint);

            Sprite node = EnsureNodeSprite();
            var buttons = new Button[count];
            var numerals = new Text[count];
            var markers = new Image[count];
            for (int i = 0; i < count; i++)
            {
                float y = firstRow - rowSpacing * i;
                Button row = EnsureRailNode(content, "Chapter " + (i + 1), new Vector2(railX, y),
                                            new Vector2(220f, 52f));
                buttons[i] = row;

                // 13 px inside a 50 px ring, because the numerals run to four characters: "VIII" set at the
                // neighbouring labels' 16 px is wider than the bead it is supposed to be riding.
                markers[i] = EnsureImage(row.transform, "Marker", node, Image.Type.Simple, Vector2.zero,
                                         new Vector2(50f, 50f), CyanMild);
                numerals[i] = EnsureLabel(row.transform, "Numeral", ChapterCatalog.At(i, null).Numeral, 13,
                                          Cyan, Vector2.zero, new Vector2(50f, 50f));
            }

            // The column that separates the route from the room it describes. Without it the dossier reads
            // as part of the rail, and the eye keeps trying to attach the paragraph to a node.
            EnsureDivider(content, "Column", new Vector2(-380f, -30f), new Vector2(1f, 548f));

            GameObject dossierObject = FindOrCreateChild(content, "Dossier");
            Transform dossier = dossierObject.transform;

            // Pinned to the page's own centre before anything is placed inside it: every label in the
            // dossier is positioned from its parent's centre, so a group that came back a few centimetres
            // off — which is what a child created by find-or-create can do — moves the whole column.
            var dossierRect = (RectTransform)dossier;
            dossierRect.anchorMin = Vector2.zero;
            dossierRect.anchorMax = Vector2.one;
            dossierRect.pivot = new Vector2(0.5f, 0.5f);
            dossierRect.offsetMin = Vector2.zero;
            dossierRect.offsetMax = Vector2.zero;
            dossierRect.localPosition = Vector3.zero;
            dossierRect.localScale = Vector3.one;

            var dossierGroup = SceneKit.EnsureComponent<CanvasGroup>(dossierObject);
            dossierGroup.alpha = 1f;
            dossierGroup.interactable = true;
            dossierGroup.blocksRaycasts = true;

            Text numeral = EnsureLabel(dossier, "Numeral", "", 20, CyanFaint, new Vector2(dossierX, 228f),
                                       new Vector2(840f, 30f));
            Text title = EnsureLabel(dossier, "Title", "", 46, Cyan, new Vector2(dossierX, 176f),
                                     new Vector2(880f, 64f), TextAnchor.MiddleCenter, true);
            Text grade = EnsureLabel(dossier, "Grade", "", 18, CyanMild, new Vector2(dossierX, 126f),
                                     new Vector2(900f, 28f));
            EnsureDivider(dossier, "Divider", new Vector2(dossierX, 100f), new Vector2(780f, 2f));
            Text body = EnsureLabel(dossier, "Body", "", 23, CyanMild, new Vector2(dossierX, 52f),
                                    new Vector2(840f, 84f));
            body.lineSpacing = 1.2f;
            Text thread = EnsureLabel(dossier, "Thread", "", 19, CyanFaint, new Vector2(dossierX, -30f),
                                      new Vector2(840f, 66f));
            thread.lineSpacing = 1.2f;
            Text record = EnsureLabel(dossier, "Record", "", 19, CyanFaint, new Vector2(dossierX, -96f),
                                      new Vector2(880f, 30f));
            Button play = EnsureButton(dossier, "Play", "walk this room", new Vector2(dossierX, -176f),
                                       new Vector2(500f, 80f));
            Transform playLabelObject = play.transform.Find("Label");
            Text playLabel = playLabelObject != null ? playLabelObject.GetComponent<Text>() : null;
            EnsureLabel(dossier, "Hint", "up/down choose    enter start    esc back", 16, CyanFaint,
                        new Vector2(dossierX, -244f), new Vector2(840f, 26f));

            // Outside the dossier group: the way out and the scoreboard must not fade with the paragraph
            // that is being replaced.
            Button back = EnsureButton(content, "Back", "back  (Esc)", new Vector2(dossierX, -300f),
                                       new Vector2(320f, 52f));
            Text status = EnsureLabel(content, "Status", "", 18, CyanFaint, new Vector2(dossierX, -352f),
                                      new Vector2(900f, 26f));

            // The pulse is drawn over the rail on purpose: a light that walks the thread to the node you
            // chose, and a ring that opens where it lands. Layered last so it is never hidden by the node
            // it is travelling to.
            var pulse = SceneKit.EnsureComponent<ChapterRailPulse>(root);
            Image travelling = EnsureImage(content, "Travelling", EnsureSolidSprite(), Image.Type.Simple,
                                           new Vector2(railX, firstRow), new Vector2(9f, 9f), Cyan);
            Image halo = EnsureImage(content, "Halo", node, Image.Type.Simple,
                                     new Vector2(railX, firstRow), new Vector2(120f, 120f),
                                     new Color(Cyan.r, Cyan.g, Cyan.b, 0f));
            SceneKit.SetFloat(pulse, "railX", railX);
            SceneKit.SetReference(pulse, "travelling", travelling);
            SceneKit.SetReference(pulse, "halo", halo);

            var panel = SceneKit.EnsureComponent<ChapterSelectPanel>(root);
            SceneKit.SetReference(panel, "group", pageGroup);
            SceneKit.SetReference(panel, "subtitleLabel", subtitle);
            SceneKit.SetReference(panel, "statusLabel", status);
            SceneKit.SetReferenceArray(panel, "railButtons", ToObjects(buttons));
            SceneKit.SetReferenceArray(panel, "railNumerals", ToObjects(numerals));
            SceneKit.SetReferenceArray(panel, "railMarkers", ToObjects(markers));
            SceneKit.SetReference(panel, "dossierGroup", dossierGroup);
            SceneKit.SetReference(panel, "dossierNumeral", numeral);
            SceneKit.SetReference(panel, "dossierTitle", title);
            SceneKit.SetReference(panel, "dossierGrade", grade);
            SceneKit.SetReference(panel, "dossierBody", body);
            SceneKit.SetReference(panel, "dossierThread", thread);
            SceneKit.SetReference(panel, "dossierRecord", record);
            SceneKit.SetReference(panel, "playButton", play);
            SceneKit.SetReference(panel, "playLabel", playLabel);
            SceneKit.SetReference(panel, "backButton", back);
            SceneKit.SetReference(panel, "pulse", pulse);
            return panel;
        }

        /// <summary>
        /// A node on the rail: a transparent target with a ring inside it, not a framed button.
        ///
        /// Ten brackets stacked down the page read as a table with ten rows; ten beads on a line read as a
        /// route, which is the whole point of the page. Hover is the only feedback, so it is the grow from
        /// <see cref="UIButtonFeedback"/> — with its glow switched off, because the frame is invisible and a
        /// lit rectangle behind every ring would be the table again.
        /// </summary>
        static Button EnsureRailNode(Transform parent, string name, Vector2 anchored, Vector2 size)
        {
            Transform existing = parent.Find(name);
            Button button = existing != null ? existing.GetComponent<Button>() : null;
            if (button == null)
            {
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<Image>();
                button = go.AddComponent<Button>();
            }

            // The row this node used to be carried four labels of its own; a bead on a rail has room for a
            // numeral and nothing else, and they would be drawn at the old row's offsets if left alone.
            foreach (string stale in new[] { "Title", "Subtitle", "State", "Record" })
            {
                Transform child = button.transform.Find(stale);
                if (child != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            var frame = button.GetComponent<Image>();
            frame.sprite = null;
            frame.type = Image.Type.Simple;
            frame.raycastTarget = true;
            frame.color = new Color(0f, 0f, 0f, 0f);

            var rect = frame.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchored;
            rect.sizeDelta = size;

            button.targetGraphic = frame;
            button.transition = Selectable.Transition.None;
            // The idle tint has to be transparent as well as the image: UIButtonFeedback repaints the frame
            // from the block's normal colour on every frame it ramps, and the default white would flash a
            // solid square behind the ring the moment the pointer leaves.
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0f, 0f, 0f, 0f);
            colors.highlightedColor = colors.normalColor;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = colors.normalColor;
            colors.disabledColor = colors.normalColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var feedback = SceneKit.EnsureComponent<UIButtonFeedback>(button.gameObject);
            SceneKit.SetReference(feedback, "frame", frame);
            SceneKit.SetFloat(feedback, "glowStrength", 0f);
            SceneKit.SetFloat(feedback, "hoverScale", 1.12f);
            return button;
        }

        static UnityEngine.Object[] ToObjects<T>(T[] items) where T : UnityEngine.Object
        {
            var objects = new UnityEngine.Object[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                objects[i] = items[i];
            }

            return objects;
        }

        /// <summary>
        /// A label with an explicit anchor and pivot, which is what a row needs and <see cref="EnsureLabel"/>'
        /// centre-only rect cannot express: a title that starts at the same x as the node above it, or a
        /// state that ends at the right edge of the row, has to be anchored to an edge rather than to the middle.
        /// </summary>
        static Text EnsureAnchoredLabel(Transform parent, string name, int fontSize, Color color,
                                        Vector2 anchor, Vector2 pivot, Vector2 anchored, Vector2 size,
                                        TextAnchor align)
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

            ApplyText(label, "", fontSize, color, anchored, size, align, false);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchored;
            rect.sizeDelta = size;
            return label;
        }

        /// <summary>
        /// A chapter node: a hairline ring with a tick crossing each axis, i.e. a point on a signal drawing
        /// rather than a bullet. Drawn at 96 texels and shown at 66 so the hairline stays a hairline — the
        /// menu's own ring sprite is 512 texels with a two-texel line and would be a quarter-pixel wide here.
        /// </summary>
        static Sprite EnsureNodeSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(NodePath);
            if (existing != null)
            {
                return existing;
            }

            SceneKit.EnsureFolder(System.IO.Path.GetDirectoryName(NodePath));
            const int size = 96;
            const float radius = 34f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float centre = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - centre;
                    float dy = y - centre;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - Mathf.Abs(d - radius) / 1.4f);

                    bool tick = (Mathf.Abs(dx) < 1f || Mathf.Abs(dy) < 1f) && d > radius + 3f && d < radius + 11f;
                    if (tick)
                    {
                        alpha = Mathf.Max(alpha, 1f - Mathf.Clamp01((d - radius - 11f) / 1.5f));
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(NodePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return ImportUiSprite(NodePath, 0);
        }

        /// <summary>
        /// The field guide page, on the same card and in the same colours as the calibration page: a bright
        /// full-screen dim, because this too has to blank out live UI sitting under it in the menu canvas.
        /// </summary>
        static GuidePanel EnsureGuide(GameObject root)
        {
            Transform content = root.transform;

            var pageGroup = SceneKit.EnsureComponent<CanvasGroup>(root);
            pageGroup.alpha = 1f;
            pageGroup.interactable = true;
            pageGroup.blocksRaycasts = true;

            EnsureImage(content, "dim", EnsureSolidSprite(), Image.Type.Simple, Vector2.zero, Vector2.zero,
                        new Color(0f, 0f, 0f, 0.96f), true, 0);

            EnsureImage(content, "ReadingGround", EnsureSolidSprite(), Image.Type.Simple,
                        Vector2.zero, new Vector2(1712f, 792f), new Color(0.008f, 0.018f, 0.022f, 1f), false, 1);
            EnsureCard(content, new Vector2(0f, 0f), new Vector2(1720f, 800f));
            EnsureDivider(content, "Divider", new Vector2(0f, 275f), new Vector2(980f, 2f));

            Text title = EnsureLabel(content, "Title", "", 34, Cyan, new Vector2(0f, 320f),
                                     new Vector2(1000f, 60f), TextAnchor.MiddleCenter, true);

            // Upper-left, and the copy in GuidePages is broken by hand: an auto-wrapped monospace paragraph
            // re-flows on every machine that is missing a glyph, and the page's whole job is to be read once
            // and remembered as a shape.
            Text body = EnsureLabel(content, "Body", "", 22, CyanMild, new Vector2(-270f, -20f),
                                    new Vector2(1040f, 530f), TextAnchor.UpperLeft, false);
            body.lineSpacing = 1.25f;

            EnsureDivider(content, "DiagramDivider", new Vector2(285f, 0f), new Vector2(1f, 510f));
            EnsureLabel(content, "DiagramTitle", "FIELD STUDY / IN MOTION", 20, Cyan,
                        new Vector2(555f, 228f), new Vector2(480f, 40f));
            Text caption = EnsureLabel(content, "DiagramCaption", "", 19, Warm,
                        new Vector2(555f, -228f), new Vector2(490f, 64f));
            GameObject diagramRoot = FindOrCreateChild(content, "Diagram");
            var diagram = SceneKit.EnsureComponent<GuideDiagram>(diagramRoot);
            diagram.raycastTarget = false;
            var diagramRect = diagram.rectTransform;
            diagramRect.anchorMin = diagramRect.anchorMax = new Vector2(0.5f, 0.5f);
            diagramRect.anchoredPosition = new Vector2(555f, 0f);
            diagramRect.sizeDelta = new Vector2(460f, 360f);
            SceneKit.SetReference(diagram, "caption", caption);

            Button prev = EnsureButton(content, "Prev", "< prev", new Vector2(-350f, -300f),
                                       new Vector2(220f, 56f));
            Text counter = EnsureLabel(content, "Counter", "", 20, CyanFaint, new Vector2(0f, -300f),
                                       new Vector2(300f, 40f));
            Button next = EnsureButton(content, "Next", "next >", new Vector2(350f, -300f),
                                       new Vector2(220f, 56f));
            Button close = EnsureButton(content, "Close", "back  (Esc)", new Vector2(0f, -360f),
                                        new Vector2(300f, 56f));

            var view = SceneKit.EnsureComponent<GuidePanel>(root);
            SceneKit.SetReference(view, "diagram", diagram);
            SceneKit.SetReference(view, "group", pageGroup);
            SceneKit.SetReference(view, "titleLabel", title);
            SceneKit.SetReference(view, "bodyLabel", body);
            SceneKit.SetReference(view, "counterLabel", counter);
            SceneKit.SetReference(view, "prevButton", prev);
            SceneKit.SetReference(view, "nextButton", next);
            SceneKit.SetReference(view, "closeButton", close);
            return view;
        }

        static CalibrationPanel EnsureCalibration(VoiceCalibration calibration, GameObject root)
        {
            Transform content = root.transform;

            // The calibration page switches its own object off when it closes, which makes it the one
            // panel allowed to fade from inside: nothing subscribes from here that a deactivation could
            // kill. The group is left at full alpha in the scene; OnEnable at runtime restarts the fade
            // from zero, and the close path only ever disables the object once alpha has reached zero.
            // EnsureComponent because this root is deliberately inactive in the editor while we build it.
            var pageGroup = SceneKit.EnsureComponent<CanvasGroup>(root);
            pageGroup.alpha = 1f;
            pageGroup.interactable = true;
            pageGroup.blocksRaycasts = true;

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

            var view = SceneKit.EnsureComponent<CalibrationPanel>(root);
            SceneKit.SetReference(view, "group", pageGroup);
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
