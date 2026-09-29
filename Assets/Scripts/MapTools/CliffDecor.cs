using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>
    /// 절벽 예고·잔해(spec 2026-09-29 §4). 판정은 안 바꾸고 렌더 전용 조각의 자리만 낸다 — 경고 줄무늬, ⚠ 표지판,
    /// 부러진 교각 조각과 철근. 모두 월드 좌표(절벽 위 바닥 윗면 = TopY − half).
    /// </summary>
    public static class CliffDecor
    {
        public const int StripeCount = 16;
        public const float SignDistance = 8f;

        static float Top(CliffPiece c, float half) => c.TopY - half;

        /// <summary>끝 16m 앞부터 끝까지 1m 칸. 짝수 번째가 노랑, 홀수 번째가 검정.</summary>
        public static List<Box2> Stripes(CliffPiece c, float half)
        {
            float top = Top(c, half);
            var boxes = new List<Box2>();
            for (int i = 0; i < StripeCount; i++)
            {
                float x0 = c.Edge - StripeCount + i;
                boxes.Add(new Box2(x0, top - 0.05f, x0 + 1f, top + 0.12f));
            }
            return boxes;
        }

        public static Box2 SignPost(CliffPiece c, float half)
        {
            float top = Top(c, half), x = c.Edge - SignDistance;
            return new Box2(x - 0.1f, top, x + 0.1f, top + 2.2f);
        }

        public static float[] SignTriangle(CliffPiece c, float half)
        {
            float top = Top(c, half), x = c.Edge - SignDistance, y = top + 2.9f;
            return new[] { x - 0.7f, y - 0.6f, x + 0.7f, y - 0.6f, x, y + 0.6f };
        }

        /// <summary>절벽 면 윗부분에 매달린 콘크리트 조각 셋(볼록 사각형, 아래로 갈수록 덜 튀어나온다).</summary>
        public static List<float[]> BrokenChunks(CliffPiece c, float half)
        {
            float top = Top(c, half), e = c.Edge;
            var quads = new List<float[]>();
            for (int k = 0; k < 3; k++)
            {
                float yt = top - 1.6f * k, yb = yt - 1.5f, reach = 0.5f - 0.12f * k;
                quads.Add(new[] { e - 0.6f, yb, e + reach, yb + 0.4f, e + reach - 0.1f, yt, e - 0.6f, yt });
            }
            return quads;
        }

        /// <summary>절벽 면에서 삐져나온 철근 — 가는 평행사변형.</summary>
        public static List<float[]> Rebars(CliffPiece c, float half)
        {
            float top = Top(c, half), e = c.Edge;
            var bars = new List<float[]>();
            for (int k = 0; k < 5; k++)
            {
                float y = top - 0.4f - 0.9f * k, len = 0.9f - 0.1f * k, rise = 0.25f - 0.08f * k;
                bars.Add(new[] { e, y, e + len, y + rise, e + len, y + rise + 0.06f, e, y + 0.06f });
            }
            return bars;
        }
    }
}
