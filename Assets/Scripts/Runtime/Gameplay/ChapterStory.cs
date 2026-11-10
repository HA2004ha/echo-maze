using System;

namespace EchoMaze.Gameplay
{
    /// <summary>How much the room asks of the player, as the story page prints it.</summary>
    public enum ChapterDifficulty
    {
        Tutorial,
        Easy,
        Medium,
        Hard,
        Brutal
    }

    /// <summary>
    /// One chapter as the story tells it: a numeral, a name, the sentence that joins it to the next room,
    /// the line whispered when it opens, and the line it leaves behind once it has been survived — plus the
    /// two facts the build needs to make the room: the grid file it is drawn from, and what shares it.
    ///
    /// The level list is <see cref="GameFlow.chapterScenes"/> — what the build contains and in what order —
    /// and it is written from this file. Keeping the geometry reference next to the prose is the whole
    /// "adding a level is one entry" promise: a chapter that has a grid is a scene the builder makes, and a
    /// chapter without one is a room with its own hand-built scene builder.
    /// </summary>
    public readonly struct ChapterStory
    {
        public ChapterStory(string scene, string numeral, string title, string subtitle,
                            string opening, string thread, string debrief,
                            ChapterDifficulty difficulty, string threat,
                            string gridPath = null, float cellSize = 2f, float cameraSize = 6f,
                            bool tutorial = false)
        {
            Scene = scene;
            Numeral = numeral;
            Title = title;
            Subtitle = subtitle;
            Opening = opening;
            Thread = thread;
            Debrief = debrief;
            Difficulty = difficulty;
            Threat = threat;
            GridPath = gridPath;
            CellSize = cellSize;
            CameraSize = cameraSize;
            Tutorial = tutorial;
        }

        /// <summary>The PlayMode scene name this chapter is loaded from.</summary>
        public string Scene { get; }

        /// <summary>I, II, III … — the chapter's number as the page writes it.</summary>
        public string Numeral { get; }

        /// <summary>Lower-case, like every other line of copy in this game. "the maze".</summary>
        public string Title { get; }

        /// <summary>What the room is, before it has been walked.</summary>
        public string Subtitle { get; }

        /// <summary>
        /// The first line the HUD whispers after the chapter title has faded. It is the only place the
        /// story is told while the player can still move, which is why it is one sentence and not a page.
        /// </summary>
        public string Opening { get; }

        /// <summary>The sentence printed under this chapter's node: how this room leads to the next one.</summary>
        public string Thread { get; }

        /// <summary>What the chapter left in the player's hands. Shown on a node that has been completed.</summary>
        public string Debrief { get; }

        public ChapterDifficulty Difficulty { get; }

        /// <summary>One line naming what shares the room and why it is dangerous. Empty for an empty room.</summary>
        public string Threat { get; }

        /// <summary>The ASCII level file, or empty for a chapter with its own scene builder (the corridor).</summary>
        public string GridPath { get; }

        public float CellSize { get; }

        /// <summary>Orthographic size of the follow camera. Smaller is closer, and the tunnels read bigger.</summary>
        public float CameraSize { get; }

        /// <summary>True for the room that teaches the controls on screen.</summary>
        public bool Tutorial { get; }

        public bool HasGrid => !string.IsNullOrEmpty(GridPath);

        /// <summary>The word the page prints beside the numeral: "easy", "medium", "brutal".</summary>
        public string Grade
        {
            get
            {
                switch (Difficulty)
                {
                    case ChapterDifficulty.Tutorial: return "tutorial";
                    case ChapterDifficulty.Easy: return "easy";
                    case ChapterDifficulty.Medium: return "medium";
                    case ChapterDifficulty.Hard: return "hard";
                    default: return "brutal";
                }
            }
        }

        /// <summary>The chapter title as the HUD prints it, e.g. "chapter iii  ·  the flooded wing".</summary>
        public string HudTitle => "chapter " + Numeral.ToLowerInvariant() + "  ·  " + Title;
    }

