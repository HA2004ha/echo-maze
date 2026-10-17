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
        /// Deformation gain of arc k in a train. With gainSign +1 and d &gt;= dMin, the nearest
        /// surface sits on the shrinking carrier circle and everything farther sits outside it,
        /// which is what reads as a wave coming back at you.
        /// </summary>
        public static float Deformation(int arcIndex, EchoTuningProfile profile) =>
            profile.gain0 * profile.gainSign * Mathf.Pow(profile.innerDecay, arcIndex);

        public static float SampleRadius(float baseRadius, float gain, float hitDistance, float dMin) =>
            baseRadius + gain * (hitDistance - dMin);

        public static float SampleAngle(PingRecord ping, int i)
        {
            if (ping.rayCount <= 1)
            {
                return ping.aimAngleRad;
            }

            float span = 2f * ping.coneHalfRad;
            return ping.aimAngleRad - ping.coneHalfRad + span * (i / (float)(ping.rayCount - 1));
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
