using UnityEngine;
using UnityEngine.UIElements;

namespace Parallax.UI
{
    /// <summary>Two baked views of one laboratory; no scene loading, render textures or update loop.</summary>
    internal sealed class TemporalMenuBackdrop : VisualElement
    {
        readonly Texture2D past, future;
        public TemporalMenuBackdrop(Texture2D past, Texture2D future)
        {
            this.past = past; this.future = future;
            pickingMode = PickingMode.Ignore; style.flexGrow = 1;
            generateVisualContent += Draw;
        }
        void Draw(MeshGenerationContext context)
        {
            if (past == null || future == null || contentRect.width < 1 || contentRect.height < 1) return;
            const int rows = 24, columns = 20;
            float w = contentRect.width, h = contentRect.height;
            // A continuous irregular seam, with a narrow offset echo of intact architecture.
            // Group textures into two passes so the renderer can batch the geometry.
            for (int layer = 0; layer < 2; layer++)
            for (int row = 0; row < rows; row++)
            {
                float y0 = h * row / rows, y1 = h * (row + 1) / rows;
                float cut0 = w * (.58f + .045f * Mathf.Sin(row * .19f) + .012f * Mathf.Sin(row * .71f));
                float cut1 = w * (.58f + .045f * Mathf.Sin((row + 1) * .19f) + .012f * Mathf.Sin((row + 1) * .71f));
                for (int col = 0; col < columns; col++)
                {
                    float x0 = w * col / columns, x1 = w * (col + 1) / columns;
                    if (layer == 0) Quad(context, future, x0, x1, x0, x1, y0, y1, .90f, 0);
                    else
                    {
                        float left0 = Mathf.Min(x0, cut0), right0 = Mathf.Min(x1, cut0);
                        float left1 = Mathf.Min(x0, cut1), right1 = Mathf.Min(x1, cut1);
                        if (right0 > left0 || right1 > left1)
                            Quad(context, past, left0, right0, left1, right1, y0, y1, .57f, 0);
                    }
                }
                if (layer == 1) Quad(context, past, cut0, cut0 + 6, cut1, cut1 + 6, y0, y1, .50f, -6);
            }
        }
        void Quad(MeshGenerationContext context, Texture2D texture, float l0, float r0, float l1, float r1, float y0, float y1, float brightness, float offset)
        {
            var mesh = context.Allocate(4, 6, texture);
            mesh.SetAllVertices(new[] { V(texture, l0, y0, brightness, offset), V(texture, r0, y0, brightness, offset), V(texture, r1, y1, brightness, offset), V(texture, l1, y1, brightness, offset) });
            mesh.SetAllIndices(new ushort[] { 0, 1, 2, 2, 3, 0 });
        }
        Vertex V(Texture2D texture, float x, float y, float brightness, float offset)
        {
            float w = contentRect.width, h = contentRect.height;
            float cover = Mathf.Max(w / texture.width, h / texture.height);
            float u = ((x + offset) + (texture.width * cover - w) * .5f) / (texture.width * cover);
            float v = (y + (texture.height * cover - h) * .5f) / (texture.height * cover);
            float edge = Mathf.SmoothStep(0, 1, Mathf.Clamp01(x / (w * .33f)));
            edge *= Mathf.SmoothStep(0, 1, Mathf.Clamp01(y / (h * .12f))) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((h - y) / (h * .14f)));
            return new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = new Color(brightness, brightness, brightness, edge), uv = new Vector2(u, 1 - v) };
        }
    }
}
