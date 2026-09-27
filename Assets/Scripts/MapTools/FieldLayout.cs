using System;
using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>낙하 샤프트 한 조각. 평지 바닥에 뚫린 선택 구멍 — 회랑은 위로 그대로 이어진다.</summary>
    public readonly struct ShaftPiece
    {
        public readonly float X0, ChimneyX0, X1, FloorY, Depth;
        public ShaftPiece(float x0, float floorY, float depth)
        {
            X0 = x0;
            ChimneyX0 = x0 + FieldLayout.ShaftWidth + FieldLayout.PocketFloor;
            X1 = ChimneyX0 + FieldLayout.ChimneyWidth;
            FloorY = floorY;
            Depth = depth;
        }
        public float Span => X1 - X0;
        public float PocketFloorY => FloorY - Depth;
        public float PocketTop => PocketFloorY + FieldLayout.PocketHeight;
    }

    /// <summary>
    /// 묶음 1 기믹의 자리(spec 2026-09-27 §2·§3). 샤프트는 구간마다 가장 긴 평지 가운데,
    /// 상승기류는 계곡 오르막 시작과 굴뚝, 하강기류는 샤프트 안, 홀로그램은 샤프트 뒤 첫 보통 관문.
    /// </summary>
    public static class FieldLayout
    {
        public const float ShaftWidth = 8f;
        public const float PocketFloor = 12f;
        public const float ChimneyWidth = 8f;
        /// <summary>주머니 안 높이 — 날갯짓 한 번(3.1m)과 몸(0.9m)의 두 배.</summary>
        public const float PocketHeight = 8f;
        public const float SideWall = 2f;
        /// <summary>샤프트 앞뒤로 관문을 두지 않는 거리. 파이프가 구멍 위에 서면 아래가 허공에 뜬다.</summary>
        public const float GateClear = 3f;
        public const float UpdraftWidth = 7f;
        public static readonly float[] ShaftDepths = { 15f, 20f, 25f };

        public static List<ShaftPiece> PlaceShafts(CourseProfile p, float startX, float length, int sections,
                                                   float corridorHalf)
        {
            var shafts = new List<ShaftPiece>();
            float need = ShaftWidth + PocketFloor + ChimneyWidth + 2f * (SideWall + GateClear + CourseProfileRule.GateMargin);
            float sectionLength = length / sections;
            for (int s = 0; s < sections; s++)
            {
                float lo = startX + sectionLength * s, hi = lo + sectionLength;
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
                float span = ShaftWidth + PocketFloor + ChimneyWidth;
                float x0 = (best.From + best.To) * 0.5f - span * 0.5f;
                float floorY = p.CenterAt(x0) - corridorHalf;
                shafts.Add(new ShaftPiece(x0, floorY, ShaftDepths[Math.Min(s, ShaftDepths.Length - 1)]));
            }
            return shafts;
        }

        public static bool GateBlocked(IReadOnlyList<ShaftPiece> shafts, float x)
        {
            for (int i = 0; i < shafts.Count; i++)
            {
                if (x > shafts[i].X0 - GateClear && x < shafts[i].X1 + GateClear) { return true; }
            }
            return false;
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

        /// <summary>샤프트 굴뚝 뒤 첫 보통 관문(창이 하나인). 없으면 −1.</summary>
        public static int HologramGate(IReadOnlyList<CoursePipe> pipes, ShaftPiece shaft)
        {
            for (int i = 0; i < pipes.Count; i++)
            {
                if (pipes[i].X > shaft.X1 + GateClear && pipes[i].HasChallenge == false) { return i; }
            }
            return -1;
        }
    }
}
