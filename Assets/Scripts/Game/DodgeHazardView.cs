using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 위험의 바닥 층 — "어디가 위험한가"를 읽히게 하는 반투명 표시(그림자·과즙 원·줄 자리·온돌 칸·예고).
    /// 테마 물건(슬리퍼·수박·장독·줄)은 <see cref="DodgePropView"/>가 따로 그린다.
    /// 판정과 같은 식에 **내 몸이 그려지는 틱**을 넣으므로, 보이는 것이 곧 서버 판정이다.
    /// MonoBehaviour가 아니다 — 서버 씬에서 빠진 스크립트가 되지 않게(SkydiveLaserView와 같은 이유).
    /// </summary>
    public class DodgeHazardView : ILateTickable, IDisposable
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly DodgeClientState state;
        private readonly DodgeConfig config;
        private readonly DodgePropKit kit;
        private readonly List<DodgeShape> shapes = new List<DodgeShape>();
        // 모양별로 나눈 풀 — 한 풀을 번갈아 쓰면 칸 수가 바뀔 때마다 부수고 다시 만든다.
        private readonly List<Renderer> discs = new List<Renderer>();
        private readonly List<Renderer> boxes = new List<Renderer>();
        private GameObject root;

        public DodgeHazardView(GameFramework.Runner.IRunner runner, DodgeClientState state, DodgeConfig config, DodgePropKit kit)
        {
            this.runner = runner;
            this.state = state;
            this.config = config;
            this.kit = kit;
        }

        public void LateTick()
        {
            if (kit == null || runner?.tickUpdater == null)
            {
                return;
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;   // Run 전 — 나누면 NaN
            }

            // tickUpdater.tick은 "다음에 계산할 틱"이라 내 몸보다 한 틱 앞이다. 몸이 그려지는 시각에 맞춘다.
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            long tick = ShapeTick(renderTick, out float frac);

            shapes.Clear();
            foreach (var p in state.Patterns)
            {
                DodgeHazards.Shapes(p, tick, config, shapes);
            }

            root ??= new GameObject("DodgeHazards");
            int usedDiscs = 0, usedBoxes = 0;
            foreach (var s in shapes)
            {
                if (!DodgePropPose.DrawnOnGround(s))
                {
                    continue;   // 슬리퍼·굴러가는 장독은 물건 층
                }
                var r = s.Type == DodgeShapeType.Circle
                    ? Take(discs, usedDiscs++, PrimitiveType.Cylinder)
                    : Take(boxes, usedBoxes++, PrimitiveType.Cube);
                Place(r, s, frac);
            }
            for (int i = usedDiscs; i < discs.Count; i++) discs[i].enabled = false;
            for (int i = usedBoxes; i < boxes.Count; i++) boxes[i].enabled = false;
        }

        // 몸은 틱 T의 결과를 시각 T에 두고 T→T+1을 섞는다. 도형 틱 T+1은 X1=b(T), X0=b(T+1)이라
        // 같은 frac로 섞으면 몸과 같은 순간이다. 켜짐·꺼짐도 T+1 기준 — 지금 보이는 구간을 재는 판정이 T+1이다.
        public static long ShapeTick(double renderTick, out float frac)
        {
            long tick = (long)Math.Floor(renderTick);
            frac = (float)(renderTick - tick);
            return tick + 1;
        }

        public static Vector2 CircleCenter(DodgeShape s, float frac) =>
            new Vector2(Mathf.Lerp(s.X1, s.X0, frac), Mathf.Lerp(s.Z1, s.Z0, frac));

        // 판정이 원이면 원기둥(납작한 원판)으로 — 상자면 네 모서리가 "보이는데 안 맞는" 자리가 된다.
        public static PrimitiveType GroundPrimitive(DodgeShapeType type) =>
            type == DodgeShapeType.Circle ? PrimitiveType.Cylinder : PrimitiveType.Cube;

        private void Place(Renderer r, DodgeShape s, float frac)
        {
            var t = r.transform;
            r.enabled = true;
            switch (s.Type)
            {
                case DodgeShapeType.Circle:
                {
                    // 폭탄: 예고는 커지는 수박 그림자, 켜지면 과즙 원. 바위: 들어올 자리의 예고 원.
                    var c = CircleCenter(s, frac);
                    bool bomb = s.Kind == DodgePatternKind.Bomb;
                    r.sharedMaterial = s.Active ? kit.juice : bomb ? kit.shadow : kit.warn;
                    float d = s.Radius * 2f * (s.Active ? 1f : Mathf.Max(0.15f, s.Progress));
                    t.position = new Vector3(c.x, 0.03f, c.y);
                    t.rotation = Quaternion.identity;
                    t.localScale = new Vector3(d, 0.02f, d);   // 원기둥 높이는 2 — 두께 0.04
                    break;
                }
                case DodgeShapeType.Segment:
                {
                    // 줄넘기 자리: 예고는 가는 선, 켜지면 판정 폭 그대로의 빨간 띠. 줄 자체는 물건 층.
                    var a = new Vector3(s.X0, 0.04f, s.Z0);
                    var b = new Vector3(s.X1, 0.04f, s.Z1);
                    r.sharedMaterial = s.Active ? kit.hot : kit.warn;
                    float width = s.Active ? s.Radius * 2f : 0.08f + 0.12f * s.Progress;
                    t.position = (a + b) * 0.5f;
                    t.rotation = Quaternion.LookRotation(b - a, Vector3.up);
                    t.localScale = new Vector3(width, 0.05f, (b - a).magnitude);
                    break;
                }
                case DodgeShapeType.Rect:
                {
                    // 온돌: 예고는 칸 가운데서 차오르고(다 차면 발동), 켜지면 칸 전체가 빨갛게.
                    r.sharedMaterial = s.Active ? kit.tileHot : kit.tileWarm;
                    float fill = TileFill(s.Active, s.Progress);
                    t.position = new Vector3((s.X0 + s.X1) * 0.5f, 0.02f, (s.Z0 + s.Z1) * 0.5f);
                    t.rotation = Quaternion.identity;
                    t.localScale = new Vector3((s.X1 - s.X0) * fill, 0.02f, (s.Z1 - s.Z0) * fill);
                    break;
                }
            }
        }

        /// <summary>온돌 칸을 덮는 비율. 예고는 0.25에서 0.96까지 차오르고 켜지면 0.96(칸 사이 틈만 남김).</summary>
        public static float TileFill(bool active, float progress) =>
            active ? 0.96f : Mathf.Lerp(0.25f, 0.96f, Mathf.Clamp01(progress));

        private Renderer Take(List<Renderer> pool, int index, PrimitiveType type)
        {
            if (index < pool.Count)
            {
                return pool[index];
            }
            var go = GameObject.CreatePrimitive(type);
            go.name = "DodgeHazard";
            // 콜라이더가 남으면 캐릭터 이동이 위험을 벽으로 여긴다.
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }
            go.transform.SetParent(root.transform, worldPositionStays: false);
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pool.Add(r);
            return r;
        }

        public void Dispose()
        {
            discs.Clear();
            boxes.Clear();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
            }
            // 재질은 키트 에셋이라 지우지 않는다.
        }
    }
}
