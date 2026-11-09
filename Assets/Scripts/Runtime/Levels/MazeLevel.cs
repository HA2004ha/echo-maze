using System.Collections.Generic;
using EchoMaze.Core;
using EchoMaze.Data;
using EchoMaze.Gameplay;
using UnityEngine;

namespace EchoMaze.Levels
{
    /// <summary>
    /// The room drawn from a text file: one component, one grid, and every collider, zone and interactable
    /// in the level created from the characters in it.
    ///
    /// This exists instead of hundreds of scene objects for three reasons. The designer edits one readable
    /// file rather than clicking through an invisible maze. The same file is the input to the EditMode
    /// reachability tests, so the route the test proves and the route the player walks cannot drift apart.
    /// And a scene whose geometry is generated on first Awake stays a few kilobytes of YAML, which is the
    /// only reason two people can both touch the level without a merge conflict in <c>.unity</c> text.
    ///
    /// Nothing here is drawn: the walls are colliders with a <see cref="SurfaceKind.Plain"/> signature, and
    /// the only picture the game makes of them is the wave that comes back.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20)]
    public sealed class MazeLevel : MonoBehaviour
    {
        [Tooltip("The ASCII map, legend and all. Referenced, not copied: the file is the level.")]
        [SerializeField] TextAsset levelFile;
        [Tooltip("World units per grid cell. Two metres leaves a corridor wide enough to walk and turn in " +
                 "with a 0.25 m body, and keeps the whole 21x15 map inside about four pings of reach.")]
        [SerializeField, Min(0.5f)] float cellSize = 2f;
        [SerializeField] EchoDirector director;
        [SerializeField] Player player;
        [SerializeField] LevelSession session;
        [Tooltip("Optional: the rig whose clamp this level sets from its own extent.")]
        [SerializeField] PlayerCameraRig cameraRig;

        readonly List<Door> doors = new List<Door>();

        MazeGrid grid;

        /// <summary>The parsed map, for tests and for the HUD's proximity line.</summary>
        public MazeGrid Grid => grid;

        public float CellSize => cellSize;

        public int DoorCount => doors.Count;

        void Awake()
        {
            if (levelFile == null || director == null || player == null)
            {
                Debug.LogError(name + " is missing its level file, director or player: the room would be " +
                               "empty. Rebuild the scene with Tools/Echo Maze.", this);
                return;
            }

            grid = MazeGrid.Parse(levelFile.text, cellSize);

            if (!grid.FindFirst(MazeGrid.Spawn, out Vector2Int spawn))
            {
                Debug.LogError("level file has no '" + MazeGrid.Spawn + "' cell: the player has nowhere to " +
                               "stand. Rebuild the scene with Tools/Echo Maze.", this);
                return;
            }

            BuildWalls();
            BuildObstacles();
            BuildDoors(spawn);
            BuildNoiseZones();
            BuildSanctuaries();
            BuildExit();

            player.PlaceAt(grid.WorldOfCell(spawn.x, spawn.y));

            if (cameraRig != null)
            {
                // Half the map (outer wall edge): when the player is pressed against a wall, frame that
                // wall's outer edge at the screen edge — the touch point — rather than stopping at the
                // wall's inner face and leaving the wall cut off / overlapping the body.
                cameraRig.SetBounds(new Vector2(grid.Width * cellSize * 0.5f,
                                                grid.Height * cellSize * 0.5f));
            }
        }

        void BuildWalls()
        {
            Transform root = Child("Walls");
            int index = 0;
            foreach (MazeGrid.WallRect rect in grid.WallRects())
            {
                GameObject go = new GameObject("Wall " + index++);
                go.transform.SetParent(root, false);
                go.transform.position = rect.Centre(cellSize, grid.Width, grid.Height);
                go.layer = LayerMask.NameToLayer("EchoTarget");

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = rect.Size(cellSize);

                var surface = go.AddComponent<EchoSurface>();
                surface.kind = SurfaceKind.Plain;
                surface.reflectivity = 1f;
            }
        }

