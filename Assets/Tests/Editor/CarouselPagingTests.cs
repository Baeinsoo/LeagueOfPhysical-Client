using NUnit.Framework;
using LOP.UI;

namespace LOP.Tests
{
    /// <summary>
    /// 종목 카드 줄 넘김. 1920 폭·카드 간격 340·양 끝 여백 80 기준 — 한 화면에 5장(1760/340)이 다 보인다.
    /// </summary>
    public class CarouselPagingTests
    {
        private static CarouselPaging Row(int count, float viewport = 1920f) => new CarouselPaging(viewport, 340f, 80f, count);

        [Test]
        public void 다_들어가면_넘길_곳이_없다()
        {
            var p = Row(5);
            Assert.AreEqual(0f, p.MaxOffset);
            Assert.IsFalse(p.CanPrev(0));
            Assert.IsFalse(p.CanNext(0));
            Assert.AreEqual(1, p.PageCount);
        }

        [Test]
        public void 여섯_장이면_뒤로만_넘길_수_있다()
        {
            var p = Row(6);
            Assert.AreEqual(280f, p.MaxOffset);   // 160 + 2040 − 1920
            Assert.IsFalse(p.CanPrev(0));
            Assert.IsTrue(p.CanNext(0));
            Assert.AreEqual(2, p.PageCount);
        }

        [Test]
        public void 화살표는_한_화면씩_넘기고_끝에서_멈춘다()
        {
            var p = Row(12);   // 끝 = 160 + 4080 − 1920 = 2320
            Assert.AreEqual(5, p.PerPage);
            Assert.AreEqual(1700f, p.Step(0, +1));
            Assert.AreEqual(2320f, p.Step(1700f, +1));
            Assert.AreEqual(2320f, p.Step(2320f, +1));
            //  끝에서 한 화면 뒤(620)는 카드 사이라 가까운 카드 자리(680)에 멈춘다.
            Assert.AreEqual(680f, p.Step(2320f, -1));
            Assert.AreEqual(0f, p.Step(680f, -1));
        }

        [Test]
        public void 손으로_밀다_놓으면_가까운_카드에_멈춘다()
        {
            var p = Row(12);
            Assert.AreEqual(340f, p.Snap(400f));
            Assert.AreEqual(680f, p.Snap(600f));
            Assert.AreEqual(0f, p.Snap(-30f));
            //  끝 근처는 카드 자리보다 끝이 가까우면 끝에 붙는다.
            Assert.AreEqual(2320f, p.Snap(2300f));
        }

        [Test]
        public void 쪽_표시는_지금_위치를_가리킨다()
        {
            var p = Row(12);
            Assert.AreEqual(3, p.PageCount);
            Assert.AreEqual(0, p.PageIndex(0));
            Assert.AreEqual(1, p.PageIndex(1700f));
            Assert.AreEqual(2, p.PageIndex(2320f));
        }

        [Test]
        public void 고른_카드가_가려져_있으면_보이게_넘긴다()
        {
            var p = Row(12);
            Assert.AreEqual(0f, p.Reveal(2, 0f), "이미 보이면 그대로");
            Assert.AreEqual(340f * 5, p.Reveal(9, 0f), "뒤쪽 카드 — 맨 오른쪽에 보이게");
            Assert.AreEqual(340f * 3, p.Reveal(3, 2000f), "앞쪽 카드 — 맨 왼쪽에 보이게");
            Assert.AreEqual(2320f, p.Reveal(11, 0f));
        }

        [Test]
        public void 고른_카드도_같은_간격으로_잰다()
        {
            //  보통 카드 300 + 20·20, 고른 카드 312 + 14·14(테두리 6). 위치 차이로 재면 346·334가 나왔다.
            Assert.AreEqual(340f, CarouselPaging.MeasureStride(300f, 20f, 20f));
            Assert.AreEqual(340f, CarouselPaging.MeasureStride(312f, 14f, 14f));
        }

        [Test]
        public void 레이아웃_전에는_넘길_곳이_없다()
        {
            var p = new CarouselPaging(float.NaN, float.NaN, float.NaN, 12);
            Assert.AreEqual(0f, p.MaxOffset);
            Assert.AreEqual(1, p.PageCount);
            Assert.AreEqual(0, p.PageIndex(0));
            Assert.AreEqual(0f, p.Step(0, +1));
            Assert.AreEqual(0f, p.Snap(0));
        }
    }
}
