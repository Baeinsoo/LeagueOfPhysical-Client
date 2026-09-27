using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>한 발 승부 결과 화면의 계산 — 선수 색, 과녁 좌표, 제목, 점 순서, 문구. 그리기는 뷰가 한다.</summary>
    public static class ArcheryShootOffResultLayout
    {
        public const float PinStaggerSeconds = 0.15f;
        private const float DrawLimit = 1.25f;
        private const float CloseGapMeters = 0.03f;
        private const int CloseBeforeNextTicks = 60;   // 다음 라운드 과녁이 서기 1.2초 전

        private static readonly Color[] Palette =
        {
            new Color(1f, 0x4F / 255f, 0x5E / 255f),
            new Color(0x2E / 255f, 0xC4 / 255f, 0xA6 / 255f),
            new Color(0x6C / 255f, 0x63 / 255f, 0xFF / 255f),
            new Color(0xF5 / 255f, 0x9E / 255f, 0x0B / 255f),
        };

        //  화면 좌우 배치와 같은 기준 — 누구 화면에서 줄 세워도 같은 순서다.
        public static List<string> Roster(IEnumerable<string> ids)
        {
            var list = new List<string>(ids);
            list.Sort(string.CompareOrdinal);
            return list;
        }

        public static Color ColorOf(IReadOnlyList<string> roster, string id)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i] == id)
                {
                    return Palette[i % Palette.Length];
                }
            }
            return Color.white;   // 판 중간에 나간 사람
        }

        public static long CloseTick(long roundCloseTick, int stepGapTicks)
            => roundCloseTick + stepGapTicks - CloseBeforeNextTicks;

        public static bool IsDrawn(Vector2 faceMeters, float faceRadius)
            => faceRadius > 0f && faceMeters.magnitude <= faceRadius * DrawLimit;

        //  과녁 좌표(미터, 오른쪽·위) → 패널 좌표(픽셀, 오른쪽·아래).
        public static Vector2 ToPanel(Vector2 faceMeters, float faceRadius, Vector2 center, float panelRadius)
            => new Vector2(center.x + faceMeters.x / faceRadius * panelRadius,
                           center.y - faceMeters.y / faceRadius * panelRadius);

        public static string Headline(IReadOnlyList<ArcheryRoundPlacement> byRank, Func<string, string> nameOf)
        {
            if (byRank.Count == 0 || byRank[0].Hit == false)
            {
                return "아무도 못 맞혔다!";
            }
            if (byRank.Count >= 2 && byRank[1].Hit)
            {
                if (byRank[1].Rank == byRank[0].Rank)
                {
                    return "공동 1위!";
                }
                float gap = byRank[1].Distance - byRank[0].Distance;
                if (gap <= CloseGapMeters + 1e-5f)
                {
                    return $"단 {Mathf.Max(1, Mathf.RoundToInt(gap * 100f))}cm 차이!";
                }
            }
            return $"{nameOf(byRank[0].ShooterId)} 승!";
        }

        //  꼴찌부터 1등 순 — 1등 점이 마지막에, 맨 위에 찍힌다.
        public static List<int> PinOrder(IReadOnlyList<ArcheryRoundPlacement> byRank, float faceRadius)
        {
            var order = new List<int>();
            for (int i = byRank.Count - 1; i >= 0; i--)
            {
                if (byRank[i].Hit && IsDrawn(byRank[i].FaceOffset, faceRadius))
                {
                    order.Add(i);
                }
            }
            return order;
        }

        public static int PinsShown(float secondsSinceOpen, int pinCount)
        {
            if (pinCount <= 0)
            {
                return 0;
            }
            int shown = Mathf.FloorToInt(Mathf.Max(0f, secondsSinceOpen) / PinStaggerSeconds + 1e-4f) + 1;
            return Mathf.Clamp(shown, 1, pinCount);
        }

        public static string RankLabel(int rank) => $"{rank + 1}위";

        public static string PointsText(int points, int multiplier)
            => multiplier > 1 ? $"+{points} ({multiplier}배)" : $"+{points}";

        public static string DistanceText(bool hit, float distanceMeters)
            => hit ? $"{Mathf.RoundToInt(distanceMeters * 100f)}cm" : "빗나감";

        //  양궁 과녁 색 — 띠 점수로 고른다(띠 개수가 달라도 맞는 색이 나온다).
        public static Color BandColor(int points)
        {
            if (points >= 9) return new Color(1f, 0.82f, 0.2f);
            if (points >= 7) return new Color(0.9f, 0.22f, 0.22f);
            if (points >= 5) return new Color(0.25f, 0.55f, 0.95f);
            if (points >= 3) return new Color(0.15f, 0.15f, 0.17f);
            return new Color(0.96f, 0.96f, 0.96f);
        }
    }
}