        /// <summary>
        /// The two objects the GDD asks a tutorial to contain: a round signature and a cornered one. They are
        /// placed as cells rather than as free transforms so that the map and the wave cannot disagree about
        /// where the pillar is.
        /// </summary>
        void BuildObstacles()
        {
            Transform root = Child("Obstacles");
            int index = 0;

            foreach (Vector2Int cell in grid.FindAll(MazeGrid.Pillar))
            {
                GameObject go = new GameObject("Pillar " + index++);
                go.transform.SetParent(root, false);
                go.transform.position = grid.WorldOfCell(cell.x, cell.y);
                go.layer = LayerMask.NameToLayer("EchoTarget");

                go.AddComponent<CircleCollider2D>().radius = cellSize * 0.3f;

                var surface = go.AddComponent<EchoSurface>();
                surface.kind = SurfaceKind.Plain;
                surface.reflectivity = 1f;
            }

            foreach (Vector2Int cell in grid.FindAll(MazeGrid.Box))
            {
                GameObject go = new GameObject("Box " + index++);
                go.transform.SetParent(root, false);
                go.transform.position = grid.WorldOfCell(cell.x, cell.y);
                go.layer = LayerMask.NameToLayer("EchoTarget");

                // Deliberately smaller than the cell: a box that fills its cell is indistinguishable from a
                // wall, and the point of the object is the four bright corners a wall does not have.
                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(cellSize, cellSize) * 0.7f;

                var surface = go.AddComponent<EchoSurface>();
                surface.kind = SurfaceKind.Plain;
                surface.reflectivity = 1f;
            }
        }

        /// <summary>
        /// A door cell holds a door and nothing else: the door builds its own leaf. This method only decides
        /// which mouth of the corridor the leaf stands in, and which way the player comes at it, both read
        /// from the map rather than authored — the one thing a designer should not have to remember is which
        /// way this door swings.
        ///
        /// A doorway has to be a **straight** passage: one open neighbour on each side of the same axis and
        /// walls on both flanks, so the leaf can close the corridor end flush between two wall faces. A door
        /// on a corner has no such line to stand in; the builder used to put one there anyway, which is how
        /// a leaf across one arm ended up beside a stub hanging into the other. That is a level-file mistake,
        /// so it stops the build with an error instead of shipping geometry no corridor has.
        /// </summary>
        void BuildDoors(Vector2Int spawn)
        {
            Transform root = Child("Doors");
            int index = 0;

            // Doors are closed while the map is walked, so this is the side of each door the player can
            // reach: the corridor the door is at the end of. The leaf goes in that corridor's mouth.
            HashSet<Vector2Int> spawnSide = grid.Reachable(spawn, doorsOpen: false);

            foreach (Vector2Int cell in grid.FindAll(MazeGrid.Door))
            {
                bool openEastWest = !grid.IsWall(cell.x - 1, cell.y) && !grid.IsWall(cell.x + 1, cell.y);
                bool openNorthSouth = !grid.IsWall(cell.x, cell.y - 1) && !grid.IsWall(cell.x, cell.y + 1);
                bool wallFlanks = openEastWest
                    ? grid.IsWall(cell.x, cell.y - 1) && grid.IsWall(cell.x, cell.y + 1)
                    : grid.IsWall(cell.x - 1, cell.y) && grid.IsWall(cell.x + 1, cell.y);

                if (openEastWest == openNorthSouth || !wallFlanks)
                {
                    Debug.LogError("door cell " + cell.x + "," + cell.y + " is not in a straight 1-cell " +
                                   "corridor (open left/right = " + openEastWest + ", open down/up = " +
                                   openNorthSouth + ", walls on both flanks = " + wallFlanks + "): a door " +
                                   "closes the end of a corridor, so it needs exactly that. Fix the map.", this);
                    continue;
                }

                Vector2 centre = grid.WorldOfCell(cell.x, cell.y);

                GameObject go = new GameObject("Door " + index++);
                go.transform.SetParent(root, false);
                go.transform.position = centre;
                go.layer = LayerMask.NameToLayer("Trigger");

                var probe = go.AddComponent<BoxCollider2D>();
                probe.size = new Vector2(cellSize, cellSize) * 1.2f;
                probe.isTrigger = true;

                // Direction of travel from the corridor the player can reach into the door's cell. The other
                // end of the cell is the new corridor, so the leaf and the swing both face that way.
                var axis = openEastWest ? Vector2Int.right : Vector2Int.up;
                bool approachFromNegative = spawnSide.Contains(cell - axis);
                Vector2 intoCell = approachFromNegative || !spawnSide.Contains(cell + axis)
                    ? (Vector2)axis
                    : -(Vector2)axis;

                var door = go.AddComponent<Door>();
                door.Build(director, player, true, intoCell, cellSize);
                doors.Add(door);

                session?.RegisterResetHook(door.ResetToStart);
            }
        }

