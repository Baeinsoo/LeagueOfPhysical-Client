using System.Collections.Generic;
using UnityEngine;

namespace LOP.MapTools
{
    /// <summary>
    /// 증명된 경로 하나가 <b>도는 장애물 하나</b>의 원반을 얼마나 비켜 갔나.
    ///
    /// <para>원반 = 날개가 한 바퀴 돌며 쓸고 가는 자리. <see cref="ObstaclePlacement.DiscRadius"/>가
    /// 그 반지름이다(②-b가 쓰는 그 값 그대로).</para>
    /// </summary>
    public readonly struct SweptDiscGap
    {
        public readonly string Name;
        /// <summary>쟀는가. 원반 반지름을 못 쟀거나 경로가 없으면 false다 — 그런 자리는
        /// 통과로도 미달로도 세지 않는다(②-b가 <see cref="ObstaclePlacement.Measured"/>를
        /// 다루는 방식과 같다).</summary>
        public readonly bool Measured;
        /// <summary>경로 전체에 걸친 최소 여유(m). <b>음수면 그만큼 원반 안으로 파고들었다.</b></summary>
        public readonly float Gap;
        /// <summary>그 최소가 난 자리의 x. 틱 사이에서 날 수 있으므로 정수 틱의 x가 아니다.</summary>
        public readonly float AtX;
        /// <summary>그 최소가 난 <b>틱 구간의 시작</b>(경로 점 인덱스 — 0이 출발점). 최소는 이
        /// 틱과 다음 틱 사이 어디서든 날 수 있어서, 정확한 자리는 <see cref="AtX"/>가 말한다.</summary>
        public readonly int AtTick;

        public SweptDiscGap(string name, bool measured, float gap, float atX, int atTick)
        {
            Name = name;
            Measured = measured;
            Gap = gap;
            AtX = atX;
            AtTick = atTick;
        }
    }

    /// <summary>경로 하나에 대한 판정 — 날개가 어느 각도에 서 있어도 이 경로가 성립하는가.</summary>
    public readonly struct SweptDiscVerdict
    {
        /// <summary>잴 거리가 있었는가(경로도 있고 도는 장애물도 있었는가). false면 리포트는
        /// 이 줄을 아예 안 찍는다 — 빈 문구는 "쟀는데 없었다"로 잘못 읽힌다.</summary>
        public readonly bool Measured;
        /// <summary>원반을 실제로 잰 장애물 수.</summary>
        public readonly int DiscCount;
        /// <summary>원반 반지름을 못 재 판정에서 뺀 장애물 수. 0이 아니면 "무관하다"고 말할 수 없다.</summary>
        public readonly int Unmeasured;
        /// <summary>여유가 가장 작았던 장애물. <see cref="DiscCount"/>가 0이면 뜻이 없다.</summary>
        public readonly SweptDiscGap Worst;

        public SweptDiscVerdict(bool measured, int discCount, int unmeasured, in SweptDiscGap worst)
        {
            Measured = measured;
            DiscCount = discCount;
            Unmeasured = unmeasured;
            Worst = worst;
        }

        /// <summary>이 경로가 원반 <b>밖</b>으로만 지났는가 — 그렇다면 날개 각도와 무관하게 성립한다.
        /// <para>못 잰 장애물이 하나라도 있으면 참이라고 말하지 않는다(모르는 것을 아는 척하지 않는다).
        /// 딱 0이면 참으로 센다 — 원반에 닿지 않고 스치는 자리이고, ②-b도 "딱 기준만큼이면 통과"다.</para></summary>
        public bool PhaseIndependent
            => Measured && Unmeasured == 0 && DiscCount > 0 && Worst.Gap >= 0f;
    }

