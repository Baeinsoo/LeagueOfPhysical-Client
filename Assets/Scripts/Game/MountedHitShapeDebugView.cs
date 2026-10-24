using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 디버그 전용: 화면에 그려진 몸마다 판정 구(<see cref="MountedArcheryHitTest"/>)를 노란 고리로 겹쳐 그린다.
    /// 판정이 그림을 넘는지 눈으로 대조하는 용도다. 에디터 메뉴 LOP/Debug/말 활쏘기 판정 구 보기로 켠다.
    /// </summary>
    public class MountedHitShapeDebugView : ILateTickable, IDisposable
    {
        /// <summary>켜면 다음 프레임부터 그린다. 에디터 메뉴가 바꾼다.</summary>
        public static bool Enabled { get; set; }

        private const int Segments = 24;

        private readonly ActorRegistry actorRegistry;
        private readonly MountedArcheryClientState state;
        private readonly List<MountedPredictedHits.Target> bodies = new List<MountedPredictedHits.Target>();
        // 구 하나에 고리 셋(수평·앞뒤·좌우).
        private readonly List<LineRenderer> rings = new List<LineRenderer>();

        private GameObject root;
        private Material material;

        public MountedHitShapeDebugView(ActorRegistry actorRegistry, MountedArcheryClientState state)
        {
            this.actorRegistry = actorRegistry;
            this.state = state;
        }

        public void LateTick()
        {
            if (Enabled == false)
            {
                if (root != null)
                {
                    root.SetActive(false);
                }
                return;
            }

            // 나도 그린다 — 내 말과 기수도 대조해 볼 수 있게(빈 id는 아무도 빼지 않는다).
            MountedPredictedHits.CollectDrawnTargets(actorRegistry, state, string.Empty, bodies);
            int used = 0;
            foreach (var body in bodies)
            {
                for (int i = 0; i < MountedArcheryHitTest.ShapeCount; i++)
                {
                    var (center, radius) = MountedArcheryHitTest.WorldShape(i, body.Position, body.HeadingDeg);
                    Draw(Ring(used++), center, radius, Vector3.right, Vector3.forward);
                    Draw(Ring(used++), center, radius, Vector3.right, Vector3.up);
                    Draw(Ring(used++), center, radius, Vector3.forward, Vector3.up);
                }
            }
            for (int i = used; i < rings.Count; i++)
            {
                rings[i].enabled = false;
            }
        }

        private LineRenderer Ring(int index)
        {
            if (root == null)
            {
                root = new GameObject("MountedHitShapeDebug");
                material = new Material(Shader.Find("Sprites/Default"));
            }
            root.SetActive(true);
            while (rings.Count <= index)
            {
                var go = new GameObject("Ring");
                go.transform.SetParent(root.transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.loop = true;
                line.useWorldSpace = true;
                line.positionCount = Segments;
                line.widthMultiplier = 0.02f;
                line.startColor = line.endColor = new Color(1f, 0.9f, 0.1f, 0.9f);
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rings.Add(line);
            }
            rings[index].enabled = true;
            return rings[index];
        }

        private static void Draw(LineRenderer line, Vector3 center, float radius, Vector3 a, Vector3 b)
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                line.SetPosition(i, center + (a * Mathf.Cos(angle) + b * Mathf.Sin(angle)) * radius);
            }
        }

        public void Dispose()
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
            }
            rings.Clear();
            if (material != null)
            {
                UnityEngine.Object.Destroy(material);
                material = null;
            }
        }
    }
}
