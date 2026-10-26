using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 결과판 시각표의 "내 기록" 자리(spec §7): 등수표 → 랭크·보상 줄 → 카드 → (경신이면) 카드가 선 뒤 0.3초에 NEW BEST! → 확인.
    /// 기록이 없으면 지금 시각표 그대로(다른 종목 결과판 무변경).
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
        public void 카드는_아래_줄_다음_자리()
        {
            var r = new ResultReveal(4, hasRecords: true);

            Assert.AreEqual(r.LineAt(ResultReveal.LineSlots), r.RecordAt, 1e-5f);
            Assert.AreEqual(r.RecordAt + ResultReveal.RecordSettle + ResultReveal.OkPause, r.Ok, 1e-5f);
        }

        [Test]
        public void 경신하면_카드가_선_뒤_0_3초에_도장_그다음_확인()
        {
            var r = new ResultReveal(4, hasRecords: true, hasNewBest: true);

            Assert.AreEqual(r.RecordAt + 0.3f + 0.3f, r.NewBestAt, 1e-5f);
            Assert.IsFalse(r.NewBestShown(r.NewBestAt - 0.01f, skipped: false));
            Assert.IsTrue(r.NewBestShown(r.NewBestAt, skipped: false));
            Assert.IsTrue(r.NewBestShown(0f, skipped: true), "건너뛰면 바로");
            Assert.AreEqual(r.NewBestAt + ResultReveal.NewBestIn + ResultReveal.OkPause, r.Ok, 1e-5f);
        }

        [Test]
        public void 기록이_없으면_경신도_없다()
        {
            Assert.IsFalse(new ResultReveal(4, hasRecords: false, hasNewBest: true).NewBestShown(100f, skipped: true));
        }
    }
}
