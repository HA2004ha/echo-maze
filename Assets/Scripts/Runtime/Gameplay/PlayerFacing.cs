using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The eight poses the player can be drawn in, four cardinals and four diagonals. The art is one still
    /// per pose, so the whole character system is a choice between these eight and nothing else — three of
    /// them are the same texture mirrored, which is why there are five files and eight poses.
    /// </summary>
    public enum PlayerPose
    {
        Front,
        Back,
        ProfileLeft,
        ProfileRight,
        DiagonalDownRight,
        DiagonalDownLeft,
        DiagonalUpRight,
        DiagonalUpLeft
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
        /// Eight poses over the eight directions a hand can offer means nothing has to be given away: the
        /// four cardinals are the four single keys and the four diagonals are the four two-key holds, which
        /// is what <c>ResolveDirection</c> is measured against.
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
        ///
        /// Written as a slope comparison rather than an <c>Atan2</c> and a modulo so that it is exactly
        /// scale-invariant and never has to meet the wrap at ±π. The two tests are <c>y <= x * tan(22.5)</c>
        /// and <c>x <= y * tan(22.5)</c>, which is eight equal 45° sectors centred on the compass.
        /// </summary>
        public static PlayerPose ResolveDirection(Vector2 dir)
        {
            float x = Mathf.Abs(dir.x);
            float y = Mathf.Abs(dir.y);

            if (y <= x * CardinalSlopeLimit)
            {
                return dir.x > 0f ? PlayerPose.ProfileRight : PlayerPose.ProfileLeft;
            }

            if (x <= y * CardinalSlopeLimit)
            {
                return dir.y > 0f ? PlayerPose.Back : PlayerPose.Front;
            }

            if (dir.y > 0f)
            {
                return dir.x > 0f ? PlayerPose.DiagonalUpRight : PlayerPose.DiagonalUpLeft;
            }

            return dir.x > 0f ? PlayerPose.DiagonalDownRight : PlayerPose.DiagonalDownLeft;
        }

        /// <summary>
        /// <c>tan(22.5°) = sqrt(2) − 1</c>: the slope of the line where a cardinal sector hands over to a
        /// diagonal one. A heading exactly on it stays with the cardinal, which is the pose whose file the
        /// artist drew straight-on rather than one that is only ever seen flipped.
        ///
        /// This is also the place where the old four-pose rule used to live. It compared <c>|y|</c> with
        /// <c>|x|</c> and gave the exact diagonal to the vertical, with a <c>1e-6f</c> epsilon to keep the
        /// tie deterministic, because a WASD diagonal *was* the boundary. With eight sectors the same input
        /// is the dead centre of a diagonal, so the tie the epsilon existed for no longer occurs, and the
        /// boundaries that remain are at 22.5° — a number no keyboard can produce.
        /// </summary>
        public const float CardinalSlopeLimit = 0.41421356f;

        /// <summary>
        /// The direction each pose is centred on, i.e. the heading it is most correct for. The four cardinals
        /// and the four diagonals, because the sectors are eighths: <c>SectorHalfWidthDegrees</c> is the
        /// distance from here to the boundary where a neighbour takes over.
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
                case PlayerPose.ProfileRight: return new Vector2(1f, 0f);
                case PlayerPose.DiagonalDownRight: return new Vector2(HalfDiagonal, -HalfDiagonal);
                case PlayerPose.DiagonalDownLeft: return new Vector2(-HalfDiagonal, -HalfDiagonal);
                case PlayerPose.DiagonalUpRight: return new Vector2(HalfDiagonal, HalfDiagonal);
                default: return new Vector2(-HalfDiagonal, HalfDiagonal);
            }
        }

        /// <summary><c>1 / sqrt(2)</c>, the component of a unit diagonal.</summary>
        const float HalfDiagonal = 0.70710678f;

        /// <summary>
        /// Half the width of a pose's slice of the compass: eight poses over 360°. A heading this far plus
        /// the hysteresis band away from <c>PoseCentre</c> has properly left the pose it is in, and a
        /// neighbour is allowed.
        ///
        /// It was 45° while the fan had four poses. <c>Docs/ISSUES.md</c> #2 widened it to eight, which
        /// halves this to 22.5° and halves the hysteresis band that sits on top of it — the boundary count
        /// doubles, so the stickiness per boundary has to halve to keep the same total dead angle.
        /// <c>TheSectorHalfWidth_IsWhatTheSectorsAreActuallyWorth</c> is what stops the two numbers from
        /// being re-tuned apart the next time the pose count moves.
        /// </summary>
        public const float SectorHalfWidthDegrees = 22.5f;

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