    /// <summary>
    /// The whole story in one place: ten chapters from the enemy-free maze to the sump.
    ///
    /// This is the file to edit when a level is added. One entry carries the prose, the difficulty, the
    /// grid file and the camera; the boot scene, the build list, the flow's chapter list, the story page and
    /// the chapter's own scene are all derived from it by the editor bootstrap. Nothing else in the project
    /// needs to know a chapter exists.
    /// </summary>
    public static class ChapterCatalog
    {
        /// <summary>The room the game starts in. Referenced by the tests and by nothing else.</summary>
        public const string MazeScene = "Level01_Maze";

        /// <summary>Chapter two is the hand-built corridor, so it lives here without a grid.</summary>
        public const string CorridorScene = "Level02_Corridor";

        static readonly ChapterStory[] chapters =
        {
            new ChapterStory(
                "Level01_Maze", "I", "the maze", "a room that teaches your voice",
                "you wake in a black room with no memory of the door. the walls will answer, if you ask them",
                "the exit was not outside. it was a corridor, and something in there heard the door open",
                "the room taught you its language. the corridor has already heard it",
                ChapterDifficulty.Tutorial, "nothing in here reacts to you. only the floor does",
                "Assets/Data/Levels/Level01_Maze.txt", tutorial: true),

            new ChapterStory(
                CorridorScene, "II", "the corridor", "something in it has learned your shape",
                "narrower than the maze, and you are not alone in it. close what you open, and listen before you move",
                "the corridor ends at a bulkhead. under it, the water is louder than your voice",
                "you came out the far side of it. below, the sump is still breathing",
                ChapterDifficulty.Easy, "a crawler walks the corridor: slower than you, and it cannot open a door"),

            new ChapterStory(
                "Level03_FloodedWing", "III", "the flooded wing", "ankles deep, and every step rings",
                "the bulkhead was not locked. behind it the wing is drowned to the knee",
                "the water drains toward a stairwell, and the stairwell only goes down",
                "you keep the water behind you. the stairwell is another kind of dark",
                ChapterDifficulty.Easy, "nothing hunts here yet. the glass under the water announces you",
                "Assets/Data/Levels/Level03_FloodedWing.txt"),

            new ChapterStory(
                "Level04_ListeningRoom", "IV", "the listening room", "something counts your steps",
                "the stairs end in a room with one door and no other way out",
                "past the listening room the coal chute is the only way down",
                "it stopped where you stopped, and then it walked to where you had been",
                ChapterDifficulty.Medium, "a crawler patrols the main artery. doors hold it, hollows hide you",
                "Assets/Data/Levels/Level04_ListeningRoom.txt"),

            new ChapterStory(
                "Level05_SumpStairs", "V", "the sump stairs", "two doors and a floor of glass",
                "the chute drops you into a stairwell that only goes one way: down",
                "the stairs flood out into a boiler run, and the boiler is still warm",
                "two doors spent, and the boiler room still ahead of you",
                ChapterDifficulty.Medium, "a crawler, and more glass than you have silent steps for",
                "Assets/Data/Levels/Level05_SumpStairs.txt"),

            new ChapterStory(
                "Level06_BoilerRun", "VI", "the boiler run", "the pipes carry a sound further than you do",
                "the boiler has not been off for years. every pipe in the run is warm and loud",
                "the run ends at a dry well that should not exist this far under water",
                "the pipes are behind you. they are still carrying your footsteps",
                ChapterDifficulty.Medium, "a crawler on a long patrol, and a glass run on the way out",
                "Assets/Data/Levels/Level06_BoilerRun.txt"),

            new ChapterStory(
                "Level07_DryWell", "VII", "the dry well", "a well with no water and too much echo",
                "the well is dry, and the echo comes back wrong: twice, from two places",
                "the well's ladder ends against a cold store door",
                "you have learned which of the two echoes is the room and which is not",
                ChapterDifficulty.Hard, "a crawler, and hollows that only hide a quiet step",
                "Assets/Data/Levels/Level07_DryWell.txt"),

            new ChapterStory(
                "Level08_ColdStore", "VIII", "the cold store", "three doors, one of them yours",
                "the cold store is a grid of racks and three doors, and the one behind you is shut",
                "the store's far wall is a floodgate, and the gate is not holding",
                "three doors, and you still had to choose which two to spend",
                ChapterDifficulty.Hard, "a crawler among racks where nothing echoes twice the same way",
                "Assets/Data/Levels/Level08_ColdStore.txt"),

            new ChapterStory(
                "Level09_Floodgate", "IX", "the floodgate", "the gate is holding. barely",
                "the gate is holding a wall of water, and the room over it is one long glass floor",
                "behind the gate the sump opens up, and the water stops being a floor",
                "the gate holds behind you. the sump does not",
                ChapterDifficulty.Hard, "a crawler, a glass floor, and one sanctuary on the far side of it",
                "Assets/Data/Levels/Level09_Floodgate.txt"),

            new ChapterStory(
                "Level10_Sump", "X", "the sump", "the last room, and one light you did not bring",
                "the sump is the bottom of the facility. something down here is lit that you did not carry in",
                "the light is the way out. the facility was built to listen, and it has been listening to you",
                "you were not the first voice in here. the facility kept every one of them",
                ChapterDifficulty.Brutal, "a crawler in the sump, one sanctuary, and the longest walk in the game",
                "Assets/Data/Levels/Level10_Sump.txt"),
        };

