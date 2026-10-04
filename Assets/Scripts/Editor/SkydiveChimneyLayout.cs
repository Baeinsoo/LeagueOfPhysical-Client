using System.Collections.Generic;
using UnityEngine;
using static LOP.EditorTools.SkydiveCourseBuilder;

namespace LOP.EditorTools
{
    /// <summary>
    /// 굴뚝 시제품 표 — 젤다 굴뚝처럼 좁은 굴뚝에 레이저 그물을 촘촘히 쌓아 자세 선택을 강요한다.
    /// 그물마다 구멍(창)이 하나고, "계단"에선 10m마다 창이 9m씩 옮겨 간다 — 1m 낙하당 0.9m라
    /// 대자(0.2)로는 못 따라가고 패러세일(2.3)로만 된다. 패러세일은 스태미나 15초라 어디서 펼지가 고민이 된다.
    /// </summary>
    internal static class SkydiveChimneyLayout
    {
        public const float Half = 20f;              // 굴뚝 안쪽 반폭
        public const float Wall = 4f;
        public const float SpawnY = 600f;
        public const float LedgeY = 320f;           // 중간 턱 — 내려서 스태미나를 채운다
        public const float ExitY = 60f;             // 굴뚝 바닥(그 아래는 트인 착지 마당)
        public const float GroundHalf = 100f;
        public const float WindowHalf = 4f;         // 몸 중심이 지나갈 수 있는 창(반폭)
        public const float BeamRadius = 0.15f;      // 젤다식 가는 레이저(빛 번짐 굵기 = 판정 굵기)
        public const float BeamSpacing = 1.0f;      // < 2·(빔 0.15 + 몸 0.4) = 1.1 — 빔 사이로 못 빠진다. 보이는 틈 0.7 < 몸 0.8
        private const float BodyRadius = 0.4f;      // #SkydiveConfig body_radius

        //  #SkydiveConfig에서 옮겨 적음(SkydiveCourseBuilder의 대자·다이브 상수와 같은 방식).
        private const float SpreadFall = 60f, SpreadMove = 12f, SpreadTurn = 22f;
        private const float GlideFall = 6f, GlideMove = 14f, GlideTurn = 18f;
        private const float FallBrake = 150f, StaminaMax = 300f, GlideDrain = 20f;

        public const float GlideBudgetSeconds = StaminaMax / GlideDrain;
        private const float BrakeSeconds = (SpreadFall - GlideFall) / FallBrake;   // 펴서 낙하가 꺾이는 동안
        private const float LeadSeconds = 1f;                                       // 첫 그물보다 조금 일찍 펴는 여유
        private const float GroundApproachSeconds = 2f;                             // 바닥 앞에서 다시 펴 내려앉기

        public readonly struct Net
        {
            public readonly float Y, X, Z;
            public Net(float y, float x, float z) { Y = y; X = x; Z = z; }
        }

        public enum StepKind { Fall, Glide }

        public readonly struct Step
        {
            public readonly float FromY, ToY, Distance;
            public readonly StepKind Kind;
            public float Drop => FromY - ToY;
            public Step(float fromY, float toY, float distance, StepKind kind) { FromY = fromY; ToY = toY; Distance = distance; Kind = kind; }
        }

        /// <summary>스폰 판의 뚫린 자리(남쪽 큰 구멍). 스폰은 북쪽 줄.</summary>
        public static Hole SpawnOpening => new Hole(0f, -6f, 28f, hasDoor: false);

        /// <summary>턱의 구멍 — 마지막 계단 창에서 패러세일로도 곧장 못 가는 구석. 내려서 걸어가야 한다.</summary>
        public static Hole LedgeHole => new Hole(-15f, 15f, 8f, hasDoor: false);

        //  위에서 아래로. 주석: 낙하 = 대자로 가는 칸, 계단 = 패러세일로만 가는 칸.
        public static readonly Net[] Nets =
        {
            new Net(560f, 0f, -6f),     // 낙하(익히기)
            new Net(520f, -2f, -4f),
            new Net(470f, -2f, -4f),    // 계단 1 — 이 위에서 미리 펴야 한다
            new Net(460f, 7f, -4f),
            new Net(450f, 7f, 5f),
            new Net(400f, 5f, 3f),      // 낙하
            new Net(360f, 5f, 3f),      // 계단 2
            new Net(350f, -4f, 3f),
            new Net(340f, -4f, -6f),
            new Net(330f, 5f, -6f),
            //  턱 320
            new Net(280f, -12f, 12f),   // 낙하
            new Net(240f, -12f, 12f),   // 계단 3
            new Net(230f, -3f, 12f),
            new Net(220f, -3f, 3f),
            new Net(180f, -1f, 1f),     // 낙하
            new Net(140f, -1f, 1f),     // 계단 4
            new Net(130f, -10f, 1f),
            new Net(120f, -10f, 10f),
            new Net(110f, -1f, 10f),
        };

