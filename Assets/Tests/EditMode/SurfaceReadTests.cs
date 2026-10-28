using System.Collections.Generic;
using EchoMaze.Core;
using EchoMaze.Data;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// GDD 3.0 removed surface materials: the wave has to tell a wall from a pillar from a corner by
    /// the shape of the distance profile alone. These tests put real colliders in front of a real fan and
    /// assert the ordering, so a tuning change that flattens the difference fails here rather than in a
    /// playtest where nobody can explain why the room stopped making sense.
    ///
    /// Rigs sit at (40, 40), away from the sandbox geometry near the origin, which would otherwise
    /// answer the rays too.
    /// </summary>
    public class SurfaceReadTests
    {
        const float Loudness = 0.7f;
        static readonly Vector2 RigOrigin = new Vector2(40f, 40f);

        EchoTuningProfile profile;
        readonly List<GameObject> spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<EchoTuningProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject body in spawned)
            {
                Object.DestroyImmediate(body);
            }

            spawned.Clear();
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void FlatWall_SitsAtTheBaseBrightness()
        {
            AddBox(RigOrigin + new Vector2(3f, 0f), new Vector2(0.3f, 8f), 0f);
            PingRecord ping = EmitEast();

            float peak = PeakBrightness(ping);
            Assert.That(peak, Is.EqualTo(profile.brightnessBase).Within(0.02f),
                "a plane must not earn brightness from curvature, otherwise everything is equally bright " +
                "and the ordering is meaningless. Peak was " + peak.ToString("F3"));
        }

        [Test]
        public void Pillar_IsBrighterThanAFlatWall()
        {
            AddBox(RigOrigin + new Vector2(3f, 0f), new Vector2(0.3f, 8f), 0f);
            float wall = BrightnessAtNearestSurface(EmitEast());

            ClearRig();
            AddCircle(RigOrigin + new Vector2(3f, 0f), 0.6f);
            float pillar = BrightnessAtNearestSurface(EmitEast());

            Assert.Greater(pillar, wall,
                "a 0.6 m pillar must read brighter than the flat wall behind it");
        }

        [Test]
        public void Corner_IsBrighterThanAPillar()
        {
            AddCircle(RigOrigin + new Vector2(3f, 0f), 0.6f);
            float pillar = BrightnessAtNearestSurface(EmitEast());

            ClearRig();
            AddBox(RigOrigin + new Vector2(3f, 0f), new Vector2(1.2f, 1.2f), 45f);
            float corner = BrightnessAtNearestSurface(EmitEast());

            Assert.Greater(corner, pillar,
                "a convex corner is the sharpest thing a fan can measure, it has to top the pillar");
            Assert.GreaterOrEqual(corner, profile.brightnessBase + profile.brightnessGain - 0.01f,
                "and it should saturate: the hot point is the cue, not a mid-grey one");
        }

        [Test]
        public void Brightness_NeverExceedsBasePlusGain()
        {
            AddCircle(RigOrigin + new Vector2(3f, 0f), 0.6f);
            AddBox(RigOrigin + new Vector2(3f, 3f), new Vector2(1.2f, 1.2f), 45f);
            PingRecord ping = EmitEast();

            for (int i = 0; i < ping.rayCount; i++)
            {
                Assert.That(ping.brightness[i],
                    Is.LessThanOrEqualTo(profile.brightnessBase + profile.brightnessGain + 1e-4f));
            }
        }

        [Test]
        public void Misses_GetNoBrightness()
        {
            AddCircle(RigOrigin + new Vector2(3f, 0f), 0.6f);
            PingRecord ping = EmitEast();

            for (int i = 0; i < ping.rayCount; i++)
            {
                if (ArcMath.HasReflection(ping, i))
                {
                    continue;
                }

                Assert.That(ping.brightness[i], Is.EqualTo(0f));
                Assert.That(ping.broken[i], Is.False, "nothing to break where nothing was drawn");
            }
        }

        [Test]
        public void Fracture_DropsSegmentsAcrossAFlatRun()
        {
            AddBox(RigOrigin + new Vector2(3f, 0f), new Vector2(0.3f, 8f), 0f);
            PingRecord ping = EmitEast();

            int drops = CountDrops(ping);
            Assert.Greater(drops, 0, "a fully drawn outline is a printed map, not an echo");
            Assert.Less(drops, ping.ValidSampleCount, "and it must not eat the whole arc");
        }

        [Test]
        public void Fracture_NeverBreaksAcrossAFeature()
        {
            AddBox(RigOrigin + new Vector2(3f, 0f), new Vector2(1.2f, 1.2f), 45f);
            PingRecord ping = EmitEast();

            for (int i = 0; i < ping.rayCount; i++)
            {
                int next = SurfaceRead.NextSample(ping, i);
                if (next < 0 || !ArcMath.HasReflection(ping, i) || !ArcMath.HasReflection(ping, next))
                {
                    continue;
                }

                if (Mathf.Max(ping.curvature[i], ping.curvature[next]) >= profile.fractureFlatBelow)
                {
                    Assert.That(ping.broken[i], Is.False,
                        "sample " + i + " is on a corner and the gap pattern would hide it");
                }
            }
        }

        [Test]
        public void Fracture_IsDeterministicFromTheSeed()
        {
            AddCircle(RigOrigin + new Vector2(3f, 0f), 0.6f);
            AddBox(RigOrigin + new Vector2(5f, 0f), new Vector2(0.3f, 6f), 0f);
            Physics2D.SyncTransforms();

            PingRecord first = EmitEast();

            var second = PingRecord.CreatePooled(profile.CapacityRays);
            second.Begin(RigOrigin, Loudness, 0f, profile.ConeHalfRad,
                         ArcMath.PingRadius(Loudness, profile), 0f,
                         ArcMath.RayCount(Loudness, profile), first.seed);
            RayFan.Cast(second, RayFan.BuildFilter(~0), new RaycastHit2D[profile.CapacityRays]);
            SurfaceRead.Compute(second, profile);

            Assert.AreEqual(MaskOf(first), MaskOf(second),
                "the same seed must produce the same gaps, or a replayed ping cannot be debugged");
        }

        [Test]
        public void DifferentSeed_GivesADifferentPattern()
        {
            AddBox(RigOrigin + new Vector2(3f, 0f), new Vector2(0.3f, 8f), 0f);
            PingRecord a = EmitEastWithSeed(11);
            PingRecord b = EmitEastWithSeed(2147483599);

            Assert.AreNotEqual(MaskOf(a), MaskOf(b),
                "if every ping breaks in the same places the player learns the pattern instead of the room");
        }

        int CountDrops(PingRecord ping)
        {
            int drops = 0;
            for (int i = 0; i < ping.rayCount; i++)
            {
                if (ping.broken[i])
                {
                    drops++;
                }
            }

            return drops;
        }

        string MaskOf(PingRecord ping)
        {
            var text = new System.Text.StringBuilder(ping.rayCount);
            for (int i = 0; i < ping.rayCount; i++)
            {
                text.Append(ping.broken[i] ? '1' : '0');
            }

            return text.ToString();
        }

        float PeakBrightness(PingRecord ping)
        {
            float peak = 0f;
            for (int i = 0; i < ping.rayCount; i++)
            {
                peak = Mathf.Max(peak, ping.brightness[i]);
            }

            return peak;
        }

        /// <summary>
        /// Brightness at the closest point of the arc, which is where the feature the player is aiming at
        /// actually sits. Peak brightness is not a fair comparison between a pillar and a corner: a
        /// cylinder's silhouette is itself a tangent edge, so its peak saturates for the wrong reason.
        /// </summary>
        float BrightnessAtNearestSurface(PingRecord ping)
        {
            int nearest = -1;
            for (int i = 0; i < ping.rayCount; i++)
            {
                if (ArcMath.HasReflection(ping, i) && (nearest < 0 || ping.d[i] < ping.d[nearest]))
                {
                    nearest = i;
                }
            }

            Assert.That(nearest, Is.GreaterThanOrEqualTo(0), "nothing was hit at all");
            return ping.brightness[nearest];
        }

        void ClearRig()
        {
            foreach (GameObject body in spawned)
            {
                Object.DestroyImmediate(body);
            }

            spawned.Clear();
        }

        void AddCircle(Vector2 centre, float radius)
        {
            var body = new GameObject("test pillar");
            body.transform.position = centre;
            body.AddComponent<CircleCollider2D>().radius = radius;
            body.AddComponent<EchoSurface>();
            spawned.Add(body);
        }

        void AddBox(Vector2 centre, Vector2 size, float degrees)
        {
            var body = new GameObject("test wall");
            body.transform.position = centre;
            body.transform.rotation = Quaternion.Euler(0f, 0f, degrees);
            body.AddComponent<BoxCollider2D>().size = size;
            body.AddComponent<EchoSurface>();
            spawned.Add(body);
        }

        PingRecord EmitEast() => EmitEastWithSeed(7);

        PingRecord EmitEastWithSeed(int seed)
        {
            Physics2D.SyncTransforms();

            var ping = PingRecord.CreatePooled(profile.CapacityRays);
            ping.Begin(RigOrigin, Loudness, 0f, profile.ConeHalfRad,
                       ArcMath.PingRadius(Loudness, profile), 0f,
                       ArcMath.RayCount(Loudness, profile), seed);
            RayFan.Cast(ping, RayFan.BuildFilter(~0), new RaycastHit2D[profile.CapacityRays]);
            SurfaceRead.Compute(ping, profile);

            Assert.Greater(ping.ValidSampleCount, 0, "the fan hit nothing: the rig is broken, not the wave");
            return ping;
        }
    }
}