    /// <summary>
    /// <b>증명된 경로가 도는 장애물의 원반 안으로 들어가는가</b>에 답한다 — 시뮬레이션이 아니라
    /// 기하다.
    ///
    /// <para><b>왜 이 측정인가.</b> 전수 탐색은 자유공간 캐시가 칸마다 답을 한 번 재고 재사용해서,
    /// 도는 장애물을 <i>틱 0 자세로 굳은 벽</i>으로 본다. 그래서 탐색이 찾아 재생으로 증명한 ✅는
    /// <i>그 한 장면</i>에서만 참일 수 있다. 그런데 경로가 날개가 쓸고 가는 원반 밖으로만 지난다면
    /// 날개가 어느 각도에 있든 그 경로는 그대로 성립한다 — 굳은 벽이라는 한계가 그 경로에는
    /// 무해해진다. 이 클래스는 딱 그 한 가지를 잰다.</para>
    ///
    /// <para><b>재는 방식.</b> 새는 점이 아니라 캡슐이다(반지름 r, 높이 h). 캡슐의 축은 몸 가운데에서
    /// 위아래로 <c>h/2 − r</c>만큼이고, 여유 = (축과 원반 중심 사이 거리) − 원반 반지름 − r 이다.
    /// 틱 사이도 잰다 — 틱 하나에 몸이 최대 0.6m 옮겨 가므로 <b>점만 재면 그 사이로 들어갔다 나온
    /// 것을 놓친다</b>. 그래서 두 틱을 잇는 선분과 원반을 통째로 견준다.</para>
    ///
    /// <para><b>z는 안 본다</b> — ②-b와 같다. 새는 z=0에 붙어 있고 원반의 z 두께는 안 재므로,
    /// 원반을 z축으로 무한히 긴 기둥으로 보는 셈이다. 실제보다 <b>가깝게</b> 재는 쪽이라
    /// "무관하다"는 판정에는 무해하다(안전한 쪽으로 틀린다).</para>
    ///
    /// <para>산업 표준 매핑: 도는 몸이 지나간 자리 전체를 하나의 막힌 덩어리로 보고 경로와
    /// 견주는 것은 로보틱스 경로계획의 <i>swept volume</i> 검사 그대로다. 이름의 "Swept"가 그것이다.</para>
    /// </summary>
    public static class SweptDiscRule
    {
        /// <summary>몸이 <paramref name="from"/>에서 <paramref name="to"/>로 한 틱 지나가는 동안의
        /// 최소 여유(m). 음수면 파고든 깊이다.</summary>
        /// <param name="from">틱 시작의 <b>몸 가운데</b>(발밑이 아니다).</param>
        /// <param name="atFraction">최소가 난 자리가 그 구간의 어디였나(0=시작, 1=끝).</param>
        public static float Gap(Vector3 from, Vector3 to, float bodyRadius, float bodyHeight,
                                float discX, float discY, float discRadius, out float atFraction)
        {
            //  캡슐 축의 반길이. 몸이 공(h = 2r)이면 0이라 축이 점 하나로 줄어든다.
            float half = bodyHeight * 0.5f - bodyRadius;
            if (half < 0f)
            {
                half = 0f;
            }
            //  "움직이는 캡슐 축 ↔ 원반 중심"은 "움직인 자취(선분) ↔ 원반 중심에 세운 같은 길이의
            //  세로 선분"과 같은 거리다(축을 몸에서 떼어 원반 쪽에 붙인 것뿐이다). 그래서 선분
            //  둘의 최단거리 하나로 끝난다.
            float distance = SegmentDistance(
                new Vector2(from.x, from.y), new Vector2(to.x, to.y),
                new Vector2(discX, discY - half), new Vector2(discX, discY + half),
                out atFraction);
            return distance - discRadius - bodyRadius;
        }

