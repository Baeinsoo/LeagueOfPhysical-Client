using System.IO;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>9부 작은 창 — 나가기 확인·매칭 실패·관전 막대가 키트 모습이고 View가 찾는 이름을 지키는지.</summary>
    public class SmallWindowsKitTests
    {
        private static void AssertNames(string uxml, params string[] names)
        {
            foreach (var n in names) StringAssert.Contains($"name=\"{n}\"", uxml);
        }

        private static void AssertNoOldClasses(string uxml)
        {
            StringAssert.DoesNotContain("class=\"scrim", uxml);
            StringAssert.DoesNotContain("class=\"card ", uxml);
            StringAssert.DoesNotContain("\"btn ", uxml);
        }

        [Test]
        public void 나가기_확인은_키트_모달()
        {
            var x = File.ReadAllText("Assets/UI/LeaveMatchConfirm/LeaveMatchConfirmView.uxml");
            AssertNames(x, "leave-root", "leave-card", "leave-cancel", "leave-confirm");
            StringAssert.Contains("class=\"kit leave-root", x);
            //  바깥 누르기 판정이 이 요소를 본다 — 판 전체(뒷면 그림자 포함)여야 가장자리를 눌러도 취소되지 않는다.
            StringAssert.Contains("name=\"leave-card\" class=\"kit-panel", x);
            StringAssert.Contains("판을 떠날까요?", x);
            AssertNoOldClasses(x);
            StringAssert.DoesNotContain("#000", File.ReadAllText("Assets/UI/LeaveMatchConfirm/LeaveMatchConfirmView.uss"));
        }

        [Test]
        public void 매칭_실패는_키트_모달_출전_취소()
        {
            var x = File.ReadAllText("Assets/UI/Matchmaking/MatchmakingFailedView.uxml");
            AssertNames(x, "mmf-root", "mmf-card", "mmf-message", "mmf-confirm");
            StringAssert.Contains("class=\"kit mmf-root", x);
            StringAssert.Contains("kit-panel", x);
            StringAssert.Contains("출전 취소", x);
            AssertNoOldClasses(x);
            var s = File.ReadAllText("Assets/UI/Matchmaking/MatchmakingFailedView.uss");
            StringAssert.Contains("white-space: normal", s);   // 사유 문구가 길면 판 안에서 줄바꿈
            StringAssert.DoesNotContain("#000", s);
        }

        [TestCase(CancellationReason.Timeout, "상대를 찾지 못했어요.")]
        [TestCase(CancellationReason.Internal, "경기장 준비에 실패했어요. 다시 해 주세요.")]
        [TestCase(CancellationReason.User, "출전이 취소됐어요.")]
        public void 출전_취소_사유_문구(CancellationReason reason, string expected) =>
            Assert.AreEqual(expected, LOP.UI.MatchmakingCoordinator.MessageFor(reason));
    }
}
