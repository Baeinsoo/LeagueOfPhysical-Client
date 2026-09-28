using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    public class LookDevShaderTests
    {
        [TestCase("Assets/Shaders/LOP/LOPToon.shader", "LOP/Toon")]
        [TestCase("Assets/Shaders/LOP/LOPToonDecal.shader", "LOP/ToonDecal")]
        [TestCase("Assets/Shaders/LOP/LOPWatercolorSky.shader", "LOP/WatercolorSky")]
        public void 셰이더가_오류_없이_컴파일된다(string path, string name)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.IsNotNull(shader, path);
            Assert.AreEqual(name, shader.name);
            var messages = new List<string>();
            foreach (var m in ShaderUtil.GetShaderMessages(shader))
            {
                messages.Add(m.message);
            }
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), string.Join("\n", messages));
        }

        [Test]
        public void 툰_셰이더에_외곽선_그림자_깊이_패스가_있다()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/LOP/LOPToon.shader");
            Assert.IsNotNull(shader);
            var sub = ShaderUtil.GetShaderData(shader).GetSubshader(0);
            var names = new List<string>();
            for (int i = 0; i < sub.PassCount; i++)
            {
                names.Add(sub.GetPass(i).Name.ToUpperInvariant());
            }
            CollectionAssert.IsSupersetOf(names, new[] { "FORWARDTOON", "OUTLINE", "SHADOWCASTER", "DEPTHONLY" });
        }
    }
}
