using System.Collections.Generic;
using EchoMaze.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    public sealed class VictoryMapView : MonoBehaviour
    {
        const float RevealSeconds = 1.8f;
        public static readonly Rect MapViewport = new Rect(0.055f, 0.235f, 0.89f, 0.56f);

        readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        LevelSession session;
        Camera worldCamera;
        Canvas canvas;
        CanvasGroup group;
        CanvasGroup chrome;
        VictoryMapGraphic map;
        Button continueButton;
        Font font;
        Vector3 cameraStart;
        float sizeStart;
        float elapsed;
        bool continued;
        public bool IsReady => elapsed >= RevealSeconds;
        public VictoryMapGraphic Map => map;

        public void Show(LevelSession completedSession, Player player)
        {
            if (!completedSession.IsComplete || canvas != null) return;
            session = completedSession;
            worldCamera = Camera.main;
            if (player != null) player.enabled = false;
            foreach (PlayerCameraRig rig in FindObjectsByType<PlayerCameraRig>(FindObjectsSortMode.None))
                rig.enabled = false;
            foreach (AimCursor cursor in FindObjectsByType<AimCursor>(FindObjectsSortMode.None))
                cursor.enabled = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            EventSystem.current?.SetSelectedGameObject(null);

            font = Resources.Load<Font>("Fonts/ShareTechMono-Regular");
            if (font == null)
            {
                foreach (Text text in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (text.font != null) { font = text.font; break; }
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            foreach (Canvas existing in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!existing.enabled) continue;
                hiddenCanvases.Add(existing);
                existing.enabled = false;
            }
            var root = new GameObject("Victory Map", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 900f);
            scaler.matchWidthOrHeight = 1f;
            group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            RectTransform background = Element("Paper", root.transform, Vector2.zero, Vector2.one);
            background.gameObject.AddComponent<Image>().color = VictoryMapGraphic.Paper;
            RectTransform drawing = Element("Revealed geometry", root.transform, Vector2.zero, Vector2.one);
            map = drawing.gameObject.AddComponent<VictoryMapGraphic>();
            map.Capture(worldCamera, player);
            RectTransform chromeRoot = Element("Atlas labels", root.transform, Vector2.zero, Vector2.one);
            chrome = chromeRoot.gameObject.AddComponent<CanvasGroup>();
            chrome.alpha = 0f;
            BuildChrome(chromeRoot);
            if (worldCamera != null)
            {
                cameraStart = worldCamera.transform.position;
                sizeStart = worldCamera.orthographicSize;
            }
        }

        public static float FitSize(Bounds bounds, float aspect) =>
            Mathf.Max((bounds.size.y + 1.6f) / MapViewport.height,
                (bounds.size.x + 1.6f) / (aspect * MapViewport.width)) * 0.54f;

        void LateUpdate()
        {
            if (canvas == null || continued) return;
            bool wasReady = IsReady;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / RevealSeconds);
            float eased = t * t * t * (t * (6f * t - 15f) + 10f);
            group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.1f));
            chrome.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - 1.1f) / 0.7f));
            if (worldCamera != null)
            {
                float size = FitSize(map.MapBounds, worldCamera.aspect);
                Vector3 centre = map.MapBounds.center;
                centre.y -= (MapViewport.center.y - 0.5f) * size * 2f;
                centre.z = cameraStart.z;
                Vector3 position = Vector3.Lerp(cameraStart, centre, eased);
                float zoom = Mathf.Lerp(sizeStart, size, eased);
                if (worldCamera.transform.position != position || worldCamera.orthographicSize != zoom)
                {
                    worldCamera.transform.position = position;
                    worldCamera.orthographicSize = zoom;
                    map.SetVerticesDirty();
                }
            }
            if (!wasReady && IsReady)
            {
                group.interactable = true;
                continueButton.interactable = true;
                EventSystem.current?.SetSelectedGameObject(continueButton.gameObject);
            }
            Keyboard keyboard = Keyboard.current;
            if (IsReady && keyboard != null && keyboard.enterKey.wasPressedThisFrame) Continue();
        }

        public void Continue()
        {
            if (!IsReady || continued || session == null || !session.IsComplete) return;
            continued = true;
            foreach (Canvas hidden in hiddenCanvases)
                if (hidden != null) hidden.enabled = true;
            canvas.gameObject.SetActive(false);
            if (GameFlow.Active != null && GameFlow.Active.IsRunning)
                GameFlow.Active.ContinueAfterVictory();
            else
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(gameObject.scene.name);
            }
        }

        void BuildChrome(Transform root)
        {
            Label(root, "E C H O   M A Z E   /   F I E L D   A T L A S", 16, VictoryMapGraphic.Muted,
                new Vector2(0.055f, 0.928f), new Vector2(0.94f, 0.965f));
            Label(root, "The dark, decoded.", 42, VictoryMapGraphic.Ink,
                new Vector2(0.055f, 0.857f), new Vector2(0.66f, 0.925f));
            Label(root, session.ChapterTitle.ToUpperInvariant() + "   /   COMPLETE", 16, VictoryMapGraphic.Teal,
                new Vector2(0.055f, 0.816f), new Vector2(0.70f, 0.855f));
            Label(root, session.PingCount + " WAVES   /   " + session.DeathCount + " RETRIES\n" +
                System.TimeSpan.FromSeconds(session.ElapsedSeconds).ToString(@"mm\:ss") + " IN THE DARK",
                18, VictoryMapGraphic.Muted, new Vector2(0.70f, 0.845f), new Vector2(0.945f, 0.925f),
                TextAnchor.MiddleRight);
            Rule(root, 0.808f);
            Rule(root, 0.217f);
            Label(root, "EVERYTHING THAT WAS THERE.", 15, VictoryMapGraphic.Muted,
                new Vector2(0.055f, 0.17f), new Vector2(0.62f, 0.211f));
            string[] names = { "WALL", "PILLAR / BOX", "DOOR", "GLASS", "SANCTUARY", "HOLLOW", "CRAWLER", "START / YOU", "EXIT" };
            Color[] colors = { VictoryMapGraphic.Ink, VictoryMapGraphic.Muted, VictoryMapGraphic.Amber,
                VictoryMapGraphic.Amber, VictoryMapGraphic.Teal, VictoryMapGraphic.Violet,
                VictoryMapGraphic.Coral, VictoryMapGraphic.Blue, VictoryMapGraphic.Teal };
            for (int i = 0; i < names.Length; i++)
            {
                float x = 0.055f + (i % 5) * 0.127f;
                float y = 0.127f - (i / 5) * 0.037f;
                RectTransform swatch = Element("Key " + names[i], root, new Vector2(x, y + 0.011f), new Vector2(x, y + 0.011f));
                swatch.sizeDelta = new Vector2(7f, 7f);
                swatch.gameObject.AddComponent<Image>().color = colors[i];
                Label(root, names[i], 13, VictoryMapGraphic.Ink, new Vector2(x + 0.008f, y), new Vector2(x + 0.125f, y + 0.03f));
            }
            Label(root, "No more guessing. Take a moment to see the room.", 15, VictoryMapGraphic.Muted,
                new Vector2(0.055f, 0.028f), new Vector2(0.68f, 0.071f));
            RectTransform buttonRect = Element("Continue", root, new Vector2(0.742f, 0.078f), new Vector2(0.945f, 0.145f));
            Image image = buttonRect.gameObject.AddComponent<Image>();
            image.color = VictoryMapGraphic.Ink;
            continueButton = buttonRect.gameObject.AddComponent<Button>();
            continueButton.targetGraphic = image;
            continueButton.interactable = false;
            ColorBlock colorsBlock = continueButton.colors;
            colorsBlock.highlightedColor = new Color(0.7f, 0.95f, 0.85f);
            colorsBlock.selectedColor = colorsBlock.highlightedColor;
            colorsBlock.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            continueButton.colors = colorsBlock;
            continueButton.onClick.AddListener(Continue);
            GameFlow flow = GameFlow.Active;
            string action = flow == null || !flow.IsRunning ? "PLAY AGAIN" :
                flow.ChapterIndex + 1 < flow.ChapterCount ? "NEXT CHAPTER  >" : "RUN SUMMARY  >";
            Label(buttonRect, action, 18, Color.white, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            Label(root, "ENTER TO CONTINUE", 12, VictoryMapGraphic.Muted,
                new Vector2(0.742f, 0.04f), new Vector2(0.945f, 0.073f), TextAnchor.MiddleCenter);
        }

        void Rule(Transform root, float y)
        {
            RectTransform line = Element("Divider", root, new Vector2(0.055f, y), new Vector2(0.945f, y));
            line.sizeDelta = new Vector2(0f, 1f);
            Image image = line.gameObject.AddComponent<Image>();
            image.color = new Color32(60, 80, 85, 255);
            image.raycastTarget = false;
        }

        Text Label(Transform root, string value, int size, Color color, Vector2 min, Vector2 max,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            Text text = Element(value, root, min, max).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(9, size / 2);
            text.resizeTextMaxSize = size;
            text.raycastTarget = false;
            return text;
        }

        static RectTransform Element(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
