using EchoMaze.Core;
using EchoMaze.Data;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// Pins the arc formula from the GDD. These are the only tests that can prove the wave will read
    /// the way the design says it must, and they need neither a scene nor a microphone.
    /// </summary>
    public class ArcMathTests
    {
        EchoTuningProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<EchoTuningProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
        }

        static PingRecord MakePing(EchoTuningProfile tuning, params float[] distances)
        {
            var ping = PingRecord.CreatePooled(distances.Length);
            ping.Begin(new Vector2(1f, 2f), 0.8f, 0f, tuning.ConeHalfRad, 6f, 0f, distances.Length, 1234);

            for (int i = 0; i < distances.Length; i++)
            {
                bool hit = !float.IsPositiveInfinity(distances[i]);
                ping.WriteSample(i, distances[i], hit ? 1f : 0f, hit ? SurfaceKind.Concrete : SurfaceKind.None, hit);
            }

            return ping;
        }

        [Test]
        public void Formula_MatchesGdd()
        {
            // r_k(theta) = R_k + A_k * (d(theta) - d_min)
            PingRecord ping = MakePing(profile, 2f, 4f, 6f);
            float baseRadius = 5f;
            float gain = ArcMath.Deformation(0, profile);

            Assert.AreEqual(5f + gain * (2f - 2f), ArcMath.SampleRadius(baseRadius, gain, 2f, ping.dMin), 1e-5f);
            Assert.AreEqual(5f + gain * (4f - 2f), ArcMath.SampleRadius(baseRadius, gain, 4f, ping.dMin), 1e-5f);
            Assert.AreEqual(5f + gain * (6f - 2f), ArcMath.SampleRadius(baseRadius, gain, 6f, ping.dMin), 1e-5f);
        }

        [Test]
        public void MonotonicInDistance()
        {
            float previous = float.NegativeInfinity;
            for (int i = 1; i <= 20; i++)
            {
                float radius = ArcMath.SampleRadius(4f, profile.gain0, i * 0.4f, 0.4f);
                Assert.Greater(radius, previous, "farther hit must sit farther out, sample " + i);
                previous = radius;
            }
        }

        [Test]
        public void NearestSurfaceSitsOnTheCarrierCircle()
        {
            // With gainSign +1 the nearest hit lands exactly on R_k, so the first thing to be
            // swallowed by the collapsing wave is what is closest to the player. That is the
            // "returning wave" the GDD asks for; the opposite sign makes it look emitted.
            PingRecord ping = MakePing(profile, 3f, 3f);
            Assert.AreEqual(4.5f,
                ArcMath.SampleRadius(4.5f, ArcMath.Deformation(0, profile), ping.d[1], ping.dMin), 1e-5f);
            Assert.Greater(ArcMath.SampleRadius(4.5f, ArcMath.Deformation(0, profile), 8f, ping.dMin), 4.5f);
        }

        [Test]
        public void CollapsesToOrigin_AsAgeGrows()
        {
            float radius0 = 9f;
            float age = 0f;
            float life = (radius0 - profile.rKill) / profile.waveSpeed;

            Assert.IsTrue(ArcMath.IsAlive(ArcMath.BaseRadius(radius0, profile.waveSpeed, age), profile.rKill));
            Assert.IsTrue(ArcMath.IsAlive(ArcMath.BaseRadius(radius0, profile.waveSpeed, life * 0.99f), profile.rKill));
            Assert.IsFalse(ArcMath.IsAlive(ArcMath.BaseRadius(radius0, profile.waveSpeed, life), profile.rKill),
                "the arc must die exactly when its carrier radius reaches rKill");
            Assert.IsFalse(float.IsNaN(ArcMath.BaseRadius(radius0, profile.waveSpeed, life * 2f)));
        }

        [Test]
        public void AllMisses_ProduceNoSamples_AndNoNaN()
        {
            PingRecord ping = MakePing(profile,
                float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

            Assert.AreEqual(0, ping.ValidSampleCount);
            Assert.AreEqual(float.PositiveInfinity, ping.dMin);

            for (int i = 0; i < ping.rayCount; i++)
            {
                Assert.IsFalse(ArcMath.HasReflection(ping, i), "a missed ray is no information, not a far surface");
            }
        }

        [Test]
        public void GainSign_FlipsDeformation()
        {
            profile.gainSign = 1f;
            float positive = ArcMath.Deformation(0, profile);
            profile.gainSign = -1f;
            float negative = ArcMath.Deformation(0, profile);

            Assert.AreEqual(-positive, negative, 1e-6f);
            Assert.Greater(ArcMath.SampleRadius(4f, positive, 7f, 2f), ArcMath.SampleRadius(4f, negative, 7f, 2f));
        }

        [Test]
        public void InnerArcsDeformLess()
        {
            profile.innerDecay = 0.8f;
            Assert.Greater(ArcMath.Deformation(0, profile), ArcMath.Deformation(3, profile));
            Assert.Greater(ArcMath.Deformation(3, profile), ArcMath.Deformation(9, profile));
        }

        [Test]
        public void LoudnessDrivesReachAndDensity()
        {
            Assert.AreEqual(profile.minRadius, ArcMath.PingRadius(0f, profile), 1e-4f);
            Assert.AreEqual(profile.maxRadius, ArcMath.PingRadius(1f, profile), 1e-4f);
            Assert.Greater(ArcMath.PingRadius(0.9f, profile), ArcMath.PingRadius(0.2f, profile));

            Assert.AreEqual(profile.minRays | 1, ArcMath.RayCount(0f, profile));
            Assert.AreEqual(profile.maxRays | 1, ArcMath.RayCount(1f, profile));
            Assert.LessOrEqual(ArcMath.RayCount(1f, profile), profile.CapacityRays,
                "a ping may never ask for more rays than the pooled buffers hold");

            for (int step = 0; step <= 10; step++)
            {
                Assert.AreEqual(1, ArcMath.RayCount(step / 10f, profile) % 2,
                    "an even ray count puts the aim direction exactly between two samples");
            }
        }

        [Test]
        public void AlphaStartsBrightAndEndsInvisible()
        {
            float life = 1.25f;
            Assert.Greater(ArcMath.Alpha(0f, life, profile.alphaCurve), 0.9f);
            Assert.AreEqual(0f, ArcMath.Alpha(life, life, profile.alphaCurve), 1e-4f);
            Assert.AreEqual(0f, ArcMath.Alpha(life * 3f, life, profile.alphaCurve), 1e-4f);
        }

        [Test]
        public void SampleAnglesSpanTheCone()
        {
            // An odd ray count is what puts one ray exactly on the aim direction, so the centre of
            // the cone is never between two samples.
            PingRecord ping = MakePing(profile, 1f, 1f, 1f, 1f, 1f);
            float half = profile.ConeHalfRad;

            Assert.AreEqual(-half, ArcMath.SampleAngle(ping, 0) - ping.aimAngleRad, 1e-5f);
            Assert.AreEqual(half, ArcMath.SampleAngle(ping, ping.rayCount - 1) - ping.aimAngleRad, 1e-5f);
            Assert.AreEqual(0f, ArcMath.SampleAngle(ping, 2) - ping.aimAngleRad, 1e-5f);
        }

        [Test]
        public void PingRecordReuseResetsTrackedExtremes()
        {
            PingRecord ping = MakePing(profile, 2f, 8f);
            Assert.AreEqual(2f, ping.dMin, 1e-5f);
            Assert.AreEqual(8f, ping.dMax, 1e-5f);

            ping.Begin(ping.origin, 0.5f, 0f, profile.ConeHalfRad, 4f, 1f, 1, 99);
            ping.WriteSample(0, 3f, 1f, SurfaceKind.Metal, true);

            Assert.AreEqual(3f, ping.dMin, 1e-5f, "a pooled record must not inherit the previous ping's dMin");
            Assert.AreEqual(3f, ping.dMax, 1e-5f);
            Assert.AreEqual(1, ping.ValidSampleCount);
        }
    }
}
