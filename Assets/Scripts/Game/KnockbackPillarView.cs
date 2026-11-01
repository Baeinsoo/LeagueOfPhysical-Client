using System;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 기둥 그림. 기둥의 충돌은 Shared의 콜라이더가 맡으므로 여기선 보이기만 한다.
    /// 아래 칸이 무너지기 1초 전부터 바닥과 같은 빨강으로 깜박이고, 무너진 뒤 0.5초 동안 가라앉아 사라진다.
    /// </summary>
    public class KnockbackPillarView : IStartable, ILateTickable, IDisposable
    {
        private const long WarningTicks = 50;
        private const float BlinkSeconds = 0.15f;
        private const float SinkSeconds = 0.5f;

        private readonly GameFramework.Runner.IRunner runner;
        private readonly KnockbackPillars pillars;
        private GameObject root;
        private Material normal;
        private Material warning;
        private GameObject[] objects = Array.Empty<GameObject>();
        private MeshRenderer[] renderers = Array.Empty<MeshRenderer>();

        public KnockbackPillarView(GameFramework.Runner.IRunner runner, KnockbackPillars pillars)
        {
            this.runner = runner;
            this.pillars = pillars;
        }

        /// <summary>무너진 뒤 지난 시간에 따른 가라앉은 깊이. 무너지기 전 0, 0.5초에 걸쳐 선형, 그 뒤 height.</summary>
        public static float SinkDepth(long collapseTick, double renderTick, float height)
        {
            if (collapseTick == long.MaxValue) return 0f;
            double seconds = (renderTick - collapseTick) * KnockbackConfig.TickInterval;
            float t = Mathf.Clamp01((float)(seconds / SinkSeconds));
            return height * t;
        }

        public void Start()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            normal = new Material(shader) { color = new Color32(0x8A, 0x8F, 0x98, 0xFF) };
            warning = new Material(shader) { color = new Color32(0xE0, 0x3A, 0x30, 0xFF) };
            root = new GameObject("KnockbackPillars");
            objects = new GameObject[pillars.Count];
            renderers = new MeshRenderer[pillars.Count];
            for (int i = 0; i < pillars.Count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = $"Pillar_{i}";
                // 충돌은 Shared 콜라이더가 맡는다 — 겹치면 몸 이동이 두 번 막힌다.
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(root.transform, false);
                // 원기둥 기본형은 지름 1, 높이 2.
                go.transform.localScale = new Vector3(pillars.Radius * 2f, pillars.Height * 0.5f, pillars.Radius * 2f);
                var c = pillars.Center(i);
                go.transform.position = new Vector3(c.x, pillars.Height * 0.5f, c.y);
                renderers[i] = go.GetComponent<MeshRenderer>();
                renderers[i].sharedMaterial = normal;
                objects[i] = go;
            }
        }

        public void LateTick()
        {
            if (runner?.tickUpdater == null || objects.Length == 0) return;
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d) return;
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            long tick = (long)Math.Floor(renderTick);
            bool blinkOn = ((int)(Time.unscaledTime / BlinkSeconds) & 1) == 0;

            for (int i = 0; i < objects.Length; i++)
            {
                bool standing = pillars.IsStanding(i, tick);
                float sink = standing ? 0f : SinkDepth(pillars.CollapseTick(i), renderTick, pillars.Height);
                bool visible = sink < pillars.Height;
                objects[i].SetActive(visible);
                if (!visible) continue;

                var c = pillars.Center(i);
                objects[i].transform.position = new Vector3(c.x, pillars.Height * 0.5f - sink, c.y);
                bool warn = standing && !pillars.IsStanding(i, tick + WarningTicks);
                renderers[i].sharedMaterial = warn && blinkOn ? warning : normal;
            }
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (normal != null) UnityEngine.Object.Destroy(normal);
            if (warning != null) UnityEngine.Object.Destroy(warning);
        }
    }
}
