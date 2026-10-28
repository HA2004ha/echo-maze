using System.Collections.Generic;
using EchoMaze.Core;
using EchoMaze.Data;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The step/kink split, measured in the week 2 blockout: a fan aimed at the north wall crossed from
    /// the corner box (4.05 m) to the wall beside it (5.66 m), and the second difference on the two
    /// samples straddling that edge read 12670 — three orders of magnitude above what saturates a sample,
    /// because a crossing between two surfaces is not a bend in one. A brightness cue that fires on every
    /// silhouette is not a cue, so a distance <em>step</em> now breaks the line instead of glowing, and only
    /// a continuous <em>kink</em> — a real corner — is allowed to flare.
    ///
    /// These rigs sit at (40, 40), like SurfaceReadTests, away from the sandbox geometry near the origin.
    /// </summary>
    public class EdgeBreakTests
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
        public void AStepBetweenTwoSurfaces_BreaksTheLine()
        {
            AddBox(new Vector2(3f, 1.8f), new Vector2(0.3f, 4f), 0f);
            AddBox(new Vector2(5f, -1.8f), new Vector2(0.3f, 4f), 0f);
            PingRecord ping = EmitEast();

            int steps = 0;
            for (int i = 0; i < ping.rayCount; i++)
            {
                int next = SurfaceRead.NextSample(ping, i);
                if (next < 0 || !ArcMath.HasReflection(ping, i) || !ArcMath.HasReflection(ping, next))
                {
                    continue;
                }

                if (SurfaceRead.IsSurfaceJump(ping, i, next, profile.edgeJumpFraction))
                {
                    steps++;
                    Assert.That(ping.broken[i], Is.True,
                        "sample " + i + " crosses from one surface to another and connecting them draws a " +
                        "spoke from one object to the next, which describes neither");
                }
            }

            Assert.Greater(steps, 0, "the rig produced no step at all, so it measures nothing");
        }

        [Test]
        public void AStepBetweenTwoSurfaces_EarnsNoBrightness()
        {
            AddBox(new Vector2(3f, 1.8f), new Vector2(0.3f, 4f), 0f);
            AddBox(new Vector2(5f, -1.8f), new Vector2(0.3f, 4f), 0f);
            PingRecord ping = EmitEast();

            for (int i = 0; i < ping.rayCount; i++)
            {
                int next = SurfaceRead.NextSample(ping, i);
                if (next < 0 || !ArcMath.HasReflection(ping, i) || !ArcMath.HasReflection(ping, next))
                {
                    continue;
                }

                if (!SurfaceRead.IsSurfaceJump(ping, i, next, profile.edgeJumpFraction))
                {
                    continue;
                }

                Assert.That(ping.curvature[i], Is.EqualTo(0f),
                    "a second difference taken across two surfaces is a distance gap, not a shape");
                Assert.That(ping.brightness[i], Is.LessThanOrEqualTo(profile.brightnessBase + 1e-4f),
                            "and a distance gap must not light up: sample " + i);
            }
        }

        [Test]
        public void ACornerKink_FlaresAndStaysConnected()
        {
            AddBox(new Vector2(3f, 0f), new Vector2(1.2f, 1.2f), 45f);
            PingRecord ping = EmitEast();

            int flared = -1;
            for (int i = 0; i < ping.rayCount; i++)
            {
                if (ping.brightness[i] >= profile.brightnessBase + profile.brightnessGain - 0.01f)
                {
                    flared = i;
                    break;
                }
            }

            Assert.That(flared, Is.GreaterThanOrEqualTo(0),
                "the corner stopped flaring: the hot point is the cue the GDD asks for");

            int next = SurfaceRead.NextSample(ping, flared);
            Assert.That(ping.broken[flared], Is.False,
                "a corner is continuous in distance and only its slope jumps, so the line through it stays " +
                "drawn — that continuity is exactly what separates it from a silhouette edge");
            Assert.That(next, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void AClutteredCone_DoesNotPaintEverythingBright()
        {
            // The blockout's complaint in one rig: a wall, a pillar in front of it and a box to the side —
            // the arrangement that produced the 12670 measured in `Level02_Corridor`.
            AddBox(new Vector2(3.2f, 0f), new Vector2(0.3f, 7f), 0f);
            AddCircle(new Vector2(2.2f, 1.4f), 0.6f);
            AddBox(new Vector2(4.2f, -1.6f), new Vector2(1.2f, 1.2f), 0f);
            PingRecord ping = EmitEast();

            int full = 0;
            for (int i = 0; i < ping.rayCount; i++)
            {
                if (ping.brightness[i] >= profile.brightnessBase + profile.brightnessGain - 0.01f)
                {
                    full++;
                }
            }

            float share = full / (float)ping.rayCount;
            Assert.That(share, Is.LessThan(0.12f),
                "at " + (share * 100f).ToString("F1") + "% of the cone at full brightness the wave has no " +
                "rest level left, so nothing reads as a feature. The flare must be a point, not a state.");
        }

        void AddCircle(Vector2 offset, float radius)
        {
            var body = new GameObject("test pillar");
            body.transform.position = RigOrigin + offset;
            body.AddComponent<CircleCollider2D>().radius = radius;
            body.AddComponent<EchoSurface>().kind = SurfaceKind.Plain;
            spawned.Add(body);
        }

        void AddBox(Vector2 offset, Vector2 size, float degrees)
        {
            var body = new GameObject("test wall");
            body.transform.position = RigOrigin + offset;
            body.transform.rotation = Quaternion.Euler(0f, 0f, degrees);
            body.AddComponent<BoxCollider2D>().size = size;
            body.AddComponent<EchoSurface>().kind = SurfaceKind.Plain;
            spawned.Add(body);
        }

        PingRecord EmitEast()
        {
            Physics2D.SyncTransforms();

            var ping = PingRecord.CreatePooled(profile.CapacityRays);
            ping.Begin(RigOrigin, Loudness, 0f, profile.ConeHalfRad,
                       ArcMath.PingRadius(Loudness, profile), 0f,
                       ArcMath.RayCount(Loudness, profile), 7);
            RayFan.Cast(ping, RayFan.BuildFilter(~0), new RaycastHit2D[profile.CapacityRays]);
            SurfaceRead.Compute(ping, profile);

            Assert.Greater(ping.ValidSampleCount, 0, "the fan hit nothing: the rig is broken, not the wave");
            return ping;
        }
    }
}
