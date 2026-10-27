using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 연속 날갯짓 연출 "집중형"의 모양(spec §7, 사용자 결정 10-26). 시제품 <c>flap-plus.html</c>의 집중형 값을 그대로 옮겼다.
    /// <para>길이는 화면 패널 px. 단위 u = 시제품 무대(높이 545px)의 1px — 화면 높이에 비례해 같은 비율로 보인다.
    /// 새 둘레 간격만 새 반지름(rPx)으로 잰다 — 게임의 새는 시제품보다 화면에서 작아, 시제품 px 그대로면 몸을 덮는다.</para>
    /// <para>모두 새 뒤·위에만 붙는다 — 앞길(오른쪽)과 화면 가운데를 비운다. 날갯짓 소리는 원래 소리 그대로(올라가는 음 없음).</para>
    /// </summary>
    public static class FlapComboLook
    {
        /// <summary>시제품 무대 높이(px). 16:9, 가로 약 968px.</summary>
        public const float StageHeight = 545f;

        public const float NumberLife = 0.45f;
        /// <summary>"+N" 글자 크기(u) — 시제품 <c>max(18, H×0.055)</c>의 1.3배(사용자 피드백 10-27: 더 묵직하게).</summary>
        public const float NumberFont = 39f;
        /// <summary>사는 동안 떠오르는 높이(u) — 시제품 <c>H×0.04</c>.</summary>
        public const float NumberRise = 21.8f;

        /// <summary>테두리 두께 = 글자 × 이 값. 한마디(<see cref="WordOutlineRatio"/>)보다 두껍게(10-27).</summary>
        public const float NumberOutlineRatio = 0.16f;
        /// <summary>고비 한마디 테두리 두께 = 글자 × 이 값 — 시제품 테두리 lineWidth(글자×0.2, 바깥 반만 보임)의 반.</summary>
        public const float WordOutlineRatio = 0.1f;
        /// <summary>숫자 아래 그림자 — 글자 크기의 이 비율만큼 아래로(10-27).</summary>
        public const float NumberShadowOffsetRatio = 0.06f;
        /// <summary>숫자 아래 그림자 — 글자 크기의 이 비율만큼 부드럽게 퍼진다(10-27).</summary>
        public const float NumberShadowBlurRatio = 0.05f;

        public const float WordLife = 1.1f;
        /// <summary>고비 한마디 글자 크기(u) — 시제품 <c>max(16, H×0.045)</c>.</summary>
        public const float WordFont = 24.5f;
        public const float WordRise = 20f;
        /// <summary>한마디 판 바닥이 숫자 판 바닥보다 높은 만큼(u) — 시제품 y −52 대 −24.</summary>
        public const float WordAbove = 28f;

        public const float RingLife = 0.6f;

        public const float FeatherLifeMin = 0.5f, FeatherLifeMax = 0.9f;
        /// <summary>깃털 가로·세로 속력 폭, 위로 튐, 중력, 뒤로 밀림(u/s, u/s²). 밀림은 시제품 <c>W×0.2</c>(W ≈ 968).</summary>
        public const float FeatherSpeedXMin = 60f, FeatherSpeedXMax = 180f, FeatherSpeedYMin = 40f, FeatherSpeedYMax = 120f;
        public const float FeatherLift = 30f, FeatherGravity = 260f, FeatherDrift = 194f;

        public const int TrailFrom = 25;

        public static readonly Color Outline = Hex(0x2a1a0c);
        public static readonly Color FeatherColor = Hex(0xf5e9d6);
        /// <summary>숫자 그림자 색 — 테두리와 같은 어두운 색, 60% 불투명(10-27).</summary>
        public static readonly Color NumberShadow = Hex(0x2a1a0c, 0.6f);

        private static readonly Color Tier0 = Hex(0xfff6e6), Tier1 = Hex(0xffd257), Tier2 = Hex(0xff9a3c), Tier3 = Hex(0xff6a3d);

        private static Color Hex(int rgb, float a = 1f) => new Color((rgb >> 16 & 0xFF) / 255f, (rgb >> 8 & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public static float Unit(float panelHeight) => panelHeight / StageHeight;

        public static Color TierColor(int n) => n >= 50 ? Tier3 : n >= 25 ? Tier2 : n >= 10 ? Tier1 : Tier0;

        public static string Word(int n)
        {
            switch (n)
            {
                case 10: return "좋아!";
                case 25: return "멋져!";
                case 50: return "미쳤다!";
                case 100: return "전설!";
                default: return null;
            }
        }

        public static bool IsMilestone(int n) => Word(n) != null;

        public static string Text(int n) => "+" + n.ToString(CultureInfo.InvariantCulture);

        /// <summary>연속이 길수록 조금 크게 — 60에서 멈춘다.</summary>
        public static float NumberSize(int n) => 0.62f + Mathf.Min(n, 60) / 300f;

        /// <summary>k = 나이/수명. 0.6 → 1.3(15%) → 1.0(30%) — 10-27 피드백으로 더 세게 튄다.</summary>
        public static float NumberPop(float k)
        {
            if (k < 0.15f)
            {
                return 0.6f + k / 0.15f * 0.7f;
            }
            if (k < 0.3f)
            {
                return 1.3f - (k - 0.15f) / 0.15f * 0.3f;
            }
            return 1f;
        }

        /// <summary>반까지 또렷, 그 뒤 사라진다.</summary>
        public static float NumberAlpha(float k) => k < 0.5f ? 1f : Mathf.Clamp01(1f - (k - 0.5f) / 0.5f);

        /// <summary>1.8배에서 12%에 1배.</summary>
        public static float WordPop(float k) => k < 0.12f ? 1.8f - k / 0.12f * 0.8f : 1f;

        public static float WordAlpha(float k) => k < 0.7f ? 1f : Mathf.Clamp01(1f - (k - 0.7f) / 0.3f);

        /// <summary>
        /// "+N" 판의 오른쪽 아래 모서리. 몸 가운데보다 반지름 반만큼 뒤, 몸 꼭대기보다 2u 위, 거기서 몸 지름(2r)만큼 더 위
        /// (10-27 피드백: 더 높게) — 판이 왼쪽 위로 자라서 앞길(오른쪽)과 몸을 가리지 않는다. 노란 화살표(몸 바로 위 가운데)와도 비켜 선다.
        /// </summary>
        public static Vector2 NumberCorner(Vector2 birdPx, float rPx, float u)
            => new Vector2(birdPx.x - 0.5f * rPx, birdPx.y - 3f * rPx - 2f * u);

        /// <summary>고비 한마디 판의 오른쪽 아래 모서리 — 숫자 위.</summary>
        public static Vector2 WordCorner(Vector2 birdPx, float rPx, float u)
            => NumberCorner(birdPx, rPx, u) - new Vector2(0f, WordAbove * u);

        /// <summary>시제품 고리는 몸 안(0.83r)에서 시작했다 — 화면 위 그림은 새를 덮으니 몸 밖 4u에서 시작해 36u 퍼진다.</summary>
        public static float RingRadius(float k, float rPx, float u) => rPx + 4f * u + 36f * u * k;

        public static float RingWidth(float k, float u) => (4f * (1f - k) + 1f) * u;

        public static int FeatherCount(int n) => 2 + Mathf.Min(2, n / 25);

        /// <summary>꼬리 점이 남는 시간(초) — 길이. 80에서 멈춘다.</summary>
        public static float TrailLife(int n) => 0.15f + Mathf.Min(n, 80) / 160f;

        /// <summary>a = 1 − 나이/수명(머리 쪽 1).</summary>
        public static float TrailWidth(int n, float a, float u) => (2f + Mathf.Min(n, 80) / 30f * a) * u;

        public static Color TrailColor(int n, float a)
        {
            Color c = TierColor(n);
            c.a = a * (n >= 50 ? 0.8f : n >= 25 ? 0.7f : 0.6f);
            return c;
        }
    }

    /// <summary>
    /// 연속 날갯짓 연출의 흐름(spec §7) — 무엇이 어디서 얼마나 살아 있나. 그리는 쪽(<see cref="FlapComboView"/>)은 이것만 읽는다.
    /// <para>날갯짓은 먼저 <see cref="Queue"/>로 받아 두고, 그 장의 그려진 새 자리를 아는 때(<see cref="Resolve"/>)에 낸다 —
    /// 누른 순간은 이번 장 보간 전이라 옛 자리다. 숫자·고리는 낸 자리에 머물고(새를 따라 옮겨지지 않는다), 한마디만 새를 따라간다.</para>
    /// </summary>
    public sealed class FlapComboFx
    {
        public const int MaxNumbers = 8;
        public const int MaxFeathers = 24;

        public struct Number
        {
            public Vector2 Corner;
            public int Count;
            public float Age;
        }

        public struct Feather
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public bool Spark;
            public Color Color;
        }

        public struct Ring
        {
            public Vector2 Center;
            public float Age;
            public Color Color;
        }

        public struct TrailPoint
        {
            public Vector3 World;
            public float Age;
        }

        private readonly System.Random _rng;
        private readonly List<int> _pending = new List<int>();
        private float _wordAge = float.PositiveInfinity;

        public readonly List<Number> Numbers = new List<Number>();
        public readonly List<Feather> Feathers = new List<Feather>();
        public readonly List<Ring> Rings = new List<Ring>();
        public readonly List<TrailPoint> Trail = new List<TrailPoint>();

        public FlapComboFx(System.Random rng)
        {
            _rng = rng;
        }

        /// <summary>지금 연속 수(꼬리 색·길이).</summary>
        public int Count { get; private set; }

        public string WordText { get; private set; }
        public Color WordColor { get; private set; }
        public float WordAge => _wordAge;
        public bool WordVisible => _wordAge < FlapComboLook.WordLife;

        /// <summary>먹힌 날갯짓 하나(연속 수 n).</summary>
        public void Queue(int count)
        {
            _pending.Add(count);
            Count = count;
        }

        /// <summary>그 장에 새가 안 그려졌다 — 밀린 날갯짓은 낼 자리가 없다.</summary>
        public void DropPending() => _pending.Clear();

        /// <summary>그 장에 그려진 새 자리(패널 px)로 밀린 날갯짓을 낸다.</summary>
        public void Resolve(Vector2 birdPx, float rPx, float u)
        {
            foreach (int n in _pending)
            {
                Spawn(n, birdPx, rPx, u);
            }
            _pending.Clear();
        }

        private void Spawn(int n, Vector2 birdPx, float rPx, float u)
        {
            if (Numbers.Count >= MaxNumbers)
            {
                Numbers.RemoveAt(0);
            }
            Numbers.Add(new Number { Corner = FlapComboLook.NumberCorner(birdPx, rPx, u), Count = n });

            //  깃털은 몸 뒤 끝에서 뒤쪽 ±27° 부채꼴로 — 시제품 집중형 π(0.85~1.15).
            int k = FlapComboLook.FeatherCount(n);
            for (int i = 0; i < k; i++)
            {
                if (Feathers.Count >= MaxFeathers)
                {
                    Feathers.RemoveAt(0);
                }
                float a = Mathf.PI * (0.85f + Next() * 0.3f);
                float sx = FlapComboLook.FeatherSpeedXMin + Next() * (FlapComboLook.FeatherSpeedXMax - FlapComboLook.FeatherSpeedXMin);
                float sy = FlapComboLook.FeatherSpeedYMin + Next() * (FlapComboLook.FeatherSpeedYMax - FlapComboLook.FeatherSpeedYMin);
                bool spark = i % 3 == 0 && n >= 10;
                Feathers.Add(new Feather
                {
                    Position = birdPx - new Vector2(rPx, 0f),
                    //  패널 y는 아래로 크다 — 시제품 캔버스와 같은 방향이라 부호가 그대로다.
                    Velocity = new Vector2(Mathf.Cos(a) * sx, Mathf.Sin(a) * sy - FlapComboLook.FeatherLift) * u,
                    Life = FlapComboLook.FeatherLifeMin + Next() * (FlapComboLook.FeatherLifeMax - FlapComboLook.FeatherLifeMin),
                    Spark = spark,
                    Color = spark ? FlapComboLook.TierColor(n) : FlapComboLook.FeatherColor,
                });
            }

            if (FlapComboLook.IsMilestone(n))
            {
                WordText = FlapComboLook.Word(n);
                WordColor = FlapComboLook.TierColor(n);
                _wordAge = 0f;
                Rings.Add(new Ring { Center = birdPx, Color = FlapComboLook.TierColor(n) });
            }
        }

        private float Next() => (float)_rng.NextDouble();

        public void Advance(float dt, float u)
        {
            for (int i = Numbers.Count - 1; i >= 0; i--)
            {
                var p = Numbers[i];
                p.Age += dt;
                if (p.Age >= FlapComboLook.NumberLife) { Numbers.RemoveAt(i); } else { Numbers[i] = p; }
            }
            for (int i = Feathers.Count - 1; i >= 0; i--)
            {
                var p = Feathers[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    Feathers.RemoveAt(i);
                    continue;
                }
                p.Position += p.Velocity * dt + new Vector2(-FlapComboLook.FeatherDrift * u * dt, 0f);
                p.Velocity.y += FlapComboLook.FeatherGravity * u * dt;
                Feathers[i] = p;
            }
            for (int i = Rings.Count - 1; i >= 0; i--)
            {
                var p = Rings[i];
                p.Age += dt;
                if (p.Age >= FlapComboLook.RingLife) { Rings.RemoveAt(i); } else { Rings[i] = p; }
            }
            float trailLife = FlapComboLook.TrailLife(Count);
            for (int i = Trail.Count - 1; i >= 0; i--)
            {
                var p = Trail[i];
                p.Age += dt;
                if (p.Age >= trailLife) { Trail.RemoveAt(i); } else { Trail[i] = p; }
            }
            _wordAge += dt;
        }

        /// <summary>그 장의 새 가운데(월드). 25부터 달리는 동안만 꼬리 점을 더한다 — 결승 뒤엔 남은 점이 제 수명대로 사라진다.</summary>
        public void RecordTrail(Vector3 world, bool racing)
        {
            if (racing && Count >= FlapComboLook.TrailFrom)
            {
                Trail.Add(new TrailPoint { World = world });
            }
        }

        /// <summary>내 새가 기절에 들어갔다 — 연속 0, 꼬리는 끈다. 떠 있는 숫자·깃털·한마디는 제 수명대로 조용히 사라진다.</summary>
        public void Crash()
        {
            Count = 0;
            _pending.Clear();
            Trail.Clear();
        }
    }
}
