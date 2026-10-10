using System;
using System.Collections.Generic;

namespace LOP.EditorTools
{
    /// <summary>
    /// 손으로 칠한 듯한 바위 면 텍스처(10-11 Fix 1c, 사용자 "A — 큰 덩이 그림자·굵은 금 몇 개·읽히는 지층, 동키콩 광산 배경처럼").
    /// 정사각 타일(가로·세로 이음매 없음)을 RGBA 바이트로 칠한다 — 유니티에 기대지 않는 순수 함수라 테스트·미리 보기가 쉽다.
    ///
    /// <para><b>그리는 것</b>(전부 바위 평균 색 <c>baseRgb</c>에 곱하는 명암, 따뜻한 갈색만):
    /// ① 가로로 긴 큰 바위 덩이(지터 격자 보로노이, 덩이마다 명암이 조금 다르고 왼쪽 위에서 빛을 받아 덩이 안이 부드럽게 기운다),
    /// ② 덩이 사이 어두운 틈 + 빛 반대쪽(오른쪽 아래) 가장자리의 그림자 띠 — 빛 쪽 가장자리는 아주 조금만 밝다(<b>밝은 테 없음</b>:
    /// 밝은 선 = 닿는 것은 판정선 위 얇은 EdgeRim 하나만),
    /// ③ 물결 진 가로 지층 띠(두 톤), ④ 굵은 금 몇 개(가지 하나씩, 끝이 가늘어진다), ⑤ 아주 낮은 얼룩.
    /// 명암은 몇 단계로 끊어(포스터화) 칠한 느낌을 낸다. 끝으로 평균 명암을 1로 맞춰 텍스처 평균 색 = <c>baseRgb</c>
    /// (장식 재질이 이 평균을 기준으로 색을 정한다 — <see cref="FlappyMineMaterials"/>).</para>
    ///
    /// <para><b>밝기 상한</b>: 명암은 <see cref="MaxShade"/>를 넘지 않는다(돌출 혹·점 무늬·밝은 테 없음 — v1 "요란하다").</para>
    /// </summary>
    public static class MineRockPaint
    {
        /// <summary>명암(바위 평균 대비 배수) 상한 — 가장 밝은 화소도 평균의 이 배를 넘지 않는다.</summary>
        public const double MaxShade = 1.3;
        /// <summary>명암 하한(금 가운데).</summary>
        public const double MinShade = 0.38;

        /// <summary>칠하기 값 — <see cref="FlappyMineLook"/>의 "칠한 바위" 조절판이 채운다.</summary>
        public readonly struct Settings
        {
            public readonly int Size;          // 한 변 화소 수(2의 거듭제곱)
            public readonly int BlocksX, BlocksY; // 타일 하나의 덩이 격자(가로 × 세로)
            public readonly double Contrast;   // 명암 폭 배수(1 = 기본, 0 = 민짜)
            public readonly double Strata;     // 지층 띠 세기(기본 1)
            public readonly int Strands;       // 타일 하나 지층 띠 수
            public readonly int Cracks;        // 타일 하나 굵은 금 수
            public readonly int Seed;

            public Settings(int size, int blocksX, int blocksY, double contrast, double strata, int strands, int cracks, int seed)
            {
                Size = size; BlocksX = Math.Max(1, blocksX); BlocksY = Math.Max(1, blocksY);
                Contrast = contrast; Strata = strata; Strands = Math.Max(1, strands); Cracks = Math.Max(0, cracks); Seed = seed;
            }
        }

