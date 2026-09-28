using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public readonly struct ArcheryCrowdSeat
    {
        public readonly Vector3 Position;
        public readonly int Row;
        public readonly int Shirt;
        public readonly int Skin;
        public readonly float Scale;
        public readonly float Phase;
        public readonly Quaternion Facing;
        public readonly int Sign;

        public ArcheryCrowdSeat(Vector3 position, int row, int shirt, int skin, float scale, float phase,
                                Quaternion facing, int sign)
        {
            Position = position;
            Row = row;
            Shirt = shirt;
            Skin = skin;
            Scale = scale;
            Phase = phase;
            Facing = facing;
            Sign = sign;
        }
    }

    public readonly struct ArcheryCrowdBox
    {
        public readonly Vector3 Center;
        public readonly Vector3 Size;
        public readonly Quaternion Rotation;

        public ArcheryCrowdBox(Vector3 center, Vector3 size, Quaternion rotation)
        {
            Center = center;
            Size = size;
            Rotation = rotation;
        }
    }

    /// <summary>
    /// 사대 양옆 관중석. 조준 중(1인칭)에도 곁눈으로 들어와야 하고, 85m 너머보다 실루엣이 커서 알아볼 수 있다.
    /// 배치는 고정 씨앗으로 뽑는다 — 어느 클라에서나 같은 관중석이 보인다.
    /// </summary>
    public sealed class ArcheryCrowdLayout
    {
        public const float SideOffset = 8.5f;
        public const int Rows = 2;
        public const float RowRise = 0.6f;
        public const float RowDepth = 1.2f;
        public const float Spacing = 0.66f;
        public const int PerRow = 36;
        public const float BackFrom = -2f;
        public const float Length = 24f;
        public const float FanHeight = 1.7f;
        private const int Seed = 20260928;

        public static readonly Color[] Shirts =
        {
            Hex(0xFF4F5E), Hex(0x2EC4A6), Hex(0x6C63FF), Hex(0xF59E0B), Hex(0x3B82F6),
            Hex(0xEC4899), Hex(0x10B981), Hex(0xF97316), Hex(0x8B5CF6), Hex(0xFACC15),
        };
        public static readonly Color[] Skins = { Hex(0xF5D0A9), Hex(0xE0AC80), Hex(0xC68642), Hex(0x8D5524), Hex(0xFFE0BD) };
        public static readonly Color BooGray = Hex(0x6B6470);

        private readonly List<ArcheryCrowdSeat> seats = new List<ArcheryCrowdSeat>();
        private readonly List<ArcheryCrowdBox> steps = new List<ArcheryCrowdBox>();
        private readonly List<ArcheryCrowdBox> volumes = new List<ArcheryCrowdBox>();

        public IReadOnlyList<ArcheryCrowdSeat> Seats => seats;
        public IReadOnlyList<ArcheryCrowdBox> Steps => steps;
        public IReadOnlyList<ArcheryCrowdBox> Volumes => volumes;
        public Vector3 Right { get; private set; }

        public static ArcheryCrowdLayout Build(Vector3 shooterPosition, Vector3 forward)
        {
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.forward;
            var layout = new ArcheryCrowdLayout { Right = ArcheryTargetMotion.ShooterRightAxis(-forward) };
            var boxRotation = Quaternion.LookRotation(forward);
            var random = new System.Random(Seed);
            //  팻말 자리(앞줄 번호) — 왼쪽 2개, 오른쪽 3개.
            var leftSigns = new[] { 9, 27 };
            var rightSigns = new[] { 5, 18, 31 };
            int sign = 0;
            float mid = BackFrom + Length * 0.5f;

            foreach (int side in new[] { -1, 1 })
            {
                var facing = Quaternion.LookRotation(-layout.Right * side);   // 레인 쪽을 본다
                for (int row = 0; row < Rows; row++)
                {
                    float lateral = side * (SideOffset + row * RowDepth);
                    float top = (row + 1) * RowRise;
                    Vector3 rowCenter = shooterPosition + layout.Right * lateral + forward * mid;
                    layout.steps.Add(new ArcheryCrowdBox(rowCenter + Vector3.up * (top * 0.5f),
                                                         new Vector3(RowDepth, top, Length + 0.5f), boxRotation));
                    layout.volumes.Add(new ArcheryCrowdBox(rowCenter + Vector3.up * (top + FanHeight * 0.5f),
                                                           new Vector3(RowDepth, FanHeight, Length + 0.5f), boxRotation));

                    float stagger = row % 2 == 0 ? -Spacing * 0.25f : Spacing * 0.25f;
                    var signsHere = row == 0 ? (side < 0 ? leftSigns : rightSigns) : System.Array.Empty<int>();
                    for (int i = 0; i < PerRow; i++)
                    {
                        float along = BackFrom + Spacing * (i + 0.5f) + stagger;
                        int shirt = random.Next(Shirts.Length);
                        int skin = random.Next(Skins.Length);
                        float scale = 0.92f + (float)random.NextDouble() * 0.16f;
                        float phase = (float)random.NextDouble() * Mathf.PI * 2f;
                        bool isSign = System.Array.IndexOf(signsHere, i) >= 0;
                        Vector3 feet = shooterPosition + layout.Right * lateral + forward * along + Vector3.up * top;
                        layout.seats.Add(new ArcheryCrowdSeat(feet, row, shirt, skin, scale, phase, facing, isSign ? sign++ : -1));
                    }
                }
            }
            return layout;
        }

        /// <summary>아직 화살을 안 맞은 사람 중 <paramref name="at"/>에 가장 가까운 사람. 없으면 −1.</summary>
        public static int NearestFree(IReadOnlyList<Vector3> points, System.Func<int, bool> taken, Vector3 at)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                if (taken(i))
                {
                    continue;
                }
                float sqr = (points[i] - at).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }

        private static Color Hex(int rgb)
            => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
