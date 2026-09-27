using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

//  EditMode 테스트(Assets/Tests/Editor, 별도 어셈블리)가 아래 internal 테스트 seam(markerSearch·
//  TriggerCount·Tick(float))에 접근하려면 이 선언이 필요하다 — 코드를 옮기지 않고 어셈블리
//  경계만 살짝 튼다.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]

namespace LOP
{
    /// <summary>
    /// 홀로그램 관문(<see cref="FlappyHologramMarker"/>)의 클라 전용 연출. 새가(대시 중이라야
    /// 물리적으로 들어올 수 있다) 관문 안으로 들어오면 판정 없이 ① 재질 알파를 0.3초 흔들고
    /// ② 작은 조각 12개를 카메라 쪽(-z)으로 흩었다가 0.5초에 걸쳐 제자리로 모은다.
    ///
    /// <para>마커는 맵 씬(양쪽이 읽음)에 있어 로직이 없다 — 이 클래스가 런타임에 마커를 찾아
    /// 연출만 클라 쪽에 얹는다. 씬에 미리 놓을 대상이 아니라 <see cref="FlappyChaserView"/>·
    /// <see cref="FlappyAtmosphere"/>와 같은 진입점(ITickable) 모양을 따른다.</para>
    /// </summary>
    public class FlappyHologramFx : ITickable, System.IDisposable
    {
        private const float ShakeDuration = 0.3f;
        private const float ShardDuration = 0.5f;
        private const int ShardCount = 12;
        private const float ShardScatterDistance = 1.2f;   // 카메라 쪽(-z)으로 흩어지는 거리(m)
        private const float ShardSize = 0.25f;
        private const float ShakeFrequency = 40f;          // 라디안/초 — 0.3초 동안 몇 번 흔들릴지
        private const float ShakeAmplitude = 0.35f;
        private static readonly Color ShardColor = new Color(0.62f, 0.42f, 0.92f, 0.55f);

        //  맵이 additive로 늦게 올라올 수 있어 못 찾으면 잠깐 재시도한다 — 그래도 없으면
        //  이 맵엔 홀로그램이 아예 없다는 뜻이니 영원히 찾지 않는다(EnsureMarkers 참고).
        private const float MarkerSearchWindow = 5f;     // 이 시간(초) 안에서만 재시도
        private const float MarkerSearchInterval = 1f;   // 재시도 간격(초)

        private readonly GameFramework.World.EntityRegistry entityRegistry;

        private FlappyHologramMarker[] markers;
        private float markerSearchClock;
        private float nextMarkerSearchAt;
        private bool markerSearchGivenUp;
        private readonly Dictionary<FlappyHologramMarker, HologramFxState> states = new Dictionary<FlappyHologramMarker, HologramFxState>();
        private Material shardMaterial;

        //  테스트 전용 seam — 실제로는 씬을 뒤지는 DefaultMarkerSearch, EditMode 테스트는 가짜
        //  탐색으로 갈아 끼워 몇 번 불렸는지 세거나 원하는 시점에 마커를 "발견"시킨다.
        internal System.Func<FlappyHologramMarker[]> markerSearch = DefaultMarkerSearch;

        //  테스트 전용 seam — 홀로그램 연출이 실제로 발동한(들어오는 순간) 횟수.
        internal int TriggerCount { get; private set; }

        private class HologramFxState
        {
            public Renderer Renderer;
            public Material MaterialInstance;
            public Material OriginalSharedMaterial;
            public float BaseAlpha;
            public bool WasInside;
            public bool Active;
            public float Elapsed;
            public GameObject ShardHolder;
            public Transform[] Shards;
            public Vector3[] ShardHomes;
            public Vector3[] ShardScattered;
        }

