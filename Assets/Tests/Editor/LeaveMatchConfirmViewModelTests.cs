using Cysharp.Threading.Tasks;
using NUnit.Framework;
using LOP.UI;

namespace LOP.Tests
{
    /// <summary>
    /// 나가기 확인의 결과. <b>어떤 경로로 닫혀도 결과가 확정돼야 한다</b> —
    /// 안 그러면 WindowManager.OpenModalAsync의 대기가 영영 안 풀린다.
    /// </summary>
    public class LeaveMatchConfirmViewModelTests
    {
        //  결과를 읽기 전에 <b>끝났는지부터</b> 단언한다. 안 그러면 결과가 확정되지 않았을 때
        //  테스트가 실패가 아니라 <b>정지</b>로 끝나 뮤테이션 확인이 불가능하다.
        private static void AssertResult(LeaveMatchConfirmViewModel viewModel, bool expected)
        {
            var result = viewModel.ResultAsync;
            Assert.AreEqual(UniTaskStatus.Succeeded, result.Status, "결과가 확정되지 않았다");
            Assert.AreEqual(expected, result.GetAwaiter().GetResult());
        }

        [Test]
        public void 나가기를_누르면_참이다()
        {
            var viewModel = new LeaveMatchConfirmViewModel();

            viewModel.Confirm();

            AssertResult(viewModel, true);
        }

        [Test]
        public void 취소를_누르면_거짓이다()
        {
            var viewModel = new LeaveMatchConfirmViewModel();

            viewModel.Cancel();

            AssertResult(viewModel, false);
        }

        [Test]
        public void 백드롭으로_닫혀도_거짓으로_끝난다()
        {
            //  백드롭 클릭은 Close → View.Dispose → ViewModel.Dispose로 온다. 여기서 결과를
            //  확정하지 않으면 나가기를 누른 쪽이 영원히 기다린다.
            var viewModel = new LeaveMatchConfirmViewModel();

            viewModel.Dispose();

            AssertResult(viewModel, false);
        }

        [Test]
        public void 확정된_뒤에_닫혀도_답이_안_바뀐다()
        {
            var viewModel = new LeaveMatchConfirmViewModel();

            viewModel.Confirm();
            viewModel.Dispose();

            AssertResult(viewModel, true);
        }
    }
}
