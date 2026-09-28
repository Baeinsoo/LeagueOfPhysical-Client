using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 활쏘기의 하늘·안개·앰비언트·해. 맵은 additive라 활성 씬(Room)의 RenderSettings만 먹는다 — 코드로 켜고, 나갈 때 되돌린다
    /// (<see cref="FlappyAtmosphere"/>와 같은 이유). 안개는 90m 과녁이 보이게 멀리서 시작한다.
    /// <para>맵(과 그 흰 해)은 스코프 시작 뒤에 뜬다 — 씬이 뜰 때마다 해를 다시 고른다. 카메라의 Skybox 컴포넌트는
    /// RenderSettings.skybox를 덮으므로 있는 동안 꺼 둔다.</para>
    /// </summary>
    public class ArcheryAtmosphere : IStartable, System.IDisposable
    {
        public const float FogStart = 70f;
        public const float FogEnd = 260f;

        private readonly bool fog;
        private readonly FogMode fogMode;
        private readonly Color fogColor;
        private readonly float fogStart, fogEnd;
        private readonly Material skybox;
        private readonly AmbientMode ambientMode;
        private readonly Color ambientSky, ambientEquator, ambientGround;
        private readonly Light sun;
        private readonly List<Light> dimmed = new List<Light>();
        private readonly List<Skybox> hiddenBoxes = new List<Skybox>();
        private readonly string mainSceneName;
        private bool listening;
        private Light mainSun;
        private Color mainColor;
        private Material sky;
        private bool applied;

        [VContainer.Inject]
        public ArcheryAtmosphere() : this("Archery")
        {
        }

        /// <param name="mainSceneName">주인공 해가 있는 씬(게임 씬). 시험은 열린 씬 이름을 준다.</param>
        public ArcheryAtmosphere(string mainSceneName)
        {
            this.mainSceneName = mainSceneName;
            fog = RenderSettings.fog;
            fogMode = RenderSettings.fogMode;
            fogColor = RenderSettings.fogColor;
            fogStart = RenderSettings.fogStartDistance;
            fogEnd = RenderSettings.fogEndDistance;
            skybox = RenderSettings.skybox;
            ambientMode = RenderSettings.ambientMode;
            ambientSky = RenderSettings.ambientSkyColor;
            ambientEquator = RenderSettings.ambientEquatorColor;
            ambientGround = RenderSettings.ambientGroundColor;
            sun = RenderSettings.sun;
        }

        public void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            listening = true;
            Apply();
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (applied)
            {
                PickSun();
                HideCameraSkyboxes();
            }
        }

        public void Apply()
        {
            sky = new Material(Shader.Find("LOP/WatercolorSky"));
            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("#A9D6F0");
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#FFF4E0");
            RenderSettings.ambientEquatorColor = Hex("#C8D8F0");
            RenderSettings.ambientGroundColor = Hex("#7F92D8");
            PickSun();
            HideCameraSkyboxes();
            DynamicGI.UpdateEnvironment();
            applied = true;
        }

        public void Dispose()
        {
            if (listening)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                listening = false;
            }
            if (applied == false)
            {
                return;
            }
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.sun = sun;
            foreach (var l in dimmed)
            {
                if (l != null)
                {
                    l.enabled = true;
                }
            }
            dimmed.Clear();
            foreach (var box in hiddenBoxes)
            {
                if (box != null)
                {
                    box.enabled = true;
                }
            }
            hiddenBoxes.Clear();
            if (mainSun != null)
            {
                mainSun.color = mainColor;
                mainSun = null;
            }
            if (sky != null)
            {
                if (UnityEngine.Application.isPlaying) { Object.Destroy(sky); } else { Object.DestroyImmediate(sky); }   // 편집 모드 시험에서도 부른다
                sky = null;
            }
            DynamicGI.UpdateEnvironment();
            applied = false;
        }

        /// <summary>선형 안개 비율(0 = 맑음, 1 = 안개색). 시험이 먼 과녁의 가림을 잰다.</summary>
        public static float FogAmount(float distance) => Mathf.Clamp01((distance - FogStart) / (FogEnd - FogStart));

        //  해가 둘이다(게임 씬 따뜻한 해 + 맵 흰 해). 게임 씬(Archery) 해를 주인공으로, 나머지 방향광은 끈다.
        private void PickSun()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            if (mainSun == null)
            {
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional && l.enabled && l.gameObject.scene.name == mainSceneName)
                    {
                        mainSun = l;
                        break;
                    }
                }
                if (mainSun == null)
                {
                    return;
                }
                mainColor = mainSun.color;
                mainSun.color = Hex("#FFF1D8");
                RenderSettings.sun = mainSun;
            }
            var main = mainSun;
            foreach (var l in lights)
            {
                if (l != main && l.type == LightType.Directional && l.enabled)
                {
                    l.enabled = false;
                    dimmed.Add(l);
                }
            }
        }

        private void HideCameraSkyboxes()
        {
            foreach (var box in Object.FindObjectsByType<Skybox>(FindObjectsSortMode.None))
            {
                if (box.enabled)
                {
                    box.enabled = false;
                    hiddenBoxes.Add(box);
                }
            }
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
