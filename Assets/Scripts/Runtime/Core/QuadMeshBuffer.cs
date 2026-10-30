using UnityEngine;
using UnityEngine.Rendering;

namespace EchoMaze.Core
{
    /// <summary>
    /// A fixed-capacity mesh of ribbon quads in the UV layout the EchoArc shader expects (uv.y runs across
    /// the ribbon), for the things that are drawn next to the wave but must not spend its quad budget:
    /// the wall memory and the footprint trail. Same trick as <see cref="EchoRenderer"/>: the index buffer
    /// is written once, unused quads collapse onto the origin with a zero colour.
    /// </summary>
    public sealed class QuadMeshBuffer
    {
        const int VertsPerQuad = 4;

        readonly Vector3[] verts;
        readonly Color[] colors;
        int cursor;
        int lastUsed;

        public QuadMeshBuffer(int capacity, string name)
        {
            Capacity = capacity;
            int vertCount = capacity * VertsPerQuad;
            verts = new Vector3[vertCount];
            colors = new Color[vertCount];
            var uvs = new Vector2[vertCount];
            var indices = new int[capacity * 6];

            for (int q = 0; q < capacity; q++)
            {
                int v = q * VertsPerQuad;
                int o = q * 6;
                indices[o] = v;
                indices[o + 1] = v + 1;
                indices[o + 2] = v + 2;
                indices[o + 3] = v;
                indices[o + 4] = v + 2;
                indices[o + 5] = v + 3;
                uvs[v] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(0f, 1f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(1f, 0f);
            }

            Mesh = new Mesh { name = name };
            Mesh.MarkDynamic();
            Mesh.SetVertices(verts, 0, vertCount, MeshUpdateFlags.DontRecalculateBounds);
            Mesh.SetColors(colors, 0, vertCount, MeshUpdateFlags.DontRecalculateBounds);
            Mesh.SetUVs(0, uvs, 0, vertCount, MeshUpdateFlags.DontRecalculateBounds);
            Mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            Mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1024f);
        }

        public Mesh Mesh { get; }
        public int Capacity { get; }
        public int Count => cursor;

        public void Begin()
        {
            int n = lastUsed * VertsPerQuad;
            for (int i = 0; i < n; i++)
            {
                verts[i] = Vector3.zero;
                colors[i] = Color.clear;
            }

            cursor = 0;
        }

        /// <summary>One ribbon from a to b, <paramref name="halfWidth"/> either side. False when full.</summary>
        public bool AddSegment(Vector2 a, Vector2 b, float halfWidth, Color color)
        {
            Vector2 dir = b - a;
            float len = dir.magnitude;
            if (cursor >= Capacity || len <= 1e-6f)
            {
                return false;
            }

            Vector2 side = new Vector2(-dir.y, dir.x) / len * halfWidth;
            int v = cursor * VertsPerQuad;
            verts[v] = a - side;
            verts[v + 1] = a + side;
            verts[v + 2] = b + side;
            verts[v + 3] = b - side;
            colors[v] = color;
            colors[v + 1] = color;
            colors[v + 2] = color;
            colors[v + 3] = color;
            cursor++;
            return true;
        }

        public void End()
        {
            Mesh.SetVertices(verts, 0, verts.Length, MeshUpdateFlags.DontRecalculateBounds);
            Mesh.SetColors(colors, 0, colors.Length, MeshUpdateFlags.DontRecalculateBounds);
            lastUsed = cursor;
        }
    }
}
