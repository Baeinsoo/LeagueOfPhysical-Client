using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 대기(안개색·밀도·하늘 틴트)를 내 고도에 맞춰 매 프레임 갱신한다.
    ///
    /// 월드에서 높이를 <b>읽기만</b> 한다 — 시뮬은 자신이 관찰되는 것을 모른다.
    /// 연속 상태라 이벤트가 아니라 pull이다(world-core-connection-architecture.md).
    ///
    /// 안개 밀도는 여기서만 쓴다. 씬에서 읽어 오지 않는 이유 — 맵이 additive로 늦게 로드되면
    /// 기준값을 0으로 물을 수 있고, 읽는 쪽과 쓰는 쪽이 같아져 값이 누적된다.
    /// </summary>
    public class SkydiveAtmosphere : VContainer.Unity.ITickable, System.IDisposable
    {
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        // RenderSettings.skybox는 프로젝트 에셋(공유, git submodule)이라 그대로 칠하면 플레이할
        // 때마다 .mat 파일이 더러워진다. 그래서 처음 한 번만 복사본을 만들어 그것만 칠한다.
        private Material skyboxInstance;

        // 시작할 때의 안개·하늘 값. Dispose에서 되돌려 다음 게임모드로 새어나가지 않게 한다.
        private readonly bool originalFog;
        private readonly FogMode originalFogMode;
        private readonly Color originalFogColor;
        private readonly float originalFogDensity;
        private readonly Material originalSkybox;
        private bool disposed;

        public SkydiveAtmosphere(IPlayerContext playerContext,
                                 GameFramework.World.EntityRegistry entityRegistry)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;

            originalFog = RenderSettings.fog;
            originalFogMode = RenderSettings.fogMode;
            originalFogColor = RenderSettings.fogColor;
            originalFogDensity = RenderSettings.fogDensity;
            originalSkybox = RenderSettings.skybox;
        }

        /// <summary>안개·하늘을 시작 전 값으로 되돌리고 복제한 스카이박스를 정리한다.</summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;

            RenderSettings.fog = originalFog;
            RenderSettings.fogMode = originalFogMode;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogDensity = originalFogDensity;
            RenderSettings.skybox = originalSkybox;

            if (skyboxInstance != null)
            {
                // 플레이 중이 아니면(에디터/테스트) DestroyImmediate — Destroy는 다음 프레임까지
                // 미뤄져 에디터·테스트에선 자국이 남는다.
                // (같은 namespace LOP에 MonoSingleton `LOP.Application`이 있어 짧은 이름은 그쪽으로 잡힌다.)
                if (UnityEngine.Application.isPlaying)
                {
                    Object.Destroy(skyboxInstance);
                }
                else
                {
                    Object.DestroyImmediate(skyboxInstance);
                }
                skyboxInstance = null;
            }
        }

        public void Tick()
        {
            if (string.IsNullOrEmpty(playerContext.entityId))
            {
                return;   // 아직 참가 전 — 손대지 않는다
            }

            var entity = entityRegistry.Get(playerContext.entityId);
            var transform = entity?.Get<GameFramework.World.Transform>();
            if (transform == null)
            {
                return;
            }

            Apply(transform.Position.Y);
        }

        /// <summary>고도 하나로 대기 전체가 정해진다. 테스트가 이 문으로 들어온다.</summary>
        public void Apply(float altitude)
        {
            var colors = SkydiveSkyGradient.Evaluate(altitude);

            // 안개 on/off·모드도 여기서 정한다 — 씬 파일(RenderSettings)에 맡기면 씬이 additive로
            // 로드될 때 활성 씬(m_Fog=0)이 이긴다(활성 씬 기준으로만 안개가 적용됨).
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = colors.fog;
            RenderSettings.fogDensity = SkydiveCloudLayers.DensityAt(altitude);

            TintSky(colors.skyTint);
        }

        private void TintSky(Color skyTint)
        {
            if (skyboxInstance == null)
            {
                Material source = RenderSettings.skybox;
                if (source == null)
                {
                    return;   // 아직 하늘 머티리얼이 없으면 칠할 게 없다
                }

                // 복사는 딱 한 번 — 이후 RenderSettings.skybox는 항상 이 복사본을 가리킨다.
                skyboxInstance = new Material(source);
                RenderSettings.skybox = skyboxInstance;
            }

            // Procedural 스카이박스는 _SkyTint, 그 외 일부 셰이더는 _Tint를 쓴다.
            if (skyboxInstance.HasProperty("_SkyTint"))
            {
                skyboxInstance.SetColor("_SkyTint", skyTint);
            }
            else if (skyboxInstance.HasProperty("_Tint"))
            {
                skyboxInstance.SetColor("_Tint", skyTint);
            }
        }
    }
}
