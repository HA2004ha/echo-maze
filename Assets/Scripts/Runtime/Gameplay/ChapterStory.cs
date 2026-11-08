using System;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// One chapter as the story tells it: a numeral, a name, the sentence that joins it to the next room,
    /// the line whispered when it opens, and the line it leaves behind once it has been survived.
    ///
    /// The level list is <see cref="GameFlow.chapterScenes"/> — what the build actually contains and in what
    /// order. This is the other half of the same list: the words. Keeping them apart is what lets the level
    /// select screen be about the story without the flow having to know any prose.
    /// </summary>
    public readonly struct ChapterStory
    {
        public ChapterStory(string scene, string numeral, string title, string subtitle,
                            string opening, string thread, string debrief)
        {
            Scene = scene;
            Numeral = numeral;
            Title = title;
            Subtitle = subtitle;
            Opening = opening;
            Thread = thread;
            Debrief = debrief;
        }

        /// <summary>The PlayMode scene name this chapter is loaded from.</summary>
        public string Scene { get; }

        /// <summary>I, II, III — the chapter's number as the page writes it.</summary>
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

        public bool IsNamed => !string.IsNullOrEmpty(Scene);
    }

    /// <summary>
    /// The prose for every chapter in the build, in the flow's order, plus the one node at the end of the
    /// thread that the build does not contain yet.
    ///
    /// The page is written from here rather than from the scene so a level that is added later gets its
    /// node by adding one entry, and so a chapter can be reworded without opening an editor.
    /// </summary>
    public static class ChapterCatalog
    {
        /// <summary>Chapter one is the GDD's enemy-free tutorial: the room that teaches the language.</summary>
        public const string MazeScene = "Level01_Maze";

        /// <summary>Chapter two is the corridor with the Crawler in it.</summary>
        public const string CorridorScene = "Level02_Corridor";

        static readonly ChapterStory[] Chapters =
        {
            new ChapterStory(
                MazeScene,
                "I",
                "the maze",
                "a room that teaches your voice",
                "you wake in a black room with no memory of the door. the walls will answer, if you ask them",
                "the exit was not outside. it was a corridor, and something in there heard the door open",
                "the room taught you its language. the corridor has already heard it"),

            new ChapterStory(
                CorridorScene,
                "II",
                "the corridor",
                "something in it has learned your shape",
                "narrower than the maze, and you are not alone in it. close what you open, and listen before you move",
                "the corridor ends at a bulkhead. under it, the water is louder than your voice",
                "you came out the far side of it. below, the sump is still breathing")
        };

        /// <summary>
        /// The unexplained third node: the GDD's finale, which this build does not contain. It is drawn as
        /// part of the thread so the story visibly has somewhere to go, and it is never a button that plays
        /// a room that does not exist.
        /// </summary>
        public const string EpilogueNumeral = "III";
        public const string EpilogueTitle = "the sump";
        public const string EpilogueSubtitle = "not excavated in this build";
        public const string EpilogueRecord = "the water is still rising";

        public static int Count => Chapters.Length;

        /// <summary>
        /// The chapter at a position in the flow. A scene the catalogue does not know still gets a node: it
        /// is named after its scene rather than left blank, so a chapter added to the flow and not to this
        /// file is visible as a missing paragraph instead of an empty row.
        /// </summary>
        public static ChapterStory At(int index, string sceneName = null)
        {
            if (index >= 0 && index < Chapters.Length)
            {
                return Chapters[index];
            }

            string scene = string.IsNullOrEmpty(sceneName) ? "chapter " + (index + 1) : sceneName;
            return new ChapterStory(scene, (index + 1).ToString(), scene, "no words written for this room yet",
                                    "", "", "");
        }

        /// <summary>The chapter whose scene is <paramref name="sceneName"/>, or the unnamed fallback.</summary>
        public static bool ForScene(string sceneName, out ChapterStory story)
        {
            foreach (ChapterStory candidate in Chapters)
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

        internal static readonly string[] SceneNames = Array.ConvertAll(Chapters, chapter => chapter.Scene);
    }
}
