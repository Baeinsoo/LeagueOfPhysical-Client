using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace LOP.EditorTools
{
    /// <summary>
    /// 에디터 창의 그림을 PNG로 저장한다(시안 비교용). 화면 픽셀을 읽으면 위에 겹친 다른 앱 창까지
    /// 찍히므로, 창이 자기 그림을 그려 둔 버퍼를 직접 읽는다(에디터 내부 GrabPixels).
    /// </summary>
    public static class UiSnapshot
    {
        private const int MaxWidth = 1920;
        private const int MaxHeight = 1080;

        //  20:9(2400×1080)·4:3(1920×1440) 패널은 모니터보다 커서 1920×1080 창에 들어가게 줄여 그린다.
        private static float FitScale(int width, int height) => Mathf.Min(1f, Mathf.Min((float)MaxWidth / width, (float)MaxHeight / height));

        /// <summary>패널 크기(width×height)의 바탕을 root에 붙인다. 창보다 크면 줄여 그린다(배치는 원래 크기 그대로).</summary>
        public static VisualElement AddHost(VisualElement root, int width, int height)
        {
            var host = new VisualElement();
            host.style.width = width;
            host.style.height = height;
            host.style.flexShrink = 0;
            float s = FitScale(width, height);
            host.style.transformOrigin = new TransformOrigin(0, 0);
            host.style.scale = new Scale(new Vector3(s, s, 1));
            root.Add(host);
            return host;
        }

        /// <summary>창을 패널 크기(width×height, 줄인 크기)로 맞춘 뒤 1초 뒤에 PNG로 저장한다.</summary>
        public static void Save(EditorWindow w, string relativePath, int width = 1920, int height = 1080)
        {
            float s = FitScale(width, height);
            w.position = new Rect(40, 40, Mathf.Round(width * s), Mathf.Round(height * s) + 22);
            w.Repaint();
            double at = EditorApplication.timeSinceStartup + 1.0;
            void Tick()
            {
                if (EditorApplication.timeSinceStartup < at) return;
                EditorApplication.update -= Tick;
                Grab(w, relativePath);
            }
            EditorApplication.update += Tick;
        }

        /// <summary>
        /// 여러 장을 차례로 찍는다 — 앞 장을 저장한 뒤 다음 장 창을 띄운다. show는 찍을 그림을 띄운 창을 돌려준다.
        /// </summary>
        public static void SaveAll(IReadOnlyList<(Func<EditorWindow> show, string path, int width, int height)> shots)
        {
            int next = 0;
            double at = 0;
            void Tick()
            {
                if (EditorApplication.timeSinceStartup < at) return;
                if (next >= shots.Count)
                {
                    EditorApplication.update -= Tick;
                    return;
                }
                var shot = shots[next++];
                Save(shot.show(), shot.path, shot.width, shot.height);
                //  Save는 1초 뒤에 찍는다 — 그 전에 창을 바꾸면 다음 그림이 찍힌다.
                at = EditorApplication.timeSinceStartup + 1.5;
            }
            EditorApplication.update += Tick;
        }

        private static void Grab(EditorWindow w, string relativePath)
        {
            var parent = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(w);
            var grab = parent?.GetType().GetMethod("GrabPixels", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (grab == null)
            {
                Debug.LogError("창 그림을 읽지 못했다 — 이 유니티 버전에 GrabPixels가 없다.");
                return;
            }

            //  창 전체(탭 줄 포함)를 픽셀 단위로 읽는다.
            float scale = EditorGUIUtility.pixelsPerPoint;
            int pw = Mathf.RoundToInt(w.position.width * scale);
            int ph = Mathf.RoundToInt(w.position.height * scale);
            //  창 버퍼는 이미 화면 색(감마)이라 그대로 옮긴다 — sRGB 변환을 한 번 더 하면 색이 바랜다.
            var rt = new RenderTexture(pw, ph, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            grab.Invoke(parent, new object[] { rt, new Rect(0, 0, pw, ph) });

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(pw, ph, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, pw, ph), 0, 0);
            RenderTexture.active = previous;

            //  창 버퍼는 위아래가 뒤집혀 있다.
            var pixels = tex.GetPixels32();
            var flipped = new Color32[pixels.Length];
            for (int y = 0; y < ph; y++) System.Array.Copy(pixels, y * pw, flipped, (ph - 1 - y) * pw, pw);
            tex.SetPixels32(flipped);
            tex.Apply();

            var path = Path.GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"PNG 저장: {path}");

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
