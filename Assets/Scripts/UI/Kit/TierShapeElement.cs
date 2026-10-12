using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>티어 도형 하나. 색은 USS의 --tier-color(티어 클래스가 정함).</summary>
    [UxmlElement]
    public partial class TierShapeElement : VisualElement
    {
        private static readonly CustomStyleProperty<Color> ColorProp = new("--tier-color");
        private string _tierClass;

        public TierShape Shape { get; private set; } = TierShape.Circle;
        public Color Fill = new Color32(0x2C, 0x2A, 0x6B, 0xFF);

        public TierShapeElement()
        {
            AddToClassList("tier-shape");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                if (e.customStyle.TryGetValue(ColorProp, out var c)) Fill = c;
                MarkDirtyRepaint();
            });
        }

        public void SetTier(string tierCode)
        {
            if (_tierClass != null) RemoveFromClassList(_tierClass);
            _tierClass = string.IsNullOrEmpty(tierCode) ? null : $"tier-shape--{tierCode}";
            if (_tierClass != null) AddToClassList(_tierClass);
            Shape = TierShapeGeometry.ForTierCode(tierCode);
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var r = new Rect(Vector2.zero, layout.size);
            if (!(r.width > 0f) || !(r.height > 0f)) return;
            var p = ctx.painter2D;
            p.fillColor = Fill;
            p.BeginPath();
            if (Shape == TierShape.Circle)
            {
                p.Arc(r.center, Mathf.Min(r.width, r.height) * 0.5f, 0f, 360f);
            }
            else
            {
                var pts = TierShapeGeometry.Points(Shape, r);
                p.MoveTo(pts[0]);
                for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            }
            p.ClosePath();
            p.Fill();
        }
    }
}
