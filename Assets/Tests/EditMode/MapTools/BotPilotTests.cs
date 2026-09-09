using System;
using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class BotPilotTests
    {
        const float BodyRadius = 0.45f;
        const float BodyHeight = 0.9f;
        const float Step = 0.1f;
        const float BottomY = 0f;
        const float TickSeconds = 0.02f;
        const float Gravity = 70f;
        const float MaxFallSpeed = 30f;
        const float FlapImpulse = 23f;
        const float ForwardSpeed = 11f;
        //  실제 도구(FlappyMapPlayabilityCheck)가 쓰는 것과 같은 값 — 근거리 열은 0.20초
        //  앞(초당 틱수 1/0.02=50 기준 10틱)을 본다. 아치 훑기에는 지평 인자가 없다 —
        //  세로 속도가 0이 되는 자리(정점, 17틱)에서 물리가 알아서 멈춘다.
        const int TicksToNear = 10;
        //  근거리 열을 재는 x — 새의 자리에서 0.20초 앞(11 × 0.20 = 2.2m).
        const float NearScanX = 2.2f;
        //  상자를 "코스 끝까지"로 늘릴 때 쓰는 큰 값. 실제 좌표는 이보다 한참 작다.
        const float FarAway = 1000f;

        //  아래에서 위로 0.1m 간격. true = 막힘.
        static bool[] Column(params bool[] cells) => cells;

        //  Free/Band로 만든 표는 이 높이까지 전부 채운다(뚫린 구간 빼고는 막힘) — 뚫린 구간
        //  바로 위에 벽 하나만 세우고 표를 끝내면, 표 밖 높이를 묻는 질의가 헬퍼마다 다르게
        //  굴러 테스트가 재현하려던 상황이 아니게 된다. 실제 FlyBot이 만드는 표는 탐색 밴드
        //  전체를 덮으므로 이 문제가 없다 — 손으로 짧게 만드는 표에서만 생기는 인공물이다.
        const float TableTopHeight = 100f;

        static int TableCells(float step) => (int)Math.Round(TableTopHeight / step) + 1;

        //  아래 n칸이 뚫리고 그 위(표 끝까지)가 막힌 기둥. 폭은 (n−1) × 0.1m 다.
        static bool[] Free(int cells)
        {
            var column = new bool[TableCells(Step)];
            for (int i = cells; i < column.Length; i++)
            {
                column[i] = true;
            }
            return column;
        }

        //  임의 간격으로 [low, low+span]만 뚫리고 그 아래·위(표 끝까지)는 막힌 기둥. 문턱값을
        //  정밀하게 맞춰야 하는 테스트(threshold law)에서 0.1m 눈금보다 촘촘한 폭이 필요할 때 쓴다.
        static bool[] Band(float low, float span, float bottomY, float step)
        {
            int lowIndex = (int)Math.Round((low - bottomY) / step);
            //  TryFindGap이 재는 폭은 (칸수−1)×step이다(Free()와 같은 관례 — 마지막 자유
            //  칸의 "끝"이 아니라 "시작"을 재기 때문). span을 그대로 재려면 칸수를 하나 더
            //  얹어야 (칸수−1)×step ≈ span이 된다.
            int cells = (int)Math.Round(span / step) + 1;
            int total = Math.Max(TableCells(step), lowIndex + cells + 1);
            var column = new bool[total];
            for (int i = 0; i < lowIndex; i++)
            {
                column[i] = true;
            }
            for (int i = lowIndex + cells; i < total; i++)
            {
                column[i] = true;
            }
            return column;
        }

        //  실제 도구(FlappyMapPlayabilityCheck.FlyBot)가 하는 그대로 — 같은 프로브를 그 열의
        //  x에서 눈금 간격으로 찍어 근거리 열을 만든다. 지형과 표가 따로 놀지 않게 한다.
        static bool[] ColumnFrom(FreeSpaceProbe isFree, float scanX, int cells)
        {
            var column = new bool[cells];
            for (int i = 0; i < cells; i++)
            {
                column[i] = isFree(scanX, BottomY + i * Step) == false;
            }
            return column;
        }

        //  막힌 상자들의 목록으로 자유공간 프로브를 만든다. 상자 하나 = (xMin, xMax, yMin, yMax).
        //  몸을 세는 방식은 실제 도구(FlappyMapPlayabilityCheck.FreeSpaceGrid)와 같다 — 자리
        //  (x, y)는 발밑이고 몸은 그 위로 선 캡슐(아래 구 중심 y+반지름, 위 구 중심 y+높이−반지름)이다.
        //  캡슐이 상자에 닿으면 막힘. 그래서 두께 t짜리 판 하나는 발 높이로는 t+0.9m 구간을 막는다.
        static FreeSpaceProbe Blocks(params (float xMin, float xMax, float yMin, float yMax)[] boxes)
        {
            return (x, y) =>
            {
                float lower = y + BodyRadius;
                float upper = y + BodyHeight - BodyRadius;
                foreach (var box in boxes)
                {
                    float dx = Math.Max(Math.Max(box.xMin - x, x - box.xMax), 0f);
                    float dy = Math.Max(Math.Max(box.yMin - upper, lower - box.yMax), 0f);
                    if (dx * dx + dy * dy < BodyRadius * BodyRadius)
                    {
                        return false;
                    }
                }
                return true;
            };
        }

        //  지형이 아예 없는 하늘. 천장 걱정이 없으니 판단은 바닥 규칙만으로 갈린다.
        static FreeSpaceProbe OpenSky() => Blocks();

        //  발 높이 freeTop까지만 뚫린 회랑(코스 전체 길이에 걸쳐). 상자 바닥을 freeTop+몸높이에
        //  두어야 "발이 freeTop을 넘는 순간 막힘"이 된다 — 몸이 그 위로 서기 때문이다.
        static FreeSpaceProbe Ceiling(float freeTop)
            => Blocks((-FarAway, FarAway, freeTop + BodyHeight, FarAway));

        //  발 높이 [low, high]만 뚫린 회랑.
        static FreeSpaceProbe Corridor(float low, float high)
            => Blocks((-FarAway, FarAway, -FarAway, low),
                      (-FarAway, FarAway, high + BodyHeight, FarAway));

        //  어디에도 몸이 못 들어가는 바위 덩어리.
        static FreeSpaceProbe Solid() => Blocks((-FarAway, FarAway, -FarAway, FarAway));

        //  어디를 물어도 "뚫렸다"고 답하면서, 물어본 자리를 순서대로 적어 두는 프로브. 훑기
        //  루프 안을 테스트가 직접 볼 수 없으므로 "어디를 물었나"로 아치를 되짚는다.
        static FreeSpaceProbe Recording(List<(float x, float y)> log)
            => (x, y) => { log.Add((x, y)); return true; };

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
            //  작아야 한다 — 아직 정점에 도달하기 전이라서다. 천장 가드의 훑기 루프도 이
            //  순서(누른 틱은 임펄스 그대로, 그다음부터 중력)를 그대로 쓴다.
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
            var decision = BotPilot.Decide(gap, BottomY, Step, currentX: 0f, currentY: 2.0f, verticalSpeed: -5f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, OpenSky());

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 목표보다_아래로_떨어질_참이면_누른다()
        {
            //  천장이 없는 하늘. 새는 0.5에 있고 세로 속도 −30 — 근거리 열(10틱)까지 굴려도
            //  바닥 마진(0.45) 아래로 떨어진다.
            var gap = Free(101);
            var decision = BotPilot.Decide(gap, BottomY, Step, currentX: 0f, currentY: 0.5f, verticalSpeed: -30f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, OpenSky());

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 목표_위에_충분히_있으면_안_누른다()
        {
            //  회랑이 1.4m까지만 뚫려 있다 — 아치(4.012)보다 한참 좁다. 지금 누르면 두 틱 만에
            //  1.792로 천장을 뚫는다 — 아직 위험하지 않은데도 누르면 스스로 박는 쪽이라 누르지 않는다.
            var gap = Free(15);
            var decision = BotPilot.Decide(gap, BottomY, Step, currentX: 0f, currentY: 0.9f, verticalSpeed: 0f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, Ceiling(1.4f));

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap);
        }

        [Test]
        public void 몸이_다_못_들어가는_좁은_자리도_천장_가드를_적용받는다()
        {
            //  자유 구간은 0.1~0.2m뿐이라 실제 폭은 0.1m — 몸 지름(0.9m)보다 한참 좁아 몸이
            //  통째로 들어갈 "틈"은 아니다. 그렇다고 근거 없이 무조건 날갯짓하지 않는다 —
            //  반지름 조건을 0으로 풀어 그 좁은 자리라도 찾아내고, 거기에도 같은 천장 가드를
            //  건다. 한 틱만 눌러도 0.61로 그 자리를 벗어나 박으므로 누르지 않는다 —
            //  옛 버그(가드 없이 무조건 날갯짓)라면 여기서 눌러 버렸을 것이다.
            var column = Band(low: 0.1f, span: 0.1f, BottomY, Step);
            var decision = BotPilot.Decide(column, BottomY, Step, currentX: 0f, currentY: 0.15f, verticalSpeed: 0f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, Corridor(0.1f, 0.2f));

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap);
        }

        [Test]
        public void 틈이_없으면_고도를_지키려_누른다()
        {
            //  판단할 근거가 없을 때 떨어지게 두면 바닥에 부딪힌다 — 통과 가능성을 묻는 검사이므로
            //  아무 정보가 없을 땐 떠 있는 쪽을 고른다. 반지름을 0으로 풀어도 뚫린 자리가
            //  아예 없다(전부 막힘). 이 결정은 천장 가드보다 앞서야 한다 — 프로브도 전부
            //  막힌 바위로 줬으므로, 가드를 먼저 걸면 이 테스트가 빨강이 된다.
            var column = Column(true, true, true);
            var decision = BotPilot.Decide(column, BottomY, Step, currentX: 0f, currentY: 0.2f, verticalSpeed: -30f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, Solid());

            Assert.IsFalse(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 앞쪽_천장이_낮아도_그_아래로_지나갈_수_있으면_누른다()
        {
            //  천장이 3.7m로 낮지만 2.2m 앞에서 끝난다. 지금 누르면 그 구간을 3.34m까지만
            //  오른 채로 빠져나가고(10틱), 천장이 끝난 뒤에야 정점(4.012)까지 오른다 — 실제로
            //  지나갈 수 있으므로 눌러야 한다. 아치를 훑지 않고 "정점 높이가 앞 천장 아래인가"만
            //  물으면 여기서 겁을 먹는다.
            var isFree = Blocks((-FarAway, 2.2f, 3.7f + BodyHeight, FarAway));
            var near = ColumnFrom(isFree, NearScanX, cells: 101);
            var decision = BotPilot.Decide(near, BottomY, Step, currentX: 0f, currentY: 0f, verticalSpeed: 0f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, isFree);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 문턱_폭_이상이면_누른다()
        {
            //  "문턱 법칙" — 밴드 바닥에서 잰 자유 폭이 아치+마진 이상이면 누르고, 그보다
            //  조금이라도 좁으면 안 누른다. 훑기가 정점(17틱)을 지나므로 아치 전체가 기준이
            //  되고, 마진은 바닥 쪽(반지름)만 붙는다(천장 쪽은 몸이 지나는 자리를 프로브가
            //  직접 재므로 마진을 또 두지 않는다).
            float flapArc = BotPilot.FlapArc(FlapImpulse, Gravity, TickSeconds);
            float threshold = BodyRadius + flapArc;
            const float fineStep = 0.001f;
            //  부동소수 나눗셈이 눈금 경계에서 반올림 방향을 뒤집을 수 있어(threshold를
            //  fineStep으로 나눈 값이 근소하게 .5 아래로 떨어지는 경우 등), 눈금 한 칸만큼
            //  여유를 얹어 "이상" 쪽에 확실히 서게 한다 — 문턱 자체(threshold)는 그대로다.
            var gap = Band(low: 0f, span: threshold + fineStep, BottomY, fineStep);

            //  바닥 마진 경계(0+반지름)에 정확히 서서 강하게 떨어지는 참 — 근거리 열까지
            //  굴려도 확실히 마진 아래로 떨어져 "누를지 말지"는 오직 천장 여유로만 갈린다.
            var decision = BotPilot.Decide(gap, BottomY, fineStep, currentX: 0f, currentY: BodyRadius,
                                           verticalSpeed: -30f, BodyRadius, FlapImpulse, Gravity, MaxFallSpeed,
                                           ForwardSpeed, TicksToNear, TickSeconds,
                                           Ceiling(threshold + fineStep));

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

            var decision = BotPilot.Decide(gap, BottomY, fineStep, currentX: 0f, currentY: BodyRadius,
                                           verticalSpeed: -30f, BodyRadius, FlapImpulse, Gravity, MaxFallSpeed,
                                           ForwardSpeed, TicksToNear, TickSeconds,
                                           Ceiling(threshold - 0.02f));

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
            var isFree = Ceiling(6f);
            float y = 3f, vy = 0f;

            for (int tick = 0; tick < 80; tick++)
            {
                var decision = BotPilot.Decide(gap, BottomY, Step, currentX: tick * ForwardSpeed * TickSeconds,
                                               currentY: y, verticalSpeed: vy, BodyRadius,
                                               FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                               TicksToNear, TickSeconds, isFree);
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
            var isFree = Ceiling(6f);
            float y = 0.5f, vy = -30f;
            int flaps = 0;

            for (int tick = 0; tick < 150; tick++)
            {
                var decision = BotPilot.Decide(gap, BottomY, Step, currentX: tick * ForwardSpeed * TickSeconds,
                                               currentY: y, verticalSpeed: vy, BodyRadius,
                                               FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                               TicksToNear, TickSeconds, isFree);
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
        public void 아치_정점만_스치는_천장도_잡는다()
        {
            //  천장이 낮은(4.4m) 구간이 x 3.5~4.0에만 있다 — 근거리 열(2.2m)에도 원거리
            //  지평 끝(4.4m)에도 안 걸리고, 아치가 가장 높은 자리(정점 x=3.74, y=4.512)에만
            //  걸린다. 두 열의 도착 높이만 재던 옛 가드는 이 기둥을 통과시켰다.
            var isFree = Blocks((3.5f, 4.0f, 4.4f + BodyHeight, FarAway));
            var near = ColumnFrom(isFree, NearScanX, cells: 101);
            var decision = BotPilot.Decide(near, BottomY, Step, currentX: 0f, currentY: 0.5f, verticalSpeed: -30f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, isFree);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap);
        }

        [Test]
        public void 아치_정점이_들어가는_천장이면_누른다()
        {
            //  위 테스트와 정확히 같은 자리, 그 구간 천장만 4.6m로 올린다 — 아치 최고점
            //  (0.5+4.012=4.512)이 이번엔 안에 들어와 눌러도 안전하다. 가드가 무조건
            //  막는 게 아님을 보이는 짝이다.
            var isFree = Blocks((3.5f, 4.0f, 4.6f + BodyHeight, FarAway));
            var near = ColumnFrom(isFree, NearScanX, cells: 101);
            var decision = BotPilot.Decide(near, BottomY, Step, currentX: 0f, currentY: 0.5f, verticalSpeed: -30f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, isFree);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 머리_위로_지나갈_수_있는_기둥은_겁먹지_않고_누른다()
        {
            //  x 3.5~4.0에 판이 하나 떠 있어 그 자리에서는 발 높이 1.0~2.0m가 막힌다. 아치는
            //  그 위(4.2m)로 지나가므로 눌러야 한다 — 열 하나를 통째로 "막혔다"고 읽거나
            //  아래쪽 틈만 보고 겁먹으면 통과 가능한 맵을 불가능으로 보고하게 된다.
            var isFree = Blocks((3.5f, 4.0f, 1.9f, 2.0f));
            var near = ColumnFrom(isFree, NearScanX, cells: 101);
            var decision = BotPilot.Decide(near, BottomY, Step, currentX: 0f, currentY: 0.2f, verticalSpeed: -30f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, isFree);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap,
                "머리 위로 지나갈 수 있는데도 누르지 않았다.");
        }

        [Test]
        public void 기둥_위까지_막히면_안_누른다()
        {
            //  위 테스트의 짝 — 같은 자리의 판을 위로 끝까지 늘려 발 높이 1.0m 위가 전부
            //  막히면, 아치 최고점(4.212)이 갈 곳이 없으므로 누르지 않아야 한다.
            var isFree = Blocks((3.5f, 4.0f, 1.9f, FarAway));
            var near = ColumnFrom(isFree, NearScanX, cells: 101);
            var decision = BotPilot.Decide(near, BottomY, Step, currentX: 0f, currentY: 0.2f, verticalSpeed: -30f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, isFree);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap);
        }

        [Test]
        public void 직선_회랑에서는_아치가_들어갈_만큼_넓을_때만_누른다()
        {
            //  통로가 하나뿐인 회랑에서는 천장 가드를 어떻게 구현하든(틈 고르기 → 칸 조회 →
            //  아치 훑기) 판단이 같아야 한다 — 폭을 4.0~5.2m로 훑으며, 회랑이 currentY+아치
            //  (4.012)를 담을 만큼 넓을 때만 누르는지 확인한다. 기대값이 바뀌어야 통과한다면
            //  봇이 요구하는 회랑 폭이 바뀐 것이니(= 보수성이 변했다) 멈추고 따져야 한다.
            //
            //  기대값은 Free(cells)의 실제 자유 상단으로 세운다 — 마지막 자유 칸의 y는
            //  (cells−1)×step이라 Free(46)의 자유 상단은 4.6이 아니라 4.5다. 4.6(안 누름)과
            //  4.7(누름)이 살아 있는 경계다.
            const float currentY = 0.5f;
            const float verticalSpeed = -30f; // 강하 중 — 바닥 규칙은 늘 누르고 싶어 하게 만든다
            float reach = currentY + BotPilot.FlapArc(FlapImpulse, Gravity, TickSeconds);

            for (int i = 0; i <= 12; i++)
            {
                float width = 4.0f + i * 0.1f; // 4.0, 4.1, …, 5.2
                int cells = (int)Math.Round(width / Step);
                var corridor = Free(cells);
                float freeTop = (cells - 1) * Step;
                var decision = BotPilot.Decide(corridor, BottomY, Step, currentX: 0f, currentY: currentY,
                                               verticalSpeed: verticalSpeed, BodyRadius, FlapImpulse, Gravity,
                                               MaxFallSpeed, ForwardSpeed, TicksToNear, TickSeconds,
                                               Ceiling(freeTop));

                bool expectFlap = reach <= freeTop;
                Assert.AreEqual(expectFlap, decision.Flap, $"width={width} (자유 상단 {freeTop}, 도달 {reach})");
            }
        }

        [Test]
        public void 도달_높이가_천장을_조금이라도_넘으면_안_누른다()
        {
            //  경계를 4mm 단위로 못박는다 — 자유 상단이 6.0인 회랑에서 y=1.992면 아치 최고점은
            //  1.992+4.012001=6.004001로 천장을 0.004m 넘는다. 훑기는 몸이 실제로 지나는 자리를
            //  묻기 때문에 이 4mm를 그대로 잡아내야 한다. 여기서 눌러 버리는 구현은 물리가
            //  허락하는 것보다 덜 조심스러운 봇이고, 천장은 한 번 뚫으면 되돌릴 수 없다.
            var corridor = Free(61); // 0~6.0m
            var decision = BotPilot.Decide(corridor, BottomY, Step, currentX: 0f, currentY: 1.992f,
                                           verticalSpeed: -30f, BodyRadius, FlapImpulse, Gravity, MaxFallSpeed,
                                           ForwardSpeed, TicksToNear, TickSeconds, Ceiling(6f));

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap,
                "아치 최고점 6.004가 천장(6.0)을 넘는데 눌렀다.");
        }

        [Test]
        public void 올라가는_길이_막혔으면_도착_높이가_뚫려_있어도_안_누른다()
        {
            //  이 과제가 고친 결함 그 자체. 천장 슬래브가 3.56~4.06m를 채우고 있고 새는 y=1.0에
            //  있다. 도착 높이만 재던 옛 가드가 보던 세 지점은 전부 슬래브 *위*의 진짜 빈
            //  하늘이다 — 근거리 4.34, 정점·원거리 5.012. 그래서 옛 가드는 "뚫렸다"고 읽고 눌렀다.
            //  그러나 새는 거기 못 간다: 몸이 슬래브 아래 끝(3.56)에 닿는 발 높이가 2.66이라,
            //  올라가는 길에 4틱째(y=2.672)에서 박는다.
            //
            //  돌연변이 검증(2026-09-09): 직전 커밋(714e64a3)의 세-열 가드를 그대로 돌려
            //  같은 프로브에서 근거리·정점·원거리 열을 뽑아 넣으면 flap=true(=누른다)가 나온다 —
            //  이 테스트는 그 코드에서 빨강이다. 자세한 절차는 task-10-report.md 참고.
            var isFree = Blocks((-FarAway, FarAway, 3.56f, 4.06f));
            var near = ColumnFrom(isFree, NearScanX, cells: 101);
            var decision = BotPilot.Decide(near, BottomY, Step, currentX: 0f, currentY: 1.0f, verticalSpeed: -30f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, isFree);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap,
                "도착 높이가 슬래브 위로 뚫려 있다고 눌렀다 — 올라가는 길에 슬래브를 뚫는다.");
        }

        [Test]
        public void 슬래브를_치우면_같은_자리에서_누른다()
        {
            //  위 테스트의 짝 — 같은 자리·같은 속도인데 슬래브만 없앤다. 가드가 무조건 막는
            //  게 아니라 "가는 길이 막혔을 때만" 막는다는 것을 보인다.
            var isFree = OpenSky();
            var near = ColumnFrom(isFree, NearScanX, cells: 101);
            var decision = BotPilot.Decide(near, BottomY, Step, currentX: 0f, currentY: 1.0f, verticalSpeed: -30f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, isFree);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 두_틱_표본_사이에_낀_얇은_판도_선분_훑기가_잡는다()
        {
            //  첫 틱은 x로 0.22m·y로 0.46m 움직여 표본 사이 거리가 0.51m다. 몸 반지름이
            //  0.45m라 두 표본의 몸이 덮지 못하는 렌즈 모양 틈이 그 사이에 남는다 — 거기에
            //  2cm짜리 판을 하나 두면, 양 끝점만 보는 구현은 판을 통과해 버린다.
            //  (판의 자리: 첫 틱 선분의 중점에서 수직으로 0.41m 떨어진 곳. 끝점까지 거리는
            //  0.469m로 반지름 밖이고, 선분까지 거리는 0.41m로 반지름 안이다.)
            //
            //  돌연변이 검증(2026-09-09): BotPilot.Decide의 훑기에서 CleanRunSearch.SegmentIsFree
            //  호출을 빼고 끝점(isFree(nextX, nextY))만 보게 하면 이 테스트가 빨강이 된다
            //  (판을 못 보고 눌러 버린다). 확인 후 원복.
            const float plateX = 0.47995f;
            const float plateY = 0.50307f;   // 월드 y — 발 높이로는 0.053 근처의 몸을 막는다
            const float half = 0.01f;
            var isFree = Blocks((plateX - half, plateX + half, plateY - half, plateY + half));
            var near = ColumnFrom(isFree, NearScanX, cells: 101);
            var decision = BotPilot.Decide(near, BottomY, Step, currentX: 0f, currentY: 0f, verticalSpeed: -30f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, isFree);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap,
                "틱 표본 사이에 낀 판을 못 봤다 — 끝점만 보고 통과시켰다.");
        }

        [Test]
        public void 정점을_지난_뒤_아래가_막힌_것은_지금_안_누를_이유가_아니다()
        {
            //  이 과제가 고친 결함. x [3.84, 4.34] 구간에서 4.4820m 아래가 전부 선반이고
            //  나머지는 뚫려 있다. 새는 y=0.5, vy=−30.
            //  아치 정점(17틱, x=3.74, y=4.512001)까지는 선반을 안 스친다 — 지금 누르고 정점에서
            //  한 번 더 누르면 선반을 건드리지 않고 지나간다(오프라인 재생으로 확인, x=6.16까지 무충돌).
            //  정점 뒤까지 훑던 옛 코드는 19틱째(x=4.18, y=4.452 — 이미 내려오는 중)에서 선반에
            //  걸려 거부했다. 그 자리는 "한 번 더 누르면 다시 오르는" 자리이므로 지금 안 누를
            //  이유가 되지 못한다 — 통과 가능한 맵을 불가로 읽게 만든다.
            //
            //  돌연변이 검증(2026-09-09): 훑기 종료 조건을 다시 20틱 지평으로 되돌리면
            //  이 테스트가 빨강이 된다(직전 커밋 ae07bc1a에서 flap=false임을 오프라인으로 확인).
            var isFree = Blocks((3.84f, 4.34f, -FarAway, 4.482f));
            var near = ColumnFrom(isFree, NearScanX, cells: 101);
            var decision = BotPilot.Decide(near, BottomY, Step, currentX: 0f, currentY: 0.5f, verticalSpeed: -30f,
                                           BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                           TicksToNear, TickSeconds, isFree);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap,
                "정점을 지나 내려오는 구간까지 훑어서, 통과 가능한 맵을 불가로 읽었다.");
        }

        [Test]
        public void 훑기가_지나는_자리는_FlapRiseAfter의_아치_그대로이고_정점에서_끝난다()
        {
            //  훑기는 FlapRiseAfter와 같은 산술을 다시 적는다 — 둘이 어긋나면 봇이 실제와 다른
            //  궤적을 검사하게 된다. 훑기 루프 안은 못 보므로 "어디를 물었나"로 확인한다:
            //  ① 모든 틱 t에서 (x = t×0.22, y = FlapRiseAfter(t))를 물었고,
            //  ② 정점(속도가 0 이하가 되는 첫 틱) 너머는 아예 안 물었다.
            //
            //  돌연변이 검증(2026-09-09): 중력 적용을 한 틱 앞당기면(속도를 임펄스−중력×틱에서
            //  시작) ①이 빨강, 종료 조건을 20틱 지평으로 되돌리면 ②가 빨강임을 확인했다.
            var log = new List<(float x, float y)>();
            BotPilot.Decide(Free(101), BottomY, Step, currentX: 0f, currentY: 0f, verticalSpeed: -30f,
                            BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                            TicksToNear, TickSeconds, Recording(log));

            //  정점 틱수를 상수로 적지 않는다 — 물리에서 그대로 뽑는다(FlapArc와 같은 종료 조건).
            int apexTicks = 0;
            for (float speed = FlapImpulse; speed > 0f; speed -= Gravity * TickSeconds)
            {
                apexTicks++;
            }

            for (int t = 0; t <= apexTicks; t++)
            {
                float x = t * ForwardSpeed * TickSeconds;
                float y = BotPilot.FlapRiseAfter(FlapImpulse, Gravity, TickSeconds, t);
                Assert.IsTrue(log.Exists(p => Math.Abs(p.x - x) < 1e-4f && Math.Abs(p.y - y) < 1e-4f),
                    $"t={t}: 훑기가 (x={x}, y={y})를 묻지 않았다 — FlapRiseAfter와 다른 아치를 그린다.");
            }

            float apexX = apexTicks * ForwardSpeed * TickSeconds;
            foreach (var point in log)
            {
                Assert.LessOrEqual(point.x, apexX + 1e-4f,
                    $"정점({apexTicks}틱, x={apexX})을 지나서까지 훑었다 — x={point.x}를 물었다.");
            }
        }

        [Test]
        public void 격자에_안_걸리는_회랑도_문턱을_넘으면_누른다()
        {
            //  0.1m 격자 위의 폭만 훑는 테스트는 문턱이 움직여도 못 본다 — 실제 문턱(아치 정점
            //  0.45 + 4.012001 = 4.462001)은 격자 사이에 살기 때문이다. 그래서 격자에 안 걸리는
            //  자유 상단으로 양쪽에서 조인다. 위쪽은 4.47 — 문턱보다 8mm 넓으니 눌러야 한다.
            //  (2026-09-09 실측: 자유 상단 4.4620에서 안 누름 → 4.4625에서 누름으로 뒤집힌다.
            //   4.47/4.45는 그 문턱에서 각각 +8mm/−12mm, 즉 1cm 안쪽의 쌍이다.)
            const float fineStep = 0.001f;
            const float freeTop = 4.47f;
            var gap = Band(low: 0f, span: freeTop, BottomY, fineStep);
            var decision = BotPilot.Decide(gap, BottomY, fineStep, currentX: 0f, currentY: 0.45f,
                                           verticalSpeed: -30f, BodyRadius, FlapImpulse, Gravity, MaxFallSpeed,
                                           ForwardSpeed, TicksToNear, TickSeconds, Ceiling(freeTop));

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap, "아치 정점 4.462001이 자유 상단 4.47 아래인데 안 눌렀다.");
        }

        [Test]
        public void 격자에_안_걸리는_회랑에서_문턱에_못_미치면_안_누른다()
        {
            //  위 테스트의 짝 — 자유 상단만 4.45로 낮춘다. 아치 정점 4.462001이 12mm 넘치므로
            //  누르면 안 된다. 이 쌍이 봇이 요구하는 회랑 폭을 격자 사이에서 못박는다.
            const float fineStep = 0.001f;
            const float freeTop = 4.45f;
            var gap = Band(low: 0f, span: freeTop, BottomY, fineStep);
            var decision = BotPilot.Decide(gap, BottomY, fineStep, currentX: 0f, currentY: 0.45f,
                                           verticalSpeed: -30f, BodyRadius, FlapImpulse, Gravity, MaxFallSpeed,
                                           ForwardSpeed, TicksToNear, TickSeconds, Ceiling(freeTop));

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap, "아치 정점 4.462001이 자유 상단 4.45를 넘는데 눌렀다.");
        }
    }
}
