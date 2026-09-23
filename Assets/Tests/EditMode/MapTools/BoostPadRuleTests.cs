using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 부스트 패드 검사. 패드는 콜라이더가 없어 <b>잘못 놓여도 조용하다</b> — 이 절이 말해 주지
    /// 않으면 아무도 모른다. 그래서 "못 밟는 자리"를 빠뜨리지 않는 것이 이 테스트의 전부다.
    /// </summary>
    public class BoostPadRuleTests
    {
        const float Forward = 6.8f;
        const float DashMult = 2f;
        const float CollisionCost = 5.4f;

        static BoostPadMeasure Pad(float y0 = -2.18f, float y1 = 2.18f, string overlap = null,
                                   float low = -10.92f, float high = 10.92f)
            => new BoostPadMeasure("BoostPad_100", 100f, y0, y1, 0.6f, low, high, overlap);

        [Test]
        public void 회랑_안이고_안_겹치면_통과다()
        {
            Assert.IsTrue(Pad().Ok);
            Assert.That(BoostPadRule.Section(new List<BoostPadMeasure> { Pad() },
                                             Forward, DashMult, CollisionCost),
                        Does.Contain("✅"));
        }

        [Test]
        public void 회랑_밖이면_잡는다()
        {
            //  고저차를 따라가지 못해 패드가 차선 밖으로 나가는 것이 실제로 있을 수 있는 버그다.
            var pad = Pad(y0: 9f, y1: 13f);
            Assert.IsTrue(pad.OutsideCorridor);
            Assert.IsFalse(pad.Ok);
            Assert.That(BoostPadRule.Section(new List<BoostPadMeasure> { pad },
                                             Forward, DashMult, CollisionCost),
                        Does.Contain("회랑 밖"));
        }

        [Test]
        public void 장애물과_겹치면_잡는다()
        {
            var pad = Pad(overlap: "PipeMid_100");
            Assert.IsTrue(pad.Overlapped);
            Assert.That(BoostPadRule.Section(new List<BoostPadMeasure> { pad },
                                             Forward, DashMult, CollisionCost),
                        Does.Contain("PipeMid_100"));
        }

        [Test]
        public void 패드가_아예_없으면_그렇다고_말한다()
        {
            //  빈 절은 "재 봤더니 문제없다"로 잘못 읽힌다 — 없으면 없다고 찍어야 한다.
            Assert.That(BoostPadRule.Section(new List<BoostPadMeasure>(),
                                             Forward, DashMult, CollisionCost),
                        Does.Contain("패드가 없다"));
        }

        [Test]
        public void 이득은_대시와_같은_계산이다()
        {
            //  0.6초 × 6.8m/s × (2−1) = 4.08m. 충돌 5.4m의 0.76배 — 패드 하나가 충돌을 다 메우지
            //  못하는 것이 의도다. 메우면 위험한 쪽이 공짜가 된다.
            Assert.That(BoostPadRule.GainMeters(0.6f, Forward, DashMult),
                        Is.EqualTo(4.08f).Within(1e-3f));
        }

        [Test]
        public void 여섯_개면_코스의_몇_퍼센트인지_셀_수_있다()
        {
            //  코스 612m(90초 × 6.8)에서 6개 = 24.5m = 4%. 관문마다 놓아 24개가 되면 16%라
            //  대시 경제가 통째로 무의미해진다 — 그 선을 이 한 줄로 못박는다.
            var pads = new List<BoostPadMeasure>();
            for (int i = 0; i < 6; i++) { pads.Add(Pad()); }

            string section = BoostPadRule.Section(pads, Forward, DashMult, CollisionCost);

            Assert.That(section, Does.Contain("패드 6개"));
            Assert.That(section, Does.Contain("+24.5m"));
        }
    }
}
