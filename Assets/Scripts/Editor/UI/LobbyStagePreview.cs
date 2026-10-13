using System.IO;
using LOP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LOP.EditorTools
{
    /// <summary>
    /// 로비 3D 무대를 미리보기 씬에 세우고 카메라 렌더를 PNG로 저장한다(화면을 찍지 않는다).
    /// 로비 UI 구역(버튼 줄·PLAY 묶음·이름표·띠)을 빨간 테두리로 그려 선수가 가리는지 본다.
    /// </summary>
    public static class LobbyStagePreview
    {
        [MenuItem("LOP/UI/로비 3D 미리보기 PNG 저장")]
        public static void SavePngs()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var oldAmbient = RenderSettings.ambientLight;
            var oldMode = RenderSettings.ambientMode;
            GameObject root = null;
            try
            {
                root = new GameObject("PreviewStage");
                SceneManager.MoveGameObjectToScene(root, scene);
                LobbyTunnel.Build(root.transform, new TunnelLayout(), out var arena);
                var lights = LobbyStage.CreateLights(root.transform, arena, null);
                var rig = StageRig.Build(root.transform, new TunnelLayout());

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPlayer.ChibiKey);
                var face = AssetDatabase.LoadAssetAtPath<Material>(LobbyPlayer.FaceKey);
                if (prefab != null)
                {
                    var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    body.transform.position = LobbyPlayer.StandPosition;
                    body.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/Chibi/Anim/Idle.anim");
                    if (idle != null) idle.SampleAnimation(body, 0f);
                    if (ChibiDresser.IsChibi(body)) ChibiDresser.Dress("preview-user", body, face);
                }

                var camGo = new GameObject("PreviewCamera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                LobbyStage.ConfigureCamera(cam);

                foreach (var (w, h, tag) in new[] { (1920, 1080, "16x9"), (2400, 1080, "20x9"), (1920, 1440, "4x3") })
                {
                    foreach (bool ranked in new[] { false, true })
                    {
                        var weights = new StageEffectWeights(ranked);
                        LobbyLighting.Apply(StageLighting.For(ranked), lights);
                        rig.Pose(1.3f, weights, float.PositiveInfinity);
                        var pose = StageCameraPose.For(ranked);
                        cam.transform.position = pose.Position;
                        cam.transform.LookAt(pose.LookAt);
                        Save(cam, w, h, $"output/lobby3d-{(ranked ? "ranked" : "casual")}-{tag}.png");
                    }
                }
                //  랭크 조명 쇼 도중(0.8초) 한 장
                var showWeights = new StageEffectWeights(true);
                LobbyLighting.Apply(StageLighting.For(true), lights, LightShowCurve.Dim(0.8f));
                rig.Pose(1.3f, showWeights, 0.8f);
                Save(cam, 1920, 1080, "output/lobby3d-ranked-show-16x9.png");
                Debug.Log("로비 3D 미리보기 7장 저장: output/lobby3d-*.png");
            }
            finally
            {
                if (root != null) LobbyTunnel.DestroyMaterials(root);
                RenderSettings.ambientLight = oldAmbient;
                RenderSettings.ambientMode = oldMode;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Save(Camera cam, int w, int h, string path)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;
            cam.targetTexture = null;
            DrawUiZones(tex, w, h);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        //  로비 UI는 1920×1080 기준, 패널 Expand — 화면 px = 기준 px × scale. 모서리에 붙은 구역은 그 모서리에서 잰다.
        private static void DrawUiZones(Texture2D tex, int w, int h)
        {
            float s = Mathf.Min(w / 1920f, h / 1080f);
            float pw = w / s, ph = h / s;   // 패널 크기(기준 단위)
            void Rect(float left, float top, float width, float height)
            {
                int x0 = Mathf.RoundToInt(left * s), x1 = Mathf.RoundToInt((left + width) * s);
                int y0 = Mathf.RoundToInt(top * s), y1 = Mathf.RoundToInt((top + height) * s);
                for (int t = 0; t < 3; t++)
                {
                    for (int x = x0; x <= x1; x++) { Px(tex, x, y0 + t, h); Px(tex, x, y1 - t, h); }
                    for (int y = y0; y <= y1; y++) { Px(tex, x0 + t, y, h); Px(tex, x1 - t, y, h); }
                }
            }
            Rect(80, 32, 560, 96);                                   // 이름표
            Rect(pw - 80 - 200, 32, 200, 64);                        // 코인
            Rect(80, 150, 156, 3 * 132 + 2 * 16);                    // 왼쪽 버튼 줄
            Rect(pw - 80 - 156, 150, 156, 3 * 132 + 2 * 16);         // 오른쪽 버튼 줄
            Rect(pw - 80 - 460, ph - 92 - 366, 460, 366);            // PLAY 묶음(전환 칩·PLAY·종목 칩)
            Rect(0, ph - 60, pw, 60);                                // LIVE 띠
            Rect(80, ph - 92 - 190, 640, 190);                        // 방송 선수 소개 자막(랭크)
            Rect(pw / 2f - 210, 44, 420, 52);                         // 중계 표시(랭크)
        }

        private static void Px(Texture2D tex, int x, int yTop, int h)
        {
            int y = h - 1 - yTop;   // 텍스처는 아래가 0
            if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) return;
            tex.SetPixel(x, y, new Color(0.91f, 0.2f, 0.18f));
        }
    }
}
