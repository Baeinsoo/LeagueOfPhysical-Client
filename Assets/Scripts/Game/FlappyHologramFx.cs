using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

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

        private class HologramFxState
        {
            public Renderer Renderer;
            public Material MaterialInstance;
            public float BaseAlpha;
            public bool WasInside;
            public bool Active;
            public float Elapsed;
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
            EnsureMarkers();
            if (markers == null || markers.Length == 0)
            {
                return;
            }

            float dt = Time.deltaTime;
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
                }
                state.WasInside = inside;

                if (state.Active == false)
                {
                    continue;
                }

                Animate(state, state.Elapsed);
                state.Elapsed += dt;
                if (state.Elapsed >= maxDuration)
                {
                    state.Active = false;
                    Reset(state);
                }
            }
        }

        //  스코프 시작(첫 틱)에 한 번 찾고, 못 찾으면 최대 5초 동안 1초 간격으로 재시도한 뒤
        //  그만둔다 — 홀로그램이 없는 맵에서 매 틱 FindObjectsByType을 영원히 돌리지 않는다.
        private void EnsureMarkers()
        {
            if (markers != null && markers.Length > 0)
            {
                return;
            }
            if (markerSearchGivenUp)
            {
                return;
            }

            markerSearchClock += Time.deltaTime;
            if (markerSearchClock < nextMarkerSearchAt)
            {
                return;
            }

            var found = Object.FindObjectsByType<FlappyHologramMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (found.Length > 0)
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

                if (bounds.Contains(GameFramework.World.EntityMotionExtensions.GetPosition(entity)))
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

            state = new HologramFxState
            {
                Renderer = renderer,
                //  .material은 첫 접근에서 자동으로 개별 복사본을 만든다 — 관문 셋이 같은 원본
                //  재질을 공유해도 흔들림은 서로 안 섞인다.
                MaterialInstance = renderer.material,
            };
            state.BaseAlpha = GetAlpha(state.MaterialInstance);
            state.Shards = BuildShards(marker.transform, renderer.bounds, out state.ShardHomes, out state.ShardScattered);
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
                    Object.Destroy(collider);
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
                if (state.MaterialInstance != null)
                {
                    Object.Destroy(state.MaterialInstance);
                }
                if (state.Shards != null)
                {
                    foreach (var shard in state.Shards)
                    {
                        if (shard != null)
                        {
                            Object.Destroy(shard.gameObject);
                        }
                    }
                }
            }
            states.Clear();

            if (shardMaterial != null)
            {
                Object.Destroy(shardMaterial);
                shardMaterial = null;
            }
        }
    }
}
