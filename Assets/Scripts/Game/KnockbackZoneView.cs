using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 넉백 판의 원 그림: 지금 원은 빨간 고리(줄어드는 중엔 깜박임), 서버가 공개한 다음 원은 흰 고리.
    /// 원 밖 칸은 바닥 쪽(<see cref="KnockbackFloorView"/>)에서 무너져 사라지므로 여기선 선만 긋는다.
    /// 그림일 뿐이라 콜라이더가 없다.
    /// </summary>
    public class KnockbackZoneView : ILateTickable, System.IDisposable
    {
        private const int Segments = 96;
        private const float LineY = 0.15f;
        private const float BlinkSeconds = 0.2f;

        private readonly GameFramework.Runner.IRunner runner;
        private readonly KnockbackWorld world;
        private readonly KnockbackClientState state;

        private LineRenderer current;
        private LineRenderer next;
        private Material currentMaterial;
        private Material nextMaterial;
        private GameObject root;
        private readonly Vector3[] points = new Vector3[Segments];

        public KnockbackZoneView(GameFramework.Runner.IRunner runner, KnockbackWorld world, KnockbackClientState state)
        {
            this.runner = runner;
            this.world = world;
            this.state = state;
        }

        public void LateTick()
        {
            if (runner?.tickUpdater == null)
            {
                return;
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;

            EnsureObjects();
            if (root == null)
            {
                return;
            }

            var stages = state.ZoneStages;
            long lo = (long)System.Math.Floor(renderTick);

            // 서버가 아직 아무 단계도 안 알렸으면 처음 판이 그대로라 선을 그을 필요가 없다.
            current.enabled = stages.Count > 0;
            if (current.enabled)
            {
                var circle = CircleAtRender(stages, renderTick, world.Floor.ZoneStartRadius);
                Draw(current, circle.center, circle.radius);
                bool blinkOff = IsShrinking(stages, lo) && ((int)(Time.unscaledTime / BlinkSeconds) & 1) == 1;
                currentMaterial.color = new Color(1f, 0.15f, 0.15f, blinkOff ? 0.35f : 1f);
            }

            bool hasNext = state.TryGetNextCircle(lo, out var nextCircle);
            next.enabled = hasNext;
            if (hasNext)
            {
                Draw(next, nextCircle.center, nextCircle.radius);
            }
        }

        /// <summary>식은 정수 틱만 받으므로 앞뒤 두 틱에서 구해 사이를 잇는다 — 안 그러면 원이 틱마다 계단으로 줄어든다.</summary>
        internal static (Vector2 center, float radius) CircleAtRender(IReadOnlyList<ZoneStage> stages, double tick, float startRadius)
        {
            long lo = (long)System.Math.Floor(tick);
            float frac = (float)(tick - lo);
            var a = MountedArcheryZone.CircleAt(stages, lo, Vector2.zero, startRadius);
            var b = MountedArcheryZone.CircleAt(stages, lo + 1, Vector2.zero, startRadius);
            return (Vector2.Lerp(a.center, b.center, frac), Mathf.Lerp(a.radius, b.radius, frac));
        }

        private static bool IsShrinking(IReadOnlyList<ZoneStage> stages, long tick)
        {
            for (int i = 0; i < stages.Count; i++)
            {
                if (tick >= stages[i].shrinkStartTick && tick < stages[i].shrinkEndTick) return true;
            }
            return false;
        }

        private void Draw(LineRenderer line, Vector2 center, float radius)
        {
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                points[i] = new Vector3(center.x + Mathf.Cos(a) * radius, LineY, center.y + Mathf.Sin(a) * radius);
            }
            line.SetPositions(points);
        }

        private void EnsureObjects()
        {
            if (root != null)
            {
                return;
            }
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return;   // 없으면 선을 안 긋는다 — 판정은 서버가 한다
            }
            currentMaterial = new Material(shader) { color = new Color(1f, 0.15f, 0.15f, 1f) };
            nextMaterial = new Material(shader) { color = Color.white };

            root = new GameObject("KnockbackZone");
            current = MakeLine("Current", currentMaterial, 0.35f);
            next = MakeLine("Next", nextMaterial, 0.25f);
        }

        private LineRenderer MakeLine(string name, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Segments;
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            return line;
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
            if (currentMaterial != null) Object.Destroy(currentMaterial);
            if (nextMaterial != null) Object.Destroy(nextMaterial);
        }
    }
}
