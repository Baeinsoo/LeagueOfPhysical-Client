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
    }
}
