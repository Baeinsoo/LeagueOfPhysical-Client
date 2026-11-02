using System.Collections.Generic;
using GameFramework;
using MessagePipe;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 충격파 폭탄이 터진 자리에 고리를 그린다. 서버 터짐 사건을 받는 즉시 0.3초 동안 반경 끝까지 커지며 흐려진다.
    /// 고리 오브젝트는 풀에서 꺼내 쓰고 끝나면 돌려놓는다. 그림용이라 월드 상태는 건드리지 않는다.
    /// </summary>
    public class KnockbackBlastView : MessageHandlerBase
    {
        public const float DurationSeconds = 0.3f;
        private const float StartAlpha = 0.85f;
        private const float RingInner = 0.82f;
        private const float RingHeight = 0.05f;
        private const int RingSegments = 48;
        private static readonly Color RingColor = new Color(1f, 0.5f, 0.1f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>고리 반지름. 터진 순간 0에서 시작해 0.3초 만에 반경까지 커진다.</summary>
        public static float RingRadius(float elapsedSeconds, float maxRadius)
            => maxRadius * Mathf.Clamp01(elapsedSeconds / DurationSeconds);

        /// <summary>고리 불투명도. 커지는 동안 선형으로 흐려져 끝에서 0.</summary>
        public static float RingAlpha(float elapsedSeconds)
            => StartAlpha * (1f - Mathf.Clamp01(elapsedSeconds / DurationSeconds));

        public static bool IsFinished(float elapsedSeconds) => elapsedSeconds >= DurationSeconds;

        private class Ring
        {
            public GameObject go;
            public MeshRenderer renderer;
            public float startedAt;
        }

        private readonly ISubscriber<WorldEventBatchToC> batchSubscriber;
        private readonly KnockbackConfig config;
        private readonly List<Ring> active = new List<Ring>();
        private readonly Stack<Ring> pool = new Stack<Ring>();
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        private Transform root;
        private Mesh mesh;
        private Material material;
        private KnockbackLateUpdateDriver driver;

        public KnockbackBlastView(ISubscriber<WorldEventBatchToC> batchSubscriber, KnockbackConfig config)
        {
            this.batchSubscriber = batchSubscriber;
            this.config = config;
        }

        protected override void Subscribe() => Track(batchSubscriber.Subscribe(OnBatch));

        private void OnBatch(WorldEventBatchToC msg)
        {
            foreach (var rec in msg.Events)
            {
                if (rec.EventCase != WorldEventToC.EventOneofCase.KnockbackBlast)
                {
                    continue;
                }
                var blast = (KnockbackBlastEvent)WorldEventWire.FromWire(rec);
                Spawn(blast.position);
            }
        }

        private void Spawn(Vector2 position)
        {
            EnsureObjects();
            Ring ring;
            if (pool.Count > 0)
            {
                ring = pool.Pop();
                ring.go.SetActive(true);
            }
            else
            {
                var go = new GameObject("KnockbackBlastRing");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                ring = new Ring { go = go, renderer = r };
            }
            ring.startedAt = Time.unscaledTime;
            ring.go.transform.position = new Vector3(position.x, RingHeight, position.y);
            Apply(ring, 0f);
            active.Add(ring);
        }

        private void Step()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var ring = active[i];
                float elapsed = Time.unscaledTime - ring.startedAt;
                if (IsFinished(elapsed))
                {
                    ring.go.SetActive(false);
                    pool.Push(ring);
                    active.RemoveAt(i);
                    continue;
                }
                Apply(ring, elapsed);
            }
        }

        private void Apply(Ring ring, float elapsed)
        {
            float r = Mathf.Max(0.01f, RingRadius(elapsed, config.BlastRadius));
            ring.go.transform.localScale = new Vector3(r, 1f, r);
            var c = RingColor;
            c.a = RingAlpha(elapsed);
            ring.renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            ring.renderer.SetPropertyBlock(block);
        }

        private void EnsureObjects()
        {
            if (root != null) return;
            root = new GameObject("KnockbackBlasts").transform;
            mesh = BuildRingMesh();
            material = KnockbackAimIndicatorView.CreateTransparentMaterial();
            driver = KnockbackLateUpdateDriver.Create("KnockbackBlastViewDriver", Step);
        }

        // 반지름 1짜리 납작한 고리(안쪽 0.82). 크기는 스케일로 바꾼다.
        private static Mesh BuildRingMesh()
        {
            var verts = new Vector3[RingSegments * 2];
            var tris = new int[RingSegments * 6];
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts[i * 2] = dir * RingInner;
                verts[i * 2 + 1] = dir;
                int next = (i + 1) % RingSegments;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = next * 2 + 1; tris[t + 2] = i * 2 + 1;
                tris[t + 3] = i * 2; tris[t + 4] = next * 2; tris[t + 5] = next * 2 + 1;
            }
            var m = new Mesh { name = "BlastRing", vertices = verts, triangles = tris };
            m.RecalculateBounds();
            return m;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (driver != null) Object.Destroy(driver.gameObject);
            if (root != null) Object.Destroy(root.gameObject);
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
        }
    }
}
