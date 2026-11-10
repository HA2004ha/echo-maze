using System;
using System.Collections.Generic;
using EchoMaze.Core;
using EchoMaze.Data;
using EchoMaze.Gameplay;
using EchoMaze.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// Every chapter that is a text file, built by one script.
    ///
    /// This is the whole level pipeline: <see cref="ChapterCatalog"/> says which chapters exist, this class
    /// makes one scene per chapter, and the scene's geometry is still drawn at runtime by
    /// <see cref="MazeLevel"/> from the ASCII file. Adding a level is therefore three edits — an entry, a
    /// .txt, and re-running the bootstrap — and none of them is a scene built by hand.
    ///
    /// The two things a grid can ask for that <see cref="MazeLevel"/> does not draw are placed here instead,
    /// because they are scene objects with audio and serialized tuning: the crawler ('C' plus its 'c'
    /// waypoints) and the hollows ('h'). Both are cleaned out by name before they are rebuilt, so a level
    /// that loses a crawler does not keep the last one's body.
    /// </summary>
    public static class ChapterSceneSetup
    {
        [MenuItem("Tools/Echo Maze/Build Chapter Scenes")]
        public static void BuildAll()
        {
            AssetDatabase.Refresh();

            foreach (ChapterStory chapter in ChapterCatalog.All)
            {
                if (chapter.HasGrid)
                {
                    BuildOne(chapter);
                }
            }
        }

        public static string ScenePathOf(ChapterStory chapter) =>
            "Assets/Scenes/" + chapter.Scene + ".unity";

        public static void BuildOne(ChapterStory chapter)
        {
            string scenePath = ScenePathOf(chapter);

            // One scene lifecycle per build: a new scene is built and saved at the end, an existing one is
            // opened. Saving an empty scene and immediately re-opening it works, but it destroys the objects
            // the build just made and the editor keeps handing their stale references back for the rest of
            // the frame — which is exactly how the first run of this builder failed.
            if (System.IO.File.Exists(scenePath))
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(chapter.GridPath);
            if (file == null)
            {
                Debug.LogError(chapter.GridPath + " is missing or not imported: " + chapter.Scene +
                               " would be an empty room. Save the .txt and re-run Tools/Echo Maze.");

                return;
            }

            MazeGrid grid;
            try
            {
                grid = MazeGrid.Parse(file.text, chapter.CellSize);
            }
            catch (FormatException error)
            {
                Debug.LogError(chapter.GridPath + " does not parse: " + error.Message);
                return;
            }

            EchoTuningProfile tuning = SceneKit.EnsureTuning();
            Material arcMaterial = SceneKit.EnsureMaterial();

            SceneKit.RemoveGlobalLights2D();
            SceneKit.EnsureVolume(SceneKit.EnsureVolumeProfile());
            EchoRenderer field = SceneKit.EnsureEchoField(arcMaterial);

            // The body is parked at the origin and the grid's own 'P' cell moves it during MazeLevel.Awake,
            // which runs before any Update: the spawn is authored in the map, not here.
            GameObject playerObject = SceneKit.EnsurePlayer(Vector2.zero);
            TouchMarks marks = SceneKit.EnsureTouchMarks();
            EchoDirector director = SceneKit.EnsureDirector(tuning, field);
            Player player = playerObject.GetComponent<Player>();
            SceneKit.SetReference(player, "director", director);
            SceneKit.SetReference(player, "marks", marks);
            SceneKit.EnsureAimCursor(playerObject);

            var half = new Vector2(grid.Width * chapter.CellSize * 0.5f, grid.Height * chapter.CellSize * 0.5f);
            LevelSession session = LevelSceneKit.BuildChrome(director, player, half, chapter.CameraSize,
                                                             chapter.HudTitle,
                                                             LevelSceneKit.LampsFor(chapter.Difficulty));

            // Only the room that teaches the controls teaches them on screen.
            if (chapter.Tutorial)
            {
                LevelSceneKit.EnsureHints(director, player, marks, session);
            }

            // The rig's clamp is overwritten by the level from the grid it actually read; what is wired here
            // is the object, so the camera follows from the first frame rather than after the first ping.
            var rig = UnityEngine.Object.FindAnyObjectByType<PlayerCameraRig>();
            SceneKit.EnsureMazeLevel(chapter.GridPath, chapter.CellSize, director, player, session, rig);

            PlaceCrawlers(grid, director, player);
            PlaceHollows(grid, player, session);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
            Selection.activeGameObject = playerObject;
        }

        /// <summary>
        /// One creature per 'C', walking every 'c' in file order. The patrol is shared rather than grouped per
        /// creature on purpose: a level with two of them needs a second patrol syntax, and no level in the
        /// story has two — the difficulty comes from the room, not from the count.
        /// </summary>
        static void PlaceCrawlers(MazeGrid grid, EchoDirector director, Player player)
        {
            RemoveStale<Crawler>("Crawler");

            List<Vector2Int> patrolCells = grid.FindAll(MazeGrid.Patrol);
            var patrol = new List<Vector2>();
            foreach (Vector2Int cell in patrolCells)
            {
                patrol.Add(grid.WorldOfCell(cell.x, cell.y));
            }

            int index = 0;
            foreach (Vector2Int spawn in grid.FindAll(MazeGrid.Crawler))
            {
                var points = new List<Vector2>(patrol);
                if (points.Count < 2)
                {
                    // A creature with no route would stand still forever: it still has to be walkable round,
                    // so it gets the cell beside its spawn as a two-point beat.
                    points.Add(grid.WorldOfCell(spawn.x, spawn.y));
                    points.Add(grid.WorldOfCell(Mathf.Min(spawn.x + 2, grid.Width - 2), spawn.y));
                }

                index++;
                SceneKit.EnsureCrawler(grid.WorldOfCell(spawn.x, spawn.y), points.ToArray(), director, player,
                                       "Crawler " + index);
            }
        }

        static void PlaceHollows(MazeGrid grid, Player player, LevelSession session)
        {
            RemoveStale<Hollow>("Hollow");

            int index = 0;
            foreach (Vector2Int cell in grid.FindAll(MazeGrid.Hollow))
            {
                index++;
                LevelSceneKit.EnsureHollow("Hollow " + index, grid.WorldOfCell(cell.x, cell.y),
                                           new Vector2(grid.CellSize * 0.9f, grid.CellSize * 0.9f),
                                           player, session);
            }
        }

        /// <summary>
        /// Removes the objects the last build of this scene made. Find-or-create by name is what keeps a
        /// rebuild from stacking a second HUD; it is also what keeps a creature the level no longer has, so
        /// the level's own objects are cleared by prefix before they are placed again.
        /// </summary>
        static void RemoveStale<T>(string prefix) where T : Component
        {
            foreach (T component in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include,
                                                                          FindObjectsSortMode.None))
            {
                if (component.gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(component.gameObject);
                }
            }
        }
    }
}
