using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>목숨 하트 하나(Painter2D). 차 있으면 빨강, 잃었으면 어두운 속에 옅은 테두리만 — 그림 에셋 없이.</summary>
    public sealed class HeartElement : VisualElement
    {
        private static readonly Color Full = new Color(1f, 0.31f, 0.37f);
        private static readonly Color EmptyFill = new Color(0f, 0f, 0f, 0.35f);
        private static readonly Color EmptyEdge = new Color(1f, 1f, 1f, 0.45f);
        private bool filled;

        public HeartElement()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("heart");
            generateVisualContent += Draw;
        }

        public bool Filled
        {
            get => filled;
            set
            {
                if (filled == value) return;
                filled = value;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;
            var p = ctx.painter2D;
            Vector2 P(float x, float y) => new Vector2(r.width * x, r.height * y);
            p.BeginPath();
            p.MoveTo(P(0.5f, 0.95f));
            p.BezierCurveTo(P(0.2f, 0.72f), P(0f, 0.5f), P(0.05f, 0.3f));
            p.BezierCurveTo(P(0.1f, 0.05f), P(0.42f, 0.02f), P(0.5f, 0.25f));
            p.BezierCurveTo(P(0.58f, 0.02f), P(0.9f, 0.05f), P(0.95f, 0.3f));
            p.BezierCurveTo(P(1f, 0.5f), P(0.8f, 0.72f), P(0.5f, 0.95f));
            p.ClosePath();
            p.fillColor = filled ? Full : EmptyFill;
            p.Fill();
            if (!filled)
            {
                p.strokeColor = EmptyEdge;
                p.lineWidth = 2f;
                p.Stroke();
            }
        }
    }
}