        /// <summary>
        /// One zone per connected run of glass cells, not one per cell: three adjacent zones each with their
        /// own timer would report the same footstep three times and make the loudest floor in the game out of
        /// a patch that is meant to be a route, not an alarm.
        /// </summary>
        void BuildNoiseZones()
        {
            Transform root = Child("Noise zones");
            var taken = new HashSet<Vector2Int>();
            int index = 0;

            foreach (Vector2Int cell in grid.FindAll(MazeGrid.Glass))
            {
                if (taken.Contains(cell))
                {
                    continue;
                }

                var patch = FloodFill(cell, MazeGrid.Glass);
                foreach (Vector2Int c in patch)
                {
                    taken.Add(c);
                }

                Vector2 min = grid.WorldOfCell(cell.x, cell.y);
                Vector2 max = min;
                foreach (Vector2Int c in patch)
                {
                    Vector2 w = grid.WorldOfCell(c.x, c.y);
                    min = Vector2.Min(min, w);
                    max = Vector2.Max(max, w);
                }

                Vector2 size = max - min + new Vector2(cellSize, cellSize);
                Vector2 centre = (max + min) * 0.5f;

                GameObject go = new GameObject("Glass " + index++);
                go.transform.SetParent(root, false);
                go.transform.position = centre;

                var zone = go.AddComponent<NoisyFloor>();
                EchoTuningProfile profile = director.Profile;
                zone.Build(director, player, centre, size, profile.noisyFloorStepInterval);
                zone.Noised += () => session.Notice("the floor answers your step");
            }
        }

        void BuildSanctuaries()
        {
            Transform root = Child("Sanctuaries");
            int index = 0;

            foreach (Vector2Int cell in grid.FindAll(MazeGrid.Sanctuary))
            {
                Vector2 centre = grid.WorldOfCell(cell.x, cell.y);

                GameObject go = new GameObject("Sanctuary " + index++);
                go.transform.SetParent(root, false);
                go.transform.position = centre;

                var zone = go.AddComponent<Sanctuary>();
                zone.Build(player, session, centre, new Vector2(cellSize, cellSize));
            }
        }

        void BuildExit()
        {
            if (!grid.FindFirst(MazeGrid.Exit, out Vector2Int cell))
            {
                Debug.LogWarning("level file has no '" + MazeGrid.Exit + "' cell: this chapter cannot be " +
                                 "finished.", this);
                return;
            }

            GameObject go = new GameObject("Exit");
            go.transform.SetParent(Child("Exit"), false);
            go.transform.position = grid.WorldOfCell(cell.x, cell.y);
            go.layer = LayerMask.NameToLayer("Trigger");

            var probe = go.AddComponent<BoxCollider2D>();
            probe.size = new Vector2(cellSize, cellSize);
            probe.isTrigger = true;

            go.AddComponent<LevelExit>().Build(session);
        }

        List<Vector2Int> FloodFill(Vector2Int from, char marker)
        {
            var seen = new List<Vector2Int> { from };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    var next = new Vector2Int(cell.x + (i == 0 ? 1 : i == 1 ? -1 : 0),
                                              cell.y + (i == 2 ? 1 : i == 3 ? -1 : 0));
                    if (grid.CharAt(next.x, next.y) == marker && !seen.Contains(next))
                    {
                        seen.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }

            return seen;
        }

        Transform Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }
    }
}
