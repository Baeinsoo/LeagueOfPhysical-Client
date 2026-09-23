using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 런타임 코드가 <c>Shader.Find</c>로 찾는 셰이더가 폰 빌드에서도 잡히는지 지킨다.
    /// <para>에디터에서는 프로젝트 전체를 뒤져 늘 찾아내지만, 폰 빌드에서는 <b>빌드에 담긴 것만</b>
    /// 찾는다. 못 찾으면 null이 오고 <c>new Material(null)</c>에서 "Value cannot be null.
    /// Parameter name: shader"로 매 프레임 터진다 — 2026-09-23 아이폰 양궁에서 과녁과 화살이
    /// 그렇게 안 그려졌다(4천여 줄).</para>
    /// </summary>
    public class RuntimeShaderInclusionTests
    {
        private const string SettingsPath = "ProjectSettings/GraphicsSettings.asset";

        //  엔진이 자기 내부용으로 들고 있는 리소스 묶음. 여기 든 셰이더는 모든 빌드에 무조건
        //  따라가므로 목록에 얹을 필요가 없다 — 얹으면 오히려 빌드가 깨진다(아래 두 번째 시험).
        private const string EngineResources = "Library/unity default resources";

        [Test]
        public void RuntimeCode_ShaderFind_NamesAreAvailableInPlayer()
        {
            var wanted = FindShaderNamesUsedByRuntimeCode();
            Assert.That(wanted, Is.Not.Empty, "런타임 Shader.Find를 하나도 못 찾았다 — 스캔이 깨진 것이다.");

            var included = AlwaysIncludedShaderNames();
            var missing = new List<string>();

            foreach (var pair in wanted)
            {
                var shader = Shader.Find(pair.Key);
                if (shader != null && AssetDatabase.GetAssetPath(shader) == EngineResources)
                {
                    continue;   // 엔진이 늘 들고 다니는 것
                }
                if (included.Contains(pair.Key) == false)
                {
                    missing.Add($"  {pair.Key}  ← {string.Join(", ", pair.Value)}");
                }
            }

            Assert.That(missing, Is.Empty,
                "빌드에 안 담기는 셰이더를 런타임 코드가 이름으로 찾는다. "
                + "Graphics ▸ Always Included Shaders에 넣어야 한다:\n" + string.Join("\n", missing));
        }

        /// <summary>
        /// 엔진 내부 리소스를 Always Included 목록에 넣으면 <b>플레이어 빌드 자체가 깨진다</b>.
        /// <para>2026-09-23에 <c>Hidden/Internal-Colored</c>를 넣었다가 겪었다 — 컴파일은 다 통과한 뒤
        /// 마지막 쓰기 단계에서 <c>Assertion failed on expression: 'm_LockCount == 0'</c> 와
        /// <c>Failed to write file: .../Resources/unity_builtin_extra</c> 로 죽는다. 이 시험이 없으면
        /// 5분짜리 빌드를 돌린 뒤에야 알게 되고, 메시지가 원인을 전혀 안 가리킨다.</para>
        /// </summary>
        [Test]
        public void AlwaysIncludedShaders_DoNotContainEngineInternalResources()
        {
            var offenders = AlwaysIncludedShaders()
                .Where(s => AssetDatabase.GetAssetPath(s) == EngineResources)
                .Select(s => s.name)
                .ToList();

            Assert.That(offenders, Is.Empty,
                $"'{EngineResources}' 의 셰이더는 Always Included 목록에 넣으면 안 된다"
                + "(어차피 늘 빌드에 있고, 넣으면 빌드가 깨진다): " + string.Join(", ", offenders));
        }

        //  파일 텍스트를 훑는다 — "이름으로 찾는 곳이 있는지"가 질문이라 실행하지 않고도 답이 나오는
        //  쪽이 정확하다. 문자열 상수가 아닌 인자는 못 잡지만 지금 코드베이스엔 없다.
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

        private static IEnumerable<Object> AlwaysIncludedShaders()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath(SettingsPath).First();
            var list = new SerializedObject(settings).FindProperty("m_AlwaysIncludedShaders");

            for (int i = 0; i < list.arraySize; i++)
            {
                var shader = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (shader != null)
                {
                    yield return shader;
                }
            }
        }

        private static HashSet<string> AlwaysIncludedShaderNames()
        {
            return new HashSet<string>(AlwaysIncludedShaders().Select(s => s.name));
        }
    }
}
