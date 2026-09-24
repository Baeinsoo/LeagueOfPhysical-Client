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
        public void 부스트_앞이_막혔으면_잡는다()
        {
            //  이것이 없어서 6개 패드가 전부 함정인 채로 배포됐다(2026-09-24). 대시는 중력도
            //  날갯짓도 없는 수평 직선이라, 패드 자리가 비어 있어도 앞이 막혔으면 그대로 박는다.
            var pad = new BoostPadMeasure("BoostPad_74", 74f, -2.18f, 2.18f, 0.6f,
                                          -10.92f, 10.92f, null,
                                          blockedAheadName: "ComposedMap/PipeHigh_79", boostSpan: 8.16f);

            Assert.IsTrue(pad.BlockedAhead);
            Assert.IsFalse(pad.Ok, "자리가 비었다는 것만으로 통과시키면 안 된다");

            string section = BoostPadRule.Section(new List<BoostPadMeasure> { pad },
                                                 Forward, DashMult, CollisionCost);
            Assert.That(section, Does.Contain("PipeHigh_79"));
            Assert.That(section, Does.Contain("높이를 못 바꾸므로"));
        }

        [Test]
        public void 앞이_뚫렸으면_통과다()
        {
            var pad = new BoostPadMeasure("BoostPad_74", 74f, -2.18f, 2.18f, 0.6f,
                                          -10.92f, 10.92f, null,
                                          blockedAheadName: null, boostSpan: 8.16f);
            Assert.IsFalse(pad.BlockedAhead);
            Assert.IsTrue(pad.Ok);
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
        public void 회랑_안이면_그대로_둔다()
        {
            float y = 0f;
            Assert.That(BoostPadRule.Fit(ref y, 4f, -10f, 10f), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void 벽에_물린_쪽만_잘라_낸다()
        {
            //  통째로 밀지 않는다 — 밀면 패드가 도전 차선을 벗어나 안전한 쪽으로 넘어간다.
            //  아래가 0.5m 물렸으면 아래만 0.5m 깎이고 위는 그대로다.
            float y = -8f;                               // 아래 −10, 위 −6
            float height = BoostPadRule.Fit(ref y, 4f, -9.5f, 10f);

            Assert.That(height, Is.EqualTo(3.5f).Within(1e-4f));
            Assert.That(y, Is.EqualTo(-7.75f).Within(1e-4f));   // (−9.5 + −6) / 2
        }

        [Test]
        public void 양쪽_다_물리면_양쪽_다_깎는다()
        {
            float y = 0f;
            Assert.That(BoostPadRule.Fit(ref y, 10f, -2f, 3f), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(y, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void 회랑이_아예_없으면_0을_돌려준다()
        {
            //  부르는 쪽이 "최소 높이 미달"로 걸러야 한다 — 억지로 놓으면 못 밟는 패드가 된다.
            float y = 20f;
            Assert.That(BoostPadRule.Fit(ref y, 4f, -1f, 1f), Is.EqualTo(0f).Within(1e-4f));
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