        /// <summary>
        /// 명암 지도(행 0 = 아래, 열 0 = 왼쪽, 값 = 평균 대비 배수, 평균 1). 가로·세로로 이어 붙여도 이음매가 없다.
        /// </summary>
        public static double[] Shade(Settings s)
        {
            int n = s.Size;
            var shade = new double[n * n];
            var rnd = new Random(s.Seed);

            //  덩이 중심 — 칸마다 하나, 칸 안 지터. 거리는 가로로 눌러(세로 축 × Squash) 덩이가 가로로 긴 층 바위가 된다.
            int bx = s.BlocksX, by = s.BlocksY;
            var sites = new (double x, double y, double tone)[bx * by];
            for (int j = 0; j < by; j++)
            {
                for (int i = 0; i < bx; i++)
                {
                    //  행마다 반 칸 어긋나게 — 벽돌처럼 쌓인 층 바위.
                    double ox = (j % 2) * 0.5;
                    sites[j * bx + i] = ((i + ox + 0.15 + 0.7 * rnd.NextDouble()) / bx, (j + 0.2 + 0.6 * rnd.NextDouble()) / by, rnd.NextDouble() * 2 - 1);
                }
            }
            double squash = (double)bx / by * 0.7;   // 세로 거리를 더 무겁게 — 덩이가 가로로 넓적한 층 바위
            //  빛은 왼쪽 위에서.
            double lx = -0.6, ly = 0.8;

            //  지층 띠의 가로 물결(주기 함수 — 이음매 없음).
            double p1 = rnd.NextDouble() * Math.PI * 2, p2 = rnd.NextDouble() * Math.PI * 2;
            double strandPhase = rnd.NextDouble();
            //  덩이 하나 크기(타일 비율)와 거기 맞춘 잡음 주파수 — 덩이 수를 바꿔도 덩이 하나의 그림은 같다.
            double cell = 1.0 / bx;
            int f1 = Math.Max(1, (int)Math.Round(4.0 * bx / 3)), f2 = Math.Max(2, (int)Math.Round(11.0 * bx / 3));
            int wa = Math.Max(1, bx / 3), wb = Math.Max(2, 2 * bx / 3);   // 지층 물결 주기(정수 — 이음매 없음)

            for (int y = 0; y < n; y++)
            {
                double v = (y + 0.5) / n;
                for (int x = 0; x < n; x++)
                {
                    double u = (x + 0.5) / n;
                    //  덩이 경계를 물결지게 — 감싼 잡음으로 좌표를 비튼다(곧은 다각형이면 포장 돌로 읽혔다).
                    double wu = u + cell * (0.105 * PeriodicNoise(u, v, f1, s.Seed + 3) + 0.036 * PeriodicNoise(u, v, f2, s.Seed + 5));
                    double wv = v + cell * (0.105 * PeriodicNoise(u, v, f1, s.Seed + 7) + 0.036 * PeriodicNoise(u, v, f2, s.Seed + 9));

                    //  ① 보로노이(감싼 거리) — 가장 가까운 둘.
                    double d1 = double.MaxValue, d2 = double.MaxValue;
                    int k1 = 0, k2 = 0;
                    double n1x = 0, n1y = 0, n2x = 0, n2y = 0;
                    for (int k = 0; k < sites.Length; k++)
                    {
                        double dx = Wrap(sites[k].x - wu), dy = Wrap(sites[k].y - wv) / squash;
                        double d = Math.Sqrt(dx * dx + dy * dy);
                        if (d < d1) { d2 = d1; k2 = k1; n2x = n1x; n2y = n1y; d1 = d; k1 = k; n1x = dx; n1y = dy; }
                        else if (d < d2) { d2 = d; k2 = k; n2x = dx; n2y = dy; }
                    }
                    //  경계까지 거리(근사) = 두 중심을 잇는 선 위 투영.
                    double ex = n2x - n1x, ey = n2y - n1y;
                    double el = Math.Sqrt(ex * ex + ey * ey) + 1e-9;
                    double edge = ((n1x + n2x) * 0.5 * ex + (n1y + n2y) * 0.5 * ey) / el;   // 화소 → 경계 거리(덩이 단위)
                    edge = Math.Max(0, edge) * bx;   // 칸 너비 1 기준
                    //  경계 쪽 방향(화소에서 이웃 덩이 쪽, 세로는 다시 펴서) · 빛.
                    double facing = (ex * lx + ey * squash * ly) / Math.Sqrt(ex * ex + ey * squash * ey * squash + 1e-12);

                    double t = 1.0;
                    //  덩이마다 명암.
                    t += 0.09 * sites[k1].tone;
                    //  덩이 안 둥근 명암 — 중심에서 빛 쪽이 밝다(−n1 = 중심 → 화소).
                    double rx = -n1x * bx, ry = -n1y * bx * squash;
                    t += 0.13 * Clamp(rx * lx + ry * ly, -1, 1);
                    //  ② 가장자리: 빛 반대쪽은 넓은 그림자, 빛 쪽은 아주 좁고 약한 밝음. 틈은 어둡게.
                    if (facing < 0)
                    {
                        t -= 0.20 * (-facing) * SmoothFall(edge, 0.16);
                    }
                    else
                    {
                        t += 0.05 * facing * SmoothFall(edge, 0.05);
                    }
                    //  틈은 경계마다 세기가 다르다(덩이 쌍 해시) — 모든 경계를 똑같이 그으면 타일 바닥으로 읽힌다.
                    double gap = 0.35 + 0.65 * (0.5 + 0.5 * Hash(Math.Min(k1, k2), Math.Max(k1, k2), 1 << 20, s.Seed + 41));
                    t -= 0.34 * gap * SmoothFall(edge, 0.022);

                    //  ③ 지층 — 물결 진 가로 띠 두 톤(덩이 그림 위에 은은히).
                    double warp = cell * (0.105 * Math.Sin(2 * Math.PI * wa * u + p1) + 0.06 * Math.Sin(2 * Math.PI * wb * u + p2));
                    double band = Frac((v + warp) * s.Strands + strandPhase);
                    double strand = band < 0.38 ? -1 : (band < 0.5 ? 1 : 0);
                    t += 0.07 * s.Strata * strand;

                    //  ⑤ 얼룩(낮은 주파수 주기 잡음).
                    t += 0.025 * PeriodicNoise(u, v, Math.Max(1, bx), s.Seed + 17) + 0.015 * PeriodicNoise(u, v, Math.Max(2, 7 * bx / 3), s.Seed + 29);

                    shade[y * n + x] = t;
                }
            }

            //  ④ 굵은 금 — 덩이 사이 아무 데서나 시작해 비스듬히 걷다가 가지 하나. 감싸서 그린다.
            var crack = new double[n * n];
            for (int c = 0; c < s.Cracks; c++)
            {
                double cx = rnd.NextDouble(), cy = rnd.NextDouble();
                double ang = (rnd.NextDouble() < 0.5 ? Math.PI : 0) + (rnd.NextDouble() - 0.5) * 1.6;   // 대체로 가로·비스듬히
                double len = (0.28 + 0.14 * rnd.NextDouble()) * 3 * cell;
                DrawCrack(crack, n, cx, cy, ang, len, 0.0211 * n * cell, rnd, branch: true);
            }
            for (int i = 0; i < shade.Length; i++)
            {
                shade[i] = shade[i] * (1 - crack[i]) + 0.45 * crack[i];
            }

            //  포스터화(칠한 느낌) — 평균 둘레로 대비를 곱하고 단계로 끊는다.
            double mean = 0;
            for (int i = 0; i < shade.Length; i++) { mean += shade[i]; }
            mean /= shade.Length;
            for (int i = 0; i < shade.Length; i++)
            {
                double d = (shade[i] - mean) * s.Contrast;
                d = Math.Round(d / 0.035) * 0.035;
                shade[i] = 1 + d;
            }
            //  평균을 1로(포스터화가 조금 밀었다), 상·하한.
            mean = 0;
            for (int i = 0; i < shade.Length; i++) { mean += shade[i]; }
            mean /= shade.Length;
            for (int i = 0; i < shade.Length; i++) { shade[i] = Clamp(shade[i] / mean, MinShade, MaxShade); }
            return shade;
        }

