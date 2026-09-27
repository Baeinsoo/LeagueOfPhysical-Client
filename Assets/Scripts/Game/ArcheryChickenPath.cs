using UnityEngine;

namespace LOP
{
    public readonly struct ArcheryChickenState
    {
        public readonly bool Visible;
        public readonly Vector3 Position;
        public readonly Vector3 Heading;
        /// <summary>걸은 거리 ÷ 보폭 — 다리를 번갈아 움직이는 데 쓴다.</summary>
        public readonly float StepPhase;

        public ArcheryChickenState(bool visible, Vector3 position, Vector3 heading, float stepPhase)
        {
            Visible = visible;
            Position = position;
            Heading = heading;
            StepPhase = stepPhase;
        }
    }

    /// <summary>
    /// 과녁 뒤를 가로지르는 닭. 라운드 시작부터 흐른 시간만으로 자리를 정하므로 모든 클라에서 같은 때 같은 곳에 보인다.
    /// 판정과는 상관없다(닭에는 충돌체가 없다).
    /// </summary>
    public static class ArcheryChickenPath
    {
        public const float StartDelaySeconds = 1f;
        public const float Speed = 2f;
        public const float HalfSpan = 7f;
        public const float BehindTarget = 3f;
        public const float ScareRadius = 1.5f;
        private const float StepLength = 0.15f;

        private static readonly int[] Rounds = { 2, 6, 9 };

        public static bool IsChickenRound(int round) => System.Array.IndexOf(Rounds, round) >= 0;

        public static int DirectionOf(int round)
        {
            int k = System.Array.IndexOf(Rounds, round);
            return k < 0 ? 0 : (k % 2 == 0 ? 1 : -1);
        }

        public static ArcheryChickenState At(int round, double secondsSinceRoundStart, Vector3 standCenter,
                                             Vector3 forward, Vector3 right, float groundY)
        {
            int direction = DirectionOf(round);
            float walked = (float)(secondsSinceRoundStart - StartDelaySeconds) * Speed;
            if (direction == 0 || walked < 0f || walked > HalfSpan * 2f)
            {
                return default;
            }
            float lateral = direction * (walked - HalfSpan);
            Vector3 position = standCenter + forward * BehindTarget + right * lateral;
            position.y = groundY;
            return new ArcheryChickenState(true, position, right * direction, walked / StepLength);
        }
    }
}