        public FlappyHologramFx(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public void Tick()
        {
            Tick(Time.deltaTime);
        }

        //  실 로직은 dt를 인자로 받는다 — EditMode 테스트는 Time.deltaTime(플레이 중이 아니면
        //  0에 가깝다)에 기대지 않고 원하는 시간 간격을 직접 넣어 검증한다.
        internal void Tick(float deltaTime)
        {
            EnsureMarkers(deltaTime);
            if (markers == null || markers.Length == 0)
            {
                return;
            }

            float maxDuration = Mathf.Max(ShakeDuration, ShardDuration);

            foreach (var marker in markers)
            {
                if (marker == null)
                {
                    continue;
                }

                var state = GetState(marker);
                if (state == null)
                {
                    continue;
                }

                bool inside = AnyBirdInside(state.Renderer.bounds);
                if (inside && state.WasInside == false)
                {
                    state.Active = true;
                    state.Elapsed = 0f;
                    TriggerCount++;
                }
                state.WasInside = inside;

                if (state.Active == false)
                {
                    continue;
                }

                Animate(state, state.Elapsed);
                state.Elapsed += deltaTime;
                if (state.Elapsed >= maxDuration)
                {
                    state.Active = false;
                    Reset(state);
                }
            }
        }

        //  스코프 시작(첫 틱)에 한 번 찾고, 못 찾으면 최대 5초 동안 1초 간격으로 재시도한 뒤
        //  그만둔다 — 홀로그램이 없는 맵에서 매 틱 FindObjectsByType을 영원히 돌리지 않는다.
        private void EnsureMarkers(float deltaTime)
        {
            if (markers != null && markers.Length > 0)
            {
                return;
            }
            if (markerSearchGivenUp)
            {
                return;
            }

            markerSearchClock += deltaTime;
            if (markerSearchClock < nextMarkerSearchAt)
            {
                return;
            }

            var found = markerSearch();
            if (found != null && found.Length > 0)
            {
                markers = found;
                return;
            }

            if (markerSearchClock >= MarkerSearchWindow)
            {
                markerSearchGivenUp = true;   // 5초 안에 하나도 없었다 — 이 맵엔 홀로그램이 없다
                return;
            }

            nextMarkerSearchAt = markerSearchClock + MarkerSearchInterval;
        }

        private static FlappyHologramMarker[] DefaultMarkerSearch()
        {
            return Object.FindObjectsByType<FlappyHologramMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        private bool AnyBirdInside(Bounds bounds)
        {
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }

                if (entity.Get<GameFramework.World.Transform>() == null)
                {
                    continue;
                }

                    //  x·y만 본다 — 관문의 그림은 z [−2.5, 0]이고 새는 판정면 z = 0에 딱 붙어 있어,
                //  z까지 보면 경계에 걸친 부동소수 비교 한 번에 발동이 갈린다.
                Vector3 position = GameFramework.World.EntityMotionExtensions.GetPosition(entity);
                if (position.x >= bounds.min.x && position.x <= bounds.max.x
                    && position.y >= bounds.min.y && position.y <= bounds.max.y)
                {
                    return true;
                }
            }
            return false;
        }

        private HologramFxState GetState(FlappyHologramMarker marker)
        {
            if (states.TryGetValue(marker, out var state))
            {
                return state;
            }

            var renderer = marker.GetComponent<Renderer>();
            if (renderer == null)
            {
                return null;
            }

            Material original = renderer.sharedMaterial;
            if (original == null)
            {
                return null;   // 흔들 재질이 없다
            }

            //  renderer.material로 인스턴스를 만들면 에디트 모드에서 유니티가 경고 로그를 남긴다
            //  (테스트 프레임워크가 이걸 실패로 잡는다) — 같은 결과를 직접 복제해서 만든다.
            //  관문 셋이 같은 원본 재질을 공유해도 이러면 흔들림이 서로 안 섞인다.
            Material instance = new Material(original) { name = original.name + " (Hologram Instance)" };
            renderer.sharedMaterial = instance;

            state = new HologramFxState
            {
                Renderer = renderer,
                OriginalSharedMaterial = original,
                MaterialInstance = instance,
            };
            state.BaseAlpha = GetAlpha(state.MaterialInstance);
            //  조각은 마커 밑이 아니라 따로 둔 크기 1짜리 그릇에 담는다 — 마커는 (1.6, 4.37, 2.5)로
            //  늘여 놓은 상자라 그 밑에 두면 정사각 조각이 세로로 길쭉하게 늘어난다.
            state.ShardHolder = new GameObject($"HologramShards_{marker.name}");
            state.Shards = BuildShards(state.ShardHolder.transform, renderer.bounds, out state.ShardHomes, out state.ShardScattered);
            states[marker] = state;
            return state;
        }

        private void Animate(HologramFxState state, float t)
        {
            //  ① 알파 흔들림 — 사인파, 시간이 갈수록 잦아든다.
            if (t < ShakeDuration)
            {
                float decay = 1f - t / ShakeDuration;
                float wobble = Mathf.Sin(t * ShakeFrequency) * ShakeAmplitude * decay;
                SetAlpha(state.MaterialInstance, Mathf.Clamp01(state.BaseAlpha + wobble));
            }

            //  ② 조각 12개 — 흩어진 자리에서 제자리로 모인다.
            if (t < ShardDuration)
            {
                float shardT = t / ShardDuration;
                for (int i = 0; i < state.Shards.Length; i++)
                {
                    var shard = state.Shards[i];
                    if (shard == null)
                    {
                        continue;
                    }
                    if (shard.gameObject.activeSelf == false)
                    {
                        shard.gameObject.SetActive(true);
                    }
                    shard.position = Vector3.Lerp(state.ShardScattered[i], state.ShardHomes[i], shardT);
                }
            }
        }

