using System;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// <b>최소 생존 회랑 게이트.</b> "봇이 버틸 수 있는 가장 좁은 직선 회랑은 몇 미터인가"를
    /// 코드로 못박는다.
    ///
    /// <para>왜 게이트인가: 이 숫자는 봇의 보수성을 한 값으로 요약한다. 봇이 조금이라도
    /// 겁쟁이가 되면 이 숫자가 올라가고(더 넓은 통로를 요구한다), 조금이라도 대담해지면
    /// 내려간다. 그래서 봇의 판단 규칙을 건드리는 변경이 이 숫자를 움직이면 즉시 빨강이 된다 —
    /// "진단만 더했다"고 말하면서 실은 조종을 바꾼 변경을 여기서 잡는다.</para>
    ///
    /// <para><b>왜 테스트로 박는가</b>: 이 숫자는 그동안 저장소 어디에도 없었다. 과제마다
    /// 구현자가 산문 설명에서 하니스를 다시 지었고, 미지정 부분(죽음 판정·시작 높이 표본·시작
    /// 세로속도·몇 틱을 버텨야 생존인지)에 따라 값이 4.4225와 4.4875 사이를 오갔다. 게이트로
    /// 쓰는 숫자가 잴 때마다 다르면 게이트가 아니다. 그래서 <b>측정 방식 전부를 아래 상수로
    /// 못박고</b>, 각 상수에 왜 그 값인지를 남긴다.</para>
    ///
    /// <para><b>숫자가 둘인 이유</b>: 회랑 폭은 <i>천장 쪽</i> 보수성만 잡는다. 얼마나 그런지는
    /// 실측으로 확인됐다 — 바닥 문턱을 <b>−1.5 ~ +5.0m</b> 움직여도 회랑 숫자는 4.4750에서
    /// 안 변하고, 바닥 규칙을 <b>통째로 무시</b>해도(<c>flap = ceilingSafe</c>) 역시 4.4750이
    /// 나온다. 즉 <b>회랑 숫자는 순수하게 천장 훑기의 눈금이고, 바닥 축은 1.99125가 혼자
    /// 진다.</b> 다음에 이 파일을 읽는 사람에게: "회랑 게이트가 대충 덮으니 바닥 테스트는
    /// 지워도 되겠지"는 <b>틀렸다</b> — 바닥 규칙을 완전히 없애도 회랑 게이트는 초록이다.</para>
    /// </summary>
    public class BotCorridorGateTests
    {
        //  ── 물리 (실제 Flappy 값 — 여기를 고치면 다른 숫자를 재는 것이다) ──
        const float BodyRadius = 0.45f;
        const float TickSeconds = 0.02f;
        const float Gravity = 70f;
        const float MaxFallSpeed = 30f;
        const float FlapImpulse = 23f;
        const float ForwardSpeed = 11f;

        //  실제 도구(FlappyMapPlayabilityCheck)의 HeightGrid와 같다 — 봇이 보는 표의 눈금이자
        //  아치 훑기가 선분을 찍는 간격이다. 이 값이 곧 봇의 시력이라 측정의 일부다.
        const float ScanStep = 0.1f;
        const float BottomY = 0f;
        //  근거리 열은 0.20초 앞을 본다(도구와 같은 값). 초당 50틱이므로 10틱.
        const int TicksToNear = 10;

        //  ── 측정 방식 (이 다섯이 곧 "무엇을 쟀나"의 정의다) ──

        //  막힘 표를 이 높이까지 채운다. 회랑(≈4.5m)보다 한참 위라 봇이 표 끝을 "뚫린 하늘"로
        //  오해할 일이 없다 — 표를 회랑 바로 위에서 끊으면 TryFindGap이 표 밖을 어떻게 보느냐에
        //  따라 답이 갈려, 지형이 아니라 표의 길이가 숫자를 정하게 된다.
        const float TableTop = 12f;

        //  시작 세로속도. 0인 이유: 0이 아니면 "이만큼 떨어지던 중에도 회복하는가"라는 다른
        //  질문이 회랑 숫자에 섞인다(실측: −30으로 출발시키면 문턱이 4.5625로 올라간다).
        //  +임펄스로 출발시키면 천장 근처 시작 높이가 무조건 솟아 박아서 어떤 폭도 통과 못 한다.
        const float StartVerticalSpeed = 0f;

        //  몇 틱을 버텨야 "생존"인가. 250틱 = 5초 = 코스 55m. 날갯짓 한 번의 아치가 17틱이니
        //  14사이클이 넘는다 — 과도기가 아니라 정상 상태를 본다는 뜻이다. 실측으로 250·500·
        //  1000틱이 모두 같은 문턱을 주고 100틱만 낮게 나온다(4.4650). 즉 250은 수렴한 뒤다.
        const int SurviveTicks = 250;

        //  시작 높이 표본 간격. 회랑 [0, W] 전체를 이 간격으로 훑는다. 이 계는 혼돈스러워서
        //  1m 차이로 결과가 뒤집히므로, "어느 높이에서 출발해도 버틴다"를 요구해야 숫자가
        //  운에 안 흔들린다(운 좋은 한 높이만 세면 4.415가 나오는데, 그 폭은 18개 시작 높이 중
        //  2개만 산다 — 통과할 수 있는 통로라 부를 수 없다).
        const float StartHeightStep = 0.25f;

        //  훑는 폭의 해상도. 0.0025m는 위 250틱 × 18개 시작 높이와 함께 0.3초 안에 끝난다.
        //
        //  ⚠️ <b>두 게이트 중 이쪽이 더 얇다.</b> 4.4750에서 가장 빠듯한 궤적은 천장을
        //  <b>0.99mm</b> 남기고 스친다 — 바닥 게이트(1.99125)의 여백 1.25mm보다 좁다. 즉 float
        //  마지막 비트가 흔들릴 여지가 더 큰 쪽은 회랑 숫자다. (이 걱정 메모는 원래 바닥
        //  게이트에 붙어 있었는데 엉뚱한 데 붙어 있었다 — 1.99125는 여백 1.25mm에 세 방법으로
        //  확인됐다. 그래서 여기로 옮긴다.) 흔들리면 WidthStep을 줄이지 말고 — 그러면 답이
        //  달라진다 — 어느 기계에서 얼마가 나오는지부터 재라.
        const float WidthStep = 0.0025f;
        //  훑는 폭의 범위. 아래 끝은 반드시 죽고 위 끝은 반드시 살아야 한다 — 그 두 단언이
        //  "답이 이 밴드 안에 있다"를 보장한다(밴드를 잘못 잡아 답을 스쳐 지나가지 않게).
        const float ScanLowWidth = 4.30f;
        const float ScanHighWidth = 4.60f;

        //  ── 못박는 숫자 ──
        //  자유 발 구간이 [0, W]인 직선 회랑에서, 위 방식으로 재어 봇이 버티는 가장 좁은 폭.
        //  아치 한 번(4.012001) + 바닥 여유(몸 반지름 0.45) = 4.462001이 봇이 "누르겠다"고
        //  판단하는 문턱인데, 실제 생존 문턱은 그보다 13mm 넓다 — 봇이 탐욕적이라 누를 수
        //  있는 가장 낮은 자리에서 정확히 누르지 못하고 조금 늦기 때문이다. 그 13mm가 곧
        //  "이 봇이 물리적 최적보다 얼마나 못한가"의 크기다.
        const float NarrowestSurvivableWidth = 4.4750f;

        //  자유 발 구간이 [0, width]인 직선 회랑. 발이 그 밖이면 지형 안이다.
        //  실제 맵 검사와 달리 캡슐을 재지 않는다 — 게이트가 재려는 것은 "봇의 조종"이지
        //  "몸 모양"이 아니라서, 몸은 이미 폭 안에 반영된 것으로 보고 발 좌표만으로 정의한다.
        static ExactFreeSpaceProbe Corridor(float width) => (x, y) => y >= 0f && y <= width;

        //  실제 도구(FlyBot)가 하는 그대로 — 같은 프로브를 눈금 간격으로 찍어 막힘 표를 만든다.
        //  직선 회랑은 x에 따라 변하지 않으므로 한 번만 만들면 된다.
        static bool[] ColumnOf(float width)
        {
            var free = Corridor(width);
            int cells = (int)Math.Round(TableTop / ScanStep) + 1;
            var column = new bool[cells];
            for (int i = 0; i < cells; i++)
            {
                column[i] = free(0f, BottomY + i * ScanStep) == false;
            }
            return column;
        }

        /// <summary>폭 <paramref name="width"/>인 회랑에서 <paramref name="startY"/>로 출발해
        /// <see cref="SurviveTicks"/>틱을 버티는가.
        /// <para><b>죽음 판정</b>: 한 틱을 밟은 뒤 발이 자유 구간 밖이면 죽음이다. 틱 사이를
        /// 따로 훑지 않는 이유는 자유 구간이 하나로 이어진 띠라서다 — 양 끝이 다 안에 있으면
        /// 그 사이도 안이다(한 틱에 최대 0.6m 움직이는데 벽을 뚫고 반대편 자유 공간으로 나갈
        /// 곳이 없다).</para></summary>
        static bool Survives(float width, float startY)
        {
            var free = Corridor(width);
            bool[] column = ColumnOf(width);
            float y = startY;
            float vy = StartVerticalSpeed;
            //  출발 자리가 이미 지형 안이면 잰 것이 없다 — 실패로 센다(밴드를 [0, W]로
            //  잡으므로 정상적으로는 안 걸린다).
            if (free(0f, y) == false)
            {
                return false;
            }

            for (int tick = 0; tick < SurviveTicks; tick++)
            {
                BotDecision decision = BotPilot.Decide(
                    column, BottomY, ScanStep, currentX: tick * ForwardSpeed * TickSeconds,
                    currentY: y, verticalSpeed: vy, BodyRadius, FlapImpulse, Gravity, MaxFallSpeed,
                    ForwardSpeed, TicksToNear, TickSeconds, free);

                //  실제 게임 커널(FlappyMapPlayabilityCheck.Step)과 같은 순서 — 중력을 먼저
                //  깎고 종단속도로 자른 뒤, 날갯짓이면 그 값을 덮어쓰고(그 틱은 감쇠 없음),
                //  그 속도로 움직인다. 순서를 바꾸면 다른 물리를 재게 된다.
                vy -= Gravity * TickSeconds;
                if (vy < -MaxFallSpeed) { vy = -MaxFallSpeed; }
                if (decision.Flap) { vy = FlapImpulse; }
                y += vy * TickSeconds;

                if (free(0f, y) == false)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>회랑 [0, width]의 모든 시작 높이에서 버티는가. "어느 높이에서 출발해도"가
        /// 이 게이트의 생존 정의다.</summary>
        static bool SurvivesFromEveryStartHeight(float width)
        {
            //  훑는 값을 더해 나가지 않고 인덱스로 곱해 만든다 — float은 더할수록 오차가
            //  쌓여서, 800번쯤 더하면 격자가 통째로 한 칸 어긋날 수 있다. 그러면 못박은
            //  숫자가 기계·빌드에 따라 흔들려 게이트 노릇을 못 한다.
            int steps = (int)(width / StartHeightStep);
            for (int i = 0; i <= steps; i++)
            {
                if (Survives(width, i * StartHeightStep) == false)
                {
                    return false;
                }
            }
            return true;
        }

        //  밴드를 훑어 처음으로 버티는 폭. 못 찾으면 −1.
        static float SweepForNarrowestWidth(float low, float high, float step)
        {
            int steps = (int)((high - low) / step);
            for (int i = 0; i <= steps; i++)
            {
                float width = low + i * step;
                if (SurvivesFromEveryStartHeight(width))
                {
                    return width;
                }
            }
            return -1f;
        }

        [Test]
        public void 최소_생존_회랑은_4_4750m다()
        {
            //  밴드 양 끝이 각각 확실히 죽고 확실히 사는지 먼저 확인한다 — 이것 없이 훑기만
            //  하면, 밴드를 잘못 잡아 답을 통째로 스쳐 지나가도 "찾았다"는 값이 나온다.
            //  ⚠️ <b>아래 끝 단언은 하니스 자기검사다(게이트 아님).</b> 4.30m는 문턱보다
            //  17.5mm 좁아, 돌연변이 17개 전부에서 초록이었다 — 봇이 바뀌었는지는 아래
            //  AreEqual이 잡는다. 대역이 답을 감싸는지 보는 용도로만 값을 한다.
            Assert.IsFalse(SurvivesFromEveryStartHeight(ScanLowWidth),
                $"훑기 밴드의 아래 끝({ScanLowWidth}m)에서 이미 버틴다 — 답이 이 밴드 밖이다.");
            Assert.IsTrue(SurvivesFromEveryStartHeight(ScanHighWidth),
                $"훑기 밴드의 위 끝({ScanHighWidth}m)에서도 못 버틴다 — 답이 이 밴드 밖이다.");

            float narrowest = SweepForNarrowestWidth(ScanLowWidth, ScanHighWidth, WidthStep);

            //  허용 오차는 훑기 간격의 절반 — "같은 격자점을 짚었나"만 묻는 폭이다. 이보다
            //  넉넉하게 잡으면 문턱이 한 칸 움직여도 초록이라 게이트가 아니게 된다.
            Assert.AreEqual(NarrowestSurvivableWidth, narrowest, WidthStep * 0.5f,
                $"최소 생존 회랑이 움직였다 — 봇의 보수성이 바뀌었다는 뜻이다. "
                + $"(잰 값 {narrowest:F4}m, 못박은 값 {NarrowestSurvivableWidth:F4}m)");

            //  "가장 좁은"의 반대편 — 한 칸 좁으면 정말 못 버텨야 한다. 위 등호만으로는
            //  훑기가 첫 성공을 돌려준다는 사실에 기대는 셈이라, 그 사실 자체를 여기서 확인한다.
            //  ⚠️ 이것도 <b>하니스 자기검사</b> 쪽에 가깝다 — 돌연변이 17개 전부에서 초록이었다.
            //  봇이 대담해지면 이 줄이 아니라 위 AreEqual이 먼저 빨강이 된다.
            Assert.IsFalse(SurvivesFromEveryStartHeight(NarrowestSurvivableWidth - WidthStep),
                $"{NarrowestSurvivableWidth - WidthStep:F4}m에서도 버틴다 — 그럼 그쪽이 최소다.");
        }

        [Test]
        public void 문턱보다_넓은_회랑은_전부_버틴다()
        {
            //  문턱이 우연히 얻어걸린 한 점이 아니라 진짜 경계임을 확인한다. 이 계는 혼돈스러워
            //  "넓을수록 쉽다"가 공짜로 주어지지 않는다 — 실제로 문턱 아래에서는 폭을 넓혀도
            //  생존 시작 높이 수가 들쭉날쭉하다(2 → 4 → 5 → 5 → 6 …). 그 들쭉날쭉함이 문턱
            //  위로는 사라진다는 것이 이 단언이다.
            for (int i = 0; i * 0.05f <= 5.2f - NarrowestSurvivableWidth; i++)
            {
                float width = NarrowestSurvivableWidth + i * 0.05f;
                Assert.IsTrue(SurvivesFromEveryStartHeight(width),
                    $"폭 {width:F4}m — 문턱({NarrowestSurvivableWidth:F4}m)보다 넓은데 못 버텼다.");
            }
        }

        //  ── 게이트의 반대쪽 절반 (바닥 규칙) ──
        //  위 회랑 숫자는 <b>천장 쪽 보수성만</b> 잰다. 실측 범위가 넓다: 바닥 문턱(safeFloor)을
        //  <b>−1.5m에서 +5.0m까지</b> 움직여도 회랑 숫자는 4.4750에서 1mm도 안 움직이고,
        //  바닥 규칙을 아예 빼도(<c>flap = ceilingSafe</c>) 같은 값이 나온다. 구조적으로
        //  그렇다 — 폭이 4.475m인 회랑에서 아치(4.012)가 들어가는 높이는 y ≤ 0.463뿐이라,
        //  "언제 누르고 싶은가"와 무관하게 <b>언제 누를 수 있는가</b>가 판을 지배한다.
        //  그래서 바닥 쪽을 잡는 숫자를 하나 더 못박는다. <b>이것 없이는 게이트가 반쪽이
        //  아니라 아예 없다</b> — 바닥 규칙을 통째로 지워도 회랑 게이트는 초록이니까.

        //  천장이 아무 상관 없는 높은 회랑. 아래 문턱(≈1.99)에서 아치를 다 그려도 6.0이라
        //  표 꼭대기(12m)에 한참 못 미친다 — 천장 가드가 이 측정에 끼어들 여지가 없다.
        const float OpenCorridorTop = TableTop;

        //  바닥 문턱을 훑는 간격과 <b>반 칸 어긋난 시작점</b>. 어긋나게 두는 이유: 참 문턱은
        //  바닥 여유(0.45) + 10틱 자유낙하(1.54) = 1.99로 딱 떨어지는 값이라, 격자를 0에서
        //  시작하면 문턱이 격자점 위에 정확히 놓인다. 그러면 마지막 비트 하나 차이로 답이
        //  한 칸 튀어 기계마다 다른 값이 나온다. 반 칸 어긋나면 양쪽으로 1.25mm씩 여유가 생긴다.
        //  이 1.25mm 여백은 세 방법으로 확인됐다 — <b>두 게이트 중 이쪽이 더 두껍다</b>.
        //  더 얇은 쪽(회랑 4.4750, 여백 0.99mm)의 걱정 메모는 위 WidthStep에 있다.
        const float FloorProbeStep = 0.0025f;
        const float FloorProbeOffset = 0.00125f;

        //  가만히 있어도 바닥 규칙이 손을 대기 시작하는 높이 — 이보다 낮으면 누르고 높으면
        //  안 누른다. 곧 "봇이 바닥에서 얼마나 일찍 겁내는가"의 크기다. 겁쟁이(+0.05)로
        //  만들면 2.04125, 대담(−0.05)하게 만들면 1.94125로 그만큼 그대로 움직인다.
        const float FloorHoldHeight = 1.99125f;

        [Test]
        public void 바닥_규칙이_손을_대기_시작하는_높이는_1_99125m다()
        {
            var free = Corridor(OpenCorridorTop);
            bool[] column = ColumnOf(OpenCorridorTop);

            float held = -1f;
            //  아래에서 위로 훑어, 처음으로 "안 누른다"가 나오는 높이를 찾는다. 세로속도 0
            //  (정지)로 묻는 이유는 회랑 게이트와 같다 — 떨어지던 속도를 섞으면 다른 질문이 된다.
            for (int i = 0; i * FloorProbeStep + FloorProbeOffset <= 6f; i++)
            {
                float y = FloorProbeOffset + i * FloorProbeStep;
                BotDecision decision = BotPilot.Decide(
                    column, BottomY, ScanStep, currentX: 0f, currentY: y, verticalSpeed: 0f,
                    BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                    TicksToNear, TickSeconds, free);
                if (decision.Flap == false) { held = y; break; }
            }

            Assert.AreEqual(FloorHoldHeight, held, FloorProbeStep * 0.5f,
                $"바닥 규칙의 문턱이 움직였다 — 봇이 바닥에서 겁내는 시점이 바뀌었다는 뜻이다. "
                + $"(잰 값 {held:F5}m, 못박은 값 {FloorHoldHeight:F5}m)");

            //  바로 아래 칸에서는 정말 누르는지 — 위 값이 "훑기가 첫 성공을 돌려준다"는 사실에만
            //  기대지 않게 반대편도 확인한다.
            var below = BotPilot.Decide(column, BottomY, ScanStep, currentX: 0f,
                                        currentY: FloorHoldHeight - FloorProbeStep, verticalSpeed: 0f,
                                        BodyRadius, FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                        TicksToNear, TickSeconds, free);
            Assert.IsTrue(below.Flap,
                $"{FloorHoldHeight - FloorProbeStep:F5}m에서도 안 누른다 — 그럼 그쪽이 문턱이다.");
        }

        /// <summary><b>하니스 자기검사 — 게이트가 아니다.</b>
        ///
        /// <para>3m는 날갯짓 한 번의 아치(4.012)보다 좁아, 누르든 안 누르든 죽는다. 즉 이 단언은
        /// <c>BotPilot</c>이 아니라 <b>상수에 대한 정리</b>다 — 봇의 조종을 어떻게 바꿔도 참이다.
        /// 실제로 돌연변이 17개(항상 누름·항상 안 누름·가드 제거·완벽 오라클 포함) 전부에서
        /// 초록이었다. <b>봇이 바뀌었는지를 이 테스트에 기대지 말 것.</b> 그 일은 위의 두
        /// 게이트(4.4750 / 1.99125)가 한다.</para>
        ///
        /// <para>그래도 지우지 않는 이유: 하니스가 <i>죽음을 잴 줄 아는가</i>를 본다. 하니스가
        /// 무엇이든 초록으로 만드는 물건이 되면(예: 죽음 판정이 통째로 깨지면) 여기가 빨강이 된다.
        /// 대역이 맞는지 보는 자기검사로는 값을 한다.</para></summary>
        [Test]
        public void 하니스_자기검사_아치보다_좁은_회랑은_못_버틴다()
        {
            for (int i = 0; i * StartHeightStep <= 3f; i++)
            {
                float startY = i * StartHeightStep;
                Assert.IsFalse(Survives(3f, startY),
                    $"시작 y={startY:F2}: 아치(4.012m)가 안 들어가는 3m 회랑에서 250틱을 버텼다"
                    + " — 하니스가 죽음을 못 재고 있다는 뜻이다.");
            }
        }
    }
}
