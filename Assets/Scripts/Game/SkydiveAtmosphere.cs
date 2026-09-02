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
    public class SkydiveAtmosphere : VContainer.Unity.ITickable
    {
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        // RenderSettings.skybox는 프로젝트 에셋(공유, git submodule)이라 그대로 칠하면 플레이할
        // 때마다 .mat 파일이 더러워진다. 그래서 처음 한 번만 복사본을 만들어 그것만 칠한다.
        private Material skyboxInstance;

        public SkydiveAtmosphere(IPlayerContext playerContext,
                                 GameFramework.World.EntityRegistry entityRegistry)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
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
