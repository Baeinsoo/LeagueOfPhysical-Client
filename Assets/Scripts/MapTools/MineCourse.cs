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

        public MineFork(float x0, float x1, float baseY, bool tunnelUp, MineRect tunnelLane)
        {
            X0 = x0; X1 = x1; Base = baseY; TunnelUp = tunnelUp; TunnelLane = tunnelLane;
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

        /// <summary>낮은 천장 중심선 물결 진폭(10-07 난이도 올림: 1.5 → 3.5)과 주기.</summary>
        internal const double LowAmp = 3.5, LowPer = 17;

        public IReadOnlyList<MineGate> Gates;
        public IReadOnlyList<MineTube> Tubes;
        public IReadOnlyList<MineRect> Walls;
        public IReadOnlyList<MinePad> Pads;
        public IReadOnlyList<MineFork> Forks;
        public IReadOnlyList<MineSection> Sections;
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
    /// <para>순서(10-09 3막): 출발 2관문 → 1막 하강(수직 갱 22 m → 슬라럼 8 → 갈림길⬇) → 2막 갱 바닥(낮은 천장 24 m →
    /// 롤러코스터 55 m → 급반전 → 갈림길⬆ → 깊은 갱 28 m) → 3막 탈출(긴 통로 → 굴뚝 28 m → 갈림길⬆) → 출구. 통과 가능성 증명은 맵 검사 몫이다.</para>
    /// </summary>
    public static class MineCourseRule
    {
        /// <summary>프로토타입 <c>seeded(7 + 9)</c> → 10-09 3막(v30 "0 · 광산 3막")은 <c>seeded(7 + 10)</c> — 관문 1·2·29·마지막 핀이 16으로는 안 맞고 17로만 맞는다.</summary>
        public const uint Seed = 17;

        /// <summary>파이프 두께 · 관문 틈 · 관문 사이 빈 거리(원조 간격 5.4 m − 두께).</summary>
        const double PW = 1.95, GAP = 3.75, FREE = 5.4 - PW;

        /// <summary>갈림길 2번 점프 굴의 틈.</summary>
        const double TunnelGap = 3.1;

        /// <summary>⚡ 패드 부스트 시간(초).</summary>
        const float PadDuration = 1.1f;

        //  지형 (프로토타입 TERRAIN)
        const double DropLen = 8, Drop = 22, DiveNarrow = 2.8;
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
        /// <summary>급반전 오르내림(예전 ±4.0).</summary>
        const double Flip = 5.5;
        /// <summary>롤러코스터 굴 틈(예전 5.0 → 10-07 4.85 → 10-09 3막 4.8. 4.7은 검사에서 거의 못 지난다).</summary>
        const double CoasterGap = 4.8;
        /// <summary>갈림길 굴 쪽 칸 중심 — 통로가 좁아져 위 굴은 위로 ForkTunnelUp, 아래 굴은 아래로 ForkTunnelDown(비대칭, 예전 둘 다 4.5).</summary>
        const double ForkTunnelUp = 2.6, ForkTunnelDown = 4.6;
        /// <summary>갈림길 반대쪽 보통 칸 관문 틈 중심(통로 중심에서 ±ForkOther, 예전 ±4.5).</summary>
        const double ForkOther = 3.9;
        /// <summary>갈림길 반대쪽 보통 관문 틈 중심의 난수 폭(예전 4.0 → 2.0 → 10-09 3막 3.0 — 검사에서 가장 쉬운 곳이었다).</summary>
        const double ForkBand = 3.0;
        /// <summary>갈림길 반대쪽 보통 관문의 틈(10-09 3막: 원조 3.75 → 3.3). 굴(3.1)보다는 넉넉한 "안전하지만 느린 길".</summary>
        const double ForkGap = 3.3;

        /// <summary>구간 옵션(10-09 3막). 비워 두면 그 구간의 기본값 — 관문 수(슬라럼), 길이(낮은 천장·롤러코스터·수직 갱·굴뚝), 높이 차(수직 갱·굴뚝).</summary>
        sealed class SectionOptions
        {
            public int? Gates;
            public double? Length;
            public double? Rise;
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
            void Norm(int n) { for (int i = 0; i < n; i++) { Put(PW, baseY + (Rnd() - 0.5) * ConnBand); } }
            void Tube(double x0, double x1, Func<double, double> center, double gap, double low = double.NaN, double high = double.NaN)
            {
                tubes.Add(new MineTube { X0 = (float)x0, X1 = (float)x1, Gap = (float)gap, Low = (float)low, High = (float)high,
                                         Center = x => (float)center(x) });
            }

            //  날갯짓 → 같은 높이로 돌아오는 틱 수. 프로토타입 정의(반올림, 30)라 FlapArc.TicksPerArc(31)와 다르다.
            int arcT = (int)Math.Round(2.0 * p.Flap / p.Gravity / p.Tick);

            //  고수 갈림길: 가운데 칸막이로 위·아래가 갈린다. 한쪽은 날갯짓 정확히 2번짜리 포물선 굴(틈 폭 일정) + 끝에 ⚡ 패드,
            //  반대쪽은 보통 관문.
            void Fork(bool up)
            {
                double side = up ? 1 : -1, fx = cur, laneC = baseY + (up ? ForkTunnelUp : -ForkTunnelDown);
                double top = baseY + MineCourse.BaseHalf + 1, bot = baseY - MineCourse.BaseHalf - 1;

                //  굴 중심선: 입구에서 날갯짓, arcT틱 뒤 다시 날갯짓 — 같은 높이로 두 번 튀어 오른다.
                var path = new List<(double x, double y)>();
                {
                    double y = laneC, vy = 0, x = fx + 3.0;
                    path.Add((x, y));
                    for (int k = 0; k < 2 * arcT; k++)
                    {
                        vy = (k % arcT == 0) ? p.Flap : Math.Max(-(double)p.MaxFall, vy - (double)p.Gravity * p.Tick);
                        y += vy * p.Tick;
                        x += (double)p.Forward * p.Tick;
                        path.Add((x, y));
                    }
                }
                double ax1 = path[path.Count - 1].x;
                double laneLo = up ? baseY + 0.6 : bot, laneHi = up ? top : baseY - 0.6;
                Tube(fx + 3.0, ax1, x => PathAt(path, x), TunnelGap, laneLo, laneHi);

                double padX = ax1 + 0.3;
                pads.Add(new MinePad(new MineRect((float)padX, (float)(padX + 1.2), (float)(laneC - 1.4), (float)(laneC + 1.4)), PadDuration));
                double fx1 = padX + 12;   // 부스트 약 10 m가 칸막이 안에서 끝나게
                walls.Add(new MineRect((float)fx, (float)fx1, (float)(baseY - 0.6), (float)(baseY + 0.6)));

                for (double gx = fx + 3; gx < fx1 - 2; gx += 5.4)
                {
                    gates.Add(new MineGate((float)(gx + PW / 2), (float)PW, (float)(baseY - side * ForkOther + (Rnd() - 0.5) * ForkBand), (float)ForkGap,
                                           (float)(up ? bot : baseY + 0.6), (float)(up ? baseY - 0.6 : top)));
                }
                forks.Add(new MineFork((float)fx, (float)fx1, (float)baseY, up,
                                       new MineRect((float)fx, (float)fx1, (float)laneLo, (float)laneHi)));
                pts.Add((fx1, baseY));
                cur = fx1 + FREE;
            }

            void Section(string name, SectionOptions o)
            {
                switch (name)
                {
                    case "긴 통로":
                        Put(12, baseY + (Rnd() - 0.5) * 1.5, LongGap);
                        break;
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
                        for (int i = 0; i < 4; i++) { baseY += (i % 2 == 0) ? Flip : -Flip; Put(PW, baseY); }
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
                    case "고수 갈림길 ⬆굴": Fork(true); break;
                    case "고수 갈림길 ⬇굴": Fork(false); break;
                    default: throw new ArgumentException(name);
                }
            }

            //  광산 코스 3막(10-09, 프로토타입 MINE3_ORDER): [구간, 뒤에 붙는 보통 관문 수, 옵션].
            //  1막 하강 — 노을 입구에서 곧장 수직 갱. 2막 갱 바닥 — 롤러코스터 중심, 더 깊은 두 번째 갱으로 끝. 3막 탈출 — 가장 긴 굴뚝 → 마지막 갈림길.
            //  물결 터널은 낮은 천장과 겹쳐 뺐다(코드는 남긴다).
            var order = new (string name, int after, SectionOptions o)[]
            {
                ("수직 갱 낙하", 1, null), ("슬라럼", 1, new SectionOptions { Gates = 8 }), ("고수 갈림길 ⬇굴", 2, null),
                ("낮은 천장", 1, new SectionOptions { Length = 24 }), ("레일 롤러코스터", 1, new SectionOptions { Length = 55 }), ("급반전", 1, null),
                ("고수 갈림길 ⬆굴", 0, null), ("수직 갱 낙하", 2, new SectionOptions { Length = 10, Rise = 28 }),
                ("긴 통로", 1, null), ("굴뚝 오르기", 0, new SectionOptions { Length = 21, Rise = 28 }), ("고수 갈림길 ⬆굴", 3, null),
            };
            Norm(2);
            foreach (var (name, after, o) in order)
            {
                sections.Add(new MineSection(name, (float)cur));
                Section(name, o);
                Norm(after);
            }

            c.Gates = gates; c.Tubes = tubes; c.Walls = walls; c.Pads = pads; c.Forks = forks; c.Sections = sections;
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
