using System;
using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>낙하 샤프트 한 조각. 평지 바닥에 뚫린 선택 구멍 — 회랑은 위로 그대로 이어진다.</summary>
    public readonly struct ShaftPiece
    {
        public readonly float X0, ChimneyX0, X1, FloorY, Depth;

        /// <summary>이 샤프트의 굴뚝 폭 — 깊이가 깊을수록 오르는 데 시간이 걸려 넓어진다
        /// (<see cref="FieldLayout.ChimneyWidthFor"/>).</summary>
        public float ChimneyWidth { get; }

        public ShaftPiece(float x0, float floorY, float depth, float chimneyWidth)
        {
            X0 = x0;
            ChimneyX0 = x0 + FieldLayout.ShaftWidth + FieldLayout.PocketFloor;
            ChimneyWidth = chimneyWidth;
            X1 = ChimneyX0 + chimneyWidth;
            FloorY = floorY;
            Depth = depth;
        }
        public float Span => X1 - X0;
        public float PocketFloorY => FloorY - Depth;
        public float PocketTop => PocketFloorY + FieldLayout.PocketHeight;
    }

    /// <summary>
    /// 묶음 1 기믹의 자리(spec 2026-09-27 §2·§3). 샤프트는 구간마다 가장 긴 평지 가운데,
    /// 상승기류는 계곡 오르막 시작(지름길 계곡은 출구 뒤)과 굴뚝, 하강기류는 샤프트 안,
    /// 홀로그램은 샤프트 바로 뒤에 따로 세우는 전용 관문.
    /// </summary>
    public static class FieldLayout
    {
        public const float ShaftWidth = 8f;
        public const float PocketFloor = 12f;
        /// <summary>주머니 안 높이 — 날갯짓 한 번(3.1m)과 몸(0.9m)의 두 배.</summary>
        public const float PocketHeight = 8f;
        public const float SideWall = 2f;
        /// <summary>샤프트 앞뒤로 관문을 두지 않는 거리. 파이프가 구멍 위에 서면 아래가 허공에 뜬다.</summary>
        public const float GateClear = 3f;
        public const float UpdraftWidth = 7f;
        public static readonly float[] ShaftDepths = { 15f, 20f, 25f };

        /// <summary>굴뚝을 지나는 동안 주머니 바닥에서 회랑 바닥 위 3m까지 오를 시간을 담아야
        /// 한다 — 그 시간은 상승 상한(riseCap)까지 가속하는 구간 + riseCap로 나는 구간의 합이다.
        /// 1m는 여유. 0.5m 단위로 올림한다(재는 사람이 눈으로 맞춰 보기 좋게).</summary>
        public static float ChimneyWidthFor(float depth, float forwardSpeed, float upAccel, float riseCap)
        {
            float raw = forwardSpeed * ((depth + 3f) / riseCap + riseCap / upAccel) + 1f;
            return (float)Math.Ceiling(raw / 0.5) * 0.5f;
        }

        public static List<ShaftPiece> PlaceShafts(CourseProfile p, float startX, float length, int sections,
                                                   float corridorHalf, float forwardSpeed, float upAccel,
                                                   float riseCap)
        {
            var shafts = new List<ShaftPiece>();
            float sectionLength = length / sections;
            for (int s = 0; s < sections; s++)
            {
                float lo = startX + sectionLength * s, hi = lo + sectionLength;
                float baseDepth = ShaftDepths[Math.Min(s, ShaftDepths.Length - 1)];
                //  깊이 그대로 안 들어가면 5m씩 얕게 — 최소 10m까지. 그래도 안 들어가면 그
                //  구간은 건너뛴다(억지로 짧은 평지에 우겨넣지 않는다).
                for (float depth = baseDepth; depth >= 10f; depth -= 5f)
                {
                    float chimneyWidth = ChimneyWidthFor(depth, forwardSpeed, upAccel, riseCap);
                    float span = ShaftWidth + PocketFloor + chimneyWidth;
                    //  관문이 못 서는 거리(GateClear)는 여기서 셀 필요가 없다 — GateBlocked가
                    //  따로 그걸 지킨다. 여기 need는 오직 "샤프트 벽이 평지 안에 들어가는가"만
                    //  묻는다 — 옆벽 폭(SideWall) 밖으로 경사가 시작되기 전 1m 여유만 두면 된다.
                    float need = span + 2f * (SideWall + 1f);
                    FlatSpan best = default;
                    float bestLength = 0f;
                    foreach (FlatSpan f in p.Flats)
                    {
                        float a = Math.Max(f.From, lo), b = Math.Min(f.To, hi);
                        //  출발 평지(스폰)는 건너뛴다 — 스폰 앞에서 바로 바닥이 꺼지면 배울 틈이 없다.
                        if (a <= startX + 1f || b - a < need || b - a <= bestLength) { continue; }
                        best = new FlatSpan(a, b);
                        bestLength = b - a;
                    }
                    if (bestLength <= 0f) { continue; }
                    float x0 = (best.From + best.To) * 0.5f - span * 0.5f;
                    float floorY = p.CenterAt(x0) - corridorHalf;
                    shafts.Add(new ShaftPiece(x0, floorY, depth, chimneyWidth));
                    break;
                }
            }
            return shafts;
        }

        /// <summary>
        /// 보통 관문이 이 x에 못 서나. 샤프트 앞뒤 <see cref="GateClear"/>에 더해, 굴뚝 뒤로
        /// 관문 간격 하나(<paramref name="spacing"/>)까지 막는다 — 그 자리는 전용 홀로그램 관문
        /// (<see cref="HologramGateXs"/>) 몫이라, 보통 관문이 바짝 붙으면 두 관문이 한 관문처럼 겹친다.
        /// </summary>
        public static bool GateBlocked(IReadOnlyList<ShaftPiece> shafts, float x, float spacing)
        {
            for (int i = 0; i < shafts.Count; i++)
            {
                if (x > shafts[i].X0 - GateClear && x < shafts[i].X1 + GateClear + spacing) { return true; }
            }
            return false;
        }

        /// <summary>전용 홀로그램 관문이 굴뚝 오른쪽 담장에서 떨어지는 거리(<see cref="GateClear"/> 밖으로 1m 더).</summary>
        public const float HologramGateOffset = 1f;

        /// <summary>
        /// 샤프트마다 굴뚝 바로 뒤(X1 + GateClear + 1m)에 세울 전용 홀로그램 관문 x.
        /// 샤프트를 막 빠져나온 새는 게이지가 차 있으니, 바로 다음 관문에서 대시를 써 볼 수
        /// 있어야 한다(사용자 결정 2026-09-27). 그 자리가 평지 안쪽(<see cref="CourseProfileRule.GateMargin"/>)이
        /// 아니거나 결승선 앞 <see cref="GateClear"/> 안으로 못 들어오면 그 샤프트는 건너뛴다 —
        /// 경사에 걸친 파이프는 아래가 허공에 뜬다.
        /// </summary>
        public static List<float> HologramGateXs(CourseProfile p, IReadOnlyList<ShaftPiece> shafts, float finishX)
        {
            var xs = new List<float>(shafts.Count);
            foreach (ShaftPiece s in shafts)
            {
                float x = s.X1 + GateClear + HologramGateOffset;
                if (p.GateAllowedAt(x, CourseProfileRule.GateMargin) == false || x > finishX - GateClear)
                {
                    continue;
                }
                xs.Add(x);
            }
            return xs;
        }

        public static List<LOP.FlappyAirflowRect> Airflows(CourseProfile p, IReadOnlyList<ShaftPiece> shafts,
                                                            float corridorHalf)
        {
            var rects = new List<LOP.FlappyAirflowRect>();
            foreach (ValleyPiece v in p.Valleys)
            {
                float x0 = v.Bottom1;
                //  지름길 계곡은 출구 뒤부터 — 출구 전엔 지름길 새가 아직 지붕 밑을 난다.
                foreach (ShortcutRect sc in p.Shortcuts)
                {
                    if (sc.X0 > v.X0 && sc.X1 < v.X1 + 1f) { x0 = Math.Max(x0, sc.X1); }
                }
                float x1 = Math.Min(x0 + UpdraftWidth, v.X1);
                if (x1 - x0 < 1f) { continue; }
                float y0 = Math.Min(p.CenterAt(x0), p.CenterAt(x1)) - corridorHalf;
                float y1 = Math.Max(p.CenterAt(x0), p.CenterAt(x1)) + corridorHalf;
                rects.Add(new LOP.FlappyAirflowRect(x0, x1, y0, y1, LOP.FlappyAirflowKind.Up));
            }
            foreach (ShaftPiece s in shafts)
            {
                rects.Add(new LOP.FlappyAirflowRect(s.X0, s.X0 + ShaftWidth, s.PocketFloorY, s.FloorY, LOP.FlappyAirflowKind.Down));
                //  굴뚝은 회랑 바닥 위로 3m 더 민다 — 바닥 높이에서 딱 끊기면 입구 턱에 걸린다.
                rects.Add(new LOP.FlappyAirflowRect(s.ChimneyX0, s.X1, s.PocketFloorY, s.FloorY + 3f, LOP.FlappyAirflowKind.Up));
            }
            return rects;
        }
    }
}
