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
        // 코스 옆 위쪽 — 선반 두어 장과 그 아래 구름 층이 한 화면에 들어온다.
        private static readonly Vector3 Eye = new Vector3(230f, 2760f, -230f);
        private static readonly Vector3 Look = new Vector3(0f, 2200f, 0f);

        [MenuItem("LOP/Skydive/보기 캡처")]
        public static void Capture()
        {
            var go = new GameObject("__LookCam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.farClipPlane = 8000f;
                cam.nearClipPlane = 1f;
                cam.fieldOfView = 55f;
                go.transform.position = Eye;
                go.transform.LookAt(Look);

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
                Object.DestroyImmediate(go);
            }
        }
    }
}