        private void Reset(HologramFxState state)
        {
            SetAlpha(state.MaterialInstance, state.BaseAlpha);
            foreach (var shard in state.Shards)
            {
                if (shard != null)
                {
                    shard.gameObject.SetActive(false);
                }
            }
        }

        //  정면(카메라 쪽 면)에 4×3 격자로 흩뿌려 판 전체가 깨지는 느낌을 준다.
        private Transform[] BuildShards(Transform parent, Bounds bounds, out Vector3[] homes, out Vector3[] scattered)
        {
            EnsureShardMaterial();

            const int cols = 4;
            const int rows = 3;
            var shards = new Transform[ShardCount];
            homes = new Vector3[ShardCount];
            scattered = new Vector3[ShardCount];

            float frontZ = bounds.min.z;   // z가 작을수록 카메라(-z)에 가깝다
            for (int i = 0; i < ShardCount; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float u = (col + 0.5f) / cols;
                float v = (row + 0.5f) / rows;
                Vector3 home = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, u),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, v),
                    frontZ);

                var shardObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                shardObject.name = $"HologramShard_{i}";
                var collider = shardObject.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyObject(collider);
                }
                shardObject.GetComponent<MeshRenderer>().sharedMaterial = shardMaterial;

                var shard = shardObject.transform;
                shard.SetParent(parent, worldPositionStays: true);
                shard.localScale = Vector3.one * ShardSize;
                shard.position = home;
                shardObject.SetActive(false);

                shards[i] = shard;
                homes[i] = home;
                scattered[i] = home + Vector3.back * ShardScatterDistance;
            }
            return shards;
        }

        private void EnsureShardMaterial()
        {
            if (shardMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[FlappyHologramFx] URP Lit 셰이더를 못 찾아 조각 재질을 못 만들었다.");
                return;
            }

            shardMaterial = new Material(shader) { name = "HologramShard" };
            shardMaterial.SetColor("_BaseColor", ShardColor);
            shardMaterial.SetFloat("_Metallic", 0f);
            shardMaterial.SetFloat("_Smoothness", 0.2f);
            shardMaterial.SetFloat("_Surface", 1f);   // Transparent
            shardMaterial.SetFloat("_Blend", 0f);     // Alpha
            shardMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            shardMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            shardMaterial.SetFloat("_ZWrite", 0f);
            shardMaterial.SetFloat("_Cull", 0f);
            shardMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            shardMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static float GetAlpha(Material material)
        {
            return material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor").a : 1f;
        }

        private static void SetAlpha(Material material, float alpha)
        {
            if (material.HasProperty("_BaseColor") == false)
            {
                return;
            }
            Color c = material.GetColor("_BaseColor");
            c.a = alpha;
            material.SetColor("_BaseColor", c);
        }

        public void Dispose()
        {
            foreach (var state in states.Values)
            {
                //  인스턴스 재질을 지우기 전에 렌더러를 원본 공유 재질로 되돌린다 — 안 그러면
                //  렌더러가 곧 파괴될 재질을 계속 참조한 채 남는다(FlappyAtmosphere.Dispose와 같은 순서).
                if (state.Renderer != null && state.OriginalSharedMaterial != null)
                {
                    state.Renderer.sharedMaterial = state.OriginalSharedMaterial;
                }
                if (state.MaterialInstance != null)
                {
                    DestroyObject(state.MaterialInstance);
                }
                if (state.ShardHolder != null)
                {
                    DestroyObject(state.ShardHolder);
                }
            }
            states.Clear();

            if (shardMaterial != null)
            {
                DestroyObject(shardMaterial);
                shardMaterial = null;
            }
        }

        //  플레이 중이 아니면(EditMode 테스트 등) Destroy가 안 먹는다 — Unity가 요구하는 대로
        //  DestroyImmediate로 갈아 끼운다(FlappyAtmosphere.Dispose와 같은 분기).
        private static void DestroyObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }
            //  namespace LOP 안에 LOP.Application(MonoSingleton)이 따로 있어 UnityEngine.Application을
            //  풀네임으로 한정해야 한다 — 안 그러면 LOP.Application으로 잘못 잡혀 컴파일이 깨진다.
            if (UnityEngine.Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
