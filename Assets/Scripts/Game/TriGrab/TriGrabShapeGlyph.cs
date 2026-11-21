using UnityEngine;
using UnityEngine.UIElements;

namespace LOP
{
    /// <summary>
    /// 카드 모양(원·세모·네모)을 그리는 위젯. 자기 칸 가운데에 칸 높이만 한 도형을 채우고 테두리를 두른다.
    /// 어떤 카드인지는 모른다 — 모양과 색만 받는다.
    /// </summary>
    public sealed class TriGrabShapeGlyph : VisualElement
    {
        private int shape = -1;
        private Color fill = Color.white;
        private Color stroke = Color.black;
        private float strokeWidth = 6f;

        public TriGrabShapeGlyph()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public int Shape => shape;

        /// <summary>바뀐 것이 있을 때만 다시 그린다. <paramref name="shapeIndex"/>가 음수면 아무것도 안 그린다.</summary>
        public void Set(int shapeIndex, Color fillColor, Color strokeColor, float width)
        {
            if (shape == shapeIndex && fill == fillColor && stroke == strokeColor && Mathf.Approximately(strokeWidth, width)) return;
            shape = shapeIndex;
            fill = fillColor;
            stroke = strokeColor;
            strokeWidth = width;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (shape < 0) return;
            var rect = contentRect;
            float size = Mathf.Min(rect.width, rect.height) - strokeWidth;
            if (size <= 0f) return;
            var center = rect.center;
            float half = size * 0.5f;

            var painter = context.painter2D;
            painter.fillColor = fill;
            painter.strokeColor = stroke;
            painter.lineWidth = strokeWidth;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            switch (shape)
            {
                case (int)TriGrabShape.Circle:
                    painter.Arc(center, half, 0f, 360f);
                    break;
                case (int)TriGrabShape.Triangle:
                    //  세모는 가로를 넓혀 글자가 들어갈 자리를 만든다.
                    painter.MoveTo(new Vector2(center.x, center.y - half));
                    painter.LineTo(new Vector2(center.x + half * 1.25f, center.y + half));
                    painter.LineTo(new Vector2(center.x - half * 1.25f, center.y + half));
                    break;
                default:
                    painter.MoveTo(new Vector2(center.x - half, center.y - half));
                    painter.LineTo(new Vector2(center.x + half, center.y - half));
                    painter.LineTo(new Vector2(center.x + half, center.y + half));
                    painter.LineTo(new Vector2(center.x - half, center.y + half));
                    break;
            }
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }
    }
}
