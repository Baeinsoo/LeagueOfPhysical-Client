using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 결과판 시각표의 "내 기록" 자리(spec §7, 사용자 결정 10-26): 카드는 등수 줄과 <b>함께</b> 들어온다 — 첫 줄 자리.
    /// 경신이면 카드가 선 뒤 0.3초에 NEW BEST!, 확인은 모든 등장이 끝난 뒤. 기록이 없으면 지금 시각표 그대로(다른 종목 결과판 무변경).
    /// </summary>
    public class ResultRevealRecordTests
    {
        [Test]
        public void 기록이_없으면_시각표가_그대로()
        {
            var before = new ResultReveal(4);
            var plain = new ResultReveal(4, hasRecords: false, hasNewBest: false);

            Assert.AreEqual(before.LineAt(ResultReveal.LineSlots - 1) + ResultReveal.OkPause, plain.Ok, 1e-5f);
            Assert.IsFalse(plain.NewBestShown(100f, skipped: true));
        }

        [Test]
        public void 카드는_등수_첫_줄과_함께_들어온다()
        {
            //  10-26: 예전엔 랭크·보상 줄 뒤(등수 2줄이면 하얀 빛이 걷힌 뒤 1.18초)라 결과판을 일찍 닫으면 못 봤다.
            var r = new ResultReveal(4, hasRecords: true);

            Assert.AreEqual(r.RowAt(0), r.RecordAt, 1e-5f);
            Assert.Less(r.RecordAt, r.LineAt(0), "랭크 줄보다 먼저");
        }

        [Test]
        public void 카드가_일찍_서도_확인은_아래_줄이_다_선_뒤()
        {
            var r = new ResultReveal(4, hasRecords: true);

            Assert.AreEqual(r.LineAt(ResultReveal.LineSlots - 1) + ResultReveal.OkPause, r.Ok, 1e-5f);
        }

        [Test]
        public void 경신하면_카드가_선_뒤_0_3초에_도장_그다음_확인()
        {
            var r = new ResultReveal(2, hasRecords: true, hasNewBest: true);

            Assert.AreEqual(r.RecordAt + 0.3f + 0.3f, r.NewBestAt, 1e-5f);
            Assert.IsFalse(r.NewBestShown(r.NewBestAt - 0.01f, skipped: false));
            Assert.IsTrue(r.NewBestShown(r.NewBestAt, skipped: false));
            Assert.IsTrue(r.NewBestShown(0f, skipped: true), "건너뛰면 바로");
            Assert.AreEqual(r.NewBestAt + ResultReveal.NewBestIn + ResultReveal.OkPause, r.Ok, 1e-5f, "등수 2줄이면 도장이 아래 줄보다 늦다");
        }

        [Test]
        public void 등수_줄이_많아도_확인은_제일_늦은_등장_뒤()
        {
            var r = new ResultReveal(8, hasRecords: true, hasNewBest: true);

            Assert.Greater(r.LineAt(ResultReveal.LineSlots - 1), r.NewBestAt + ResultReveal.NewBestIn);
            Assert.AreEqual(r.LineAt(ResultReveal.LineSlots - 1) + ResultReveal.OkPause, r.Ok, 1e-5f);
        }

        [Test]
        public void 기록이_없으면_경신도_없다()
        {
            Assert.IsFalse(new ResultReveal(4, hasRecords: false, hasNewBest: true).NewBestShown(100f, skipped: true));
        }
    }
}
