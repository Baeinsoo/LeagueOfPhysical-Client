using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 원 미니맵(Painter2D). 판 한가운데를 지도 중심으로 두고, 카메라가 보는 쪽이 위로 오게 돌려 그린다.
    /// 바위 같은 장애물은 그리지 않는다.
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

        private static void Ring(Painter2D p, Vector2 center, float radius, Color color, float width)
        {
            p.BeginPath();
            p.Arc(center, radius, 0f, 360f);
            p.strokeColor = color;
            p.lineWidth = width;
            p.Stroke();
        }

        private static void Dot(Painter2D p, Vector2 at, float radius, Color color)
        {
            p.BeginPath();
            p.Arc(at, radius, 0f, 360f);
            p.fillColor = color;
            p.Fill();
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
            float scale = (half - 2f) / map.InitialRadius;
            float yaw = cameraYawDeg * Mathf.Deg2Rad;

            p.BeginPath();
            p.Arc(centerPx, half, 0f, 360f);
            p.fillColor = new Color(0f, 0f, 0f, 0.4f);
            p.Fill();

            Ring(p, ToPixel(map.CurrentCenter, scale, centerPx, yaw), map.CurrentRadius * scale,
                 new Color(0.3f, 0.55f, 1f, 0.95f), 3f);
            if (map.HasNext)
            {
                Ring(p, ToPixel(map.NextCenter, scale, centerPx, yaw), map.NextRadius * scale,
                     new Color(1f, 1f, 1f, 0.95f), 2f);
            }

            foreach (var other in map.Others)
            {
                Dot(p, ToPixel(other, scale, centerPx, yaw), 3f, new Color(1f, 0.35f, 0.35f));
            }
            if (map.HasMe)
            {
                Dot(p, ToPixel(map.Me, scale, centerPx, yaw), 4.5f, new Color(1f, 0.9f, 0.2f));
            }
        }
    }
}
