using UnityEngine;

namespace EchoMaze.UI
{
    /// <summary>
    /// The arithmetic behind the aim cursor, kept out of the MonoBehaviour for the same reason
    /// <c>PlayerFacing</c> is: a rule you can put in a table is a rule you can argue about with a number
    /// instead of with a screenshot. Nothing here reads a device, the clock, or a transform.
    /// </summary>
    public static class AimCursorMath
    {
        /// <summary>
        /// Rec.709 luminance of the project's interface cyan (0.498, 0.914, 0.941) — the colour every
        /// interface element in the game uses, from <c>arcColor</c> to <c>markColor</c> to the death label.
        /// Over the pure black the game renders, a straight-alpha sprite lands at exactly this times its
        /// alpha, which is what makes the alpha the brightness control rather than a style choice.
        /// </summary>
        public const float CyanLuminance = 0.827f;

        /// <summary>
        /// Bloom's threshold as the scene builder sets it (<c>SceneKit.EnsureVolumeProfile</c>). Measured
        /// today it is inert — the committed volume profile holds no Bloom component — so this is the number
        /// the interface has to stay under when the profile is rebuilt, not a live effect.
        /// </summary>
        public const float BloomThreshold = 0.55f;

        /// <summary>
        /// The brightest alpha an interface element may have without becoming a light source, which
        /// <c>Docs/QA_CHECKLIST.md</c> and the GDD's art direction both forbid ("The HUD uses the same thin
        /// lines but dimmer than the waves, so it never becomes a visible light source itself"). Not a
        /// preference: anything above it crosses Bloom and the reticle starts glowing, and a glowing reticle
        /// is a second sun in a game whose only light is sound.
        /// </summary>
        public const float AlphaCeiling = BloomThreshold / CyanLuminance;

        /// <summary>
        /// The pointer-to-body separation below which the aim stops being a measurement, which is
        /// <c>PlayerVisual</c>'s noise radius. Duplicated deliberately and pinned equal by a test, the same
        /// treatment <c>PlayerFacing.MoveDeadzoneSqr</c> gets against the footstep threshold: two components
        /// that have to agree on one number, with the agreement asserted rather than refactored.
        /// </summary>
        public const float ParkedRadiusDefault = 0.27f;

        /// <summary>
        /// True when the cursor is parked on the character's own body. Before the first pointer sample the
        /// distance is <see cref="float.PositiveInfinity"/>, which is not parked: there is no aim at all yet,
        /// and the reticle has nothing to apologise for.
        /// </summary>
        public static bool IsParked(float aimDistance, float parkedRadius)
        {
            return aimDistance <= parkedRadius;
        }

        /// <summary>
        /// How far a manual-echo hold has run, 0 to 1. Negative seconds means no hold is in progress, and a
        /// zero or missing charge time returns 0 rather than dividing: an untuned profile must not make the
        /// reticle flash at full brightness. This is the same normalisation
        /// <c>ArcMath.ManualLoudness</c> applies, so the head peaks on the frame the shout does.
        /// </summary>
        public static float ChargeProgress(float chargeSeconds, float chargeTime)
        {
            if (chargeSeconds < 0f || chargeTime <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(chargeSeconds / chargeTime);
        }

        /// <summary>
        /// Which alpha the direction head gets this frame: lifted toward the ceiling while a charge is
        /// building, slack when the cursor is parked on the body, steady otherwise.
        ///
        /// A charge wins over a park, in both readings. An intentional hold is a louder statement than the
        /// dead-zone hint, and a player who starts charging without moving the cursor off their own body is
        /// about to fire along an angle the character is not showing, which is exactly when the head should
        /// be brightest rather than dimmest.
        /// </summary>
        public static float HeadAlpha(float chargeProgress, bool parked,
                                     float idleAlpha, float parkedAlpha, float chargedAlpha)
        {
            if (chargeProgress > 0f)
            {
                return Mathf.Lerp(idleAlpha, chargedAlpha, chargeProgress);
            }

            return parked ? parkedAlpha : idleAlpha;
        }
    }
}
