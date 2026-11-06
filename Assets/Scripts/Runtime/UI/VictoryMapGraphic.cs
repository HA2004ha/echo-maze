using System.Collections.Generic;
using EchoMaze.Core;
using EchoMaze.Gameplay;
using EchoMaze.Levels;
using UnityEngine;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    public sealed class VictoryMapGraphic : MaskableGraphic
    {
        public static readonly Color Paper = new Color32(239, 242, 237, 255);
        public static readonly Color Ink = new Color32(43, 61, 64, 255);
        public static readonly Color Muted = new Color32(104, 121, 119, 255);
        public static readonly Color Teal = new Color32(30, 141, 121, 255);
        public static readonly Color Amber = new Color32(180, 124, 44, 255);
        public static readonly Color Coral = new Color32(195, 86, 79, 255);
        public static readonly Color Blue = new Color32(75, 124, 171, 255);
        public static readonly Color Violet = new Color32(132, 108, 164, 255);

        struct Shape
        {
            public Vector2 centre;
            public Vector2 size;
            public float radius;
            public float rotation;
            public Color tint;
        }

        readonly List<Shape> shapes = new List<Shape>();
        Camera worldCamera;
        public Bounds MapBounds { get; private set; }
        public int ShapeCount => shapes.Count;

        public void Capture(Camera camera, Player player)
        {
            worldCamera = camera;
            raycastTarget = false;
            shapes.Clear();
            var surfaces = FindObjectsByType<EchoSurface>(FindObjectsSortMode.None);
            var maze = FindAnyObjectByType<MazeLevel>();
            Bounds bounds = new Bounds(player != null ? (Vector3)player.Position : Vector3.zero, Vector3.zero);
            bool found = false;
            foreach (EchoSurface surface in surfaces)
            {
                if (surface.GetComponentInParent<Crawler>() != null) continue;
                Collider2D collider = surface.GetComponent<Collider2D>();
                if (collider == null || collider.isTrigger || !collider.enabled) continue;
                if (!found) { bounds = collider.bounds; found = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            if (maze != null && maze.Grid != null)
                bounds = new Bounds(Vector3.zero, new Vector3(maze.Grid.Width, maze.Grid.Height) * maze.CellSize);
            foreach (Hollow hollow in FindObjectsByType<Hollow>(FindObjectsSortMode.None))
                bounds.Encapsulate(hollow.Area);
            foreach (LevelExit exit in FindObjectsByType<LevelExit>(FindObjectsSortMode.None))
                bounds.Encapsulate(exit.transform.position);
            MapBounds = bounds;
            float unit = maze != null ? maze.CellSize : 1f;

            Box((Vector2)bounds.center + Vector2.down * unit * 0.12f,
                (Vector2)bounds.size + Vector2.one * unit * 0.8f, unit * 0.3f,
                new Color32(217, 224, 216, 255));
            Box(bounds.center, (Vector2)bounds.size + Vector2.one * unit * 0.8f,
                unit * 0.3f, new Color32(251, 252, 248, 255));

            if (maze != null && maze.Grid != null)
            {
                for (int y = 0; y < maze.Grid.Height; y++)
                    for (int x = 0; x < maze.Grid.Width; x++)
                        if (!maze.Grid.IsWall(x, y))
                            Box(maze.Grid.WorldOfCell(x, y), Vector2.one * unit * 0.94f,
                                unit * 0.08f, new Color32(241, 244, 237, 255));
            }

            foreach (NoisyFloor floor in FindObjectsByType<NoisyFloor>(FindObjectsSortMode.None))
            {
                Box(floor.Area.center, floor.Area.size, unit * 0.08f, new Color32(246, 232, 203, 255));
                Vector2 min = floor.Area.min;
                Vector2 max = floor.Area.max;
                for (float x = min.x + unit * 0.25f; x < max.x; x += unit * 0.5f)
                    for (float y = min.y + unit * 0.25f; y < max.y; y += unit * 0.5f)
                        Diamond(new Vector2(x, y), unit * 0.1f, Amber);
            }
            foreach (Hollow hollow in FindObjectsByType<Hollow>(FindObjectsSortMode.None))
            {
                Box(hollow.Area.center, hollow.Area.size, unit * 0.15f, new Color32(232, 225, 241, 255));
                Ring(hollow.Area.center, unit * 0.27f, unit * 0.035f, Violet);
            }
            foreach (Sanctuary sanctuary in FindObjectsByType<Sanctuary>(FindObjectsSortMode.None))
            {
                Box(sanctuary.Area.center, sanctuary.Area.size, unit * 0.15f, new Color32(209, 236, 221, 255));
                Vector2 p = sanctuary.Area.center;
                Line(p - Vector2.right * unit * 0.2f, p + Vector2.right * unit * 0.2f, unit * 0.07f, Teal);
                Line(p - Vector2.up * unit * 0.2f, p + Vector2.up * unit * 0.2f, unit * 0.07f, Teal);
            }

            foreach (EchoSurface surface in surfaces)
            {
                if (surface.GetComponentInParent<Crawler>() != null || surface.GetComponentInParent<Door>() != null)
                    continue;
                Collider2D collider = surface.GetComponent<Collider2D>();
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (collider is CircleCollider2D)
                {
                    Vector2 p = collider.bounds.center;
                    float r = collider.bounds.extents.x;
                    Disc(p + Vector2.down * unit * 0.08f, r, new Color32(184, 195, 187, 255));
                    Disc(p, r, Muted);
                    Ring(p, r * 0.67f, unit * 0.025f, new Color32(218, 227, 218, 255));
                }
                else
                {
                    Vector2 p = collider.bounds.center;
                    Vector2 size = collider.bounds.size;
                    bool crate = collider.gameObject.name.StartsWith("Box");
                    Color tint = crate ? new Color32(168, 150, 119, 255) : Ink;
                    Box(p + Vector2.down * unit * 0.08f, size, unit * 0.045f, new Color32(174, 188, 180, 255));
                    Box(p, size, unit * 0.045f, tint);
                    if (crate)
                    {
                        Vector2 inset = size * 0.3f;
                        Line(p - inset, p + inset, unit * 0.035f, new Color32(233, 221, 193, 255));
                        Line(p + new Vector2(-inset.x, inset.y), p + new Vector2(inset.x, -inset.y),
                            unit * 0.035f, new Color32(233, 221, 193, 255));
                    }
                    else if (size.x > unit || size.y > unit)
                    {
                        Box(p + new Vector2(0f, size.y * 0.5f - unit * 0.06f),
                            new Vector2(Mathf.Max(0.02f, size.x - unit * 0.1f), unit * 0.025f), 0f,
                            new Color32(94, 112, 110, 255));
                    }
                }
            }
            foreach (Door door in FindObjectsByType<Door>(FindObjectsSortMode.None))
            {
                Vector2 p = door.transform.position;
                Collider2D[] panels = door.GetComponentsInChildren<Collider2D>();
                Vector2 direction = Vector2.up;
                if (panels.Length > 2)
                    direction = ((Vector2)panels[2].transform.position - (Vector2)panels[1].transform.position).normalized;
                if (direction.sqrMagnitude < 0.1f) direction = Vector2.up;
                float reach = unit * 0.42f;
                Line(p - direction * reach, p - direction * reach * 0.55f, unit * 0.12f, Amber);
                Line(p + direction * reach * 0.55f, p + direction * reach, unit * 0.12f, Amber);
                if (door.IsClosed) Line(p - direction * reach, p + direction * reach, unit * 0.09f, Amber);
                else
                    for (int i = -1; i <= 1; i++) Disc(p + direction * i * unit * 0.15f, unit * 0.035f, Amber);
            }
            if (maze != null && maze.Grid != null && maze.Grid.FindFirst(MazeGrid.Spawn, out Vector2Int spawn))
                Ring(maze.Grid.WorldOfCell(spawn.x, spawn.y), unit * 0.23f, unit * 0.045f, Blue);
            foreach (LevelExit exit in FindObjectsByType<LevelExit>(FindObjectsSortMode.None))
            {
                Vector2 p = exit.transform.position;
                Box(p, Vector2.one * unit * 0.72f, unit * 0.14f, Teal);
                Line(p - Vector2.right * unit * 0.21f, p + Vector2.right * unit * 0.19f, unit * 0.055f, Color.white);
                Line(p + new Vector2(0.04f, 0.15f) * unit, p + Vector2.right * unit * 0.2f, unit * 0.055f, Color.white);
                Line(p + new Vector2(0.04f, -0.15f) * unit, p + Vector2.right * unit * 0.2f, unit * 0.055f, Color.white);
            }
            foreach (Crawler crawler in FindObjectsByType<Crawler>(FindObjectsSortMode.None))
            {
                Vector2 p = crawler.transform.position;
                Disc(p, unit * 0.5f, new Color32(249, 223, 214, 255));
                for (int i = -1; i <= 1; i++)
                {
                    float y = i * unit * 0.16f;
                    Line(p + new Vector2(-0.17f * unit, y), p + new Vector2(-0.34f * unit, y + unit * 0.12f), unit * 0.045f, Coral);
                    Line(p + new Vector2(0.17f * unit, y), p + new Vector2(0.34f * unit, y + unit * 0.12f), unit * 0.045f, Coral);
                }
                Box(p, new Vector2(0.34f, 0.6f) * unit, unit * 0.16f, Coral);
                Disc(p + new Vector2(-0.065f, 0.15f) * unit, unit * 0.035f, Color.white);
                Disc(p + new Vector2(0.065f, 0.15f) * unit, unit * 0.035f, Color.white);
            }
            if (player != null)
            {
                Vector2 p = player.Position;
                Disc(p, unit * 0.25f, Color.white);
                Disc(p, unit * 0.19f, Blue);
                Disc(p + Vector2.up * unit * 0.045f, unit * 0.06f, Color.white);
            }
            SetVerticesDirty();
        }

        void Box(Vector2 centre, Vector2 size, float radius, Color tint, float rotation = 0f) =>
            shapes.Add(new Shape { centre = centre, size = size, radius = radius, tint = tint, rotation = rotation });
        void Disc(Vector2 p, float radius, Color tint) => Box(p, Vector2.one * radius * 2f, radius, tint);
        void Diamond(Vector2 p, float radius, Color tint) => Box(p, Vector2.one * radius * 2f, 0f, tint, 45f);
        void Line(Vector2 a, Vector2 b, float width, Color tint) =>
            Box((a + b) * 0.5f, new Vector2(Vector2.Distance(a, b), width), width * 0.5f, tint,
                Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        void Ring(Vector2 p, float radius, float width, Color tint)
        {
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI / 16f;
                float b = (i + 1) * Mathf.PI / 16f;
                Line(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    p + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, tint);
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (worldCamera == null) return;
            Rect rect = rectTransform.rect;
            float scale = rect.height / (2f * worldCamera.orthographicSize);
            Vector2 origin = rect.center - (Vector2)worldCamera.transform.position * scale;
            foreach (Shape shape in shapes)
            {
                Vector2 half = shape.size * 0.5f;
                float r = Mathf.Min(shape.radius, Mathf.Min(half.x, half.y));
                int start = vh.currentVertCount;
                Quaternion rotation = Quaternion.Euler(0f, 0f, shape.rotation);
                vh.AddVert(origin + shape.centre * scale, shape.tint, Vector2.zero);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 centre = new Vector2(corner == 0 || corner == 3 ? half.x - r : -half.x + r,
                        corner < 2 ? half.y - r : -half.y + r);
                    for (int step = 0; step <= 4; step++)
                    {
                        float angle = (corner * 90f + step * 22.5f) * Mathf.Deg2Rad;
                        Vector2 point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                        Vector2 local = rotation * point;
                        vh.AddVert(origin + (shape.centre + local) * scale, shape.tint, Vector2.zero);
                    }
                }
                for (int i = 0; i < 20; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 20);
            }
        }
    }
}
