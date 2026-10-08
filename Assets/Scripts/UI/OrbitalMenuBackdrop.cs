using UnityEngine;
using UnityEngine.UIElements;

namespace Parallax.UI
{
    /// <summary>Resolution-independent station diagram. Repaints only when layout changes.</summary>
    internal sealed class OrbitalMenuBackdrop : VisualElement
    {
        private static readonly Color Cyan = new Color(0.39f, 0.75f, 0.82f, 0.65f);
        private static readonly Color Ghost = new Color(0.51f, 0.43f, 0.72f, 0.22f);
        private static readonly Color Hull = new Color(0.06f, 0.105f, 0.155f);

        public OrbitalMenuBackdrop()
        {
            pickingMode = PickingMode.Ignore;
            style.flexGrow = 1;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 1 || h < 1) return;
            var p = context.painter2D;
            var random = new System.Random(150);
            // A sparse star chart leaves the title and controls legible.
            for (int i = 0; i < 140; i++)
            {
                float x = (float)random.NextDouble() * w, y = (float)random.NextDouble() * h;
                float alpha = Mathf.Lerp(0.09f, 0.45f, (float)random.NextDouble());
                float radius = i % 9 == 0 ? 1.4f : 0.7f;
                Rect(p, new Vector2(x, y), radius * 2, radius * 2,
                    new Color(0.65f, 0.82f, 0.94f, alpha), Color.clear);
            }
            Vector2 c = new Vector2(w * 0.76f, h * 0.48f);
            float size = Mathf.Min(h * 0.60f, w * 0.32f);
            // Two displaced orbital traces evoke the station's two timelines.
            for (int i = 0; i < 3; i++)
                Ellipse(p, c + new Vector2(18, -22), size * (0.75f + i * 0.09f), size * (0.45f + i * 0.05f), Ghost, 1);
            Ellipse(p, c, size, size * 0.65f, new Color(0.22f, 0.43f, 0.57f, 0.21f), 1);
            Ellipse(p, c, size * 0.78f, size * 0.48f, new Color(0.07f, 0.15f, 0.21f), 22);
            Ellipse(p, c, size * 0.80f, size * 0.50f, Cyan, 1.4f);
            Ellipse(p, c, size * 0.74f, size * 0.44f, new Color(0.25f, 0.52f, 0.62f, 0.65f), 1);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                Vector2 rim = Point(c, size * 0.77f, size * 0.47f, angle);
                Line(p, c, rim, new Color(0.09f, 0.18f, 0.25f), 13);
                Line(p, c, rim, new Color(0.25f, 0.46f, 0.57f, 0.6f), 1);
                Rect(p, rim, 18, 13, Hull, Cyan);
            }
            // Solar arrays and the central maintenance spine.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 array = c + new Vector2(side * size * 0.38f, -size * 0.36f);
                Rect(p, array, size * 0.24f, size * 0.37f, new Color(0.075f, 0.12f, 0.20f), new Color(0.29f, 0.40f, 0.59f));
                for (int row = -3; row <= 3; row++)
                    Line(p, array + new Vector2(-size * 0.12f, row * size * 0.046f),
                        array + new Vector2(size * 0.12f, row * size * 0.046f), new Color(0.22f, 0.32f, 0.47f), 1);
                Line(p, array + Vector2.up * size * 0.185f, array - Vector2.up * size * 0.185f, new Color(0.22f, 0.32f, 0.47f), 1);
                Line(p, c + new Vector2(0, -size * 0.25f), array, Cyan, 2);
            }
            Rect(p, c, size * 0.12f, size * 1.15f, Hull, Cyan);
            Rect(p, c, size * 0.26f, size * 0.20f, new Color(0.10f, 0.18f, 0.24f), Cyan);
            Rect(p, c, 8, 8, new Color(0.60f, 0.90f, 0.91f), Color.clear);
            Line(p, c + new Vector2(0, -size * 0.58f), c + new Vector2(0, -size * 0.76f), Cyan, 1);
            // Quiet registration marks reinforce the maintenance-terminal aesthetic.
            for (int i = 0; i < 4; i++)
            {
                Vector2 mark = c + new Vector2((i % 2 == 0 ? -1 : 1) * size, (i < 2 ? -1 : 1) * size * 0.75f);
                Line(p, mark - Vector2.right * 5, mark + Vector2.right * 5, Cyan, 1);
                Line(p, mark - Vector2.up * 5, mark + Vector2.up * 5, Cyan, 1);
            }
        }

        private static Vector2 Point(Vector2 c, float rx, float ry, float a)
        {
            float x = Mathf.Cos(a) * rx, y = Mathf.Sin(a) * ry;
            return c + new Vector2(x * 0.966f - y * 0.259f, x * 0.259f + y * 0.966f);
        }

        private static void Ellipse(Painter2D p, Vector2 c, float rx, float ry, Color color, float width)
        {
            p.strokeColor = color; p.lineWidth = width; p.BeginPath();
            p.MoveTo(Point(c, rx, ry, 0));
            for (int i = 1; i <= 96; i++) p.LineTo(Point(c, rx, ry, i * Mathf.PI * 2 / 96));
            p.ClosePath(); p.Stroke();
        }

        private static void Line(Painter2D p, Vector2 a, Vector2 b, Color color, float width)
        {
            p.strokeColor = color; p.lineWidth = width;
            p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
        }

        private static void Rect(Painter2D p, Vector2 c, float w, float h, Color fill, Color stroke)
        {
            p.BeginPath(); p.MoveTo(c + new Vector2(-w / 2, -h / 2));
            p.LineTo(c + new Vector2(w / 2, -h / 2)); p.LineTo(c + new Vector2(w / 2, h / 2));
            p.LineTo(c + new Vector2(-w / 2, h / 2)); p.ClosePath();
            p.fillColor = fill; p.Fill();
            if (stroke.a > 0) { p.strokeColor = stroke; p.lineWidth = 1; p.Stroke(); }
        }
    }
}
