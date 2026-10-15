using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LOP.EditorTools
{
    /// <summary>
    /// 결과 단계 공용 시상대를 미리보기 씬에 세우고 카메라 렌더를 PNG로 저장한다(화면을 찍지 않는다).
    /// 결과판·아래 줄·확인 자리를 빨간 테두리로 그려 선수·단이 가리는지 본다. 월드 공간 글자(현수막·단 번호)는 미리보기 씬에서 안 그려진다.
    /// </summary>
    public static class ResultPodiumPreview
    {
        [MenuItem("LOP/UI/결과 시상대 미리보기 PNG 저장")]
        public static void SavePngs()
        {
            int saved = 0;
            foreach (int count in new[] { 2, 4, 8 })
            {
                foreach (bool ranked in new[] { false, true })
                {
                    SaveOne(count, ranked, $"output/result-podium-{count}-{(ranked ? "ranked" : "casual")}.png");
                    saved++;
                }
            }
            Debug.Log($"결과 시상대 미리보기 {saved}장 저장: output/result-podium-*.png");
        }

        private static void SaveOne(int count, bool ranked, string path)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("PreviewPodium");
                SceneManager.MoveGameObjectToScene(root, scene);

                var sun = new GameObject("Sun").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(sun.gameObject, scene);
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                var placements = new int[count];
                for (int i = 0; i < count; i++) placements[i] = i + 1;
                var stage = new CommonPodiumSet().Build(root.transform, new PodiumContext(ranked, placements, false));

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPlayer.ChibiKey);
                var face = AssetDatabase.LoadAssetAtPath<Material>(LobbyPlayer.FaceKey);
                var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/Chibi/Anim/Idle.anim");
                if (prefab != null)
                {
                    for (int i = 0; i < stage.Slots.Count; i++)
                    {
                        var slot = stage.Slots[i];
                        var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                        body.transform.SetParent(root.transform, false);
                        body.transform.position = slot.Position;
                        body.transform.rotation = Quaternion.Euler(0f, slot.Yaw, 0f);
                        body.transform.localScale = prefab.transform.localScale * slot.Scale;   // 치비 뿌리는 크기 3
                        if (idle != null) idle.SampleAnimation(body, 0f);
                        if (ChibiDresser.IsChibi(body)) ChibiDresser.Dress($"preview-{i}", body, face);
                    }
                }

                var camGo = new GameObject("PreviewCamera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                LobbyStage.ConfigureCamera(cam);
                cam.transform.position = stage.Camera.Position;
                cam.transform.LookAt(stage.Camera.LookAt);
                Save(cam, 1920, 1080, path, count);
            }
            finally
            {
                if (root != null) LobbyTunnel.DestroyMaterials(root);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Save(Camera cam, int w, int h, string path, int rows)
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
            DrawUiZones(tex, w, h, rows);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        //  결과판 UXML/USS 값(1920×1080 기준): 판 left 64·top 48·width 920, 머리띠 약 60 + 줄 60씩, 안쪽 26.
        private static void DrawUiZones(Texture2D tex, int w, int h, int rows)
        {
            void Rect(int left, int top, int width, int height)
            {
                for (int t = 0; t < 3; t++)
                {
                    for (int x = left; x <= left + width; x++) { Px(tex, x, top + t, h); Px(tex, x, top + height - t, h); }
                    for (int y = top; y <= top + height; y++) { Px(tex, left + t, y, h); Px(tex, left + width - t, y, h); }
                }
            }
            Rect(64, 48, 920, 26 * 2 + 60 + 60 * rows);   // 결과판
            Rect(64, h - 70 - (92 + 14 + 68 + 14), 760, 92 + 14 + 68 + 14);   // 아래 줄(랭크 줄 크게 + 보상 줄)
            Rect(w - 70 - 280, h - 70 - 110, 280, 110);   // 확인
        }

        private static void Px(Texture2D tex, int x, int yTop, int h)
        {
            int y = h - 1 - yTop;   // 텍스처는 아래가 0
            if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) return;
            tex.SetPixel(x, y, new Color(0.91f, 0.2f, 0.18f));
        }
    }
}
