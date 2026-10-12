using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 놓쳐서 바닥 구름에 닿았을 때의 번개 — 규칙은 서버의 다시 시작(순간이동) 그대로이고, 이건 그 순간을 가리는 그림의 규칙이다.
    /// </summary>
    public static class SkydiveLightning
    {
        public const float NearFloor = 40f;
        public const float Cooldown = 1.5f;
        public const float BoltSeconds = 0.45f;
        public const float FlashHold = 0.15f;
        public const float FlashFade = 0.45f;
        public const float FlashExposure = 1.4f;   // 출구 번쩍과 같은 세기 — 더 세면 화면이 날아간다
        public const float FlashBloom = 3.5f;
        public const float ShakeSeconds = 0.6f;
        public const float ShakeAmplitude = 0.25f;
        public const float BoltHeight = 160f;
        public const float BoltJitter = 6f;
        public const int BoltSegments = 9;

        /// <summary>화면은 "다시 시작이었다"를 따로 받지 못한다 — 위로 크게 튀었고 직전 자리가 바닥 구름 근처면 번개로 본다.</summary>
        public static bool Struck(float previousY, float y, IReadOnlyList<RetryZone> zones)
        {
            if (zones == null || SkydiveLookRules.Teleported(previousY, y) == false)
            {
                return false;
            }
            for (int i = 0; i < zones.Count; i++)
            {
                if (previousY < zones[i].BelowY + NearFloor)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 번쩍을 분위기에 얹는다 — 더하지 않고 밝은 쪽을 쓴다. 다시 시작 자리가 출구 바로 위라 출구 번쩍과 겹치기 때문.
        /// </summary>
        public static SkydiveMoodKey WithFlash(SkydiveMoodKey m, float flash)
        {
            m.Exposure = Mathf.Max(m.Exposure, Mathf.Lerp(m.Exposure, FlashExposure, flash));
            m.Bloom = Mathf.Max(m.Bloom, Mathf.Lerp(m.Bloom, FlashBloom, flash));
            return m;
        }

        /// <summary>내 화면 하얀 번쩍 — 잠깐 꽉 찼다가 걷힌다. 0..1.</summary>
        public static float FlashBoost(float elapsed)
        {
            if (elapsed < 0f) return 0f;
            if (elapsed <= FlashHold) return 1f;
            return Mathf.Clamp01(1f - (elapsed - FlashHold) / FlashFade);
        }

        /// <summary>번개 기둥 밝기 — 번쩍, 한 번 죽었다 다시 번쩍, 그리고 사라진다.</summary>
        public static float BoltAlpha(float elapsed)
        {
            if (elapsed < 0f || elapsed >= BoltSeconds) return 0f;
            if (elapsed < 0.08f) return 1f;
            if (elapsed < 0.14f) return 0.25f;
            if (elapsed < 0.22f) return 1f;
            return 1f - (elapsed - 0.22f) / (BoltSeconds - 0.22f);
        }

        /// <summary>감전된 몸의 지지직 떨림 — 초당 30번 무작위로 튀고, 점점 잦아든다.</summary>
        public static Vector3 ShakeOffset(float elapsed, int seed)
        {
            if (elapsed < 0f || elapsed >= ShakeSeconds) return Vector3.zero;
            int step = Mathf.FloorToInt(elapsed * 30f);
            var rng = new System.Random(seed * 7919 + step);
            var dir = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f);
            return dir * (ShakeAmplitude * (1f - elapsed / ShakeSeconds));
        }

        /// <summary>하늘에서 땅(직전 자리)까지 꺾여 내려오는 점들. 양 끝은 곧고 가운데만 옆으로 흔들린다.</summary>
        public static Vector3[] BoltPoints(Vector3 ground, int seed)
        {
            var rng = new System.Random(seed);
            var points = new Vector3[BoltSegments + 1];
            for (int i = 0; i <= BoltSegments; i++)
            {
                float t = (float)i / BoltSegments;
                var p = ground + Vector3.up * (BoltHeight * (1f - t));
                if (i > 0 && i < BoltSegments)
                {
                    p.x += ((float)rng.NextDouble() * 2f - 1f) * BoltJitter;
                    p.z += ((float)rng.NextDouble() * 2f - 1f) * BoltJitter;
                }
                points[i] = p;
            }
            return points;
        }
    }

    /// <summary>엔티티마다 직전 높이를 기억해 번개를 한 번씩만 알린다(보간이 순간이동을 여러 프레임에 나눠도).</summary>
    public class SkydiveStrikeDetector
    {
        private struct Seen
        {
            public float Y;
            public bool Finished;
            public float StruckAt;
        }

        private readonly Dictionary<string, Seen> seen = new Dictionary<string, Seen>();

        public bool Observe(string id, float y, bool finished, float now, IReadOnlyList<RetryZone> zones)
        {
            bool struck = false;
            float struckAt = float.NegativeInfinity;
            if (seen.TryGetValue(id, out var last))
            {
                struckAt = last.StruckAt;
                //  잡은 사람은 구름 위에 내려앉는다 — 그다음 위로 옮겨지는 건 다시 시작이 아니다
                struck = last.Finished == false
                         && now - last.StruckAt >= SkydiveLightning.Cooldown
                         && SkydiveLightning.Struck(last.Y, y, zones);
            }
            if (struck)
            {
                struckAt = now;
            }
            seen[id] = new Seen { Y = y, Finished = finished, StruckAt = struckAt };
            return struck;
        }

        public void Clear() => seen.Clear();
    }
}
