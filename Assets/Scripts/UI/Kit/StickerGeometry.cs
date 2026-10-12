using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 스티커 모양의 꼭짓점. 앞면은 윗변이 오른쪽으로 밀린 평행사변형이고(시안의 clip-path),
    /// 뒷면은 같은 모양을 오른쪽 아래로 옮긴 것이다. 그리는 일은 StickerElement가 한다.
    /// </summary>
    public static class StickerGeometry
    {
        /// <summary>[왼아래, 오른아래, 오른위, 왼위]. 크기가 없으면 빈 배열(레이아웃 전).</summary>
        public static Vector2[] Face(Rect r, float tilt, float tailPx)
        {
            if (!(r.width > 0f) || !(r.height > 0f)) return System.Array.Empty<Vector2>();

            float shift = tailPx > 0f ? tailPx : tilt * r.width;
            //  절반을 넘기면 윗변과 아랫변이 엇갈려 모양이 뒤집힌다.
            shift = Mathf.Clamp(shift, 0f, r.width * 0.5f);

            return new[]
            {
                new Vector2(r.xMin, r.yMax),
                new Vector2(r.xMax - shift, r.yMax),
                new Vector2(r.xMax, r.yMin),
                new Vector2(r.xMin + shift, r.yMin),
            };
        }

        public static Vector2[] Offset(Vector2[] pts, float dx, float dy)
        {
            var d = new Vector2(dx, dy);
            var result = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) result[i] = pts[i] + d;
            return result;
        }
    }
}
