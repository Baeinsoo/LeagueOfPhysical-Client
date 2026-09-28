using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LOP.LookDevEditor
{
    /// <summary>
    /// 룩 개발 씬을 처음부터 다시 조립한다(몇 번을 돌려도 같은 결과). 값은 모두 여기 코드에 둔다 —
    /// 캡처를 보고 고칠 때도 이 파일을 고치고 다시 조립한다. 빌드 목록에는 넣지 않는다.
    /// </summary>
    public static class LookDevSceneBuilder
    {
        public const string ScenePath = "Assets/LookDev/LookDev.unity";
        private const string MaterialDir = "Assets/LookDev/Materials";
        private const string ProfilePath = "Assets/LookDev/LOPLook.asset";
        private const string ChibiRoot = "Assets/Art/PolyOne/Chibi Character";
        private const string ChibiModel = ChibiRoot + "/Model/SM_Chibi_Character.fbx";
        private const string ChibiTexture = ChibiRoot + "/Texture/Texture_ChibiCharacter_BaseColor.png";
        private const string ChibiAnimDir = ChibiRoot + "/Animation";
        private const string FaceAtlas = "Assets/Characters/Chibi/Face/FaceAtlas.png";
        private const float ChibiScale = 3f;   // 모델 키 0.49m → 약 1.47m(게임 캐릭터 1.5m)

        /// <summary>플레이 중이거나 저장 안 된 씬이 있으면 다시 조립하지 않는다 — 새 씬이 열린 씬을 말없이 버린다.</summary>
        public static bool CanRebuild(bool isPlaying, bool anySceneDirty) => isPlaying == false && anySceneDirty == false;

        [MenuItem("LOP/LookDev/Build Scene")]
        public static void Build()
        {
            bool dirty = false;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                dirty |= UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty;
            }
            if (CanRebuild(EditorApplication.isPlayingOrWillChangePlaymode, dirty) == false)
            {
                Debug.LogError("[LookDev] 플레이 중이거나 저장 안 된 씬이 있어 조립하지 않았다 — 저장하거나 플레이를 멈춘 뒤 다시.");
                return;
            }
            ConfigureFaceAtlasImport();
            regions = LookDevRegionBaker.Bake();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var mats = new Materials();
            var root = new GameObject("LookDev").transform;

            BuildLightingAndSky(mats);
            BuildGround(root, mats);
            BuildTarget(root, mats);
            BuildTrees(root, mats);
            BuildStands(root, mats);

            var hero = SpawnChibi(root, mats, new Vector3(-0.9f, 0f, 1.6f), 25f, ChibiExpression.Cheer, "Happy", "#FF4F5E");
            hero.name = "Hero_Cheer";
            var rival = SpawnChibi(root, mats, new Vector3(1.1f, 0f, 2.0f), 5f, ChibiExpression.Despair, "Sad", "#6C63FF");
            rival.name = "Rival_Despair";

            BuildCameras();
            BuildVolume();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[LookDev] 씬 조립 완료: " + ScenePath);
        }

        // ── 재질 ─────────────────────────────────────────────
        private sealed class Materials
        {
            public readonly Material Chibi, Grass, Dirt, Wood, Flag, Stand, Leaf, Hill, Face, Sky;
            public readonly Material[] Rings;

            public Materials()
            {
                Directory.CreateDirectory(MaterialDir);
                Chibi = Toon("Toon_Chibi", Color.white, outline: true, baseMap: ChibiTexture);
                Grass = Toon("Toon_Grass", Hex("#7CC46E"));
                Dirt = Toon("Toon_Dirt", Hex("#EBD9B4"));
                Wood = Toon("Toon_Wood", Hex("#8B5E34"));
                Flag = Toon("Toon_Flag", Hex("#FF4F5E"));
                Stand = Toon("Toon_Stand", Hex("#C9A77E"));
                Leaf = Toon("Toon_Leaf", Hex("#45B060"));
                Hill = Toon("Toon_Hill", Hex("#9CC0B8"));
                Rings = new[]
                {
                    Toon("Toon_TargetWhite", Hex("#FFFFFF")), Toon("Toon_TargetBlack", Hex("#2B2B2B")),
                    Toon("Toon_TargetBlue", Hex("#3B82F6")), Toon("Toon_TargetRed", Hex("#EF4444")),
                    Toon("Toon_TargetGold", Hex("#FACC15")),
                };
                var face = new Material(Load("Assets/Shaders/LOP/LOPToonDecal.shader"));
                face.SetTexture("_FaceMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FaceAtlas));
                Face = Save("Face", face);   // 값을 다 넣은 뒤에 에셋으로 만든다 — 뒤에 넣은 값은 파일에 안 남았다
                Sky = Save("Sky", new Material(Load("Assets/Shaders/LOP/LOPWatercolorSky.shader")));
            }

            private readonly System.Collections.Generic.Dictionary<string, Material> chibi = new System.Collections.Generic.Dictionary<string, Material>();

            //  PolyOne 베이스는 옷·피부 구분이 없는 민무늬 몸이다 — 룩 개발에선 몸 전체를 파티 색으로 칠한다.
            //  팀 저지(09-28 사용자 선택): 윗옷·소매 = 캐릭터 색, 바지 = 짙은 남보라, 신발 = 흰색, 피부는 사람마다.
            public Material ChibiIn(string hex, string skin)
            {
                string key = hex + skin;
                if (chibi.TryGetValue(key, out var m) == false)
                {
                    m = new Material(Load("Assets/Shaders/LOP/LOPToon.shader"));
                    m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ChibiTexture));
                    m.SetFloat("_UseRegions", 1f);
                    m.SetColor("_SkinColor", Hex(skin));
                    m.SetColor("_TopColor", Hex(hex));
                    m.SetColor("_SleeveColor", Hex(hex));
                    m.SetColor("_BottomColor", Hex("#3A3450"));
                    m.SetColor("_ShoeColor", Hex("#FFFFFF"));
                    m.SetColor("_HairColor", Hex(Hairs[(hex.GetHashCode() & 0x7fffffff) % Hairs.Length]));
                    m.SetColor("_OutlineColor", Hex(hex) * 0.4f);
                    m.SetShaderPassEnabled("SRPDefaultUnlit", true);
                    m = Save("Toon_Chibi_" + hex.TrimStart('#') + "_" + skin.TrimStart('#'), m);
                    chibi[key] = m;
                }
                return m;
            }

            private static Material Toon(string name, Color color, bool outline = false, string baseMap = null,
                                         Color? outlineColor = null)
            {
                var m = new Material(Load("Assets/Shaders/LOP/LOPToon.shader"));
                m.SetColor("_BaseColor", color);
                if (baseMap != null) { m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseMap)); }
                if (outlineColor.HasValue) { m.SetColor("_OutlineColor", outlineColor.Value); }
                m.SetShaderPassEnabled("SRPDefaultUnlit", outline);   // 외곽선은 캐릭터만
                return Save(name, m);
            }

            private static Shader Load(string path) => AssetDatabase.LoadAssetAtPath<Shader>(path);

            private static Material Save(string name, Material m)
            {
                string path = MaterialDir + "/" + name + ".mat";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(m, path);
                return m;
            }
        }

        // ── 빛·하늘·안개 ─────────────────────────────────────
        private static void BuildLightingAndSky(Materials mats)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("#FFF1D8");
            sun.intensity = 1.0f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, 150f, 0f);   // 사수 쪽(+Z) 위에서 과녁 쪽으로

            RenderSettings.skybox = mats.Sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#FFF4E0");
            RenderSettings.ambientEquatorColor = Hex("#C8D8F0");
            RenderSettings.ambientGroundColor = Hex("#7F92D8");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("#A9D6F0");
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 80f;
            DynamicGI.UpdateEnvironment();
        }

        // ── 땅·언덕 ──────────────────────────────────────────
        private static void BuildGround(Transform root, Materials mats)
        {
            Prim(root, "Ground", PrimitiveType.Cylinder, mats.Grass, new Vector3(0f, -0.3f, 0f), new Vector3(18f, 0.3f, 18f));
            Prim(root, "Dirt", PrimitiveType.Cylinder, mats.Dirt, new Vector3(0f, 0.01f, 1.2f), new Vector3(4.6f, 0.01f, 4.6f));
            var hills = new[] { new Vector4(-30f, -70f, 9f, 0f), new Vector4(10f, -80f, 12f, 0f), new Vector4(40f, -64f, 8f, 0f), new Vector4(-56f, -40f, 9f, 0f) };
            foreach (var h in hills)
            {
                Prim(root, "Hill", PrimitiveType.Sphere, mats.Hill, new Vector3(h.x, -h.z * 0.62f, h.y), Vector3.one * h.z * 2f);
            }
        }

        // ── 과녁 + 과녁 위 깃발 ──────────────────────────────
        private static void BuildTarget(Transform root, Materials mats)
        {
            var target = new GameObject("Target").transform;
            target.SetParent(root, false);
            target.position = new Vector3(0f, 0f, -4.2f);
            Prim(target, "Post", PrimitiveType.Cylinder, mats.Wood, new Vector3(0f, 0.7f, 0f), new Vector3(0.12f, 0.7f, 0.12f));
            for (int i = 0; i < mats.Rings.Length; i++)
            {
                float r = 0.78f * (1f - i / 5f);
                var ring = Prim(target, "Ring" + i, PrimitiveType.Cylinder, mats.Rings[i],
                                new Vector3(0f, 1.55f, 0.02f * i), new Vector3(r * 2f, 0.04f + i * 0.006f, r * 2f));
                ring.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            float top = 1.55f + 0.78f + 0.3f;
            Prim(target, "FlagPole", PrimitiveType.Cylinder, mats.Wood, new Vector3(0f, top + 0.25f, 0f), new Vector3(0.03f, 0.25f, 0.03f));
            var flag = Prim(target, "Flag", PrimitiveType.Quad, mats.Flag, new Vector3(0.23f, top + 0.42f, 0f), new Vector3(0.45f, 0.26f, 1f));
            flag.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 쿼드는 한 면만 보인다 — 사수 쪽으로
            Object.DestroyImmediate(flag.GetComponent<Collider>());
        }

        private static void BuildTrees(Transform root, Materials mats)
        {
            foreach (var p in new[] { new Vector2(-5.5f, -1.5f), new Vector2(-6.2f, 2.4f), new Vector2(5.8f, -3.8f), new Vector2(6.6f, 2.8f) })
            {
                Prim(root, "Trunk", PrimitiveType.Cylinder, mats.Wood, new Vector3(p.x, 0.45f, p.y), new Vector3(0.36f, 0.45f, 0.36f));
                Prim(root, "Leaf", PrimitiveType.Sphere, mats.Leaf, new Vector3(p.x, 1.5f, p.y), Vector3.one * 1.7f);
                Prim(root, "Leaf", PrimitiveType.Sphere, mats.Leaf, new Vector3(p.x + 0.45f, 1.25f, p.y + 0.2f), Vector3.one * 1.1f);
                Prim(root, "Leaf", PrimitiveType.Sphere, mats.Leaf, new Vector3(p.x - 0.4f, 1.3f, p.y - 0.1f), Vector3.one * 1.2f);
            }
        }

        private static readonly string[] Skins = { "#F7D2AE", "#FFE0BD", "#F3C9A0", "#E3B089", "#F7D2AE", "#C68642" };
        private static readonly string[] Hairs = { "#4A3222", "#2A2A30", "#8A5A3B", "#D9A441", "#4A3222" };
        private static Mesh regions;

        private static readonly string[] FanColors = { "#2EC4A6", "#F59E0B", "#EC4899", "#3B82F6", "#10B981", "#F97316", "#8B5CF6", "#FACC15" };

        private static void BuildStands(Transform root, Materials mats)
        {
            int k = 0;
            foreach (int side in new[] { -1, 1 })
            {
                Prim(root, "Stand", PrimitiveType.Cube, mats.Stand, new Vector3(side * 4.3f, 0.25f, 0.5f), new Vector3(1.1f, 0.5f, 6f));
                for (int i = 0; i < 6; i++)
                {
                    var expr = (i + k) % 3 == 0 ? ChibiExpression.Cheer : ChibiExpression.Normal;
                    var fan = SpawnChibi(root, mats, new Vector3(side * 4.3f, 0.5f, -1.9f + i * 0.95f), side > 0 ? -90f : 90f,
                                         expr, expr == ChibiExpression.Cheer ? "Happy" : "Idle", FanColors[(i + k * 3) % FanColors.Length], 0.5f);
                    fan.name = "Fan";
                }
                k++;
            }
        }

        // ── 치비 ─────────────────────────────────────────────
        private static GameObject SpawnChibi(Transform root, Materials mats, Vector3 position, float yaw,
                                             ChibiExpression expression, string clipName, string color, float sizeFactor = 1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChibiModel);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(root, false);
            var body = mats.ChibiIn(color, Skins[Mathf.Abs(position.GetHashCode()) % Skins.Length]);
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.sharedMaterial = body;
                if (smr.name == "SM_Chibi_Body" && regions != null) { smr.sharedMesh = regions; }   // 옷 영역을 구운 메시
            }
            //  편집 모드에선 애니메이션이 안 돈다 — 한 프레임을 샘플해 자세를 잡아 둔다. 샘플이 루트 위치를
            //  덮어쓰므로 자리·방향·크기는 그 뒤에 준다.
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ChibiAnimDir + "/" + clipName + ".anim");
            if (clip != null)
            {
                clip.SampleAnimation(go, clip.length * 0.3f);
            }
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * ChibiScale * sizeFactor;
            var face = go.AddComponent<ChibiFace>();
            face.faceMaterial = mats.Face;
            face.expression = expression;
            face.Build();
            return go;
        }

        // ── 카메라·후처리 ────────────────────────────────────
        private static void BuildCameras()
        {
            Camera("LookDevCam_Near", new Vector3(5.2f, 3.4f, 9.2f), new Vector3(0f, 1.0f, 1.5f), tag: "MainCamera");
            //  한 발 승부 평소 3인칭: 사수 뒤 3m, 눈높이(약 1.2m) + 0.8m, 과녁 쪽을 본다.
            Camera("LookDevCam_Game", new Vector3(-0.4f, 2.8f, 7.0f), new Vector3(0.2f, 0.9f, -2.0f));
            //  얼굴 정면 — 주인공(빨강) 앞 3m.
            Camera("LookDevCam_Face", new Vector3(0.37f, 1.45f, 4.35f), new Vector3(-0.9f, 1.1f, 1.6f));
        }

        private static void Camera(string name, Vector3 position, Vector3 lookAt, string tag = "Untagged")
        {
            var go = new GameObject(name) { tag = tag };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            go.transform.position = position;
            go.transform.LookAt(lookAt);
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
        }

        private static void BuildVolume()
        {
            AssetDatabase.DeleteAsset(ProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.25f);
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(8f);
            foreach (var c in profile.components)
            {
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);

            var volume = new GameObject("GlobalVolume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // ── 도구 ─────────────────────────────────────────────
        private static Transform Prim(Transform parent, string name, PrimitiveType type, Material mat, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static void ConfigureFaceAtlasImport()
        {
            var importer = AssetImporter.GetAtPath(FaceAtlas) as TextureImporter;
            if (importer == null) { return; }
            //  값이 이미 맞으면 다시 임포트하지 않는다 — 임포트 직후엔 텍스처를 못 읽어 재질이 빈다.
            if (importer.alphaIsTransparency && importer.wrapMode == TextureWrapMode.Clamp
                && importer.mipmapEnabled && importer.npotScale == TextureImporterNPOTScale.None)
            {
                return;
            }
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;   // 3×2 칸이 2의 거듭제곱으로 늘어나지 않게
            importer.SaveAndReimport();
            AssetDatabase.Refresh();
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
