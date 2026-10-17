using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using EchoMaze.Core;
using EchoMaze.Data;
using EchoMaze.Gameplay;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// Writes the week-1 world with editor APIs instead of hand-editing YAML: Unity produces the
    /// scene, the material, the volume profile and the tuning asset, so the .meta files, GUIDs and
    /// serialization versions are always the ones this editor expects.
    ///
    /// Every step is idempotent, so it can be re-run after a reset or from batchmode with
    /// -executeMethod EchoMaze.EditorTools.SandboxSceneSetup.Bootstrap.
    /// </summary>
    public static class SandboxSceneSetup
    {
        const string SandboxPath = "Assets/Scenes/Sandbox.unity";
        const string TuningPath = "Assets/Data/Echo/EchoTuning.asset";
        const string VolumePath = "Assets/Settings/EchoVolumeProfile.asset";
        const string MaterialPath = "Assets/Art/Materials/Arc_Cyan.mat";
        static readonly string[] Layers = { "World", "EchoTarget", "Enemy", "Absorber", "Player", "Trigger" };
        static readonly string[] SortingLayers = { "Echo", "HUD" };

        [MenuItem("Tools/Echo Maze/Week 1 Bootstrap")]
        public static void Bootstrap()
        {
            ApplyProjectSettings();
            BuildSandbox();
            Debug.Log("Echo Maze: week 1 bootstrap done. Open " + SandboxPath + " and press Play.");
        }

        [MenuItem("Tools/Echo Maze/Apply Project Settings")]
        public static void ApplyProjectSettings()
        {
            // Assigning enterPlayModeOptions flips enterPlayModeOptionsEnabled back to true, so the
            // switch must be written last or the project ships with domain reload disabled.
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
            EditorSettings.enterPlayModeOptionsEnabled = false;
            Debug.Log("Echo Maze: enterPlayModeOptionsEnabled=" + EditorSettings.enterPlayModeOptionsEnabled +
                      " options=" + EditorSettings.enterPlayModeOptions);

            ConfigureLayers();
            ConfigureSortingLayers();
            AssetDatabase.SaveAssets();
        }

        static void ConfigureLayers()
        {
            var asset = LoadProjectSetting("ProjectSettings/TagManager.asset");
            if (asset == null)
            {
                return;
            }

            var so = new SerializedObject(asset);
            SerializedProperty layers = so.FindProperty("layers");
            if (layers == null)
            {
                return;
            }

            foreach (string wanted in Layers)
            {
                bool present = false;
                for (int i = 0; i < layers.arraySize; i++)
                {
                    if (layers.GetArrayElementAtIndex(i).stringValue == wanted)
                    {
                        present = true;
                        break;
                    }
                }

                if (present)
                {
                    continue;
                }

                // 0-5 are engine defaults (index 3 is reserved and always blank), so user layers start at 6.
                for (int i = 6; i < layers.arraySize; i++)
                {
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    {
                        layers.GetArrayElementAtIndex(i).stringValue = wanted;
                        break;
                    }
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureSortingLayers()
        {
            var asset = LoadProjectSetting("ProjectSettings/TagManager.asset");
            if (asset == null)
            {
                return;
            }

            var so = new SerializedObject(asset);
            SerializedProperty sorting = so.FindProperty("m_SortingLayers");
            if (sorting == null)
            {
                return;
            }

            int nextId = 1;
            for (int i = 0; i < sorting.arraySize; i++)
            {
                int id = sorting.GetArrayElementAtIndex(i).FindPropertyRelative("uniqueID").intValue;
                nextId = Mathf.Max(nextId, id + 1);
            }

            foreach (string wanted in SortingLayers)
            {
                bool present = false;
                for (int i = 0; i < sorting.arraySize; i++)
                {
                    if (sorting.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == wanted)
                    {
                        present = true;
                        break;
                    }
                }

                if (present)
                {
                    continue;
                }

                // Appending puts the layer above the existing ones, which is the order we want:
                // Default (invisible geometry) < Echo (the wave) < HUD.
                int index = sorting.arraySize;
                sorting.InsertArrayElementAtIndex(index);
                SerializedProperty entry = sorting.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("name").stringValue = wanted;
                entry.FindPropertyRelative("uniqueID").intValue = nextId++;
                entry.FindPropertyRelative("locked").boolValue = false;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static UnityEngine.Object LoadProjectSetting(string path)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("Cannot open " + path + " for editing.");
                return null;
            }

            return assets[0];
        }

        [MenuItem("Tools/Echo Maze/Build Sandbox Scene")]
        public static void BuildSandbox()
        {
            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Single);

            // Loading these before OpenScene makes them lose their last reference: the scene load
            // unloads unused assets and the local variables are left pointing at destroyed objects,
            // so the director gets wired to a fake-null profile and ships with an empty field.
            EchoTuningProfile tuning = EnsureTuning();
            VolumeProfile volume = EnsureVolumeProfile();
            Material arcMaterial = EnsureMaterial();

            foreach (Light2D stray in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Exclude))
            {
                // The game is unlit; a Global Light 2D only adds a lighting pass and a chance of the
                // wave being multiplied by an ambient colour instead of glowing.
                Object.DestroyImmediate(stray.gameObject);
            }

            Camera camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            // Unity 6 has no Camera.renderPostProcessing; the checkbox lives on URP's camera data.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.nearClipPlane = -100f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            EnsureVolume(volume);
            EchoRenderer field = EnsureEchoField(arcMaterial);
            EnsureCollider("Pillar_Concrete", new Vector2(3f, 0f), Circle(0.6f), SurfaceKind.Concrete, 1f);
            EnsureCollider("Wall_Concrete", new Vector2(0f, 5f), Box(4f, 0.3f), SurfaceKind.Concrete, 1f);
            EnsureCollider("Box_CornerTest", new Vector2(-3.5f, 2.2f), Box(1.2f, 1.2f), SurfaceKind.Concrete, 1f);
            GameObject player = EnsurePlayer();
            EnsureDirector(tuning, field);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), SandboxPath);
            Selection.activeGameObject = player;
        }

        static EchoTuningProfile EnsureTuning()
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

        static VolumeProfile EnsureVolumeProfile()
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

        static Material EnsureMaterial()
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

        static void EnsureVolume(VolumeProfile profile)
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

        static EchoRenderer EnsureEchoField(Material material)
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

        static void EnsureCollider(string name, Vector2 position, ColliderDef def, SurfaceKind kind, float reflectivity)
        {
            var existing = GameObject.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing;
                foreach (Collider2D old in go.GetComponents<Collider2D>())
                {
                    Object.DestroyImmediate(old);
                }
            }
            else
            {
                go = new GameObject(name);
            }

            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("EchoTarget");

            def.Assign(go);
            var surface = go.GetComponent<EchoSurface>();
            if (surface == null)
            {
                surface = go.AddComponent<EchoSurface>();
            }

            surface.kind = kind;
            surface.reflectivity = reflectivity;
        }

        static ColliderDef Circle(float radius) => new ColliderDef { circle = true, radius = radius };

        static ColliderDef Box(float width, float height) => new ColliderDef { width = width, height = height };

        class ColliderDef
        {
            public bool circle;
            public float radius;
            public float width = 1f;
            public float height = 1f;

            public void Assign(GameObject go)
            {
                if (circle)
                {
                    var c = go.AddComponent<CircleCollider2D>();
                    c.radius = radius;
                }
                else
                {
                    var c = go.AddComponent<BoxCollider2D>();
                    c.size = new Vector2(width, height);
                }
            }
        }

        static GameObject EnsurePlayer()
        {
            var player = GameObject.Find("Player");
            if (player == null)
            {
                player = new GameObject("Player");
            }

            player.transform.position = Vector2.zero;
            player.layer = LayerMask.NameToLayer("Player");

            var body = player.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = player.AddComponent<Rigidbody2D>();
            }

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = player.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = player.AddComponent<CircleCollider2D>();
            }

            collider.radius = 0.25f;

            var mock = player.GetComponent<PlayerMock>();
            if (mock == null)
            {
                mock = player.AddComponent<PlayerMock>();
            }

            var sprite = player.GetComponent<SpriteRenderer>();
            if (sprite == null)
            {
                sprite = player.AddComponent<SpriteRenderer>();
            }

            // A faint disc only so the origin of the wave is findable while tuning; the player is
            // meant to be effectively invisible, and this is not the final look. It has to be a real
            // asset: a runtime Sprite.Create reference cannot survive a scene save.
            Sprite dot = EnsurePlayerDot();
            if (dot != null)
            {
                sprite.sprite = dot;
            }

            sprite.color = new Color(0.25f, 0.45f, 0.47f, 0.35f);
            sprite.sortingLayerName = "Default";

            return player;
        }

        const string PlayerDotPath = "Assets/Art/Sprites/Player_dot.png";

        static Sprite EnsurePlayerDot()
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

        static void EnsureDirector(EchoTuningProfile tuning, EchoRenderer field)
        {
            var director = Object.FindAnyObjectByType<EchoDirector>();
            if (director == null)
            {
                director = new GameObject("Echo System").AddComponent<EchoDirector>();
            }

            int mask = 0;
            foreach (string layer in new[] { "World", "EchoTarget", "Absorber" })
            {
                int index = LayerMask.NameToLayer(layer);
                if (index >= 0)
                {
                    mask |= 1 << index;
                }
            }

            var so = new SerializedObject(director);
            so.FindProperty("profile").objectReferenceValue = tuning;
            so.FindProperty("echoLayerMask").intValue = mask;
            so.FindProperty("fieldRenderer").objectReferenceValue = field;
            so.ApplyModifiedPropertiesWithoutUndo();
            so.Update();
            if (so.FindProperty("profile").objectReferenceValue == null)
            {
                // Without the profile the director falls back to code defaults and logs a warning on
                // every Play, which is exactly the failure this bootstrap is meant to prevent.
                Debug.LogError("EchoDirector was saved without " + TuningPath + "; the scene ships untuned.");
            }
        }

        static void EnsureFolder(string path)
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
    }
}
