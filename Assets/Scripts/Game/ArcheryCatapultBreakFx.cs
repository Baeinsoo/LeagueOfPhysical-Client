using UnityEngine;

namespace LOP
{
    public enum ArcheryBreakKind
    {
        /// <summary>두 쪽으로 갈라져 좌우로 떨어진다(방패·사과).</summary>
        Split,
        /// <summary>통째로 "깡" 하고 사수 반대쪽으로 튕겨 오른 뒤 떨어진다(투구).</summary>
        Clang,
        /// <summary>금화가 고리처럼 우수수 퍼진다(성배).</summary>
        Coins,
        /// <summary>쏘면 안 되는 것 — 깨지지 않고 빨갛게 번쩍이며 떨어진다(관중이 야유한다).</summary>
        Shame,
    }

    /// <summary>
    /// 투석기 표적이 맞았을 때 조각이 어디로 가는지. 시간(t초)만 보고 답하는 순수 계산이라 시험할 수 있고,
    /// 프레임 수와 상관없이 같은 모양이 나온다. 연출일 뿐이라 클·서가 맞출 필요는 없다.
    /// </summary>
    public static class ArcheryCatapultBreakFx
    {
        public const float Seconds = 1.2f;
        public const int CoinCount = 12;
        private const float Gravity = 9.81f;

        //  번쩍이는 반 주기. 떨어지는 1.2초 동안 네 번 빨개진다 — 한 번이면 "맞았다"로만 읽힌다.
        private const float ShameBlinkHalf = 0.15f;

        public static ArcheryBreakKind KindOf(string code)
        {
            switch (code)
            {
                case "Helmet": return ArcheryBreakKind.Clang;
                case "Grail": return ArcheryBreakKind.Coins;
                case "KingFlag":
                case "FlowerBasket": return ArcheryBreakKind.Shame;
                default: return ArcheryBreakKind.Split;
            }
        }

        public static int PieceCount(ArcheryBreakKind kind)
            => kind == ArcheryBreakKind.Split ? 2 : kind == ArcheryBreakKind.Coins ? CoinCount : 1;

        /// <summary>맞은 자리에서 t초 뒤 조각의 위치 차이(세계 좌표). <paramref name="toShooter"/>는 표적이 사수를 보는 쪽.</summary>
        public static Vector3 PieceOffset(ArcheryBreakKind kind, int piece, float t, Vector3 toShooter)
        {
            if (t <= 0f)
            {
                return Vector3.zero;
            }
            Vector3 away = -Flat(toShooter);
            Vector3 side = Vector3.Cross(Vector3.up, away).normalized;   // 사수 기준 오른쪽
            Vector3 v;
            switch (kind)
            {
                case ArcheryBreakKind.Split:
                    v = side * (piece == 0 ? -2.2f : 2.2f) + Vector3.up * 2.0f + away * 0.8f;
                    break;
                case ArcheryBreakKind.Clang:
                    v = away * 3.5f + Vector3.up * 4.5f;
                    break;
                case ArcheryBreakKind.Coins:
                    float a = piece * (2f * Mathf.PI / CoinCount);
                    v = (side * Mathf.Cos(a) + away * Mathf.Sin(a)) * 2.4f + Vector3.up * (3.0f + (piece % 3) * 0.6f);
                    break;
                default:
                    v = Vector3.up * 1.0f;
                    break;
            }
            return v * t + Vector3.down * (0.5f * Gravity * t * t);
        }

        public static Quaternion PieceSpin(ArcheryBreakKind kind, int piece, float t)
        {
            float speed = kind == ArcheryBreakKind.Clang ? 720f : kind == ArcheryBreakKind.Coins ? 900f : 240f;
            float sign = piece % 2 == 0 ? 1f : -1f;
            return Quaternion.Euler(speed * t * 0.4f, 0f, sign * speed * t);
        }

        /// <summary>쏘면 안 되는 것이 t초에 빨간 재질인지. 맞는 순간부터 빨갛고, 원래 색과 번갈아 깜빡인다.</summary>
        public static bool ShameIsRed(float t)
            => t <= 0f || ((int)(t / ShameBlinkHalf)) % 2 == 0;

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-8f ? Vector3.back : v.normalized;
        }
    }
}
