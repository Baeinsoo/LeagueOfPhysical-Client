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
        private const string SkyBlendPath = TextureDir + "/sky_mouth_blend.png";
        private const string HazeBlendPath = TextureDir + "/haze_mouth_blend.png";
        private const string FarHazeBlendPath = TextureDir + "/haze_far_mouth_blend.png";
        private const string GlowPath = TextureDir + "/lantern_glow.png";
        internal const string RockPaintPath = TextureDir + "/rock_paint.png";
        /// <summary>칠한 바위 텍스처 한 변(화소). 2의 거듭제곱 — 반복(Repeat)·밉맵.</summary>
        internal const int RockPaintSize = 1024;
        private const int RockPaintSeed = 7;
        internal const string LookAssetPath = "Assets/Art/Settings/FlappyMineLook.asset";

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
            //  바위 덩어리(Strata 재질 = 흰색 × rock_paint)의 평균 색 — 장식 색의 기준.
            Color rockBase = CalmRockBase(look);

            //  펼치기 1차 새 슬롯 9개(Task 3 보고 표) — Global Constraints에 없는 색은 그 표에서 고정으로 가져온다.
            //  signColor(간판 판자)·boltGlow·padGlow만 FlappyMineLook 조절 값이다.
            Color timberColor = ToColor(0x9a, 0x64, 0x38);     // 갱목 — Wood보다 어둡고 붉은 갈색
            Color signInlayColor = ToColor(0x3a, 0x26, 0x18);  // 간판 그림 홈 — 탄 갈색
            Color ropeColor = ToColor(0x5a, 0x46, 0x36);       // 줄(랜턴 줄·케이지 줄)
            Color boltRimColor = ToColor(0x2a, 0x1a, 0x12);    // ⚡ 테두리 — 거의 검은 갈색
            Color padFrameColor = ToColor(0x4a, 0x3a, 0x30);   // 발판 어두운 틀
            //  빛 재질의 밑감(Task 3 표 "모양" 열 — ⚡ 노란 면·발판 윗면 색) × look.boltGlow/padGlow. LOP/Toon은
            //  발광(_Emission) 프로퍼티가 없어서(shader property 목록으로 직접 확인, ADR-0017 — 소품 외곽선도
            //  끄는 셰이더) "모양"과 "빛"을 따로 못 둔다 — 이 둘만 LanternGlow와 같은 URP Unlit 하나로 모양+빛을
            //  합쳐 밑감에 세기를 곱한 값을 그대로 _BaseColor로 쓴다(조명 각도와 상관없이 일정하게 빛난다).
            //  1 미만이면 블룸 문턱 밑이라(은은하게) 과하게 빛나지 않는다 — padGlow(0.8)는 일부러 거기 둔다.
            Color boltGlowBase = ToColor(0xff, 0xd8, 0x4a);
            Color padGlowBase = ToColor(0xff, 0xcf, 0x5a);

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

                //  펼치기 1차 새 슬롯 9개.
                ["Timber"] = Toon("Timber", Grain(look, timberColor), wood, shadow),
                ["SignWood"] = Toon("SignWood", Grain(look, look.signColor), wood, shadow),
                ["SignInlay"] = Toon("SignInlay", signInlayColor, null, shadow),
                ["Rope"] = Toon("Rope", ropeColor, null, shadow),
                //  10-11 Fix 1c(사용자 v3 "A"): 민짜 띠(strata_calm, v2·v3 "휑하다") 대신 손으로 칠한 듯한 바위 면(rock_paint.png, MineRockPaint) —
                //  큰 덩이 그림자·굵은 금·지층. 평균 색 = CalmRockBase(장식 색 기준 그대로). 틴트는 흰색(색은 텍스처에).
                //  UV는 굽기가 월드 좌표 / rockPaintTile로 다시 쓴다(FlappyMineDressing.WorldUv) — 판이 커도 늘어나지 않고 조각끼리 이어진다.
                ["Strata"] = Toon("Strata", Color.white, EnsureRockPaint(look), shadow),
                ["BoltRim"] = Toon("BoltRim", boltRimColor, null, shadow),
                ["PadFrame"] = Toon("PadFrame", padFrameColor, null, shadow),
                ["BoltGlow"] = UnlitOpaque("BoltGlow", ScaleRgb(boltGlowBase, look.boltGlow)),
                ["PadGlow"] = UnlitOpaque("PadGlow", ScaleRgb(padGlowBase, look.padGlow)),

                //  10-10 Fix 1b 바위 면 장식 — 전부 바위 평균 색(rock_paint 평균 = CalmRockBase)이나 갱목 색에서 조금만 어긋나게(밝은 것 = 닿는 것이라
                //  밝은 테보다 늘 어둡다). 지층 렌즈 슬롯 기본은 어두운 쪽 — 밝은 쪽은 굽기가 층(Layer 1)마다 갈아 끼운다.
                ["StrataPatch"] = Toon("StrataPatchDark", ScaleRgb(rockBase, 1f - look.decorContrast), null, shadow),
                ["Seam"] = Toon("Seam", ScaleRgb(rockBase, look.decorSeamShade), null, shadow),
                ["TimberDim"] = Toon("TimberDim", ScaleRgb(Grain(look, timberColor), look.decorTimberShade), wood, shadow),
                ["OreDim"] = Toon("OreDim", ScaleRgb(oreColor, look.decorOreShade), null, shadow),
                ["RailDim"] = Toon("RailDim", ScaleRgb(railColor, 0.85f), null, shadow),
            };
            Toon("StrataPatchLight", ScaleRgb(rockBase, 1f + look.decorContrast), null, shadow);

            //  실루엣 네 겹 — 기본 슬롯 리매핑은 Silhouette_Cave1로(클래스 주석 참고).
            var silhouetteCave1 = UnlitOpaque("Silhouette_Cave1", look.caveSilhouette1Color);
            UnlitOpaque("Silhouette_Cave2", look.caveSilhouette2Color);
            UnlitOpaque("Silhouette_Canyon1", look.canyonSilhouette1Color);
            UnlitOpaque("Silhouette_Canyon2", look.canyonSilhouette2Color);
            slots["Silhouette"] = silhouetteCave1;

            //  안개 막(z=2.2, §4) — 반투명, 빛 안 받음. 바깥/굴 사이(굴 입구)는 가로 그러데이션 텍스처로 잇는다(10-08 이음매).
            //  먼 안개 막(z=12, 먼 비계·실루엣만 한 겹 더)은 같은 색에 구역별 진하기(farHaze*Alpha) — Haze_Far_*.
            //  입구 띠는 색과 진하기를 둘 다 텍스처가 담는다(바깥 → 굴로 알파도 섞인다). 재질 _BaseColor는 흰색.
            var layers = new[]
            {
                ("Haze_", look.hazeAlpha, look.hazeCaveAlpha, HazeBlendPath),
                ("Haze_Far_", look.farHazeOutsideAlpha, look.farHazeCaveAlpha, FarHazeBlendPath),
            };
            foreach (var (prefix, outsideAlpha, caveAlpha, blendPath) in layers)
            {
                UnlitTransparent(prefix + "Cave", WithAlpha(look.hazeCaveColor, caveAlpha));
                UnlitTransparent(prefix + "Outside", WithAlpha(look.hazeOutsideColor, outsideAlpha));
                var mouth = UnlitTransparent(prefix + "Mouth", Color.white);
                if (mouth != null) { mouth.SetTexture("_BaseMap", EnsureHazeBlend(look, blendPath, outsideAlpha, caveAlpha)); }
            }

            //  랜턴 빛 — 가산(Blend One One)을 직접 걸어 흉내 낸다. URP Unlit 인스펙터엔
            //  "가산" 프리셋이 없지만, 셰이더 Blend 식이 _SrcBlend/_DstBlend를 그대로 읽어 먹는다.
            //  가운데가 밝고 가장자리가 0인 방사형 텍스처를 곱해 테두리 없이 옅어진다(10-08 — 균일 원판은 스티커처럼 보였다).
            var glow = UnlitAdditive("LanternGlow", WithAlpha(look.lanternColor, 0.5f));
            if (glow != null) { glow.SetTexture("_BaseMap", EnsureGlowTexture()); }

            //  하늘 — 굴 안은 단색(굴 배경과 같은 값), 바깥은 그러데이션 텍스처, 그 사이는 둘을 가로로 섞은 텍스처.
            UnlitOpaque("Sky_Cave", look.hazeCaveColor);
            var sky = UnlitOpaque("Sky_Sunset", Color.white);
            if (sky != null) { sky.SetTexture("_BaseMap", EnsureSunsetGradient(look)); }
            var skyMouth = UnlitOpaque("Sky_Mouth", Color.white);
            if (skyMouth != null) { skyMouth.SetTexture("_BaseMap", EnsureSkyBlend(look)); }

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
                //  막 만든 에셋을 한 번 왕복(저장 → 동기 재가져오기)시키고 다시 읽어 돌려준다 — 그러지 않고
                //  바로 호출자가 SetColor 등으로 고치면(10-10 겪음), 그사이 끼는 비동기 임포트가 그 수정을
                //  디스크 기본값으로 덮어써 버릴 때가 있다(재질마다 23개를 잇따라 만드는 Ensure에서만 보였다).
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                m = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            return m;
        }

        // ---- 만든 텍스처(하늘·안개 그러데이션, 랜턴 빛) ----

        //  1×256 세로 그러데이션. V=0(아래)=skySunsetBottomColor, V=1(위)=skySunsetTopColor.
        private static Texture2D EnsureSunsetGradient(FlappyMineLook look)
        {
            return WriteTexture(GradientPath, 1, 256, (u, v) => SunsetAt(look, v));
        }

        //  64×256: u = 0(바깥) → 1(굴)로 노을 그러데이션을 굴 배경색으로 섞는다. v는 노을과 같다(같은 높이의 띠에 붙는다).
        private static Texture2D EnsureSkyBlend(FlappyMineLook look)
        {
            return WriteTexture(SkyBlendPath, 64, 256, (u, v) => Color.Lerp(SunsetAt(look, v), look.hazeCaveColor, Mathf.SmoothStep(0f, 1f, u)));
        }

        //  64×1: u = 0(바깥 안개, outsideAlpha) → 1(굴 안개, caveAlpha). 색·알파를 같이 섞는다.
        private static Texture2D EnsureHazeBlend(FlappyMineLook look, string path, float outsideAlpha, float caveAlpha)
        {
            Color outside = WithAlpha(look.hazeOutsideColor, outsideAlpha), cave = WithAlpha(look.hazeCaveColor, caveAlpha);
            return WriteTexture(path, 64, 1, (u, v) => Color.Lerp(outside, cave, Mathf.SmoothStep(0f, 1f, u)));
        }

        //  64×64 방사형: 가운데 0.9 → 가장자리(반지름 0.5) 0, (1 − r)^1.5로 부드럽게. 가산이라 RGB가 곧 밝기다(알파도 같은 값).
        //  선형(sRGB 끔)으로 임포트한다 — sRGB로 읽으면 값이 2.2제곱으로 눌려 빛이 랜턴 몸통 뒤에만 몰린다(10-08 캡처).
        private static Texture2D EnsureGlowTexture()
        {
            return WriteTexture(GlowPath, 64, 64, (u, v) =>
            {
                float r = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) / 0.5f);
                float k = 0.9f * Mathf.Pow(1f - r, 1.5f);
                return new Color(k, k, k, k);
            }, linear: true);
        }

        //  칠한 바위(MineRockPaint) RockPaintSize² — 평균 색 CalmRockBase. 반복·밉맵(멀리서도 반짝이지 않게), sRGB.
        private static Texture2D EnsureRockPaint(FlappyMineLook look)
        {
            Color b = CalmRockBase(look);
            var settings = RockPaintSettings(look);
            byte[] rgba = MineRockPaint.Rgba(settings, b.r, b.g, b.b);
            var tex = new Texture2D(settings.Size, settings.Size, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgba);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            Directory.CreateDirectory(TextureDir);
            bool same = File.Exists(RockPaintPath) && File.ReadAllBytes(RockPaintPath).SequenceEqual(png);
            if (same == false)
            {
                File.WriteAllBytes(RockPaintPath, png);
                AssetDatabase.ImportAsset(RockPaintPath, ImportAssetOptions.ForceUpdate);
            }
            if (AssetImporter.GetAtPath(RockPaintPath) is TextureImporter importer
                && (importer.wrapMode != TextureWrapMode.Repeat || importer.mipmapEnabled == false || importer.sRGBTexture == false
                    || importer.anisoLevel != 4 || importer.maxTextureSize < settings.Size))
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.sRGBTexture = true;
                importer.anisoLevel = 4;
                importer.maxTextureSize = Mathf.Max(importer.maxTextureSize, settings.Size);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(RockPaintPath);
        }

        internal static MineRockPaint.Settings RockPaintSettings(FlappyMineLook look) =>
            new MineRockPaint.Settings(RockPaintSize, look.rockPaintBlocks, look.rockPaintBlocks, look.rockPaintContrast, look.rockPaintStrata,
                                       look.rockPaintStrands, look.rockPaintCracks, RockPaintSeed);

        /// <summary>바위 덩어리 텍스처(rock_paint)의 평균 색 = strataColors 평균 × strataBrightness(명암은 평균 둘레로만 흔든다).</summary>
        internal static Color CalmRockBase(FlappyMineLook look)
        {
            Color[] bands = look.strataColors != null && look.strataColors.Length > 0 ? look.strataColors : DefaultStrataColors;
            return ScaleRgb(AverageRgb(bands), look.strataBrightness);
        }

        private static Color SunsetAt(FlappyMineLook look, float v) => Color.Lerp(look.skySunsetBottomColor, look.skySunsetTopColor, v);

        //  픽셀 중심의 (u, v)로 칠한 PNG를 쓴다 — 내용이 같으면 파일을 다시 쓰지도, 다시 임포트하지도 않는다(굽기마다 부른다).
        //  가장자리 픽셀이 정확히 0·1이 되도록 u = x / (w − 1)(폭 1이면 0)로 잰다. 클램프, 밉맵 없음. linear면 sRGB 변환 없이 값 그대로 쓴다.
        private static Texture2D WriteTexture(string path, int width, int height, System.Func<float, float, Color> colorAt, bool linear = false)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = width > 1 ? x / (float)(width - 1) : 0f, v = height > 1 ? y / (float)(height - 1) : 0f;
                    tex.SetPixel(x, y, colorAt(u, v));
                }
            }
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            Directory.CreateDirectory(TextureDir);
            bool same = File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(png);
            if (same == false)
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            if (AssetImporter.GetAtPath(path) is TextureImporter importer
                && (importer.wrapMode != TextureWrapMode.Clamp || importer.mipmapEnabled || importer.sRGBTexture == linear))
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = !linear;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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

        //  빛 재질 밑감에 세기를 곱한다 — 1보다 크면(밑감 밝은 채널이 1에 가까워서) HDR 값이 되어 블룸 문턱을 살짝 넘는다.
        private static Color ScaleRgb(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        //  strata.png를 구운 기본 네 색(STRATA_COLORS, mine_parts.py) — strataColors가 비었을 때 대신 쓰는 값.
        private static readonly Color[] DefaultStrataColors =
        {
            new Color32(0x7a, 0x56, 0x46, 255), new Color32(0x6a, 0x48, 0x38, 255),
            new Color32(0x8a, 0x64, 0x4e, 255), new Color32(0x5e, 0x40, 0x34, 255),
        };

        private static Color AverageRgb(Color[] colors)
        {
            if (colors == null || colors.Length == 0) { return Color.white; }
            float r = 0f, g = 0f, b = 0f;
            foreach (Color c in colors) { r += c.r; g += c.g; b += c.b; }
            int n = colors.Length;
            return new Color(r / n, g / n, b / n);
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private static Color ToColor(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        private static Texture2D LoadTexture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/{name}.png");
    }
}
