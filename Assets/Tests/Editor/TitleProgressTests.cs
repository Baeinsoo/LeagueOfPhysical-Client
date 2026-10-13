using System.Linq;
using LOP;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>타이틀 진행 막대 — 부팅 단계가 시작될 때마다 한 칸씩, 끝나야 가득.</summary>
    public class TitleProgressTests
    {
        [Test]
        public void 단계가_시작되면_그_칸까지_찬다()
        {
            var p = new EntranceProgress();
            using var vm = new TitleViewModel(p);
            p.Begin(0, 5, "서버 확인 중");
            Assert.AreEqual(0.2f, vm.Ratio.CurrentValue, 1e-6);
            Assert.AreEqual("서버 확인 중 · 1 / 5", vm.StepText.CurrentValue);
            p.Begin(2, 5, "선수 기록 불러오는 중");
            Assert.AreEqual(0.6f, vm.Ratio.CurrentValue, 1e-6);
            Assert.AreEqual("선수 기록 불러오는 중 · 3 / 5", vm.StepText.CurrentValue);
        }

        [Test]
        public void 끝내야_가득_찬다()
        {
            var p = new EntranceProgress();
            using var vm = new TitleViewModel(p);
            p.Begin(4, 5, "데이터 불러오는 중");
            p.Complete();
            Assert.AreEqual(1f, vm.Ratio.CurrentValue, 1e-6);
        }

        [Test]
        public void 끝내지_않으면_백퍼센트가_아니다()
        {
            //  마지막 단계가 예외로 끝나면 Complete가 불리지 않는다 — 성공처럼 가득 차 보이면 안 된다.
            var p = new EntranceProgress();
            using var vm = new TitleViewModel(p);
            p.Begin(4, 5, "데이터 불러오는 중");
            Assert.Less(vm.Ratio.CurrentValue, 1f);
        }

        [Test]
        public void 시작_전엔_빈_막대에_준비_글자()
        {
            var p = new EntranceProgress();
            using var vm = new TitleViewModel(p);
            Assert.AreEqual(0f, vm.Ratio.CurrentValue);
            Assert.AreEqual("준비 중", vm.StepText.CurrentValue);
            Assert.IsTrue(vm.ShowProgress.CurrentValue);
            Assert.AreEqual(TitleViewModel.TickerBooting, vm.TickerText.CurrentValue);
        }

        [Test]
        public void 입력_대기_중엔_막대를_숨기고_띠_문구를_바꾼다()
        {
            var p = new EntranceProgress();
            using var vm = new TitleViewModel(p);
            p.Begin(1, 5, "선수 확인 중");
            p.SetAwaitingInput(true);
            Assert.IsFalse(vm.ShowProgress.CurrentValue);
            Assert.AreEqual(TitleViewModel.TickerAwaiting, vm.TickerText.CurrentValue);
            p.SetAwaitingInput(false);
            Assert.IsTrue(vm.ShowProgress.CurrentValue);
            Assert.AreEqual(TitleViewModel.TickerBooting, vm.TickerText.CurrentValue);
        }

        [Test]
        public void 다섯_단계의_글자는_비어_있지_않고_서로_다르다()
        {
            var labels = new IEntranceComponent[]
            {
                new ServerCheckComponent(null),
                new LoginComponent(null, null, null, null, null),
                new LoadUserComponent(null, null),
                new JoinLobbyComponent(null),
                new LoadMasterDataComponent(null),
            }.Select(c => c.StepLabel).ToArray();
            Assert.IsTrue(labels.All(l => !string.IsNullOrWhiteSpace(l)));
            Assert.AreEqual(labels.Length, labels.Distinct().Count());
        }
    }
}
