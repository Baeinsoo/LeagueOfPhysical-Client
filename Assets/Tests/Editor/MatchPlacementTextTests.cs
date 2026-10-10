using System.Collections.Generic;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 무승부를 등수로 어떻게 적는지 고정한다. 결과 화면과 프로필 전적이 <b>같은 판단</b>을 써야 하는데,
    /// 한쪽만 고쳤다가 실제로 갈라진 적이 있다 — 결과 화면엔 "무승부"가 뜨는데 전적엔 "1등"이 남았다.
    /// 무승부 판정은 "전원 동점"만 인정한다 — 1등만 여럿이고 뒤가 갈리면 공동 1등일 뿐이다.
    /// </summary>
    public class MatchPlacementTextTests
    {
        [Test]
        public void 일등이_하나면_무승부가_아니다()
        {
            Assert.IsFalse(MatchResultViewModel.IsDrawn(new List<int> { 1, 2 }));
        }

        [Test]
        public void 일등이_여럿이면서_전원이_동점이면_무승부다()
        {
            //  승자 없이 끝난 판은 전원 공동 1등으로 온다 — 전원이 같은 등수일 때만 무승부다.
            Assert.IsTrue(MatchResultViewModel.IsDrawn(new List<int> { 1, 1 }));
            Assert.IsTrue(MatchResultViewModel.IsDrawn(new List<int> { 2, 2 }));
        }

        [Test]
        public void 일등이_여럿이어도_뒤가_갈리면_무승부가_아니다()
        {
            //  1등 둘은 동점이지만 3·4등이 있다 — 전원 동점이 아니므로 공동 1등일 뿐, 무승부는 아니다.
            Assert.IsFalse(MatchResultViewModel.IsDrawn(new List<int> { 1, 1, 3, 4 }));
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
            Assert.AreEqual("-", MatchResultViewModel.FormatPlacement(1, isDraw: true, tiedCount: 2));
        }

        [Test]
        public void 무승부가_아니고_동점도_없으면_등수를_그대로_적는다()
        {
            Assert.AreEqual("2등", MatchResultViewModel.FormatPlacement(2, isDraw: false, tiedCount: 1));
            Assert.AreEqual("3등", MatchResultViewModel.FormatPlacement(3, isDraw: false, tiedCount: 1));
        }

        [Test]
        public void 무승부가_아니지만_그_등수에_둘_이상_몰리면_공동으로_적는다()
        {
            //  2위 이하 동점 — 전원 동점은 아니라 무승부는 아니지만, 그 등수 자체는 공동이다.
            Assert.AreEqual("공동 1등", MatchResultViewModel.FormatPlacement(1, isDraw: false, tiedCount: 2));
        }

        [Test]
        public void 그_등수에_몰린_인원수를_센다()
        {
            var placements = new List<int> { 1, 1, 3 };
            Assert.AreEqual(2, MatchResultViewModel.TiedCount(placements, 1));
            Assert.AreEqual(1, MatchResultViewModel.TiedCount(placements, 3));
            Assert.AreEqual(0, MatchResultViewModel.TiedCount(placements, 2));
        }

        [Test]
        public void 줄은_표기가_아니라_사실을_든다()
        {
            //  줄에 "-" 같은 표기를 박아 두면 데이터가 표현을 정해 버린다. 줄은 "무승부였다"만
            //  들고, 그걸 어떻게 적을지는 그리는 쪽이 공용 표기 함수로 정한다.
            var drawn = new MatchResultRow(1, "나", isMe: true, isDraw: true);
            var won = new MatchResultRow(1, "나", isMe: true, isDraw: false);

            Assert.IsTrue(drawn.IsDraw);
            Assert.IsFalse(won.IsDraw);
            Assert.AreEqual(1, drawn.Placement, "등수 자체는 서버가 준 값 그대로 남는다");

            //  그리는 쪽이 같은 함수를 거치므로 두 화면이 갈라질 수 없다.
            Assert.AreEqual("-", MatchResultViewModel.FormatPlacement(drawn.Placement, drawn.IsDraw, tiedCount: 2));
            Assert.AreEqual("1등", MatchResultViewModel.FormatPlacement(won.Placement, won.IsDraw, tiedCount: 1));
        }
    }
}
