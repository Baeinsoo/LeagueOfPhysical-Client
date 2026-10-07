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
        private readonly Light originalSun;
        private bool disposed;

        //  해 — 게임 씬(Skydive) 방향광을 주인공으로, 나머지(맵 쪽) 방향광은 끈다. 씬 수가 바뀔 때만 다시 고른다.
        private readonly System.Collections.Generic.List<Light> dimmedSuns = new System.Collections.Generic.List<Light>();
        private int sunSceneCount = -1;

        //  Skydive.unity 카메라의 Skybox 컴포넌트는 RenderSettings.skybox를 덮는다(예전 고도 틴트가 안 보인 이유) — 있는 동안 끈다.
        private readonly System.Collections.Generic.List<Skybox> hiddenBoxes = new System.Collections.Generic.List<Skybox>();

        //  맵별 분위기(SkydiveMood) — 있으면 고도 곡선 대신 이것을 쓴다(빛을 향한 낙하: 위는 어둡게, 출구는 금빛).
        private SkydiveMood mood;
        private int moodSceneCount = -1;
        private readonly UnityEngine.Rendering.AmbientMode originalAmbientMode = RenderSettings.ambientMode;
        private readonly Color originalAmbient = RenderSettings.ambientLight;
        private Color originalSunColor;
        private float originalSunIntensity;
        private bool sunTouched;
        private UnityEngine.Rendering.Volume volume;
        private UnityEngine.Rendering.VolumeProfile originalProfile;
        private UnityEngine.Rendering.VolumeProfile profileInstance;
        private UnityEngine.Rendering.Universal.Bloom bloom;
        private UnityEngine.Rendering.Universal.ColorAdjustments colorAdjust;

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
            originalSun = RenderSettings.sun;
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
            if (sunTouched && RenderSettings.sun != null)
            {
                RenderSettings.sun.color = originalSunColor;
                RenderSettings.sun.intensity = originalSunIntensity;
            }
            RenderSettings.sun = originalSun;
            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientLight = originalAmbient;
            if (volume != null && originalProfile != null)
            {
                volume.sharedProfile = originalProfile;   // 복사본만 칠했다 — 에셋은 그대로
            }
            if (profileInstance != null)
            {
                if (UnityEngine.Application.isPlaying) { Object.Destroy(profileInstance); } else { Object.DestroyImmediate(profileInstance); }
                profileInstance = null;
            }
            foreach (var l in dimmedSuns)
            {
                if (l != null) { l.enabled = true; }
            }
            dimmedSuns.Clear();
            foreach (var b in hiddenBoxes)
            {
                if (b != null) { b.enabled = true; }
            }
            hiddenBoxes.Clear();

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
            FindMoodIfScenesChanged();
            if (mood != null && SkydiveMoodCurve.TryEvaluate(mood.Keys, altitude, out var m))
            {
                ApplyMood(m);
                return;
            }
            var colors = SkydiveSkyGradient.Evaluate(altitude);

            // 안개 on/off·모드도 여기서 정한다 — 씬 파일(RenderSettings)에 맡기면 씬이 additive로
            // 로드될 때 활성 씬(m_Fog=0)이 이긴다(활성 씬 기준으로만 안개가 적용됨).
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = colors.fog;
            RenderSettings.fogDensity = SkydiveCloudLayers.DensityAt(altitude);

            UseWatercolorSky();
            PaintSky(skyboxInstance, colors.fog);
            PickSunIfScenesChanged();
        }

        /// <summary>맵별 분위기 한 점을 화면에 — 안개·주변광·하늘 지평선·햇빛·블룸·노출.</summary>
        private void ApplyMood(in SkydiveMoodKey m)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = m.Fog;
            RenderSettings.fogDensity = m.FogDensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = m.Ambient;
            UseWatercolorSky();
            PaintSky(skyboxInstance, m.Fog);
            PickSunIfScenesChanged();
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                if (sunTouched == false)
                {
                    originalSunColor = sun.color;
                    originalSunIntensity = sun.intensity;
                    sunTouched = true;
                }
                sun.color = m.Sun;
                sun.intensity = m.SunIntensity;
            }
            EnsurePost();
            if (bloom != null) { bloom.intensity.Override(m.Bloom); }
            if (colorAdjust != null) { colorAdjust.postExposure.Override(m.Exposure); }
        }

        private void FindMoodIfScenesChanged()
        {
            int count = UnityEngine.SceneManagement.SceneManager.sceneCount;
            if (count == moodSceneCount)
            {
                return;   // 맵이 additive로 늦게 뜬다 — 씬 수가 바뀔 때만 다시 찾는다(분위기 없는 맵에서 매 프레임 훑지 않게)
            }
            moodSceneCount = count;
            mood = Object.FindFirstObjectByType<SkydiveMood>();
        }

        //  후처리는 전역 볼륨 프로필의 복사본만 칠한다(에셋이 더러워지지 않게 — 스카이박스와 같은 이유).
        private void EnsurePost()
        {
            if (profileInstance != null)
            {
                return;
            }
            foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
            {
                if (v.isGlobal && v.sharedProfile != null)
                {
                    volume = v;
                    break;
                }
            }
            if (volume == null)
            {
                return;
            }
            originalProfile = volume.sharedProfile;
            profileInstance = Object.Instantiate(originalProfile);
            volume.sharedProfile = profileInstance;
            if (profileInstance.TryGet(out bloom) == false) { bloom = profileInstance.Add<UnityEngine.Rendering.Universal.Bloom>(true); }
            if (profileInstance.TryGet(out colorAdjust) == false) { colorAdjust = profileInstance.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true); }
        }

        /// <summary>새 룩(슬라이스 4) — 하늘은 수채화 하늘 복사본 하나. 처음 한 번 만들고 이후엔 그것만 칠한다.</summary>
        public void UseWatercolorSky()
        {
            if (skyboxInstance != null)
            {
                return;
            }
            var shader = Shader.Find("LOP/WatercolorSky");
            if (shader == null)
            {
                return;   // 셰이더가 빠진 빌드 — 원래 하늘 그대로(분홍보다 낫다)
            }
            skyboxInstance = new Material(shader);
            RenderSettings.skybox = skyboxInstance;
        }

        /// <summary>지평선을 안개색에 맞춘다 — 멀수록 하늘로 녹아드는 공기 원근(왕눈). 아래쪽은 조금 밝게.</summary>
        public static void PaintSky(Material sky, Color fog)
        {
            if (sky == null)
            {
                return;
            }
            sky.SetColor("_HorizonColor", fog);
            sky.SetColor("_BottomColor", Color.Lerp(fog, Color.white, 0.25f));
        }

        private void PickSunIfScenesChanged()
        {
            int count = UnityEngine.SceneManagement.SceneManager.sceneCount;
            if (count == sunSceneCount)
            {
                return;
            }
            sunSceneCount = count;
            foreach (var b in Object.FindObjectsByType<Skybox>(FindObjectsSortMode.None))
            {
                if (b.enabled)
                {
                    b.enabled = false;
                    hiddenBoxes.Add(b);
                }
            }
            Light main = RenderSettings.sun != null && RenderSettings.sun.gameObject.scene.name == "Skydive" ? RenderSettings.sun : null;
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (main == null && l.type == LightType.Directional && l.enabled && l.gameObject.scene.name == "Skydive")
                {
                    main = l;
                }
            }
            if (main == null)
            {
                return;
            }
            RenderSettings.sun = main;
            foreach (var l in lights)
            {
                if (l != main && l.type == LightType.Directional && l.enabled)
                {
                    l.enabled = false;
                    dimmedSuns.Add(l);
                }
            }
        }
    }
}
