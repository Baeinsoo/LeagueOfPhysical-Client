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
        /// <summary>쓸고 지나가는 x 범위 — 🚪 검사가 "띠에 들어가기 전"과 "띠 끝"을 여기서 잰다.</summary>
        public float BandX0 => Sector.HalfAngleDegrees >= 180f
            ? Sector.CenterX - Sector.Radius
            : Sector.CenterX - Sector.Radius * Mathf.Sin(Mathf.Min(90f, Sector.HalfAngleDegrees) * Mathf.Deg2Rad);
        public float BandX1 => 2f * Sector.CenterX - BandX0;
    }

    /// <summary>
    /// 갈림길 입구 문지기의 자리(spec 2026-10-03 §2.1). 빌딩 위층 = 광고판, 언덕 굴 = 진자,
    /// 계곡 지름길은 <b>첫 번째만</b> 진자 — 마지막 계곡은 타이밍 없는 지름길로 남긴다.
    /// </summary>
    public static class GuardLayout
    {
        public const string MarkerPrefix = "Guard_";
        public const float PeriodSeconds = 2.5f;
        public const float PendulumAmplitude = 55f;
        public const float RodThickness = 0.3f;
        public const float TipWidth = 1.6f;
        public const float TipHeight = 0.8f;
        //  맨 아래에서 남는 틈. 새(0.9m)보다 작아야 "아래로 빠져나감"이 없다.
        public const float BottomGap = 0.6f;
        public const float PendulumInset = 1.5f;
        public const float MinRod = 2.5f;
        public const float MaxRod = 6f;
        public const float BoardLength = 7f;
        public const float BoardThickness = 0.6f;
        //  반 바퀴마다 한 번 열린다 — 반 바퀴 2.5초.
        public const float BoardSpeed = 180f / PeriodSeconds;
        public const float BoardFrontGap = 0.3f;
        public const float BoardRiseOverFloor = 0.3f;

        public static List<GuardSpot> ForCourse(IReadOnlyList<Branch> branches)
        {
            var spots = new List<GuardSpot>();
            if (branches == null) { return spots; }
            var sorted = new List<Branch>(branches);
            sorted.Sort((a, b) => a.Rect.X0.CompareTo(b.Rect.X0));
            bool valleyTaken = false;
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
                    default:
                        if (valleyTaken == false && TryPendulum(b, out var valley)) { spots.Add(valley); valleyTaken = true; }
                        break;
                }
            }
            return spots;
        }

        public static bool TryPendulum(in Branch b, out GuardSpot spot)
        {
            float rod = (b.Rect.Y1 - b.Rect.Y0) - BottomGap - TipHeight * 0.5f;
            if (rod < MinRod)
            {
                spot = default;
                return false;
            }
            rod = Mathf.Min(rod, MaxRod);
            float pivotX = b.Rect.X0 + PendulumInset;
            float pivotY = b.Rect.Y1;
            //  끝 철골의 모서리까지 덮는다 — 막대 끝에서 반 폭만큼 옆으로, 반 높이만큼 아래로 더 나간다.
            float reach = Mathf.Sqrt((rod + TipHeight * 0.5f) * (rod + TipHeight * 0.5f) + TipWidth * TipWidth * 0.25f);
            float extra = Mathf.Atan2(TipWidth * 0.5f, rod) * Mathf.Rad2Deg;
            var sector = new GuardSector(pivotX, pivotY, reach, -90f, PendulumAmplitude + extra);
            spot = new GuardSpot(GuardKind.Pendulum, b.Rect.X0, pivotX, pivotY, rod, PendulumAmplitude, sector);
            return true;
        }

        public static GuardSpot Billboard(in Branch b)
        {
            float half = BoardLength * 0.5f;
            float pivotX = b.Rect.X0 - BoardFrontGap - half;
            float pivotY = b.Rect.Y0 + BoardRiseOverFloor + half;
            float reach = Mathf.Sqrt(half * half + BoardThickness * BoardThickness * 0.25f);
            var sector = new GuardSector(pivotX, pivotY, reach, 0f, 180f);
            return new GuardSpot(GuardKind.Billboard, b.Rect.X0, pivotX, pivotY, BoardLength, 0f, sector);
        }
    }
}
