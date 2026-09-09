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
    /// <para>높이만 눈금으로 뭉개므로, 찾은 경로는 부르는 쪽이 진짜 커널로 재생해 증명해야 한다.</para>
    ///
    /// <para><b>이 재생 증명이 실제로 걸린 적이 있다(2026-09-07, 실측 맵).</b> 네 스폰 전부
    /// 탐색은 경로를 찾았지만 진짜 커널 재생에서 전부 어긋났다 — 높이 반올림이 틱마다 새
    /// 쪽으로 유리하게 쏠려 187~191틱에 걸쳐 누적되면 허공에 뜬 높이가 약 7m가 된다.
    /// 정직히 "찾았으나 증명 못 함"으로 보고됐을 뿐 도구가 고장 난 건 아니다. 전체 경위는
    /// <c>docs/ROADMAP.md</c>의 "Flappy 맵 플레이 가능성 검사" 항목 참고.</para>
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

            var current = new System.Collections.BitArray(grid.StateCount);
            //  출발 높이 자체가 허용 범위 밖이면 그대로 실패 — HeightBucket이 조용히 경계로
            //  밀어 넣어 버리면 "다른 자리에서 시드해 놓고 진짜 출발지는 자유공간이라 통과"라는
            //  거짓 결과가 나온다.
            if (options.StartY < options.MinY || options.StartY > options.MaxY)
            {
                return new CleanRunResult(false, System.Array.Empty<bool>(), options.StartX, 0f, 0, 0f);
            }
            //  출발: 아직 날갯짓 안 한 사다리의 첫 칸.
            if (isFree(options.StartX, options.StartY) == false)
            {
                return new CleanRunResult(false, System.Array.Empty<bool>(), options.StartX, 0f, 0, 0f);
            }
            current.Set(grid.StateIndex(grid.HeightBucket(options.StartY), ladder: 1, rung: 0), true);
            //  열마다 살아남은 상태를 쌓아 둔다 — 되짚기(ExtractFlaps)가 이걸 뒤에서부터
            //  앞으로 훑으며 직전 상태를 계산해 낸다. 시드(출발) 열도 포함.
            var columns = new List<System.Collections.BitArray> { current };

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
                var next = new System.Collections.BitArray(grid.StateCount);
                bool any = false;

                for (int state = 0; state < grid.StateCount; state++)
                {
                    if (current.Get(state) == false)
                    {
                        continue;
                    }
                    grid.Decode(state, out int heightBucket, out int ladder, out int rung);
                    float y = grid.HeightOf(heightBucket);

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

                columns.Add(next);
                grid.Measure(next, out int liveCount, out float liveSpan);
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

        //  뒤에서 앞으로 한 경로를 뽑는다. 사다리 덕에 직전 상태가 계산으로 나와 부모 포인터가 필요 없다.
        //  마지막 열의 아무 생존 상태에서 시작해, 매 단계 직전 열의 후보를 앞으로 굴려 맞는 것을 고른다.
        static bool[] ExtractFlaps(SearchGrid grid, FreeSpaceProbe isFree,
                                   List<System.Collections.BitArray> columns, in CleanRunOptions options)
        {
            int last = columns.Count - 1;
            int target = -1;
            for (int state = 0; state < grid.StateCount; state++)
            {
                if (columns[last].Get(state)) { target = state; break; }
            }
            if (target < 0)
            {
                return System.Array.Empty<bool>();
            }

            var flaps = new bool[last];
            for (int column = last; column > 0; column--)
            {
                float previousX = options.StartX + grid.StepX * (column - 1);
                bool found = false;
                for (int state = 0; state < grid.StateCount && found == false; state++)
                {
                    if (columns[column - 1].Get(state) == false) { continue; }
                    grid.Decode(state, out int heightBucket, out int ladder, out int rung);
                    float y = grid.HeightOf(heightBucket);

                    //  두 갈래를 그대로 굴려 목표 상태에 떨어지는지 본다 — 정방향과 같은 규칙이라
                    //  둘이 어긋날 수 없다.
                    for (int flap = 0; flap < 2 && found == false; flap++)
                    {
                        int nextLadder = flap == 1 ? 0 : ladder;
                        int nextRung = grid.ClampRung(flap == 1 ? 0 : rung + 1);
                        float ny = y + grid.Speed(nextLadder, nextRung) * options.TickSeconds;
                        if (ny < options.MinY || ny > options.MaxY) { continue; }
                        if (grid.StateIndex(grid.HeightBucket(ny), nextLadder, nextRung) != target) { continue; }
                        //  이 검사는 지금 규칙에선 절대 못 걸린다 — 같은 target에 도달하는
                        //  후보는 전부 같은 높이(=같은 y, 같은 선분)를 거치므로 정방향이 이미
                        //  자유롭다고 확인한 선분을 다시 확인할 뿐이다. 그래도 남겨 두는 건,
                        //  되짚기 후보 선택 규칙(지금은 가장 낮은 상태, 즉 가장 낮은 높이버킷
                        //  우선 — for문이 state를 0부터 오름차순으로 훑다 처음 맞는 것을
                        //  고른다)이 나중에 바뀌면 이 전제가 깨져 검사가 다시 의미를 가질 수
                        //  있어서다.
                        if (SegmentIsFree(isFree, previousX, y, previousX + grid.StepX, ny,
                                          options.HeightGrid) == false) { continue; }

                        flaps[column - 1] = flap == 1;
                        target = state;
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
                               System.Collections.BitArray next)
        {
            int clamped = grid.ClampRung(rung);
            float vy = grid.Speed(ladder, clamped);
            float ny = y + vy * options.TickSeconds;
            if (ny < options.MinY || ny > options.MaxY)
            {
                return false;
            }
            if (SegmentIsFree(isFree, x, y, x + grid.StepX, ny, options.HeightGrid) == false)
            {
                return false;
            }
            next.Set(grid.StateIndex(grid.HeightBucket(ny), ladder, clamped), true);
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

        public SearchGrid(in CleanRunOptions options)
        {
            this.options = options;
            StepX = options.ForwardSpeed * options.TickSeconds;
            ColumnCount = UnityEngine.Mathf.CeilToInt((options.FinishX - options.StartX) / StepX);
            HeightBucketCount = UnityEngine.Mathf.CeilToInt((options.MaxY - options.MinY) / options.HeightGrid) + 1;

            //  사다리는 −MaxFallSpeed에 닿으면 더 안 변한다. 거기까지만 만들고 그 뒤는 흡수 상태다.
            float drop = options.Gravity * options.TickSeconds;
            RungCount = UnityEngine.Mathf.CeilToInt((options.FlapImpulse + options.MaxFallSpeed) / drop) + 2;
            afterFlap = BuildLadder(options.FlapImpulse, drop, options.MaxFallSpeed, RungCount);
            beforeFlap = BuildLadder(0f, drop, options.MaxFallSpeed, RungCount);

            StateCount = HeightBucketCount * 2 * RungCount;
        }

        static float[] BuildLadder(float first, float drop, float maxFall, int count)
        {
            var ladder = new float[count];
            ladder[0] = first;
            for (int i = 1; i < count; i++)
            {
                float v = ladder[i - 1] - drop;
                ladder[i] = v < -maxFall ? -maxFall : v;
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

        public float HeightOf(int bucket) => options.MinY + bucket * options.HeightGrid;

        public int StateIndex(int heightBucket, int ladder, int rung)
            => (heightBucket * 2 + ladder) * RungCount + rung;

        public void Decode(int state, out int heightBucket, out int ladder, out int rung)
        {
            rung = state % RungCount;
            int rest = state / RungCount;
            ladder = rest % 2;
            heightBucket = rest / 2;
        }

        /// <summary>이 열에 살아남은 상태 수와, 그것들이 걸친 높이 폭.</summary>
        public void Measure(System.Collections.BitArray column, out int count, out float span)
        {
            count = 0;
            int lo = int.MaxValue, hi = int.MinValue;
            for (int state = 0; state < StateCount; state++)
            {
                if (column.Get(state) == false) { continue; }
                count++;
                Decode(state, out int bucket, out _, out _);
                if (bucket < lo) { lo = bucket; }
                if (bucket > hi) { hi = bucket; }
            }
            span = count == 0 ? 0f : (hi - lo) * options.HeightGrid;
        }
    }
}
