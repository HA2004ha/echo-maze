using System.Collections.Generic;
using UnityEngine;

namespace EchoMaze.Levels
{
    /// <summary>
    /// Where the lanterns of a room stand, chosen from the map rather than drawn into it.
    ///
    /// A lamp is a place, so the question this answers is "which places are worth a lamp" — and the answer
    /// that keeps the room readable is: the far corners and the dead ends, never the cell the player wakes
    /// on. Farthest-point sampling gives that with no authoring: take the walkable cell furthest from the
    /// spawn, then the one furthest from everything already taken, and so on. Two or three lamps therefore
    /// always land in different parts of the level, whatever shape the level is.
    ///
    /// Pure, so an EditMode test can prove the choice without a scene: no colliders, no physics queries, no
    /// random numbers — the same grid always gives the same lamps, which is also what makes a speedrun
    /// comparison between two players fair.
    /// </summary>
    public static class LanternPlacement
    {
        /// <summary>
        /// Up to <paramref name="count"/> cells for lanterns, farthest-spread first. Empty for a count of
        /// zero or a map with nowhere to stand.
        ///
        /// The spawn cell is excluded, and so is the exit (a lamp on the way out is a lamp wasted) and any
        /// door cell (the light would be drawn across a gap that opens).
        /// </summary>
        public static List<Vector2Int> Choose(MazeGrid grid, Vector2Int spawn, int count)
        {
            var chosen = new List<Vector2Int>();
            if (grid == null || count <= 0)
            {
                return chosen;
            }

            var candidates = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    char c = grid.CharAt(x, y);
                    if (cell == spawn || c == MazeGrid.Exit || c == MazeGrid.Door ||
                        grid.BlocksMovement(x, y, doorsOpen: true))
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }
            }

            if (candidates.Count == 0)
            {
                return chosen;
            }

            // The first lamp is the cell furthest from where the player wakes, so lighting it tells them
            // something they did not already know.
            Vector2Int first = Farthest(candidates, new List<Vector2Int> { spawn });
            chosen.Add(first);
            candidates.Remove(first);

            while (chosen.Count < count && candidates.Count > 0)
            {
                Vector2Int next = Farthest(candidates, chosen);
                chosen.Add(next);
                candidates.Remove(next);
            }

            return chosen;
        }

        /// <summary>
        /// The candidate whose distance to the nearest of <paramref name="taken"/> is largest. Ties go to
        /// the lower y then the lower x, so the result does not depend on the order the scan happened to
        /// find the cells in.
        /// </summary>
        static Vector2Int Farthest(List<Vector2Int> candidates, List<Vector2Int> taken)
        {
            Vector2Int best = candidates[0];
            float bestSqr = -1f;

            foreach (Vector2Int cell in candidates)
            {
                float nearest = float.PositiveInfinity;
                foreach (Vector2Int other in taken)
                {
                    float sqr = (cell - other).sqrMagnitude;
                    if (sqr < nearest)
                    {
                        nearest = sqr;
                    }
                }

                if (nearest > bestSqr ||
                    (Mathf.Approximately(nearest, bestSqr) && (cell.y < best.y || (cell.y == best.y && cell.x < best.x))))
                {
                    bestSqr = nearest;
                    best = cell;
                }
            }

            return best;
        }
    }
}
