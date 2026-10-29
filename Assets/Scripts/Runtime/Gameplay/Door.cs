using EchoMaze.Core;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// A door: two blockers with a gap between them, which is exactly the signature the GDD asks for
    /// ("two line fragments with a gap"), produced by the ray pipeline with no door-specific drawing code.
    ///
    /// Closed, it is a wall to the rays, to the body and — from week 4's NoiseBoard — to hearing. Open, its
    /// colliders are switched off, so the wave measures straight through the doorway and the pocket of the
    /// next room arrives on the next ping. Nothing is drawn for either state, because the only thing the
    /// player is meant to see is the difference in the wave.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Door : MonoBehaviour, IInteractable
    {
        EchoDirector director;
        Collider2D[] blockers;
        bool startClosed = true;

        public bool IsClosed { get; private set; }

        /// <summary>Set by whoever builds the door — the maze builder at runtime, or a test rig.</summary>
        public void Build(EchoDirector owningDirector, Collider2D[] owningBlockers, bool closedAtStart)
        {
            director = owningDirector;
            blockers = owningBlockers;
            startClosed = closedAtStart;
            IsClosed = closedAtStart;
            ApplyBlocks();
        }

        public string Prompt => IsClosed ? "E   OPEN THE DOOR" : "E   CLOSE THE DOOR";

        public bool CanInteract(Player player) => blockers != null && blockers.Length > 0;

        public void Interact(Player player)
        {
            IsClosed = !IsClosed;
            ApplyBlocks();

            // Opening and closing are different noises in the GDD's table (low / medium): the first is the
            // wake trigger of every chase space, the second is the move the player makes while being hunted.
            EchoTuningProfile profile = director != null ? director.Profile : null;
            if (director != null && profile != null)
            {
                Vector2 here = transform.position;
                director.ReportNoise(here, IsClosed ? profile.doorCloseLoudness : profile.doorOpenLoudness);
            }
        }

        /// <summary>
        /// Back to the state the map authored. Called on a checkpoint restart, because a door the player
        /// opened on the run that got them killed has to be shut again: an open door is the one piece of
        /// information a corpse cannot have given away, and leaving it open lets a retry win the room with
        /// the noise it paid for the first time.
        /// </summary>
        public void ResetToStart()
        {
            IsClosed = startClosed;
            ApplyBlocks();
        }

        void ApplyBlocks()
        {
            if (blockers == null)
            {
                return;
            }

            foreach (Collider2D blocker in blockers)
            {
                if (blocker != null)
                {
                    blocker.enabled = IsClosed;
                }
            }
        }
    }
}
