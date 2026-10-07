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
    }
}
