using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 안개·하늘을 코스 진행률에 맞춰 매 프레임 갱신한다. <see cref="SkydiveAtmosphere"/>의 짝이고
    /// 축만 고도(y)가 아니라 진행(x)이다.
    ///
    /// <para>월드에서 <b>읽기만</b> 한다 — 시뮬은 자신이 관찰되는 것을 모른다. 연속 상태라
    /// 이벤트가 아니라 pull이다(world-core-connection-architecture.md).</para>
    ///
    /// <para><b>씬에 맡기지 않는 이유</b>: 맵 씬은 additive로 로드되고 유니티는 <i>활성 씬</i>의
    /// RenderSettings만 적용한다. 맵 씬에 안개를 켜 저장해도 활성 씬(꺼짐)이 이긴다.</para>
    /// </summary>
    public class FlappyAtmosphere : VContainer.Unity.ITickable, System.IDisposable
    {
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        //  결승선은 씬에서 찾는다. 못 찾으면 진행률이 0에 머물러 하늘이 안 변할 뿐 터지지 않는다.
        private float courseStartX;
        private float courseLength;
        private bool measured;

        //  원본 스카이박스는 서브모듈 에셋(공유)이라 그대로 칠하면 플레이할 때마다 .mat이
        //  더러워진다. 처음 한 번만 복사본을 만들어 그것만 칠한다.
        private Material skyboxInstance;

        private readonly bool originalFog;
        private readonly FogMode originalFogMode;
        private readonly Color originalFogColor;
        private readonly float originalFogDensity;
        private readonly Material originalSkybox;
        private bool disposed;

        public FlappyAtmosphere(IPlayerContext playerContext,
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

            Measure();
            Apply(LOP.MapTools.CourseSectionRule.Progress(transform.Position.X, courseStartX, courseLength));
        }

        /// <summary>진행률 하나로 대기 전체가 정해진다. 테스트가 이 문으로 들어온다.</summary>
        public void Apply(float progress)
        {
            var sky = FlappySkyGradient.Evaluate(progress);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = sky.fog;
            RenderSettings.fogDensity = sky.density;

            TintSky(sky.skyTint);
        }

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

        //  결승선 x가 곧 코스 길이다 — 맵마다 다른 값이라 상수로 박지 않는다.
        private void Measure()
        {
            if (measured)
            {
                return;
            }
            var finish = Object.FindFirstObjectByType<FinishLine>(FindObjectsInactive.Include);
            if (finish == null)
            {
                return;   // 맵이 아직 안 올라왔다 — 다음 틱에 다시 본다
            }
            courseStartX = 0f;
            courseLength = finish.transform.position.x - courseStartX;
            measured = true;
        }

        private void TintSky(Color skyTint)
        {
            if (skyboxInstance == null)
            {
                Material source = RenderSettings.skybox;
                if (source == null)
                {
                    return;
                }
                skyboxInstance = new Material(source);
                RenderSettings.skybox = skyboxInstance;
            }

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
