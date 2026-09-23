using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

namespace LOP.Tests
{
    /// <summary>
    /// 런타임 코드가 이름으로 찾는 셰이더는 전부 Graphics ▸ Always Included Shaders에 있어야 한다.
    /// <para>에디터에서는 <c>Shader.Find</c>가 프로젝트 전체를 뒤져 늘 찾아내지만, 폰 빌드에서는
    /// <b>빌드에 담긴 셰이더만</b> 찾는다. 번들 안에 든 셰이더도 이름으로는 안 잡힌다. 못 찾으면
    /// null이 오고, 그걸 <c>new Material(null)</c>에 넘겨 "Value cannot be null. Parameter name:
    /// shader"로 매 프레임 터진다 — 실제로 2026-09-23 아이폰 양궁에서 과녁과 화살이 그렇게
    /// 안 그려졌다(4천여 줄).</para>
    /// </summary>
    public class RuntimeShaderInclusionTests
    {
        private const string SettingsPath = "ProjectSettings/GraphicsSettings.asset";

        [Test]
        public void RuntimeCode_ShaderFind_NamesAreAllAlwaysIncluded()
        {
            var wanted = FindShaderNamesUsedByRuntimeCode();
            Assert.That(wanted, Is.Not.Empty, "런타임 Shader.Find를 하나도 못 찾았다 — 스캔이 깨진 것이다.");

            var included = AlwaysIncludedShaderNames();
            var missing = wanted.Keys.Where(name => included.Contains(name) == false).ToList();

            Assert.That(missing, Is.Empty,
                "빌드에 안 담기는 셰이더를 런타임 코드가 이름으로 찾는다:\n"
                + string.Join("\n", missing.Select(m => $"  {m}  ← {string.Join(", ", wanted[m])}")));
        }

        //  파일 텍스트를 훑는다 — 호출이 실제로 도는지가 아니라 "이름으로 찾는 곳이 있는지"가
        //  질문이라, 실행하지 않고도 답이 나오는 쪽이 정확하다. 문자열 상수가 아닌 인자
        //  (변수로 넘기는 경우)는 못 잡지만, 지금 코드베이스엔 없다.
        private static Dictionary<string, List<string>> FindShaderNamesUsedByRuntimeCode()
        {
            var found = new Dictionary<string, List<string>>();
            var pattern = new Regex(@"Shader\.Find\(\s*""([^""]+)""");

            foreach (var path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                var normalized = path.Replace('\\', '/');
                if (normalized.Contains("/Editor/"))
                {
                    continue;   // 에디터 전용 코드는 플레이어에 안 들어간다
                }

                foreach (System.Text.RegularExpressions.Match match in pattern.Matches(File.ReadAllText(path)))
                {
                    var name = match.Groups[1].Value;
                    if (found.TryGetValue(name, out var users) == false)
                    {
                        found[name] = users = new List<string>();
                    }
                    if (users.Contains(normalized) == false)
                    {
                        users.Add(normalized);
                    }
                }
            }

            return found;
        }

        private static HashSet<string> AlwaysIncludedShaderNames()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath(SettingsPath).First();
            var list = new SerializedObject(settings).FindProperty("m_AlwaysIncludedShaders");

            var names = new HashSet<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var shader = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (shader != null)
                {
                    names.Add(shader.name);
                }
            }
            return names;
        }
    }
}
