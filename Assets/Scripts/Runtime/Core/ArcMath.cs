using System;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Core
{
    /// <summary>
    /// The arc math from the GDD, kept free of MonoBehaviour and scene access so EditMode tests
    /// can pin the behaviour that makes the wave readable.
    ///
    /// r_k(theta) = R_k + A_k * (d(theta) - d_min), with R_k = R_0 - waveSpeed * age.
    /// </summary>
    public static class ArcMath
    {
        public static float PingRadius(float loudness, EchoTuningProfile profile)
        {
            float t = Mathf.Pow(Mathf.Clamp01(loudness), profile.radiusLoudnessExponent);
            return Mathf.Lerp(profile.minRadius, profile.maxRadius, t);
        }

        /// <summary>
        /// How far a noise carries: the GDD's H = rho * R(L), i.e. the same loudness that sets the visible
        /// reach also sets the radius an enemy can hear it from, scaled by one knob. Walls, closed doors,
        /// noise zones and the per-enemy hearing factor are week 3 (NoiseBoard + HeardBy); this straight
        /// line is what the week 2 Crawler stub reacts to, kept here so it can be tested without a scene.
        /// </summary>
        public static float HearingRadius(float loudness, EchoTuningProfile profile) =>
            profile.rho * PingRadius(loudness, profile);

        /// <summary>
        /// The charge curve of the manual echo: a quick tap gives manualEchoLoudness, holding Space for
        /// manualChargeSeconds gives a full shout. Pure, so the cost of a panic tap is a number in a test
        /// rather than something only a playtest can notice.
        /// </summary>
        public static float ManualLoudness(float heldSeconds, EchoTuningProfile profile) =>
            Mathf.Clamp01(Mathf.Lerp(profile.manualEchoLoudness, 1f,
                       Mathf.Clamp01(heldSeconds / Mathf.Max(0.01f, profile.manualChargeSeconds))));

        public static int RayCount(float loudness, EchoTuningProfile profile)
        {
            int rays = Mathf.RoundToInt(Mathf.Lerp(profile.minRays, profile.maxRays, Mathf.Clamp01(loudness)));
            rays = Mathf.Max(3, rays);

            // Odd, so the centre of the cone is always sampled by a ray and not only from its two
            // sides: a pillar straight ahead must not come back as two symmetric bumps.
            rays |= 1;
            return Mathf.Min(rays, profile.CapacityRays);
        }

        public static float BaseRadius(float radius0, float waveSpeed, float age) => radius0 - waveSpeed * age;

        public static bool IsAlive(float baseRadius, float rKill) => baseRadius > rKill;

        /// <summary>
        /// Age of arc k in a train. The carrier leaves at age 0 and every following arc is born
        /// arcInterval later, which is what turns one ring into a wave with a front.
        /// </summary>
        public static float ArcAge(float age, int arcIndex, float arcInterval) => age - arcIndex * arcInterval;

        /// <summary>Seconds a single arc lives, from emission to its radius falling to rKill.</summary>
        public static float ArcLife(float radius0, float waveSpeed, float rKill) =>
            waveSpeed > 0f ? Mathf.Max(0f, (radius0 - rKill) / waveSpeed) : 0f;

        /// <summary>
        /// Seconds the whole train is on screen. Longer than one arc by the stagger of the later arcs,
        /// otherwise the ping is retired while its innermost arc is still travelling.
        /// </summary>
        public static float TrainLife(float radius0, float waveSpeed, float rKill, int arcsPerPing, float arcInterval) =>
            ArcLife(radius0, waveSpeed, rKill) + Mathf.Max(0, arcsPerPing - 1) * Mathf.Max(0f, arcInterval);

        /// <summary>
        /// Radius of the outward cue emitted with a ping. It grows at the same rate the returning
        /// carrier shrinks and is clamped below that carrier, so the two rings never cross and the
        /// eye always reads one wave leaving and one coming back.
        /// </summary>
        public static float EmissionRadius(float age, float waveSpeed, float carrierRadius) =>
            Mathf.Min(waveSpeed * age, Mathf.Max(0f, carrierRadius));

        public static bool HasEmissionPulse(float age, EchoTuningProfile profile) =>
            profile.emissionPulseSeconds > 0f && age <= profile.emissionPulseSeconds;

        /// <summary>Fades the emission cue out over its own window, independent of the arc curve.</summary>
        public static float EmissionAlpha(float age, EchoTuningProfile profile)
        {
            if (profile.emissionPulseSeconds <= 0f)
            {
                return 0f;
            }

            float t = Mathf.Clamp01(age / profile.emissionPulseSeconds);
            return (1f - t) * (1f - t);
        }

        /// <summary>
        /// Deformation gain of arc k in a train. With gainSign +1 and d &gt;= dMin, the nearest
        /// surface sits on the shrinking carrier circle and everything farther sits outside it,
        /// which is what reads as a wave coming back at you.
        /// </summary>
        public static float Deformation(int arcIndex, EchoTuningProfile profile) =>
            profile.gain0 * profile.gainSign * Mathf.Pow(profile.innerDecay, arcIndex);

        public static float SampleRadius(float baseRadius, float gain, float hitDistance, float dMin) =>
            baseRadius + gain * (hitDistance - dMin);

        /// <summary>
        /// Angular spacing of a fan's rays. A cone puts its first and last ray exactly on the edges, so
        /// it divides the span by rays-1; a full circle must not sample the same bearing twice, or the
        /// closing segment degenerates and the ring shows a seam.
        /// </summary>
        public static float AngleStep(PingRecord ping)
        {
            if (ping.rayCount <= 1)
            {
                return 0f;
            }

            return ping.fullCircle
                ? Mathf.PI * 2f / ping.rayCount
                : 2f * ping.coneHalfRad / (ping.rayCount - 1);
        }

        public static float SampleAngle(PingRecord ping, int i)
        {
            if (ping.rayCount <= 1)
            {
                return ping.aimAngleRad;
            }

            float start = ping.fullCircle ? ping.aimAngleRad - Mathf.PI : ping.aimAngleRad - ping.coneHalfRad;
            return start + AngleStep(ping) * i;
        }

        public static Vector2 Polar(Vector2 origin, float angleRad, float radius) =>
            origin + new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * radius;

        public static float Alpha(float age, float lifeSeconds, AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
            {
                return 1f;
            }

            float t = lifeSeconds > 0f ? Mathf.Clamp01(age / lifeSeconds) : 1f;
            return Mathf.Clamp01(curve.Evaluate(t));
        }

        /// <summary>
        /// A ray that missed everything means "no information", not "far surface", so it must break
        /// the polyline instead of stretching it to the edge of the cone.
        /// </summary>
        public static bool HasReflection(PingRecord ping, int i) =>
            i >= 0 && i < ping.rayCount && ping.amp != null && ping.amp[i] > 0f &&
            !float.IsPositiveInfinity(ping.d[i]) && !float.IsNaN(ping.d[i]);
    }
}
