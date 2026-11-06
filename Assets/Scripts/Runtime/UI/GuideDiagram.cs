using UnityEngine;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GuideDiagram : MaskableGraphic
    {
        [SerializeField] Text caption;
        static readonly Color Ink = new Color(0.498f, 0.914f, 0.941f, 1f);
        static readonly Color Warm = new Color(1f, 0.71f, 0.42f, 1f);
        int page;
        float elapsed;
        VertexHelper mesh;

        public void ShowPage(int index)
        {
            page = GuidePages.Clamp(index);
            elapsed = 0f;
            RefreshCaption();
            SetVerticesDirty();
        }

        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            RefreshCaption();
            SetVerticesDirty();
        }

        void RefreshCaption()
        {
            if (caption == null) return;
            switch (page)
            {
                case 0: caption.text = Phase < 0.45f ? "01 / sound travels out" : "02 / echoes return to you"; break;
                case 1: caption.text = Phase < 0.5f ? "voice / space : aimed cone" : "f : short, circular pulse"; break;
                case 2: caption.text = Phase < 0.5f ? "walking : footsteps make noise" : "shift : slower, silent steps"; break;
                case 3: caption.text = "crawler / spike / absorber\nsignatures - not all in the run"; break;
                case 4: caption.text = Phase < 0.5f ? "e : open the door" : "sanctuary : new checkpoint"; break;
                default: caption.text = "illustration / not live input\nnoise floor < gate < your voice"; break;
            }
        }

        float Phase => Mathf.Repeat(elapsed / 6f, 1f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            mesh = vh;
            Color grid = Ink * new Color(1f, 1f, 1f, 0.08f);
            for (int x = -200; x <= 200; x += 40) Line(new Vector2(x, -160), new Vector2(x, 160), grid, 1);
            for (int y = -160; y <= 160; y += 40) Line(new Vector2(-200, y), new Vector2(200, y), grid, 1);
            switch (page)
            {
                case 0: Echo(); break;
                case 1: Pulses(); break;
                case 2: Footsteps(); break;
                case 3: Creatures(); break;
                case 4: Door(); break;
                default: Microphone(); break;
            }
            mesh = null;
        }

        void Echo()
        {
            Vector2 origin = new Vector2(-140, 0);
            Dot(origin, Ink);
            Line(new Vector2(120, -120), new Vector2(120, 120), Ink, 4);
            float p = Phase;
            if (p < 0.45f)
                Arc(origin, Mathf.Lerp(12, 260, p / 0.45f), -25, 25, Ink);
            else
                for (int i = 0; i < 3; i++)
                {
                    float t = (p - 0.45f) / 0.55f;
                    float x = 120 - t * 260 + i * 18;
                    if (x > 120) continue;
                    Color c = Ink; c.a = (1 - t) * (1 - i * 0.2f);
                    for (int j = 0; j < 5; j++)
                        Line(new Vector2(x, -100 + j * 42), new Vector2(x, -70 + j * 42), c);
                }
        }

        void Pulses()
        {
            bool cone = Phase < 0.5f;
            Vector2 origin = cone ? new Vector2(-100, 0) : Vector2.zero;
            float t = Mathf.Repeat(Phase * 2, 1);
            Dot(origin, Ink);
            if (cone)
            {
                Line(origin, origin + Polar(280, -32), new Color(Ink.r, Ink.g, Ink.b, 0.25f));
                Line(origin, origin + Polar(280, 32), new Color(Ink.r, Ink.g, Ink.b, 0.25f));
            }
            for (int i = 0; i < 3; i++)
            {
                float r = Mathf.Repeat(t + i * 0.23f, 1);
                Color c = Ink; c.a = 1 - r;
                Arc(origin, 8 + r * (cone ? 270 : 110), cone ? -32 : 0, cone ? 32 : 360, c);
            }
        }

        void Footsteps()
        {
            bool silent = Phase >= 0.5f;
            float t = Mathf.Repeat(Phase * 2, 1);
            float end = silent ? 20 : 160;
            Vector2 player = new Vector2(Mathf.Lerp(-160, end, t), 0);
            for (int i = 0; i < 12; i++)
            {
                float x = -160 + i * 28;
                if (x >= player.x) break;
                Color c = Warm; c.a = Mathf.Clamp01(1 - (player.x - x) / 220);
                Dot(new Vector2(x, i % 2 == 0 ? -12 : 12), c, 3);
            }
            Dot(player, Ink);
            if (!silent)
            {
                float r = Mathf.Repeat(elapsed * 1.8f, 1);
                Color c = Warm; c.a = 1 - r;
                Arc(player, 10 + r * 40, 0, 360, c);
            }
        }

        void Creatures()
        {
            float beat = 0.7f + 0.3f * Mathf.Sin(elapsed * 5);
            Color c = Warm; c.a = beat;
            Arc(new Vector2(-24, 105), 27, 0, 360, c, 3);
            Arc(new Vector2(40, 105), 27, 0, 360, c, 3);
            for (int i = 0; i < 8; i++)
            {
                float x = -112 + i * 28;
                Line(new Vector2(x, -10), new Vector2(x + 14, 20), c, 3);
                Line(new Vector2(x + 14, 20), new Vector2(x + 28, -10), c, 3);
            }
            float radius = 34 + Mathf.Repeat(elapsed / 2, 1) * 32;
            Arc(new Vector2(0, -108), radius, 35, 145, Ink);
            Arc(new Vector2(0, -108), radius, 215, 325, Ink);
        }

        void Door()
        {
            float t = Phase;
            Line(new Vector2(0, 55), new Vector2(0, 150), Ink, 4);
            Line(new Vector2(0, -150), new Vector2(0, -55), Ink, 4);
            float angle = Mathf.Clamp01((t - 0.15f) / 0.25f) * 90;
            Line(new Vector2(0, 55), new Vector2(0, 55) + Polar(110, -90 + angle), Warm, 4);
            Vector2 goal = new Vector2(140, 0);
            Arc(goal, 32, 0, 360, Ink);
            Arc(goal, 42, 0, 360, new Color(Ink.r, Ink.g, Ink.b, 0.3f));
            float walk = Mathf.Clamp01((t - 0.4f) / 0.4f);
            Dot(Vector2.Lerp(new Vector2(-140, 0), goal, walk), Ink);
            if (walk >= 1) Arc(goal, 45 + Mathf.Repeat(elapsed * 0.8f, 1) * 20, 0, 360, Warm);
        }

        void Microphone()
        {
            Line(new Vector2(-185, -45), new Vector2(185, -45), Warm);
            for (int i = 0; i < 38; i++)
            {
                float x = -185 + i * 10;
                float envelope = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(i * 0.15f - elapsed * 2), 3);
                float height = 12 + envelope * 210 * (0.65f + 0.35f * Mathf.Sin(i * 2.4f + elapsed * 6));
                Line(new Vector2(x, -125), new Vector2(x, -125 + height), height > 80 ? Ink : new Color(Ink.r, Ink.g, Ink.b, 0.3f), 5);
            }
            Line(new Vector2(-185, -125), new Vector2(185, -125), Ink);
        }

        static Vector2 Polar(float radius, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }

        void Dot(Vector2 p, Color c, float radius = 7)
        {
            Arc(p, radius, 0, 360, c, radius);
        }

        void Arc(Vector2 center, float radius, float start, float end, Color c, float width = 2)
        {
            int count = Mathf.CeilToInt((end - start) / 5);
            for (int i = 0; i < count; i++)
                Line(center + Polar(radius, Mathf.Lerp(start, end, (float)i / count)),
                     center + Polar(radius, Mathf.Lerp(start, end, (float)(i + 1) / count)), c, width);
        }

        void Line(Vector2 a, Vector2 b, Color c, float width = 2)
        {
            Rect rect = rectTransform.rect;
            Vector2 scale = new Vector2(rect.width / 440f, rect.height / 360f);
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * (width * 0.5f);
            int index = mesh.currentVertCount;
            mesh.AddVert(rect.center + Vector2.Scale(a - normal, scale), c, Vector2.zero);
            mesh.AddVert(rect.center + Vector2.Scale(a + normal, scale), c, Vector2.zero);
            mesh.AddVert(rect.center + Vector2.Scale(b + normal, scale), c, Vector2.zero);
            mesh.AddVert(rect.center + Vector2.Scale(b - normal, scale), c, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
