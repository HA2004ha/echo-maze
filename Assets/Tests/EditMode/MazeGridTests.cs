using System;
using System.Collections.Generic;
using EchoMaze.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The two questions that decide whether a maze is playable, answered by arithmetic rather than by
    /// somebody walking the route: is the exit reachable once the door is open, and is it unreachable while
    /// the door is closed. The shipped level file is the input, so this suite is what fails when a designer
    /// walls the only corridor in — which is the bug that a playtest finds two days later at the worst
    /// possible moment.
    /// </summary>
    public class MazeGridTests
    {
        const string Level01Path = "Assets/Data/Levels/Level01_Maze.txt";

        // The cell size the scene builder passes to the same file; a grid built with a different one would
        // still parse, so the test pins the number the level is actually drawn at.
        const float CellSize = 2f;

        static MazeGrid Level01()
        {
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(Level01Path);
            Assert.That(file, Is.Not.Null, Level01Path + " is missing — the level is the file, not the scene");
            return MazeGrid.Parse(file.text, CellSize);
        }

        static MazeGrid Tiny(string text) => MazeGrid.Parse(text, 1f);

        [Test]
        public void Parse_IgnoresCommentsAndBlankLines()
        {
            MazeGrid grid = Tiny("; legend\n\n#\n.\n");

            Assert.That(grid.Height, Is.EqualTo(2));
            Assert.That(grid.CharAt(0, 0), Is.EqualTo('.'), "the last line of the file is the bottom row");
            Assert.That(grid.CharAt(0, 1), Is.EqualTo('#'));
        }

        [Test]
        public void Parse_RejectsARaggedRow()
        {
            // The row is named by its grid index, which is the file's order turned upside down — the point is
            // that the parse refuses at all rather than padding a short line into a walkable hole.
            Assert.That(() => Tiny("###\n#.#\n##\n"),
                        Throws.TypeOf<FormatException>().With.Message.Contains("ragged"));
        }

        [Test]
        public void Parse_RejectsAnUnknownCharacter()
        {
            // A typo in a level file otherwise becomes "the player walks through a wall" three systems away;
            // naming the row is what makes it a two-minute fix instead of an afternoon.
            Assert.That(() => Tiny("###\n#Q#\n###\n"),
                        Throws.TypeOf<FormatException>().With.Message.Contains("'Q'"));
        }

        [Test]
        public void Parse_RejectsAnEmptyFile()
        {
            Assert.That(() => MazeGrid.Parse("", CellSize), Throws.TypeOf<FormatException>());
        }

        [Test]
        public void OutsideTheMapIsWall()
        {
            MazeGrid grid = Tiny("##\n##\n");

            Assert.That(grid.CharAt(-1, 0), Is.EqualTo(MazeGrid.Wall));
            Assert.That(grid.CharAt(0, grid.Height), Is.EqualTo(MazeGrid.Wall));
        }

        [Test]
        public void CellOf_IsTheInverseOfWorldOfCell()
        {
            MazeGrid grid = Level01();

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Assert.That(grid.CellOf(grid.WorldOfCell(x, y)), Is.EqualTo(new Vector2Int(x, y)),
                                "cell (" + x + "," + y + ") must come back from its own world position");
                }
            }
        }

        [Test]
        public void Level01_HasExactlyOneSpawnAndOneExit()
        {
            MazeGrid grid = Level01();

            Assert.That(grid.FindAll(MazeGrid.Spawn), Has.Count.EqualTo(1));
            Assert.That(grid.FindAll(MazeGrid.Exit), Has.Count.EqualTo(1));
            Assert.That(grid.FindAll(MazeGrid.Door), Has.Count.EqualTo(1),
                        "the tutorial teaches one door; two of them is a different level");
        }

        [Test]
        public void Level01_TheDoorGatesTheExit()
        {
            MazeGrid grid = Level01();
            Assert.That(grid.FindFirst(MazeGrid.Spawn, out Vector2Int spawn), Is.True);
            Assert.That(grid.FindFirst(MazeGrid.Exit, out Vector2Int exit), Is.True);

            HashSet<Vector2Int> closed = grid.Reachable(spawn, doorsOpen: false);
            HashSet<Vector2Int> open = grid.Reachable(spawn, doorsOpen: true);

            Assert.That(closed.Contains(exit), Is.False,
                        "with the door shut the exit must be on the other side of it — otherwise pressing E " +
                        "is never required and the level teaches nothing");
            Assert.That(open.Contains(exit), Is.True,
                        "with the door open the level has to be finishable at all");
        }

        [Test]
        public void Level01_EveryWalkableCellIsReachableWithTheDoorOpen()
        {
            MazeGrid grid = Level01();
            Assert.That(grid.FindFirst(MazeGrid.Spawn, out Vector2Int spawn), Is.True);

            HashSet<Vector2Int> open = grid.Reachable(spawn, doorsOpen: true);

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.BlocksMovement(x, y, doorsOpen: true))
                    {
                        continue;
                    }

                    Assert.That(open, Contains.Item(new Vector2Int(x, y)),
                                "cell (" + x + "," + y + ") is walkable but nothing can reach it: it is a " +
                                "pocket the player can never enter");
                }
            }
        }

        [Test]
        public void Level01_PillarsAndBoxesBlockTheRoute()
        {
            MazeGrid grid = Level01();

            foreach (Vector2Int pillar in grid.FindAll(MazeGrid.Pillar))
            {
                Assert.That(grid.BlocksMovement(pillar.x, pillar.y, true), Is.True);
            }

            foreach (Vector2Int box in grid.FindAll(MazeGrid.Box))
            {
                Assert.That(grid.BlocksMovement(box.x, box.y, true), Is.True);
            }
        }

        [Test]
        public void WallRects_CoverEveryWallCellExactlyOnce()
        {
            MazeGrid grid = Level01();
            List<MazeGrid.WallRect> rects = grid.WallRects();

            var seen = new HashSet<Vector2Int>();
            int cells = 0;
            foreach (MazeGrid.WallRect rect in rects)
            {
                for (int y = rect.Y; y <= rect.Max.y; y++)
                {
                    for (int x = rect.X; x <= rect.Max.x; x++)
                    {
                        Assert.That(grid.IsWall(x, y), Is.True, rect + " claims a cell that is not wall");
                        Assert.That(seen.Add(new Vector2Int(x, y)), Is.True,
                                    rect + " covers a cell another rect already has — two colliders in one " +
                                            "place is a wall the wave reads as a corner");
                        cells++;
                    }
                }
            }

            int wallCells = 0;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.IsWall(x, y))
                    {
                        wallCells++;
                    }
                }
            }

            Assert.That(cells, Is.EqualTo(wallCells), "every wall cell needs a collider");
        }

        [Test]
        public void WallRects_MergeRunsIntoLongSheets()
        {
            MazeGrid grid = Level01();
            int wallCells = 0;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.IsWall(x, y))
                    {
                        wallCells++;
                    }
                }
            }

            int rects = grid.WallRects().Count;

            // Measured on the shipped file: 168 wall cells become 40 rects. The point of the merge is that a
            // wall is one surface to the wave, and one collider per cell is the failure this asserts against.
            Assert.That(rects * 4, Is.LessThan(wallCells),
                        rects + " rects for " + wallCells + " cells is barely any merging");
        }
    }
}
