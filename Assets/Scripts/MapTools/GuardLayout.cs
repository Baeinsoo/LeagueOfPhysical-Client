using System.Collections.Generic;
using UnityEngine;

namespace LOP.MapTools
{
    public enum GuardKind { Pendulum, Billboard, Shutter }

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
        /// <summary>셔터: 갈림길 칸 바닥·천장과 다 열렸을 때 문이 올라간 거리.</summary>
        public readonly float Y0, Y1, Travel;
        public GuardSpot(GuardKind kind, float branchX0, float pivotX, float pivotY, float length,
                         float amplitudeDegrees, in GuardSector sector)
        {
            Kind = kind; BranchX0 = branchX0; PivotX = pivotX; PivotY = pivotY; Length = length;
            AmplitudeDegrees = amplitudeDegrees; Sector = sector;
            Y0 = 0f; Y1 = 0f; Travel = 0f;
        }
        private GuardSpot(float branchX0, float doorX, float y0, float y1, float travel)
        {
            Kind = GuardKind.Shutter; BranchX0 = branchX0; PivotX = doorX; PivotY = y0; Length = 0f;
            AmplitudeDegrees = 0f; Sector = default;
            Y0 = y0; Y1 = y1; Travel = travel;
        }
        internal static GuardSpot NewShutter(float branchX0, float doorX, float y0, float y1, float travel)
            => new GuardSpot(branchX0, doorX, y0, y1, travel);

        /// <summary>같은 셔터를 문 가운데만 옮겨서. 빌더가 숨을 자리를 찾아 민 x를 검사기가 씬에서 다시 읽는다.</summary>
        public GuardSpot AtDoorX(float doorX) => NewShutter(BranchX0, doorX, Y0, Y1, Travel);

        public string MarkerName => $"{GuardLayout.MarkerPrefix}{BranchX0:F0}_{Kind}";
        public string Label => Kind == GuardKind.Shutter ? "셔터" : Kind == GuardKind.Pendulum ? "철골 진자" : "회전 광고판";

        /// <summary>셔터 문 가운데 x(= PivotX).</summary>
        public float DoorX => PivotX;
        /// <summary>닫힌 문의 아래·위 끝. 바닥보다 조금 아래까지 내려와 틈이 없고, 위는 천장 속에 묻힌다.</summary>
        public float DoorBottom => Y0 - GuardLayout.ShutterFloorDip;
        public float DoorTop => Y1 + GuardLayout.ShutterTopInset;
        /// <summary>문이 닫힘~다 열림 사이에 한 번이라도 차지하는 사각형 — 기본 길 안전 증명이 이걸 피한다.</summary>
        public Box2 SweepRect => new Box2(DoorX - GuardLayout.ShutterWidth * 0.5f, DoorBottom,
                                          DoorX + GuardLayout.ShutterWidth * 0.5f, DoorTop + Travel);

        /// <summary>
        /// 쓸고 지나가는 x 범위 — 🚪 검사가 "띠에 들어가기 전"과 "띠 끝"을 여기서 잰다.
        /// 진자는 축이 똑바로 아래(−90°)거나 원 전체(반각 ≥180°)일 때만 맞는 식이다.
        /// </summary>
        public float BandX0 => Kind == GuardKind.Shutter ? DoorX - GuardLayout.ShutterWidth * 0.5f
            : Sector.HalfAngleDegrees >= 180f
            ? Sector.CenterX - Sector.Radius
            : Sector.CenterX - Sector.Radius * Mathf.Sin(Mathf.Min(90f, Sector.HalfAngleDegrees) * Mathf.Deg2Rad);
        public float BandX1 => 2f * PivotX - BandX0;
    }

    /// <summary>
    /// 갈림길 입구 문지기의 자리(spec 2026-10-03 §2.1). 빌딩 위층·언덕 굴 입구 안쪽마다 <b>셔터</b> 하나(2026-10-04 사용자 결정).
    /// 칸 안에 머무는 것(진자·광고판)은 날갯짓 호(높이 ≈2.9m, 길이 ≈4.3m)가 지날 틈을 못 남겼다 — 셔터는 천장 속으로
    /// 다 들어가 칸을 통째로 연다. 아래 진자·광고판 설명은 그 전 배치다.
    /// 광고판은 위층 입구 <b>안쪽</b>에서 돈다 — 바닥·천장과 0.3씩 띄운 원이라 위로 넘어가거나 아래로 빠질 수 없고,
    /// 판이 수평일 때만 위·아래로 지나간다. (입구 앞 허공에 두면 새가 넘어가 위층에 떨어져 늘 열려 있었다.)
    /// 진자는 언덕 굴 입구 안쪽 천장에 달고 막대를 칸 높이보다 2m 짧게 — 진자가 옆으로 비킨 틈이나 아래로 지난다.
    /// 계곡 지름길은 문지기 없이 남긴다 — 입구 앞이 계곡 가장자리 땅이고 뒤가 물결 굴이라 진자를 걸 자리가 없다(2026-10-04 사용자 결정).
    /// </summary>
    public static class GuardLayout
    {
        public const string MarkerPrefix = "Guard_";
        public const float ShutterWidth = 0.8f;
        //  다 열렸을 때 문 바닥이 천장보다 이만큼(−바닥 묻힘) 더 올라간다.
        public const float ShutterHide = 0.3f;
        //  닫힌 문 위쪽이 천장 속에 묻힌 깊이 — 문 위로 빈틈이 안 보이게.
        public const float ShutterTopInset = 0.45f;
        //  닫힌 문이 바닥 속으로 들어간 깊이 — 문 밑으로 새는 틈이 없게.
        public const float ShutterFloorDip = 0.05f;
        //  입구 벽(X0)에서 문 앞면까지.
        public const float ShutterInset = 0.3f;
        public const float ShutterOpenShare = 0.4f;
        public const float ShutterMoveShare = 0.15f;
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
                if (b.Kind == BranchKind.Building || b.Kind == BranchKind.Hill)
                {
                    spots.Add(Shutter(b, DefaultDoorX(b)));
                }
            }
            return spots;
        }

        public static float DefaultDoorX(in Branch b) => b.Rect.X0 + ShutterInset + ShutterWidth * 0.5f;

        /// <summary>문 가운데를 <paramref name="doorX"/>에 둔 셔터. 빌더가 숨을 자리를 찾아 x를 밀 수 있다.</summary>
        public static GuardSpot Shutter(in Branch b, float doorX)
        {
            float travel = (b.Rect.Y1 - b.Rect.Y0) + ShutterHide;
            return GuardSpot.NewShutter(b.Rect.X0, doorX, b.Rect.Y0, b.Rect.Y1, travel);
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