        /// <summary>
        /// RGBA32 바이트(행 0 = 아래 — <c>Texture2D.LoadRawTextureData</c>·<c>SetPixels32</c> 순서). 색 = baseRgb × 명암,
        /// 어두운 쪽은 붉게·밝은 쪽은 노랗게 조금 비틀어(따뜻한 갈색만) 칠한 느낌을 더한다.
        /// </summary>
        public static byte[] Rgba(Settings s, double r, double g, double b)
        {
            double[] shade = Shade(s);
            var bytes = new byte[shade.Length * 4];
            for (int i = 0; i < shade.Length; i++)
            {
                double t = shade[i];
                double warm = t - 1;   // 음수 = 그림자
                double rr = r * t * (1 + (warm < 0 ? -0.10 * warm : 0.02 * warm));
                double gg = g * t * (1 + (warm < 0 ? 0.04 * warm : 0.04 * warm));
                double bb = b * t * (1 + (warm < 0 ? 0.12 * warm : -0.05 * warm));
                bytes[i * 4] = ToByte(rr);
                bytes[i * 4 + 1] = ToByte(gg);
                bytes[i * 4 + 2] = ToByte(bb);
                bytes[i * 4 + 3] = 255;
            }
            return bytes;
        }

        // ---- 도우미 ----

