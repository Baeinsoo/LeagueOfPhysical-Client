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
        const float Gravity = 70f;
        const float MaxFallSpeed = 30f;

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
            //  0~10m가 뚫린 넉넉한 틈(아치 4.012보다 한참 넓어 천장 걱정이 없다). 새는 0.5에
            //  있고 세로 속도 −30 — 몇 틱만 굴려도 바닥 마진(0.45) 아래로 떨어진다. 천장이
            //  안전하니 그대로 눌러야 한다.
            var gap = Free(101);
            var decision = BotPilot.Decide(gap, gap, BottomY, Step, currentY: 0.5f, verticalSpeed: -30f,
                                           BodyRadius, flapArc: 4.012f, Gravity, MaxFallSpeed, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 목표_위에_충분히_있으면_안_누른다()
        {
            var gap = Free(15);
            var decision = BotPilot.Decide(gap, gap, BottomY, Step, currentY: 0.9f, verticalSpeed: 0f,
                                           BodyRadius, flapArc: 4.012f, Gravity, MaxFallSpeed, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap);
        }

        [Test]
        public void 지나갈_틈이_없으면_그렇다고_알린다()
        {
            //  자유 구간은 인덱스 1~2뿐이라 실제 폭은 0.1m — 몸 지름(0.9m)보다 한참 좁아
            //  통과할 틈이 아니다.
            var column = Column(true, false, false, true, true);
            var decision = BotPilot.Decide(column, column, BottomY, Step, currentY: 0.2f, verticalSpeed: 0f,
                                           BodyRadius, flapArc: 4.012f, Gravity, MaxFallSpeed, TickSeconds);

            Assert.IsFalse(decision.GapFound);
        }

        [Test]
        public void 틈이_없으면_고도를_지키려_누른다()
        {
            //  판단할 근거가 없을 때 떨어지게 두면 바닥에 부딪힌다 — 통과 가능성을 묻는 검사이므로
            //  아무 정보가 없을 땐 떠 있는 쪽을 고른다.
            var column = Column(true, true, true);
            var decision = BotPilot.Decide(column, column, BottomY, Step,
                                           currentY: 0.2f, verticalSpeed: -30f, BodyRadius, flapArc: 4.012f,
                                           Gravity, MaxFallSpeed, TickSeconds);

            Assert.IsFalse(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 겨냥_높이는_틈이_넓으면_아치가_들어갈_자리로_내려_잡는다()
        {
            //  0~10m가 뚫려 있으면 AimHeight(0, 10, 4.012) = (10−4.012)/2 = 2.994.
            var gap = Free(101);
            var decision = BotPilot.Decide(gap, gap, BottomY, Step, currentY: 5f, verticalSpeed: 0f,
                                           BodyRadius, flapArc: 4.012f, Gravity, MaxFallSpeed, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.AreEqual(2.994f, decision.AimY, 0.01f);
        }

        [Test]
        public void 아치보다_좁은_틈에서는_천장을_뚫느니_누르지_않는다()
        {
            //  Free(15)는 0~1.4m — 아치(4.012)보다 한참 좁다. 바닥 마진(0.45) 아래로 떨어질
            //  참이라 누르고 싶어지지만, 누르면 정점이 0.2+4.012=4.212로 천장(1.4)을 훨씬
            //  넘는다. 바닥으로 처지는 위험을 감수하더라도 천장을 뚫어서는 안 된다 — 뚫으면
            //  그 자리에서 못 지나가지만, 안 누르면 다음 틱에 다시 판단할 여지라도 남는다.
            var gap = Free(15);
            var decision = BotPilot.Decide(gap, gap, BottomY, Step, currentY: 0.2f, verticalSpeed: -30f,
                                           BodyRadius, flapArc: 4.012f, Gravity, MaxFallSpeed, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap);
        }

        [Test]
        public void 한_틱만_보면_늦어_여러_틱_앞을_내다보고_미리_누른다()
        {
            //  0~10m로 넉넉해 천장 걱정은 없다. 한 틱 뒤만 보면 1.0+(−20×0.02)=0.6으로 아직
            //  바닥 마진(0.45) 위처럼 보이지만, 중력이 매 틱 더 세져 실제로는 세 틱 안에
            //  이미 마진 아래(약 −0.37)로 떨어진다. 한 틱만 보는 규칙이면 이 상태에서 아직
            //  안 눌러도 된다고 오판한다 — 여러 틱을 내다봐야 지금 눌러야 한다는 걸 안다.
            var gap = Free(101);
            var decision = BotPilot.Decide(gap, gap, BottomY, Step, currentY: 1.0f, verticalSpeed: -20f,
                                           BodyRadius, flapArc: 4.012f, Gravity, MaxFallSpeed, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 다음_틈이_낮으면_지금_틈의_넓은_천장만_믿고_누르지_않는다()
        {
            //  지금 틈은 0~20m로 넉넉하다. 바닥만 보면(둘 다 바닥이 0이라) 눌러야 할
            //  상황이지만, 바로 다음 틈은 0~3m뿐이다. 지금 눌러 정점(1.0+4.012=5.012)까지
            //  오르면 다음 틈의 천장(3m)을 훨씬 넘긴다. 지금 틈의 넓은 천장(20m)만 믿고
            //  누르면 안 된다 — 다음 틈을 감안해 지금부터 내려가게 둬야 한다.
            var near = Free(201);
            var far = Free(31);
            var decision = BotPilot.Decide(near, far, BottomY, Step, currentY: 1.0f, verticalSpeed: -30f,
                                           BodyRadius, flapArc: 4.012f, Gravity, MaxFallSpeed, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap);
        }
    }
}
