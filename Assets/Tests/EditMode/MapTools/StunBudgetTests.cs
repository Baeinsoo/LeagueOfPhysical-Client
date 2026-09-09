using System.Collections.Generic;
using LOP;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class StunBudgetTests
    {
        //  0.05는 넓어서 몸 반지름 항(0.45 / 11 = 0.041초)이 통째로 그 안에 숨었다 —
        //  StunBudget의 "+ config.BodyRadius"를 지워도 초록이었다(리뷰어 돌연변이 확인).
        //  그 항이 반드시 걸리도록 조인다.
        const float Tolerance = 0.005f;

        //  FlappyRaceMap 실측값. 도구가 spec §4.3의 손계산과 같은 답을 내는지가 이 스위트의 목적이다.
        const float StartX = -2f;
        const float FinishX = 632f;

        static FlappyConfig Config(float invulnTime = 0.6f)
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                stunTime: 0.8f, invulnTime: invulnTime,
                                dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f,
                                chaserStartX: -60f, chaserInitialSpeed: 7f,
                                chaserAcceleration: 0.075f, chaserMaxSpeed: 10f);

        [Test]
        public void 허용_정지시간은_추격자와의_간격을_전진속도로_나눈_것이다()
        {
            //  20초 시점: 추격자 = −60 + 7×20 + 0.0375×400 = 95.
            //  새 뒷면이 그 자리에 오려면 −2 + 11×(20−S) − 0.45 = 95  →  S = 20 − 97.45/11 = 11.14초.
            float allowed = StunBudget.AllowedStallSeconds(Config(), 20f, StartX, FinishX);

            //  주석의 손계산이 실제로 쓰는 항이 전부 걸려야 한다 — 특히 "+ 0.45"(몸 반지름)를
            //  빼면 11.18이 나오는데, 허용 오차가 0.05면 그것도 통과해 버린다.
            Assert.AreEqual(11.14f, allowed, Tolerance);
        }

        [Test]
        public void 허용_스턴_횟수는_허용_정지시간을_스턴시간으로_나눈_몫이다()
        {
            //  11.14 ÷ 0.8 = 13.9 → 13번.
            Assert.AreEqual(13, StunBudget.AllowedStuns(Config(), 20f, StartX, FinishX));
        }

        [Test]
        public void 무적_때문에_그_시점까지_맞을_수_있는_횟수에_상한이_있다()
        {
            //  n번 맞으려면 (0.8+0.6)n − 0.6 초가 든다. 9.5초 안에 들어가는 최대 n은 7이다(1.4×7−0.6=9.2 ≤ 9.5 < 10.6=1.4×8−0.6).
            Assert.AreEqual(7, StunBudget.PossibleStuns(Config(), 9.5f));
        }

        [Test]
        public void 무적이_없으면_상한이_달라진다()
        {
            //  이 테스트가 없으면 무적 항이 죽어 있어도 아무도 모른다.
            //  무적 0이면 0.8n ≤ 10 → n = 12.
            Assert.AreEqual(12, StunBudget.PossibleStuns(Config(invulnTime: 0f), 10f));
        }

        [Test]
        public void 가장_빨리_잡히는_경우는_19초_14번째다()
        {
            //  spec §4.3의 손계산. 도구가 이 값을 재현하는지가 첫 검증이다.
            EarliestCatch catchInfo = StunBudget.FindEarliestCatch(Config(), StartX, FinishX);

            Assert.IsTrue(catchInfo.Caught);
            Assert.AreEqual(14, catchInfo.StunCount);
            Assert.AreEqual(19.0f, catchInfo.Seconds, Tolerance);
        }

        [Test]
        public void 추격자가_안_움직이면_영영_안_잡힌다()
        {
            var still = new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                         bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                         stunTime: 0.8f, invulnTime: 0.6f,
                                         dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f,
                                         chaserStartX: -60f, chaserInitialSpeed: 0f,
                                         chaserAcceleration: 0f, chaserMaxSpeed: 0f);

            Assert.IsFalse(StunBudget.FindEarliestCatch(still, StartX, FinishX).Caught);
        }

        [Test]
        public void 곡선은_경과시간마다_허용과_가능을_함께_낸다()
        {
            List<StunBudgetPoint> curve = StunBudget.Curve(Config(), StartX, FinishX, stepSeconds: 10f);

            Assert.AreEqual(10f, curve[0].ElapsedSeconds, Tolerance);
            Assert.AreEqual(108f, curve[0].CleanRunX, 1f);   // −2 + 11×10
            Assert.AreEqual(10, curve[0].AllowedStuns);
            Assert.AreEqual(7, curve[0].PossibleStuns);
        }

        [Test]
        public void 곡선의_마지막_점은_간격에_안_걸려도_골인_시각이다()
        {
            //  간격(10초)은 골인 시각(634 ÷ 11 = 57.64초)에 안 떨어진다 — 그래서 Curve가
            //  골인 점을 따로 덧붙인다. 그 줄을 지우면 곡선이 50초에서 잘려 "마지막 여유가
            //  얼마인지"를 못 읽는다. 옛 단언(LessOrEqual(마지막, 57.7))은 잘린 곡선(50초)도
            //  만족해서 그 삭제를 통째로 놓쳤다.
            const float CleanRunSeconds = (FinishX - StartX) / 11f;
            List<StunBudgetPoint> curve = StunBudget.Curve(Config(), StartX, FinishX, stepSeconds: 10f);
            StunBudgetPoint last = curve[curve.Count - 1];

            Assert.AreEqual(CleanRunSeconds, last.ElapsedSeconds, Tolerance);
            Assert.AreEqual(FinishX, last.CleanRunX, 0.5f);
            //  간격에 걸리는 마지막 점은 50초다 — 골인 점이 그 뒤에 하나 더 붙어야 한다.
            Assert.AreEqual(50f, curve[curve.Count - 2].ElapsedSeconds, Tolerance);
        }

        [Test]
        public void 이미_골인한_뒤의_시각은_잡힌_것으로_치지_않는다()
        {
            //  추격자가 결승선에서 멈춘 뒤에도 스턴 횟수만 늘려 가면, 언젠가는 "정지시간이
            //  허용치를 넘었다"가 성립한다 — 이미 골인한 사람까지 잡힌 것으로 세는 셈이다.
            //  FindEarliestCatch의 조기 반환("그 시각에 이미 골인했으면 볼 것 없다")이 그걸
            //  막는데, 지워도 아무 검사가 빨개지지 않았다(리뷰어 확인).
            //  짧은 코스(19.7m) + 늦게 붙는 추격자: 4번째 스턴이 끝나는 5.0초에 새는 이미
            //  17.8m(≥ 17.7m)로 골인해 있고, 바로 그 시각에 벽이 결승선에 닿는다.
            var lateChaser = new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                              bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                              stunTime: 0.8f, invulnTime: 0.6f,
                                              dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f,
                                              chaserStartX: -5f, chaserInitialSpeed: 0f,
                                              chaserAcceleration: 2f, chaserMaxSpeed: 100f);

            Assert.IsFalse(StunBudget.FindEarliestCatch(lateChaser, StartX, finishX: 17.7f).Caught);
        }
    }
}
