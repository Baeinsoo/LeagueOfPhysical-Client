using UnityEngine;

namespace LOP.UI
{
    public enum TierShape { Circle, Triangle, RoundedSquare, Star, Hexagon, Pentagon, Diamond, Crown }

    /// <summary>티어 칩 도형의 꼭짓점. 엠블럼 그림이 오기 전까지 이 도형이 티어를 표시한다.</summary>
    public static class TierShapeGeometry
    {
        public static TierShape ForTierCode(string code) => code switch
        {
            "bronze" => TierShape.Triangle,
            "silver" => TierShape.RoundedSquare,
            "gold" => TierShape.Star,
            "platinum" => TierShape.Hexagon,
            "emerald" => TierShape.Pentagon,
            "diamond" => TierShape.Diamond,
            "master" => TierShape.Crown,
            _ => TierShape.Circle,
        };

        public static Vector2[] Points(TierShape s, Rect r)
        {
            if (!(r.width > 0f) || !(r.height > 0f) || s == TierShape.Circle) return System.Array.Empty<Vector2>();
            var c = r.center;
            float rx = r.width * 0.5f, ry = r.height * 0.5f;
            switch (s)
            {
                case TierShape.Triangle: return Regular(c, rx, ry, 3);
                case TierShape.Pentagon: return Regular(c, rx, ry, 5);
                case TierShape.Hexagon: return Regular(c, rx, ry, 6);
                case TierShape.Diamond: return Regular(c, rx, ry, 4);
                case TierShape.RoundedSquare:
                    return new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
                case TierShape.Star:
                {
                    var pts = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float k = i % 2 == 0 ? 1f : 0.45f;
                        float a = -Mathf.PI / 2f + i * Mathf.PI / 5f;
                        pts[i] = c + new Vector2(Mathf.Cos(a) * rx * k, Mathf.Sin(a) * ry * k);
                    }
                    return pts;
                }
                case TierShape.Crown:
                    return new[]
                    {
                        new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax),
                        new Vector2(r.xMax, r.yMin + r.height * 0.15f), new Vector2(r.xMin + r.width * 0.72f, r.yMin + r.height * 0.55f),
                        new Vector2(c.x, r.yMin), new Vector2(r.xMin + r.width * 0.28f, r.yMin + r.height * 0.55f),
                        new Vector2(r.xMin, r.yMin + r.height * 0.15f),
                    };
            }
            return System.Array.Empty<Vector2>();
        }

        //  꼭짓점 하나가 위를 향하는 정다각형.
        private static Vector2[] Regular(Vector2 c, float rx, float ry, int n)
        {
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = -Mathf.PI / 2f + i * 2f * Mathf.PI / n;
                pts[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
            }
            return pts;
        }
    }
}
