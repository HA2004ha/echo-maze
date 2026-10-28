using System;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Core
{
    /// <summary>
    /// Reading the shape of a room out of the distance profile a fan measured, which is what replaced
    /// surface materials in GDD 3.0. Two outputs, both computed once per ping and stored on the record:
    ///
    /// brightness, from the local curvature of d(theta); and a fracture mask, so a long flat run of
    /// wall is drawn as a broken line rather than a printed outline.
    ///
    /// Pure and static on purpose: EditMode tests can hand it a profile with no scene, no camera and
    /// no Play Mode.
    /// </summary>
    public static class SurfaceRead
    {
        /// <summary>
        /// Normalised curvature of the distance profile at sample i: the discrete second difference
        /// divided by the square of the ray spacing (to turn it into a real second derivative) and by the
        /// distance it was measured at (to make it a property of the shape rather than of how far away
        /// the shape happens to be).
        ///
        /// A flat plane works out to exactly 1.0 at normal incidence and about 1.8 at the edge of a
        /// 32 degree cone; a 0.6 m pillar at 3 m is about 5; a convex corner is a slope discontinuity
        /// and lands in the tens. Dividing by the ray spacing is what makes the number independent of
        /// how densely the fan was sampled, so a whisper and a shout describe the same corner equally.
        ///
        /// Samples with no valid neighbour on both sides get zero, because an incomplete stencil is
        /// missing information, not a flat run.
        /// </summary>
        public static float NormalisedCurvature(PingRecord ping, int i)
        {
            if (!ArcMath.HasReflection(ping, i))
            {
                return 0f;
            }

            int prev = NeighbourIndex(ping, i, -1);
            int next = NeighbourIndex(ping, i, 1);
            if (prev < 0 || next < 0 || !ArcMath.HasReflection(ping, prev) || !ArcMath.HasReflection(ping, next))
            {
                return 0f;
            }

            float here = ping.d[i];
            float step = ArcMath.AngleStep(ping);
            if (here <= 1e-5f || step <= 1e-6f)
            {
                return 0f;
            }

            float second = Mathf.Abs(ping.d[next] - 2f * ping.d[i] + ping.d[prev]);
            float curvature = second / (here * step * step);
            return float.IsNaN(curvature) || float.IsInfinity(curvature) ? 0f : curvature;
        }

        /// <summary>
        /// Fills ping.brightness and ping.broken. Called at emit time only, never per frame, and the
        /// fracture RNG is seeded from the ping's own seed so the gaps are deterministic: replaying a
        /// recorded ping must produce the same wave, and the arcs of one train must agree about where
        /// the line is broken or the wave visibly crawls.
        /// </summary>
        public static void Compute(PingRecord ping, EchoTuningProfile profile)
        {
            if (ping == null || profile == null || !ping.Allocated)
            {
                return;
            }

            int n = ping.rayCount;
            for (int i = 0; i < n; i++)
            {
                if (!ArcMath.HasReflection(ping, i))
                {
                    ping.curvature[i] = 0f;
                    ping.brightness[i] = 0f;
                    ping.broken[i] = false;
                    continue;
                }

                ping.curvature[i] = NormalisedCurvature(ping, i);
                ping.brightness[i] = Brightness(ping.curvature[i], profile);
            }

            var rng = new System.Random(ping.seed);
            for (int i = 0; i < n; i++)
            {
                int next = NextSample(ping, i);
                if (next < 0 || !ArcMath.HasReflection(ping, i) || !ArcMath.HasReflection(ping, next))
                {
                    ping.broken[i] = false;
                    continue;
                }

                // Breaking the line across a feature would hide the very thing the arc was emitted to
                // find, so only locally flat runs are eligible.
                bool flat = Mathf.Max(ping.curvature[i], ping.curvature[next]) < profile.fractureFlatBelow;
                ping.broken[i] = flat && rng.NextDouble() < profile.flatDropChance;
            }
        }

        /// <summary>
        /// The sample the arc connects to after i: the next ray, or the first ray for a closed ring, and
        /// nothing at all at the edge of an open cone.
        /// </summary>
        public static int NextSample(PingRecord ping, int i) => NeighbourIndex(ping, i, 1);

        public static float Brightness(float curvature, EchoTuningProfile profile) =>
            profile.brightnessBase +
            profile.brightnessGain * Mathf.Clamp01((curvature - profile.curvatureFloor) / profile.curvatureRange);

        /// <summary>
        /// Things that breathe. Only creatures shimmer on the wave; a wall that pulses would be read as
        /// an enemy by a player who has learned the language, which is the opposite of the design.
        /// </summary>
        public static bool IsCreature(SurfaceKind kind) => kind == SurfaceKind.Crawler || kind == SurfaceKind.Spike;

        /// <summary>
        /// A brightness multiplier that makes creature samples blink at the heartbeat rate. The phase comes
        /// from the sample index so a creature's two bumps do not breathe in lockstep with each other.
        /// </summary>
        public static float Shimmer(int sampleIndex, float time, EchoTuningProfile profile)
        {
            if (profile.shimmer <= 0f || profile.heartbeatHz <= 0f)
            {
                return 1f;
            }

            float phase = sampleIndex * 0.37f;
            return 1f + profile.shimmer * Mathf.Sin((2f * Mathf.PI * profile.heartbeatHz * time) + phase);
        }

        static int NeighbourIndex(PingRecord ping, int i, int delta)
        {
            if (ping.rayCount <= 0)
            {
                return -1;
            }

            if (!ping.fullCircle)
            {
                int j = i + delta;
                return j < 0 || j >= ping.rayCount ? -1 : j;
            }

            int wrapped = (i + delta) % ping.rayCount;
            return wrapped < 0 ? wrapped + ping.rayCount : wrapped;
        }
    }
}
