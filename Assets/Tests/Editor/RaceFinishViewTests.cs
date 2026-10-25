using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>결승 도장 화면: 요소가 다 있고, 관전 막대·남은 경기를 가리지 않게 입력을 안 잡으며, 도장 기울기·외곽 규칙이 있다.</summary>
    public class RaceFinishViewTests
    {
        private const string Uxml = "Assets/UI/RaceFinish/RaceFinishView.uxml";
        private const string Uss = "Assets/UI/RaceFinish/RaceFinishView.uss";

        private static readonly string[] Names =
        {
            "race-finish-root", "race-finish-stamp", "race-finish-rank-stamp", "race-finish-rank-sub",
            "race-finish-rank-text", "race-finish-settled", "race-finish-place",
        };

        [Test]
        public void 요소가_다_있고_입력을_안_잡는다()
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).Instantiate();
            foreach (var name in Names)
            {
                Assert.IsNotNull(root.Q(name), name);
                Assert.AreEqual(PickingMode.Ignore, root.Q(name).pickingMode, name);
            }
        }

        [Test]
        public void 도장_기울기와_외곽()
        {
            string s = File.ReadAllText(Uss);
            StringAssert.Contains("rotate: -3.5deg", s, "FINISH! −3.5°");
            StringAssert.Contains("rotate: 3deg", s, "순위 도장 +3°");
            StringAssert.Contains("-unity-text-outline-color: rgb(27, 18, 8)", s, "흰 글자·검은 외곽");
            StringAssert.Contains("max-width: 640px", s, "작은 등수 판은 가운데 좁게 — 오른쪽 위 [나가기]와 안 겹친다");
        }
    }
}
