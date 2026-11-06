using System.Text;
using EchoMaze.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// The end of a run: the three numbers the game has been keeping score of — waves spent, times heard,
    /// and how long the player spent in the dark — and one button that goes back to the menu.
    ///
    /// It reads the totals from <see cref="GameFlow"/>, which is the only object that survives the scene the
    /// last exit was reached in, and the scene's own session is already paused by that exit. Asking the flow
    /// is the difference between the panel printing the run and printing the last room.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VictoryPanel : MonoBehaviour
    {
        [SerializeField] GameObject content;
        [SerializeField] UIPanelFade fade;
        [SerializeField] UITypewriter typewriter;
        [SerializeField] Text summaryLabel;
        [SerializeField] Button menuButton;

        void Awake()
        {
            if (menuButton != null)
            {
                menuButton.onClick.AddListener(() => GameFlow.Active?.ReturnToMenu());
            }

            if (GameFlow.Active != null)
            {
                GameFlow.Active.RunCompleted += Show;
            }

            if (fade != null)
            {
                fade.HideImmediate();
            }
            else if (content != null)
            {
                content.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (GameFlow.Active != null)
            {
                GameFlow.Active.RunCompleted -= Show;
            }
        }

        void Show()
        {
            GameFlow flow = GameFlow.Active;
            string text = null;
            if (summaryLabel != null && flow != null)
            {
                // The heading already says "YOU WERE NOT HEARD"; the summary is only the run's three numbers.
                var builder = new StringBuilder();
                builder.AppendLine(Count(flow.ChapterCount, "room") + ", " +
                                   Count(flow.TotalPings, "wave") + ", " +
                                   Count(flow.TotalDeaths, "time") + " heard");
                builder.Append(FormatTime(flow.TotalSeconds));
                text = builder.ToString();
            }

            if (fade != null)
            {
                fade.Show();
            }
            else if (content != null)
            {
                content.SetActive(true);
            }

            // The page goes up first and the numbers arrive under it: a panel that fades in already
            // full of text says nothing happened, while one that types says the machine is still
            // counting what you just did.
            if (typewriter != null && text != null)
            {
                typewriter.Play(text);
            }
            else if (text != null)
            {
                summaryLabel.text = text;
            }
        }

        /// <summary>One number and its noun, with the plural only where English wants one.</summary>
        static string Count(int value, string noun) =>
            value + " " + noun + (value == 1 ? "" : "s");

        /// <summary>
        /// Minutes and seconds, because a run measured in <c>float</c> seconds to one decimal place is a
        /// number nobody can compare against another run in conversation.
        /// </summary>
        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (total / 60) + ":" + (total % 60).ToString("00");
        }
    }
}
