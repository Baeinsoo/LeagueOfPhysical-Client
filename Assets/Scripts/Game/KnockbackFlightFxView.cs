using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 맞아서 날아가는 몸의 연출(그림만): 가는 방향 반대로 상체가 젖혀지고 발밑에 흙먼지가 남는다.
    /// 캐릭터 애니메이터에 맞음 동작이 없어서 코드로 낸다. 날림 여부는 서버가 정한 KnockbackEndTick 하나만 읽는다.
    /// 보간기가 매 프레임 몸 뿌리 자세를 덮어쓰므로 기울기는 자식 Model에만 준다.
    /// </summary>
    public class KnockbackFlightFxView : IStartable, System.IDisposable
    {
        public const float MaxLeanDegrees = 35f;
        public const float FullLeanSpeed = 8f;
        public const float LeanReleaseSeconds = 0.15f;
        private const float DustInterval = 0.06f;
        private const float DustLife = 0.4f;
        private const float DustHeight = 0.05f;
        private const float DustStartSize = 0.3f;
        private const float DustEndSize = 0.55f;
        private const float DustAlpha = 0.55f;
        private const float FallingY = -0.05f;
        private const int PoolSize = 32;
        private static readonly Color DustColor = new Color(0.86f, 0.78f, 0.62f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>속도에 비례해 기울고(8 m/s에서 최대), 날림이 끝난 뒤에는 0.15초에 걸쳐 선다.</summary>
        public static float LeanDegrees(float horizontalSpeed, float secondsSinceEnd)
        {
            float byEnd = 1f - Mathf.Clamp01(secondsSinceEnd / LeanReleaseSeconds);
            return MaxLeanDegrees * Mathf.Clamp01(horizontalSpeed / FullLeanSpeed) * byEnd;
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
            public Vector3 lastVelocity;
            public float dustTimer;
        }

        private struct Puff
        {
            public Transform transform;
            public Renderer renderer;
            public float bornAt;
            public bool alive;
        }

        private readonly ActorRegistry actors;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private readonly List<string> gone = new List<string>();
        private readonly Puff[] puffs = new Puff[PoolSize];
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private Material dustMaterial;
        private GameObject root;
        private KnockbackLateUpdateDriver driver;
        private int nextPuff;

        public KnockbackFlightFxView(ActorRegistry actors, GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.Runner.IRunner runner)
        {
            this.actors = actors;
            this.entityRegistry = entityRegistry;
            this.runner = runner;
        }

        public void Start()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            dustMaterial = new Material(shader) { color = DustColor };
            // 반투명으로 쓰려고 URP Unlit의 투명 설정을 코드로 켠다.
            dustMaterial.SetFloat("_Surface", 1f);
            dustMaterial.SetFloat("_Blend", 0f);
            dustMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            dustMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            dustMaterial.SetFloat("_ZWrite", 0f);
            dustMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            dustMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            root = new GameObject("KnockbackFlightFx");
            for (int i = 0; i < PoolSize; i++)
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
                puffs[i] = new Puff { transform = go.transform, renderer = r };
            }
            // 몸(보간기 0)과 카메라(3000)가 움직인 뒤에 돌아야 기울기가 덮이지 않는다.
            driver = KnockbackLateUpdateDriver.Create("KnockbackFlightFxDriver", Place);
        }

        private void Place()
        {
            var tickUpdater = runner?.tickUpdater;
            if (tickUpdater == null || tickUpdater.interval <= 0d) return;
            double interval = tickUpdater.interval;
            double renderTick = (tickUpdater.elapsedTime - interval) / interval;
            long tick = (long)System.Math.Floor(renderTick);
            float dt = Time.unscaledDeltaTime;

            foreach (var actor in actors.All)
            {
                var visual = actor.visualGameObject;
                var entity = entityRegistry.Get(actor.entityId);
                var body = entity?.Get<KnockbackBody>();
                if (visual == null || body == null) continue;

                entries.TryGetValue(actor.entityId, out var entry);
                bool flying = tick < body.KnockbackEndTick;
                float sinceEnd = flying ? 0f : Mathf.Max(0f, (float)((renderTick - body.KnockbackEndTick) * interval));
                bool fading = !flying && entry != null && entry.leaning && sinceEnd < LeanReleaseSeconds;

                if (!flying && !fading)
                {
                    if (entry != null && entry.leaning) Release(entry);
                    continue;
                }

                if (entry == null)
                {
                    entry = new Entry();
                    entries[actor.entityId] = entry;
                }
                if (entry.model == null || !entry.model.IsChildOf(visual.transform))
                {
                    entry.model = visual.transform.Find(ChibiRig.ModelChildName);
                    if (entry.model == null) continue;
                    entry.rest = entry.model.localRotation;
                    entry.leaning = false;
                }

                if (flying)
                {
                    var v = entity.Get<GameFramework.World.Velocity>();
                    if (v != null)
                    {
                        var linear = v.Linear;
                        var now = new Vector3(linear.X, 0f, linear.Z);
                        // 멈춘 순간에는 마지막 방향을 유지해 기울기가 반대로 튀지 않게 한다.
                        if (now.sqrMagnitude > 1e-6f) entry.lastVelocity = now;
                        else entry.lastVelocity = entry.lastVelocity.normalized * now.magnitude;
                    }
                }

                float degrees = LeanDegrees(entry.lastVelocity.magnitude, sinceEnd);
                var axis = LeanAxis(entry.lastVelocity);
                if (axis == Vector3.zero) continue;
                var localAxis = Quaternion.Inverse(visual.transform.rotation) * axis;
                entry.model.localRotation = Quaternion.AngleAxis(degrees, localAxis) * entry.rest;
                entry.leaning = true;

                if (flying && LeavesDust(visual.transform.position.y))
                {
                    entry.dustTimer -= dt;
                    if (entry.dustTimer <= 0f)
                    {
                        entry.dustTimer = DustInterval;
                        var p = visual.transform.position;
                        Spawn(new Vector3(p.x, DustHeight, p.z));
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
            entry.dustTimer = 0f;
        }

        private void Spawn(Vector3 position)
        {
            ref var puff = ref puffs[nextPuff];
            nextPuff = (nextPuff + 1) % PoolSize;
            puff.alive = true;
            puff.bornAt = Time.unscaledTime;
            puff.transform.position = position;
            puff.transform.gameObject.SetActive(true);
        }

        private void UpdatePuffs()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                ref var puff = ref puffs[i];
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
