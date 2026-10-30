using System.Collections.Generic;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// A hollow: a pocket in the wall where the player is nearly unhearable (GDD). A quiet sound made inside
    /// one — footsteps, the F pulse, a bump — carries only <c>hollowHearingMultiplier</c> of its normal
    /// radius, so the creature walking past does not turn. A loud one (<c>hollowBreaksAbove</c>: a ping, a
    /// shout) is heard at full radius, because pinging from cover is what breaks it. An enemy that is
    /// already chasing ignores the hollow altogether: it is a place to hide *before* you are found.
    ///
    /// Draws nothing, like every other volume: the wave shows the pocket as a notch in the wall and the HUD
    /// says "You are hidden" / "You are exposed" on the edges, which is the only cue there is.
    ///
    /// Containment is a box test on the player's position, the same way <see cref="Sanctuary"/> does it,
    /// and the registry is static so the Crawler's hearing can ask "was that sound made in cover?" without
    /// a reference. That query is the week-2 stub's: NoiseBoard / HeardBy replace it in week 3 and take
    /// <see cref="HearingScale"/> with them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Hollow : MonoBehaviour
    {
        public const string HiddenNotice = "You are hidden";
        public const string ExposedNotice = "You are exposed";

        static readonly List<Hollow> Active = new List<Hollow>();

        [SerializeField] Player player;
        [SerializeField] LevelSession session;
        [Tooltip("Centre of the pocket in world units.")]
        [SerializeField] Vector2 centre;
        [Tooltip("Size of the pocket. Kept to the inside of the alcove, so that standing in the doorway " +
                 "of the room is not cover.")]
        [SerializeField] Vector2 size = new Vector2(1.2f, 1.2f);

        Bounds area;
        bool inside;

        void OnEnable()
        {
            area = new Bounds(centre, size);
            Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
            inside = false;
        }

        void Update()
        {
            if (player == null)
            {
                return;
            }

            // A dead body is in no cover, and the retry puts it back in the hollow's sight: forgetting here
            // is what makes a respawn inside the alcove say "hidden" again.
            if (!player.IsAlive)
            {
                inside = false;
                return;
            }

            bool nowInside = area.Contains(player.Position);
            if (nowInside != inside && session != null)
            {
                session.Notice(nowInside ? HiddenNotice : ExposedNotice);
            }

            inside = nowInside;
        }

        public bool Contains(Vector2 position) => area.Contains(position);

        /// <summary>True when a noise at <paramref name="position"/> was made inside any hollow.</summary>
        public static bool AnyContains(Vector2 position)
        {
            foreach (Hollow hollow in Active)
            {
                if (hollow.area.Contains(position))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The factor on an enemy's hearing radius. Pure, so the whole rule is one table in a test: cover
        /// only helps against a quiet sound and only against an enemy that is not already chasing.
        /// </summary>
        public static float HearingScale(bool madeInHollow, float loudness, bool enemyChasing,
                                         EchoTuningProfile profile) =>
            madeInHollow && !enemyChasing && loudness <= profile.hollowBreaksAbove
                ? profile.hollowHearingMultiplier
                : 1f;
    }
}
