using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 바닥 구름에 닿아 다시 시작되는 순간 — 그 자리에 번개를 내리꽂고(모두의 화면), 새 자리에서 몸을 떨게 하고,
    /// 내 것이면 화면을 하얗게 번쩍인다. 규칙은 안 건드린다(서버가 이미 옮겼다 — 이건 그 순간을 가리는 그림).
    /// </summary>
    public class SkydiveLightningView : IStartable, ILateTickable, System.IDisposable
    {
        private static readonly Color BoltColor = new Color(0.75f, 0.88f, 1f);
        //  1을 넘겨야 블룸에 걸려 번쩍인다
        private const float BoltIntensity = 3f;
        private const float BoltWidth = 1.2f;
        private const int PoolSize = 4;

        private readonly ActorRegistry actorRegistry;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly RetryField retryField;
        private readonly IPlayerContext playerContext;
        private readonly SkydiveAtmosphere atmosphere;
        private readonly CameraController cameraController;
        private SkydiveLightningDriver driver;
        //  내 몸을 떨면 카메라도 같이 따라가 몸은 가만하고 세상이 흔들린다 — 카메라 중심에서 그만큼 빼 몸이 떨려 보이게 한다
        private bool cameraOffsetSet;
        private readonly SkydiveStrikeDetector detector = new SkydiveStrikeDetector();

        private class Bolt
        {
            public GameObject Root;
            public Transform[] Segments;
            public Material Material;
            public float StartedAt = float.NegativeInfinity;
        }

        //  몸 위치는 보간기가 매 프레임 월드 좌표로 덮어쓴다 — 그래서 "기준 자리"를 기억하지 않고,
        //  덮어쓰였으면 새 자리에, 안 덮어쓰였으면 지난번에 더한 떨림만 빼고 새 떨림을 더한다.
        private class Shake
        {
            public Transform Visual;
            public float StartedAt;
            public int Seed;
            public Vector3 LastOffset;
            public Vector3 LastPosition;
        }

        private readonly List<Bolt> bolts = new List<Bolt>();
        private readonly Dictionary<string, Shake> shakes = new Dictionary<string, Shake>();
        private readonly List<string> finishedShakes = new List<string>();
        private readonly Dictionary<string, Vector3> lastPosition = new Dictionary<string, Vector3>();
        private GameObject root;
        private int strikeCount;

        public SkydiveLightningView(ActorRegistry actorRegistry, GameFramework.World.EntityRegistry entityRegistry,
                                    RetryField retryField, IPlayerContext playerContext, SkydiveAtmosphere atmosphere,
                                    CameraController cameraController)
        {
            this.cameraController = cameraController;
            this.actorRegistry = actorRegistry;
            this.entityRegistry = entityRegistry;
            this.retryField = retryField;
            this.playerContext = playerContext;
            this.atmosphere = atmosphere;
        }

        public void Start()
        {
            driver = new GameObject(nameof(SkydiveLightningDriver)).AddComponent<SkydiveLightningDriver>();
            driver.View = this;
        }

        /// <summary>번개 기둥 재질 — 레이저 재질 복사본이되, 내 높이로 옅어지는 깊이 효과는 끈다(다시 시작 뒤 기둥은 150m 아래다).</summary>
        public static Material CreateBoltMaterial()
        {
            var template = Resources.Load<Material>(SkydiveLaserView.CoreMaterialResource);
            //  공유 재질 에셋을 칠하면 파일이 더러워진다 — 번개마다 복사본
            var material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (template != null)
            {
                SkydiveLaserView.ApplyFade(material);
                material.SetFloat("_FadeMin", 1f);
                material.SetFloat("_FadeAbove", 1f);
            }
            return material;
        }

        public static Color BoltColorAt(float alpha) => BoltColor * (BoltIntensity * alpha);

        public void LateTick()
        {
            float now = Time.time;
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<GameFramework.World.Transform>() == null)
                {
                    continue;
                }
                var position = GameFramework.World.EntityMotionExtensions.GetPosition(entity);
                bool finished = entity.Get<FinishState>()?.Finished ?? false;
                bool struck = detector.Observe(entity.Id, position.y, finished, now, retryField.All);
                lastPosition.TryGetValue(entity.Id, out var before);
                lastPosition[entity.Id] = position;
                if (struck == false)
                {
                    continue;
                }
                //  다시 시작 자리는 (0,315,0)이라 x,z도 바뀐다 — 번개는 지금 자리가 아니라 닿았던 직전 자리에 꽂는다
                strikeCount++;
                StartBolt(before, now);
                StartShake(entity.Id, now);
                if (entity.Id == playerContext.entityId)
                {
                    atmosphere.Strike(now);
                }
            }
            UpdateBolts(now);
        }

        private void StartBolt(Vector3 ground, float now)
        {
            EnsureRoot();
            Bolt bolt = null;
            foreach (var b in bolts)
            {
                if (now - b.StartedAt >= SkydiveLightning.BoltSeconds) { bolt = b; break; }
            }
            if (bolt == null)
            {
                if (bolts.Count >= PoolSize)
                {
                    bolt = bolts[0];   // 한꺼번에 넷 넘게 맞으면 가장 오래된 것을 다시 쓴다
                }
                else
                {
                    bolt = CreateBolt();
                    bolts.Add(bolt);
                }
            }
            var points = SkydiveLightning.BoltPoints(ground, strikeCount);
            for (int i = 0; i < bolt.Segments.Length; i++)
            {
                PlaceSegment(bolt.Segments[i], points[i], points[i + 1]);
            }
            bolt.StartedAt = now;
            bolt.Root.SetActive(true);
        }

        private static void PlaceSegment(Transform segment, Vector3 from, Vector3 to)
        {
            var delta = to - from;
            //  유니티 원기둥은 높이 2(위아래 ±1)라 길이의 절반을 y 크기로 준다
            segment.position = (from + to) * 0.5f;
            segment.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            segment.localScale = new Vector3(BoltWidth, delta.magnitude * 0.5f, BoltWidth);
        }

        private void UpdateBolts(float now)
        {
            foreach (var b in bolts)
            {
                float alpha = SkydiveLightning.BoltAlpha(now - b.StartedAt);
                if (alpha <= 0f)
                {
                    if (b.Root.activeSelf) b.Root.SetActive(false);
                    continue;
                }
                b.Material.SetColor("_Color", BoltColorAt(alpha));
            }
        }

        private void StartShake(string id, float now)
        {
            if (actorRegistry.TryGet(id, out var actor) == false || actor == null || actor.visualGameObject == null)
            {
                return;
            }
            var visual = actor.visualGameObject.transform;
            if (shakes.TryGetValue(id, out var running) && running.Visual == visual)
            {
                running.StartedAt = now;   // 떨던 중이면 다시 떤다
                return;
            }
            shakes[id] = new Shake { Visual = visual, StartedAt = now, Seed = strikeCount, LastPosition = visual.position };
        }

        //  보간기가 이번 프레임 덮어썼으면 지금 자리가 기준, 아니면 지난번에 더한 떨림을 빼서 기준을 되찾는다.
        private static Vector3 BasePosition(Shake s)
        {
            var current = s.Visual.position;
            return current == s.LastPosition ? current - s.LastOffset : current;
        }

        /// <summary>보간기 뒤·카메라 앞에서 매 프레임 한 번(<see cref="SkydiveLightningDriver"/>).</summary>
        public void ApplyShakes()
        {
            float now = Time.time;
            var localOffset = Vector3.zero;
            finishedShakes.Clear();
            foreach (var pair in shakes)
            {
                var s = pair.Value;
                if (s.Visual == null)
                {
                    finishedShakes.Add(pair.Key);
                    continue;
                }
                float elapsed = now - s.StartedAt;
                var basePosition = BasePosition(s);
                if (elapsed >= SkydiveLightning.ShakeSeconds)
                {
                    s.Visual.position = basePosition;
                    finishedShakes.Add(pair.Key);
                    continue;
                }
                s.LastOffset = SkydiveLightning.ShakeOffset(elapsed, s.Seed);
                s.Visual.position = basePosition + s.LastOffset;
                s.LastPosition = s.Visual.position;
                if (pair.Key == playerContext.entityId)
                {
                    localOffset = s.LastOffset;
                }
            }
            foreach (var id in finishedShakes)
            {
                shakes.Remove(id);
            }
            SetCameraOffset(localOffset);
        }

        private void SetCameraOffset(Vector3 offset)
        {
            if (cameraController == null || (offset == Vector3.zero && cameraOffsetSet == false))
            {
                return;   // 떨지 않을 땐 다른 쪽이 쓰는 PivotOffset을 건드리지 않는다
            }
            cameraController.PivotOffset = -offset;
            cameraOffsetSet = offset != Vector3.zero;
        }

        private void EnsureRoot()
        {
            if (root == null)
            {
                root = new GameObject("SkydiveLightning");
            }
        }

        private Bolt CreateBolt()
        {
            var material = CreateBoltMaterial();
            var bolt = new Bolt { Root = new GameObject("Bolt"), Segments = new Transform[SkydiveLightning.BoltSegments], Material = material };
            bolt.Root.transform.SetParent(root.transform, worldPositionStays: false);
            for (int i = 0; i < bolt.Segments.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Segment" + i;
                go.transform.SetParent(bolt.Root.transform, worldPositionStays: false);
                //  콜라이더가 붙으면 키네마틱 이동이 벽으로 인식한다
                var collider = go.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.sharedMaterial = material;
                bolt.Segments[i] = go.transform;
            }
            bolt.Root.SetActive(false);
            return bolt;
        }

        public void Dispose()
        {
            if (driver != null)
            {
                driver.View = null;
                Object.Destroy(driver.gameObject);
                driver = null;
            }
            SetCameraOffset(Vector3.zero);
            foreach (var s in shakes.Values)
            {
                if (s.Visual != null) s.Visual.position = BasePosition(s);
            }
            shakes.Clear();
            foreach (var b in bolts)
            {
                if (b.Material != null) Object.Destroy(b.Material);
            }
            bolts.Clear();
            if (root != null)
            {
                Object.Destroy(root);
                root = null;
            }
            detector.Clear();
            lastPosition.Clear();
        }
    }
}
