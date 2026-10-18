using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>파티클 채널 — 채널마다 파티클 시스템 하나. 중력이 달라서 나눈다(<see cref="ArcheryHitFxRecipe.GravityOf"/>).</summary>
    public enum ArcheryFxChannel { Droplet = 0, Shard = 1, Spark = 2, Confetti = 3 }

    /// <summary>한 번에 뿜는 파티클 한 묶음. 방향은 고른 구면, 속도·수명·크기는 범위 안, 색은 팔레트에서 고르게(같은 색을 여러 번 넣어 비율을 만든다).</summary>
    public readonly struct ArcheryFxBurst
    {
        public readonly ArcheryFxChannel Channel;
        public readonly int Count;
        public readonly float SpeedMin, SpeedMax;
        public readonly float UpBoost;    // 위로 더하는 속도(m/s)
        public readonly bool UpOnly;      // 위쪽 반구로만(피냐타 폭포)
        public readonly float LifeMin, LifeMax;
        public readonly float SizeMin, SizeMax;
        public readonly Color32[] Palette;

        public ArcheryFxBurst(ArcheryFxChannel channel, int count, float speedMin, float speedMax, float upBoost,
                              float lifeMin, float lifeMax, float sizeMin, float sizeMax, Color32[] palette, bool upOnly = false)
        {
            Channel = channel; Count = count; SpeedMin = speedMin; SpeedMax = speedMax; UpBoost = upBoost; UpOnly = upOnly;
            LifeMin = lifeMin; LifeMax = lifeMax; SizeMin = sizeMin; SizeMax = sizeMax; Palette = palette;
        }

        public ArcheryFxBurst Slowed(float speed, float life)
            => new ArcheryFxBurst(Channel, Count, SpeedMin * speed, SpeedMax * speed, UpBoost * speed,
                                  LifeMin * life, LifeMax * life, SizeMin, SizeMax, Palette, UpOnly);
    }

    /// <summary>맞은 자리에서 커지며 사라지는 고리(카메라를 본다). 시작 크기는 표적 반지름의 배수, 커지는 폭은 m.</summary>
    public readonly struct ArcheryFxRing
    {
        public readonly float Delay, Seconds, StartScale, Grow;
        public readonly Color32 Color;

        public ArcheryFxRing(float delay, float seconds, float startScale, float grow, Color32 color)
        {
            Delay = delay; Seconds = seconds; StartScale = startScale; Grow = grow; Color = color;
        }

        public ArcheryFxRing Slowed(float life) => new ArcheryFxRing(Delay * life, Seconds * life, StartScale, Grow, Color);

        /// <summary>맞은 뒤 t초의 고리. 차례 전·끝난 뒤엔 거짓. 알파는 1에서 0으로 곧게 준다.</summary>
        public bool At(float t, float targetRadius, out float radius, out float alpha)
        {
            float k = Seconds > 0f ? (t - Delay) / Seconds : -1f;
            radius = 0f;
            alpha = 0f;
            if (k < 0f || k > 1f)
            {
                return false;
            }
            radius = StartScale * targetRadius + Grow * k;
            alpha = 1f - k;
            return true;
        }
    }

    /// <summary>폭죽: 맞은 자리에서 더 솟았다가(Fuse 동안 ClimbMps) 하늘에서 크게 터진다. 터지는 색은 Colors에서 하나.</summary>
    public readonly struct ArcheryFxFirework
    {
        public readonly float Fuse, ClimbMps;
        public readonly ArcheryFxBurst Sparks;   // 팔레트는 화면이 고른 한 색 넷 + 흰색 하나로 바꿔 쓴다
        public readonly float RingStart, RingGrow, RingSeconds;
        public readonly Color32 Trail;
        public readonly Color32[] Colors;

        public ArcheryFxFirework(float fuse, float climbMps, ArcheryFxBurst sparks, float ringStart, float ringGrow,
                                 float ringSeconds, Color32 trail, Color32[] colors)
        {
            Fuse = fuse; ClimbMps = climbMps; Sparks = sparks; RingStart = ringStart; RingGrow = ringGrow;
            RingSeconds = ringSeconds; Trail = trail; Colors = colors;
        }
    }

    /// <summary>
    /// 투석기 표적이 맞았을 때 무엇이 얼마나 터지나(2차 spec §4). 화면 없는 숫자라 시험할 수 있고, 시안(hitfx)의 `burst()` 숫자를 그대로 옮겼다.
    /// <b>멀티 규칙</b>: 게임 시계는 건드리지 않는다 — 시안의 전체 멈춤(0.06초)은 표적 그림의 번쩍임으로, 전체 슬로모션은 대박 파티클만 느리게로 바꿨다.
    /// 클·서가 맞출 필요는 없다(연출일 뿐).
    /// </summary>
    public sealed class ArcheryHitFxRecipe
    {
        public const float FlashSeconds = 0.06f;
        public const float FlashScale = 1.12f;
        public const float SlowSpeed = 0.6f;
        public const float SlowLife = 1.6f;
        public const float ShameSeconds = 1.2f;
        public const float SwingSeconds = 1.2f;
        //  남의 적중은 작고 옅게 — 8인 판에서 남의 불꽃에 내 조준이 묻히지 않게.
        public const float OthersScale = 0.6f;
        public const float OthersAlpha = 0.6f;

        public IReadOnlyList<ArcheryFxBurst> Bursts { get; private set; } = new ArcheryFxBurst[0];
        public IReadOnlyList<ArcheryFxRing> Rings { get; private set; } = new ArcheryFxRing[0];
        public ArcheryFxFirework? Firework { get; private set; }
        public bool Ding { get; private set; }
        public bool Swing { get; private set; }
        public bool Shame { get; private set; }
        public bool Slow { get; private set; }
        public float ShakeMeters { get; private set; } = 0.025f;
        public float ShakeSeconds { get; private set; } = 0.2f;

        public int ParticleCount
        {
            get
            {
                int n = Firework.HasValue ? Firework.Value.Sparks.Count : 0;
                foreach (var b in Bursts) { n += b.Count; }
                return n;
            }
        }

        /// <summary>보는 사람 기준 크기 배수 — 내 적중은 그대로, 남의 적중은 <see cref="OthersScale"/>.</summary>
        public static float ViewerScale(bool mine) => mine ? 1f : OthersScale;

        /// <summary>보는 사람 기준 진하기 배수 — 내 적중은 그대로, 남의 적중은 <see cref="OthersAlpha"/>.</summary>
        public static float ViewerAlpha(bool mine) => mine ? 1f : OthersAlpha;

        /// <summary>
        /// 맞은 뒤 t초의 표적 그림 크기 배수(번쩍임). 게임 시계를 멈추는 대신 표적만 부풀린다:
        /// 0초 1배에서 <see cref="FlashSeconds"/>까지 <see cref="FlashScale"/>로 곧게 커지고, 그 뒤엔 터지므로 1로 돌아간다.
        /// </summary>
        public static float FlashScaleAt(float t)
        {
            if (t <= 0f || t > FlashSeconds) { return 1f; }
            return 1f + (FlashScale - 1f) * (t / FlashSeconds);
        }

        public static float GravityOf(ArcheryFxChannel channel)
            => channel == ArcheryFxChannel.Spark ? 0.25f : channel == ArcheryFxChannel.Confetti ? 0.55f : 1f;

        private static readonly Dictionary<string, ArcheryHitFxRecipe> ByCode = Build();
        private static readonly ArcheryHitFxRecipe Fallback = new ArcheryHitFxRecipe
        {
            Bursts = new[] { new ArcheryFxBurst(ArcheryFxChannel.Droplet, 20, 3f, 7f, 1.5f, 0.5f, 0.9f, 0.08f, 0.16f, P("#FFF3B0", "#FFFFFF")) },
        };

        public static ArcheryHitFxRecipe For(string code)
            => code != null && ByCode.TryGetValue(code, out var r) ? r : Fallback;

        private static Dictionary<string, ArcheryHitFxRecipe> Build()
        {
            var d = new Dictionary<string, ArcheryHitFxRecipe>
            {
                ["WaterBalloon"] = new ArcheryHitFxRecipe
                {
                    Bursts = new[]
                    {
                        new ArcheryFxBurst(ArcheryFxChannel.Droplet, 46, 4f, 9f, 2f, 0.5f, 0.9f, 0.08f, 0.18f, P("#5EC8FF", "#C9EFFF")),
                        new ArcheryFxBurst(ArcheryFxChannel.Shard, 8, 1f, 3f, 2f, 0.5f, 0.5f, 0.25f, 0.25f, P("#2F8FD6")),
                    },
                    Rings = new[] { new ArcheryFxRing(0f, 0.35f, 1f, 5f, new Color32(160, 225, 255, 255)) },
                },
                ["Watermelon"] = new ArcheryHitFxRecipe
                {
                    Bursts = new[]
                    {
                        new ArcheryFxBurst(ArcheryFxChannel.Droplet, 40, 3f, 8f, 2f, 0.6f, 1f, 0.07f, 0.16f, P("#FF3B4E", "#FF3B4E", "#FF3B4E", "#2A1A1A")),
                        new ArcheryFxBurst(ArcheryFxChannel.Shard, 9, 3f, 6f, 3f, 1.1f, 1.1f, 0.25f, 0.4f, P("#2F9E44", "#FF5468")),
                    },
                },
                ["Plate"] = new ArcheryHitFxRecipe
                {
                    Bursts = new[]
                    {
                        new ArcheryFxBurst(ArcheryFxChannel.Shard, 22, 4f, 9f, 1.5f, 0.7f, 1.1f, 0.12f, 0.3f,
                                           P("#F4F1EA", "#F4F1EA", "#F4F1EA", "#F4F1EA", "#F4F1EA", "#F4F1EA", "#F4F1EA", "#3B6FD8", "#3B6FD8", "#3B6FD8")),
                        new ArcheryFxBurst(ArcheryFxChannel.Spark, 20, 6f, 12f, 0f, 0.35f, 0.35f, 0.06f, 0.06f, P("#FFFFFF")),
                    },
                },
                ["Rocket"] = new ArcheryHitFxRecipe
                {
                    Firework = new ArcheryFxFirework(0.35f, 9f,
                        new ArcheryFxBurst(ArcheryFxChannel.Spark, 90, 8f, 13f, 0f, 1f, 1.5f, 0.12f, 0.22f, P("#FFFFFF")),
                        ringStart: 1f, ringGrow: 14f, ringSeconds: 0.5f, trail: C("#FFCF7A"),
                        colors: P("#FF5D5D", "#FFD23F", "#5EE1FF", "#B97BFF", "#7DFF9A")),
                },
                ["GoldenBell"] = Jackpot(new ArcheryHitFxRecipe
                {
                    Bursts = new[] { new ArcheryFxBurst(ArcheryFxChannel.Spark, 30, 2f, 6f, 0f, 0.6f, 1.2f, 0.05f, 0.12f, P("#FFE27A")) },
                    Rings = new[]
                    {
                        new ArcheryFxRing(0.00f, 0.7f, 0.8f, 7f, new Color32(255, 214, 90, 255)),
                        new ArcheryFxRing(0.12f, 0.7f, 0.8f, 10f, new Color32(255, 214, 90, 255)),
                        new ArcheryFxRing(0.24f, 0.7f, 0.8f, 13f, new Color32(255, 214, 90, 255)),
                        new ArcheryFxRing(0.36f, 0.7f, 0.8f, 16f, new Color32(255, 214, 90, 255)),
                    },
                    Ding = true,
                    Swing = true,
                }),
                ["Pinata"] = Jackpot(new ArcheryHitFxRecipe
                {
                    Bursts = new[]
                    {
                        new ArcheryFxBurst(ArcheryFxChannel.Confetti, 70, 3f, 9f, 3f, 1.2f, 2f, 0.1f, 0.2f,
                                           P("#FF4D8D", "#FFD23F", "#44D17A", "#4AA8FF", "#B97BFF", "#FF8A3D"), upOnly: true),
                        //  사탕: 색종이보다 무겁게(중력 1) 떨어지는 밝은 덩어리. 피냐타는 "사탕·색종이 폭포"다.
                        new ArcheryFxBurst(ArcheryFxChannel.Shard, 12, 2f, 6f, 3f, 1f, 1.6f, 0.14f, 0.24f,
                                           P("#FF4D8D", "#FFD23F", "#44D17A", "#4AA8FF", "#FFFFFF"), upOnly: true),
                    },
                }),
            };
            d["BirdNest"] = Forbidden("#C9A46A");
            d["CheerFlag"] = Forbidden("#5E7BFF");
            d["KidBalloon"] = Forbidden("#FFD23F");
            return d;
        }

        //  대박: 파티클·고리만 느리게(게임 시계는 그대로), 흔들림은 조금 더 크게.
        private static ArcheryHitFxRecipe Jackpot(ArcheryHitFxRecipe r)
        {
            var bursts = new ArcheryFxBurst[r.Bursts.Count];
            for (int i = 0; i < bursts.Length; i++) { bursts[i] = r.Bursts[i].Slowed(SlowSpeed, SlowLife); }
            var rings = new ArcheryFxRing[r.Rings.Count];
            for (int i = 0; i < rings.Length; i++) { rings[i] = r.Rings[i].Slowed(SlowLife); }
            r.Bursts = bursts;
            r.Rings = rings;
            r.Slow = true;
            r.ShakeMeters = 0.04f;
            r.ShakeSeconds = 0.3f;
            return r;
        }

        private static ArcheryHitFxRecipe Forbidden(string debris) => new ArcheryHitFxRecipe
        {
            //  둥지 지푸라기·깃발 천·풍선 고무 — 물이 아니라 조각이 흩어진다.
            Bursts = new[] { new ArcheryFxBurst(ArcheryFxChannel.Shard, 16, 2f, 5f, 1f, 0.7f, 0.7f, 0.08f, 0.16f, P(debris)) },
            Rings = new[] { new ArcheryFxRing(0f, 0.4f, 1f, 3f, new Color32(255, 60, 60, 255)) },
            Shame = true,
        };

        /// <summary>쏘면 안 되는 것이 맞은 뒤 t초의 자리 차이 — 살짝 튀었다가 떨어진다(1차 Shame과 같다).</summary>
        public static Vector3 ShameOffset(float t)
            => t <= 0f ? Vector3.zero : Vector3.up * (1f * t - 0.5f * 9.81f * t * t);

        /// <summary>맞는 순간부터 빨갛고, 0.15초마다 원래 색과 번갈아 깜빡인다(1차 그대로).</summary>
        public static bool ShameIsRed(float t) => t <= 0f || ((int)(t / 0.15f)) % 2 == 0;

        /// <summary>맞은 종이 흔들리는 각(도) — 35°에서 줄어들어 1.2초에 멈춘다.</summary>
        public static float SwingDegrees(float t)
        {
            if (t <= 0f || t >= SwingSeconds) { return 0f; }
            return 35f * (1f - t / SwingSeconds) * Mathf.Sin(t * 14f);
        }

        private static Color32 C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        private static Color32[] P(params string[] hex)
        {
            var p = new Color32[hex.Length];
            for (int i = 0; i < hex.Length; i++) { p[i] = C(hex[i]); }
            return p;
        }
    }

    /// <summary>
    /// 내 적중의 화면 흔들림. <b>내가 맞혔고 당기지 않을 때만</b>(흔들리는 동안 매 프레임 다시 묻는다 — 도중에 당기기 시작하면 바로 멈춘다)(spec §4 — 남의 적중은 터지는 연출만, 확대 중엔 조준을 흔들지 않는다).
    /// 한 발 승부 10점(<see cref="ArcheryBullseyeFx"/>, 0.06 m)보다 작다.
    /// </summary>
    public static class ArcheryHitShake
    {
        public static bool Allowed(bool mine, bool drawing) => mine && drawing == false;

        public static Vector3 Offset(float elapsed, float meters, float seconds)
        {
            if (elapsed < 0f || elapsed >= seconds || seconds <= 0f)
            {
                return Vector3.zero;
            }
            float decay = 1f - elapsed / seconds;
            return new Vector3(Mathf.Sin(elapsed * 97f + 0.4f), Mathf.Sin(elapsed * 131f + 1.3f), 0f) * (meters * decay);
        }
    }
}