        /// <summary>경로 하나 × 장애물 하나 — 경로 전체에 걸친 최소 여유.</summary>
        /// <param name="path">틱마다의 <b>몸 가운데</b> 자리.</param>
        public static SweptDiscGap Measure(string name, IReadOnlyList<Vector3> path,
                                           float bodyRadius, float bodyHeight,
                                           float discX, float discY, float discRadius)
        {
            if (path == null || path.Count == 0 || discRadius <= 0f)
            {
                return new SweptDiscGap(name, measured: false, gap: 0f, atX: 0f, atTick: 0);
            }
            //  점이 하나뿐이면 그 점만 잰다(길이 0짜리 구간).
            int segments = path.Count == 1 ? 1 : path.Count - 1;
            float best = float.MaxValue;
            float bestX = path[0].x;
            int bestTick = 0;
            for (int i = 0; i < segments; i++)
            {
                Vector3 from = path[i];
                Vector3 to = path.Count == 1 ? from : path[i + 1];
                float gap = Gap(from, to, bodyRadius, bodyHeight, discX, discY, discRadius,
                                out float fraction);
                if (gap < best)
                {
                    best = gap;
                    bestX = from.x + (to.x - from.x) * fraction;
                    bestTick = i;
                }
            }
            return new SweptDiscGap(name, measured: true, best, bestX, bestTick);
        }

        /// <summary>경로 하나 × 장애물 전부. ②-b가 쓰는 배치(<see cref="ObstaclePlacement"/>)를
        /// 그대로 받는다 — 허브 자리도 팔 길이도 거기서 이미 씬에서 뽑아 놓은 값이다.</summary>
        public static List<SweptDiscGap> MeasureAll(IReadOnlyList<Vector3> path,
                                                    float bodyRadius, float bodyHeight,
                                                    IReadOnlyList<ObstaclePlacement> placements)
        {
            var gaps = new List<SweptDiscGap>();
            if (path == null || path.Count == 0 || placements == null)
            {
                return gaps;
            }
            for (int i = 0; i < placements.Count; i++)
            {
                ObstaclePlacement placement = placements[i];
                if (placement.Measured == false)
                {
                    gaps.Add(new SweptDiscGap(placement.Name, measured: false, 0f, 0f, 0));
                    continue;
                }
                gaps.Add(Measure(placement.Name, path, bodyRadius, bodyHeight,
                                 placement.CenterX, placement.CenterY, placement.DiscRadius));
            }
            return gaps;
        }

        public static SweptDiscVerdict Judge(IReadOnlyList<SweptDiscGap> gaps)
        {
            if (gaps == null || gaps.Count == 0)
            {
                return default;
            }
            int measured = 0;
            int unmeasured = 0;
            var worst = default(SweptDiscGap);
            for (int i = 0; i < gaps.Count; i++)
            {
                SweptDiscGap gap = gaps[i];
                if (gap.Measured == false)
                {
                    unmeasured++;
                    continue;
                }
                //  <가 아니라 <=면 동점일 때 뒤엣것이 이겨 같은 입력에도 답이 목록 순서에 흔들린다.
                if (measured == 0 || gap.Gap < worst.Gap)
                {
                    worst = gap;
                }
                measured++;
            }
            return new SweptDiscVerdict(measured: true, measured, unmeasured, worst);
        }

        /// <summary>①의 자리별 줄에 덧붙일 문구. 잴 것이 없었으면 빈 문자열이다(줄을 안 늘린다).</summary>
        public static string Line(in SweptDiscVerdict verdict)
        {
            if (verdict.Measured == false)
            {
                return string.Empty;
            }
            //  못 잰 것이 남아 있어도 <b>이미 파고든 것을 봤다면</b> 답은 정해졌다 — "모르겠다"보다
            //  "아니다"가 강하다.
            if (verdict.DiscCount > 0 && verdict.Worst.Gap < 0f)
            {
                return $"⚠️ 틱 0 위상에서만 — {verdict.Worst.Name} 원반에 {-verdict.Worst.Gap:F2}m 파고듦"
                     + $" (x={verdict.Worst.AtX:F1} · {verdict.Worst.AtTick}틱)";
            }
            if (verdict.Unmeasured > 0)
            {
                string measured = verdict.DiscCount > 0
                    ? $" (잰 {verdict.DiscCount}개는 최소 {verdict.Worst.Gap:F2}m)"
                    : string.Empty;
                return $"⛔ 회전 무관 판정 못 함 — 원반을 못 잰 풍차 {verdict.Unmeasured}개{measured}";
            }
            return $"🌀 회전 무관 (풍차 {verdict.DiscCount}개 · 원반까지 최소 {verdict.Worst.Gap:F2}m"
                 + $" — {verdict.Worst.Name} x={verdict.Worst.AtX:F1} · {verdict.Worst.AtTick}틱)";
        }

