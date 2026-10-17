using System.Collections.Generic;
using EchoMaze.Core;
using EchoMaze.Data;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The other half of week 1: not whether the formula is right, but whether the shape it produces
    /// is big enough for a player to actually see. Everything is asserted in screen pixels, because
    /// that is the unit a playtest judges in, and a tuning change that flattens the wave then fails
    /// here instead of in someone's play session.
    ///
    /// Every rig sits at (40, 40): EditMode tests run in whatever scene the editor has open, and the
    /// sandbox objects near the origin would otherwise answer the raycasts too.
    /// </summary>
    public class ArcReadabilityTests
    {
        const float Loudness = 0.7f;
        static readonly Vector2 RigOrigin = new Vector2(40f, 40f);

        // The sandbox camera shows 12 world units over the short edge, and the delivery target is
        // 1080p, so one world unit is 90 pixels.
        const float PixelsPerUnit = 1080f / 12f;

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
        public void Pillar_DentsItsOwnOutline()
        {
            AddCircle(RigOrigin + new Vector2(3f, 0f), 0.6f);
            float spread = ArcRadiusSpread(EmitToward(RigOrigin, 0f));

            Assert.GreaterOrEqual(spread * PixelsPerUnit, 25f,
                "a pillar has to bend the ring by at least 25 pixels, measured " +
                (spread * PixelsPerUnit).ToString("F1"));
        }

        [Test]
        public void PillarAndWallBehindIt_ArriveAsTwoDifferentRadii()
        {
            AddCircle(RigOrigin + new Vector2(3f, 0f), 0.6f);
            AddBox(RigOrigin + new Vector2(4.5f, 0f), new Vector2(0.3f, 4f));
            float spread = ArcRadiusSpread(EmitToward(RigOrigin, 0f));

            Assert.GreaterOrEqual(spread * PixelsPerUnit, 90f,
                "the near object and the one behind it must not collapse into one ring, measured " +
                (spread * PixelsPerUnit).ToString("F1"));
        }

        [Test]
        public void PillarCentre_IsTheInnermostPointOfTheArc()
        {
            AddCircle(RigOrigin + new Vector2(3f, 0f), 0.6f);
            PingRecord ping = EmitToward(RigOrigin, 0f);

            float gain = ArcMath.Deformation(0, profile);
            float carrier = ping.radius;
            int centre = ping.rayCount / 2;
            Assert.That(ArcMath.HasReflection(ping, centre), Is.True, "the centre ray must hit the pillar");

            float centreRadius = ArcMath.SampleRadius(carrier, gain, ping.d[centre], ping.dMin);
            for (int i = 0; i < ping.rayCount; i++)
            {
                if (!ArcMath.HasReflection(ping, i))
                {
                    continue;
                }

                float r = ArcMath.SampleRadius(carrier, gain, ping.d[i], ping.dMin);
                Assert.That(r, Is.GreaterThanOrEqualTo(centreRadius - 1e-4f),
                    "the near face of the pillar has to sit closest to the player");
            }
        }

        [Test]
        public void Ribbon_IsAtLeastFourPixelsWide()
        {
            Assert.GreaterOrEqual(profile.baseThickness * PixelsPerUnit, 4f,
                "the wave is drawn about two pixels wide and cannot be followed");
        }

        void AddCircle(Vector2 centre, float radius)
        {
            var body = new GameObject("test pillar");
            body.transform.position = centre;
            body.AddComponent<CircleCollider2D>().radius = radius;
            body.AddComponent<EchoSurface>();
            spawned.Add(body);
        }

        void AddBox(Vector2 centre, Vector2 size)
        {
            var body = new GameObject("test wall");
            body.transform.position = centre;
            body.AddComponent<BoxCollider2D>().size = size;
            body.AddComponent<EchoSurface>();
            spawned.Add(body);
        }

        PingRecord EmitToward(Vector2 origin, float aimRad)
        {
            Physics2D.SyncTransforms();

            var ping = PingRecord.CreatePooled(profile.CapacityRays);
            ping.Begin(origin, Loudness, aimRad, profile.ConeHalfRad,
                       ArcMath.PingRadius(Loudness, profile), 0f,
                       ArcMath.RayCount(Loudness, profile), 7);
            RayFan.Cast(ping, RayFan.BuildFilter(~0), new RaycastHit2D[profile.CapacityRays]);

            Assert.Greater(ping.ValidSampleCount, 0, "the fan hit nothing: the rig is broken, not the wave");
            return ping;
        }

        float ArcRadiusSpread(PingRecord ping)
        {
            float gain = ArcMath.Deformation(0, profile);
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;

            for (int i = 0; i < ping.rayCount; i++)
            {
                if (!ArcMath.HasReflection(ping, i))
                {
                    continue;
                }

                float r = ArcMath.SampleRadius(ping.radius, gain, ping.d[i], ping.dMin);
                min = Mathf.Min(min, r);
                max = Mathf.Max(max, r);
            }

            return float.IsPositiveInfinity(min) ? 0f : max - min;
        }
    }
}
