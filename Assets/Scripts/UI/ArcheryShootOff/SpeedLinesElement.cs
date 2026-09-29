using UnityEngine;
using UnityEngine.UIElements;

namespace LOP
{
    /// <summary>집중선 — 얼굴 쪽(왼쪽 30%)으로 모이는 가시 모양 선. 그림 에셋 없이 매번 같은 모양(고정 시드).</summary>
    public sealed class SpeedLinesElement : VisualElement
    {
        private static readonly Color Cream = new Color(0.953f, 0.937f, 0.902f);
        private static readonly Color Party = new Color(1f, 0.31f, 0.37f);

        public SpeedLinesElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;
            var p = ctx.painter2D;
            var center = new Vector2(r.width * 0.3f, r.height * 0.5f);
            float outer = r.width * 1.2f;
            var rng = new System.Random(7);
            for (int i = 0; i < 160; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float spread = 0.012f + (float)rng.NextDouble() * 0.02f;
                float inner = (0.18f + (float)rng.NextDouble() * 0.25f) * r.width;
                var c = rng.NextDouble() < 0.18 ? Party : Cream;
                c.a = 0.5f + (float)rng.NextDouble() * 0.5f;
                p.fillColor = c;
                p.BeginPath();
                p.MoveTo(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outer);
                p.LineTo(center + new Vector2(Mathf.Cos(a + spread), Mathf.Sin(a + spread)) * outer);
                p.LineTo(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * inner);
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
