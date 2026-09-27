using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 결과 화면의 큰 과녁. 띠를 그리고 선수 색 점을 차례로 찍는다(Painter2D). 점이 있는 곳을 확대해
    /// 그린다 — 가장자리가 과녁 위 <c>viewRadius</c>미터다. 가장자리 밖으로 나가는 띠는 가장자리에서 자른다.
    /// </summary>
    public class ArcheryShootOffResultPanel : VisualElement
    {
        private float faceRadius;
        private float viewRadius;
        private IReadOnlyList<ArcheryRingBand> bands = System.Array.Empty<ArcheryRingBand>();
        private readonly List<(Vector2 faceMeters, Color color)> pins = new List<(Vector2, Color)>();
        private int shown;

        public ArcheryShootOffResultPanel()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetFace(float radius, IReadOnlyList<ArcheryRingBand> faceBands, float shownRadius)
        {
            faceRadius = radius;
            viewRadius = shownRadius;
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
            if (r <= 0f || faceRadius <= 0f || viewRadius <= 0f)
            {
                return;
            }
            var p = ctx.painter2D;
            Vector2 c = rect.center;
            float pixelsPerMeter = r / viewRadius;

            //  바깥 띠부터 채워 안쪽 띠가 위에 온다. 띠가 없으면 흰 판 하나.
            if (bands.Count == 0)
            {
                Disc(p, c, Mathf.Min(faceRadius * pixelsPerMeter, r), ArcheryShootOffResultLayout.BandColor(0));
            }
            for (int i = bands.Count - 1; i >= 0; i--)
            {
                Disc(p, c, Mathf.Min(bands[i].OuterRatio * faceRadius * pixelsPerMeter, r),
                     ArcheryShootOffResultLayout.BandColor(bands[i].Points));
            }

            for (int i = 0; i < shown && i < pins.Count; i++)
            {
                Vector2 at = ArcheryShootOffResultLayout.ToPanel(pins[i].faceMeters, viewRadius, c, r);
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
