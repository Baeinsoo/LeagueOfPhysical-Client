using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class BotPilotTests
    {
        const float BodyRadius = 0.45f;
        const float Step = 0.1f;
        const float BottomY = 0f;

        //  아래에서 위로 0.1m 간격. true = 막힘.
        static bool[] Column(params bool[] cells) => cells;

        //  아래 n칸이 뚫리고 그 위가 막힌 기둥. 폭은 (n−1) × 0.1m 다.
        static bool[] Free(int cells)
        {
            var column = new bool[cells + 1];
            column[cells] = true;
            return column;
        }

        [Test]
        public void 날갯짓_한_번의_상승_폭은_속도가_0이_될_때까지_더한_값이다()
        {
            //  23, 21.6, 20.2 … 를 0.02씩 곱해 더하면 4.012m (17틱).
            Assert.AreEqual(4.012f, BotPilot.FlapArc(flapImpulse: 23f, gravity: 70f, tickSeconds: 0.02f), 0.005f);
        }

        [Test]
        public void 목표보다_아래로_떨어질_참이면_누른다()
        {
            //  0.0~1.0이 뚫려 있고 새는 0.5에 있는데 세로 속도가 −10이면 다음 틱에 0.3으로 떨어진다.
            //  겨냥 높이는 AimHeight(0, 1.0, 4.012) = 0(틈이 아치보다 좁으니 바닥에 붙인다).
            //  0.3은 아직 0보다 위지만, 그 다음 틱을 기다리면 이미 늦다 — 규칙은 "한 틱 뒤"로 본다.
            //  뚫린 폭을 1.4m로 잡는다. 최소 통과 폭(지름 0.9)과 정확히 같으면 부동소수 경계에 걸린다.
            var decision = BotPilot.Decide(Free(15), BottomY, Step, currentY: 0.5f, verticalSpeed: -30f,
                                           BodyRadius, flapArc: 4.012f);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 목표_위에_충분히_있으면_안_누른다()
        {
            var decision = BotPilot.Decide(Free(15), BottomY, Step, currentY: 0.9f, verticalSpeed: 0f,
                                           BodyRadius, flapArc: 4.012f);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap);
        }

        [Test]
        public void 지나갈_틈이_없으면_그렇다고_알린다()
        {
            //  몸이 0.9m(지름)인데 뚫린 곳이 0.2m뿐이면 통과할 틈이 아니다.
            var decision = BotPilot.Decide(Column(true, false, false, true, true),
                                           BottomY, Step, currentY: 0.2f, verticalSpeed: 0f,
                                           BodyRadius, flapArc: 4.012f);

            Assert.IsFalse(decision.GapFound);
        }

        [Test]
        public void 틈이_없으면_고도를_지키려_누른다()
        {
            //  판단할 근거가 없을 때 떨어지게 두면 바닥에 부딪힌다 — 통과 가능성을 묻는 검사이므로
            //  아무 정보가 없을 땐 떠 있는 쪽을 고른다.
            var decision = BotPilot.Decide(Column(true, true, true), BottomY, Step,
                                           currentY: 0.2f, verticalSpeed: -30f, BodyRadius, flapArc: 4.012f);

            Assert.IsFalse(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 겨냥_높이는_틈이_넓으면_아치가_들어갈_자리로_내려_잡는다()
        {
            //  0~10m가 뚫려 있으면 AimHeight(0, 10, 4.012) = (10−4.012)/2 = 2.994.
            var decision = BotPilot.Decide(Free(101), BottomY, Step, currentY: 5f, verticalSpeed: 0f,
                                           BodyRadius, flapArc: 4.012f);

            Assert.IsTrue(decision.GapFound);
            Assert.AreEqual(2.994f, decision.AimY, 0.01f);
        }
    }
}