        public static readonly IReadOnlyDictionary<float, Vector3> RespawnPoints = new Dictionary<float, Vector3>
        {
            { SpawnY, new Vector3(0f, SpawnY, 14f) },
            { LedgeY, new Vector3(10f, LedgeY, -10f) },
        };

        public static float SpreadReach(float drop) => SkydiveWindReach.SelfReach(SpreadMove, SpreadTurn, drop, SpreadFall);
        public static float GlideReach(float drop) => SkydiveWindReach.SelfReach(GlideMove, GlideTurn, drop, GlideFall);

        /// <summary>
        /// 그물 하나 = 두 겹 빗살(위 x 방향, 1m 아래 z 방향). 빔은 창 가장자리에서 바깥으로 벽까지 깐다 —
        /// 창 바로 옆 빔이 창 경계에 딱 붙어야 열린 폭이 창과 같다(듬성듬성 깔면 창이 넓어진다).
        /// </summary>
        public static LaserSpec[] NetLasers(Net net)
        {
            var list = new List<LaserSpec>();
            foreach (float z in Rows(net.Z))
            {
                list.Add(new LaserSpec($"Net_{net.Y:0}_X{z:0.0}", new Vector3(-Half, net.Y, z), Half * 2f, BeamRadius, 0f, 0f, 0f, 0, 0, 0));
            }
            foreach (float x in Rows(net.X))
            {
                list.Add(new LaserSpec($"Net_{net.Y:0}_Z{x:0.0}", new Vector3(x, net.Y - 1f, -Half), Half * 2f, BeamRadius, 90f, 0f, 0f, 0, 0, 0));
            }
            return list.ToArray();
        }

        private static IEnumerable<float> Rows(float center)
        {
            float first = WindowHalf + BeamRadius + BodyRadius;
            float reachWall = Half - BodyRadius + BeamRadius + BodyRadius;   // 몸이 벽에 붙어도 덮이게
            for (float d = first; d - (BeamRadius + BodyRadius) < Half + Mathf.Abs(center); d += BeamSpacing)
            {
                if (center + d <= reachWall) { yield return center + d; }
                if (center - d >= -reachWall) { yield return center - d; }
            }
        }

        public static LaserSpec[] Lasers()
        {
            var all = new List<LaserSpec>();
            foreach (var n in Nets) { all.AddRange(NetLasers(n)); }
            return all.ToArray();
        }

        /// <summary>창에서 다음 창까지. 턱에서 끊긴다(턱 위 마지막 그물 → 턱 착지는 칸이 아니다 — 어디든 내려앉으면 된다).</summary>
        public static Step[] Transitions()
        {
            var steps = new List<Step>();
            var from = (y: SpawnY, x: SpawnOpening.X, z: SpawnOpening.Z);
            foreach (var n in Nets)
            {
                if (from.y > LedgeY && n.Y < LedgeY)
                {
                    from = (LedgeY, LedgeHole.X, LedgeHole.Z);
                }
                float d = new Vector2(n.X - from.x, n.Z - from.z).magnitude;
                float drop = from.y - n.Y;
                //  가장 가까운 가장자리끼리도 대자로 못 닿으면 계단이다.
                var kind = SpreadReach(drop) < d - WindowHalf * 2f ? StepKind.Glide : StepKind.Fall;
                steps.Add(new Step(from.y, n.Y, d, kind));
                from = (n.Y, n.X, n.Z);
            }
            return steps.ToArray();
        }

        /// <summary>
        /// 구간(스폰→턱, 턱→바닥)마다 꼭 펴야 하는 시간. 계단마다 = 꺾이는 시간 + 일찍 펴는 여유 + 계단 높이 ÷ 패러세일 낙하.
        /// 턱·바닥에는 펴고 내려앉아야 한다(착지 치사 속도 15).
        /// </summary>
        public static (float fromY, float seconds)[] GlideSecondsPerSection()
        {
            var result = new List<(float, float)>();
            float sectionStart = SpawnY, seconds = 0f, lastNetY = SpawnY;
            bool inStair = false;
            foreach (var s in Transitions())
            {
                if (s.FromY == LedgeY && sectionStart == SpawnY)
                {
                    seconds += (lastNetY - LedgeY) / GlideFall;   // 마지막 계단에서 편 채로 턱에 내려앉는다
                    result.Add((sectionStart, seconds));
                    sectionStart = LedgeY;
                    seconds = 0f;
                    inStair = false;
                }
                if (s.Kind == StepKind.Glide)
                {
                    if (inStair == false) { seconds += BrakeSeconds + LeadSeconds; }
                    seconds += s.Drop / GlideFall;
                }
                inStair = s.Kind == StepKind.Glide;
                lastNetY = s.ToY;
            }
            result.Add((sectionStart, seconds + GroundApproachSeconds));
            return result.ToArray();
        }
    }
}
