using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>발밑이 (x, y)일 때 몸이 아무데도 안 닿고 들어가는가. 부르는 쪽은 질의 좌표를
    /// 격자에 스냅해 <b>그 스냅된 자리에서</b> 재고 답을 칸에 캐시한다 — 그래서 누가 언제 묻든
    /// 같은 칸이면 같은 답이 나온다(격자 위에서 잘 정의된 함수).</summary>
    public delegate bool FreeSpaceProbe(float x, float y);

    /// <summary>캐시를 타지 않고 정확한 좌표에서 재는 자유공간 프로브. 격자에 스냅해 재는
    /// <see cref="FreeSpaceProbe"/>와 섞이면 안 되므로 타입 자체를 가른다 — 둘 다 같은
    /// (float, float) → bool 이라, 타입이 하나면 호출부가 뒤바꿔 넘겨도 컴파일러가 못 잡는다.</summary>
    public delegate bool ExactFreeSpaceProbe(float x, float y);

    /// <summary>
    /// 발밑이 (x, y)이고 세로 속도가 <paramref name="verticalSpeed"/>일 때, <b>한 틱을 나아가는
    /// 동안</b> 몸이 아무 데도 안 닿는가. 도착점 하나를 찍는 <see cref="FreeSpaceProbe"/>와 달리
    /// 그 틱의 <i>지나간 자리 전부</i>를 묻는다 — 진짜 이동 커널이 캡슐을 쓸어서 판정하기
    /// 때문이다(<see cref="FlappyTickSweep"/>가 그 커널을 그대로 부르는 구현).
    ///
    /// <para>한 틱의 가로 이동거리와 dt는 묻지 않는다 — 둘 다 프로브 쪽이 이미 알고 있고,
    /// 여기서 다시 넘기면 탐색이 믿는 값과 커널이 쓰는 값이 갈릴 자리가 하나 더 생긴다.</para>
    /// </summary>
    public delegate bool TickSweepProbe(float x, float y, float verticalSpeed);

    /// <summary>한 틱 동안 새가 서 있던 자리의 기류. 탐색·재생이 게임과 같은 자리(이동 전)를 묻는다.</summary>
    public delegate LOP.FlappyAirflowKind AirflowProbe(float x, float y);

    /// <summary>
    /// 자유공간 캐시(<see cref="FreeSpaceProbe"/>를 구현하는 쪽)가 쓰는 격자 산술. 캐시 본체는
    /// 에디터 어셈블리에 있어 테스트가 닿지 않으므로, 그 답을 좌우하는 이 산술만 여기로 뺐다 —
    /// 여기를 항등으로 되돌리면 캐시가 다시 "누가 먼저 물었나"에 흔들린다.
    /// </summary>
    public static class FreeSpaceGridMath
    {
        /// <summary>값이 속한 격자 칸의 번호. 칸 중심을 기준으로 가장 가까운 칸을 고른다.</summary>
        public static int CellOf(float value, float grid)
            => UnityEngine.Mathf.RoundToInt(value / grid);

        /// <summary>자유공간 캐시가 실제로 재는 자리. 격자 점으로 스냅해 "누가 언제 묻든 같은
        /// 칸이면 같은 답"을 만든다. 이미 격자 위에 있는 값에는 아무 일도 하지 않는다.</summary>
        public static float SnapToGrid(float value, float grid)
            => CellOf(value, grid) * grid;
    }

    /// <summary>
    /// Flappy 한 틱의 세로 속도 갱신. <b>탐색과 진짜 커널(<c>FlappyMapPlayabilityCheck.Step</c>)이
    /// 이 한 코드를 같이 쓴다</b> — 같은 규칙을 두 곳에 따로 적어 두면 한쪽만 고쳐져도 아무도
    /// 못 알아채고, 그러면 탐색이 찾은 경로가 재생에서 깨진다.
    ///
    /// <para>순서가 전부다: <b>중력(또는 기류)을 한 번 적용하고 → 종단속도로 자르고 → 날갯짓이면
    /// 덮어쓴다.</b> 날갯짓은 더하기가 아니라 <i>덮어쓰기</i>(그때까지의 세로 속도를 버린다)라서 맨
    /// 뒤여야 한다. 식 자체는 게임과 같은 <see cref="LOP.FlappyVerticalKernel"/> 한 곳에만 있다.</para>
    /// </summary>
    public static class FlappyTickMath
    {
        /// <summary>
        /// 틱마다의 x를 <b>재생과 같은 방식(더하기)으로</b> 채운 표. <paramref name="count"/>개를 만든다.
        ///
        /// <para><b>왜 곱셈이 아닌가.</b> <c>startX + stepX * n</c>은 수학적으로 같지만 부동소수에서
        /// 다르다 — 진짜 이동 커널은 매 틱 x에 <c>stepX</c>를 <i>더해</i> 나아가므로, 곱셈으로 구한
        /// x는 틱이 쌓일수록 벌어진다. 실측(전진 6.8 · 틱 0.02): 2848틱 1.1cm · 3683틱 2.2cm ·
        /// 4267틱 3.0cm. 커널이 벽에서 띄우는 여유가 0.02m라 3cm면 <b>스치는 판정이 뒤집힌다</b> —
        /// 탐색이 자유라고 본 틱에서 재생이 벽에 걸렸다(2026-09-23, 코스를 60초→90초로 늘리자 드러났다).
        /// 세로 속도·높이를 양쪽이 같은 코드로 구하게 맞춘 것과 <b>같은 이유·같은 자리</b>다.</para>
        /// </summary>
        public static float[] ColumnXTable(float startX, float stepX, int count)
        {
            if (count < 1)
            {
                count = 1;
            }
            var table = new float[count];
            table[0] = startX;
            for (int i = 1; i < count; i++)
            {
                table[i] = table[i - 1] + stepX;
            }
            return table;
        }

        /// <summary>기류 없는 자리의 세로 속도. 아래 기류판에 <c>None</c>을 넘긴 것과 같다.</summary>
        public static float NextVerticalSpeed(float verticalSpeed, bool flap,
                                              float flapImpulse, float gravity,
                                              float maxFallSpeed, float tickSeconds)
            => NextVerticalSpeed(verticalSpeed, flap, LOP.FlappyAirflowKind.None, flapImpulse, gravity,
                                 maxFallSpeed, tickSeconds, 0f, 0f, 1f);

        /// <summary>게임의 세로 속도 식 그 자체(<see cref="LOP.FlappyVerticalKernel"/>). 따로 적지 않는다.</summary>
        public static float NextVerticalSpeed(float verticalSpeed, bool flap, LOP.FlappyAirflowKind air,
                                              float flapImpulse, float gravity, float maxFallSpeed, float tickSeconds,
                                              float upAccel, float riseCap, float shaftGravityMult)
            => LOP.FlappyVerticalKernel.Next(verticalSpeed, flap, air, flapImpulse, gravity, maxFallSpeed,
                                             tickSeconds, upAccel, riseCap, shaftGravityMult);

        /// <summary>
        /// 세로로 한 틱 움직인 뒤의 높이. <b>진짜 이동 커널(<c>LOP.KinematicMover</c>)의 수직
        /// 스텝과 같은 모양</b>이어야 해서 이렇게 쓴다 — 이동 거리를 먼저 변수 하나에 담고,
        /// 그 다음 부호를 붙여 더한다.
        ///
        /// <para><b>왜 <c>y + vy * dt</c>라고 한 줄에 쓰지 않나(실측).</b> 곱셈과 덧셈을 한 식에
        /// 붙여 쓰면 JIT이 둘을 <i>하나의</i> 명령(fused multiply-add)으로 합쳐 중간 반올림을
        /// 건너뛴다. 커널은 중간 거리를 변수에 담으므로 거기서 한 번 더 반올림된다. 그 1 ulp
        /// 차이가 틱마다 쌓여, 자유낙하 13틱 만에 6e-8, 190틱이면 눈에 띄게 갈렸다
        /// (2026-09-14 실측, Apple Silicon Mono). "거의 같다"로는 이 탐색이 성립하지 않는다 —
        /// 재생이 <b>정확히</b> 같아야 찾은 경로가 증명이 된다.</para>
        ///
        /// <para>아주 느릴 때(한 틱 이동이 1e-5m 미만) 커널은 아예 안 움직이므로 여기서도 안
        /// 움직인다. 그 문턱도 커널에서 그대로 가져온 값이다.</para>
        /// </summary>
        public static float AdvanceHeight(float y, float verticalSpeed, float tickSeconds)
        {
            float distance = UnityEngine.Mathf.Abs(verticalSpeed) * tickSeconds;
            if (distance <= 1e-5f)
            {
                return y;
            }
            return verticalSpeed >= 0f ? y + distance : y - distance;
        }
    }

    public readonly struct CleanRunOptions
    {
        public readonly float StartX, StartY, FinishX;
        public readonly float MinY, MaxY;
        public readonly float ForwardSpeed, FlapImpulse, Gravity, MaxFallSpeed;
        public readonly float TickSeconds;
        public readonly float HeightGrid;
        public readonly float UpAccel, RiseCap, ShaftGravityMult;

        public CleanRunOptions(float startX, float startY, float finishX, float minY, float maxY,
                               float forwardSpeed, float flapImpulse, float gravity, float maxFallSpeed,
                               float tickSeconds, float heightGrid,
                               float upAccel = 0f, float riseCap = 0f, float shaftGravityMult = 1f)
        {
            UpAccel = upAccel; RiseCap = riseCap; ShaftGravityMult = shaftGravityMult;
            StartX = startX; StartY = startY; FinishX = finishX;
            MinY = minY; MaxY = maxY;
            ForwardSpeed = forwardSpeed; FlapImpulse = flapImpulse;
            Gravity = gravity; MaxFallSpeed = maxFallSpeed;
            TickSeconds = tickSeconds; HeightGrid = heightGrid;
        }
    }

    public readonly struct CleanRunResult
    {
        public readonly bool Reachable;
        /// <summary>열마다 "이 틱에 날갯짓했나". 도달 가능할 때만 채워진다.</summary>
        public readonly IReadOnlyList<bool> Flaps;
        public readonly float BlockedX;
        /// <summary>
        /// 막혔을 때만 의미 있는 진단값 — "고칠 자리는 여기"를 가리킨다. 아래 세 필드 모두
        /// <c>NarrowestCount == 0</c>이면 "측정 안 함"이다: 도달 가능했거나(회랑 통계 자체가
        /// 필요 없음), 상태 수가 늘어나길 멈추기도 전에(출발 직후 과도기 중) 막혀서 진짜
        /// 병목을 아직 못 본 경우다.
        /// </summary>
        public readonly float NarrowestX;
        /// <summary>0이면 "측정 안 함". 위 <see cref="NarrowestX"/> 요약 참고.</summary>
        public readonly int NarrowestCount;
        public readonly float NarrowestHeightSpan;

        public CleanRunResult(bool reachable, IReadOnlyList<bool> flaps, float blockedX,
                              float narrowestX, int narrowestCount, float narrowestHeightSpan)
        {
            Reachable = reachable;
            Flaps = flaps;
            BlockedX = blockedX;
            NarrowestX = narrowestX;
            NarrowestCount = narrowestCount;
            NarrowestHeightSpan = narrowestHeightSpan;
        }
    }

    /// <summary>
    /// 한 번도 안 부딪히고 결승선까지 갈 경로가 있는가를 상태공간 탐색으로 답한다.
    ///
    /// <para>세 가지 덕에 문제가 작다. ① 안 닿는 동안 커널이 하는 일은 <c>위치 += 속도 × dt</c>뿐이라
    /// 물리가 정확히 포물선이다. ② 전진이 상수라 x는 선택의 대상이 아니고 "열 = 틱"이다.
    /// ③ 상태는 (높이칸, 세로속도칸)이고 각 칸에 <b>정확한</b> (높이, 세로속도)를 들고 다닌다.
    /// 예전엔 세로 속도를 "마지막 날갯짓 뒤 몇 틱"의 사다리로 셌지만, 기류 안에선 속도가 자리에
    /// 따라 달라져 그 전제가 깨진다. 속도칸 폭(0.05 m/s)은 기류 밖 속도들을 한 칸에 합치지 않을
    /// 만큼 좁다(종단속도에 닿아 속도가 똑같아진 경우만 합쳐지고, 그건 아래 "같은 칸이면 높은 쪽" 근사와 같은 종류다).
    /// 그래서 기류가 없으면 예전과 같은 답이 나온다.</para>
    ///
    /// <para><b>높이는 정확히 이어 가고, 눈금은 "이미 가 본 상태인가"의 열쇠로만 쓴다.</b>
    /// 한 틱 전진은 진짜 커널(<c>FlappyMapPlayabilityCheck.Step</c> → <c>KinematicMover</c>)과
    /// 같은 산술이므로, 찾은 경로를 그 커널로 재생하면 같은 높이가 나와야 한다.</para>
    ///
    /// <para><b>왜 이렇게 바꿨나(2026-09-07 → 09-14).</b> 예전에는 매 틱 높이를 0.1m 눈금에
    /// 반올림하고 <i>그 반올림된 값을 다음 틱의 입력으로</i> 썼다. 그래서 틱당 약 0.002m씩
    /// 편향이 쌓이고(낙하 첫 몇 틱은 한 틱 하강 0.028m이 눈금보다 작아 아예 안 내려갔다),
    /// 190틱쯤 지나면 탐색이 믿는 높이가 진짜 물리와 수 m 벌어졌다. 실측 맵 네 스폰 전부
    /// 탐색은 경로를 찾았는데 진짜 커널 재생에서 깨져 "모름"으로 남았다. 지금은 상태가
    /// 정확한 (높이, 세로속도)를 들고 다녀 그 편향이 없다.</para>
    ///
    /// <para><b>닿았나도 커널과 같은 자로 잰다(2026-09-14).</b> 예전에는 도착점 하나를 0.1m
    /// 눈금에 붙여 정지 캡슐 검사로 찍었다 — 커널은 캡슐을 한 틱 동안 쓸고 벽에서 0.02m를
    /// 띄우므로, 탐색이 커널보다 <b>관대</b>했고 찾은 경로가 재생에서 벽에 걸렸다. 지금은
    /// 한 틱 전진 판정을 <see cref="TickSweepProbe"/>로 묻고, 실검사에서는 그 구현이
    /// <see cref="FlappyTickSweep"/> = <b>진짜 커널 그 자체</b>다.</para>
    ///
    /// <para><b>기류도 게임과 같은 자리에서 묻는다</b> — 그 틱에 움직이기 <i>전</i> 새가 선 자리.
    /// 게임(<c>FlappyMoveSystem</c>)이 그렇게 하므로 한 자리라도 다르면 재생이 갈린다.</para>
    ///
    /// <para>남은 근사는 <b>하나뿐</b>이다 — 같은 열쇠 칸에 든 정확한 상태 중 하나만 남긴다
    /// (<see cref="TryAdvance"/> 주석 참고). 그래서 <b>✅(재생까지 통과)는 증명이지만 ❌는
    /// 여전히 "이 근사 아래서 못 찾았다"</b>이다 — 밀려난 상태로만 빠져나가는 길이 있으면
    /// 탐색은 그것을 못 본다.</para>
    /// </summary>
    public static class CleanRunSearch
    {
        /// <param name="seedIsFree">출발 자리가 지형에 파묻혀 있지는 않은가. <b>격자에 붙이지
        /// 않은 정확한 좌표</b>에서 재는 점 검사다 — 한 틱 전진 판정이 아니라 "시작할 수 있는
        /// 자리인가"만 본다.</param>
        /// <param name="tickIsFree">한 틱 전진이 아무 데도 안 닿는가. 실검사에서는 진짜 이동
        /// 커널 그 자체다(<see cref="FlappyTickSweep"/>).</param>
        /// <param name="air">자리마다의 기류. 없으면 어디에도 기류가 없는 것으로 본다.</param>
        public static CleanRunResult Run(in CleanRunOptions options, ExactFreeSpaceProbe seedIsFree,
                                         TickSweepProbe tickIsFree, AirflowProbe air = null)
        {
            //  결승선이 출발점보다 앞이거나 같으면 코스 길이가 0 이하다 — 그러면 아래 열
            //  순회가 한 번도 안 돌아 그대로 "도달 가능"으로 떨어진다(빈 Flaps와 함께).
            //  부르는 쪽이 이 전제를 지킨다고 믿지 않고 여기서 직접 막는다 — 순수 계층이
            //  자기 전제를 스스로 지켜야, 호출부가 실수해도 "거꾸로 된 코스가 통과했다"는
            //  거짓 결과가 나오지 않는다.
            if (options.FinishX <= options.StartX)
            {
                return new CleanRunResult(false, System.Array.Empty<bool>(), options.StartX, 0f, 0, 0f);
            }

            var grid = new SearchGrid(options);

            var current = NewColumn(grid.StateCount);
            var currentV = new float[grid.StateCount];
            var currentLive = new List<int>();
            //  출발 높이 자체가 허용 범위 밖이면 그대로 실패 — HeightBucket이 조용히 경계로
            //  밀어 넣어 버리면 "다른 자리에서 시드해 놓고 진짜 출발지는 자유공간이라 통과"라는
            //  거짓 결과가 나온다.
            if (options.StartY < options.MinY || options.StartY > options.MaxY)
            {
                return new CleanRunResult(false, System.Array.Empty<bool>(), options.StartX, 0f, 0, 0f);
            }
            //  출발: 세로 속도 0. 높이는 <b>눈금에 붙이지 않은 그대로</b>
            //  넣는다 — 여기서 반올림하면 첫 틱부터 진짜 물리와 최대 반 칸 어긋난 채 출발한다.
            if (seedIsFree(options.StartX, options.StartY) == false)
            {
                return new CleanRunResult(false, System.Array.Empty<bool>(), options.StartX, 0f, 0, 0f);
            }
            int seed = grid.StateIndex(grid.HeightBucket(options.StartY), grid.SpeedBucket(0f));
            current[seed] = options.StartY;
            currentV[seed] = 0f;
            currentLive.Add(seed);
            //  버퍼 둘을 번갈아 쓴다 — 열마다 상태표 전체를 새로 만들고 NaN으로 채우는 비용이 칸 수에
            //  비례해서, 높이 범위가 넓은 코스에서 탐색 시간의 대부분이 됐다. 다 쓴 칸만 비운다.
            var spare = NewColumn(grid.StateCount);
            var spareV = new float[grid.StateCount];
            var spareLive = new List<int>();
            //  열마다 살아남은 상태를 쌓아 둔다 — 되짚기(ExtractFlaps)가 이걸 뒤에서부터
            //  앞으로 훑으며 직전 상태를 계산해 낸다. 시드(출발) 열도 포함.
            var columns = new List<Column> { Archive(current, currentV, currentLive, grid, out _, out _) };

            float narrowestX = 0f, narrowestSpan = 0f;
            int narrowestCount = int.MaxValue;
            //  회랑 폭은 "고칠 자리"를 가리키는 진단값이라, 아직 상태 수가 불어나는
            //  출발 직후 과도기에는 재지 않는다 — 그 구간의 최솟값은 항상 시드 근처일
            //  뿐 진짜 병목이 아니다. 늘어나길 멈춘(=정체되거나 줄어든) 첫 열부터 잰다.
            int previousLiveCount = 1;
            bool measuringNarrowest = false;

            for (int column = 0; column < grid.ColumnCount; column++)
            {
                float x = grid.ColumnX(column);
                float nextX = grid.ColumnX(column + 1);
                var next = spare;
                var nextV = spareV;
                var nextLive = spareLive;
                bool any = false;

                for (int li = 0; li < currentLive.Count; li++)
                {
                    int state = currentLive[li];
                    float y = current[state], vy = currentV[state];
                    for (int f = 0; f < 2; f++)
                    {
                        if (TryAdvance(grid, tickIsFree, air, x, y, vy, f == 1, options, next, nextV, nextLive))
                        {
                            any = true;
                        }
                    }
                }

                if (any == false)
                {
                    return new CleanRunResult(false, System.Array.Empty<bool>(), nextX,
                                              narrowestX, narrowestCount == int.MaxValue ? 0 : narrowestCount,
                                              narrowestSpan);
                }

                columns.Add(Archive(next, nextV, nextLive, grid, out int liveCount, out float liveSpan));
                if (measuringNarrowest == false && liveCount <= previousLiveCount)
                {
                    measuringNarrowest = true;
                }
                if (measuringNarrowest && liveCount < narrowestCount)
                {
                    narrowestCount = liveCount;
                    narrowestSpan = liveSpan;
                    narrowestX = nextX;
                }
                previousLiveCount = liveCount;

                //  다 쓴 버퍼는 쓴 칸만 NaN으로 되돌려 다음 열의 빈 버퍼로 돌린다. 속도 버퍼는
                //  안 비운다 — 높이가 NaN인 칸의 속도는 아무도 읽지 않는다.
                for (int li = 0; li < currentLive.Count; li++) { current[currentLive[li]] = float.NaN; }
                currentLive.Clear();
                spare = current;
                spareV = currentV;
                spareLive = currentLive;
                current = next;
                currentV = nextV;
                currentLive = nextLive;
            }

            //  도달 가능하면 회랑 진단은 의미가 없다 — "막힌 이유"를 보여주는 값이지 성공
            //  경로의 성질이 아니다.
            bool[] flaps = ExtractFlaps(grid, tickIsFree, air, columns, options);
            return new CleanRunResult(true, flaps, 0f, 0f, 0, 0f);
        }

        /// <summary>
        /// 탐색이 찾은 날갯짓 순서를 <b>탐색과 같은 산술로</b> 다시 굴려, 틱마다 탐색이 믿은
        /// 높이를 낸다. 돌려주는 배열의 [0]은 출발 높이고 [t]는 t번째 틱을 밟은 뒤다
        /// (<see cref="CleanRunResult.Flaps"/>의 i번째가 t=i+1 틱이다 — 재생이 틱을 1부터
        /// 세는 것과 같다).
        ///
        /// <para>탐색은 경로만 돌려주고 틱별 높이는 안 들고 있으므로 여기서 다시 굴린다.
        /// 눈금 반올림이 없으므로 이 값은 <b>진짜 커널 재생과 같아야 한다</b> — 갈린다면
        /// 두 곳의 산술이 다르다는 뜻이고, 그 차이 자체가 찾아야 할 결함이다.</para>
        /// </summary>
        public static float[] PathHeights(in CleanRunOptions options, IReadOnlyList<bool> flaps,
                                          AirflowProbe air = null)
        {
            var grid = new SearchGrid(options);
            int count = flaps == null ? 0 : flaps.Count;
            var heights = new float[count + 1];

            //  Run()의 시드와 같다 — 세로 속도 0, 높이는 출발값 그대로.
            float y = options.StartY, vy = 0f;
            heights[0] = y;

            for (int i = 0; i < count; i++)
            {
                //  TryAdvance와 같은 계산이다(자유공간 검사만 빠졌다 — 이미 통과한 경로를
                //  되짚는 것이라 다시 물을 것이 없다). 기류도 같은 자리(이동 전)에서 묻는다.
                vy = NextSpeed(options, air, grid.ColumnX(i), y, vy, flaps[i]);
                y = FlappyTickMath.AdvanceHeight(y, vy, options.TickSeconds);
                heights[i + 1] = y;
            }
            return heights;
        }

        //  탐색·되짚기·PathHeights가 모두 이 한 줄로 세로 속도를 구한다 — 셋이 갈리면 되짚기가
        //  정방향이 간 길을 못 찾거나, 믿는 높이가 탐색과 달라진다.
        static float NextSpeed(in CleanRunOptions o, AirflowProbe air, float x, float y, float vy, bool flap)
            => FlappyTickMath.NextVerticalSpeed(vy, flap, air == null ? LOP.FlappyAirflowKind.None : air(x, y),
                                                o.FlapImpulse, o.Gravity, o.MaxFallSpeed, o.TickSeconds,
                                                o.UpAccel, o.RiseCap, o.ShaftGravityMult);

        /// <summary>한 열의 생존 상태 — 열쇠 칸 번호와 <b>그 칸에 남은 정확한 높이·세로 속도</b>.
        /// 칸 번호는 오름차순이다(되짚기가 "마지막 열의 가장 낮은 생존 상태"에서 시작한다는
        /// 규칙이 이 순서에 기댄다).</summary>
        readonly struct Column
        {
            public readonly int[] States;
            public readonly float[] Heights;
            public readonly float[] Speeds;

            public Column(int[] states, float[] heights, float[] speeds)
            {
                States = states;
                Heights = heights;
                Speeds = speeds;
            }
        }

        //  빈 칸은 NaN으로 표시한다 — 0은 못 쓴다. 높이 0m는 멀쩡한 값이라 빈 칸과 구별이 안 된다.
        static float[] NewColumn(int stateCount)
        {
            var column = new float[stateCount];
            for (int i = 0; i < stateCount; i++)
            {
                column[i] = float.NaN;
            }
            return column;
        }

        //  열 하나를 되짚기용으로 압축한다. 살아 있는 칸만 담으므로, 열마다 상태표 전체를
        //  들고 있을 때와 달리 긴 코스·높은 대역에서도 메모리가 생존 상태 수에만 비례한다.
        //  같은 한 번의 훑기로 회랑 진단값(생존 수·걸친 높이 폭)도 같이 낸다.
        static Column Archive(float[] dense, float[] denseSpeeds, List<int> live, SearchGrid grid,
                              out int count, out float span)
        {
            //  칸 번호 오름차순이어야 한다 — 되짚기가 "가장 낮은 칸부터" 고르는 전제다.
            live.Sort();
            count = live.Count;
            int lo = int.MaxValue, hi = int.MinValue;
            var states = new int[count];
            var heights = new float[count];
            var speeds = new float[count];
            for (int n = 0; n < count; n++)
            {
                int i = live[n];
                grid.Decode(i, out int bucket, out _);
                if (bucket < lo) { lo = bucket; }
                if (bucket > hi) { hi = bucket; }
                states[n] = i;
                heights[n] = dense[i];
                speeds[n] = denseSpeeds[i];
            }
            span = count == 0 ? 0f : (hi - lo) * grid.HeightGrid;
            return new Column(states, heights, speeds);
        }

        //  뒤에서 앞으로 한 경로를 뽑는다. 열마다 정확한 (높이, 속도)를 남겨 두어 직전 상태를 앞으로
        //  굴려 맞춰 볼 수 있으므로 부모 포인터가 필요 없다.
        //  마지막 열의 아무 생존 상태에서 시작해, 매 단계 직전 열의 후보를 앞으로 굴려 맞는 것을 고른다.
        static bool[] ExtractFlaps(SearchGrid grid, TickSweepProbe tickIsFree, AirflowProbe air,
                                   List<Column> columns, in CleanRunOptions options)
        {
            int last = columns.Count - 1;
            if (columns[last].States.Length == 0)
            {
                return System.Array.Empty<bool>();
            }
            //  마지막 열의 가장 낮은 생존 상태에서 시작한다(States가 칸 번호 오름차순이다).
            //  칸 번호뿐 아니라 <b>그 칸에 남은 정확한 높이·속도</b>도 같이 들고 내려간다 — 아래에서
            //  직전 상태를 고를 때 "칸이 같다"만으로는 모자라기 때문이다(같은 칸에 들어왔다가
            //  밀려난 후보가 있을 수 있다).
            int target = columns[last].States[0];
            float targetY = columns[last].Heights[0];
            float targetV = columns[last].Speeds[0];

            var flaps = new bool[last];
            for (int column = last; column > 0; column--)
            {
                float previousX = grid.ColumnX(column - 1);
                Column previous = columns[column - 1];
                bool found = false;
                for (int i = 0; i < previous.States.Length && found == false; i++)
                {
                    int state = previous.States[i];
                    float y = previous.Heights[i];
                    float v = previous.Speeds[i];

                    //  두 갈래를 그대로 굴려 목표 상태에 떨어지는지 본다 — 정방향과 같은 규칙이라
                    //  둘이 어긋날 수 없다.
                    for (int flap = 0; flap < 2 && found == false; flap++)
                    {
                        float nvy = NextSpeed(options, air, previousX, y, v, flap == 1);
                        float ny = FlappyTickMath.AdvanceHeight(y, nvy, options.TickSeconds);
                        if (ny < options.MinY || ny > options.MaxY) { continue; }
                        if (grid.StateIndex(grid.HeightBucket(ny), grid.SpeedBucket(nvy)) != target) { continue; }
                        //  같은 칸에 닿긴 했지만 더 높은 쪽에 밀려난 후보를 걸러낸다. 이걸 빼면
                        //  되짚기가 정방향이 실제로 남긴 값과 다른 상태를 고르고, 그 뒤로는
                        //  탐색이 검사한 적 없는 궤적이 나온다. 속도도 정확히 같아야 한다 —
                        //  기류 안에선 한 칸에 속도가 조금씩 다른 상태가 들어온다.
                        if (ny != targetY || nvy != targetV) { continue; }
                        //  이 검사는 지금 규칙에선 못 걸린다 — 위 조건을 다 통과한 후보는 출발
                        //  높이·속도가 같으므로 정방향이 이미 자유롭다고 확인한 바로 그 선분이다.
                        //  그래도 남겨 두는 건 되짚기 후보 선택 규칙(지금은 칸 번호가 가장 낮은 것
                        //  우선)이 바뀌면 이 전제가 깨져 검사가 다시 의미를 가질 수 있어서다.
                        if (tickIsFree(previousX, y, nvy) == false) { continue; }

                        flaps[column - 1] = flap == 1;
                        target = state;
                        targetY = y;
                        targetV = v;
                        found = true;
                    }
                }
                if (found == false)
                {
                    //  일어나면 정방향과 역방향이 다른 규칙을 쓴다는 뜻이다 — 여기서 조용히 빈
                    //  배열을 돌려주면 Run()은 그대로 Reachable=true를 내면서 Flaps만 비게
                    //  되어, 부르는 쪽이 "성공"과 "성공이라는데 되짚기는 깨졌다"를 구분할
                    //  수 없다. 그래서 조용히 넘기지 않고 바로 터뜨린다.
                    throw new System.InvalidOperationException(
                        $"CleanRunSearch.ExtractFlaps: {column - 1}번째 열에서 되짚을 직전 상태를 " +
                        "못 찾았다 — 정방향과 역방향이 다른 규칙을 쓴다는 뜻이다.");
                }
            }
            return flaps;
        }

        //  한 스텝 나아가 본다. 몸이 스치면 그 갈래를 버린다.
        static bool TryAdvance(SearchGrid grid, TickSweepProbe tickIsFree, AirflowProbe air, float x, float y,
                               float vy, bool flap, in CleanRunOptions options,
                               float[] nextY, float[] nextV, List<int> nextLive)
        {
            //  진짜 커널(FlappyMapPlayabilityCheck.Step → KinematicMover)이 하는 것과 <b>같은
            //  산술</b>이다: 이동 전 자리의 기류로 세로 속도를 한 틱 갱신하고, 그 속도로 한 틱
            //  움직인다. 높이·속도를 눈금에 붙이지 않고 그대로 이어 간다 — 붙이면 어긋난 값이
            //  다음 틱의 <i>입력</i>이 되어 편향이 쌓이고, 그러면 찾은 경로가 재생에서 깨진다.
            float nvy = NextSpeed(options, air, x, y, vy, flap);
            float ny = FlappyTickMath.AdvanceHeight(y, nvy, options.TickSeconds);
            if (ny < options.MinY || ny > options.MaxY)
            {
                return false;
            }
            //  <b>닿았나를 커널과 같은 자로 잰다</b> — 도착점을 눈금에 붙여 찍는 것이 아니라
            //  한 틱 동안 캡슐을 쓸어 본다(벽에서 0.02m 띄우는 여유까지 커널 그대로다).
            //  점 검사는 커널보다 관대해서, 그것으로 찾은 경로가 재생에서 벽에 걸렸다.
            if (tickIsFree(x, y, nvy) == false)
            {
                return false;
            }
            int index = grid.StateIndex(grid.HeightBucket(ny), grid.SpeedBucket(nvy));
            //  눈금은 여기서만 쓴다 — "이미 가 본 상태인가"의 열쇠다. 같은 칸에 서로 다른
            //  정확한 상태가 들어오면 <b>더 높은 쪽</b> 하나만 남긴다(속도도 그 쪽 값으로).
            //
            //  왜 높은 쪽인가: 어느 쪽도 우월하지 않아서 <i>순서에 안 흔들리는</i> 쪽을 고른
            //  것뿐이다. 기류 밖에선 같은 칸이면 속도가 똑같아서 두 미래는 0.1m 미만만큼
            //  위아래로 나란히 옮긴 <i>같은 곡선</i>이다 — 바닥이 위험한 자리에선 높은 쪽이,
            //  천장이 위험한 자리에선 낮은 쪽이 살아남는다. "먼저 도달한 쪽"으로 하면 상태를
            //  훑는 순서가 답에 새어 들지만, 값으로 정하면 순서와 무관하게 늘 같은 답이 나온다.
            //
            //  <b>버리는 쪽이 "실제로 있는 경로를 놓치는" 유일하게 남은 원인이다</b>(자유공간
            //  검사는 이제 커널 그 자체라 더는 원인이 아니다). 밀려난 쪽으로만 빠져나가는 길이
            //  있으면 탐색은 그것을 못 본다 — 그래서 이 탐색의 ❌는 "없다"가 아니라 "이 근사
            //  아래서 못 찾았다"이다.
            if (float.IsNaN(nextY[index]))
            {
                nextLive.Add(index);
                nextY[index] = ny;
                nextV[index] = nvy;
            }
            else if (ny > nextY[index])
            {
                nextY[index] = ny;
                nextV[index] = nvy;
            }
            return true;
        }

        //  선분 하나를 이보다 잘게 찍어야 한다면 입력이 잘못된 것이다 — 실제 값(한 틱에
        //  x로 0.22m·y로 최대 0.6m, 눈금 0.1m)으로는 8이면 끝나고, 테스트가 쓰는 가장
        //  촘촘한 눈금(0.001m)으로도 512다. 1만은 그 20배라 정상 입력을 막을 일이 없다.
        internal const int MaxSegmentSamples = 10000;

        //  한 틱 사이 몸이 지나는 선분을 눈금 간격으로 찍어 본다. 끝점만 보면 얇은 벽을 통과한다.
        //  internal — BotPilot의 천장 가드도 아치를 틱마다 훑을 때 같은 스윕을 쓴다(같은 어셈블리라
        //  이걸로 충분하다. 테스트를 위해 다른 어셈블리로 옮기지 않는다).
        internal static bool SegmentIsFree(FreeSpaceProbe isFree, float x0, float y0, float x1, float y1, float grid)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float length = UnityEngine.Mathf.Sqrt(dx * dx + dy * dy);
            int samples = UnityEngine.Mathf.CeilToInt(length / grid) + 1;
            //  표본 수 상한을 아치 틱 상한(BotPilot.ArcTickLimit)과 같은 모양으로 여기에 둔다 —
            //  입력에서 유도하고, 넘으면 던진다. 여기 두는 이유는 표본 수가 여기서 정해지기
            //  때문이다: 부르는 쪽은 선분 길이만 알지 그것이 몇 번의 프로브가 되는지 모른다.
            //  틱 수만 막아 두면 부족하다 — FlapImpulse도 Gravity처럼 MasterData에서 검증 없이
            //  복사되므로(FlappyMapPlayabilityCheck), 임펄스가 크면 틱 수는 상한 안이면서
            //  선분 하나가 수천 m로 늘어나 프로브가 억 단위로 터진다.
            if (samples > MaxSegmentSamples || samples < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(grid), grid,
                    $"segment of length {length} would need {samples} samples at this grid " +
                    $"(limit {MaxSegmentSamples}) — the segment is far too long for the grid.");
            }
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                if (isFree(x0 + dx * t, y0 + dy * t) == false)
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>상태 (높이칸, 세로속도칸)을 정수 하나로 누르는 규칙.</summary>
    internal sealed class SearchGrid
    {
        /// <summary>세로속도 칸 폭. 기류 밖에서 나오는 속도들(출발 0에서 떨어지는 줄, 날갯짓 뒤
        /// 떨어지는 줄)은 서로 이 폭보다 훨씬 멀다(실제 설정에서 가장 가까운 둘이 0.28 m/s) — 그래서
        /// 한 칸에 합쳐지는 건 둘 다 종단속도에 닿아 속도가 똑같아졌을 때뿐이다.</summary>
        public const float SpeedGrid = 0.05f;

        readonly CleanRunOptions options;
        readonly float minSpeed;

        public readonly int HeightBucketCount;
        public readonly int SpeedBucketCount;
        public readonly int StateCount;
        public readonly int ColumnCount;
        public readonly float StepX;

        //  컬럼마다의 x를 <b>미리 누적해</b> 담아 둔다.
        //
        //  <b>왜 곱셈으로 구하지 않나(StartX + StepX * column).</b> 수학적으로는 같지만 부동소수에서
        //  다르다 — 재생(진짜 커널)은 매 틱 x에 StepX를 <i>더해</i> 나아가므로, 곱셈으로 구한 x는
        //  틱이 쌓일수록 재생의 x와 벌어진다. 실측: 2848틱 1.1cm · 3683틱 2.2cm · 4267틱 3.0cm.
        //  커널이 벽에서 띄우는 여유가 0.02m라, 3cm면 스치는 판정이 뒤집힌다 — 탐색이 자유라고
        //  본 틱에서 재생이 벽에 걸렸다(2026-09-23, 코스를 60초→90초로 늘리자 드러났다).
        //
        //  높이(y)는 2026-09-14에 같은 이유로 "정확한 값을 들고 다니게" 고쳤다. 이것이 그
        //  <b>나머지 절반</b>이다.
        private readonly float[] columnX;

        /// <summary><paramref name="column"/>번째 틱의 x. 재생과 <b>같은 방식으로</b> 누적한 값이다.</summary>
        public float ColumnX(int column)
        {
            if (column < 0) { return columnX[0]; }
            return column < columnX.Length ? columnX[column] : columnX[columnX.Length - 1];
        }
        /// <summary>상태를 묶는 열쇠 칸의 높이 폭. 이제 <b>열쇠에만</b> 쓴다 — 상태가 들고
        /// 다니는 높이는 여기에 안 붙는다.</summary>
        public readonly float HeightGrid;

        public SearchGrid(in CleanRunOptions options)
        {
            this.options = options;
            HeightGrid = options.HeightGrid;
            StepX = options.ForwardSpeed * options.TickSeconds;
            ColumnCount = UnityEngine.Mathf.CeilToInt((options.FinishX - options.StartX) / StepX);
            HeightBucketCount = UnityEngine.Mathf.CeilToInt((options.MaxY - options.MinY) / options.HeightGrid) + 1;

            //  재생이 하는 것과 같은 더하기로 채운다. 한 칸 더 잡는 이유는 마지막 컬럼의
            //  "다음 x"(nextX)까지 같은 표에서 읽기 위해서다.
            columnX = FlappyTickMath.ColumnXTable(options.StartX, StepX, ColumnCount + 2);

            //  세로 속도는 종단속도(−MaxFallSpeed)와 날갯짓·기류 상한 중 큰 쪽 사이에만 있다.
            minSpeed = -options.MaxFallSpeed;
            float maxSpeed = System.Math.Max(options.FlapImpulse, options.RiseCap);
            SpeedBucketCount = UnityEngine.Mathf.CeilToInt((maxSpeed - minSpeed) / SpeedGrid) + 1;
            StateCount = HeightBucketCount * SpeedBucketCount;
        }

        public int HeightBucket(float y)
        {
            int bucket = UnityEngine.Mathf.RoundToInt((y - options.MinY) / options.HeightGrid);
            if (bucket < 0) { return 0; }
            if (bucket >= HeightBucketCount) { return HeightBucketCount - 1; }
            return bucket;
        }

        public int SpeedBucket(float vy)
        {
            int bucket = UnityEngine.Mathf.RoundToInt((vy - minSpeed) / SpeedGrid);
            if (bucket < 0) { return 0; }
            if (bucket >= SpeedBucketCount) { return SpeedBucketCount - 1; }
            return bucket;
        }

        public int StateIndex(int heightBucket, int speedBucket) => heightBucket * SpeedBucketCount + speedBucket;

        public void Decode(int state, out int heightBucket, out int speedBucket)
        {
            speedBucket = state % SpeedBucketCount;
            heightBucket = state / SpeedBucketCount;
        }
    }
}
