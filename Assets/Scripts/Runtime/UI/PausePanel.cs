using EchoMaze.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// The panel the game shows while it is holding its breath: resume, restart this chapter, back to the
    /// menu, quit. Esc is handled by <see cref="LevelSession"/>, which owns the clock; this is the half that
    /// has to be clickable, because a player who has just been heard may want to stop rather than continue.
    ///
    /// The component sits on the always-active canvas and switches a child. Deactivating the object that owns
    /// the subscription is the classic way a pause panel becomes a one-way door: the resume turns it off, the
    /// off unsubscribes it from the toggle, and the next Esc shows nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PausePanel : MonoBehaviour
    {
        [SerializeField] LevelSession session;
        [Tooltip("The visible part, switched with the pause. The component is not on it, on purpose.")]
        [SerializeField] GameObject content;
        [SerializeField] Button resumeButton;
        [SerializeField] Button restartButton;
        [SerializeField] Button menuButton;
        [SerializeField] Button quitButton;
        [SerializeField] Text chapterLabel;

        void Awake()
        {
            if (session != null)
            {
                session.PauseToggled += OnPauseToggled;
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(Resume);
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(RestartChapter);
            }

            if (menuButton != null)
            {
                menuButton.onClick.AddListener(ReturnToMenu);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(Quit);
            }

            if (content != null)
            {
                content.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (session != null)
            {
                session.PauseToggled -= OnPauseToggled;
            }
        }

        void OnPauseToggled(bool paused)
        {
            if (content == null)
            {
                return;
            }

            // A finished chapter also stops the clock, but the pause page is not the page for that: the
            // victory panel is. Without this guard the two panels are printed on top of each other, which
            // is exactly what the first capture of the victory screen showed.
            bool visible = paused && (session == null || !session.IsComplete);
            content.SetActive(visible);
            if (visible && chapterLabel != null && session != null)
            {
                chapterLabel.text = session.ChapterTitle;
            }
        }

        public void Resume()
        {
            if (session != null)
            {
                session.SetPaused(false);
            }
        }

        /// <summary>
        /// Restarts the chapter, not the run. With one-hit death and a checkpoint per sanctuary, a player who
        /// wants out of a room they have been heard in wants this room again, and the count of how many times
        /// they have already had it is on the session they can see.
        /// </summary>
        public void RestartChapter()
        {
            Resume();
            GameFlow flow = GameFlow.Active;
            if (flow != null)
            {
                flow.LoadChapter(flow.ChapterIndex);
            }
        }

        public void ReturnToMenu()
        {
            Resume();
            GameFlow.Active?.ReturnToMenu();
        }

        public void Quit()
        {
            // Editor-wise this is a no-op that Unity logs, which is the honest behaviour: a team that tests
            // in the editor should not have a Quit button that quietly kills the editor session.
            Application.Quit();
        }
    }
}
