using System.Collections.Generic;

namespace LOP.MapTools
{
    public enum GuardKind { Shutter }

    /// <summary>문지기가 쓸고 지나가는 부채꼴. 반각이 180° 이상이면 원 전체. (지금 코스는 셔터라 안 쓰지만 GuardRule이 잰다.)</summary>
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
        /// <summary>BranchX0 = 갈림길 입구 x, DoorX = 문 가운데 x. Y0·Y1 = 칸 바닥·천장, Travel = 다 열렸을 때 문이 올라간 거리.</summary>
        public readonly float BranchX0, DoorX, Y0, Y1, Travel;

        private GuardSpot(float branchX0, float doorX, float y0, float y1, float travel)
        {
            Kind = GuardKind.Shutter; BranchX0 = branchX0; DoorX = doorX; Y0 = y0; Y1 = y1; Travel = travel;
        }
        internal static GuardSpot NewShutter(float branchX0, float doorX, float y0, float y1, float travel)
            => new GuardSpot(branchX0, doorX, y0, y1, travel);

        /// <summary>같은 셔터를 문 가운데만 옮겨서. 빌더가 숨을 자리를 찾아 민 x를 검사기가 씬에서 다시 읽는다.</summary>
        public GuardSpot AtDoorX(float doorX) => NewShutter(BranchX0, doorX, Y0, Y1, Travel);

        public string MarkerName => $"{GuardLayout.MarkerPrefix}{BranchX0:F0}_{Kind}";
        public string Label => "셔터";
        /// <summary>리포트의 x(= DoorX). 문지기가 진자이던 때 이름이다.</summary>
        public float PivotX => DoorX;

        /// <summary>닫힌 문의 아래·위 끝. 바닥보다 조금 아래까지 내려와 틈이 없고, 위는 천장 속에 묻힌다.</summary>
        public float DoorBottom => Y0 - GuardLayout.ShutterFloorDip;
        public float DoorTop => Y1 + GuardLayout.ShutterTopInset;
        /// <summary>문이 닫힘~다 열림 사이에 한 번이라도 차지하는 사각형 — 기본 길 안전 증명이 이걸 피한다.</summary>
        public Box2 SweepRect => new Box2(BandX0, DoorBottom, BandX1, DoorTop + Travel);
        /// <summary>문이 차지하는 x 범위 — 🚪 검사가 "띠에 들어가기 전"과 "띠 끝"을 여기서 잰다.</summary>
        public float BandX0 => DoorX - GuardLayout.ShutterWidth * 0.5f;
        public float BandX1 => DoorX + GuardLayout.ShutterWidth * 0.5f;
    }

    /// <summary>
    /// 갈림길 입구 문지기의 자리(spec 2026-10-03 §2.1). 빌딩 위층·언덕 굴 입구 안쪽마다 <b>셔터</b> 하나(2026-10-04 사용자 결정).
    /// 칸 안에 머무는 것(진자·광고판)은 날갯짓 호(높이 ≈2.9m, 길이 ≈4.3m)가 지날 틈을 못 남겼다 — 셔터는 천장 속으로
    /// 다 들어가 칸을 통째로 연다. 계곡 지름길은 문지기 없이 남긴다(2026-10-04 사용자 결정).
    /// </summary>
    public static class GuardLayout
    {
        public const string MarkerPrefix = "Guard_";
        public const float PeriodSeconds = 2.5f;
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
    }
}
