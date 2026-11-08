using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The one line of story a chapter whispers once the chapter title has faded out: the sentence that
    /// carries the player from the room they just left into this one.
    ///
    /// It rides the session's own hint channel rather than a second text system, so it lands in the same
    /// place the tutorial's lines appear and fades the same way. Nothing here blocks, pauses or needs a click
    /// — the player is free to ping while it is still on screen, which is right for a game whose silence is
    /// a resource.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChapterOpening : MonoBehaviour
    {
        [SerializeField] LevelSession session;
        [Tooltip("Waited out so the line arrives after the HUD's chapter title has faded, not on top of it.")]
        [SerializeField] float delaySeconds = 4.2f;
        [SerializeField] float showSeconds = 7f;

        string line;
        float speakAt;

        void Awake()
        {
            line = ChapterCatalog.OpeningForScene(gameObject.scene.name);
            speakAt = Time.unscaledTime + delaySeconds;
        }

        void Update()
        {
            if (string.IsNullOrEmpty(line) || Time.unscaledTime < speakAt)
            {
                return;
            }

            speakAt = float.PositiveInfinity;
            if (session != null)
            {
                session.Hint(line, showSeconds);
            }
        }
    }
}
