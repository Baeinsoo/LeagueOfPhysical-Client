using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Map.Tests
{
    /// <summary>
    /// FlappyRaceMap 씬이 다시 오염되는 것을 막는 회귀 테스트.
    /// 씬을 에디터에서 열지 않고 저장된 .unity 텍스트를 그대로 읽는다 — 이 프로젝트는
    /// "메모리로 보면 지운 것 같은데 저장된 파일엔 남아 있던" 사고를 겪은 적이 있어서,
    /// 저장된 파일이 진실원본이다. 프로덕션 코드는 참조하지 않는다.
    /// </summary>
    public class FlappyRaceMapSceneTests
    {
        // 검증 대상 씬 경로(Assets 기준). 테스트가 실제로 실패할 수 있는지 확인할 때만 임시로 바꾼다.
        const string ScenePathFromAssets = "Art/Scenes/FlappyRaceMap.unity";

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        static string ReadSceneText()
        {
            var path = Path.Combine(Application.dataPath, ScenePathFromAssets);
            return File.ReadAllText(path);
        }

        // manifest.json의 file: 참조를 따라가 LeagueOfPhysical-Shared 패키지의 실제 위치를 찾는다.
        // (하드코딩된 절대경로 대신 매니페스트를 텍스트로 읽어 따라간다 — 다른 머신에서도 동작해야 해서다.)
        static string SharedRuntimeRoot()
        {
            var manifestPath = Path.Combine(ProjectRoot, "Packages", "manifest.json");
            var manifestText = File.ReadAllText(manifestPath);
            var packageMatch = Regex.Match(
                manifestText,
                "\"com\\.baegames\\.lop\\.shared\"\\s*:\\s*\"file:(?<rel>[^\"]+)\"");
            Assert.IsTrue(packageMatch.Success,
                "manifest.json에서 com.baegames.lop.shared의 file: 참조를 못 찾았다.");

            return Path.GetFullPath(
                Path.Combine(ProjectRoot, "Packages", packageMatch.Groups["rel"].Value, "Runtime"));
        }

        static HashSet<string> SharedPackageScriptGuids()
        {
            var runtimeRoot = SharedRuntimeRoot();
            var guids = new HashSet<string>();
            foreach (var metaPath in Directory.GetFiles(runtimeRoot, "*.cs.meta", SearchOption.AllDirectories))
            {
                var guidMatch = Regex.Match(File.ReadAllText(metaPath), @"^guid:\s*([0-9a-fA-F]{32})",
                                            RegexOptions.Multiline);
                if (guidMatch.Success)
                {
                    guids.Add(guidMatch.Groups[1].Value);
                }
            }

            Assert.IsNotEmpty(guids, $"{runtimeRoot} 아래에서 스크립트 guid를 하나도 못 읽었다.");
            return guids;
        }

        [Test]
        //  지키려는 것은 "SpawnPoint만 있어야 한다"가 아니라 "공용 패키지에서 온 스크립트여야 한다"이다.
        //  맵 씬은 클라에서 만들고 서버가 읽는데, 클라에만 있는 스크립트는 서버에서 missing script가
        //  되고 그 빈 컴포넌트가 씬 주입을 끊는다. 공용 패키지 스크립트는 양쪽이 같은 GUID를 보므로
        //  안전하다 — 그래서 마커가 늘어도(SpawnPoint, FinishLine, …) 이 테스트는 그대로 둔다.
        public void MonoBehavioursComeFromSharedPackage()
        {
            var sceneText = ReadSceneText();
            var sharedGuids = SharedPackageScriptGuids();

            var foreignGuids = Regex.Matches(sceneText, @"m_Script: \{fileID: 11500000, guid: ([0-9a-fA-F]{32}),")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Where(guid => sharedGuids.Contains(guid) == false)
                .Distinct()
                .ToList();

            Assert.IsEmpty(foreignGuids,
                "맵 씬에 공용 패키지(LeagueOfPhysical-Shared) 밖의 MonoBehaviour가 있다 (guid: " +
                string.Join(", ", foreignGuids) +
                "). 한쪽에만 있는 스크립트는 서버에서 missing script가 되어 씬 주입을 끊는다.");
        }

        [Test]
        //  유니티가 스크립트 자산을 못 찾으면 guid 없이 {fileID: N}만 적어 저장한다. 에디터 메모리에선
        //  멀쩡히 붙어 있어 맵 검사도 통과하지만, 다시 불러오면 missing script가 되어 씬 주입이 끊긴다
        //  (2026-09-28 홀로그램 관문 — 클라가 기류·결승선을 몰라 서버와 갈렸다).
        public void EveryMonoBehaviourReferencesAScriptAsset()
        {
            var sceneText = ReadSceneText();
            var dangling = Regex.Matches(sceneText, @"m_Script: \{fileID: -?\d+\}").Count;

            Assert.AreEqual(0, dangling,
                "맵 씬에 스크립트 자산을 가리키지 않는 MonoBehaviour가 있다 — 다시 불러오면 missing script다.");
        }

        [Test]
        //  위 사고의 뿌리. 유니티는 파일 이름과 같은 이름의 클래스만 컴포넌트 스크립트로 찾는다.
        //  다른 파일에 끼워 둔 MonoBehaviour는 붙일 수는 있어도 씬에 제대로 저장되지 않는다.
        public void SharedMonoBehavioursLiveInFilesOfTheirName()
        {
            var misplaced = new List<string>();
            foreach (var path in Directory.GetFiles(SharedRuntimeRoot(), "*.cs", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                foreach (Match m in Regex.Matches(File.ReadAllText(path),
                             @"class\s+(\w+)\s*:\s*(?:UnityEngine\.)?MonoBehaviour\b"))
                {
                    if (m.Groups[1].Value != fileName)
                    {
                        misplaced.Add($"{m.Groups[1].Value} ({fileName}.cs)");
                    }
                }
            }

            Assert.IsEmpty(misplaced,
                "공용 패키지에 파일 이름과 다른 MonoBehaviour가 있다: " + string.Join(", ", misplaced));
        }

        [Test]
        public void NoTriggerColliders()
        {
            var sceneText = ReadSceneText();
            var triggerCount = Regex.Matches(sceneText, "m_IsTrigger: 1").Count;

            Assert.AreEqual(0, triggerCount,
                "맵에 트리거 콜라이더가 남아 있다 — sweep이 트리거를 걸러서 새가 그냥 통과한다.");
        }

        [Test]
        public void NoCameraOrLight()
        {
            var sceneText = ReadSceneText();
            var cameraCount = Regex.Matches(sceneText, @"(?m)^--- !u!20 &").Count;
            var lightCount = Regex.Matches(sceneText, @"(?m)^--- !u!108 &").Count;

            Assert.AreEqual(0, cameraCount, "맵 씬에 Camera가 남아 있다 — 게임 씬이 이미 갖고 있다.");
            Assert.AreEqual(0, lightCount, "맵 씬에 Light가 남아 있다 — 게임 씬이 이미 갖고 있다.");
        }

        [Test]
        public void SpawnPointsHaveDistinctOrder()
        {
            var sceneText = ReadSceneText();
            //  줄 끝을 \s*로 받는다 - 같은 씬이 체크아웃 설정에 따라 LF로도 CRLF로도 놓이는데,
            //  $는 캐리지 리턴 앞에서 안 맞아 "SpawnPoint가 하나도 없다"는 엉뚱한 실패가 난다(실제로 겪었다).
            var orders = Regex.Matches(sceneText, @"(?m)^  Order: (-?\d+)\s*$")
                .Cast<Match>()
                .Select(m => int.Parse(m.Groups[1].Value))
                .ToList();

            Assert.IsNotEmpty(orders, "맵 씬에 SpawnPoint가 하나도 없다.");
            Assert.AreEqual(orders.Count, orders.Distinct().Count(),
                "SpawnPoint의 Order 값이 겹친다 — 겹치면 자리 배정이 흔들린다.");
        }
    }
}
