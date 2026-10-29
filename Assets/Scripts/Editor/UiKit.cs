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
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Cyan = new Color(0.498f, 0.914f, 0.941f, 1f);

        const int HudSortingOrder = 10;
        const int PanelSortingOrder = 100;

        const string SolidPath = "Assets/Art/Sprites/UI_solid.png";
        const int SolidTexels = 4;

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

        public static Text AddText(Transform parent, string name, string value, int fontSize,
                                   Color color, Vector2 anchored, Vector2 size, bool center = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Text label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = value;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = center ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            label.raycastTarget = false;

            var rect = label.rectTransform;
            rect.anchoredPosition = anchored;
            rect.sizeDelta = size;
            return label;
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

            Image background = go.AddComponent<Image>();
            // Almost transparent, because the interface is a line drawing: a filled panel behind a label is
            // the brightest thing on the screen and Bloom would turn the menu into a light source.
            background.color = new Color(0f, 0f, 0f, 0.35f);
            background.sprite = EnsureSolidSprite();

            var rect = background.rectTransform;
            rect.anchoredPosition = anchored;
            rect.sizeDelta = size;

            var button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;

            Text text = AddText(go.transform, "Label", label, 30, Cyan, Vector2.zero, size);
            text.raycastTarget = false;
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

        /// <summary>
        /// The HUD canvas for a level: the cooldown bar, the chapter title, the prompt line and the notice.
        /// Re-running keeps one canvas and re-wires the same four elements, so a bootstrap cannot leave a
        /// second bar on screen.
        /// </summary>
        public static HudView EnsureHud(EchoDirector director, Player player, LevelSession session)
        {
            Canvas canvas = EnsureCanvas("HUD", HudSortingOrder);
            Transform root = canvas.transform;

            Image meter = FindBar(root, "Cooldown") ??
                          AddBar(root, "Cooldown", Cyan, new Vector2(-860f, 420f), new Vector2(16f, 220f));
            Text title = FindText(root, "Title") ??
                         AddText(root, "Title", "", 40, Cyan, new Vector2(0f, 430f), new Vector2(1100f, 90f));
            Text prompt = FindText(root, "Prompt") ??
                          AddText(root, "Prompt", "", 30, Cyan, new Vector2(0f, -470f),
                                  new Vector2(1100f, 70f));
            Text notice = FindText(root, "Notice") ??
                          AddText(root, "Notice", "", 26, Cyan, new Vector2(0f, 340f), new Vector2(1100f, 60f));

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

        static Image FindBar(Transform root, string name)
        {
            Transform child = root.Find(name);
            return child != null ? child.GetComponent<Image>() : null;
        }

        static Text FindText(Transform root, string name)
        {
            Transform child = root.Find(name);
            return child != null ? child.GetComponent<Text>() : null;
        }

        /// <summary>
        /// A panel canvas with a dim wash and a titled column of buttons. Shared by pause, victory and
        /// calibration because three screens with the same four words set five pixels apart is exactly the
        /// kind of drift a generated UI is supposed to prevent.
        ///
        /// The component lives on the canvas and everything visible lives inside the <c>content</c> child,
        /// because a panel that switches itself off also unsubscribes itself from the event that would
        /// switch it back on.
        /// </summary>
        public static Canvas EnsurePanel(string name, out Transform content, out Text heading)
        {
            Canvas canvas = EnsureCanvas(name, PanelSortingOrder);

            GameObject contentObject = FindOrCreateChild(canvas.transform, "Content");
            content = contentObject.transform;

            if (content.Find("wash") == null)
            {
                AddBackground(content, "wash", new Color(0f, 0f, 0f, 0.82f));
            }

            heading = content.Find("Heading") != null
                ? content.Find("Heading").GetComponent<Text>()
                : AddText(content, "Heading", "", 44, Cyan, new Vector2(0f, 330f), new Vector2(1200f, 90f));

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

        static Button EnsureButton(Transform parent, string name, string label, Vector2 anchored, Vector2 size)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<Button>();
            }

            return AddButton(parent, name, label, anchored, size);
        }

        static Text EnsureText(Transform parent, string name, string label, int size, Vector2 anchored,
                               Vector2 extent)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<Text>();
            }

            return AddText(parent, name, label, size, Cyan, anchored, extent);
        }

        /// <summary>
        /// The pause screen. Every button on it does one thing that a keyboard cannot do comfortably: quit,
        /// and go back to a menu the player has already left.
        /// </summary>
        public static PausePanel EnsurePausePanel(LevelSession session)
        {
            Canvas canvas = EnsurePanel("Pause Panel", out Transform content, out Text heading);
            heading.text = "HELD YOUR BREATH";

            Button resume = EnsureButton(content, "Resume", "resume  (Esc)", new Vector2(0f, 160f),
                                         new Vector2(420f, 74f));
            Button restart = EnsureButton(content, "Restart", "restart this room", new Vector2(0f, 40f),
                                          new Vector2(420f, 74f));
            Button menu = EnsureButton(content, "Menu", "back to the menu", new Vector2(0f, -80f),
                                       new Vector2(420f, 74f));
            Button quit = EnsureButton(content, "Quit", "quit", new Vector2(0f, -200f), new Vector2(420f, 74f));
            Text room = EnsureText(content, "Room", "", 22, new Vector2(0f, -300f), new Vector2(700f, 40f));

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
            heading.text = "YOU WERE NOT HEARD";

            Button menu = EnsureButton(content, "Menu", "back to the menu", new Vector2(0f, -220f),
                                       new Vector2(460f, 74f));
            Text summary = EnsureText(content, "Summary", "", 28, new Vector2(0f, 60f),
                                      new Vector2(1100f, 220f));
            summary.alignment = TextAnchor.MiddleCenter;

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
        /// than information, so it is the one label sized for looking at instead of for reading at a glance.
        /// </summary>
        public static MainMenu EnsureMainMenu(EchoDirector director, VoiceCalibration calibration)
        {
            Canvas canvas = EnsurePanel("Main Menu", out Transform content, out Text heading);
            heading.text = "ECHO MAZE";
            heading.fontSize = 72;
            heading.rectTransform.anchoredPosition = new Vector2(0f, 300f);

            Button start = EnsureButton(content, "Start", "begin", new Vector2(0f, 120f), new Vector2(420f, 78f));
            Button calibrate = EnsureButton(content, "Calibrate", "tune your voice", new Vector2(0f, 4f),
                                            new Vector2(420f, 64f));
            Button quit = EnsureButton(content, "Quit", "quit", new Vector2(0f, -100f), new Vector2(420f, 64f));
            Text status = EnsureText(content, "Status", "", 24, new Vector2(0f, -260f), new Vector2(1400f, 44f));
            Text legend = EnsureText(content, "Legend",
                                     "WASD move   mouse aim   Space shout (hold to charge)   F pulse   " +
                                     "Shift silent   E touch   Esc stop",
                                     22, new Vector2(0f, -330f), new Vector2(1600f, 44f));
            legend.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.62f);

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

            Button begin = EnsureButton(content, "Begin", "listen", new Vector2(0f, -120f), new Vector2(360f, 64f));
            Button close = EnsureButton(content, "Close", "back", new Vector2(0f, -210f), new Vector2(360f, 64f));
            Image meter = FindBar(content, "Meter") ??
                          AddBar(content, "Meter", Cyan, new Vector2(0f, 120f), new Vector2(900f, 26f));
            Text instruction = EnsureText(content, "Instruction", "", 26, new Vector2(0f, 220f),
                                          new Vector2(1200f, 60f));
            Text result = EnsureText(content, "Result", "", 24, new Vector2(0f, -20f), new Vector2(1200f, 60f));

            // The input choice is a step button rather than a Dropdown: a generated dropdown needs a template,
            // a mask, a scrollbar and an item prefab, all of it built by hand in editor code and none of it
            // visible to anybody who runs the bootstrap headless. One button that names what the game is
            // actually hearing and applies on the press does the same job with parts this file already knows
            // how to make. See Docs/DESIGN_DECISIONS.md, 2026-10-29.
            Button device = EnsureButton(content, "Device", "input: auto", new Vector2(0f, -300f),
                                         new Vector2(620f, 56f));
            Text deviceInfo = EnsureText(content, "DeviceInfo", "", 22, new Vector2(0f, -360f),
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