        /// <summary>위 문구가 한 줄이라도 찍힐 때 ① 끝에 같이 적는 설명(줄바꿈 없음).
        /// <para>여기에 판정 글리프(✅/🟡/❌)를 쓰지 않는다 — 설명문은 자리별 판정과 무관하게
        /// 찍히므로, 넣으면 "이 리포트에 ✅가 있다"가 참이 되어 자리별 판정을 확인하는 검사가
        /// 공허해진다(리포트 머리말이 같은 이유로 글리프를 피한다).</para></summary>
        public static string Note()
            => "  (🌀 회전 무관 = 그 경로가 날개가 쓸고 가는 원반 밖으로만 지난다는 뜻이다 —"
             + " 날개가 어느 각도에 서 있어도 그 경로는 그대로 성립한다.\n"
             + "   ⚠️가 붙은 자리는 탐색이 본 틱 0 자세에서만 증명된 것이다 — 실제 판은 그 각도로"
             + " 시작하지 않는다.)";

        // ── 기하 ─────────────────────────────────────────────────────────────

        //  선분 둘의 2차원 최단거리. atFraction은 첫 선분 위 가장 가까운 자리(0~1)다.
        static float SegmentDistance(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1,
                                     out float atFraction)
        {
            if (TryCross(a0, a1, b0, b1, out atFraction))
            {
                return 0f;
            }
            //  안 만나면 최단거리는 반드시 네 끝점 중 하나에서 난다.
            float best = PointToSegment(a0, b0, b1, out _);
            atFraction = 0f;
            float distance = PointToSegment(a1, b0, b1, out _);
            if (distance < best)
            {
                best = distance;
                atFraction = 1f;
            }
            distance = PointToSegment(b0, a0, a1, out float fraction);
            if (distance < best)
            {
                best = distance;
                atFraction = fraction;
            }
            distance = PointToSegment(b1, a0, a1, out fraction);
            if (distance < best)
            {
                best = distance;
                atFraction = fraction;
            }
            return best;
        }

        //  두 선분이 실제로 교차하나. 길이 0짜리(점)나 나란한 선분은 false를 주고 위의 끝점
        //  비교에 맡긴다 — 그 경우는 끝점 넷만으로도 답이 정확하다.
        static bool TryCross(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, out float atFraction)
        {
            atFraction = 0f;
            Vector2 r = a1 - a0;
            Vector2 s = b1 - b0;
            float denominator = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denominator) < 1e-12f)
            {
                return false;
            }
            Vector2 gap = b0 - a0;
            float t = (gap.x * s.y - gap.y * s.x) / denominator;
            float u = (gap.x * r.y - gap.y * r.x) / denominator;
            if (t < 0f || t > 1f || u < 0f || u > 1f)
            {
                return false;
            }
            atFraction = t;
            return true;
        }

        static float PointToSegment(Vector2 point, Vector2 s0, Vector2 s1, out float atFraction)
        {
            Vector2 direction = s1 - s0;
            float lengthSquared = direction.sqrMagnitude;
            atFraction = lengthSquared <= 0f
                ? 0f
                : Mathf.Clamp01(Vector2.Dot(point - s0, direction) / lengthSquared);
            return Vector2.Distance(point, s0 + direction * atFraction);
        }
    }
}
