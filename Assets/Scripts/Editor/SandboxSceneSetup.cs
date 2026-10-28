using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EchoMaze.Core;
using EchoMaze.Data;
using EchoMaze.Gameplay;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// The week-1 math playground: three objects in an empty black space, kept exactly because it is the
    /// smallest scene that can show one wave. Gameplay is tuned in Level02_Corridor; both scenes are built
    /// by the same editor APIs in <see cref="SceneKit"/>, never by hand-editing YAML.
    ///
    /// Every step is idempotent, so it can be re-run after a reset or from batchmode with
    /// -executeMethod EchoMaze.EditorTools.SandboxSceneSetup.Bootstrap.
    /// </summary>
    public static class SandboxSceneSetup
    {
        const string SandboxPath = "Assets/Scenes/Sandbox.unity";
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
            EchoTuningProfile tuning = SceneKit.EnsureTuning();
            Material arcMaterial = SceneKit.EnsureMaterial();

            SceneKit.RemoveGlobalLights2D();
            SceneKit.ConfigureCamera();
            SceneKit.EnsureVolume(SceneKit.EnsureVolumeProfile());
            EchoRenderer field = SceneKit.EnsureEchoField(arcMaterial);

            // The "_Concrete" in these names is a leftover from the material system GDD 3.0 removed; the
            // objects are ordinary geometry now. The names stay because GitSetupValidator's merge probe
            // matches on "Pillar_Concrete", and renaming scene objects by hand-editing YAML is the exact
            // trap this project's rules forbid. Rename them in the editor, and update the probe with them.
            SceneKit.EnsureShape("Pillar_Concrete", new Vector2(3f, 0f),
                                 SceneKit.Shape.Circle(0.6f), SurfaceKind.Plain);
            SceneKit.EnsureShape("Wall_Concrete", new Vector2(0f, 5f),
                                 SceneKit.Shape.Box(4f, 0.3f), SurfaceKind.Plain);
            SceneKit.EnsureShape("Box_CornerTest", new Vector2(-3.5f, 2.2f),
                                 SceneKit.Shape.Box(1.2f, 1.2f), SurfaceKind.Plain);

            GameObject playerObject = SceneKit.EnsurePlayer(Vector2.zero);
            TouchMarks marks = SceneKit.EnsureTouchMarks();
            EchoDirector director = SceneKit.EnsureDirector(tuning, field);
            SceneKit.SetReference(playerObject.GetComponent<Player>(), "director", director);
            SceneKit.SetReference(playerObject.GetComponent<Player>(), "marks", marks);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), SandboxPath);
            Selection.activeGameObject = playerObject;
        }
    }
}