        /// <summary>Every chapter, in story order.</summary>
        public static ChapterStory[] All => chapters;

        public static int Count => chapters.Length;

        /// <summary>
        /// The chapter at a position in the flow. A scene the catalogue does not know still gets a node: it
        /// is named after its scene rather than left blank, so a chapter added to the flow and not to this
        /// file is visible as a missing paragraph instead of an empty row.
        /// </summary>
        public static ChapterStory At(int index, string sceneName = null)
        {
            if (index >= 0 && index < chapters.Length)
            {
                return chapters[index];
            }

            string scene = string.IsNullOrEmpty(sceneName) ? "chapter " + (index + 1) : sceneName;
            return new ChapterStory(scene, (index + 1).ToString(), scene, "no words written for this room yet",
                                    "", "", "", ChapterDifficulty.Medium, "");
        }

        /// <summary>The chapter whose scene is <paramref name="sceneName"/>, or the unnamed fallback.</summary>
        public static bool ForScene(string sceneName, out ChapterStory story)
        {
            foreach (ChapterStory candidate in chapters)
            {
                if (candidate.Scene == sceneName)
                {
                    story = candidate;
                    return true;
                }
            }

            story = default;
            return false;
        }

        /// <summary>
        /// The line a chapter opens with, or empty for a scene the catalogue does not know. The opening
        /// caption reads this so the level does not carry a second copy of its own introduction.
        /// </summary>
        public static string OpeningForScene(string sceneName) =>
            ForScene(sceneName, out ChapterStory story) ? story.Opening : string.Empty;

        /// <summary>The numeral for a 0-based chapter position, falling back to the plain number.</summary>
        public static string Numeral(int index, string sceneName = null) => At(index, sceneName).Numeral;

        /// <summary>The run's order, which is the catalogue's order: one list, not two that agree by luck.</summary>
        public static string[] SceneNames
        {
            get
            {
                var names = new string[chapters.Length];
                for (int i = 0; i < chapters.Length; i++)
                {
                    names[i] = chapters[i].Scene;
                }

                return names;
            }
        }

        /// <summary>What the last room leaves behind. The story page prints it once everything is walked.</summary>
        public const string FinalWord = "the exit is a door. the story ends on the other side of it.";
    }
}
