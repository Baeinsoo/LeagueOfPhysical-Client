using System.Collections.Generic;
using System.IO;
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
