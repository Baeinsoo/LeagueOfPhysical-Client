using System.IO;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// <see cref="FlappyMineMaterials.Ensure"/>는 실제 <c>Assets/Art/Materials/Mine</c>에 재질을 만든다
    /// (피스처가 아니라 이 슬라이스가 커밋하는 실물 에셋이라 임시 폴더로 옮기지 않았다). 두 번 불러도
    /// 재질 수가 늘지 않고, 값만 최신 <see cref="FlappyMineLook"/>으로 갱신되는지를 본다.
    /// </summary>
    public class FlappyMineMaterialsTests
    {
        private const string MaterialDir = "Assets/Art/Materials/Mine";

        [Test]
        public void 두_번_불러도_재질_수가_늘지_않고_값만_갱신된다()
        {
            var look = ScriptableObject.CreateInstance<FlappyMineLook>();
            //  Plank.mat은 피스처가 아니라 커밋된 실물 에셋이다 — 이 테스트가 값을 바꿔 보려고 건드리므로,
            //  무슨 일이 있어도(단언 실패 포함) 원래 값으로 되돌린다. "원래 값"은 이 look의 기본 필드값 —
            //  grainStrength 기본 1에서 Grain()은 색을 그대로 돌려주므로 재질에 구워진 값과 같다.
            Color originalPlank = look.plankColor;
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
                look.plankColor = originalPlank;
                FlappyMineMaterials.Ensure(look);   // Plank.mat을 실물 기본값으로 되돌린다
                Object.DestroyImmediate(look);
            }
        }

        [Test]
        public void 나무_슬롯은_툰_셰이더에_나무결_텍스처를_쓴다()
        {
            var look = ScriptableObject.CreateInstance<FlappyMineLook>();
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
            var look = ScriptableObject.CreateInstance<FlappyMineLook>();
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
            var look = ScriptableObject.CreateInstance<FlappyMineLook>();
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
            var look = ScriptableObject.CreateInstance<FlappyMineLook>();
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
            var look = ScriptableObject.CreateInstance<FlappyMineLook>();
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
