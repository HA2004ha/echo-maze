using System.Collections.Generic;
using EchoMaze.Core;
using EchoMaze.Data;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The train itself: three arcs staggered in time, deforming less as they come in, sharing one
    /// fracture mask, and a 360 degree pulse that has to close. These are the behaviours that make a
    /// ping read as a wave travelling at you instead of a ring blinking once.
    /// </summary>
    public class ArcTrainTests
    {
        EchoTuningProfile profile;
        readonly List<Object> spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<EchoTuningProfile>();
            profile.arcsPerPing = 3;
            profile.arcInterval = 0.12f;
            profile.innerDecay = 0.7f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object body in spawned)
            {
                Object.DestroyImmediate(body);
            }

            spawned.Clear();
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Arcs_AreBornOneIntervalApart()
        {
            Assert.That(ArcMath.ArcAge(0.5f, 0, profile.arcInterval), Is.EqualTo(0.5f));
            Assert.That(ArcMath.ArcAge(0.5f, 1, profile.arcInterval), Is.EqualTo(0.38f));
            Assert.That(ArcMath.ArcAge(0.05f, 1, profile.arcInterval), Is.LessThan(0f),
                "the second arc has not left the player yet at 0.05s");
        }

        [Test]
        public void Train_OutlivesItsFirstArc()
        {
            float single = ArcMath.ArcLife(6.5f, profile.waveSpeed, profile.rKill);
            float train = ArcMath.TrainLife(6.5f, profile.waveSpeed, profile.rKill,
                                            profile.arcsPerPing, profile.arcInterval);

            Assert.Greater(train, single,
                "if the ping is retired with the outer arc, the inner arcs are cut off mid-flight");
            Assert.That(train, Is.EqualTo(single + 2f * profile.arcInterval).Within(1e-4f));
        }

        [Test]
        public void InnerArcs_DeformLessThanTheCarrier()
        {
            float outer = ArcMath.Deformation(0, profile);
            float middle = ArcMath.Deformation(1, profile);
            float inner = ArcMath.Deformation(2, profile);

            Assert.Greater(outer, middle);
            Assert.Greater(middle, inner);
            Assert.Greater(inner, 0f, "an inner arc that deforms nothing at all is just a circle");
        }

        [Test]
        public void EveryArcOfOnePing_ShowsTheSameGaps()
        {
            PingRecord ping = FlatProfile(121, 3f);
            SurfaceRead.Compute(ping, profile);
            EchoRenderer renderer = Renderer();

            int outer = DrawnQuads(renderer, ping, 0, 3.0f);
            int middle = DrawnQuads(renderer, ping, 1, 2.6f);
            int inner = DrawnQuads(renderer, ping, 2, 2.2f);

            Assert.Greater(outer, 0, "the arc drew nothing");
            Assert.AreEqual(outer, middle, "the gaps crawled between arcs: the eye cannot track the wave");
            Assert.AreEqual(outer, inner);
        }

        [Test]
        public void Fracture_ActuallyRemovesSegments()
        {
            profile.flatDropChance = 0.3f;
            PingRecord ping = FlatProfile(121, 3f);
            SurfaceRead.Compute(ping, profile);

            int drawn = DrawnQuads(Renderer(), ping, 0, 3.0f);
            Assert.Less(drawn, ping.rayCount - 1,
                "a flat wall with a drop chance set must come back broken, not as a full ring");
        }

        [Test]
        public void ClosedRing_BridgesItsOwnSeam()
        {
            profile.flatDropChance = 0f;
            PingRecord ping = FlatProfile(121, 1.5f, closedRing: true);
            SurfaceRead.Compute(ping, profile);

            int quads = DrawnQuads(Renderer(), ping, 0, 1.5f);
            Assert.AreEqual(ping.rayCount, quads,
                "a 360 degree pulse that does not close shows a seam the player reads as an opening");
        }

        [Test]
        public void OpenCone_EndsAtItsEdges()
        {
            profile.flatDropChance = 0f;
            PingRecord ping = FlatProfile(121, 3f);
            SurfaceRead.Compute(ping, profile);

            int quads = DrawnQuads(Renderer(), ping, 0, 3.0f);
            Assert.AreEqual(ping.rayCount - 1, quads,
                "a cone must not wrap: the gap at the aim line is what says this is ahead of you, not around you");
        }

        int DrawnQuads(EchoRenderer renderer, PingRecord ping, int arcIndex, float baseRadius)
        {
            renderer.BeginFrame();
            renderer.AddArc(ping, arcIndex, baseRadius, 1f, 0f, profile);
            renderer.EndFrame();
            return renderer.LastQuadCount;
        }

        EchoRenderer Renderer()
        {
            var body = new GameObject("test field");
            spawned.Add(body);
            return body.AddComponent<EchoRenderer>();
        }

        /// <summary>
        /// A synthetic all-hit profile at a constant distance: locally flat by construction, so the only
        /// thing left to prove is how the arc is cut up and connected, with no physics in the way.
        /// </summary>
        static PingRecord FlatProfile(int rays, float distance, bool closedRing = false)
        {
            var ping = new PingRecord();
            ping.Begin(Vector2.zero, 0.7f, 0f, closedRing ? Mathf.PI : 32f * Mathf.Deg2Rad,
                       distance, 0f, rays, 1234, closedRing);

            for (int i = 0; i < rays; i++)
            {
                ping.WriteSample(i, distance, 1f, SurfaceKind.Plain, true);
            }

            return ping;
        }
    }
}
