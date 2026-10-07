using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LOP.EditorTools
{
    /// <summary>
    /// 광산 재질 — <see cref="FlappyMineLook"/>의 값을 읽어 <c>Assets/Art/Materials/Mine</c>에 만들거나
    /// (있으면) 값만 갱신한다. 같은 부품은 같은 재질 하나(GPU 인스턴싱 켬), 소품 외곽선은 0(캐릭터만 외곽선).
    ///
    /// <para><b>리매핑:</b> Task 3가 뽑은 FBX(<c>Assets/Art/Models/Mine/*.fbx</c>)의 재질 슬롯 이름(예: Plank,
    /// Strap, Wood …)을 여기서 만든 재질로 <see cref="ModelImporter.AddRemap"/>한다 — 부품을 씬에 놓기만 하면
    /// 바로 맞는 재질로 들어온다(Task 6에서 재질을 또 배선할 필요가 없다). 예외가 <b>Silhouette</b> 슬롯이다:
    /// 굴벽·협곡 실루엣 네 겹(<see cref="Ensure"/> 안 Silhouette_Cave1/2·Silhouette_Canyon1/2)이 메시 하나를
    /// 같이 쓰는데 임포터 리매핑은 슬롯 하나에 재질 하나만 받는다. 그래서 Silhouette 슬롯은 기본값
    /// (Silhouette_Cave1)로만 리매핑해 두고, 나머지 세 겹이 필요한 자리는 입히기(Task 6)가 그 인스턴스의
    /// <c>renderer.sharedMaterial</c>을 직접 갈아 끼운다.</para>
    /// </summary>
    public static class FlappyMineMaterials
    {
        private const string MaterialDir = "Assets/Art/Materials/Mine";
        private const string TextureDir = "Assets/Art/Textures/Mine";
        private const string ModelDir = "Assets/Art/Models/Mine";
        private const string GradientPath = TextureDir + "/sky_sunset_gradient.png";
        private const string LookAssetPath = "Assets/Art/Settings/FlappyMineLook.asset";

        [MenuItem("LOP/Debug/Flappy 광산 재질 갱신")]
        public static void EnsureFromMenu()
        {
            var look = AssetDatabase.LoadAssetAtPath<FlappyMineLook>(LookAssetPath);
            if (look == null)
            {
                Debug.LogError($"[광산 재질] {LookAssetPath}가 없다 — FlappyMineLook 에셋을 먼저 만들어라.");
                return;
            }
            Ensure(look);
        }

        public static void Ensure(FlappyMineLook look)
        {
            if (look == null)
            {
                Debug.LogError("[광산 재질] FlappyMineLook이 없다.");
                return;
            }
            Directory.CreateDirectory(MaterialDir);

            var wood = LoadTexture("wood_grain");
            var rock = LoadTexture("rock");
            Color shadow = look.shadowColor;

            //  쇠·광차 색은 Global Constraints에 없다 — 쇠테(밝다)·쇠띠(어둡다) 사이에서 고른 값이다.
            Color railColor = ToColor(0x6b, 0x5a, 0x4a);
            Color cartColor = ToColor(0x4a, 0x3a, 0x30);
            Color cartRimColor = ToColor(0x8a, 0x6a, 0x4a);
            Color oreColor = ToColor(0xc9, 0x79, 0x3f);
            Color rockDarkColor = Darken(look.rockColor, 0.7f);

            //  슬롯 이름(Task 3 보고) → 재질. FBX 리매핑은 이 이름으로 찾는다.
            var slots = new Dictionary<string, Material>
            {
                ["Plank"] = Toon("Plank", Grain(look, look.plankColor), wood, shadow),
                ["Wood"] = Toon("Wood", Grain(look, look.trestleColor), wood, shadow),
                ["Tie"] = Toon("Tie", Grain(look, look.trestleColor), wood, shadow),      // 침목 — 비계와 같은 나무
                ["Rock"] = Toon("Rock", Grain(look, look.rockColor), rock, shadow),
                ["RockDark"] = Toon("RockDark", Grain(look, rockDarkColor), rock, shadow),
                ["RockEdge"] = Toon("RockEdge", look.rockBandColor, null, shadow),          // 천장 밝은 띠 — 무늬 없음
                ["Strap"] = Toon("Strap", look.ironStrapColor, null, shadow),
                ["Band"] = Toon("Band", look.ironBandColor, null, shadow),
                ["Iron"] = Toon("Iron", look.ironStrapColor, null, shadow),                 // 볼트·바퀴틀 — 쇠띠와 같은 쇠
                ["Rail"] = Toon("Rail", railColor, null, shadow),
                ["Cart"] = Toon("Cart", cartColor, null, shadow),
                ["CartRim"] = Toon("CartRim", cartRimColor, null, shadow),
                ["Ore"] = Toon("Ore", oreColor, null, shadow),
                ["Glass"] = Toon("Glass", look.lanternColor, null, shadow),                 // 밝은 바탕 = 빛나는 느낌(발광 없음)
            };

            //  실루엣 네 겹 — 기본 슬롯 리매핑은 Silhouette_Cave1로(클래스 주석 참고).
            var silhouetteCave1 = UnlitOpaque("Silhouette_Cave1", look.caveSilhouette1Color);
            UnlitOpaque("Silhouette_Cave2", look.caveSilhouette2Color);
            UnlitOpaque("Silhouette_Canyon1", look.canyonSilhouette1Color);
            UnlitOpaque("Silhouette_Canyon2", look.canyonSilhouette2Color);
            slots["Silhouette"] = silhouetteCave1;

            //  안개 막(z=2.2, §4) — 반투명, 빛 안 받음.
            UnlitTransparent("Haze_Cave", WithAlpha(look.hazeCaveColor, look.hazeAlpha));
            UnlitTransparent("Haze_Outside", WithAlpha(look.hazeOutsideColor, look.hazeAlpha));

            //  랜턴 빛 원판 — 가산(Blend One One)을 직접 걸어 흉내 낸다. URP Unlit 인스펙터엔
            //  "가산" 프리셋이 없지만, 셰이더 Blend 식이 _SrcBlend/_DstBlend를 그대로 읽어 먹는다.
            UnlitAdditive("LanternGlow", WithAlpha(look.lanternColor, 0.5f));

            //  하늘 — 굴 안은 단색(굴 배경과 같은 값), 바깥은 그러데이션 텍스처.
            UnlitOpaque("Sky_Cave", look.hazeCaveColor);
            var sky = UnlitOpaque("Sky_Sunset", Color.white);
            if (sky != null) { sky.SetTexture("_BaseMap", EnsureSunsetGradient(look)); }

            RemapModelMaterials(slots);

            AssetDatabase.SaveAssets();
            Debug.Log($"[광산 재질] 슬롯 {slots.Count}개 + 안개·하늘·실루엣 재질 갱신 완료.");
        }

        // ---- 부품 슬롯 재질 ----

        private static Material Toon(string name, Color baseColor, Texture2D baseMap, Color shadowColor)
        {
            var m = LoadOrCreate(name, "LOP/Toon");
            if (m == null) { return null; }   // LoadOrCreate가 이미 에러를 찍었다 — 이 재질만 건너뛴다
            m.SetColor("_BaseColor", baseColor);
            m.SetTexture("_BaseMap", baseMap);
            m.SetColor("_ShadowColor", shadowColor);
            m.SetFloat("_OutlineWidth", 0f);
            m.SetShaderPassEnabled("SRPDefaultUnlit", false);   // 외곽선은 캐릭터만(ADR-0017)
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material UnlitOpaque(string name, Color color)
        {
            var m = LoadOrCreate(name, "Universal Render Pipeline/Unlit");
            if (m == null) { return null; }   // LoadOrCreate가 이미 에러를 찍었다 — 이 재질만 건너뛴다
            m.SetColor("_BaseColor", color);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material UnlitTransparent(string name, Color colorWithAlpha)
        {
            var m = UnlitOpaque(name, colorWithAlpha);
            if (m == null) { return null; }
            SetTransparent(m, additive: false);
            return m;
        }

        private static Material UnlitAdditive(string name, Color colorWithAlpha)
        {
            var m = UnlitOpaque(name, colorWithAlpha);
            if (m == null) { return null; }
            SetTransparent(m, additive: true);
            return m;
        }

        //  URP Unlit의 투명/가산 — 커스텀 셰이더 GUI 없이 인스펙터가 쓰는 그 프로퍼티를 직접 건다.
        private static void SetTransparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1f);   // 1 = Transparent
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            if (additive)
            {
                m.SetInt("_SrcBlend", (int)BlendMode.One);
                m.SetInt("_DstBlend", (int)BlendMode.One);
                m.DisableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            else
            {
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
        }

        private static Material LoadOrCreate(string name, string shaderName)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogError($"[광산 재질] 셰이더를 못 찾았다: {shaderName}");
                    return null;
                }
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        // ---- 하늘 그러데이션 텍스처 ----

        //  1×256 세로 그러데이션. V=0(아래)=skySunsetBottomColor, V=1(위)=skySunsetTopColor.
        private static Texture2D EnsureSunsetGradient(FlappyMineLook look)
        {
            const int height = 256;
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                tex.SetPixel(0, y, Color.Lerp(look.skySunsetBottomColor, look.skySunsetTopColor, t));
            }
            tex.Apply();

            Directory.CreateDirectory(TextureDir);
            File.WriteAllBytes(GradientPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(GradientPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(GradientPath) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(GradientPath);
        }

        // ---- FBX 리매핑 ----

        //  각 FBX가 임포트 때 만든 기본(내장) 재질의 이름 = 블렌더 슬롯 이름. 그 이름으로 찾아 우리 재질로 바꿔 건다.
        private static void RemapModelMaterials(Dictionary<string, Material> slots)
        {
            if (Directory.Exists(ModelDir) == false) { return; }

            foreach (string fbxPath in Directory.GetFiles(ModelDir, "*.fbx").Select(p => p.Replace('\\', '/')))
            {
                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null) { continue; }

                var current = importer.GetExternalObjectMap();
                bool changed = false;
                foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>())
                {
                    if (slots.TryGetValue(embedded.name, out Material target) == false || target == null) { continue; }
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name);
                    if (current.TryGetValue(id, out Object already) && already == target) { continue; }
                    importer.AddRemap(id, target);
                    changed = true;
                }
                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        // ---- 색 도우미 ----

        private static Color Grain(FlappyMineLook look, Color color) => Color.Lerp(Color.white, color, look.grainStrength);

        private static Color Darken(Color c, float factor) => new Color(c.r * factor, c.g * factor, c.b * factor, c.a);

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private static Color ToColor(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        private static Texture2D LoadTexture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/{name}.png");
    }
}
