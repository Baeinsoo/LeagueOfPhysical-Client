using System;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class BotPilotTests
    {
        const float BodyRadius = 0.45f;
        const float Step = 0.1f;
        const float BottomY = 0f;
        const float TickSeconds = 0.02f;

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
            //  닫힌 식 impulse²/(2·gravity) = 23²/140 ≈ 3.7786과 다르다 — 그 식은 연속시간
            //  적분이고, 여기는 이산 틱을 실제로 밟아 더하므로 구현이 틀리면 이 테스트가 잡아낸다.
            Assert.AreEqual(4.012f, BotPilot.FlapArc(flapImpulse: 23f, gravity: 70f, tickSeconds: 0.02f), 0.005f);
        }

        [Test]
        public void 중력이_0_이하면_예외를_던진다()
        {
            //  gravity <= 0이면 speed가 절대 0 밑으로 안 내려가 무한 루프가 된다 — 조용히 걸리는
            //  대신 바로 던진다.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BotPilot.FlapArc(flapImpulse: 23f, gravity: 0f, tickSeconds: 0.02f));
        }

        [Test]
        public void 틱_길이가_0_이하면_예외를_던진다()
        {
            //  tickSeconds <= 0도 같은 무한 루프다 — speed가 안 줄거나(0) 반대로 자란다(음수).
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BotPilot.FlapArc(flapImpulse: 23f, gravity: 70f, tickSeconds: 0f));
        }

        [Test]
        public void 목표보다_아래로_떨어질_참이면_누른다()
        {
            //  Free(15)는 0~1.4m가 뚫린 기둥이다. 새는 0.5에 있고 세로 속도 −30, 틱 0.02초라
            //  한 틱 뒤엔 0.5 + (−30 × 0.02) = −0.1로 내려갈 참이다.
            //  겨냥 높이는 AimHeight(0, 1.4, 4.012) = 0(틈이 아치보다 좁으니 바닥에 붙인다).
            //  현재 높이 0.5는 아직 0보다 위지만, 한 틱 뒤 −0.1은 이미 아래다 — 규칙은 "한 틱 뒤"로
            //  본다. 뚫린 폭 1.4m는 최소 통과 폭(지름 0.9)보다 넉넉히 넓어 경계에 걸리지 않는다.
            var decision = BotPilot.Decide(Free(15), BottomY, Step, currentY: 0.5f, verticalSpeed: -30f,
                                           BodyRadius, flapArc: 4.012f, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 목표_위에_충분히_있으면_안_누른다()
        {
            var decision = BotPilot.Decide(Free(15), BottomY, Step, currentY: 0.9f, verticalSpeed: 0f,
                                           BodyRadius, flapArc: 4.012f, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap);
        }

        [Test]
        public void 지나갈_틈이_없으면_그렇다고_알린다()
        {
            //  자유 구간은 인덱스 1~2뿐이라 실제 폭은 0.1m — 몸 지름(0.9m)보다 한참 좁아
            //  통과할 틈이 아니다.
            var decision = BotPilot.Decide(Column(true, false, false, true, true),
                                           BottomY, Step, currentY: 0.2f, verticalSpeed: 0f,
                                           BodyRadius, flapArc: 4.012f, TickSeconds);

            Assert.IsFalse(decision.GapFound);
        }

        [Test]
        public void 틈이_없으면_고도를_지키려_누른다()
        {
            //  판단할 근거가 없을 때 떨어지게 두면 바닥에 부딪힌다 — 통과 가능성을 묻는 검사이므로
            //  아무 정보가 없을 땐 떠 있는 쪽을 고른다.
            var decision = BotPilot.Decide(Column(true, true, true), BottomY, Step,
                                           currentY: 0.2f, verticalSpeed: -30f, BodyRadius, flapArc: 4.012f,
                                           TickSeconds);

            Assert.IsFalse(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 겨냥_높이는_틈이_넓으면_아치가_들어갈_자리로_내려_잡는다()
        {
            //  0~10m가 뚫려 있으면 AimHeight(0, 10, 4.012) = (10−4.012)/2 = 2.994.
            var decision = BotPilot.Decide(Free(101), BottomY, Step, currentY: 5f, verticalSpeed: 0f,
                                           BodyRadius, flapArc: 4.012f, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.AreEqual(2.994f, decision.AimY, 0.01f);
        }
    }
}
