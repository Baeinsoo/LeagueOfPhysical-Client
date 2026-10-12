using System.Collections.Generic;
using System.IO;
using System.Linq;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// <see cref="FlappyMineMaterials.Ensure"/>는 실제 <c>Assets/Art/{Materials,Textures,Models}/Mine</c>에 재질·텍스처·
    /// 임포트 설정을 쓴다(피스처가 아니라 이 슬라이스가 커밋하는 실물 에셋이라 임시 폴더로 옮기지 않았다). 두 번 불러도
    /// 재질 수가 늘지 않고, 값만 최신 <see cref="FlappyMineLook"/>으로 갱신되는지를 본다.
    ///
    /// <para><b>실물을 지킨다(10-08 최종 리뷰 I3).</b> 클래스 기본값으로 Ensure를 부르면 사용자가
    /// <c>FlappyMineLook.asset</c>으로 조절해 구운 값이 테스트를 돌릴 때마다 기본값으로 되돌아갔다. 그래서
    /// ① look은 실제 asset의 복사본(<see cref="Object.Instantiate(Object)"/>)을 쓰고, ② 시작 전에 Ensure가 쓰는 세 폴더의
    /// 파일 바이트를 떠 두었다가 끝나면 바뀐 파일만 되돌리고 새로 생긴 파일은 지운다 — 무슨 값을 넣어 보든
    /// Art 서브모듈은 테스트 전 그대로다.</para>
    /// </summary>
    public class FlappyMineMaterialsTests
    {
        private const string MaterialDir = "Assets/Art/Materials/Mine";
        private static readonly string[] WrittenDirs = { MaterialDir, "Assets/Art/Textures/Mine", "Assets/Art/Models/Mine" };

        private Dictionary<string, byte[]> snapshot;

        [OneTimeSetUp]
        public void SnapshotArt()
        {
            snapshot = new Dictionary<string, byte[]>();
            foreach (string dir in WrittenDirs)
            {
                if (Directory.Exists(dir) == false) { continue; }
                foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    snapshot[f.Replace('\\', '/')] = File.ReadAllBytes(f);
                }
            }
        }

        [OneTimeTearDown]
        public void RestoreArt()
        {
            var reimport = new HashSet<string>();
            var created = new List<string>();
            foreach (string dir in WrittenDirs)
            {
                if (Directory.Exists(dir) == false) { continue; }
                foreach (string raw in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    string f = raw.Replace('\\', '/');
                    if (snapshot.ContainsKey(f) == false) { created.Add(f); }
                }
            }
            foreach (var pair in snapshot)
            {
                if (File.Exists(pair.Key) && SameBytes(File.ReadAllBytes(pair.Key), pair.Value)) { continue; }
                File.WriteAllBytes(pair.Key, pair.Value);
                reimport.Add(pair.Key.EndsWith(".meta") ? pair.Key.Substring(0, pair.Key.Length - 5) : pair.Key);
            }
            foreach (string f in created)
            {
                if (f.EndsWith(".meta")) { continue; }   // 에셋과 같이 지운다
                AssetDatabase.DeleteAsset(f);
            }
            foreach (string f in created)
            {
                if (File.Exists(f)) { File.Delete(f); }   // 짝 없이 남은 .meta
            }
            //  디스크를 되돌렸으니 메모리의 재질·텍스처·임포터도 디스크에서 다시 읽게 한다.
            foreach (string path in reimport)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) { return false; }
            for (int i = 0; i < a.Length; i++) { if (a[i] != b[i]) { return false; } }
            return true;
        }

        //  실제 asset의 복사본 — 클래스 기본값이 아니라 지금 조절해 둔 값으로 Ensure를 부른다(asset 자체는 안 건드린다).
        private static FlappyMineLook Look()
        {
            var real = AssetDatabase.LoadAssetAtPath<FlappyMineLook>(FlappyMineMaterials.LookAssetPath);
            return real != null ? Object.Instantiate(real) : ScriptableObject.CreateInstance<FlappyMineLook>();
        }

        [Test]
        public void 두_번_불러도_재질_수가_늘지_않고_값만_갱신된다()
        {
            var look = Look();
            try
            {
                FlappyMineMaterials.Ensure(look);
                int countAfterFirst = Directory.GetFiles(MaterialDir, "*.mat").Length;

                look.plankColor = Color.magenta;   // 값을 바꿔 "두 번째 호출이 갱신한다"까지 같이 본다
                FlappyMineMaterials.Ensure(look);
                int countAfterSecond = Directory.GetFiles(MaterialDir, "*.mat").Length;

                Assert.AreEqual(countAfterFirst, countAfterSecond, "두 번째 Ensure가 재질을 새로 만들었다(중복)");

                var plank = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Plank.mat");
                Assert.AreEqual((Color)Color.magenta, plank.GetColor("_BaseColor"));
            }
            finally
            {
                Object.DestroyImmediate(look);   // Plank.mat은 RestoreArt가 디스크 바이트로 되돌린다
            }
        }

        [Test]
        public void 나무_슬롯은_툰_셰이더에_나무결_텍스처를_쓴다()
        {
            var look = Look();
            try
            {
                FlappyMineMaterials.Ensure(look);
                var wood = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Wood.mat");
                Assert.AreEqual("LOP/Toon", wood.shader.name);
                Assert.AreEqual("wood_grain", wood.GetTexture("_BaseMap").name);
                Assert.IsFalse(wood.GetShaderPassEnabled("SRPDefaultUnlit"));   // 소품 외곽선 0
            }
            finally
            {
                Object.DestroyImmediate(look);
            }
        }

        [Test]
        public void 바위_슬롯은_바위_텍스처_쇠_슬롯은_텍스처가_없다()
        {
            var look = Look();
            try
            {
                FlappyMineMaterials.Ensure(look);

                var rock = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Rock.mat");
                Assert.AreEqual("rock", rock.GetTexture("_BaseMap").name);

                var strap = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Strap.mat");
                Assert.AreEqual("LOP/Toon", strap.shader.name);
                Assert.IsNull(strap.GetTexture("_BaseMap"));
            }
            finally
            {
                Object.DestroyImmediate(look);
            }
        }

        //  재질이 가리키는 텍스처의 PNG를 읽는다(임포트된 텍스처는 CPU에서 못 읽는다).
        private static Texture2D ReadPng(Material m)
        {
            string path = AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap"));
            Assert.IsNotEmpty(path, $"{m.name}에 텍스처가 없다");
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }

        [Test]
        public void 랜턴_빛은_가운데가_밝고_가장자리가_0인_방사형_텍스처를_쓴다()
        {
            var look = Look();
            try
            {
                FlappyMineMaterials.Ensure(look);
                var tex = ReadPng(AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/LanternGlow.mat"));
                try
                {
                    Assert.Greater(tex.GetPixelBilinear(0.5f, 0.5f).r, 0.5f, "가운데가 어둡다");
                    //  테두리 없이 옅어진다 — 원 둘레(반지름 0.5)와 모서리는 0.
                    Assert.Less(tex.GetPixelBilinear(1f, 0.5f).r, 0.02f, "오른쪽 끝이 0이 아니다(딱딱한 테두리)");
                    Assert.Less(tex.GetPixelBilinear(0.5f, 0f).r, 0.02f, "아래 끝이 0이 아니다");
                    Assert.Less(tex.GetPixelBilinear(0f, 0f).r, 0.02f, "모서리가 0이 아니다(사각형이 보인다)");
                    //  가운데와 끝 사이는 그 사이 값(계단이 아니라 옅어진다).
                    float mid = tex.GetPixelBilinear(0.75f, 0.5f).r;
                    Assert.That(mid, Is.InRange(0.05f, 0.45f), "중간이 가운데·끝 사이 값이 아니다");
                }
                finally { Object.DestroyImmediate(tex); }
            }
            finally
            {
                Object.DestroyImmediate(look);
            }
        }

        [Test]
        public void 먼_안개_막은_구역마다_진하기가_다르고_입구_띠가_진하기도_섞는다()
        {
            var look = Look();
            try
            {
                FlappyMineMaterials.Ensure(look);
                Assert.AreEqual(look.farHazeOutsideAlpha, AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Haze_Far_Outside.mat").GetColor("_BaseColor").a, 0.01f);
                Assert.AreEqual(look.farHazeCaveAlpha, AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Haze_Far_Cave.mat").GetColor("_BaseColor").a, 0.01f);
                var tex = ReadPng(AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Haze_Far_Mouth.mat"));
                try
                {
                    Assert.AreEqual(look.farHazeOutsideAlpha, tex.GetPixel(0, 0).a, 0.01f, "왼끝 진하기가 바깥 먼 안개와 다르다");
                    Assert.AreEqual(look.farHazeCaveAlpha, tex.GetPixel(tex.width - 1, 0).a, 0.01f, "오른끝 진하기가 굴 먼 안개와 다르다");
                }
                finally { Object.DestroyImmediate(tex); }
            }
            finally
            {
                Object.DestroyImmediate(look);
            }
        }

        //  펼치기 1차(Task 4) — 새 슬롯 9개.

        [Test]
        public void 새_나무_슬롯_Timber_SignWood는_나무결_텍스처를_쓴다()
        {
            var look = Look();
            try
            {
                FlappyMineMaterials.Ensure(look);
                foreach (string name in new[] { "Timber", "SignWood" })
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{name}.mat");
                    Assert.IsNotNull(m, $"{name}.mat이 없다");
                    Assert.AreEqual("LOP/Toon", m.shader.name, $"{name}이 LOP/Toon이 아니다");
                    Assert.AreEqual("wood_grain", m.GetTexture("_BaseMap").name, $"{name}에 나무결 텍스처가 없다");
                    Assert.IsFalse(m.GetShaderPassEnabled("SRPDefaultUnlit"), $"{name}에 외곽선이 켜져 있다");
                }
            }
            finally
            {
                Object.DestroyImmediate(look);
            }
        }

        [Test]
        public void Strata_슬롯은_칠한_바위_텍스처를_반복으로_쓰고_평균은_바위색_밝은_테보다_어둡다()
        {
            //  10-11 Fix 1c(사용자 v3 "A"): 민짜 띠(strata_calm) 대신 칠한 바위(rock_paint) — 월드 UV로 반복하므로 Repeat·밉맵.
            var look = Look();
            try
            {
                FlappyMineMaterials.Ensure(look);
                var strata = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Strata.mat");
                Assert.IsNotNull(strata, "Strata.mat이 없다");
                Assert.AreEqual("rock_paint", strata.GetTexture("_BaseMap").name);
                var importer = (TextureImporter)AssetImporter.GetAtPath(FlappyMineMaterials.RockPaintPath);
                Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapMode, "월드 UV로 반복하는데 Repeat가 아니다");
                Assert.IsTrue(importer.mipmapEnabled, "멀리서 반짝이지 않게 밉맵이 있어야 한다");
                Assert.GreaterOrEqual(importer.maxTextureSize, FlappyMineMaterials.RockPaintSize);

                var png = new Texture2D(2, 2);
                png.LoadImage(File.ReadAllBytes(FlappyMineMaterials.RockPaintPath));
                Assert.AreEqual(FlappyMineMaterials.RockPaintSize, png.width);
                Color[] px = png.GetPixels();
                Object.DestroyImmediate(png);
                float mean = px.Average(p => p.grayscale), hi = px.Max(p => p.grayscale);
                float std = Mathf.Sqrt(px.Average(p => (p.grayscale - mean) * (p.grayscale - mean)));
                float rock = FlappyMineMaterials.CalmRockBase(look).grayscale;
                float rim = look.rockBandColor.grayscale;
                Assert.AreEqual(rock, mean, 0.02f, "텍스처 평균이 바위 평균 색(장식 색 기준)에서 벗어난다");
                Assert.Less(hi, rim * 0.75f, "바위 면에 밝은 테만큼 밝은 화소가 있다(밝은 것 = 닿는 것)");
                Assert.Greater(std, 0.025f, "바위 면이 민짜다(v2·v3 '휑하다')");
                var color = strata.GetColor("_BaseColor");
                Assert.AreEqual(1f, color.r, 0.01f);
                Assert.AreEqual(1f, color.g, 0.01f);
                Assert.AreEqual(1f, color.b, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(look);
            }
        }

        [Test]
        public void 빛_재질_BoltGlow_PadGlow는_세기_배수를_따르고_과하게_빛나지_않는다()
        {
            var look = Look();
            try
            {
                FlappyMineMaterials.Ensure(look);

                var boltGlow = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/BoltGlow.mat");
                var padGlow = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/PadGlow.mat");
                Assert.AreEqual("Universal Render Pipeline/Unlit", boltGlow.shader.name);
                Assert.AreEqual("Universal Render Pipeline/Unlit", padGlow.shader.name);

                //  밑감 = Task 3 표 "모양" 열(⚡ 노란 면 #ffd84a, 발판 윗면 #ffcf5a) × 세기 — 둘을 한 Unlit로 합친다.
                AssertColor(boltGlow.GetColor("_BaseColor"), 0xff, 0xd8, 0x4a, look.boltGlow, "BoltGlow");
                AssertColor(padGlow.GetColor("_BaseColor"), 0xff, 0xcf, 0x5a, look.padGlow, "PadGlow");

                float boltMax = Max(boltGlow.GetColor("_BaseColor"));
                float padMax = Max(padGlow.GetColor("_BaseColor"));
                //  "과하게 빛나지 않는다" — 블룸 문턱을 살짝 넘는 정도까지만(세기 배수가 그대로 밑감에 곱해진다).
                Assert.That(boltMax, Is.InRange(1f, 1.5f), "⚡ 판이 블룸 문턱을 안 넘거나 너무 밝다");
                Assert.Less(padMax, 1f, "발판이 블룸 문턱을 넘는다(은은해야 한다)");

                //  세기를 바꾸면 그대로 반영된다(두 배로 밝아지면 밑감도 두 배).
                float before = Max(AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/BoltGlow.mat").GetColor("_BaseColor"));
                look.boltGlow *= 2f;
                FlappyMineMaterials.Ensure(look);
                float after = Max(AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/BoltGlow.mat").GetColor("_BaseColor"));
                Assert.AreEqual(before * 2f, after, 0.01f, "boltGlow 세기가 밑감에 그대로 반영되지 않는다");
            }
            finally
            {
                Object.DestroyImmediate(look);
            }
        }

        private static float Max(Color c) => Mathf.Max(c.r, c.g, c.b);

        //  밑감(8비트 채널)에 세기를 곱한 값과 실제 _BaseColor를 비교한다.
        private static void AssertColor(Color actual, byte r, byte g, byte b, float intensity, string label)
        {
            Assert.AreEqual(r / 255f * intensity, actual.r, 0.01f, $"{label} r");
            Assert.AreEqual(g / 255f * intensity, actual.g, 0.01f, $"{label} g");
            Assert.AreEqual(b / 255f * intensity, actual.b, 0.01f, $"{label} b");
        }

        [Test]
        public void 바위_면_장식_재질은_밝은_테보다_어둡고_바위와_대비가_작다()
        {
            //  10-10 Fix 1b: "밝은 것 = 닿는 것" — 장식은 판정선 위 얇은 테(RockEdge)보다 늘 어둡고, 지층 렌즈는 바위와 거의 같은 밝기.
            var look = Look();
            try
            {
                FlappyMineMaterials.Ensure(look);
                float L(string n) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{n}.mat").GetColor("_BaseColor").grayscale;
                float rim = L("RockEdge"), rock = FlappyMineMaterials.CalmRockBase(look).grayscale;
                foreach (string n in new[] { "StrataPatchDark", "StrataPatchLight", "Seam", "TimberDim", "OreDim", "RailDim" })
                {
                    Assert.Less(L(n), rim * 0.75f, $"{n}이 밝은 테만큼 밝다");
                }
                Assert.Less(Mathf.Abs(L("StrataPatchLight") - rock), 0.05f, "밝은 렌즈 대비가 크다");
                Assert.Less(Mathf.Abs(L("StrataPatchDark") - rock), 0.05f, "어두운 렌즈 대비가 크다");
                Assert.Less(L("Seam"), rock, "금이 바위보다 어둡지 않다");

                var remap = ((ModelImporter)AssetImporter.GetAtPath("Assets/Art/Models/Mine/TimberSet.fbx")).GetExternalObjectMap();
                var dim = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/TimberDim.mat");
                Assert.IsTrue(remap.Any(kv => kv.Key.name == "TimberDim" && kv.Value == dim), "TimberSet.fbx의 TimberDim 슬롯이 리매핑 안 됐다");
            }
            finally
            {
                Object.DestroyImmediate(look);
            }
        }

        [Test]
        public void 새_부품_FBX는_새_재질로_리매핑된다()
        {
            var look = Look();
            try
            {
                FlappyMineMaterials.Ensure(look);

                string fbxPath = "Assets/Art/Models/Mine/BoltSign.fbx";
                Assert.IsTrue(File.Exists(fbxPath), $"{fbxPath}가 없다");
                var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
                var remap = importer.GetExternalObjectMap();

                var boltRim = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/BoltRim.mat");
                var boltGlow = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/BoltGlow.mat");
                var signWood = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/SignWood.mat");

                bool Mapped(string slot, Material target) => remap.Any(kv =>
                    kv.Key.type == typeof(Material) && kv.Key.name == slot && kv.Value == target);

                Assert.IsTrue(Mapped("BoltRim", boltRim), "BoltSign.fbx의 BoltRim 슬롯이 리매핑 안 됐다");
                Assert.IsTrue(Mapped("BoltGlow", boltGlow), "BoltSign.fbx의 BoltGlow 슬롯이 리매핑 안 됐다");
                Assert.IsTrue(Mapped("SignWood", signWood), "BoltSign.fbx의 SignWood 슬롯이 리매핑 안 됐다");
            }
            finally
            {
                Object.DestroyImmediate(look);
            }
        }

        //  펼치기 2차(10-12) — 출구 부품 새 슬롯 3개(CliffSun·FlagCream·FlagBrown).

        [Test]
        public void 출구_부품_새_슬롯은_따뜻하고_밝은_테보다_어둡고_FBX에_리매핑된다()
        {
            var look = Look();
            try
            {
                FlappyMineMaterials.Ensure(look);
                Material M(string n) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{n}.mat");
                Color C(string n) => M(n).GetColor("_BaseColor");
                float rim = C("RockEdge").grayscale;
                Color rock = FlappyMineMaterials.CalmRockBase(look);
                foreach (string n in new[] { "CliffSun", "FlagCream", "FlagBrown" })
                {
                    Assert.IsNotNull(M(n), $"{n}.mat이 없다");
                    Assert.AreEqual("LOP/Toon", M(n).shader.name, $"{n}이 LOP/Toon이 아니다");
                    Assert.IsFalse(M(n).GetShaderPassEnabled("SRPDefaultUnlit"), $"{n}에 외곽선이 켜져 있다");
                    Assert.Less(C(n).grayscale, rim, $"{n}이 밝은 테보다 밝다(밝은 것 = 닿는 것)");
                    Assert.Greater(C(n).r - C(n).b, 0.1f, $"{n}이 따뜻한 색이 아니다(형광·회색 금지)");
                }
                //  절벽 윗면: 바위보다 조금 밝고 더 따뜻하지만(노을 빛) 테처럼 튀지 않는다.
                Color sun = C("CliffSun");
                Assert.Greater(sun.grayscale, rock.grayscale, "절벽 윗면이 빛을 받은 것 같지 않다");
                Assert.Greater(sun.r - sun.b, rock.r - rock.b, "절벽 윗면이 바위보다 따뜻하지 않다");
                Assert.Less(sun.grayscale, rim * 0.75f, "절벽 윗면이 밝은 테처럼 튄다");
                //  체크 깃발은 두 색이 또렷이 갈린다.
                Assert.Greater(C("FlagCream").grayscale - C("FlagBrown").grayscale, 0.25f, "체크 두 색 대비가 작다");
                //  결승선 반투명 줄(10-12 시안 B): 깃발 두 색 그대로, 알파만 FinishStripeAlpha, 투명 큐.
                foreach (string n in new[] { "FlagCream", "FlagBrown" })
                {
                    Material fade = M(n + "Fade");
                    Assert.IsNotNull(fade, $"{n}Fade.mat이 없다");
                    Assert.AreEqual((Vector3)(Vector4)C(n), (Vector3)(Vector4)fade.GetColor("_BaseColor"), $"{n}Fade 색이 깃발과 다르다");
                    Assert.AreEqual(FlappyMineMaterials.FinishStripeAlpha, fade.GetColor("_BaseColor").a, 1e-3f, $"{n}Fade 알파");
                    Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, fade.renderQueue, $"{n}Fade가 투명 큐가 아니다");
                }

                bool Mapped(string fbx, string slot) =>
                    ((ModelImporter)AssetImporter.GetAtPath($"Assets/Art/Models/Mine/{fbx}.fbx")).GetExternalObjectMap()
                        .Any(kv => kv.Key.type == typeof(Material) && kv.Key.name == slot && kv.Value == M(slot));
                Assert.IsTrue(Mapped("CliffEdge", "CliffSun"), "CliffEdge.fbx의 CliffSun 슬롯이 리매핑 안 됐다");
                Assert.IsTrue(Mapped("CliffEdge", "Strata"), "CliffEdge.fbx의 Strata 슬롯이 리매핑 안 됐다(월드 UV 바위)");
                Assert.IsTrue(Mapped("FinishFlag", "FlagCream") && Mapped("FinishFlag", "FlagBrown") && Mapped("FinishFlag", "Timber"),
                              "FinishFlag.fbx 슬롯이 리매핑 안 됐다");
                Assert.IsTrue(Mapped("ExitSign", "SignWood") && Mapped("ExitSign", "SignInlay") && Mapped("ExitSign", "Iron"),
                              "ExitSign.fbx 슬롯(EntranceSign과 같은 재질)이 리매핑 안 됐다");
            }
            finally
            {
                Object.DestroyImmediate(look);
            }
        }

        [Test]
        public void 굴_입구_안개_띠는_바깥_안개에서_굴_안개로_섞인다()
        {
            var look = Look();
            try
            {
                FlappyMineMaterials.Ensure(look);
                var tex = ReadPng(AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Haze_Mouth.mat"));
                try
                {
                    Color left = tex.GetPixel(0, 0), right = tex.GetPixel(tex.width - 1, 0), mid = tex.GetPixel(tex.width / 2, 0);
                    Assert.AreEqual(look.hazeOutsideColor.r, left.r, 0.01f, "왼끝이 바깥 안개색이 아니다");
                    Assert.AreEqual(look.hazeCaveColor.r, right.r, 0.01f, "오른끝이 굴 안개색이 아니다");
                    Assert.AreEqual(look.hazeAlpha, left.a, 0.01f, "왼끝 진하기가 바깥 안개와 다르다");
                    Assert.AreEqual(look.hazeCaveAlpha, right.a, 0.01f, "오른끝 진하기가 굴 안개와 다르다");
                    Assert.That(mid.r, Is.InRange(look.hazeCaveColor.r + 0.1f, look.hazeOutsideColor.r - 0.1f), "가운데가 섞인 색이 아니다");
                }
                finally { Object.DestroyImmediate(tex); }
            }
            finally
            {
                Object.DestroyImmediate(look);
            }
        }
    }
}
