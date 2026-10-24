using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 원 미니맵(Painter2D). 나를 지도 중심에 두고, 카메라가 보는 쪽이 위로 오게 돌려 그린다.
    /// 현재 원·다음 원만 그리고 남은 그리지 않는다. 바위 같은 장애물도 그리지 않는다.
    /// </summary>
    public sealed class MountedMinimapElement : VisualElement
    {
        private MountedCombatHudViewModel.Minimap map;
        private bool hasMap;
        private float cameraYawDeg;

        public MountedMinimapElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(MountedCombatHudViewModel.Minimap value, float cameraYaw)
        {
            map = value;
            cameraYawDeg = cameraYaw;
            hasMap = true;
            MarkDirtyRepaint();
        }

        // 월드 (x, z)를 화면 점으로. 카메라 방위가 위쪽이 되도록 돌린다.
        private static Vector2 ToPixel(Vector2 world, float scale, Vector2 centerPx, float yawRad)
        {
            float sin = Mathf.Sin(yawRad), cos = Mathf.Cos(yawRad);
            float right = world.x * cos - world.y * sin;
            float up = world.x * sin + world.y * cos;
            return centerPx + new Vector2(right, -up) * scale;
        }

        /// <summary>디스크 밖으로 나간 점은 디스크 가장자리로 끌어당긴다.</summary>
        public static Vector2 ClampToDisc(Vector2 point, Vector2 discCenter, float discRadius)
        {
            Vector2 d = point - discCenter;
            float len = d.magnitude;
            return len <= discRadius || len < 1e-6f ? point : discCenter + d * (discRadius / len);
        }

        // 원을 선분으로 쪼개 그리되, 디스크 밖 점은 가장자리에 붙인다.
        private static void Ring(Painter2D p, Vector2 center, float radius, Vector2 discCenter, float discRadius,
                                 Color color, float width)
        {
            const int Segments = 72;
            p.BeginPath();
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                var pt = ClampToDisc(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, discCenter, discRadius);
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.strokeColor = color;
            p.lineWidth = width;
            p.Stroke();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (hasMap == false || r.width <= 0f || map.InitialRadius <= 0f)
            {
                return;
            }
            var p = ctx.painter2D;
            float half = Mathf.Min(r.width, r.height) * 0.5f;
            var centerPx = new Vector2(r.width * 0.5f, r.height * 0.5f);
            float discRadius = half - 2f;
            // 지도는 나를 한가운데에 두고, 카메라가 보는 쪽이 위로 가게 돌린다.
            float scale = discRadius / map.InitialRadius;
            float yaw = cameraYawDeg * Mathf.Deg2Rad;
            Vector2 origin = map.HasMe ? map.Me : Vector2.zero;

            p.BeginPath();
            p.Arc(centerPx, half, 0f, 360f);
            p.fillColor = new Color(0f, 0f, 0f, 0.4f);
            p.Fill();

            Ring(p, ToPixel(map.CurrentCenter - origin, scale, centerPx, yaw), map.CurrentRadius * scale,
                 centerPx, discRadius, new Color(0.3f, 0.55f, 1f, 0.95f), 3f);
            if (map.HasNext)
            {
                Ring(p, ToPixel(map.NextCenter - origin, scale, centerPx, yaw), map.NextRadius * scale,
                     centerPx, discRadius, new Color(1f, 1f, 1f, 0.95f), 2f);
            }

            // 나는 가운데 위쪽을 향한 삼각형(화면이 내가 보는 쪽 기준이라 늘 위).
            p.BeginPath();
            p.MoveTo(centerPx + new Vector2(0f, -7f));
            p.LineTo(centerPx + new Vector2(5f, 5f));
            p.LineTo(centerPx + new Vector2(-5f, 5f));
            p.ClosePath();
            p.fillColor = new Color(1f, 0.9f, 0.2f);
            p.Fill();
        }
    }
}
