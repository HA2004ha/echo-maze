using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// A sanctuary: the room that saves the run. Entering it moves the checkpoint, and the GDD's other two
    /// promises — enemies stop at the threshold, and the player can ping freely inside — are read by the
    /// enemy systems from here (week 4) rather than duplicated in this class.
    ///
    /// One-hit death with no checkpoint is the reason this exists at all: without it, dying at the far end
    /// of a maze sends the player back to the beginning of the thing they were trying to escape, which
    /// teaches nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Sanctuary : MonoBehaviour
    {
        Player player;
        LevelSession session;
        Bounds area;
        bool inside;

        public void Build(Player watched, LevelSession owningSession, Vector2 centre, Vector2 size)
        {
            player = watched;
            session = owningSession;
            area = new Bounds(centre, size);
        }

        void Update()
        {
            if (player == null || session == null || !player.IsAlive)
            {
                return;
            }

            bool nowInside = area.Contains(player.Position);
            if (nowInside && !inside)
            {
                session.SetCheckpoint(player.Position);
            }

            inside = nowInside;
        }

        public bool Contains(Vector2 position) => area.Contains(position);
    }
}
