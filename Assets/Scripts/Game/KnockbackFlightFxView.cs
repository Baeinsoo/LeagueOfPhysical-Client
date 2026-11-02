using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 맞아서 날아가는 몸의 연출(그림만): 가는 방향 반대로 상체가 젖혀지고 발밑에 흙먼지가 남는다.
    /// 캐릭터 애니메이터에 맞음 동작이 없어서 코드로 낸다. 날림 여부는 서버가 정한 KnockbackEndTick 하나만 읽되,
    /// 몸을 그리는 시각과 비교한다 — 내 몸은 예측 시각, 남의 몸은 서버보다 뒤처진 보간 시각.
    /// 보간기가 매 프레임 몸 뿌리 자세를 덮어쓰므로 기울기는 자식 Model에만 준다.
    /// </summary>
    public class KnockbackFlightFxView : IStartable, System.IDisposable
    {
        public const float MaxLeanDegrees = 35f;
        public const float FullLeanSpeed = 8f;
        public const float LeanReleaseSeconds = 0.15f;
        public const float LeanAttackSeconds = 0.08f;
        private const float DustInterval = 0.06f;
        private const float DustLife = 0.4f;
        private const float DustHeight = 0.05f;
        private const float DustStartSize = 0.45f;
        private const float DustEndSize = 0.83f;
        private const float DustAlpha = 0.8f;
        private const float FallingY = -0.05f;
        private const int StartPoolSize = 32;
        private static readonly Color DustColor = new Color(0.97f, 0.95f, 0.88f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>속도에 비례해 기울고(8 m/s에서 최대), 날림이 끝난 뒤에는 0.15초에 걸쳐 선다.</summary>
        public static float LeanDegrees(float horizontalSpeed, float secondsSinceEnd)
        {
            float byEnd = 1f - Mathf.Clamp01(secondsSinceEnd / LeanReleaseSeconds);
            return MaxLeanDegrees * Mathf.Clamp01(horizontalSpeed / FullLeanSpeed) * byEnd;
        }

        /// <summary>
        /// 날림 여부. 내 몸은 예측 시각, 남의 몸은 그 몸을 그리는 보간 시각을 <paramref name="drawnTick"/>으로 넣는다.
        /// 남의 몸은 끝 틱이 스냅 도착 즉시 덮이니 시작 틱도 함께 봐서, 맞기 전 걷던 모습이 젖혀지지 않게 한다.
        /// </summary>
        public static bool IsFlyingAt(double drawnTick, long endTick, long startTick = long.MinValue)
            => drawnTick >= startTick && drawnTick < endTick;

        /// <summary>
        /// 현재 각도에서 목표 각도로 다가간다. 켜질 때는 0.08초, 풀릴 때는 0.15초에 최대 각도만큼 움직이는 빠르기라
        /// 한 프레임에 튀지 않고, 도중에 다시 맞아도 지금 각도에서 이어진다.
        /// </summary>
        public static float ApproachDegrees(float current, float target, float dt)
        {
            float rate = target > current ? MaxLeanDegrees / LeanAttackSeconds : MaxLeanDegrees / LeanReleaseSeconds;
            return Mathf.MoveTowards(current, target, rate * Mathf.Max(0f, dt));
        }

        /// <summary>
        /// 속도에 수직인 수평축. 이 축으로 양의 각도만큼 돌리면 머리 끝이 가는 방향의 뒤쪽으로 넘어간다.
        /// </summary>
        public static Vector3 LeanAxis(Vector3 velocity)
        {
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.sqrMagnitude < 1e-8f) return Vector3.zero;
            return Vector3.Cross(flat.normalized, Vector3.up);
        }

        /// <summary>바닥 아래로 떨어지는 중이면 먼지를 남기지 않는다.</summary>
        public static bool LeavesDust(float bodyY) => bodyY >= FallingY;

        private class Entry
        {
            public Transform model;
            public Quaternion rest;
            public bool leaning;
            public Vector3 direction;
            public float degrees;
            public float dustTimer;
            public Vector3 lastDrawnPosition;
            public bool hasDrawnPosition;
        }

        private class Puff
        {
            public Transform transform;
            public Renderer renderer;
            public float bornAt;
            public bool alive;
        }

        private readonly ActorRegistry actors;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly IPlayerContext playerContext;
        private readonly EntityRenderClock renderClock;
        private readonly KnockbackConfig config;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private readonly List<string> gone = new List<string>();
        private readonly List<Puff> puffs = new List<Puff>(StartPoolSize);
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private Material dustMaterial;
        private GameObject root;
        private KnockbackLateUpdateDriver driver;

        public KnockbackFlightFxView(ActorRegistry actors, GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.Runner.IRunner runner, IPlayerContext playerContext, EntityRenderClock renderClock,
            KnockbackConfig config)
        {
            this.actors = actors;
            this.entityRegistry = entityRegistry;
            this.runner = runner;
            this.playerContext = playerContext;
            this.renderClock = renderClock;
            this.config = config;
        }

        public void Start()
        {
            var urp = Shader.Find("Universal Render Pipeline/Unlit");
            if (urp != null)
            {
                dustMaterial = new Material(urp) { color = DustColor };
                // 반투명으로 쓰려고 URP Unlit의 투명 설정을 코드로 켠다.
                dustMaterial.SetFloat("_Surface", 1f);
                dustMaterial.SetFloat("_Blend", 0f);
                dustMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                dustMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                dustMaterial.SetFloat("_ZWrite", 0f);
                dustMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                dustMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                // 셰이더를 못 찾아도 연출만 포기하고 게임은 계속한다 — 스프라이트 셰이더는 처음부터 알파 혼합이다.
                dustMaterial = new Material(Shader.Find("Sprites/Default")) { color = DustColor };
            }

            root = new GameObject("KnockbackFlightFx");
            for (int i = 0; i < StartPoolSize; i++) puffs.Add(CreatePuff());
            // 몸(보간기 0)과 카메라(3000)가 움직인 뒤에 돌아야 기울기가 덮이지 않는다.
            driver = KnockbackLateUpdateDriver.Create("KnockbackFlightFxDriver", Place);
        }

        private Puff CreatePuff()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Dust";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = dustMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return new Puff { transform = go.transform, renderer = r };
        }

        private void Place()
        {
            var tickUpdater = runner?.tickUpdater;
            if (tickUpdater == null || tickUpdater.interval <= 0d) return;
            double interval = tickUpdater.interval;
            double myRenderTick = (tickUpdater.elapsedTime - interval) / interval;
            float dt = Time.unscaledDeltaTime;
            string me = playerContext.entityId;

            foreach (var actor in actors.Values)
            {
                var visual = actor.visualGameObject;
                var entity = entityRegistry.Get(actor.entityId);
                var body = entity?.Get<KnockbackBody>();
                if (visual == null || body == null) continue;

                entries.TryGetValue(actor.entityId, out var entry);
                bool mine = actor.entityId == me;

                // 남의 몸은 서버보다 뒤처진 보간 시각으로 그려지니, 끝 틱도 그 시각과 비교한다.
                double drawnTick = mine ? myRenderTick : renderClock.TimeFor(actor.entityId) / interval;
                long startTick = mine ? long.MinValue
                    : body.KnockbackEndTick - config.Ticks(config.StunBase + body.DamagePercent * config.StunScale);
                bool flying = IsFlyingAt(drawnTick, body.KnockbackEndTick, startTick);

                var drawn = visual.transform.position;
                Vector3 drawnVelocity = Vector3.zero;
                if (entry != null && entry.hasDrawnPosition && dt > 1e-5f)
                {
                    var delta = drawn - entry.lastDrawnPosition;
                    drawnVelocity = new Vector3(delta.x, 0f, delta.z) / dt;
                }

                if (!flying && (entry == null || !entry.leaning))
                {
                    if (entry != null) { entry.lastDrawnPosition = drawn; entry.hasDrawnPosition = true; }
                    continue;
                }

                if (entry == null)
                {
                    entry = new Entry();
                    entries[actor.entityId] = entry;
                }
                entry.lastDrawnPosition = drawn;
                entry.hasDrawnPosition = true;
                if (entry.model == null || !entry.model.IsChildOf(visual.transform))
                {
                    entry.model = visual.transform.Find(ChibiRig.ModelChildName);
                    if (entry.model == null) continue;
                    entry.rest = entry.model.localRotation;
                    entry.leaning = false;
                    entry.degrees = 0f;
                }

                // 내 몸은 월드 속도, 남의 몸은 그려진 몸의 실제 이동으로 방향을 잰다.
                Vector3 velocity = drawnVelocity;
                if (mine)
                {
                    var v = entity.Get<GameFramework.World.Velocity>();
                    if (v != null) velocity = new Vector3(v.Linear.X, 0f, v.Linear.Z);
                }
                // 멈춘 순간에는 마지막 방향을 유지하고 각도만 0으로 풀어 굳지 않게 한다.
                if (velocity.sqrMagnitude > 1e-6f) entry.direction = velocity.normalized;
                float targetDegrees = flying ? LeanDegrees(velocity.magnitude, 0f) : 0f;
                entry.degrees = ApproachDegrees(entry.degrees, targetDegrees, dt);

                var axis = LeanAxis(entry.direction);
                if (axis == Vector3.zero || (entry.degrees <= 0f && !flying))
                {
                    Release(entry);
                    continue;
                }
                var localAxis = Quaternion.Inverse(visual.transform.rotation) * axis;
                entry.model.localRotation = Quaternion.AngleAxis(entry.degrees, localAxis) * entry.rest;
                entry.leaning = true;

                if (flying && LeavesDust(drawn.y))
                {
                    entry.dustTimer -= dt;
                    if (entry.dustTimer <= 0f)
                    {
                        entry.dustTimer = DustInterval;
                        Spawn(new Vector3(drawn.x, DustHeight, drawn.z));
                    }
                }
            }

            UpdatePuffs();

            gone.Clear();
            foreach (var pair in entries)
            {
                if (!actors.Contains(pair.Key)) gone.Add(pair.Key);
            }
            for (int i = 0; i < gone.Count; i++) entries.Remove(gone[i]);
        }

        private static void Release(Entry entry)
        {
            if (entry.model != null) entry.model.localRotation = entry.rest;
            entry.leaning = false;
            entry.degrees = 0f;
            entry.dustTimer = 0f;
        }

        private void Spawn(Vector3 position)
        {
            // 살아 있는 먼지를 빼앗지 않는다 — 빈 칸이 없으면 풀을 늘린다.
            Puff puff = null;
            for (int i = 0; i < puffs.Count; i++)
            {
                if (!puffs[i].alive) { puff = puffs[i]; break; }
            }
            if (puff == null)
            {
                puff = CreatePuff();
                puffs.Add(puff);
            }
            puff.alive = true;
            puff.bornAt = Time.unscaledTime;
            puff.transform.position = position;
            puff.transform.gameObject.SetActive(true);
        }

        private void UpdatePuffs()
        {
            for (int i = 0; i < puffs.Count; i++)
            {
                var puff = puffs[i];
                if (!puff.alive) continue;
                float t = (Time.unscaledTime - puff.bornAt) / DustLife;
                if (t >= 1f)
                {
                    puff.alive = false;
                    puff.transform.gameObject.SetActive(false);
                    continue;
                }
                float size = Mathf.Lerp(DustStartSize, DustEndSize, t);
                puff.transform.localScale = new Vector3(size, size * 0.3f, size);
                var c = DustColor;
                c.a = DustAlpha * (1f - t);
                puff.renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                block.SetColor(ColorId, c);
                puff.renderer.SetPropertyBlock(block);
            }
        }

        public void Dispose()
        {
            foreach (var entry in entries.Values)
            {
                if (entry.leaning) Release(entry);
            }
            entries.Clear();
            if (driver != null) Object.Destroy(driver.gameObject);
            if (root != null) Object.Destroy(root);
            if (dustMaterial != null) Object.Destroy(dustMaterial);
        }
    }
}
