using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>
    /// 카운트다운 화면: 요소가 있고 입력을 안 잡는다(카운트다운 중 탭은 월드가 무시한다). 플래피 개편은 변형 클래스(--pop·--go)에만 있고,
    /// 기본 글씨 규칙(기마 궁술·대기 문구)은 예전 그대로다(사용자 결정 10-25).
    /// </summary>
    public class RaceStartViewTests
    {
        private const string Uxml = "Assets/UI/RaceStart/RaceStartView.uxml";
        private const string Uss = "Assets/UI/RaceStart/RaceStartView.uss";

        //  ".race-start-text {" 부터 첫 "}"까지 — 기본 규칙 한 덩어리.
        private static string Block(string css, string selector)
        {
            int start = css.IndexOf(selector + " {", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, selector);
            int end = css.IndexOf('}', start);
            return css.Substring(start, end - start);
        }

        [Test]
        public void 요소가_있고_입력을_안_잡는다()
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).Instantiate();
            foreach (var name in new[] { "race-start-root", "race-start-text" })
            {
                Assert.IsNotNull(root.Q(name), name);
                Assert.AreEqual(PickingMode.Ignore, root.Q(name).pickingMode, name);
            }
        }

        [Test]
        public void 플래피_팝과_GO_초록은_변형_클래스에만()
        {
            string s = File.ReadAllText(Uss);
            string pop = Block(s, ".race-start-text--pop");
            StringAssert.Contains("font-size: 220px", pop);
            StringAssert.Contains("transform-origin: center", pop, "팝이 가운데서 커져야 글자가 옆으로 밀리지 않는다");
            StringAssert.Contains("rgb(123, 224, 138)", Block(s, ".race-start-text--go"), "GO! 초록 #7be08a");
        }

        [Test]
        public void 기본_글씨는_예전_그대로()
        {
            //  기마 궁술 카운트다운과 플래피 대기 문구가 이 규칙만 쓴다 — 바뀌면 기마 궁술 모양이 바뀐다.
            string text = Block(File.ReadAllText(Uss), ".race-start-text");
            StringAssert.Contains("font-size: 120px", text);
            StringAssert.Contains("color: rgb(255, 255, 255)", text);
            StringAssert.Contains("-unity-text-outline-width: 3px", text);
            StringAssert.DoesNotContain("transform-origin", text);
        }
    }
}
