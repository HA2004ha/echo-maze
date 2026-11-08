using System.Collections.Generic;
using EchoMaze.Gameplay;
using EchoMaze.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The ten rooms as files: every grid chapter parses, can be walked from its spawn to its exit, gates
    /// that exit behind a door, and puts its creatures on floor the player can also reach.
    ///
    /// These are the questions that used to need a person walking a maze in Play Mode. They are assertions
    /// about the text file, so a map edited by hand and a map written by the forge are held to the same bar —
    /// which is what makes hand-editing a generated level safe.
    /// </summary>
    public class ChapterLevelTests
    {
        static IEnumerable<ChapterStory> GridChapters()
        {
            foreach (ChapterStory chapter in ChapterCatalog.All)
            {
                if (chapter.HasGrid)
                {
                    yield return chapter;
                }
            }
        }

        static MazeGrid Load(ChapterStory chapter)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(chapter.GridPath);
            Assert.IsNotNull(asset, chapter.GridPath + " is missing or was never imported, so " +
                                    chapter.Scene + " would build as an empty room");

            return MazeGrid.Parse(asset.text, chapter.CellSize);
        }

        static Vector2Int Spawn(MazeGrid grid)
        {
            Assert.IsTrue(grid.FindFirst(MazeGrid.Spawn, out Vector2Int spawn),
                          "a level with no 'P' spawns the player wherever the body happens to be");
            return spawn;
        }

        static Vector2Int Exit(MazeGrid grid)
        {
            Assert.IsTrue(grid.FindFirst(MazeGrid.Exit, out Vector2Int exit), "a level with no 'E' has no end");
            return exit;
        }

        [Test]
        public void TheStory_IsAtLeastTenRooms_AndEveryOneOfThemIsAccountedFor()
        {
            Assert.GreaterOrEqual(ChapterCatalog.Count, 10,
                                  "the run is supposed to be ten chapters from the maze to the sump");

            var seen = new HashSet<string>();
            foreach (ChapterStory chapter in ChapterCatalog.All)
            {
                Assert.IsTrue(seen.Add(chapter.Scene), chapter.Scene + " is in the story twice");
            }
        }

        [Test]
        public void TheLadder_OnlyEverGetsHarder()
        {
            int previous = -1;
            foreach (ChapterStory chapter in ChapterCatalog.All)
            {
                int grade = (int)chapter.Difficulty;
                Assert.GreaterOrEqual(grade, previous,
                                      chapter.Scene + " is easier than the chapter before it, which breaks " +
                                      "the one promise the story order makes");
                previous = grade;
            }

            Assert.AreEqual(ChapterDifficulty.Tutorial, ChapterCatalog.At(0).Difficulty,
                            "the first room has to be the one that teaches");
            Assert.AreEqual(ChapterDifficulty.Brutal, ChapterCatalog.At(ChapterCatalog.Count - 1).Difficulty,
                            "the last room has to be the hardest one in the story");
        }

        [Test]
        public void EveryGridChapter_ParsesAndItsSpawnCellIsFloor()
        {
            int checkedChapters = 0;
            foreach (ChapterStory chapter in GridChapters())
            {
                checkedChapters++;
                MazeGrid grid = Load(chapter);
                Vector2Int spawn = Spawn(grid);

                Assert.IsFalse(grid.BlocksMovement(spawn.x, spawn.y, false),
                               chapter.Scene + " spawns the player inside a wall or a closed door");
                Assert.Greater(grid.Width, 8, chapter.Scene + " is too narrow to walk");
                Assert.Greater(grid.Height, 8, chapter.Scene + " is too short to walk");
                Assert.Greater(chapter.CellSize, 0f, chapter.Scene + " has no cell size");
                Assert.Greater(chapter.CameraSize, 1f, chapter.Scene + " has no camera framing");
            }

            Assert.GreaterOrEqual(checkedChapters, 9,
                                  "all but the corridor should be a text file by now");
        }

        [Test]
        public void EveryGridChapter_CanBeWalkedFromSpawnToExit()
        {
            foreach (ChapterStory chapter in GridChapters())
            {
                MazeGrid grid = Load(chapter);
                Vector2Int spawn = Spawn(grid);
                Vector2Int exit = Exit(grid);

                CollectionAssert.Contains(grid.Reachable(spawn, true), exit,
                                          chapter.Scene + " has an exit the player can never reach, even " +
                                          "with every door open");
            }
        }

        [Test]
        public void EveryDoorInAGridChapter_ActuallyGatesTheExit()
        {
            // The one rule every level has kept since the first maze: a door is on the way out, not beside
            // it. A door the player can walk around is a door that teaches nothing.
            foreach (ChapterStory chapter in GridChapters())
            {
                MazeGrid grid = Load(chapter);
                if (grid.FindAll(MazeGrid.Door).Count == 0)
                {
                    continue;
                }

                Vector2Int spawn = Spawn(grid);
                Vector2Int exit = Exit(grid);
                CollectionAssert.DoesNotContain(grid.Reachable(spawn, false), exit,
                                                chapter.Scene + " can be finished without ever opening a door, " +
                                                "so the room's whole verb is optional");
            }
        }

        [Test]
        public void EveryGridChapter_PutsItsThreatsWhereThePlayerCanAlsoStand()
        {
            foreach (ChapterStory chapter in GridChapters())
            {
                MazeGrid grid = Load(chapter);
                HashSet<Vector2Int> reachable = grid.Reachable(Spawn(grid), true);

                foreach (char marker in new[] { MazeGrid.Crawler, MazeGrid.Patrol, MazeGrid.Hollow,
                                                MazeGrid.Sanctuary, MazeGrid.Glass })
                {
                    foreach (Vector2Int cell in grid.FindAll(marker))
                    {
                        Assert.IsFalse(grid.BlocksMovement(cell.x, cell.y, false),
                                       chapter.Scene + ": '" + marker + "' at " + cell.x + "," + cell.y +
                                       " is inside something solid");
                        CollectionAssert.Contains(reachable, cell,
                                                  chapter.Scene + ": '" + marker + "' at " + cell.x + "," +
                                                  cell.y + " is sealed away from the player, so it would " +
                                                  "never be seen");
                    }
                }
            }
        }

        [Test]
        public void EveryGridChapter_HasABuiltScene()
        {
            foreach (ChapterStory chapter in GridChapters())
            {
                string path = "Assets/Scenes/" + chapter.Scene + ".unity";
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(path),
                                 path + " does not exist: run Tools/Echo Maze/Build Everything, which makes " +
                                 "one scene per chapter in the catalogue");
            }
        }

        [Test]
        public void TheRoomThePlayerStartsIn_IsTheOneThatTeachesTheControls()
        {
            Assert.IsTrue(ChapterCatalog.At(0).Tutorial,
                          "the first room has no hint system, so the game opens without teaching anything");
            Assert.IsFalse(ChapterCatalog.At(1).Tutorial,
                           "the second room still teaches on screen, so the hints never stop");
        }
    }
}
