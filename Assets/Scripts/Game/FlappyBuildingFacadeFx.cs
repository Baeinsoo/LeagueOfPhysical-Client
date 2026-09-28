using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 빌딩 앞벽(<see cref="FlappyBuildingFacade"/>)의 클라 전용 연출. 내 새가 건물 안(X0~X1)에 있는 동안 앞벽을
    /// 반투명하게 해 안쪽 층판·기둥을 비추고, 나오면 되돌린다. 판정과 무관하다 — 통로 자리는 앞벽이 처음부터
    /// 없어서(빌더) 입구는 이 연출 없이도 보인다.
    /// </summary>
    public class FlappyBuildingFacadeFx : ITickable, System.IDisposable
    {
        private const float InsideAlpha = 0.2f;
        private const float FadeSeconds = 0.3f;
        //  맵이 additive로 늦게 올라올 수 있어 잠깐 재시도하고, 그래도 없으면 빌딩 없는 맵이라 그만 찾는다
        //  (FlappyHologramFx와 같은 규칙).
        private const float MarkerSearchWindow = 5f;
        private const float MarkerSearchInterval = 1f;

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly Dictionary<FlappyBuildingFacade, FacadeState> states = new Dictionary<FlappyBuildingFacade, FacadeState>();
        private FlappyBuildingFacade[] markers;
        private float markerSearchClock;
        private float nextMarkerSearchAt;
        private bool markerSearchGivenUp;

        //  테스트 전용 seam — 실제로는 씬을 뒤지고, EditMode 테스트는 가짜 탐색을 끼운다.
        internal System.Func<FlappyBuildingFacade[]> markerSearch = DefaultMarkerSearch;

        private class FacadeState
        {
            public Renderer[] Renderers;
            public Material[] Originals;
            public Material Instance;
            public float BaseAlpha;
            public float Alpha;
        }

        public FlappyBuildingFacadeFx(IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
        }

        public void Tick()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            EnsureMarkers(deltaTime);
            if (markers == null || markers.Length == 0)
            {
                return;
            }
            float? myX = LocalBirdX();
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
                bool inside = myX.HasValue && myX.Value >= marker.X0 && myX.Value <= marker.X1;
                float target = inside ? InsideAlpha : state.BaseAlpha;
                float rate = Mathf.Abs(state.BaseAlpha - InsideAlpha) / FadeSeconds;
                state.Alpha = Mathf.MoveTowards(state.Alpha, target, rate * deltaTime);
                SetAlpha(state.Instance, state.Alpha);
            }
        }

        internal float AlphaOf(FlappyBuildingFacade marker) => states.TryGetValue(marker, out var s) ? s.Alpha : -1f;

        private float? LocalBirdX()
        {
            if (string.IsNullOrEmpty(playerContext.entityId))
            {
                return null;   // 아직 참가 전
            }
            var transform = entityRegistry.Get(playerContext.entityId)?.Get<GameFramework.World.Transform>();
            return transform == null ? (float?)null : transform.Position.X;
        }

        private void EnsureMarkers(float deltaTime)
        {
            if ((markers != null && markers.Length > 0) || markerSearchGivenUp)
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
                markerSearchGivenUp = true;
                return;
            }
            nextMarkerSearchAt = markerSearchClock + MarkerSearchInterval;
        }

        private static FlappyBuildingFacade[] DefaultMarkerSearch()
        {
            return Object.FindObjectsByType<FlappyBuildingFacade>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        private FacadeState GetState(FlappyBuildingFacade marker)
        {
            if (states.TryGetValue(marker, out var state))
            {
                return state;
            }
            var renderers = marker.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0 || renderers[0].sharedMaterial == null)
            {
                return null;
            }
            //  앞벽 조각이 수십 개라 재질 하나를 복제해 같이 쓴다 — 조각마다 복제하면 알파를 수십 번 바꿔야 한다.
            //  renderer.material은 에디트 모드에서 경고를 남겨(테스트가 실패로 잡는다) 직접 복제한다.
            Material original = renderers[0].sharedMaterial;
            var instance = new Material(original) { name = original.name + " (Facade Instance)" };
            var originals = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originals[i] = renderers[i].sharedMaterial;
                renderers[i].sharedMaterial = instance;
            }
            float alpha = GetAlpha(instance);
            state = new FacadeState
            {
                Renderers = renderers,
                Originals = originals,
                Instance = instance,
                BaseAlpha = alpha,
                Alpha = alpha,
            };
            states[marker] = state;
            return state;
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
                for (int i = 0; i < state.Renderers.Length; i++)
                {
                    if (state.Renderers[i] != null)
                    {
                        state.Renderers[i].sharedMaterial = state.Originals[i];
                    }
                }
                DestroyObject(state.Instance);
            }
            states.Clear();
        }

        //  플레이 중이 아니면(EditMode 테스트) Destroy가 안 먹어 DestroyImmediate로 갈아 끼운다.
        private static void DestroyObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }
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
