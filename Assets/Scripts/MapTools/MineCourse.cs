using System;
using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>광산 코스를 짤 때 쓰는 새 물리. 갈림길 굴(날갯짓 2번 포물선)이 이 값으로 그려진다.</summary>
    public readonly struct MinePhysics
    {
        public readonly float Forward, Flap, Gravity, MaxFall, Tick;

        public MinePhysics(float forward, float flap, float gravity, float maxFall, float tick)
        {
            Forward = forward; Flap = flap; Gravity = gravity; MaxFall = maxFall; Tick = tick;
        }
    }

    /// <summary>
    /// 관문 하나. <see cref="X"/>는 파이프 두께의 가운데. <see cref="Low"/>/<see cref="High"/>는 막힘이 끝나는 높이 —
    /// NaN이면 통로 바닥/천장까지 막는다(갈림길 칸 안의 관문만 칸막이·천장 사이로 좁혀진다).
    /// </summary>
    public readonly struct MineGate
    {
        public readonly float X, Width, GapCenter, Gap, Low, High;

        public MineGate(float x, float width, float gapCenter, float gap, float low = float.NaN, float high = float.NaN)
        {
            X = x; Width = width; GapCenter = gapCenter; Gap = gap; Low = low; High = high;
        }
    }

    /// <summary>
    /// 굴 = 틈 폭이 일정한 채로 중심선이 휘는 긴 관문. 물결 터널·롤러코스터·갈림길 2번 점프 굴.
    /// <see cref="Low"/>/<see cref="High"/>는 <see cref="MineGate"/>와 같은 뜻.
    /// </summary>
    public sealed class MineTube
    {
        public float X0, X1, Gap, Low = float.NaN, High = float.NaN;
        public Func<float, float> Center;
    }

    public readonly struct MineRect
    {
        public readonly float X0, X1, Y0, Y1;

        public MineRect(float x0, float x1, float y0, float y1)
        {
            X0 = x0; X1 = x1; Y0 = y0; Y1 = y1;
        }
    }

    /// <summary>⚡ 부스트 패드. <see cref="Duration"/>초 동안 빨라진다.</summary>
    public readonly struct MinePad
    {
        public readonly MineRect Rect;
        public readonly float Duration;

        public MinePad(MineRect rect, float duration)
        {
            Rect = rect; Duration = duration;
        }
    }

    /// <summary>
    /// 고수 갈림길. 가운데 칸막이(<see cref="Base"/> ± 0.6)로 위·아래가 갈린다. 굴 쪽(<see cref="TunnelUp"/>)은
    /// 날갯짓 정확히 2번짜리 굴 + 패드, 반대쪽은 보통 관문. <see cref="TunnelLane"/> = 굴 쪽 칸.
    /// </summary>
    public readonly struct MineFork
    {
        public readonly float X0, X1, Base;
        public readonly bool TunnelUp;
        public readonly MineRect TunnelLane;
        public readonly ForkShape Shape;

        public MineFork(float x0, float x1, float baseY, bool tunnelUp, MineRect tunnelLane, ForkShape shape = ForkShape.Arc)
        {
            X0 = x0; X1 = x1; Base = baseY; TunnelUp = tunnelUp; TunnelLane = tunnelLane; Shape = shape;
        }
    }

    /// <summary>
    /// 빛 알갱이 무더기 하나(spec 2026-10-23). <see cref="X"/>·<see cref="Y"/>는 붙은 관문의 가운데(파이프 두께 가운데, 틈 중심),
    /// <see cref="Points"/>는 거기서 잰 알갱이 자리. 굽기가 이 값 그대로 <c>FlappyMoteCluster</c> 마커를 세운다.
    /// </summary>
    public readonly struct MineMoteCluster
    {
        public readonly float X, Y;
        /// <summary>붙은 관문 틈의 반 높이 — 빛 띠를 벽에 붙여 그리는 데 쓴다.</summary>
        public readonly float GapHalf;
        public readonly bool Big;
        public readonly IReadOnlyList<(float dx, float dy)> Points;

        /// <summary>움직이는 관문(<see cref="MineCourse.Lifts"/>)의 번호 — 굽기가 그 케이지 자식으로 세워 관문과 같이 움직인다. 고정이면 -1.</summary>
        public readonly int Lift;

        /// <summary>
        /// 굴·갱 무더기면 그 굴(통로)의 x별 중심·반 틈 — 벽이 기울어 있어 총량 어림이 알갱이마다 그 x의 벽에서 재야 한다.
        /// 관문 무더기는 null(틈 중심 <see cref="Y"/>, 반 틈 <see cref="GapHalf"/> 그대로).
        /// </summary>
        public readonly Func<float, float> PassageCenter, PassageHalf;

        /// <summary>갈림길 칸 안 무더기면 그 갈림길 번호(<see cref="MineCourse.Forks"/>), 아니면 -1. 한 판엔 칸 하나만 지나간다.</summary>
        public readonly int Fork;

        /// <summary>갈림길의 고수 길 칸(굴) 무더기인가. <see cref="Fork"/>가 -1이면 뜻 없음.</summary>
        public readonly bool ForkTunnel;

        public MineMoteCluster(float x, float y, float gapHalf, bool big, IReadOnlyList<(float dx, float dy)> points, int lift = -1,
                               Func<float, float> passageCenter = null, Func<float, float> passageHalf = null,
                               int fork = -1, bool forkTunnel = false)
        {
            X = x; Y = y; GapHalf = gapHalf; Big = big; Points = points; Lift = lift;
            PassageCenter = passageCenter; PassageHalf = passageHalf; Fork = fork; ForkTunnel = forkTunnel;
        }
    }

    /// <summary>구간 이름과 시작 x(그 구간 첫 장애물의 왼쪽 끝).</summary>
    public readonly struct MineSection
    {
        public readonly string Name;
        public readonly float X;

        public MineSection(string name, float x)
        {
            Name = name; X = x;
        }
    }

    /// <summary>고수 길(굴) 모양(10-13): 날갯짓 두 번 굴 · 무너진 갱도(곧은 굴 + 받침목) · 광차 레일 굴(두 번 솟았다 내려온다).</summary>
    public enum ForkShape { Arc, Collapse, Rail }

    /// <summary>승강기 관문 하나(흔들리는 계단·승강기). <see cref="Gate"/>는 쉬는 자리(가운데). <see cref="Phase"/>는 도.</summary>
    public readonly struct MineLift
    {
        public readonly MineGate Gate;
        public readonly float Amplitude, Period, Phase;

        public MineLift(MineGate gate, float amplitude, float period, float phase)
        {
            Gate = gate; Amplitude = amplitude; Period = period; Phase = phase;
        }
    }

    /// <summary>
    /// 매단 광차 하나 = 진자 하나. 축(<see cref="PivotX"/>, <see cref="PivotY"/>) = 천장 + 1.2, <see cref="Length"/> = 축 ~ 광차 가운데,
    /// <see cref="Amplitude"/>·<see cref="Phase"/>는 도. 광차는 흔들려도 기울지 않는다(수평 사각형 — 사람 판을 받은 시안 그대로).
    /// </summary>
    public readonly struct MinePendulum
    {
        public readonly float PivotX, PivotY, Length, Amplitude, Period, Phase, CartWidth, CartHeight;

        public MinePendulum(float pivotX, float pivotY, float length, float amplitude, float period, float phase, float cartWidth, float cartHeight)
        {
            PivotX = pivotX; PivotY = pivotY; Length = length; Amplitude = amplitude; Period = period; Phase = phase;
            CartWidth = cartWidth; CartHeight = cartHeight;
        }
    }

    /// <summary>낙석 줄 하나. <see cref="X"/> = 바위 가운데 x, <see cref="Ceiling"/> = 천장선, <see cref="Drop"/> = 통로 높이 + 바위. <see cref="Phase"/>는 도.</summary>
    public readonly struct MineRockfall
    {
        public readonly float X, Ceiling, Drop, Size, Speed, Period, Phase;

        public MineRockfall(float x, float ceiling, float drop, float size, float speed, float period, float phase)
        {
            X = x; Ceiling = ceiling; Drop = drop; Size = size; Speed = speed; Period = period; Phase = phase;
        }
    }

    /// <summary>
    /// 광산 코스 한 판의 배치 결과. 통로는 꺾은선 중심(<see cref="CenterAt"/>) ± 반 높이(<see cref="HalfAt"/>).
    /// 빌더는 이것만 보고 굽는다 — 숫자는 <see cref="MineCourseRule.Layout"/>이 프로토타입 그대로 정한다.
    /// </summary>
    public sealed class MineCourse
    {
        /// <summary>통로 반 높이(10-07 원조처럼: 통로 = 화면 — 카메라 20 m 세로 14.56 m의 반. 10-08 카메라를 23 m로 물려 위아래 1 m쯤 바닥·천장이 보인다). 프로토타입은 7.28.</summary>
        public const float BaseHalf = 7.28f;

        /// <summary>좁은 구간·낮은 천장의 가장자리를 넓은 통로로 잇는 거리.</summary>
        public const float EdgeBlend = 3f;

        /// <summary>낮은 천장 구간의 반 높이(10-07 난이도 올림: 2.6 → 2.2).</summary>
        public const float LowHalf = 2.2f;

        /// <summary>낮은 천장 중심선 물결 진폭(10-07 난이도 올림: 1.5 → 3.5, 10-12 사람 판 3.5 → 2.5 — 맵 검사기 봇이 물결 내리막 천장(기울기 1.11)에 박았다. 처방 0.85 이하)과 주기.</summary>
        internal const double LowAmp = 2.5, LowPer = 17;

        public IReadOnlyList<MineGate> Gates;
        public IReadOnlyList<MineTube> Tubes;
        public IReadOnlyList<MineRect> Walls;
        public IReadOnlyList<MinePad> Pads;
        public IReadOnlyList<MineFork> Forks;
        public IReadOnlyList<MineSection> Sections;
        //  움직이는 지형(10-13). 빈 목록으로 시작한다 — 리플렉션으로 만든 합성 코스·옷 테스트의 손 코스가 null로 깨지지 않게.
        public IReadOnlyList<MineLift> Lifts = new List<MineLift>();
        public IReadOnlyList<MinePendulum> Pendulums = new List<MinePendulum>();
        public IReadOnlyList<MineRockfall> Rockfalls = new List<MineRockfall>();
        /// <summary>무너진 갱도 받침목(판정 고체).</summary>
        public IReadOnlyList<MineRect> Props = new List<MineRect>();
        /// <summary>빛 알갱이 무더기. 광산은 다이브 충전 맵이라 비어 있다(spec 2026-10-23-flappy-dive-charge) — 굽기·마커 길은 남긴다.</summary>
        public IReadOnlyList<MineMoteCluster> Motes = new List<MineMoteCluster>();
        public float Length;

        //  통로 모양 원자료 — CenterAt/HalfAt/Breaks가 쓴다. 내부는 double(프로토타입과 같은 셈).
        internal List<(double x, double y)> Points = new List<(double x, double y)>();
        internal List<(double a, double b, double h)> Narrows = new List<(double a, double b, double h)>();
        internal List<(double a, double b)> Lows = new List<(double a, double b)>();

        /// <summary>통로 중심 높이: 꺾은선 + 낮은 천장 안의 물결(3.5·sin, 주기 17 m).</summary>
        public float CenterAt(float x) => (float)CenterAtD(x);

        internal double CenterAtD(double x)
        {
            foreach (var (a, b) in Lows)
            {
                if (x > a && x < b) { return Lin(x) + LowAmp * Math.Sin(2 * Math.PI * (x - a) / LowPer); }
            }
            return Lin(x);
        }

        double Lin(double x)
        {
            if (x <= Points[0].x) { return Points[0].y; }
            for (int i = 1; i < Points.Count; i++)
            {
                if (x <= Points[i].x)
                {
                    var (x0, y0) = Points[i - 1];
                    var (x1, y1) = Points[i];
                    return y0 + (y1 - y0) * (x - x0) / Math.Max(1e-6, x1 - x0);
                }
            }
            return Points[Points.Count - 1].y;
        }

        /// <summary>
        /// 처음으로 통로가 꺾이거나 좁아지기 시작하는 x — 그 앞은 중심이 시작 높이 그대로이고 반 높이가 <see cref="BaseHalf"/>다.
        /// 굽기가 옷 입히는 범위 끝으로 쓴다(10-09: 3막 코스는 입구 바로 뒤가 수직 갱이라 옷은 그 앞 노을 바깥까지만).
        /// </summary>
        public float FlatEnd()
        {
            double end = Length;
            foreach (var (a, _, _) in Narrows) { end = Math.Min(end, a - EdgeBlend); }
            foreach (var (a, _) in Lows) { end = Math.Min(end, a - EdgeBlend); }
            for (int i = 1; i < Points.Count; i++)
            {
                if (Math.Abs(Points[i].y - Points[0].y) > 1e-9) { end = Math.Min(end, Points[i - 1].x); break; }
            }
            return (float)end;
        }

        /// <summary>통로 반 높이: 좁은 구간(수직 갱·굴뚝) → 낮은 천장 → <see cref="BaseHalf"/>. 경계 밖 3 m는 선형 전이.</summary>
        public float HalfAt(float x) => (float)HalfAtD(x);

        internal double HalfAtD(double x)
        {
            foreach (var (a, b, h) in Narrows)
            {
                if (x > a - EdgeBlend && x < b + EdgeBlend) { return Blend(x, a, b, h); }
            }
            foreach (var (a, b) in Lows)
            {
                if (x > a - EdgeBlend && x < b + EdgeBlend) { return Blend(x, a, b, LowHalf); }
            }
            return BaseHalf;
        }

        static double Blend(double x, double a, double b, double h)
        {
            double e = Math.Min(1.0, Math.Max(0.0, Math.Min(x - (a - EdgeBlend), (b + EdgeBlend) - x) / EdgeBlend));
            return BaseHalf + (h - BaseHalf) * e;
        }

        /// <summary>
        /// 바닥·천장을 자를 x 목록(오름차순, 중복 없음): 꺾은선 꼭짓점 + 좁은/낮은 구간 [a−3, b+3]을 <paramref name="step"/>마다
        /// + 0, <see cref="Length"/>. 이 x들 사이를 직선으로 이으면 통로 모양이 된다.
        /// </summary>
        public IReadOnlyList<float> Breaks(float step)
        {
            if (step <= 0f) { throw new ArgumentOutOfRangeException(nameof(step)); }
            var xs = new List<double> { 0, Length };
            foreach (var (x, _) in Points) { xs.Add(x); }
            foreach (var (a, b, _) in Narrows) { AddSpan(xs, a - EdgeBlend, b + EdgeBlend, step); }
            foreach (var (a, b) in Lows) { AddSpan(xs, a - EdgeBlend, b + EdgeBlend, step); }
            xs.Sort();

            var result = new List<float>();
            foreach (double x in xs)
            {
                if (x < 0 || x > Length) { continue; }
                float f = (float)x;
                if (result.Count > 0 && f - result[result.Count - 1] < 1e-4f) { continue; }
                result.Add(f);
            }
            return result;
        }

        static void AddSpan(List<double> xs, double from, double to, double step)
        {
            int n = (int)Math.Ceiling((to - from) / step);
            for (int i = 0; i <= n; i++) { xs.Add(Math.Min(to, from + i * step)); }
        }
    }

    /// <summary>
    /// <b>광산 코스</b> 배치 — 웹 프로토타입 <c>build()</c>의 난수 생성기와 <b>소비 순서</b>를 그대로 옮겼다(구간 순서는
    /// 3막용으로 다시 짰다 — mode 10, 시드 17은 <see cref="Seed"/> 참고). 생성기·소비 순서가 같아야
    /// "프로토타입에서 해 본 그 코스"가 나온다(테스트가 지킨다).
    ///
    /// <para>순서(10-13 동적 지형 1차, 시안 v10 PLAN_ORDER — 446 m): 출발 2관문 → 1막 하강(수직 갱 22 m → 흔들리는 계단 6(4.5 m씩) → 낙석 4줄
    /// → 갈림길⬇ 무너진 갱도) → 2막 갱 바닥(진자 광차 3자리 → 롤러코스터 30 m → 급반전 → 갈림길⬆ 두 번 굴 → 깊은 갱 24 m)
    /// → 3막 탈출(승강기 4 → 굴뚝 24 m → 갈림길⬆ 레일 굴) → 출구. 통과 가능성 증명은 맵 검사 몫이다.
    /// 깊은 갱·굴뚝은 10-12에 28 → 24 m(게임 이동 커널 검사: 10 m에 28 m는 최대 낙하 속도로도 못 내려가고, 굴뚝 28 m는 초당 4번 가까이 쳐야 했다.
    /// 24 m면 초당 3.3번으로 지나간다). 둘이 같이 줄어 출구 높이·구간 x는 그대로다.
    /// 갱 기울기는 최대 낙하(1 m 갈 때 2.5 m)보다 <b>조금 완만하게</b> 2.4(첫 갱 22 m를 8 → 9.2 m, 둘째 갱 24 m/10 m 그대로, 10-12):
    /// 더 가파르면(첫 갱 2.75) 손 떼고 떨어져도 천장이 먼저 내려와 박고(맵 검사기 봇 전부·사람 판), 훨씬 완만하면(2.0) 벽보다 빨리 떨어지는데
    /// 좁은 갱에서 날갯짓 한 번(+10)이 내려오는 천장에 비해 5 m를 튀어 올라 브레이크가 없다 — 둘 다 못 지나간다(게임 커널 검사).
    /// 슬라럼·낮은 천장·긴 통로 코드는 순서에서 빠졌지만 남겨 둔다.</para>
    /// </summary>
    public static class MineCourseRule
    {
        /// <summary>프로토타입 <c>seeded(7 + 9)</c> → 10-09 3막(v30 "0 · 광산 3막")은 <c>seeded(7 + 10)</c> — 관문 1·2·29·마지막 핀이 16으로는 안 맞고 17로만 맞는다.</summary>
        public const uint Seed = 17;

        /// <summary>파이프 두께 · 관문 틈 · 관문 사이 빈 거리(원조 간격 5.4 m − 두께).</summary>
        const double PW = 1.95, GAP = 3.75, FREE = 5.4 - PW;

        /// <summary>날갯짓 두 번 굴의 틈(10-12 사람 판 3.1 → 3.4 — "무조건 부딪힌다", 10-13 동적 지형 3.4 → 2.8 — 공 몸통 한계 2.4 + 0.4,
        /// 10-15 폰 사람 판 "통과 가능한가 싶을 정도" 2.8 → 3.1).</summary>
        const double ArcGap = 3.1;
        /// <summary>무너진 갱도·레일 굴의 틈(한계 3.2 + 0.4).</summary>
        const double ExpertGap = 3.6;
        /// <summary>무너진 갱도: 길이 · 첫 받침목 · 받침목 사이 · 튀어나온 길이 · 폭 · 개수.
        /// 10-15 폰 사람 판 "두 번 굴만큼 어렵다": 사이 3 → 4, 튀어나옴 0.8 → 0.6. 사이 4면 셋째 받침목이 9.5 ~ 10.1로 굴 끝(10)을 넘어
        /// 길이 10 → 11, 첫 받침목 1.5 → 1.0(받침목 1.0 · 5.0 · 9.0, 나가는 여유 1.9 → 1.4). 길이 12면 여유가 그대로지만
        /// 반대쪽 보통 관문이 하나 늘어 난수 소비가 밀리고 뒤 관문 틈이 전부 바뀐다 — 11까지는 관문 4개 그대로다.</summary>
        const double CollapseLen = 11, CollapseFirst = 1.0, CollapseSpacing = 4, CollapseProp = 0.6, CollapsePropWidth = 0.6;
        const int CollapseProps = 3;
        /// <summary>광차 레일 굴: 길이 · 솟는 높이 · 한 번 솟았다 내려오는 길이.</summary>
        const double RailLen = 12, RailRise = 1.6, RailPer = 6;

        /// <summary>고수 길 패드 방의 틈(10-10): 굴을 빠져나오면 땅으로 둘러싸인 조금 넓은 방 → 칸막이 끝에서 합류.</summary>
        const double ChamberGap = 4.6;

        /// <summary>⚡ 패드 부스트 시간(초).</summary>
        const float PadDuration = 1.1f;

        //  지형 (프로토타입 TERRAIN)
        //  DiveNarrow(수직 갱 반 높이) 10-12 사람 판 2.8 → 4.3: 갱 입구에서 떨어지기 시작하는 몸을 내려오는 천장이 먼저 박았다(맵 검사기 처방 "천장 1.5 m 올려라").
        const double DropLen = 8, Drop = 22, DiveNarrow = 4.3;
        const double ClimbLen = 16, Climb = 21, ClimbNarrow = 1.4;
        const double CoasterLen = 45, CoasterAmp = 4, CoasterPer = 30;

        //  10-07 난이도 올림 — 쉬던 구간을 한계 쪽으로. 직선(긴 통로)·S자(롤러코스터)는 한계와 예전 값 사이로 타협.
        /// <summary>사이 관문 틈 중심의 난수 폭(예전 3.0 — 원조처럼 5.3 m 폭에서 제각각).</summary>
        const double ConnBand = 5.3;
        /// <summary>긴 통로 12 m 관문의 틈(예전 3.75, 한계 2.5와 그 사이).</summary>
        const double LongGap = 3.1;
        /// <summary>물결 터널 틈(예전 3.75)·진폭·주기.</summary>
        const double WaveGap = 3.7, WaveAmp = 1.2, WavePer = 16;
        /// <summary>슬라럼 좌우 폭(예전 ±2.0).</summary>
        const double Slalom = 2.6;
        /// <summary>계단 내리막: 관문마다 기준 높이를 내리는 폭(10-10, 4.5 통과 · 5.5 막힘 — 초당 4번 날갯짓 검사).</summary>
        const double StairDown = 4.5;
        /// <summary>계단 관문 사이를 보통(5.4 m)보다 더 띄우는 거리(10-12 사람 판 "무조건 부딪힌다"): 5.4 m 간격에선 4.5 m를 떨어진 뒤 다음 틈 앞에서
        /// 속도를 못 죽여 파이프에 박았다(맵 검사기 봇 전부 — 지형 판정). 8 m면 떨어지고 → 한 번 쳐서 → 꼭대기에서 틈을 지난다.
        /// 10-13 사람 판 +2.6 → +1.4(간격 6.8 m). 계단 틈은 흔들린다(진폭·주기·관문마다 위상).</summary>
        const double StairExtra = 1.4, StairWobble = 1.0, StairPeriod = 3.0, StairPhaseStep = 60;
        /// <summary>급반전 오르내림(예전 ±4.0).</summary>
        //  10-12 사람 판 5.5 → 4.5: 낮은 관문에서 나와 다음 높은 관문까지 5.5 m를 못 올라 맵 검사기 봇 7/10이 박았다(깔때기 0.3 m 밖).
        const double Flip = 4.5;
        /// <summary>급반전 관문 사이를 더 띄우는 거리(10-12, 계단과 같은 이유 — 5.4 m에 ±5.5 m는 맵 검사기 봇이 다음 파이프에 박았다.
        /// 10-13 +2.6 → +1.4). 흔들림은 없다 — ±4.5 m 위에 얹으면 박자 절반에서 불가능(시안 검사 6/12).</summary>
        const double FlipExtra = 1.4;
        /// <summary>급반전 앞 보통 관문 → 첫 급반전 관문 사이를 더 띄우는 거리(10-13 검사: 0 → 2.6, 간격 5.4 → 8 m). 급반전 안 간격(6.8 m)은 그대로다.
        /// 앞 보통 관문 틈이 난수로 1.5 m 낮게 나와 5.4 m 안에 6 m를 올라야 했다 — 그 관문 뒤 살아남는 높이가 0.2 m뿐이라(구간 검사는 통과)
        /// 맵 검사기 봇 8/9가 첫 급반전 관문에 박았다. 6.8 m(+1.4)로는 살아남는 높이 2.1 m까지 넓어졌지만 봇 4/9가 여전히 여기서 박았다.</summary>
        const double FlipLead = 2.6;
        /// <summary>갈림길 끝(칸막이 끝)과 다음 관문 사이에 더 두는 거리(10-12): 위 칸은 칸막이 끝까지 못 내려오는데
        /// 다음 보통 관문이 몇 m 아래 4 m 뒤에 있으면 떨어지다 못 멈춰 박았다(맵 검사기 봇).</summary>
        const double ForkMergeRoom = 3.0;
        /// <summary>롤러코스터 굴 틈(예전 5.0 → 10-07 4.85 → 10-09 3막 4.8. 4.7은 검사에서 거의 못 지난다.
        /// 10-13 4.8 → 4.4 — 길이 30 m 물결 한 번이라 짧다).</summary>
        const double CoasterGap = 4.4;

        /// <summary>승강기: 관문 수 · 관문 사이 더 띄우는 거리(보통 5.4 + 2.6 = 8) · 진폭 · 주기 · 관문마다 위상.</summary>
        const int LiftGates = 4;
        const double LiftExtra = 2.6, LiftAmp = 1.8, LiftPeriod = 3.5, LiftPhaseStep = 90;

        /// <summary>진자 광차: 자리 수 · 자리마다 광차 수 · 자리 사이 · 자리 앞 여유 · 축 높이(천장 위) · 앞뒤 흔들림 · 주기 · 광차마다/자리마다 위상 · 광차 크기.</summary>
        const int PendulumStations = 5, CartsPerStation = 4;   // 10-24 사람 판: 3 → 5자리(코스 늘리기)
        const double StationGap = 10, StationLead = 4, PivotRise = 1.2, CartSwing = 2.5, PendulumPeriod = 2.4;
        const double CartPhaseStep = 90, StationPhaseStep = 180 / Math.PI;   // 자리마다 1 rad(57.3°) — 시안 i·1.0
        const double CartW = 1.6, CartH = 1.2;
        //  줄이 아주 짧을 때 asin이 1을 넘지 않게(시안과 같은 상한 — 지금 값에선 안 걸린다).
        const double MaxSwingSin = 0.95;

        /// <summary>낙석: 줄 수 · 줄 앞 여유 · 줄 사이 · 바위 · 낙하 속도 · 주기 · 줄마다 위상(0.5초 = 90°).</summary>
        const int RockRows = 7;   // 10-24 사람 판: 4 → 7줄(코스 늘리기)
        const double RockLead = 1, RockSpacing = 4.5, RockSize = 1.2, RockSpeed = 9, RockPeriod = 2, RockPhaseStep = 90;
        /// <summary>갈림길 굴 쪽 칸 중심 — 통로가 좁아져 위 굴은 위로 ForkTunnelUp, 아래 굴은 아래로 ForkTunnelDown(비대칭, 예전 둘 다 4.5).</summary>
        const double ForkTunnelUp = 2.6, ForkTunnelDown = 4.6;
        /// <summary>갈림길 반대쪽 보통 칸 관문 틈 중심(통로 중심에서 ±ForkOther, 예전 ±4.5).</summary>
        const double ForkOther = 3.9;
        /// <summary>갈림길 반대쪽 보통 관문 틈 중심의 난수 폭(예전 4.0 → 2.0 → 10-09 3막 3.0 — 검사에서 가장 쉬운 곳이었다).</summary>
        const double ForkBand = 3.0;
        /// <summary>갈림길 반대쪽 보통 관문의 틈(10-09 3막: 원조 3.75 → 3.3). 굴(3.1)보다는 넉넉한 "안전하지만 느린 길".</summary>
        const double ForkGap = 3.3;

        /// <summary>구간 옵션(10-09 3막). 비워 두면 그 구간의 기본값 — 관문 수(슬라럼·계단 내리막), 길이(낮은 천장·롤러코스터·수직 갱·굴뚝), 높이 차(수직 갱·굴뚝).</summary>
        sealed class SectionOptions
        {
            public int? Gates;
            public double? Length;
            public double? Rise;
            /// <summary>갈림길 고수 길 모양(10-13). 비우면 날갯짓 두 번 굴.</summary>
            public ForkShape? Shape;
        }

        /// <summary>
        /// 난수 생성기·소비 순서는 프로토타입 그대로(구간 배치는 3막용 — mode 10, 시드 17). 난수는 관문마다 하나
        /// (기본 관문·긴 통로·갈림길 반대쪽 관문)씩만 뽑는다 — 다른 곳에서 하나라도 더 뽑으면 그 뒤 코스가 통째로 달라진다.
        /// </summary>
        public static MineCourse Layout(MinePhysics p)
        {
            var c = new MineCourse();
            var gates = new List<MineGate>();
            var tubes = new List<MineTube>();
            var walls = new List<MineRect>();
            var pads = new List<MinePad>();
            var forks = new List<MineFork>();
            var sections = new List<MineSection>();
            var lifts = new List<MineLift>();
            var pendulums = new List<MinePendulum>();
            var rockfalls = new List<MineRockfall>();
            var props = new List<MineRect>();
            var pts = c.Points;
            pts.Add((0, 0));

            //  프로토타입과 같은 LCG. uint 넘침이 JS의 ">>> 0"과 같다.
            uint s = Seed;
            double Rnd() { s = unchecked(s * 1664525u + 1013904223u); return s / 4294967296.0; }

            //  커서 = 다음 장애물의 왼쪽 끝.
            double cur = 14, baseY = 0;

            void Put(double w, double center, double gap = GAP)
            {
                gates.Add(new MineGate((float)(cur + w / 2), (float)w, (float)center, (float)gap));
                pts.Add((cur + w / 2, baseY));
                cur += w + FREE;
            }
            //  움직이는 관문: Put과 같은 자리·같은 통로 꺾은선, 다만 Gates가 아니라 Lifts에 넣는다(Gates를 도는 굽기·옷이 고정 파이프를 굽지 않게).
            void Lift(double center, double amp, double period, double phaseDeg)
            {
                lifts.Add(new MineLift(new MineGate((float)(cur + PW / 2), (float)PW, (float)center, (float)GAP),
                                       (float)amp, (float)period, (float)phaseDeg));
                pts.Add((cur + PW / 2, baseY));
                cur += PW + FREE;
            }
            void Norm(int n) { for (int i = 0; i < n; i++) { Put(PW, baseY + (Rnd() - 0.5) * ConnBand); } }
            void Tube(double x0, double x1, Func<double, double> center, double gap, double low = double.NaN, double high = double.NaN)
            {
                tubes.Add(new MineTube { X0 = (float)x0, X1 = (float)x1, Gap = (float)gap, Low = (float)low, High = (float)high,
                                         Center = x => (float)center(x) });
            }

            //  날갯짓 → 같은 높이로 돌아오는 틱 수. 프로토타입 정의(반올림, 30)라 FlapArc.TicksPerArc(31)와 다르다.
            int arcT = (int)Math.Round(2.0 * p.Flap / p.Gravity / p.Tick);

            //  고수 갈림길: 가운데 칸막이로 위·아래가 갈린다. 한쪽은 고수 길 굴(모양은 갈림길마다 — 10-13) + 끝에 ⚡ 패드,
            //  반대쪽은 보통 관문. 굴을 나오면 패드 방(틈 4.6)에서 칸막이 끝과 함께 합류.
            void Fork(bool up, ForkShape shape)
            {
                double side = up ? 1 : -1, fx = cur, laneC = baseY + (up ? ForkTunnelUp : -ForkTunnelDown);
                double top = baseY + MineCourse.BaseHalf + 1, bot = baseY - MineCourse.BaseHalf - 1;

                double ax0 = fx + 3.0, ax1;
                double laneLo = up ? baseY + 0.6 : bot, laneHi = up ? top : baseY - 0.6;
                switch (shape)
                {
                    //  날갯짓 두 번 굴: 입구에서 날갯짓, arcT틱 뒤 다시 — 같은 높이로 두 번 튀어 오른다(한 방 타이밍).
                    case ForkShape.Arc:
                    {
                        var path = new List<(double x, double y)>();
                        double y = laneC, vy = 0, x = ax0;
                        path.Add((x, y));
                        for (int k = 0; k < 2 * arcT; k++)
                        {
                            vy = (k % arcT == 0) ? p.Flap : Math.Max(-(double)p.MaxFall, vy - (double)p.Gravity * p.Tick);
                            y += vy * p.Tick;
                            x += (double)p.Forward * p.Tick;
                            path.Add((x, y));
                        }
                        ax1 = path[path.Count - 1].x;
                        Tube(ax0, ax1, q => PathAt(path, q), ArcGap, laneLo, laneHi);
                        break;
                    }
                    //  무너진 갱도: 곧은 갱도에 받침목이 위·아래 번갈아 튀어나왔다 — 미리 높이를 바꿔 피한다.
                    case ForkShape.Collapse:
                    {
                        ax1 = ax0 + CollapseLen;
                        double lc = laneC;
                        Tube(ax0, ax1, _ => lc, ExpertGap, laneLo, laneHi);
                        for (int j = 0; j < CollapseProps; j++)
                        {
                            double a = ax0 + CollapseFirst + j * CollapseSpacing;
                            bool fromTop = j % 2 == 0;
                            double edge = fromTop ? lc + ExpertGap / 2 : lc - ExpertGap / 2;
                            double y0 = fromTop ? edge - CollapseProp : edge, y1 = fromTop ? edge : edge + CollapseProp;
                            props.Add(new MineRect((float)a, (float)(a + CollapsePropWidth), (float)y0, (float)y1));
                        }
                        break;
                    }
                    //  광차 레일 굴: 바닥에 레일, 굴이 두 번 솟았다 내려온다 — 높이 바꾸기. 끝 높이 = 칸 중심(패드 방).
                    default:
                    {
                        ax1 = ax0 + RailLen;
                        double lc = laneC;
                        Tube(ax0, ax1, q => lc + RailRise * (1 - Math.Cos(2 * Math.PI * (q - ax0) / RailPer)) / 2, ExpertGap, laneLo, laneHi);
                        break;
                    }
                }

                double padX = ax1 + 0.3;
                pads.Add(new MinePad(new MineRect((float)padX, (float)(padX + 1.2), (float)(laneC - 1.4), (float)(laneC + 1.4)), PadDuration));
                double fx1 = padX + 12;   // 부스트 약 10 m가 칸막이 안에서 끝나게
                //  패드 방(10-10, 웹 v33): 굴 끝 ~ 칸막이 끝, 굴 칸 중심 높이 그대로 — 위·아래는 굴과 같은 칸 끝까지 땅.
                double roomY = laneC;
                Tube(ax1, fx1, _ => roomY, ChamberGap, laneLo, laneHi);
                walls.Add(new MineRect((float)fx, (float)fx1, (float)(baseY - 0.6), (float)(baseY + 0.6)));

                for (double gx = fx + 3; gx < fx1 - 2; gx += 5.4)
                {
                    gates.Add(new MineGate((float)(gx + PW / 2), (float)PW, (float)(baseY - side * ForkOther + (Rnd() - 0.5) * ForkBand), (float)ForkGap,
                                           (float)(up ? bot : baseY + 0.6), (float)(up ? baseY - 0.6 : top)));
                }
                forks.Add(new MineFork((float)fx, (float)fx1, (float)baseY, up,
                                       new MineRect((float)fx, (float)fx1, (float)laneLo, (float)laneHi), shape));
                pts.Add((fx1, baseY));
                cur = fx1 + FREE + ForkMergeRoom;
            }

            void Section(string name, SectionOptions o)
            {
                switch (name)
                {
                    case "긴 통로":
                        Put(12, baseY + (Rnd() - 0.5) * 1.5, LongGap);
                        break;
                    //  흔들리는 계단(10-13): 관문마다 4.5 m씩 뚝뚝 — 틈이 천천히 오르내려 들어갈 때를 읽는다. 통로도 계단처럼 따라 내려간다.
                    case "계단 내리막":
                        for (int i = 0; i < (o?.Gates ?? 6); i++) { baseY -= StairDown; Lift(baseY, StairWobble, StairPeriod, i * StairPhaseStep); cur += StairExtra; }
                        break;
                    //  승강기(10-13, 긴 통로 자리): 보통 틈이 위아래로 천천히 — 관문 사이 8 m에서 움직임을 읽고 들어간다.
                    case "승강기":
                        for (int i = 0; i < LiftGates; i++) { Lift(baseY, LiftAmp, LiftPeriod, i * LiftPhaseStep); cur += LiftExtra; }
                        break;
                    //  진자 광차(10-13, 낮은 천장 자리): 한 자리에 작은 광차 넷을 통로 높이 전체에 매달아 다른 박자로 흔든다 — 어느 높이가 비는지 읽는다.
                    case "진자 광차":
                    {
                        for (int i = 0; i < PendulumStations; i++)
                        {
                            double px = cur + StationLead, ceiling = baseY + MineCourse.BaseHalf;
                            for (int k = 0; k < CartsPerStation; k++)
                            {
                                double cy = ceiling - (2 * MineCourse.BaseHalf / CartsPerStation) * (k + 0.5);
                                double len = ceiling - cy + PivotRise;
                                double amp = Math.Asin(Math.Min(MaxSwingSin, CartSwing / len)) * 180 / Math.PI;
                                pendulums.Add(new MinePendulum((float)px, (float)(ceiling + PivotRise), (float)len, (float)amp, (float)PendulumPeriod,
                                                               (float)(k * CartPhaseStep + i * StationPhaseStep), (float)CartW, (float)CartH));
                            }
                            pts.Add((px, baseY));
                            cur += StationGap;
                        }
                        cur += FREE;
                        break;
                    }
                    //  낙석(10-13): 천장에서 바위가 곧게 떨어진다. 예고 없음 — 줄마다 박자를 어긋나게.
                    case "낙석":
                    {
                        for (int i = 0; i < RockRows; i++)
                        {
                            double x0 = cur + RockLead, ceiling = baseY + MineCourse.BaseHalf;
                            rockfalls.Add(new MineRockfall((float)(x0 + RockSize / 2), (float)ceiling, (float)(2 * MineCourse.BaseHalf + RockSize),
                                                           (float)RockSize, (float)RockSpeed, (float)RockPeriod, (float)(i * RockPhaseStep)));
                            pts.Add((x0 + RockSize / 2, baseY));
                            cur += RockSpacing;
                        }
                        cur += FREE;
                        break;
                    }
                    case "슬라럼":
                        for (int i = 0; i < (o?.Gates ?? 6); i++) { Put(PW, baseY + (i % 2 == 1 ? Slalom : -Slalom)); }
                        break;
                    case "물결 터널":
                    {
                        double x0 = cur, b = baseY;
                        Tube(x0, x0 + 24, x => b + WaveAmp * Math.Sin(2 * Math.PI * (x - x0) / WavePer), WaveGap);
                        pts.Add((x0 + 12, baseY));
                        cur += 24 + FREE;
                        break;
                    }
                    case "급반전":
                        cur += FlipLead;
                        for (int i = 0; i < 4; i++) { baseY += (i % 2 == 0) ? Flip : -Flip; Put(PW, baseY); cur += FlipExtra; }
                        break;
                    case "낮은 천장":
                    {
                        double a = cur, b = cur + (o?.Length ?? 34);
                        c.Lows.Add((a, b));
                        cur = b + FREE;
                        pts.Add((b + 2, baseY));
                        break;
                    }
                    //  수직 갱: 좁은 굴이 짧은 거리에 크게 떨어진다 — 손 떼고 떨어지다 바닥에서 정확히 받아 낸다.
                    case "수직 갱 낙하":
                    {
                        double a = cur, b = cur + (o?.Length ?? DropLen);
                        c.Narrows.Add((a - 2, b + 4, DiveNarrow));
                        pts.Add((a, baseY)); baseY -= o?.Rise ?? Drop; pts.Add((b, baseY));
                        cur = b + 4 + FREE;
                        break;
                    }
                    //  굴뚝: 좁은 굴이 길게 오르막 — 쉬지 않고 빠르게 쳐야 한다.
                    case "굴뚝 오르기":
                    {
                        double a = cur, b = cur + (o?.Length ?? ClimbLen);
                        c.Narrows.Add((a - 2, b + 2, ClimbNarrow));
                        pts.Add((a, baseY)); baseY += o?.Rise ?? Climb; pts.Add((b, baseY));
                        cur = b + 2 + FREE;
                        break;
                    }
                    //  레일 롤러코스터: 큰 물결을 그리는 긴 굴 — 떨어지고 솟구치기를 굴 안에서 이어 탄다.
                    case "레일 롤러코스터":
                    {
                        double a = cur, b = baseY, len = o?.Length ?? CoasterLen;
                        Tube(a, a + len, x => b + CoasterAmp * Math.Sin(2 * Math.PI * (x - a) / CoasterPer), CoasterGap);
                        pts.Add((a + len / 2, baseY));
                        cur = a + len + FREE;
                        break;
                    }
                    case "고수 갈림길 ⬆굴": Fork(true, o?.Shape ?? ForkShape.Arc); break;
                    case "고수 갈림길 ⬇굴": Fork(false, o?.Shape ?? ForkShape.Arc); break;
                    default: throw new ArgumentException(name);
                }
            }

            //  광산 코스 3막(10-09, 프로토타입 MINE3_ORDER): [구간, 뒤에 붙는 보통 관문 수, 옵션].
            //  1막 하강 — 노을 입구에서 곧장 수직 갱. 2막 갱 바닥 — 롤러코스터 중심, 더 깊은 두 번째 갱으로 끝. 3막 탈출 — 가장 긴 굴뚝 → 마지막 갈림길.
            //  1막의 갱 바닥 다음은 10-10부터 계단 내리막 6(4.5 m씩, 프로토타입 v31) — 슬라럼 8을 대신한다(슬라럼 코드는 남긴다).
            //  물결 터널은 낮은 천장과 겹쳐 뺐다(코드는 남긴다). 깊은 갱·굴뚝 24 m는 위 요약 주석 참고(10-12, 프로토타입은 28).
            //  10-13 동적 지형 1차(spec 2026-10-13, 시안 v10 PLAN_ORDER): 막마다 움직이는 구간 하나 + 갈림길마다 다른 고수 길.
            //  낮은 천장 → 진자 광차, 긴 통로 → 승강기(같은 높이 왕복은 루즈 — 사람 판 10-10). 두 구간 코드는 남긴다.
            var order = new (string name, int after, SectionOptions o)[]
            {
                ("수직 갱 낙하", 1, new SectionOptions { Length = 9.2 }), ("계단 내리막", 1, new SectionOptions { Gates = 6 }), ("낙석", 1, null),
                ("고수 갈림길 ⬇굴", 2, new SectionOptions { Shape = ForkShape.Collapse }),
                ("진자 광차", 1, null), ("레일 롤러코스터", 1, new SectionOptions { Length = 30 }), ("급반전", 1, null),
                ("고수 갈림길 ⬆굴", 0, new SectionOptions { Shape = ForkShape.Arc }), ("수직 갱 낙하", 2, new SectionOptions { Length = 10, Rise = 24 }),
                ("승강기", 1, null), ("굴뚝 오르기", 0, new SectionOptions { Length = 21, Rise = 24 }),
                ("고수 갈림길 ⬆굴", 3, new SectionOptions { Shape = ForkShape.Rail }),
            };
            Norm(2);
            foreach (var (name, after, o) in order)
            {
                sections.Add(new MineSection(name, (float)cur));
                Section(name, o);
                Norm(after);
            }

            c.Gates = gates; c.Tubes = tubes; c.Walls = walls; c.Pads = pads; c.Forks = forks; c.Sections = sections;
            c.Lifts = lifts; c.Pendulums = pendulums; c.Rockfalls = rockfalls; c.Props = props;
            c.Length = (float)(cur + 4);
            return c;
        }


        /// <summary>궤적 꺾은선의 선형 보간(프로토타입 pathAt). 양끝 밖은 끝 값.</summary>
        static double PathAt(List<(double x, double y)> path, double x)
        {
            if (x <= path[0].x) { return path[0].y; }
            for (int i = 1; i < path.Count; i++)
            {
                if (x <= path[i].x)
                {
                    return path[i - 1].y + (path[i].y - path[i - 1].y) * (x - path[i - 1].x) / (path[i].x - path[i - 1].x);
                }
            }
            return path[path.Count - 1].y;
        }

        /// <summary>
        /// 배치가 말이 되는가 — 관문 틈이 통로 안, 굴 틈 &gt; 0, 패드가 통로 안, 길이 &gt; 0. 문제면 한국어 문장, 아니면 null.
        /// 통과 가능성(날아서 지나갈 수 있는가)은 여기서 보지 않는다 — 맵 검사 몫이다.
        /// </summary>
        public static string Validate(MineCourse c)
        {
            if (!(c.Length > 0f)) { return $"코스 길이가 {c.Length:F2} m다 — 0보다 커야 한다."; }

            foreach (MineGate g in c.Gates)
            {
                double lo = g.GapCenter - g.Gap / 2.0, hi = g.GapCenter + g.Gap / 2.0;
                //  두께 양끝과 가운데 — 두꺼운 관문(긴 통로 12 m)도 통로가 휘는 곳에 걸치면 잡는다.
                foreach (double x in new[] { g.X - g.Width / 2.0, g.X, g.X + g.Width / 2.0 })
                {
                    double center = c.CenterAtD(x), half = c.HalfAtD(x);
                    if (lo < center - half - 1e-4 || hi > center + half + 1e-4)
                    {
                        return $"x={g.X:F2} 관문의 틈({lo:F2}~{hi:F2})이 통로({center - half:F2}~{center + half:F2}) 밖으로 나간다.";
                    }
                }
            }

            //  승강기는 흔들린 끝(틈 ± 진폭)까지 통로 안이어야 한다 — 밖이면 그 박자엔 틈이 벽 속이다.
            foreach (MineLift l in c.Lifts)
            {
                double lo = l.Gate.GapCenter - l.Gate.Gap / 2.0 - l.Amplitude, hi = l.Gate.GapCenter + l.Gate.Gap / 2.0 + l.Amplitude;
                double center = c.CenterAtD(l.Gate.X), half = c.HalfAtD(l.Gate.X);
                if (lo < center - half - 1e-4 || hi > center + half + 1e-4)
                {
                    return $"x={l.Gate.X:F2} 승강기 틈이 흔들리면({lo:F2}~{hi:F2}) 통로({center - half:F2}~{center + half:F2}) 밖으로 나간다.";
                }
            }
            foreach (MineRockfall r in c.Rockfalls)
            {
                double floor = c.CenterAtD(r.X) - c.HalfAtD(r.X);
                //  다 떨어진 뒤 바위 윗면(천장 − 낙하 거리)이 바닥선 밑이어야 숨기 직전에 통로에서 사라진다.
                if (r.Ceiling - r.Drop > floor + 1e-4)
                {
                    return $"x={r.X:F2} 낙석이 바닥 밑까지 안 빠진다(낙하 {r.Drop:F2}).";
                }
            }

            foreach (MineTube t in c.Tubes)
            {
                if (!(t.Gap > 0f)) { return $"x={t.X0:F2} 굴의 틈이 {t.Gap:F2} m다 — 0보다 커야 한다."; }
            }

            foreach (MinePad pad in c.Pads)
            {
                foreach (double x in new double[] { pad.Rect.X0, pad.Rect.X1 })
                {
                    double center = c.CenterAtD(x), half = c.HalfAtD(x);
                    if (pad.Rect.Y0 < center - half - 1e-4 || pad.Rect.Y1 > center + half + 1e-4)
                    {
                        return $"x={pad.Rect.X0:F2} 패드({pad.Rect.Y0:F2}~{pad.Rect.Y1:F2})가 통로({center - half:F2}~{center + half:F2}) 밖에 있다.";
                    }
                }
            }
            return null;
        }
    }
}
