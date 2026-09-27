using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>결과 화면의 큰 과녁. 띠를 그리고 선수 색 점을 차례로 찍는다(Painter2D).</summary>
    public class ArcheryShootOffResultPanel : VisualElement
    {
        private float faceRadius;
        private IReadOnlyList<ArcheryRingBand> bands = System.Array.Empty<ArcheryRingBand>();
        private readonly List<(Vector2 faceMeters, Color color)> pins = new List<(Vector2, Color)>();
        private int shown;

        public ArcheryShootOffResultPanel()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetFace(float radius, IReadOnlyList<ArcheryRingBand> faceBands)
        {
            faceRadius = radius;
            bands = faceBands ?? System.Array.Empty<ArcheryRingBand>();
            MarkDirtyRepaint();
        }

        public void SetPins(IReadOnlyList<(Vector2 faceMeters, Color color)> newPins)
        {
            pins.Clear();
            pins.AddRange(newPins);
            shown = 0;
            MarkDirtyRepaint();
        }

        public void SetShown(int count)
        {
            if (count != shown)
            {
                shown = count;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            float r = Mathf.Min(rect.width, rect.height) * 0.5f - 6f;
            if (r <= 0f || faceRadius <= 0f)
            {
                return;
            }
            var p = ctx.painter2D;
            Vector2 c = rect.center;

            //  바깥 띠부터 채워 안쪽 띠가 위에 온다. 띠가 없으면 흰 판 하나.
            if (bands.Count == 0)
            {
                Disc(p, c, r, ArcheryShootOffResultLayout.BandColor(0));
            }
            for (int i = bands.Count - 1; i >= 0; i--)
            {
                Disc(p, c, r * bands[i].OuterRatio, ArcheryShootOffResultLayout.BandColor(bands[i].Points));
            }

            for (int i = 0; i < shown && i < pins.Count; i++)
            {
                Vector2 at = ArcheryShootOffResultLayout.ToPanel(pins[i].faceMeters, faceRadius, c, r);
                Disc(p, at, 9f, pins[i].color);
                p.strokeColor = Color.white;
                p.lineWidth = 3f;
                p.BeginPath();
                p.Arc(at, 9f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Stroke();
            }
        }

        private static void Disc(Painter2D p, Vector2 center, float radius, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            p.ClosePath();
            p.Fill();
        }
    }
}
