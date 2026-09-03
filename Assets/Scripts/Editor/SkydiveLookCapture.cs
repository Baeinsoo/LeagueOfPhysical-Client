using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 늘 같은 시점에서 게임뷰를 찍는다. 아트는 유닛테스트가 안 되므로, 같은 자리에서 찍은
    /// 전/후를 나란히 놓는 것이 유일하게 재현 가능한 확인 방법이다.
    /// </summary>
    public static class SkydiveLookCapture
    {
        // 코스 옆 위쪽 — 구름 층(2900/2730) 사이 빈 틈에서 찍는다. 원래 230/-230은 구름이
        // 생기기 전 스파이크에서 잰 값이라 밴드 안에 들어가 있었다(흰 벽만 찍힘) — 85m 위로
        // 옮기고, XZ도 안쪽으로 당겨 시선이 실제로 맨 위 선반(y=2600, X·Z ±100) 위를 지나가게
        // 했다(e=130일 때 y=2600 지점 x=z≈84.55, ±100 안).
        private static readonly Vector3 Eye = new Vector3(130f, 2815f, -130f);
        private static readonly Vector3 Look = new Vector3(0f, 2200f, 0f);

        // 실제 플레이 중엔 Skydive.unity가 태양을 제공하지만, 이 도구는 SkydiveMap 하나만 열어
        // 찍으므로 태양이 없다 — Skydive.unity의 조명값을 흉내 낸 임시 태양.
        private static readonly Quaternion TempSunRotation = Quaternion.Euler(42f, -35f, 0f);
        private static readonly Color TempSunColor = new Color(1f, 0.968627451f, 0.901960784f); // #FFF7E6
        private const float TempSunIntensity = 1.15f;

        [MenuItem("LOP/Skydive/보기 캡처")]
        public static void Capture()
        {
            var go = new GameObject("__LookCam");
            Light tempSun = null;
            Light previousSun = RenderSettings.sun;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.farClipPlane = 8000f;
                cam.nearClipPlane = 1f;
                cam.fieldOfView = 55f;
                go.transform.position = Eye;
                go.transform.LookAt(Look);

                if (HasActiveDirectionalLight() == false)
                {
                    // 프로시저럴 스카이박스는 태양이 없으면 그림이 깨진다 — 찍는 동안만 임시로 켠다.
                    var sunGo = new GameObject("__LookCamSun");
                    tempSun = sunGo.AddComponent<Light>();
                    tempSun.type = LightType.Directional;
                    tempSun.transform.rotation = TempSunRotation;
                    tempSun.color = TempSunColor;
                    tempSun.intensity = TempSunIntensity;
                    RenderSettings.sun = tempSun;
                    DynamicGI.UpdateEnvironment();
                }

                var rt = new RenderTexture(1280, 720, 24);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var shot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                shot.Apply();
                RenderTexture.active = null;

                string path = $"Temp/skydive-look-{System.DateTime.Now:HHmmss}.png";
                System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());

                // 대화상자를 쓰지 않는다 — 모달은 메인 스레드를 잡아 CLI 자동화를 멈춘다.
                Debug.Log($"[Skydive] 캡처: {path}");

                Object.DestroyImmediate(shot);
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            finally
            {
                if (tempSun != null)
                {
                    RenderSettings.sun = previousSun;   // 없애기가 아니라 원래 태양으로 되돌린다
                    Object.DestroyImmediate(tempSun.gameObject);
                }
                Object.DestroyImmediate(go);
            }
        }

        private static bool HasActiveDirectionalLight()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional && light.isActiveAndEnabled)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
