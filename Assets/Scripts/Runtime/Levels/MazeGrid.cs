using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoMaze.Levels
{
    /// <summary>
    /// A level as a grid of characters, and the only place that knows what the characters mean.
    ///
    /// The level designer edits a text file rather than placing hundreds of invisible boxes in the
    /// editor: the room is drawn by <see cref="MazeLevel"/> at runtime and by nobody else, so a hand-move
    /// in the scene view would be a second, invisible source of truth for the same geometry. The parser is
    /// pure (no engine objects) because the two questions that decide whether a maze is playable — can the
    /// player get out, and does the door actually gate that — have to be answered in an EditMode test and
    /// not by somebody walking the route.
    /// </summary>
    public sealed class MazeGrid
    {
        public const char Wall = '#';
        public const char Floor = '.';
        public const char Spawn = 'P';
        public const char Exit = 'E';
        public const char Door = 'D';
        public const char Sanctuary = 'S';
        public const char Glass = 'G';
        public const char Pillar = 'o';
        public const char Box = 'b';

        /// <summary>A comment line, so the file can carry its own legend.</summary>
        public const char Comment = ';';

        readonly string[] rows;

        public int Width { get; }

        /// <summary>Row count. <c>y = 0</c> is the bottom row, whatever order the file is written in.</summary>
        public int Height { get; }

        public float CellSize { get; }

        MazeGrid(string[] rows, float cellSize)
        {
            this.rows = rows;
            Width = rows[0].Length;
            Height = rows.Length;
            CellSize = cellSize;
        }

        /// <summary>
        /// <c>rows[0]</c> is the bottom row. Kept public for the tests, which build a grid without a file.
        /// </summary>
        public static MazeGrid FromBottomUpRows(string[] rows, float cellSize)
        {
            if (rows == null || rows.Length == 0)
            {
                throw new FormatException("empty level: no rows");
            }

            int width = rows[0].Length;
            for (int y = 0; y < rows.Length; y++)
            {
                if (rows[y].Length != width)
                {
                    throw new FormatException("level row " + y + " is " + rows[y].Length +
                                              " cells wide, row 0 is " + width + ": a ragged grid has no bottom edge");
                }
            }

            return new MazeGrid(rows, cellSize);
        }

        /// <summary>
        /// The file format: one row per line, top row first, ';' comments and blank lines ignored. Ragged
        /// rows and unknown characters are errors that name the line, because a typo in a level file
        /// otherwise shows up as "the player falls through the world" three systems away.
        /// </summary>
        public static MazeGrid Parse(string text, float cellSize)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new FormatException("empty level file");
            }

            var kept = new List<string>();
            string[] lines = text.Replace("\r", "").Split('\n');
            foreach (string line in lines)
            {
                if (line.Length == 0 || line[0] == Comment)
                {
                    continue;
                }

                kept.Add(line);
            }

            foreach (string row in kept)
            {
                foreach (char c in row)
                {
                    if (!IsKnown(c))
                    {
                        throw new FormatException("unknown level character '" + c + "' in row \"" + row +
                                                  "\"; the legend is in the header of the file");
                    }
                }
            }

            // The file is written the way it reads (top first); the grid is indexed the way the world is.
            var bottomUp = new string[kept.Count];
            for (int i = 0; i < kept.Count; i++)
            {
                bottomUp[i] = kept[kept.Count - 1 - i];
            }

            return FromBottomUpRows(bottomUp, cellSize);
        }

        static bool IsKnown(char c)
        {
            switch (c)
            {
                case Wall:
                case Floor:
                case Spawn:
                case Exit:
                case Door:
                case Sanctuary:
                case Glass:
                case Pillar:
                case Box:
                    return true;
                default:
                    return false;
            }
        }

        public char CharAt(int x, int y)
        {
            // Outside the file is still wall: a player who walks off the authored map should hit a surface,
            // not fall into an unmeasurable void.
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return Wall;
            }

            return rows[y][x];
        }

        public bool IsWall(int x, int y) => CharAt(x, y) == Wall;

        /// <summary>Anything the body cannot move through, doors included while they are closed.</summary>
        public bool BlocksMovement(int x, int y, bool doorsOpen)
        {
            char c = CharAt(x, y);
            return c == Wall || c == Pillar || c == Box || (c == Door && !doorsOpen);
        }

        /// <summary>Centred on the origin, so the whole map is one coordinate system with the player spawn.</summary>
        public Vector2 WorldOfCell(int x, int y) =>
            new Vector2((x - (Width - 1) * 0.5f) * CellSize, (y - (Height - 1) * 0.5f) * CellSize);

        public Vector2Int CellOf(Vector2 world) =>
            new Vector2Int(
                Mathf.RoundToInt(world.x / CellSize + (Width - 1) * 0.5f),
                Mathf.RoundToInt(world.y / CellSize + (Height - 1) * 0.5f));

        /// <summary>The centre of the whole grid in world units, which is the origin by construction.</summary>
        public Vector2 WorldCentre => Vector2.zero;

        public Vector2 WorldSize => new Vector2(Width * CellSize, Height * CellSize);

        public List<Vector2Int> FindAll(char marker)
        {
            var found = new List<Vector2Int>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (rows[y][x] == marker)
                    {
                        found.Add(new Vector2Int(x, y));
                    }
                }
            }

            return found;
        }

        public bool FindFirst(char marker, out Vector2Int cell)
        {
            List<Vector2Int> all = FindAll(marker);
            cell = all.Count > 0 ? all[0] : Vector2Int.zero;
            return all.Count > 0;
        }

        /// <summary>
        /// The wall cells merged into as few axis-aligned boxes as a greedy scan can find.
        ///
        /// One collider per cell would give the wave hundreds of internal seams: every cell boundary is a
        /// jump in the distance profile, and <c>SurfaceRead</c> reads a jump as the edge of an object, so a
        /// long wall built from single cells would break and brighten along its own length instead of
        /// reading as one flat sheet. Merging runs is what makes a wall look like a wall.
        /// </summary>
        public List<WallRect> WallRects()
        {
            var covered = new bool[Width, Height];
            var rects = new List<WallRect>();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!IsWall(x, y) || covered[x, y])
                    {
                        continue;
                    }

                    int runWidth = 1;
                    while (x + runWidth < Width && IsWall(x + runWidth, y) && !covered[x + runWidth, y])
                    {
                        runWidth++;
                    }

                    int runHeight = 1;
                    while (y + runHeight < Height)
                    {
                        bool full = true;
                        for (int i = 0; i < runWidth; i++)
                        {
                            if (!IsWall(x + i, y + runHeight) || covered[x + i, y + runHeight])
                            {
                                full = false;
                                break;
                            }
                        }

                        if (!full)
                        {
                            break;
                        }

                        runHeight++;
                    }

                    for (int ry = 0; ry < runHeight; ry++)
                    {
                        for (int rx = 0; rx < runWidth; rx++)
                        {
                            covered[x + rx, y + ry] = true;
                        }
                    }

                    rects.Add(new WallRect(x, y, runWidth, runHeight));
                }
            }

            return rects;
        }

        /// <summary>
        /// Flood fill over the walkable cells. <c>doorsOpen</c> is the whole difference between the level
        /// being a loop and being a corridor: the tests call it both ways to prove the door is on the
        /// player's only route to the exit.
        /// </summary>
        public HashSet<Vector2Int> Reachable(Vector2Int from, bool doorsOpen)
        {
            var seen = new HashSet<Vector2Int> { from };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = cell.x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int ny = cell.y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    var next = new Vector2Int(nx, ny);
                    if (!seen.Contains(next) && !BlocksMovement(nx, ny, doorsOpen))
                    {
                        seen.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }

            return seen;
        }

        /// <summary>A merged block of wall cells, in cell coordinates, with the world box it becomes.</summary>
        public readonly struct WallRect
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Width;
            public readonly int Height;

            public WallRect(int x, int y, int width, int height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public Vector2Int Min => new Vector2Int(X, Y);
            public Vector2Int Max => new Vector2Int(X + Width - 1, Y + Height - 1);
            public int CellCount => Width * Height;

            public Vector2 Centre(float cellSize, int gridWidth, int gridHeight) =>
                new Vector2(((X + (Width - 1) * 0.5f) - (gridWidth - 1) * 0.5f) * cellSize,
                            ((Y + (Height - 1) * 0.5f) - (gridHeight - 1) * 0.5f) * cellSize);

            public Vector2 Size(float cellSize) => new Vector2(Width * cellSize, Height * cellSize);

            public override string ToString() => "rect(" + X + "," + Y + " " + Width + "x" + Height + ")";
        }
    }
}