        //  감싼 차이: [−0.5, 0.5).
        private static double Wrap(double d) => d - Math.Floor(d + 0.5);

        private static double Frac(double x) => x - Math.Floor(x);

        private static double Clamp(double x, double lo, double hi) => x < lo ? lo : (x > hi ? hi : x);

        //  거리 0에서 1, width에서 0으로 부드럽게.
        private static double SmoothFall(double d, double width)
        {
            double k = Clamp(1 - d / width, 0, 1);
            return k * k * (3 - 2 * k);
        }

        private static byte ToByte(double c) => (byte)Math.Round(Clamp(c, 0, 1) * 255);

        //  주기 f의 값 잡음(격자 f × f, 감싼다) — [−1, 1].
        private static double PeriodicNoise(double u, double v, int f, int seed)
        {
            double x = u * f, y = v * f;
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            double fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            double a = Hash(x0, y0, f, seed), b = Hash(x0 + 1, y0, f, seed), c = Hash(x0, y0 + 1, f, seed), d = Hash(x0 + 1, y0 + 1, f, seed);
            return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy;
        }

        private static double Hash(int x, int y, int f, int seed)
        {
            x = ((x % f) + f) % f; y = ((y % f) + f) % f;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h / (double)uint.MaxValue) * 2 - 1;
            }
        }

        //  금 하나: 각도를 조금씩 흔들며 걷는 선, 끝으로 갈수록 가늘다. 감싸서 칠한다(가산 최대값).
        private static void DrawCrack(double[] crack, int n, double x, double y, double angle, double length, double width, Random rnd, bool branch)
        {
            int steps = (int)(length * n / 2) + 1;
            double step = length / steps;
            var pts = new List<(double x, double y, double w)>();
            double a = angle;
            for (int i = 0; i <= steps; i++)
            {
                double k = i / (double)steps;
                double w = width * (1 - 0.75 * k) * (k < 0.08 ? 0.4 + 0.6 * k / 0.08 : 1);
                pts.Add((x, y, w));
                a += (rnd.NextDouble() - 0.5) * 0.35;
                x += Math.Cos(a) * step;
                y += Math.Sin(a) * step;
            }
            foreach (var p in pts)
            {
                Stamp(crack, n, p.x, p.y, p.w);
            }
            if (branch && pts.Count > 8)
            {
                var from = pts[pts.Count * 2 / 5];
                double ba = angle + (rnd.NextDouble() < 0.5 ? -1 : 1) * (0.6 + 0.4 * rnd.NextDouble());
                DrawCrack(crack, n, from.x, from.y, ba, length * 0.45, from.w * 0.8, rnd, branch: false);
            }
        }

        private static void Stamp(double[] crack, int n, double cx, double cy, double radiusPx)
        {
            double px = cx * n, py = cy * n;
            int r = (int)Math.Ceiling(radiusPx + 1);
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int ix = (int)Math.Floor(px) + dx, iy = (int)Math.Floor(py) + dy;
                    double d = Math.Sqrt((ix + 0.5 - px) * (ix + 0.5 - px) + (iy + 0.5 - py) * (iy + 0.5 - py));
                    double k = Clamp(radiusPx + 0.5 - d, 0, 1);
                    if (k <= 0) { continue; }
                    int wx = ((ix % n) + n) % n, wy = ((iy % n) + n) % n;
                    int idx = wy * n + wx;
                    if (k > crack[idx]) { crack[idx] = k; }
                }
            }
        }
    }
}
