using System.Collections.Generic;
using EchoMaze.Levels;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// Where the lamps of a room stand, without a scene. The rule is arithmetic on the map, so the two
    /// things that can go wrong — a lamp in a wall, a lamp where the player already is — are asserted here
    /// rather than found by walking the level.
    /// </summary>
    public class LanternPlacementTests
    {
        const float Cell = 2f;

        /// <summary>A 9x7 room with a wall band splitting it, so "spread" has something to spread across.</summary>
        static MazeGrid Room()
        {
            string[] rows =
            {
                "#########",
                "#.......#",
                "#.......#",
                "####.####",
                "#.......#",
                "#...P...#",
                "#########",
            };

            return MazeGrid.FromBottomUpRows(rows, Cell);
        }

        [Test]
        public void TheLamps_AreWalkableCells_AndNeverTheSpawn()
        {
            MazeGrid grid = Room();
            Assert.IsTrue(grid.FindFirst(MazeGrid.Spawn, out Vector2Int spawn));

            List<Vector2Int> cells = LanternPlacement.Choose(grid, spawn, 3);

            Assert.AreEqual(3, cells.Count);
            foreach (Vector2Int cell in cells)
            {
                Assert.AreNotEqual(spawn, cell, "a lamp on the spawn is a lamp the player cannot walk to");
                Assert.IsFalse(grid.BlocksMovement(cell.x, cell.y, doorsOpen: true),
                               cell + " is inside something the body cannot enter");
            }
        }

        [Test]
        public void TheLamps_AreSpreadApart_NotClusteredInOneCorner()
        {
            MazeGrid grid = Room();
            Assert.IsTrue(grid.FindFirst(MazeGrid.Spawn, out Vector2Int spawn));

            List<Vector2Int> cells = LanternPlacement.Choose(grid, spawn, 2);

            // Farthest-point sampling puts them at opposite ends of the room. Two lamps a cell apart would
            // be one lamp and a spare, which is not what two lamps are for.
            Assert.GreaterOrEqual(Vector2Int.Distance(cells[0], cells[1]), 6,
                                  "the two lamps are " + Vector2Int.Distance(cells[0], cells[1]) + " cells apart");
        }

        [Test]
        public void TheChoice_IsDeterministic()
        {
            MazeGrid grid = Room();
            Assert.IsTrue(grid.FindFirst(MazeGrid.Spawn, out Vector2Int spawn));

            List<Vector2Int> first = LanternPlacement.Choose(grid, spawn, 3);
            List<Vector2Int> second = LanternPlacement.Choose(grid, spawn, 3);

            CollectionAssert.AreEqual(first, second, "the same room gives the same lamps, every run");
        }

        [Test]
        public void MoreLampsThanRoom_GivesTheRoom()
        {
            var grid = MazeGrid.FromBottomUpRows(new[] { "###", "#P#", "###" }, Cell);
            Assert.IsTrue(grid.FindFirst(MazeGrid.Spawn, out Vector2Int spawn));

            List<Vector2Int> cells = LanternPlacement.Choose(grid, spawn, 3);

            Assert.AreEqual(0, cells.Count, "the only walkable cell is the spawn, which is never taken");
        }

        [Test]
        public void NoLamps_ForACountOfZero()
        {
            MazeGrid grid = Room();
            Assert.IsTrue(grid.FindFirst(MazeGrid.Spawn, out Vector2Int spawn));

            Assert.IsEmpty(LanternPlacement.Choose(grid, spawn, 0));
        }

        [Test]
        public void TheExitAndDoorCells_AreLeftAlone()
        {
            string[] rows =
            {
                "#########",
                "#E......#",
                "#.......#",
                "####.####",
                "#...D...#",
                "#...P...#",
                "#########",
            };

            MazeGrid grid = MazeGrid.FromBottomUpRows(rows, Cell);
            Assert.IsTrue(grid.FindFirst(MazeGrid.Spawn, out Vector2Int spawn));

            List<Vector2Int> cells = LanternPlacement.Choose(grid, spawn, 4);

            foreach (Vector2Int cell in cells)
            {
                char c = grid.CharAt(cell.x, cell.y);
                Assert.AreNotEqual(MazeGrid.Exit, c, "a lamp on the way out is a lamp wasted");
                Assert.AreNotEqual(MazeGrid.Door, c, "a lamp in a doorway is a lamp in a gap");
            }
        }
    }
}
