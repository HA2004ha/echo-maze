using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The way out: the object that ends a chapter. It is a thing you stand next to and touch rather than a
    /// line you cross, because the last metres of a level are the ones where the player is closest to being
    /// heard, and an exit that fires on contact turns that tension into a footrace.
    ///
    /// Nothing is drawn for it. The GDD's rule is that the player finds the room by sound, and an exit
    /// rendered as a lit doorway is a shortcut past the mechanic the level exists to teach — so the exit is
    /// a probe volume and the wave's own geometry (a gap in the far wall) is what shows it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelExit : MonoBehaviour, IInteractable
    {
        LevelSession session;

        [Tooltip("The prompt E answers. Persian and English both belong to the team's copy pass; this is " +
                 "the string the HUD prints, not a key.")]
        [SerializeField] string prompt = "E   OUT";

        public string Prompt => prompt;

        public void Build(LevelSession owningSession)
        {
            session = owningSession;
        }

        public bool CanInteract(Player player) => session != null && !session.IsComplete;

        public void Interact(Player player) => session.Complete();
    }
}
