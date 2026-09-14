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
    /// <para>순서가 전부다: <b>중력을 한 번 빼고 → 종단속도로 자르고 → 날갯짓이면 덮어쓴다.</b>
    /// 날갯짓은 더하기가 아니라 <i>덮어쓰기</i>(그때까지의 세로 속도를 버린다)라서 맨 뒤여야 한다.</para>
    /// </summary>
    public static class FlappyTickMath
    {
        public static float NextVerticalSpeed(float verticalSpeed, bool flap,
                                              float flapImpulse, float gravity,
                                              float maxFallSpeed, float tickSeconds)
        {
            float vy = verticalSpeed - gravity * tickSeconds;
            if (vy < -maxFallSpeed)
            {
                vy = -maxFallSpeed;
            }
            if (flap)
            {
                vy = flapImpulse;
            }
            return vy;
        }

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

        public CleanRunOptions(float startX, float startY, float finishX, float minY, float maxY,
                               float forwardSpeed, float flapImpulse, float gravity, float maxFallSpeed,
                               float tickSeconds, float heightGrid)
        {
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
    /// ③ 세로 속도가 연속값이 아니라 사다리라(날갯짓 뒤 몇 틱 지났나로 완전히 결정) 근사가 필요 없다.</para>
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
    /// <para>남은 근사는 둘뿐이다 — ① 같은 열쇠 칸에 든 정확한 상태 중 하나만 남긴다
    /// (<see cref="TryAdvance"/> 주석 참고), ② 자유공간 프로브가 격자에 스냅해 잰다.
    /// 그래서 <b>✅(재생까지 통과)는 증명이지만 ❌는 여전히 "이 근사 아래서 못 찾았다"</b>이다.</para>
    /// </summary>
    public static class CleanRunSearch
    {
        public static CleanRunResult Run(in CleanRunOptions options, FreeSpaceProbe isFree)
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
            //  출발 높이 자체가 허용 범위 밖이면 그대로 실패 — HeightBucket이 조용히 경계로
            //  밀어 넣어 버리면 "다른 자리에서 시드해 놓고 진짜 출발지는 자유공간이라 통과"라는
            //  거짓 결과가 나온다.
            if (options.StartY < options.MinY || options.StartY > options.MaxY)
            {
                return new CleanRunResult(false, System.Array.Empty<bool>(), options.StartX, 0f, 0, 0f);
            }
            //  출발: 아직 날갯짓 안 한 사다리의 첫 칸. 높이는 <b>눈금에 붙이지 않은 그대로</b>
            //  넣는다 — 여기서 반올림하면 첫 틱부터 진짜 물리와 최대 반 칸 어긋난 채 출발한다.
            if (isFree(options.StartX, options.StartY) == false)
            {
                return new CleanRunResult(false, System.Array.Empty<bool>(), options.StartX, 0f, 0, 0f);
            }
            current[grid.StateIndex(grid.HeightBucket(options.StartY), ladder: 1, rung: 0)] = options.StartY;
            //  열마다 살아남은 상태를 쌓아 둔다 — 되짚기(ExtractFlaps)가 이걸 뒤에서부터
            //  앞으로 훑으며 직전 상태를 계산해 낸다. 시드(출발) 열도 포함.
            var columns = new List<Column> { Archive(current, grid, out _, out _) };

            float narrowestX = 0f, narrowestSpan = 0f;
            int narrowestCount = int.MaxValue;
            //  회랑 폭은 "고칠 자리"를 가리키는 진단값이라, 아직 상태 수가 불어나는
            //  출발 직후 과도기에는 재지 않는다 — 그 구간의 최솟값은 항상 시드 근처일
            //  뿐 진짜 병목이 아니다. 늘어나길 멈춘(=정체되거나 줄어든) 첫 열부터 잰다.
            int previousLiveCount = 1;
            bool measuringNarrowest = false;

            for (int column = 0; column < grid.ColumnCount; column++)
            {
                float x = options.StartX + grid.StepX * column;
                float nextX = x + grid.StepX;
                var next = NewColumn(grid.StateCount);
                bool any = false;

                for (int state = 0; state < grid.StateCount; state++)
                {
                    float y = current[state];
                    if (float.IsNaN(y))
                    {
                        continue;
                    }
                    grid.Decode(state, out _, out int ladder, out int rung);

                    //  날갯짓 안 함 — 같은 사다리의 다음 칸.
                    if (TryAdvance(grid, isFree, x, y, ladder, rung + 1, options, next))
                    {
                        any = true;
                    }
                    //  날갯짓 — 사다리 0의 첫 칸으로 갈아탄다.
                    if (TryAdvance(grid, isFree, x, y, ladder: 0, rung: 0, options, next))
                    {
                        any = true;
                    }
                }

                if (any == false)
                {
                    return new CleanRunResult(false, System.Array.Empty<bool>(), nextX,
                                              narrowestX, narrowestCount == int.MaxValue ? 0 : narrowestCount,
                                              narrowestSpan);
                }

                columns.Add(Archive(next, grid, out int liveCount, out float liveSpan));
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

                current = next;
            }

            //  도달 가능하면 회랑 진단은 의미가 없다 — "막힌 이유"를 보여주는 값이지 성공
            //  경로의 성질이 아니다.
            bool[] flaps = ExtractFlaps(grid, isFree, columns, options);
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
        public static float[] PathHeights(in CleanRunOptions options, IReadOnlyList<bool> flaps)
        {
            var grid = new SearchGrid(options);
            int count = flaps == null ? 0 : flaps.Count;
            var heights = new float[count + 1];

            //  Run()의 시드와 같다 — 아직 날갯짓 안 한 사다리(1)의 첫 칸, 높이는 출발값 그대로.
            int ladder = 1;
            int rung = 0;
            float y = options.StartY;
            heights[0] = y;

            for (int i = 0; i < count; i++)
            {
                //  날갯짓이면 사다리 0의 첫 칸으로 갈아타고, 아니면 같은 사다리의 다음 칸.
                //  TryAdvance가 하는 것과 같은 계산이다(자유공간 검사만 빠졌다 — 여기서는
                //  이미 통과한 경로를 되짚는 것이라 다시 물을 것이 없다).
                if (flaps[i]) { ladder = 0; rung = 0; }
                else { rung = rung + 1; }
                rung = grid.ClampRung(rung);
                y = FlappyTickMath.AdvanceHeight(y, grid.Speed(ladder, rung), options.TickSeconds);
                heights[i + 1] = y;
            }
            return heights;
        }

        /// <summary>한 열의 생존 상태 — 열쇠 칸 번호와 <b>그 칸에 남은 정확한 높이</b>.
        /// 칸 번호는 오름차순이다(되짚기가 "마지막 열의 가장 낮은 생존 상태"에서 시작한다는
        /// 규칙이 이 순서에 기댄다).</summary>
        readonly struct Column
        {
            public readonly int[] States;
            public readonly float[] Heights;

            public Column(int[] states, float[] heights)
            {
                States = states;
                Heights = heights;
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
        static Column Archive(float[] dense, SearchGrid grid, out int count, out float span)
        {
            count = 0;
            int lo = int.MaxValue, hi = int.MinValue;
            for (int i = 0; i < dense.Length; i++)
            {
                if (float.IsNaN(dense[i])) { continue; }
                count++;
                grid.Decode(i, out int bucket, out _, out _);
                if (bucket < lo) { lo = bucket; }
                if (bucket > hi) { hi = bucket; }
            }
            span = count == 0 ? 0f : (hi - lo) * grid.HeightGrid;

            var states = new int[count];
            var heights = new float[count];
            int n = 0;
            for (int i = 0; i < dense.Length; i++)
            {
                if (float.IsNaN(dense[i])) { continue; }
                states[n] = i;
                heights[n] = dense[i];
                n++;
            }
            return new Column(states, heights);
        }

        //  뒤에서 앞으로 한 경로를 뽑는다. 사다리 덕에 직전 상태가 계산으로 나와 부모 포인터가 필요 없다.
        //  마지막 열의 아무 생존 상태에서 시작해, 매 단계 직전 열의 후보를 앞으로 굴려 맞는 것을 고른다.
        static bool[] ExtractFlaps(SearchGrid grid, FreeSpaceProbe isFree,
                                   List<Column> columns, in CleanRunOptions options)
        {
            int last = columns.Count - 1;
            if (columns[last].States.Length == 0)
            {
                return System.Array.Empty<bool>();
            }
            //  마지막 열의 가장 낮은 생존 상태에서 시작한다(States가 칸 번호 오름차순이다).
            //  높이뿐 아니라 <b>그 칸에 남은 정확한 높이</b>도 같이 들고 내려간다 — 아래에서
            //  직전 상태를 고를 때 "칸이 같다"만으로는 모자라기 때문이다(같은 칸에 들어왔다가
            //  밀려난 후보가 있을 수 있다).
            int target = columns[last].States[0];
            float targetY = columns[last].Heights[0];

            var flaps = new bool[last];
            for (int column = last; column > 0; column--)
            {
                float previousX = options.StartX + grid.StepX * (column - 1);
                Column previous = columns[column - 1];
                bool found = false;
                for (int i = 0; i < previous.States.Length && found == false; i++)
                {
                    int state = previous.States[i];
                    float y = previous.Heights[i];
                    grid.Decode(state, out _, out int ladder, out int rung);

                    //  두 갈래를 그대로 굴려 목표 상태에 떨어지는지 본다 — 정방향과 같은 규칙이라
                    //  둘이 어긋날 수 없다.
                    for (int flap = 0; flap < 2 && found == false; flap++)
                    {
                        int nextLadder = flap == 1 ? 0 : ladder;
                        int nextRung = grid.ClampRung(flap == 1 ? 0 : rung + 1);
                        float ny = FlappyTickMath.AdvanceHeight(y, grid.Speed(nextLadder, nextRung),
                                                                options.TickSeconds);
                        if (ny < options.MinY || ny > options.MaxY) { continue; }
                        if (grid.StateIndex(grid.HeightBucket(ny), nextLadder, nextRung) != target) { continue; }
                        //  같은 칸에 닿긴 했지만 더 높은 쪽에 밀려난 후보를 걸러낸다. 이걸 빼면
                        //  되짚기가 정방향이 실제로 남긴 높이와 다른 높이를 고르고, 그 뒤로는
                        //  탐색이 검사한 적 없는 궤적이 나온다.
                        if (ny != targetY) { continue; }
                        //  이 검사는 지금 규칙에선 못 걸린다 — 위 두 조건을 다 통과한 후보는
                        //  사다리 칸이 같아 속도가 같고 도착 높이도 같으므로, 정방향이 이미
                        //  자유롭다고 확인한 바로 그 선분이다. 그래도 남겨 두는 건 되짚기 후보
                        //  선택 규칙(지금은 칸 번호가 가장 낮은 것 우선)이 바뀌면 이 전제가
                        //  깨져 검사가 다시 의미를 가질 수 있어서다.
                        if (SegmentIsFree(isFree, previousX, y, previousX + grid.StepX, ny,
                                          options.HeightGrid) == false) { continue; }

                        flaps[column - 1] = flap == 1;
                        target = state;
                        targetY = y;
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
        static bool TryAdvance(SearchGrid grid, FreeSpaceProbe isFree, float x, float y,
                               int ladder, int rung, in CleanRunOptions options,
                               float[] next)
        {
            int clamped = grid.ClampRung(rung);
            float vy = grid.Speed(ladder, clamped);
            //  진짜 커널(FlappyMapPlayabilityCheck.Step → KinematicMover)이 하는 것과 <b>같은
            //  산술</b>이다: 세로 속도를 사다리에서 꺼내고(중력 한 번 빼고 종단속도로 자른 값,
            //  날갯짓이면 그 속도를 덮어쓴 값) 그 속도로 한 틱 움직인다. 높이를 눈금에 붙이지
            //  않고 그대로 이어 간다 — 붙이면 최대 반 칸 어긋난 값이 다음 틱의 <i>입력</i>이
            //  되어 편향이 쌓이고, 그러면 찾은 경로가 재생에서 깨진다.
            float ny = FlappyTickMath.AdvanceHeight(y, vy, options.TickSeconds);
            if (ny < options.MinY || ny > options.MaxY)
            {
                return false;
            }
            if (SegmentIsFree(isFree, x, y, x + grid.StepX, ny, options.HeightGrid) == false)
            {
                return false;
            }
            int index = grid.StateIndex(grid.HeightBucket(ny), ladder, clamped);
            //  눈금은 여기서만 쓴다 — "이미 가 본 상태인가"의 열쇠다. 같은 칸에 서로 다른
            //  정확한 높이가 들어오면 <b>더 높은 쪽</b> 하나만 남긴다.
            //
            //  왜 높은 쪽인가: 어느 쪽도 우월하지 않아서 <i>순서에 안 흔들리는</i> 쪽을 고른
            //  것뿐이다. 같은 칸이면 사다리 칸도 같아서 앞으로의 속도 열이 똑같고, 그래서 두
            //  미래는 0.1m 미만만큼 위아래로 나란히 옮긴 <i>같은 곡선</i>이다 — 바닥이 위험한
            //  자리에선 높은 쪽이, 천장이 위험한 자리에선 낮은 쪽이 살아남는다. "먼저 도달한
            //  쪽"으로 하면 상태를 훑는 순서가 답에 새어 들지만, 값으로 정하면 순서와 무관하게
            //  늘 같은 답이 나온다.
            //
            //  <b>버리는 쪽이 "실제로 있는 경로를 놓치는" 유일하게 남은 원인이다</b>(자유공간
            //  프로브의 격자 스냅과 함께). 밀려난 낮은 쪽으로만 빠져나가는 길이 있으면 탐색은
            //  그것을 못 본다 — 그래서 이 탐색의 ❌는 "없다"가 아니라 "이 근사 아래서 못
            //  찾았다"이다.
            if (float.IsNaN(next[index]) || ny > next[index])
            {
                next[index] = ny;
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

    /// <summary>상태 (높이버킷, 사다리, 칸)을 정수 하나로 누르는 규칙과 세로 속도 사다리.</summary>
    internal sealed class SearchGrid
    {
        readonly CleanRunOptions options;
        readonly float[] afterFlap;
        readonly float[] beforeFlap;

        public readonly int HeightBucketCount;
        public readonly int RungCount;
        public readonly int StateCount;
        public readonly int ColumnCount;
        public readonly float StepX;
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

            //  사다리는 −MaxFallSpeed에 닿으면 더 안 변한다. 거기까지만 만들고 그 뒤는 흡수 상태다.
            float drop = options.Gravity * options.TickSeconds;
            RungCount = UnityEngine.Mathf.CeilToInt((options.FlapImpulse + options.MaxFallSpeed) / drop) + 2;
            //  칸 0은 "방금 날갯짓한 직후"(= 날갯짓이 덮어쓴 값, 곧 FlapImpulse)와 "아직 한 번도
            //  안 한 출발"(= 0)이다. 그 뒤 칸은 전부 진짜 커널과 같은 코드로 한 틱씩 굴린 값이다.
            afterFlap = BuildLadder(options, options.FlapImpulse, RungCount);
            beforeFlap = BuildLadder(options, 0f, RungCount);

            StateCount = HeightBucketCount * 2 * RungCount;
        }

        static float[] BuildLadder(in CleanRunOptions options, float first, int count)
        {
            var ladder = new float[count];
            ladder[0] = first;
            for (int i = 1; i < count; i++)
            {
                //  사다리는 "날갯짓 뒤 몇 틱 지났나"를 세는 표라 중간에 날갯짓이 없다(flap: false).
                ladder[i] = FlappyTickMath.NextVerticalSpeed(
                    ladder[i - 1], flap: false, options.FlapImpulse, options.Gravity,
                    options.MaxFallSpeed, options.TickSeconds);
            }
            return ladder;
        }

        //  이 경계값(RungCount − 1) 자체는 테스트로 안 갈린다 — 사다리가 −MaxFallSpeed에서
        //  바닥을 치기 때문에, 마지막 두 칸은 어느 쪽이든 속도가 똑같다. 그래서 여기서
        //  하나 모자라게 눌러도 밖으로 드러나는 움직임은 달라지지 않는다.
        public int ClampRung(int rung) => rung >= RungCount ? RungCount - 1 : rung;

        public float Speed(int ladder, int rung) => ladder == 0 ? afterFlap[rung] : beforeFlap[rung];

        public int HeightBucket(float y)
        {
            int bucket = UnityEngine.Mathf.RoundToInt((y - options.MinY) / options.HeightGrid);
            if (bucket < 0) { return 0; }
            if (bucket >= HeightBucketCount) { return HeightBucketCount - 1; }
            return bucket;
        }

        public int StateIndex(int heightBucket, int ladder, int rung)
            => (heightBucket * 2 + ladder) * RungCount + rung;

        public void Decode(int state, out int heightBucket, out int ladder, out int rung)
        {
            rung = state % RungCount;
            int rest = state / RungCount;
            ladder = rest % 2;
            heightBucket = rest / 2;
        }
    }
}
