using System.Collections.Generic;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 무승부를 등수로 어떻게 적는지 고정한다. 결과 화면과 프로필 전적이 <b>같은 판단</b>을 써야 하는데,
    /// 한쪽만 고쳤다가 실제로 갈라진 적이 있다 — 결과 화면엔 "무승부"가 뜨는데 전적엔 "1등"이 남았다.
    /// </summary>
    public class MatchPlacementTextTests
    {
        [Test]
        public void 일등이_하나면_무승부가_아니다()
        {
            Assert.IsFalse(MatchResultViewModel.IsDrawn(new List<int> { 1, 2 }));
        }

        [Test]
        public void 일등이_여럿이면_무승부다()
        {
            //  승자 없이 끝난 판은 전원 공동 1등으로 온다 — 등수 동점이 곧 무승부 신호다.
            Assert.IsTrue(MatchResultViewModel.IsDrawn(new List<int> { 1, 1 }));
        }

        [Test]
        public void 공동_꼴등은_무승부가_아니다()
        {
            //  1등이 하나면 그 사람이 이긴 것이다. 뒤가 몇이든 상관없다.
            Assert.IsFalse(MatchResultViewModel.IsDrawn(new List<int> { 1, 2, 2 }));
        }

        [Test]
        public void 무승부면_등수_자리를_비운다()
        {
            Assert.AreEqual("-", MatchResultViewModel.FormatPlacement(1, isDraw: true));
        }

        [Test]
        public void 무승부가_아니면_등수를_적는다()
        {
            Assert.AreEqual("2등", MatchResultViewModel.FormatPlacement(2, isDraw: false));
        }

        [Test]
        public void 줄이_표기를_들고_있어_두_화면이_갈라지지_않는다()
        {
            //  화면이 각자 `{Placement}등`을 만들면 한쪽만 고쳐지는 일이 다시 생긴다.
            var drawn = new MatchResultRow(1, "나", isMe: true, isDraw: true);
            var won = new MatchResultRow(1, "나", isMe: true, isDraw: false);

            Assert.AreEqual("-", drawn.PlacementText);
            Assert.AreEqual("1등", won.PlacementText);
        }
    }
}
