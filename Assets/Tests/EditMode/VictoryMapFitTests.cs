using EchoMaze.UI;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    public class VictoryMapFitTests
    {
        const float MarginPerSide = 0.8f;
        const float Tolerance = 0.0001f;

        [TestCase(16f / 9f, 40f, 8f)]
        [TestCase(16f / 9f, 8f, 40f)]
        [TestCase(16f / 9f, 20f, 20f)]
        [TestCase(4f / 3f, 40f, 8f)]
        [TestCase(4f / 3f, 8f, 40f)]
        [TestCase(4f / 3f, 20f, 20f)]
        [TestCase(9f / 16f, 40f, 8f)]
        [TestCase(9f / 16f, 8f, 40f)]
        [TestCase(9f / 16f, 20f, 20f)]
        public void FullBoundsAndMargin_FitInsideMapViewport(float aspect, float width, float height)
        {
            // Off-origin bounds catch fitting against world zero instead of the map's centre.
            var bounds = new Bounds(new Vector3(13f, -7f, 0f), new Vector3(width, height, 0f));
            float size = VictoryMapView.FitSize(bounds, aspect);
            Rect viewport = VictoryMapView.MapViewport;
            Assert.Greater(size, 0f);
            Assert.IsFalse(float.IsNaN(size) || float.IsInfinity(size));

            // Fitting the whole screen would let geometry overlap the title and legend.
            Vector2 cameraCentre = bounds.center;
            cameraCentre.y -= (viewport.center.y - 0.5f) * size * 2f;
            Vector2 min = (Vector2)bounds.min - Vector2.one * MarginPerSide;
            Vector2 max = (Vector2)bounds.max + Vector2.one * MarginPerSide;
            float left = 0.5f + (min.x - cameraCentre.x) / (2f * size * aspect);
            float right = 0.5f + (max.x - cameraCentre.x) / (2f * size * aspect);
            float bottom = 0.5f + (min.y - cameraCentre.y) / (2f * size);
            float top = 0.5f + (max.y - cameraCentre.y) / (2f * size);

            Assert.GreaterOrEqual(left, viewport.xMin - Tolerance, "left margin clipped");
            Assert.LessOrEqual(right, viewport.xMax + Tolerance, "right margin clipped");
            Assert.GreaterOrEqual(bottom, viewport.yMin - Tolerance, "bottom margin clipped");
            Assert.LessOrEqual(top, viewport.yMax + Tolerance, "top margin clipped");
        }

        [TestCase(16f / 9f)]
        [TestCase(4f / 3f)]
        [TestCase(9f / 16f)]
        public void GrowingBounds_NeverZoomsIn(float aspect)
        {
            var original = new Bounds(Vector3.zero, new Vector3(12f, 9f, 0f));
            float size = VictoryMapView.FitSize(original, aspect);
            foreach (Vector3 dimensions in new[]
                     { new Vector3(24f, 9f, 0f), new Vector3(12f, 18f, 0f) })
            {
                Assert.GreaterOrEqual(VictoryMapView.FitSize(new Bounds(Vector3.zero, dimensions), aspect), size);
            }

            Assert.Greater(VictoryMapView.FitSize(new Bounds(Vector3.zero, original.size * 2f), aspect), size,
                "growing both dimensions must zoom out, not return a fixed size");
            Assert.AreEqual(size,
                VictoryMapView.FitSize(new Bounds(new Vector3(100f, -50f, 0f), original.size), aspect),
                Tolerance, "translation must not change the required size");
        }

        [Test]
        public void NarrowerScreen_NeverReducesRequiredSize()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(40f, 8f, 0f));
            float landscape = VictoryMapView.FitSize(bounds, 16f / 9f);
            float standard = VictoryMapView.FitSize(bounds, 4f / 3f);
            float portrait = VictoryMapView.FitSize(bounds, 9f / 16f);
            Assert.Greater(standard, landscape);
            Assert.Greater(portrait, standard);
        }
    }
}
