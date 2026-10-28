using System.Collections.Generic;
using EchoMaze.Core;
using EchoMaze.Data;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The GDD's second tone: what comes back from something alive is warm, and what comes back from the
    /// room is cyan. The shimmer already made a creature pulse, but a pulse that arrives in the player's own
    /// colour is a brightness effect, not a signature — so this reads the colours the renderer actually put
    /// into the mesh, because a rule that only lives in a shader parameter can be dropped by a tuning change
    /// without any test noticing.
    ///
    /// Rigs sit at (40, 40) like every other EditMode readability test, and the renderer is driven headless:
    /// BeginFrame/AddArc/EndFrame build their buffers without Awake.
    /// </summary>
    public class CreatureTintTests
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
        public void ASampleOffACreature_IsDrawnWarm()
        {
            AddShape(RigOrigin + new Vector2(3f, 0f), 0.6f, SurfaceKind.Crawler);
            Color[] drawn = DrawArcToward(0f);

            int warm = 0;
            foreach (Color c in drawn)
            {
                if (SameRgb(c, profile.creatureArcColor))
                {
                    warm++;
                }
            }

            Assert.Greater(warm, 0, "the wave off a living thing arrived in the player's own colour");
            Assert.That(warm, Is.EqualTo(drawn.Length),
                "every segment of a cone that only met the creature must be warm");
        }

        [Test]
        public void ASampleOffTheRoom_IsDrawnCyan()
        {
            AddShape(RigOrigin + new Vector2(3f, 0f), 0.6f, SurfaceKind.Plain);
            Color[] drawn = DrawArcToward(0f);

            foreach (Color c in drawn)
            {
                Assert.That(SameRgb(c, profile.creatureArcColor), Is.False,
                    "a wall lit up in the creature's tone: the tone stops being a signature");
                Assert.That(SameRgb(c, profile.arcColor), Is.True, "the room is not drawn in its own colour");
            }
        }

        [Test]
        public void OneConeWithBoth_TintsEachSurfaceOnItsOwn()
        {
            AddShape(RigOrigin + new Vector2(3f, 0.9f), 0.6f, SurfaceKind.Crawler);
            AddShape(RigOrigin + new Vector2(3f, -0.9f), 0.6f, SurfaceKind.Plain);
            Color[] drawn = DrawArcToward(0f);

            int warm = 0;
            int cyan = 0;
            foreach (Color c in drawn)
            {
                if (SameRgb(c, profile.creatureArcColor)) warm++;
                else if (SameRgb(c, profile.arcColor)) cyan++;
            }

            Assert.Greater(warm, 0, "the creature half of the cone lost its tone");
            Assert.Greater(cyan, 0, "the wall half of the cone lost its tone");
            Assert.That(warm + cyan, Is.EqualTo(drawn.Length),
                "a segment arrived in a third colour: only two tones exist in the dark");
        }

        [Test]
        public void TheTwoTones_AreActuallyDifferentHues()
        {
            float hueA = HueOf(profile.arcColor);
            float hueB = HueOf(profile.creatureArcColor);
            float apart = Mathf.Min(Mathf.Abs(hueA - hueB), 1f - Mathf.Abs(hueA - hueB));

            Assert.Greater(apart, 0.1f,
                "cyan and warm have been tuned into the same tone: 'their sound' now looks like 'your echo'");
        }

        void AddShape(Vector2 centre, float radius, SurfaceKind kind)
        {
            var body = new GameObject(kind == SurfaceKind.Plain ? "test wall" : "test crawler");
            body.transform.position = centre;
            body.AddComponent<CircleCollider2D>().radius = radius;
            body.AddComponent<EchoSurface>().kind = kind;
            spawned.Add(body);
        }

        /// <summary>
        /// Draws arc 0 of one ping and returns the vertex colours of the quads that were actually written,
        /// four per segment.
        /// </summary>
        Color[] DrawArcToward(float aimRad)
        {
            Physics2D.SyncTransforms();

            GameObject field = new GameObject("echo field test");
            field.AddComponent<MeshFilter>();
            field.AddComponent<MeshRenderer>();
            EchoRenderer renderer = field.AddComponent<EchoRenderer>();
            spawned.Add(field);

            var ping = PingRecord.CreatePooled(profile.CapacityRays);
            ping.Begin(RigOrigin, Loudness, aimRad, profile.ConeHalfRad,
                       ArcMath.PingRadius(Loudness, profile), 0f,
                       ArcMath.RayCount(Loudness, profile), 7);
            RayFan.Cast(ping, RayFan.BuildFilter(~0), new RaycastHit2D[profile.CapacityRays]);
            SurfaceRead.Compute(ping, profile);

            Assert.Greater(ping.ValidSampleCount, 0, "the fan hit nothing: the rig is broken, not the wave");

            renderer.BeginFrame();
            renderer.AddArc(ping, 0, ping.radius, 1f, 0f, profile);
            renderer.EndFrame();

            Assert.Greater(renderer.LastQuadCount, 0, "nothing was drawn, so no colour can be read from it");

            // sharedMesh, not mesh: reading .mesh in edit mode makes Unity instantiate the filter's mesh
            // and log an error about leaking it, which the test framework then fails the test on.
            Color[] all = field.GetComponent<MeshFilter>().sharedMesh.colors;
            var used = new Color[renderer.LastQuadCount * 4];
            System.Array.Copy(all, used, used.Length);
            return used;
        }

        static bool SameRgb(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        static float HueOf(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            float span = max - min;
            if (span < 1e-6f)
            {
                return 0f;
            }

            float hue = max == c.r ? (c.g - c.b) / span
                        : max == c.g ? 2f + (c.b - c.r) / span
                        : 4f + (c.r - c.g) / span;
            hue /= 6f;
            return hue < 0f ? hue + 1f : hue;
        }
    }
}
