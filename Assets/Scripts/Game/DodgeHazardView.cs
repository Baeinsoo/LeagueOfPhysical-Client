using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 위험을 그린다. 판정과 같은 식에 **내 몸이 그려지는 틱**을 넣으므로, 보이는 것이 곧 서버 판정이다.
    /// 틱(50Hz)으로만 그리면 탄이 계단처럼 움직여서, 움직이는 원은 한 틱 전 자리와 지금 자리 사이를 섞는다.
    /// MonoBehaviour가 아니다 — 서버 씬에서 빠진 스크립트가 되지 않게(SkydiveLaserView와 같은 이유).
    /// </summary>
    public class DodgeHazardView : ILateTickable, IDisposable
    {
        private static readonly Color WarnColor = new Color(1f, 0.82f, 0.2f, 0.35f);
        private static readonly Color HotColor = new Color(1f, 0.25f, 0.2f, 0.9f);
        private static readonly Color BulletColor = new Color(1f, 0.95f, 0.98f, 1f);

        private readonly GameFramework.Runner.IRunner runner;
        private readonly DodgeClientState state;
        private readonly DodgeConfig config;
        private readonly List<DodgeShape> shapes = new List<DodgeShape>();
        private readonly List<Renderer> pool = new List<Renderer>();
        private GameObject root;
        private Material warn, hot, bullet;

        public DodgeHazardView(GameFramework.Runner.IRunner runner, DodgeClientState state, DodgeConfig config)
        {
            this.runner = runner;
            this.state = state;
            this.config = config;
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

            EnsureRoot();
            int used = 0;
            foreach (var s in shapes)
            {
                var r = Take(used++, IsBullet(s) ? PrimitiveType.Sphere : PrimitiveType.Cube);
                Place(r, s, frac);
            }
            for (int i = used; i < pool.Count; i++)
            {
                pool[i].enabled = false;
            }
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

        private bool IsBullet(DodgeShape s) =>
            s.Type == DodgeShapeType.Circle && s.Active && s.Radius <= config.BulletRadius + 1e-4f;

        private void Place(Renderer r, DodgeShape s, float frac)
        {
            var t = r.transform;
            r.enabled = true;
            switch (s.Type)
            {
                case DodgeShapeType.Circle:
                {
                    // 움직이는 원은 한 틱 전 → 지금 사이를 frac만큼 섞어 부드럽게(ShapeTick 참고).
                    var c = CircleCenter(s, frac);
                    float x = c.x, z = c.y;
                    if (IsBullet(s))
                    {
                        r.sharedMaterial = bullet;
                        t.position = new Vector3(x, 0.6f, z);
                        t.rotation = Quaternion.identity;
                        t.localScale = Vector3.one * (s.Radius * 2f);
                    }
                    else
                    {
                        // 예고는 차오르는 원판, 켜지면 붉은 원판. 두께는 얇게 — 바닥 위에 깔린다.
                        r.sharedMaterial = s.Active ? hot : warn;
                        float d = s.Radius * 2f * (s.Active ? 1f : Mathf.Max(0.15f, s.Progress));
                        t.position = new Vector3(x, 0.03f, z);
                        t.rotation = Quaternion.identity;
                        t.localScale = new Vector3(d, 0.04f, d);
                    }
                    break;
                }
                case DodgeShapeType.Segment:
                {
                    var a = new Vector3(s.X0, 0.4f, s.Z0);
                    var b = new Vector3(s.X1, 0.4f, s.Z1);
                    r.sharedMaterial = s.Active ? hot : warn;
                    float width = s.Active ? s.Radius * 2f : 0.08f + 0.12f * s.Progress;
                    t.position = (a + b) * 0.5f;
                    t.rotation = Quaternion.LookRotation(b - a, Vector3.up);
                    t.localScale = new Vector3(width, s.Active ? 0.6f : 0.05f, (b - a).magnitude);
                    break;
                }
                case DodgeShapeType.Rect:
                {
                    r.sharedMaterial = s.Active ? hot : warn;
                    t.position = new Vector3((s.X0 + s.X1) * 0.5f, 0.02f, (s.Z0 + s.Z1) * 0.5f);
                    t.rotation = Quaternion.identity;
                    t.localScale = new Vector3((s.X1 - s.X0) * 0.96f, 0.02f, (s.Z1 - s.Z0) * 0.96f);
                    break;
                }
            }
        }

        private Renderer Take(int index, PrimitiveType type)
        {
            while (pool.Count <= index)
            {
                pool.Add(null);
            }
            var r = pool[index];
            bool wantSphere = type == PrimitiveType.Sphere;
            if (r != null && (r.GetComponent<MeshFilter>().sharedMesh.name == "Sphere") == wantSphere)
            {
                return r;
            }
            if (r != null)
            {
                UnityEngine.Object.Destroy(r.gameObject);
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
            r = go.GetComponent<Renderer>();
            pool[index] = r;
            return r;
        }

        private void EnsureRoot()
        {
            if (root != null)
            {
                return;
            }
            root = new GameObject("DodgeHazards");
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            warn = MakeTransparent(shader, WarnColor);
            hot = MakeTransparent(shader, HotColor);
            bullet = new Material(shader) { color = BulletColor };
        }

        // URP Unlit을 반투명으로 — 예고가 바닥과 캐릭터를 가리지 않게.
        private static Material MakeTransparent(Shader shader, Color color)
        {
            var m = new Material(shader) { color = color };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        public void Dispose()
        {
            pool.Clear();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
            }
            // SkydiveLaserView는 머티리얼을 안 지워 샌다 — 여기선 지운다.
            if (warn != null) UnityEngine.Object.Destroy(warn);
            if (hot != null) UnityEngine.Object.Destroy(hot);
            if (bullet != null) UnityEngine.Object.Destroy(bullet);
            warn = hot = bullet = null;
        }
    }
}
