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
        const float FlapImpulse = 23f;
        //  실제 도구(FlappyMapPlayabilityCheck)가 쓰는 것과 같은 값 — 0.20초/0.40초 앞을
        //  본다(초당 틱수 1/0.02=50 기준 10틱/20틱). 20틱은 날갯짓의 자연 정점(17틱)보다
        //  넉넉히 커서, 그 열에 도달할 때의 상승분이 아치 전체로 자연히 수렴한다.
        const int TicksToNear = 10;
        const int TicksToFar = 20;
        //  정점 열까지 남은 틱 — 자연 정점 틱수와 같다(23÷70÷0.02초 기준 17틱, ceil(23/1.4)).
        //  FlappyMapPlayabilityCheck.FlyBot도 같은 식(Mathf.CeilToInt(FlapImpulse/(Gravity*TickSeconds)))으로
        //  구한다 — 숫자 17을 손으로 박는 게 아니라 그 식의 결과다.
        const int TicksToApex = 17;

        //  아래에서 위로 0.1m 간격. true = 막힘.
        static bool[] Column(params bool[] cells) => cells;

        //  아래 n칸이 뚫리고 그 위가 막힌 기둥. 폭은 (n−1) × 0.1m 다.
        static bool[] Free(int cells)
        {
            var column = new bool[cells + 1];
            column[cells] = true;
            return column;
        }

        //  임의 간격으로 [low, low+span]만 뚫린 기둥. 문턱값을 정밀하게 맞춰야 하는
        //  테스트(threshold law)에서 0.1m 눈금보다 촘촘한 폭이 필요할 때 쓴다.
        static bool[] Band(float low, float span, float bottomY, float step)
        {
            int lowIndex = (int)Math.Round((low - bottomY) / step);
            //  TryFindGap이 재는 폭은 (칸수−1)×step이다(Free()와 같은 관례 — 마지막 자유
            //  칸의 "끝"이 아니라 "시작"을 재기 때문). span을 그대로 재려면 칸수를 하나 더
            //  얹어야 (칸수−1)×step ≈ span이 된다.
            int cells = (int)Math.Round(span / step) + 1;
            var column = new bool[lowIndex + cells + 1];
            for (int i = 0; i < lowIndex; i++)
            {
                column[i] = true;
            }
            column[lowIndex + cells] = true;
            return column;
        }

        //  실제 게임 커널(FlappyMapPlayabilityCheck.Step)과 같은 순서로 한 틱 굴린다 —
        //  중력을 먼저 깎고, 날갯짓이면 그 값을 덮어쓴 뒤(그 틱은 감쇠가 적용 안 됨), 그
        //  속도로 움직인다. 여러 틱을 실제로 밟는 테스트(불변식·통과)가 봇의 판단만이 아니라
        //  "그 판단대로 움직이면 실제로 어떻게 되는가"까지 검증하려면 이 순서를 그대로 써야 한다.
        static (float y, float vy) StepPhysics(float y, float vy, bool flap)
        {
            vy -= Gravity * TickSeconds;
            if (vy < -MaxFallSpeed)
            {
                vy = -MaxFallSpeed;
            }
            if (flap)
            {
                vy = FlapImpulse;
            }
            y += vy * TickSeconds;
            return (y, vy);
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
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BotPilot.FlapArc(flapImpulse: 23f, gravity: 0f, tickSeconds: 0.02f));
        }

        [Test]
        public void 틱_길이가_0_이하면_예외를_던진다()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BotPilot.FlapArc(flapImpulse: 23f, gravity: 70f, tickSeconds: 0f));
        }

        [Test]
        public void 지금_누르면_n틱_뒤_도달하는_높이는_그때까지만_오른_값이다()
        {
            //  FlapArc와 같은 순서(현재 속도로 먼저 오르고 그다음 깎는다)로 10틱만 도는 값 —
            //  23,21.6,…,10.4(10개 항)를 0.02씩 곱해 더하면 3.34m. 자연 정점(4.012, 17틱)보다
            //  작아야 한다 — 아직 정점에 도달하기 전이라서다.
            float rise = BotPilot.FlapRiseAfter(FlapImpulse, Gravity, TickSeconds, ticks: 10);
            Assert.AreEqual(3.34f, rise, 0.005f);
            Assert.Less(rise, BotPilot.FlapArc(FlapImpulse, Gravity, TickSeconds));
        }

        [Test]
        public void 바닥_판단은_근거리_열까지_남은_틱수를_그대로_써야_한다()
        {
            //  2.0m에서 −5로 서서히 떨어지는 참. 3틱만 내다보면(중력이 아직 많이 안 세져)
            //  1.532m로 아직 바닥 마진(0.45) 위처럼 보이지만, 근거리 열까지 남은 진짜
            //  틱수(10)로 내다보면 −0.54로 이미 마진 아래다. 근거리 열 스캔 거리(0.20초=10틱)와
            //  다른 상수(예: 3)를 쓰면 이 상태에서 아직 안 눌러도 된다고 오판한다 —
            //  "스캔 거리가 고정이니 상수도 고정"이라는 근거는 그 상수가 스캔 거리에서
            //  그대로 나올 때만 성립한다.
            var gap = Free(101); // 0~10m — 천장 걱정 없음
            var decision = BotPilot.Decide(gap, gap, gap, BottomY, Step, currentY: 2.0f, verticalSpeed: -5f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed,
                                           TicksToNear, TicksToApex, TicksToFar, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 목표보다_아래로_떨어질_참이면_누른다()
        {
            //  0~10m가 뚫린 넉넉한 틈(아치보다 한참 넓어 천장 걱정이 없다). 새는 0.5에 있고
            //  세로 속도 −30 — 근거리 열(10틱)까지 굴려도 바닥 마진(0.45) 아래로 떨어진다.
            var gap = Free(101);
            var decision = BotPilot.Decide(gap, gap, gap, BottomY, Step, currentY: 0.5f, verticalSpeed: -30f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed,
                                           TicksToNear, TicksToApex, TicksToFar, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 목표_위에_충분히_있으면_안_누른다()
        {
            //  Free(15)는 0~1.4m — 아치보다 좁다. 지금 눌러 근거리 열(10틱) 상승분(3.34m)만
            //  더해도 0.9+3.34=4.24로 천장(1.4)을 넘는다 — 아직 위험하지 않은데도 누르면
            //  스스로 천장을 뚫는 쪽이라 누르지 않는다.
            var gap = Free(15);
            var decision = BotPilot.Decide(gap, gap, gap, BottomY, Step, currentY: 0.9f, verticalSpeed: 0f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed,
                                           TicksToNear, TicksToApex, TicksToFar, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap);
        }

        [Test]
        public void 몸이_다_못_들어가는_좁은_자리도_천장_가드를_적용받는다()
        {
            //  자유 구간은 인덱스 1~2뿐이라 실제 폭은 0.1m — 몸 지름(0.9m)보다 한참 좁아 몸이
            //  통째로 들어갈 "틈"은 아니다. 그렇다고 근거 없이 무조건 날갯짓하지 않는다 —
            //  반지름 조건을 0으로 풀어 그 좁은 자리라도 찾아내고, 거기에도 같은 천장 가드를
            //  건다. 지금 눌러 10틱 뒤 상승분(3.34m)을 더하면 그 좁은 자리의 천장(0.2)을
            //  훨씬 넘으므로 누르지 않는다 — 옛 버그(가드 없이 무조건 날갯짓)라면 여기서 눌러
            //  버렸을 것이다.
            var column = Column(true, false, false, true, true);
            var decision = BotPilot.Decide(column, column, column, BottomY, Step, currentY: 0.15f, verticalSpeed: 0f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed,
                                           TicksToNear, TicksToApex, TicksToFar, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap);
        }

        [Test]
        public void 틈이_없으면_고도를_지키려_누른다()
        {
            //  판단할 근거가 없을 때 떨어지게 두면 바닥에 부딪힌다 — 통과 가능성을 묻는 검사이므로
            //  아무 정보가 없을 땐 떠 있는 쪽을 고른다. 반지름을 0으로 풀어도 뚫린 자리가
            //  아예 없다(전부 막힘).
            var column = Column(true, true, true);
            var decision = BotPilot.Decide(column, column, column, BottomY, Step,
                                           currentY: 0.2f, verticalSpeed: -30f, BodyRadius,
                                           FlapImpulse, Gravity, MaxFallSpeed,
                                           TicksToNear, TicksToApex, TicksToFar, TickSeconds);

            Assert.IsFalse(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 근거리_열은_정점이_아니라_그_열에_도달하는_시점의_높이로_천장을_잰다()
        {
            //  근거리 틈은 0~3.7m — 10틱 뒤(도달 시점) 상승분(3.34m)은 들어가지만, 자연
            //  정점까지 다 오른 값(4.012m)은 못 들어간다. "정점"이 아니라 "그 열에 도달하는
            //  시점"으로 재야 이 열을 지나 눌러도 된다는 걸 안다 — 정점은 이 열을 이미 지나친
            //  자리(정점·원거리 열 쪽)에서 일어난다. 정점·원거리 열은 0~20m로 넉넉해 방해하지 않는다.
            var near = Band(low: 0f, span: 3.7f, BottomY, Step);
            var far = Free(201);
            var decision = BotPilot.Decide(near, far, far, BottomY, Step, currentY: 0f, verticalSpeed: 0f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed,
                                           TicksToNear, TicksToApex, TicksToFar, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 문턱_폭_이상이면_누른다()
        {
            //  "문턱 법칙" — 밴드 바닥에서 잰 자유 폭이 아치+마진 이상이면 누르고, 그보다
            //  조금이라도 좁으면 안 누른다. 원거리 열(20틱)이 자연 정점(17틱)을 넘겨 아치
            //  전체로 수렴하므로 그 값이 기준이 되고, 마진은 바닥 쪽(반지름)만 붙는다(천장
            //  쪽은 막힘 표 자체가 이미 몸 반지름을 반영해 마진을 또 두지 않는다).
            float flapArc = BotPilot.FlapArc(FlapImpulse, Gravity, TickSeconds);
            float threshold = BodyRadius + flapArc;
            const float fineStep = 0.001f;
            //  부동소수 나눗셈이 눈금 경계에서 반올림 방향을 뒤집을 수 있어(threshold를
            //  fineStep으로 나눈 값이 근소하게 .5 아래로 떨어지는 경우 등), 눈금 한 칸만큼
            //  여유를 얹어 "이상" 쪽에 확실히 서게 한다 — 문턱 자체(threshold)는 그대로다.
            var gap = Band(low: 0f, span: threshold + fineStep, BottomY, fineStep);

            //  바닥 마진 경계(0+반지름)에 정확히 서서 강하게 떨어지는 참 — 근거리 열까지
            //  굴려도 확실히 마진 아래로 떨어져 "누를지 말지"는 오직 천장 여유로만 갈린다.
            var decision = BotPilot.Decide(gap, gap, gap, BottomY, fineStep, currentY: BodyRadius,
                                           verticalSpeed: -30f, BodyRadius, FlapImpulse, Gravity, MaxFallSpeed,
                                           TicksToNear, TicksToApex, TicksToFar, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 문턱_폭보다_조금이라도_좁으면_안_누른다()
        {
            //  위 테스트와 정확히 같은 자리, 폭만 2cm 모자라다 — 경계 양쪽을 다 확인해야
            //  마진의 대가(폭을 얼마나 깎아 먹는지)가 누구든 상수를 건드리는 순간 바로 보인다.
            float flapArc = BotPilot.FlapArc(FlapImpulse, Gravity, TickSeconds);
            float threshold = BodyRadius + flapArc;
            const float fineStep = 0.001f;
            var gap = Band(low: 0f, span: threshold - 0.02f, BottomY, fineStep);

            var decision = BotPilot.Decide(gap, gap, gap, BottomY, fineStep, currentY: BodyRadius,
                                           verticalSpeed: -30f, BodyRadius, FlapImpulse, Gravity, MaxFallSpeed,
                                           TicksToNear, TicksToApex, TicksToFar, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap);
        }

        [Test]
        public void 여유_있는_틈에서는_여러_틱을_굴려도_양쪽_경계를_안_넘는다()
        {
            //  0~6m(문턱 4.462m보다 넉넉히 넓다)에서 봇의 판단대로 실제 커널 순서로 80틱을
            //  굴린다 — 단발 호출이 아니라 판단→적분→판단을 반복해도 바닥·천장 둘 다 어긴
            //  적이 없어야 한다("가둬 둘 수 있다"는 성질을 커널 적분으로 확인).
            var gap = Free(61); // 0~6m
            float y = 3f, vy = 0f;

            for (int tick = 0; tick < 80; tick++)
            {
                var decision = BotPilot.Decide(gap, gap, gap, BottomY, Step, y, vy, BodyRadius,
                                               FlapImpulse, Gravity, MaxFallSpeed,
                                               TicksToNear, TicksToApex, TicksToFar, TickSeconds);
                (y, vy) = StepPhysics(y, vy, decision.Flap);

                Assert.GreaterOrEqual(y, 0f, $"tick {tick}: 바닥 아래로 내려갔다 (y={y})");
                Assert.LessOrEqual(y, 6f, $"tick {tick}: 천장을 넘었다 (y={y})");
            }
        }

        [Test]
        public void 통과_성질_봇이_실제로_날갯짓해야만_회랑_안에_머문다()
        {
            //  퇴화한 옛 버전 — 날갯짓 0회·순수 자유낙하 산술만으로도 통과 조건(하강 중 +
            //  다음 틈 안)을 우연히 만족해, 아무것도 안 누르는 봇도 초록이었다("초록인데
            //  아무것도 안 지키는 테스트"). 넉넉한 회랑(6m, 문턱 4.462m보다 넓다)에서 바닥
            //  근처(0.5m)·강하 속도(-30, 이미 종단속도)로 출발시켜, 실제로 날갯짓하지
            //  않으면 곧바로 바닥을 뚫는 상황을 만든다.
            //
            //  돌연변이 검증(2026-09-09): BotPilot.Decide가 항상 flap:false를 내도록
            //  일부러 깨뜨려 실행 → 이 테스트가 빨강이 됨을 확인했다(바닥 아래로 내려갔다는
            //  Assert.GreaterOrEqual 실패). 확인 후 원복.
            var gap = Free(61); // 0~6m
            float y = 0.5f, vy = -30f;
            int flaps = 0;

            for (int tick = 0; tick < 150; tick++)
            {
                var decision = BotPilot.Decide(gap, gap, gap, BottomY, Step, y, vy, BodyRadius,
                                               FlapImpulse, Gravity, MaxFallSpeed,
                                               TicksToNear, TicksToApex, TicksToFar, TickSeconds);
                if (decision.Flap)
                {
                    flaps++;
                }
                (y, vy) = StepPhysics(y, vy, decision.Flap);

                Assert.GreaterOrEqual(y, 0f, $"tick {tick}: 바닥 아래로 내려갔다 (y={y})");
                Assert.LessOrEqual(y, 6f, $"tick {tick}: 천장을 넘었다 (y={y})");
            }

            Assert.Greater(flaps, 0,
                "150틱 동안 한 번도 날갯짓하지 않았다 — 순수 자유낙하로도 통과했다면 이 테스트는 아무것도 지키지 않는다.");
        }

        [Test]
        public void 정점_열만_낮은_천장이면_근거리_원거리가_뚫려도_누르지_않는다()
        {
            //  근거리·원거리는 0~10m로 넓게 뚫려 있고, 정점 열만 낮은 천장(0~2m)이다. 정점
            //  열이 없던 시절이면 근거리·원거리 가드만 보고 그대로 눌렀을 상황(목표보다
            //  아래로 떨어질 참이면 누른다와 같은 currentY/verticalSpeed) — 정점에서 아치가
            //  최고점(4.012m)에 이르러 0.5+4.012=4.512로 이 낮은 천장(2m)을 뚫는다.
            var open = Free(101);       // 0~10m
            var lowCeiling = Free(21);  // 0~2m
            var decision = BotPilot.Decide(open, lowCeiling, open, BottomY, Step,
                                           currentY: 0.5f, verticalSpeed: -30f, BodyRadius,
                                           FlapImpulse, Gravity, MaxFallSpeed,
                                           TicksToNear, TicksToApex, TicksToFar, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap,
                "정점 열의 낮은 천장을 무시하고 눌렀다 — 근거리·원거리 사이에 숨은 낮은 천장을 못 본다.");
        }

        [Test]
        public void 정점_천장을_충분히_높이면_누른다()
        {
            //  위 테스트와 같은 상황(같은 currentY/verticalSpeed)이지만 정점 열 천장도
            //  0~10m로 충분히 높인다 — 가드가 무조건 막는 게 아니라, 아치가 실제로 안전할
            //  때는 그대로 누른다는 짝 테스트.
            var open = Free(101); // 0~10m
            var decision = BotPilot.Decide(open, open, open, BottomY, Step,
                                           currentY: 0.5f, verticalSpeed: -30f, BodyRadius,
                                           FlapImpulse, Gravity, MaxFallSpeed,
                                           TicksToNear, TicksToApex, TicksToFar, TickSeconds);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }
    }
}
