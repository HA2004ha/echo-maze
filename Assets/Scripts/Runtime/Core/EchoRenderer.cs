using EchoMaze.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace EchoMaze.Core
{
    /// <summary>
    /// Draws the whole wave field as one mesh in one draw call, with per-sample colour and
    /// thickness. Chosen over LineRenderer (which cannot vary width per sample and multiplies
    /// components on every break) and over Sprite Shape (built for filled splines).
    ///
    /// Unity 6000.6 has no Mesh.SetIndexCount, so the index buffer is written once and unused
    /// quads collapse to a degenerate point with zero colour instead of shrinking the buffer.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class EchoRenderer : MonoBehaviour, IEchoRenderer
    {
        public const int MaxQuads = 2400;
        const int VertsPerQuad = 4;

        Mesh mesh;
        Vector3[] verts;
        Color[] colors;
        Vector2[] uvs;

        MeshFilter meshFilter;
        int cursor;
        int lastVertCount;

        /// <summary>Allocated once; BeginFrame uploads a prefix of it instead of building a new array per frame.</summary>
        Color[] clearedColors;

        public int LastQuadCount { get; private set; }
        public int OverflowCount { get; private set; }

        void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();

            int vertCount = MaxQuads * VertsPerQuad;
            verts = new Vector3[vertCount];
            colors = new Color[vertCount];
            uvs = new Vector2[vertCount];

            var indices = new int[MaxQuads * 6];
            for (int q = 0; q < MaxQuads; q++)
            {
                int v = q * VertsPerQuad;
                int o = q * 6;
                indices[o] = v;
                indices[o + 1] = v + 1;
                indices[o + 2] = v + 2;
                indices[o + 3] = v;
                indices[o + 4] = v + 2;
                indices[o + 5] = v + 3;
            }

            for (int i = 0; i < vertCount; i++)
            {
                uvs[i] = new Vector2(i % VertsPerQuad < 2 ? 0f : 1f, i % VertsPerQuad == 1 || i % VertsPerQuad == 2 ? 1f : 0f);
            }

            mesh = new Mesh { name = "EchoField" };
            mesh.MarkDynamic();
            clearedColors = new Color[vertCount];
            mesh.SetVertices(verts, 0, vertCount, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetColors(colors, 0, vertCount, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetUVs(0, uvs, 0, vertCount, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 64f);

            meshFilter.mesh = mesh;
            if (meshRenderer != null)
            {
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }
        }

        public void BeginFrame()
        {
            cursor = 0;
            OverflowCount = 0;

            for (int i = 0; i < lastVertCount; i++)
            {
                verts[i] = Vector3.zero;
            }

            if (lastVertCount > 0)
            {
                mesh.SetColors(clearedColors, 0, lastVertCount, MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetVertices(verts, 0, lastVertCount, MeshUpdateFlags.DontRecalculateBounds);
            }
        }

        public void AddArc(PingRecord ping, int arcIndex, float baseRadius, float alpha, EchoTuningProfile profile)
        {
            if (ping == null || alpha <= 0f)
            {
                return;
            }

            float gain = ArcMath.Deformation(arcIndex, profile);
            float half = profile.baseThickness * 0.5f;
            bool hasPrevious = false;
            Vector2 previous = Vector2.zero;
            float previousAmp = 0f;

            for (int i = 0; i < ping.rayCount; i++)
            {
                // A missing reflection is a hole in the arc, never a long line to nowhere.
                if (!ArcMath.HasReflection(ping, i))
                {
                    hasPrevious = false;
                    continue;
                }

                float r = ArcMath.SampleRadius(baseRadius, gain, ping.d[i], ping.dMin);
                if (r <= 0f || float.IsNaN(r))
                {
                    hasPrevious = false;
                    continue;
                }

                Vector2 current = ArcMath.Polar(ping.origin, ArcMath.SampleAngle(ping, i), r);
                if (hasPrevious)
                {
                    AddSegment(previous, current, half, profile.arcColor, previousAmp * alpha);
                }

                previous = current;
                previousAmp = ping.amp[i];
                hasPrevious = true;
            }
        }

        public void EndFrame()
        {
            int count = cursor * VertsPerQuad;
            if (count > 0)
            {
                mesh.SetVertices(verts, 0, count, MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetColors(colors, 0, count, MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetUVs(0, uvs, 0, count, MeshUpdateFlags.DontRecalculateBounds);
            }

            lastVertCount = count;
            LastQuadCount = cursor;
        }

        void AddSegment(Vector2 a, Vector2 b, float halfWidth, Color baseColor, float brightness)
        {
            if (cursor >= MaxQuads || brightness <= 0f)
            {
                OverflowCount++;
                return;
            }

            Vector2 dir = b - a;
            float len = dir.magnitude;
            if (len <= 1e-6f)
            {
                return;
            }

            dir /= len;
            Vector2 side = new Vector2(-dir.y, dir.x) * halfWidth;
            Color c = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(baseColor.a * brightness));

            int v = cursor * VertsPerQuad;
            verts[v] = a - side;
            verts[v + 1] = a + side;
            verts[v + 2] = b + side;
            verts[v + 3] = b - side;

            colors[v] = c;
            colors[v + 1] = c;
            colors[v + 2] = c;
            colors[v + 3] = c;

            cursor++;
        }
    }
}
