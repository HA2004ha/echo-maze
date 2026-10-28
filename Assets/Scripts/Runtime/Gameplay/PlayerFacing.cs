using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The four poses the player can be drawn in. The art is one still per pose, so the whole
    /// character system is a choice between these four and nothing else.
    /// </summary>
    public enum PlayerPose
    {
        Front,
        Back,
        ProfileLeft,
        ProfileRight
    }

    /// <summary>
    /// Which way the character looks, and where in a footstep it is. Kept free of MonoBehaviour and
    /// scene access like <c>ArcMath</c>, because both answers are the kind of thing that gets quietly
    /// broken by a later refactor and is miserable to debug by eye: a pose that picks the wrong sprite
    /// at 45 degrees, or a bounce that drifts out of sync with the sound the player can hear.
    /// </summary>
    public static class PlayerFacing
    {
        /// <summary>
        /// Below this squared magnitude the player counts as standing still, so the aim decides the pose
        /// instead of the velocity. This is the same number <c>Player.UpdateFootsteps</c> uses to decide
        /// whether the step timer runs at all: standing still is defined once, and the test
        /// <c>TheMoveDeadzone_IsTheFootstepThreshold</c> pins the two together rather than making
        /// <c>Player</c> publish a constant it does not need.
        /// </summary>
        public const float MoveDeadzoneSqr = 0.01f;

        /// <summary>
        /// Movement wins while the player is actually walking; the aim only decides when they are not.
        ///
        /// Four poses over eight directions means the diagonals have to belong to somebody, and they
        /// go to the vertical: WASD gives exactly |vx| = |vy|, so a tie is the common case rather than a
        /// corner of the number line, and "walking away from the camera" is the pose that carries the
        /// most intent — entering danger or retreating from it. The epsilon keeps that decision
        /// deterministic instead of leaving it to the last bits of a normalised vector.
        /// </summary>
        public static PlayerPose Resolve(Vector2 move, float aimRad)
        {
            Vector2 dir = move.sqrMagnitude >= MoveDeadzoneSqr
                ? move
                : new Vector2(Mathf.Cos(aimRad), Mathf.Sin(aimRad));

            return ResolveDirection(dir);
        }

        /// <summary>
        /// The same sector map, from a direction instead of from the movement/aim choice. <c>PlayerVisual</c>
        /// needs it separated because the sticky layer decides *which vector* to ask about (a cursor parked
        /// on the body is not an aim, and a pose held through a stop is not a new answer) and then applies
        /// the band around the answer it already has.
        /// </summary>
        public static PlayerPose ResolveDirection(Vector2 dir)
        {
            if (Mathf.Abs(dir.y) >= Mathf.Abs(dir.x) - 1e-6f)
            {
                return dir.y >= 0f ? PlayerPose.Back : PlayerPose.Front;
            }

            return dir.x > 0f ? PlayerPose.ProfileRight : PlayerPose.ProfileLeft;
        }

        /// <summary>
        /// The direction each pose is centred on, i.e. the heading it is most correct for. The four
        /// cardinals, because the sectors are quarters: <c>SectorHalfWidthDegrees</c> is the distance from
        /// here to the boundary where a neighbour takes over.
        ///
        /// This exists so that hysteresis can be written as an angle between two vectors rather than as
        /// arithmetic on an <c>Atan2</c> result, which would have to handle the wrap at ±π by hand.
        /// </summary>
        public static Vector2 PoseCentre(PlayerPose pose)
        {
            switch (pose)
            {
                case PlayerPose.Front: return new Vector2(0f, -1f);
                case PlayerPose.Back: return new Vector2(0f, 1f);
                case PlayerPose.ProfileLeft: return new Vector2(-1f, 0f);
                default: return new Vector2(1f, 0f);
            }
        }

        /// <summary>
        /// Half the width of a pose's slice of the compass: four poses over 360°. A heading this far plus
        /// the hysteresis band away from <c>PoseCentre</c> has properly left the pose it is in, and a
        /// neighbour is allowed.
        ///
        /// It is 45° only while there are four poses. Issue 2 in <c>Docs/ISSUES.md</c> widens the fan to
        /// eight, which makes this 22.5° and the hysteresis band that sits on top of it half as many
        /// degrees — the boundary count doubles, so the stickiness per boundary has to be halved to keep
        /// the same total dead angle.
        /// </summary>
        public const float SectorHalfWidthDegrees = 45f;

        /// <summary>
        /// Where in one footstep the player is: 0 the instant the step lands, rising to 1 as the next one
        /// comes round. Derived from <c>Player</c>'s own timer rather than a second clock, because that
        /// timer stops while the player stands still and fires immediately when they start again, and a
        /// bounce with its own phase would keep nodding through a stop-and-listen and miss the resume —
        /// the two moments the game is teaching something about noise.
        ///
        /// A stale timer (the player never walked, or <c>Revive</c> parked it at zero) reports 0, which
        /// is what leaves the character still instead of twitching at the room start.
        /// </summary>
        public static float StepPhase(float now, float nextStepTime, float period)
        {
            if (period <= 0f || nextStepTime <= now)
            {
                return 0f;
            }

            return 1f - Mathf.Clamp01((nextStepTime - now) / period);
        }
    }
}
