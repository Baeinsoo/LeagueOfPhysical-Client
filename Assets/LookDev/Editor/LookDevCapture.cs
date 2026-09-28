using System.IO;
using UnityEditor;
using UnityEngine;

namespace LOP.LookDevEditor
{
    /// <summary>룩 개발 씬의 카메라 셋(가까이·게임 거리·얼굴 정면)을 1920×1080 PNG로 남긴다(`output/lookdev/`). 후처리·안개·그림자가 들어간 화면.</summary>
    public static class LookDevCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;

        [MenuItem("LOP/LookDev/Capture")]
        public static void Capture()
        {
            Directory.CreateDirectory("output/lookdev");
            Save("LookDevCam_Near", "output/lookdev/near.png");
            Save("LookDevCam_Game", "output/lookdev/game.png");
            Save("LookDevCam_Face", "output/lookdev/face.png");
        }

        private static void Save(string cameraName, string path)
        {
            var go = GameObject.Find(cameraName);
            var cam = go != null ? go.GetComponent<Camera>() : null;
            if (cam == null)
            {
                Debug.LogError("[LookDev] 카메라 없음: " + cameraName);
                return;
            }
            var rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;

            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[LookDev] 캡처: " + path);
        }
    }
}
