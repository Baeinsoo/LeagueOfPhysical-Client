using System.Collections.Generic;
using UnityEngine;

namespace LOP.MapTools
{
    public enum GuardKind { Pendulum, Billboard }

    /// <summary>문지기가 쓸고 지나가는 범위. 반각이 180° 이상이면 원 전체(광고판).</summary>
    public readonly struct GuardSector
    {
        public readonly float CenterX, CenterY, Radius, AxisDegrees, HalfAngleDegrees;
        public GuardSector(float centerX, float centerY, float radius, float axisDegrees, float halfAngleDegrees)
        {
            CenterX = centerX; CenterY = centerY; Radius = radius; AxisDegrees = axisDegrees; HalfAngleDegrees = halfAngleDegrees;
        }
    }

    public readonly struct GuardSpot
    {
        public readonly GuardKind Kind;
        public readonly float BranchX0, PivotX, PivotY, Length, AmplitudeDegrees;
        public readonly GuardSector Sector;
        public GuardSpot(GuardKind kind, float branchX0, float pivotX, float pivotY, float length,
                         float amplitudeDegrees, in GuardSector sector)
        {
            Kind = kind; BranchX0 = branchX0; PivotX = pivotX; PivotY = pivotY; Length = length;
            AmplitudeDegrees = amplitudeDegrees; Sector = sector;
        }
        public string MarkerName => $"{GuardLayout.MarkerPrefix}{BranchX0:F0}_{Kind}";
        public string Label => Kind == GuardKind.Pendulum ? "철골 진자" : "회전 광고판";
        /// <summary>
        /// 쓸고 지나가는 x 범위 — 🚪 검사가 "띠에 들어가기 전"과 "띠 끝"을 여기서 잰다.
        /// 축이 똑바로 아래(−90°)거나 원 전체(반각 ≥180°)일 때만 맞는 식이다 — 지금 두 문지기 다 그렇다.
        /// </summary>
        public float BandX0 => Sector.HalfAngleDegrees >= 180f
            ? Sector.CenterX - Sector.Radius
            : Sector.CenterX - Sector.Radius * Mathf.Sin(Mathf.Min(90f, Sector.HalfAngleDegrees) * Mathf.Deg2Rad);
        public float BandX1 => 2f * Sector.CenterX - BandX0;
    }

    /// <summary>
    /// 갈림길 입구 문지기의 자리(spec 2026-10-03 §2.1). 빌딩 위층 = 광고판, 언덕 굴 = 진자.
    /// 광고판은 위층 입구 <b>안쪽</b>에서 돈다 — 바닥·천장과 0.3씩 띄운 원이라 위로 넘어가거나 아래로 빠질 수 없고,
    /// 판이 수평일 때만 위·아래로 지나간다. (입구 앞 허공에 두면 새가 넘어가 위층에 떨어져 늘 열려 있었다.)
    /// 진자는 언덕 굴 입구 안쪽 천장에 달고 막대를 칸 높이보다 2m 짧게 — 진자가 옆으로 비킨 틈이나 아래로 지난다.
    /// 계곡 지름길은 문지기 없이 남긴다 — 입구 앞이 계곡 가장자리 땅이고 뒤가 물결 굴이라 진자를 걸 자리가 없다(2026-10-04 사용자 결정).
    /// </summary>
    public static class GuardLayout
    {
        public const string MarkerPrefix = "Guard_";
        public const float PeriodSeconds = 2.5f;
        public const float PendulumAmplitude = 55f;
        public const float RodThickness = 0.3f;
        public const float TipWidth = 1.6f;
        public const float TipHeight = 0.8f;
        //  막대 길이 = 칸 높이 − 이 값. 한 지점에서만 출발하는 탐색으론 0/125였다 — 아래로 지나갈 틈을 남긴다.
        public const float PendulumBelowClear = 2.0f;
        public const float PendulumInset = 1.5f;
        public const float MinRod = 2.5f;
        public const float MaxRod = 6f;
        public const float BoardThickness = 0.6f;
        //  반 바퀴마다 한 번 열린다 — 반 바퀴 2.5초.
        public const float BoardSpeed = 180f / PeriodSeconds;
        //  판이 돌며 닿는 원과 바닥·천장 사이, 입구 벽과 원 사이의 틈.
        public const float BoardWallGap = 0.3f;
        public const float BoardInsideGap = 0.3f;

        public static List<GuardSpot> ForCourse(IReadOnlyList<Branch> branches)
        {
            var spots = new List<GuardSpot>();
            if (branches == null) { return spots; }
            var sorted = new List<Branch>(branches);
            sorted.Sort((a, b) => a.Rect.X0.CompareTo(b.Rect.X0));
            foreach (Branch b in sorted)
            {
                switch (b.Kind)
                {
                    case BranchKind.Building:
                        spots.Add(Billboard(b));
                        break;
                    case BranchKind.Hill:
                        if (TryPendulum(b, out var hill)) { spots.Add(hill); }
                        break;
                }
            }
            return spots;
        }

        public static bool TryPendulum(in Branch b, out GuardSpot spot)
        {
            float pivotY = b.Rect.Y1;
            float rod = (b.Rect.Y1 - b.Rect.Y0) - PendulumBelowClear;
            if (rod < MinRod)
            {
                spot = default;
                return false;
            }
            rod = Mathf.Min(rod, MaxRod);
            //  끝 철골의 모서리까지 덮는다 — 막대 끝에서 반 폭만큼 옆으로, 반 높이만큼 아래로 더 나간다.
            float reach = Mathf.Sqrt((rod + TipHeight * 0.5f) * (rod + TipHeight * 0.5f) + TipWidth * TipWidth * 0.25f);
            //  가까운 모서리(안쪽)는 축에서 반높이만큼 덜 나간 자리에서 반폭만큼 벌어진다 — 각도는 그 모서리 기준.
            float extra = Mathf.Atan2(TipWidth * 0.5f, rod - TipHeight * 0.5f) * Mathf.Rad2Deg;
            float pivotX = b.Rect.X0 + PendulumInset;
            var sector = new GuardSector(pivotX, pivotY, reach, -90f, PendulumAmplitude + extra);
            spot = new GuardSpot(GuardKind.Pendulum, b.Rect.X0, pivotX, pivotY, rod, PendulumAmplitude, sector);
            return true;
        }

        public static GuardSpot Billboard(in Branch b)
        {
            //  원(reach)이 바닥·천장에서 0.3씩 떨어지게 — 판 길이는 모서리(반길이, 반두께)가 그 원 안에 들게 거꾸로 정한다.
            float reach = (b.Rect.Y1 - b.Rect.Y0) * 0.5f - BoardWallGap;
            float length = 2f * Mathf.Sqrt(reach * reach - BoardThickness * BoardThickness * 0.25f);
            float pivotX = b.Rect.X0 + BoardInsideGap + reach;
            float pivotY = (b.Rect.Y0 + b.Rect.Y1) * 0.5f;
            var sector = new GuardSector(pivotX, pivotY, reach, 0f, 180f);
            return new GuardSpot(GuardKind.Billboard, b.Rect.X0, pivotX, pivotY, length, 0f, sector);
        }
    }
}
