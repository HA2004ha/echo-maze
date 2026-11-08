using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The chapter list and the run's totals: what order the levels come in, what carries over between them,
    /// and when the game is over. This is the difference between a tech demo with a wave in it and a game
    /// with a beginning, a middle and an end.
    ///
    /// It survives scene loads with <c>dontDestroyOnLoad</c>, which is also why it is the only class here
    /// that is looked up rather than wired: a scene cannot hold a serialized reference to an object that
    /// lives in another scene, so the menu and the HUD reach the flow through <see cref="Active"/> and
    /// nothing else does.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameFlow : MonoBehaviour
    {
        public static GameFlow Active { get; private set; }

        [Tooltip("PlayMode scene names, in the order they are played. Chapter one is the maze: the GDD's " +
                 "enemy-free tutorial and, for this milestone, the whole run. The corridor is built and " +
                 "playable from the editor, but it joins this list when the hunt systems land, so finishing " +
                 "the maze is a victory instead of a corridor with no ending.")]
        [SerializeField] string[] chapterScenes = { "Level01_Maze", "Level02_Corridor" };
        [SerializeField] string menuScene = "Boot";

        int chapterIndex = -1;
        bool hooked;
        int pings;
        int deaths;
        float seconds;

        /// <summary>Raised after a chapter scene is live and its session is bound, with the 1-based chapter number.</summary>
        public event Action<int> ChapterStarted;

        /// <summary>Raised after the last chapter's map is dismissed.</summary>
        public event Action RunCompleted;

        public int ChapterCount => chapterScenes.Length;
        public int ChapterIndex => chapterIndex;
        public int TotalPings => pings;
        public int TotalDeaths => deaths;
        public float TotalSeconds => seconds;
        public bool IsRunning => chapterIndex >= 0;

        public string ChapterName(int index) =>
            index >= 0 && index < chapterScenes.Length ? chapterScenes[index] : "";

        /// <summary>How many chapters of this run have already been survived, from the saved progress.</summary>
        public int ChaptersWalked
        {
            get
            {
                int walked = 0;
                for (int i = 0; i < chapterScenes.Length; i++)
                {
                    if (IsChapterComplete(i))
                    {
                        walked++;
                    }
                }

                return walked;
            }
        }

        public bool IsChapterComplete(int index) =>
            index >= 0 && index < chapterScenes.Length &&
            ChapterProgress.IsComplete(chapterScenes[index]);

        /// <summary>
        /// Chapter one is always open; every later room opens when the one before it has been survived. The
        /// rule lives here and not on the page because it is the order of the story, and the order of the
        /// story is this class's whole job.
        /// </summary>
        public bool IsUnlocked(int index) =>
            index >= 0 && index < chapterScenes.Length && (index == 0 || IsChapterComplete(index - 1));

        /// <summary>
        /// Where a player who presses continue belongs: the first room they have opened and not yet walked,
        /// or — with the whole story walked — the last room again, because replaying is the only thing left
        /// to offer and starting from chapter one is not what a returning player meant.
        /// </summary>
        public int ContinueIndex
        {
            get
            {
                for (int i = 0; i < chapterScenes.Length; i++)
                {
                    if (IsUnlocked(i) && !IsChapterComplete(i))
                    {
                        return i;
                    }
                }

                return chapterScenes.Length - 1;
            }
        }

        void Awake()
        {
            if (Active != null && Active != this)
            {
                // A second flow means the boot scene was loaded on top of itself; the older one is the one
                // that already has the run's totals, so this duplicate goes away rather than competing.
                Destroy(gameObject);
                return;
            }

            Active = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
                Unhook();
            }
        }

        /// <summary>From the menu: zero the counters and put the player in chapter one.</summary>
        public void StartRun() => StartChapter(0);

        /// <summary>
        /// A run that begins at a chosen room — the chapter select's whole purpose. The totals are zeroed
        /// as they are at chapter one: a run entered halfway still ends when the story does, and carrying
        /// another run's waves into it would make the summary a lie.
        /// </summary>
        public void StartChapter(int index)
        {
            if (index < 0 || index >= chapterScenes.Length || !IsUnlocked(index))
            {
                return;
            }

            pings = 0;
            deaths = 0;
            seconds = 0f;
            LoadChapter(index);
        }

        /// <summary>The menu's continue: the furthest room the player has earned, not the first one.</summary>
        public void ContinueRun() => StartChapter(ContinueIndex);

        public void LoadChapter(int index)
        {
            if (index < 0 || index >= chapterScenes.Length)
            {
                return;
            }

            chapterIndex = index;

            // Restoring the clock here is not cosmetic: a chapter completed from a paused session, or a run
            // abandoned from the pause panel, would otherwise load the next room with timeScale still 0.
            Time.timeScale = 1f;
            Hook();
            SceneManager.LoadScene(chapterScenes[index], LoadSceneMode.Single);
        }

        void Hook()
        {
            // Guarded, because LoadChapter runs once per chapter: an unguarded += would call OnSceneLoaded
            // twice on the second room, bind the same session twice and advance two chapters per exit.
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }

        void Unhook()
        {
            if (hooked)
            {
                hooked = false;
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        /// <summary>Back to the menu, and the run's totals go with it: a run you left is a run you did not finish.</summary>
        public void ReturnToMenu()
        {
            chapterIndex = -1;
            Time.timeScale = 1f;
            Unhook();
            SceneManager.LoadScene(menuScene, LoadSceneMode.Single);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LevelSession session = FindAnyObjectByType<LevelSession>();
            if (session == null)
            {
                // The menu and any scene without a level in it. Leaving the hook in place would fire again
                // on the next load with a session that is not the current chapter's.
                return;
            }

            session.Completed += OnChapterComplete;
            ChapterStarted?.Invoke(chapterIndex + 1);
        }

        void OnChapterComplete()
        {
            LevelSession session = FindAnyObjectByType<LevelSession>();
            if (session != null)
            {
                pings += session.PingCount;
                deaths += session.DeathCount;
                seconds += session.ElapsedSeconds;

                // The chapter's own numbers, not the run's: the node on the story page is about this room,
                // and a best record that grew with every later chapter would be a record of nothing.
                ChapterProgress.MarkComplete(ChapterName(chapterIndex), session.PingCount,
                                             session.DeathCount, session.ElapsedSeconds);
            }
        }

        public void ContinueAfterVictory()
        {
            LevelSession session = FindAnyObjectByType<LevelSession>();
            if (!IsRunning || session == null || !session.IsComplete)
            {
                return;
            }

            if (chapterIndex + 1 >= chapterScenes.Length)
            {
                chapterIndex = -1;
                Unhook();
                RunCompleted?.Invoke();
                return;
            }

            LoadChapter(chapterIndex + 1);
        }
    }
}
