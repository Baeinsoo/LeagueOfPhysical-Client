using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 열려 있는 <c>SkydiveMap</c> 씬에 낙하 코스를 굽는다 — 선반마다 구멍 둘(빠른 구멍·안전한
    /// 구멍)이 뚫리고, 속도감을 보여 주는 모서리 기둥이 선다.
    ///
    /// 빠른 구멍에는 여닫이 문(<c>Doors</c>)이 달린다 — 콜라이더가 있는 벽이라 닫히는 동안 사람을
    /// 밀어내고, 완전히 닫힌 패널과 몸이 겹치면 죽는다(판정은 서버).
    ///
    /// 표(<c>Shelves</c>·<c>Doors</c>)가 곧 코스 설계다. 숫자를 고치고 다시 구우면 되고, 리뷰어는 표만 보면 된다.
    /// 굽기 전에 <see cref="SkydiveWindReach"/>로 — 바람까지 넣어 — 구멍 사이가 실제로 닿는 거리인지,
    /// 빠른/안전 구멍이 요구하는 자세가 실제로 갈리는지 검사한다.
    /// </summary>
    public static class SkydiveCourseBuilder
    {
        // 굽는 결과를 전부 담는 루트. 다시 구우면 통째로 지우고 새로 만든다.
        private const string CourseRootName = "Course";

        // 선반은 바닥과 같은 넓이다(x, z ∈ [-100, 100]). 이보다 크게 만들면 바닥 밖 허공에
        // 선반만 떠 있게 된다. 옆으로 크게 벗어나면 코스를 우회할 수 있는데, 그것을 막는 것은
        // 슬라이스 5(경계)의 일이다.
        private const float SlabHalf = 100f;
        private const float SlabThickness = 3f;

        private const float PillarSide = 4f;
        // 기둥이 구멍을 막으면 그 선반은 통과할 수 없다. 표를 고칠 때 이 값을 눈으로 지키게
        // 두지 않고 FindHoleOnPillar()가 굽기 전에 확인한다.
        private const float PillarOffset = 60f;

        // 검사 기준값. 마스터데이터에서 읽지 않는다 — 기준이 데이터를 따라 조용히 움직이면
        // 검사가 아니게 된다. TbSkydiveConfig를 바꿨다면 여기도 같이 고친다.
        private const float SpreadFallSpeed = 60f;
        private const float SpreadMoveSpeed = 12f;
        private const float SpreadTurnAccel = 22f;
        private const float DiveFallSpeed = 90f;
        private const float DiveMoveSpeed = 9f;
        private const float DiveTurnAccel = 6f;

        // internal — TbSkydiveConfig와 값이 같은지 EditMode 테스트가 대조한다(SkydiveWindLagConsistencyTests).
        internal const float SpreadWindLag = 2.06f;
        internal const float DiveWindLag = 3.10f;

        // 구름 층 머티리얼. 없으면 기본 불투명 머티리얼로 51장이 통째로 시야를 막아버리므로
        // 구름 자체를 굽지 않는다(경고만 낸다).
        private const string CloudMaterialPath = "Assets/Art/Materials/SkydiveCloud.mat";

        // 선반 머티리얼. 없으면 유니티 기본 머티리얼로 굽되 경고를 낸다.
        private const string StoneMaterialPath = "Assets/Art/Materials/SkydiveStone.mat";

        // 문 패널 두께. 선반 두께에서 위아래로 조금씩 물려 둔다 — 판과 면이 정확히 겹치면
        // 물러난 패널이 판 표면과 같은 평면에 놓여 바닥이 깜빡인다(z-파이팅).
        private const float PanelRecess = 0.1f;
        private const float PanelThickness = SlabThickness - 2f * PanelRecess;

        // 화살표 밀도. 개수는 부피(반지름×높이)에 비례한다 — 세기가 아니라 "큰 볼륨에서
        // 성기지 않게"를 위한 값이다. 반지름25×높이120(작은 기둥)에서 14개가 나오게 골랐다.
        private const float ArrowCountDivisor = 250f;
        private const int ArrowCountMin = 14;
        private const int ArrowCountMax = 200;

        // 선반 하나의 구멍 하나. HasDoor=true는 "가까운 구멍"(문이 여닫혀 다이브로 달려들어야
        // 타이밍이 맞는다), false는 "먼 구멍"(문이 없어 항상 열려 있지만 대자로만 닿는다) —
        // 스펙 §1·§3.1(2026-09-06-skydive-doors-and-branching-design).
        internal readonly struct Hole
        {
            public readonly float X;
            public readonly float Z;
            public readonly float Half;
            public readonly bool HasDoor;

            public Hole(float x, float z, float side, bool hasDoor)
            {
                X = x;
                Z = z;
                Half = side * 0.5f;
                HasDoor = hasDoor;
            }
        }

        internal readonly struct Shelf
        {
            public readonly float Y;
            public readonly Hole[] Holes;

            public Shelf(float y, Hole[] holes)
            {
                Y = y;
                Holes = holes;
            }
        }

        // 코스 설계 그 자체. 위에서 아래 순서로 적는다.
        // 부활 지점은 여기 없다 — 서버가 그 값으로 사람을 세우므로 LOP.SkydiveCourseLayout이
        // 진실원본이고, 여기서 또 적으면 두 곳이 조용히 어긋난다.
        //
        // 자리를 고른 기준은 물리다 — 낙하 400m당 대자 도달 76.7m, 다이브 도달 33.25m
        // (SkydiveWindReach.SelfReach, 무풍 기준). 빠른 구멍은 다이브 도달 안에, 안전한 구멍은 그 밖이되
        // 대자 도달 안에 둔다(스펙 §3.1). 여기에 바람이 더해진다: 순풍이 미는 자리에 안전한
        // 구멍을 두면 다이브가 공짜로 실려 가 도달해 버리므로, 바람이 센 구간에서는 안전한
        // 구멍을 바람을 가로지르는 쪽에 둔다. 그 조건들을 FindRouteNotSplit()이 굽기 전에 다 잰다.
        private static readonly Shelf[] Shelves =
        {
            new Shelf(2600f, new[]
            {
                new Hole(0f, 0f, 30f, hasDoor: true),     // 스폰 바로 아래, 아무것도 안 해도 지나간다
                new Hole(0f, 60f, 20f, hasDoor: false),
            }),
            new Shelf(2200f, new[]
            {
                new Hole(30f, 0f, 24f, hasDoor: true),    // 옆으로 가는 걸 가르치는 구간
                new Hole(55f, 30f, 20f, hasDoor: false),
            }),
            new Shelf(1800f, new[]
            {
                new Hole(30f, 30f, 20f, hasDoor: true),
                new Hole(15f, 70f, 20f, hasDoor: false),
            }),
            new Shelf(1400f, new[]
            {
                new Hole(0f, 20f, 16f, hasDoor: true),
                //  Wind_1600_Head가 +X로 민다. 안전한 구멍을 그 반대편(−X) 깊숙이 두면 바람이
                //  다이브를 여기까지 데려다주지 못한다 — 대자로 천천히 거슬러야 닿는다.
                new Hole(-35f, 80f, 20f, hasDoor: false),
            }),
            new Shelf(1000f, new[]
            {
                new Hole(-20f, -10f, 16f, hasDoor: true), // 강한 −Z 순풍이 다이브를 여기까지 실어 준다
                //  Wind_1200_Strong이 −Z로 세게 민다(다이브 −57.9m). 안전한 구멍을 순풍과
                //  직각인 +X 쪽에 두어, 실려 내려가면 오히려 지나쳐 버리게 한다.
                new Hole(15f, 25f, 16f, hasDoor: false),
            }),
            new Shelf(600f, new[]
            {
                new Hole(0f, -30f, 16f, hasDoor: true),
                new Hole(30f, -25f, 16f, hasDoor: false),
            }),
            new Shelf(200f, new[]
            {
                new Hole(0f, 0f, 16f, hasDoor: true),
                //  Wind_400_Tail(+Z)은 대자를 48m 실어 주지만 다이브는 22.5m밖에 못 싣는다.
                //  그 둘 사이보다 더 먼 +Z에 두면 대자로만 닿는다.
                new Hole(0f, 45f, 16f, hasDoor: false),
            }),
        };

        internal readonly struct WindSpec
        {
            public readonly string Name;
            public readonly Vector3 Center;
            public readonly float Radius;
            public readonly float Height;
            public readonly Vector3 Wind;

            public WindSpec(string name, Vector3 center, float radius, float height, Vector3 wind)
            {
                Name = name;
                Center = center;
                Radius = radius;
                Height = height;
                Wind = wind;
            }
        }

        // 반지름 150은 코스 폭(±100)을 다 덮는다 — 옆으로 피해 갈 수 있으면 그 구간이 아무것도
        // 안 묻게 된다. 피할 수 있어야 하는 것은 기둥(반지름 25)뿐이다.
        internal static readonly WindSpec[] Winds =
        {
            // 2600→2200 가르치기 ①: 짧은 순풍. 펴면 실려 가는데, 순풍이라 손해는 없다.
            new WindSpec("Wind_2400_Tail", new Vector3(0f, 2400f, 0f), 150f, 40f, new Vector3(10f, 0f, 0f)),

            // 2200→1800 가르치기 ②: 구멍(30,30) 위의 기둥. 펴면 위로 밀려 못 내려간다.
            new WindSpec("Wind_1900_Updraft", new Vector3(30f, 1900f, 30f), 25f, 120f, new Vector3(0f, 14f, 0f)),

            // 1800→1400: 역풍. 구멍은 −X 쪽인데 바람은 +X다. 구간 전체로 깔면 55m 이동에
            //            68m 역풍이 더해져 아무도 못 지나가므로 높이를 150으로 잘라 둔다.
            new WindSpec("Wind_1600_Head", new Vector3(0f, 1600f, 0f), 150f, 150f, new Vector3(10f, 0f, 0f)),

            // 1400→1000 ★ 이 코스의 요점: 구간 전체를 덮는 강한 순풍. 타면 다이브로도 60m를 간다.
            new WindSpec("Wind_1200_Strong", new Vector3(0f, 1200f, 0f), 150f, 400f, new Vector3(0f, 0f, -20f)),

            // 1000→600: 길 좌우의 기둥 둘. 가운데 15m 통로만 천을 펴고 지날 수 있다.
            new WindSpec("Wind_800_UpdraftL", new Vector3(-30f, 800f, -27f), 25f, 120f, new Vector3(0f, 14f, 0f)),
            new WindSpec("Wind_800_UpdraftR", new Vector3(35f, 800f, -27f), 25f, 120f, new Vector3(0f, 14f, 0f)),

            // 600→200: +Z 순풍이 50m 이동을 절반쯤 대신해 준다.
            new WindSpec("Wind_400_Tail", new Vector3(0f, 400f, 0f), 150f, 250f, new Vector3(0f, 0f, 12f)),

            // 마지막 선반의 안전한 구멍 위 기둥 — 착지를 패러세일로 때우지 못하게 한다.
            //  구멍이 (0,25)에서 (0,45)로 올라가면서 원기둥(중심 z=25, 반지름 25 → z ≤ 50)이
            //  구멍(x −8~8, z 37~53) 넓이의 78.5%만 덮는다(격자 적분으로 실측). 세로 바람은
            //  자리 검사(PostureShortfall)가 일부러 안 보므로 이 약화는 검사에 안 잡힌다 —
            //  알고 두는 것이다.
            new WindSpec("Wind_300_Updraft", new Vector3(0f, 300f, 25f), 25f, 120f, new Vector3(0f, 14f, 0f)),
        };

        internal readonly struct LaserSpec
        {
            public readonly string Name;
            public readonly Vector3 Pivot;
            public readonly float Length;
            public readonly float Radius;
            public readonly float StartAngleDegrees;
            public readonly float AngularSpeedDegreesPerTick;
            public readonly float SweepHalfRangeDegrees;
            public readonly int Period;
            public readonly int OnTicks;
            public readonly int Phase;

            public LaserSpec(string name, Vector3 pivot, float length, float radius,
                             float startAngleDegrees, float angularSpeedDegreesPerTick,
                             float sweepHalfRangeDegrees, int period, int onTicks, int phase)
            {
                Name = name;
                Pivot = pivot;
                Length = length;
                Radius = radius;
                StartAngleDegrees = startAngleDegrees;
                AngularSpeedDegreesPerTick = angularSpeedDegreesPerTick;
                SweepHalfRangeDegrees = sweepHalfRangeDegrees;
                Period = period;
                OnTicks = onTicks;
                Phase = phase;
            }

            public LOP.Laser ToLaser() => new LOP.Laser(
                new System.Numerics.Vector3(Pivot.x, Pivot.y, Pivot.z),
                Length, Radius,
                StartAngleDegrees * Mathf.Deg2Rad,
                AngularSpeedDegreesPerTick * Mathf.Deg2Rad,
                SweepHalfRangeDegrees * Mathf.Deg2Rad,
                Period, OnTicks, Phase);
        }

        // 한 틱에 이보다 크게 돌면 다음 자리를 눈으로 예측할 수 없다 — 피할 수 없는 것은
        // 장애물이 아니라 주사위다.
        private const float MaxAngularSpeedDegreesPerTick = 15f;

        // 코스 설계 그 자체. 구간마다 어법이 다르다: 문지기 → 격자 → 합침.
        // 문지기는 자기 선반의 빠른 구멍을 쓸도록 세운다(피벗이 구멍 안일 수도, 밖일 수도 있다).
        // 안전한 구멍을 쓸면 안 되는데, 그건 주석이 아니라 FindLaserOnSafeHole이 막는다.
        internal static readonly LaserSpec[] Lasers =
        {
            // 2600 위: 없음 — 조작을 익히는 자리

            // 2200 빠른 구멍(30,0) 문지기 — 느린 회전. 반대편으로 들어가면 된다.
            //  빔이 26m였을 때 끝이 안전한 구멍(55,30) 모서리까지 2m 들어갔다(피벗에서 그 모서리까지
            //  25m). 20m면 빠른 구멍(반폭 12, 모서리까지 17m)은 그대로 덮으면서 안전한 구멍에서 4m 뜬다.
            new LaserSpec("Laser_2200_Gate", new Vector3(30f, 2215f, 0f),
                          length: 20f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 4f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),

            // 1800 빠른 구멍(30,30) 문지기 — 왕복. 오는 것이 보인다.
            new LaserSpec("Laser_1800_Gate", new Vector3(30f, 1815f, 30f),
                          length: 24f, radius: 0.6f,
                          startAngleDegrees: 90f, angularSpeedDegreesPerTick: 6f,
                          sweepHalfRangeDegrees: 70f, period: 0, onTicks: 0, phase: 0),

            // 1400~1800 통로: 격자 연습 — 벽에서 뻗은 고정 빔 두 층
            new LaserSpec("Laser_1650_Bar", new Vector3(-100f, 1650f, 0f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            new LaserSpec("Laser_1500_Bar", new Vector3(100f, 1500f, 40f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 180f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),

            // 1400 빠른 구멍(0,20)을 쓰는 문지기(피벗은 구멍 밖) + 통로 격자
            new LaserSpec("Laser_1400_Gate", new Vector3(-25f, 1415f, 30f),
                          length: 22f, radius: 0.6f,
                          startAngleDegrees: 45f, angularSpeedDegreesPerTick: 7f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            new LaserSpec("Laser_1250_Bar", new Vector3(-100f, 1250f, -20f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),

            // 1000 빠른 구멍(-20,-10)을 쓰는 문지기(피벗은 구멍 밖) + 점멸 격자(리듬)
            new LaserSpec("Laser_1000_Gate", new Vector3(-25f, 1015f, -30f),
                          length: 22f, radius: 0.6f,
                          startAngleDegrees: 200f, angularSpeedDegreesPerTick: 8f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            new LaserSpec("Laser_900_Blink", new Vector3(100f, 900f, 0f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 180f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 40, onTicks: 20, phase: 0),
            new LaserSpec("Laser_800_Blink", new Vector3(-100f, 800f, 30f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 40, onTicks: 20, phase: 20),

            // 600 빠른 구멍(0,-30) 문지기 — 빠르게. 옆 안전한 구멍(30,-25)이 22m 거리라
            //  빔을 18m로 줄여 거기까지 닿지 않게 했다(FindLaserOnSafeHole이 잰다).
            new LaserSpec("Laser_600_Gate", new Vector3(0f, 615f, -30f),
                          length: 18f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 11f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            new LaserSpec("Laser_450_Blink", new Vector3(100f, 450f, -40f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 180f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 30, onTicks: 15, phase: 0),

            // 200 빠른 구멍(0,0) 문지기 — 셋을 합친다
            new LaserSpec("Laser_200_Gate", new Vector3(0f, 215f, 0f),
                          length: 22f, radius: 0.6f,
                          startAngleDegrees: 120f, angularSpeedDegreesPerTick: 12f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            new LaserSpec("Laser_320_Sweep", new Vector3(-100f, 320f, 0f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 5f,
                          sweepHalfRangeDegrees: 40f, period: 0, onTicks: 0, phase: 0),
        };

        internal readonly struct DoorSpec
        {
            public readonly string Name;

            /// <summary>구멍 중심. 문 허브가 놓이는 자리다.</summary>
            public readonly Vector3 Center;

            /// <summary>덮는 폭의 절반(=구멍 반폭). 패널 하나는 이 값의 절반 길이다.</summary>
            public readonly float HalfWidth;

            /// <summary>미끄러지는 방향과 직교하는 쪽 절반.</summary>
            public readonly float HalfDepth;

            /// <summary>패널이 미끄러지는 방향(XZ 평면 각, 도).</summary>
            public readonly float AxisAngleDegrees;

            public readonly int Period;
            public readonly int OpenTicks;
            public readonly int MoveTicks;
            public readonly int Phase;

            public DoorSpec(string name, Vector3 center, float halfWidth, float halfDepth,
                            float axisAngleDegrees, int period, int openTicks, int moveTicks, int phase)
            {
                Name = name;
                Center = center;
                HalfWidth = halfWidth;
                HalfDepth = halfDepth;
                AxisAngleDegrees = axisAngleDegrees;
                Period = period;
                OpenTicks = openTicks;
                MoveTicks = moveTicks;
                Phase = phase;
            }

            /// <summary>닫혀 있는 틱 수. 나머지 셋에서 나온다.</summary>
            public int ClosedTicks => Period - OpenTicks - 2 * MoveTicks;

            public LOP.Door ToDoor() => new LOP.Door(
                new System.Numerics.Vector3(Center.x, Center.y, Center.z),
                HalfWidth, HalfDepth, PanelThickness,
                AxisAngleDegrees * Mathf.Deg2Rad,
                Period, OpenTicks, MoveTicks, Phase);
        }

        // 코스 설계 그 자체 — 문은 빠른 구멍에만, 구멍 하나에 하나씩 붙는다(스펙 §1).
        // 표 둘이 조용히 어긋나는 것은 FindDoorHoleMismatch/FindDoorSizeMismatch가 굽기 전에 잡는다.
        //
        // 틱은 50Hz다(주기 200틱 = 4초). 위에서 아래로 갈수록 주기가 짧아지고 닫혀 있는 비율이
        // 커진다(15% → 33%) — 코스가 가르치는 순서다. 닫힘은 아무리 길어도 40틱(0.8초)이라
        // 문을 놓쳐도 벌은 "선반 위에서 잠깐 기다리기"로 끝난다.
        // 주기 일곱의 최소공배수가 655,200틱(≈3.6시간)이라 한 판 안에서 같은 리듬이 되풀이되지 않는다.
        //
        // 미끄러지는 축은 90°의 배수만 쓴다 — 구멍이 정사각이라 그 외의 각도로 닫으면 네 모서리가
        // 뚫린 채로 "닫힘"이 된다. 축을 고를 때 더 좁히는 조건이 둘 더 있다: 물러난 패널은 구멍 밖
        // 판 위에 콜라이더로 남으므로 (a) 부활 지점을 덮으면 안 되고(FindRespawnInDoorPanel),
        // (b) 같은 선반의 안전한 구멍을 막으면 안 된다(FindDoorPanelOnSafeHole).
        // 200 선반의 축이 0°인 것이 그 예다 — 부활 지점이 구멍 가까이에 있어 90°로 두면 물러난
        // 패널이 그 자리를 덮는다. 거리 수치를 여기 적지 않는 것은 그 값들이 다른 표(구멍·부활
        // 지점)에 살아서, 한쪽만 옮기면 검사는 초록인 채 주석만 거짓이 되기 때문이다.
        internal static readonly DoorSpec[] Doors =
        {
            //  스폰 바로 아래. 문이 무엇인지 보여 주기만 한다 — 열려 있는 시간이 가장 길다.
            new DoorSpec("Door_2600", new Vector3(0f, 2600f, 0f), halfWidth: 15f, halfDepth: 15f,
                         axisAngleDegrees: 0f, period: 200, openTicks: 120, moveTicks: 25, phase: 0),

            new DoorSpec("Door_2200", new Vector3(30f, 2200f, 0f), halfWidth: 12f, halfDepth: 12f,
                         axisAngleDegrees: 90f, period: 180, openTicks: 100, moveTicks: 22, phase: 40),

            new DoorSpec("Door_1800", new Vector3(30f, 1800f, 30f), halfWidth: 10f, halfDepth: 10f,
                         axisAngleDegrees: 90f, period: 160, openTicks: 84, moveTicks: 20, phase: 90),

            new DoorSpec("Door_1400", new Vector3(0f, 1400f, 20f), halfWidth: 8f, halfDepth: 8f,
                         axisAngleDegrees: 90f, period: 150, openTicks: 70, moveTicks: 20, phase: 25),

            new DoorSpec("Door_1000", new Vector3(-20f, 1000f, -10f), halfWidth: 8f, halfDepth: 8f,
                         axisAngleDegrees: 0f, period: 140, openTicks: 60, moveTicks: 20, phase: 70),

            new DoorSpec("Door_600", new Vector3(0f, 600f, -30f), halfWidth: 8f, halfDepth: 8f,
                         axisAngleDegrees: 90f, period: 130, openTicks: 50, moveTicks: 20, phase: 15),

            //  0°가 강제된다 — 위 표 머리 주석 참고.
            new DoorSpec("Door_200", new Vector3(0f, 200f, 0f), halfWidth: 8f, halfDepth: 8f,
                         axisAngleDegrees: 0f, period: 120, openTicks: 40, moveTicks: 20, phase: 60),
        };

        [MenuItem("LOP/Skydive/코스 굽기")]
        public static void Build()
        {
            //  결과는 대화상자가 아니라 콘솔로 낸다. 모달 대화상자는 메인 스레드를 잡아서,
            //  자동화(CLI)로 이 메뉴를 부르면 에디터가 통째로 멈춘다 — 실제로 한 번 겪었다.
            if (Verify(out string report) == false)
            {
                Debug.LogError($"[Skydive] 코스를 굽지 않았다 — 통과 불가능하다.\n{report}");
                return;
            }

            // 옛 코스를 지우기 전에 검사한다 — 여기서 걸리면 씬은 손대지 않은 그대로다.
            string impassable = FindImpassableSection();
            if (impassable != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {impassable}. 씬은 바뀌지 않았다.");
                return;
            }

            string safeLaneBlocked = FindImpassableSection(Winds, safeOnly: true);
            if (safeLaneBlocked != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {safeLaneBlocked}. 씬은 바뀌지 않았다.");
                return;
            }

            string holeOnPillar = FindHoleOnPillar();
            if (holeOnPillar != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {holeOnPillar}. 씬은 바뀌지 않았다.");
                return;
            }

            string blockedGate = FindBlockedGate();
            if (blockedGate != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {blockedGate}. 씬은 바뀌지 않았다.");
                return;
            }

            string laserOnSafeHole = FindLaserOnSafeHole();
            if (laserOnSafeHole != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {laserOnSafeHole}. 씬은 바뀌지 않았다.");
                return;
            }

            string drift = FindShelfLayoutDrift();
            if (drift != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {drift}. 씬은 바뀌지 않았다.");
                return;
            }

            string invalidRespawn = FindInvalidRespawn();
            if (invalidRespawn != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {invalidRespawn}. 씬은 바뀌지 않았다.");
                return;
            }

            string doorHole = FindDoorHoleMismatch();
            if (doorHole != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {doorHole}. 씬은 바뀌지 않았다.");
                return;
            }

            string doorSize = FindDoorSizeMismatch();
            if (doorSize != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {doorSize}. 씬은 바뀌지 않았다.");
                return;
            }

            string respawnInDoor = FindRespawnInDoorPanel();
            if (respawnInDoor != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {respawnInDoor}. 씬은 바뀌지 않았다.");
                return;
            }

            string panelOnSafeHole = FindDoorPanelOnSafeHole();
            if (panelOnSafeHole != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {panelOnSafeHole}. 씬은 바뀌지 않았다.");
                return;
            }

            string neverCloses = FindDoorNeverCloses();
            if (neverCloses != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {neverCloses}. 씬은 바뀌지 않았다.");
                return;
            }

            string tooFast = FindTooFastLaser(Lasers);
            if (tooFast != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {tooFast}. 씬은 바뀌지 않았다.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(StoneMaterialPath);
            if (material == null)
            {
                Debug.LogWarning($"[Skydive] 선반 머티리얼이 없다 — {StoneMaterialPath}. 기본색으로 굽는다.");
            }

            GameObject root = GameObject.Find(CourseRootName);
            if (root != null)
            {
                Object.DestroyImmediate(root);   // 다시 구울 때 옛 코스가 겹쳐 남지 않게
            }
            root = new GameObject(CourseRootName);

            for (int i = 0; i < Shelves.Length; i++)
            {
                BuildShelf(root.transform, Shelves[i], material);
                float upperY = i == 0 ? LOP.SkydiveCourseLayout.SpawnY : Shelves[i - 1].Y;
                BuildPillars(root.transform, Shelves[i].Y, upperY, material);
            }

            Material cloud = AssetDatabase.LoadAssetAtPath<Material>(CloudMaterialPath);
            if (cloud == null)
            {
                // 기본 머티리얼은 불투명이라, 그대로 구우면 460m짜리 판 51장이 시야를 통째로
                // 가려버린다("판만 굽는다"가 아니라 완전 블랙아웃) — 아예 굽지 않는다.
                Debug.LogWarning($"[Skydive] 구름 머티리얼이 없다 — {CloudMaterialPath}. 구름을 굽지 않는다.");
            }
            else
            {
                var clouds = new GameObject("Clouds");
                clouds.transform.SetParent(root.transform, worldPositionStays: false);
                for (int i = 0; i < SkydiveCloudLayers.Altitudes.Length; i++)
                {
                    float y = SkydiveCloudLayers.Altitudes[i];

                    // 한 층을 판 세 장으로 겹쳐 놓는다 — 한 장이면 옆에서 볼 때 종잇장이라 층이 안 된다.
                    for (int k = 0; k < 3; k++)
                    {
                        float dy = (k - 1) * SkydiveCloudLayers.HalfThickness * 0.6f;
                        CreateCloudQuad(clouds.transform, $"Cloud_{y:0}_{k}",
                                        new Vector3(0f, y + dy, 0f), 460f, cloud);
                    }
                }
            }

            WindVisualAssets windAssets = SkydiveWindAssets.EnsureAssets();
            if (windAssets.IsComplete == false)
            {
                // 기본 머티리얼은 불투명이라 원기둥 면이 시야를 통째로 막는다. 마커만 굽는다.
                Debug.LogWarning("[Skydive] 바람 시각물 에셋을 만들지 못했다. 시각물 없이 마커만 굽는다.");
            }

            var winds = new GameObject("Winds");
            winds.transform.SetParent(root.transform, worldPositionStays: false);
            for (int i = 0; i < Winds.Length; i++)
            {
                var spec = Winds[i];
                CreateWindVolume(winds.transform, spec.Name, spec.Center,
                                 spec.Radius, spec.Height, spec.Wind, windAssets);
            }

            var lasers = new GameObject("Lasers");
            lasers.transform.SetParent(root.transform, worldPositionStays: false);
            for (int i = 0; i < Lasers.Length; i++)
            {
                CreateLaserVolume(lasers.transform, Lasers[i]);
            }

            var doors = new GameObject("Doors");
            doors.transform.SetParent(root.transform, worldPositionStays: false);
            bool doorMismatched = false;
            for (int i = 0; i < Doors.Length; i++)
            {
                GameObject door = CreateDoorVolume(doors.transform, Doors[i], material);

                //  구운 패널이 판정 상자와 같은지 여기서 되읽어 본다. 표가 아니라 굽는 코드를
                //  재는 검사라 씬을 지운 뒤에야 돌 수 있다 — 걸리면 씬이 이미 새로 구워진 상태다.
                string mismatch = FindDoorPanelMismatch(door.GetComponent<LOP.DoorVolume>(), Doors[i]);
                if (mismatch != null)
                {
                    doorMismatched = true;
                    Debug.LogError($"[Skydive] 구운 문이 판정과 어긋난다 — {mismatch}. 굽는 코드를 고쳐라.");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);

            //  어긋난 문이 있는데 "저장해라"로 끝내면 죽이는 상자와 보이는 상자가 갈린 씬이 그대로
            //  커밋된다. 다른 게이트와 달리 여기서는 씬이 이미 새로 구워진 뒤라 되돌리는 것은 사람 몫이다.
            if (doorMismatched)
            {
                Debug.LogError("[Skydive] 코스를 구웠지만 문이 판정과 어긋난다 — 저장하지 마라. " +
                               "굽는 코드를 고치고 다시 구워라(씬은 이미 새로 구워진 상태다).");
                return;
            }

            Debug.Log($"[Skydive] 코스를 구웠다 — 선반 {Shelves.Length}개, 문 {Doors.Length}개. 씬을 저장해라.\n{report}");
        }

        // 판의 사각형 한 조각(XZ 평면). Name은 이 조각이 어느 구멍의 어느 쪽에서 떨어져 나왔는지
        // (N/S/E/W를 이어 붙인 것)라서, 씬 diff나 디버깅에서 어느 조각인지 바로 읽힌다.
        internal readonly struct Plate
        {
            public readonly string Name;
            public readonly float XMin, XMax, ZMin, ZMax;

            public Plate(string name, float xMin, float xMax, float zMin, float zMax)
            {
                Name = name;
                XMin = xMin;
                XMax = xMax;
                ZMin = zMin;
                ZMax = zMax;
            }

            public float Width => XMax - XMin;
            public float Depth => ZMax - ZMin;
            public float Area => Width * Depth;
        }

        /// <summary>
        /// 판에서 구멍들을 도려내고 남은 사각형 조각들을 준다. 하나의 큰 판에 구멍을 뚫을 수는
        /// 없어서(상자 콜라이더는 볼록한 덩어리뿐) 조각으로 쪼갠다 — 구멍이 하나면 북/남/동/서
        /// 네 조각이 나오고, 구멍이 더 있으면 그때까지 남은 조각들에 같은 식을 반복한다.
        /// GameObject를 만들지 않는 순수 함수라 Unity 없이 검사할 수 있다.
        /// </summary>
        internal static List<Plate> Carve(in Plate plate, IReadOnlyList<Hole> holes)
        {
            var plates = new List<Plate> { plate };

            for (int h = 0; h < holes.Count; h++)
            {
                Hole hole = holes[h];
                float holeXMin = hole.X - hole.Half;
                float holeXMax = hole.X + hole.Half;
                float holeZMin = hole.Z - hole.Half;
                float holeZMax = hole.Z + hole.Half;

                var next = new List<Plate>();
                foreach (Plate piece in plates)
                {
                    //  이 구멍과 이 조각이 겹치는 부분만 도려낸다 — 조각이 이미 이전 구멍으로
                    //  좁아져 있을 수 있으므로 구멍 범위를 조각 범위로 한 번 더 자른다.
                    float xLo = Mathf.Max(piece.XMin, holeXMin);
                    float xHi = Mathf.Min(piece.XMax, holeXMax);
                    float zLo = Mathf.Max(piece.ZMin, holeZMin);
                    float zHi = Mathf.Min(piece.ZMax, holeZMax);

                    if (xLo >= xHi || zLo >= zHi)
                    {
                        next.Add(piece);   // 안 겹치는 조각은 그대로 둔다
                        continue;
                    }

                    string stem = piece.Name.Length == 0 ? string.Empty : piece.Name + "_";
                    if (piece.ZMax > zHi)
                    {
                        next.Add(new Plate(stem + "N", piece.XMin, piece.XMax, zHi, piece.ZMax));
                    }
                    if (zLo > piece.ZMin)
                    {
                        next.Add(new Plate(stem + "S", piece.XMin, piece.XMax, piece.ZMin, zLo));
                    }
                    if (piece.XMax > xHi)
                    {
                        next.Add(new Plate(stem + "E", xHi, piece.XMax, zLo, zHi));
                    }
                    if (xLo > piece.XMin)
                    {
                        next.Add(new Plate(stem + "W", piece.XMin, xLo, zLo, zHi));
                    }
                }
                plates = next;
            }

            return plates;
        }

        internal static Plate FullSlab() => new Plate(string.Empty, -SlabHalf, SlabHalf, -SlabHalf, SlabHalf);

        // 선반 = 구멍들을 뺀 나머지 판.
        private static void BuildShelf(Transform parent, in Shelf shelf, Material material)
        {
            List<Plate> plates = Carve(FullSlab(), shelf.Holes);

            foreach (Plate p in plates)
            {
                AddBox(parent, $"Shelf_{shelf.Y:0}_{p.Name}", material,
                    new Vector3((p.XMin + p.XMax) * 0.5f, shelf.Y, (p.ZMin + p.ZMax) * 0.5f),
                    new Vector3(p.Width, SlabThickness, p.Depth));
            }
        }

        // 떨어지는 동안 옆에 뭔가 지나가야 속도가 보인다. 길에서 멀리 떨어뜨려 놓는다.
        private static void BuildPillars(Transform parent, float lowerY, float upperY, Material material)
        {
            float height = upperY - lowerY;
            if (height <= 0f)
            {
                return;
            }
            float centerY = lowerY + height * 0.5f;

            foreach (float sx in new[] { -PillarOffset, PillarOffset })
            {
                foreach (float sz in new[] { -PillarOffset, PillarOffset })
                {
                    AddBox(parent, $"Pillar_{lowerY:0}_{sx:0}_{sz:0}", material,
                        new Vector3(sx, centerY, sz),
                        new Vector3(PillarSide, height, PillarSide));
                }
            }
        }

        private static void AddBox(Transform parent, string name, Material material,
                                   Vector3 center, Vector3 size)
        {
            //  두께가 0 이하인 판은 만들지 않는다 — 구멍이 판 끝에 붙으면 생길 수 있다.
            if (size.x <= 0.01f || size.y <= 0.01f || size.z <= 0.01f)
            {
                return;
            }

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, worldPositionStays: false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            box.layer = LayerMask.NameToLayer("Default");   // sweep 마스크가 보는 레이어
            if (material != null)
            {
                box.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        // 구름 판. 콜라이더를 반드시 지운다 — 남으면 키네마틱 이동이 벽으로 보고
        // 그 위에 착지한다.
        internal static GameObject CreateCloudQuad(Transform parent, string name,
                                                   Vector3 center, float size, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            if (parent != null)
            {
                quad.transform.SetParent(parent, worldPositionStays: false);
            }
            quad.transform.localPosition = center;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 수평으로 눕힌다
            quad.transform.localScale = new Vector3(size, size, 1f);

            var collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            if (material != null)
            {
                quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            return quad;
        }

        internal static GameObject CreateWindVolume(Transform parent, string name, Vector3 center,
                                                    float radius, float height, Vector3 wind,
                                                    WindVisualAssets assets)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, worldPositionStays: false);
            }
            go.transform.localPosition = center;

            var marker = go.AddComponent<LOP.WindVolume>();
            marker.Radius = radius;
            marker.Height = height;
            marker.Wind = wind;

            float speed = wind.magnitude;
            if (assets == null || assets.IsComplete == false || speed <= 0.001f)
            {
                return go;
            }

            var arrows = new GameObject("Arrows");
            arrows.transform.SetParent(go.transform, worldPositionStays: false);
            CreateWindArrows(arrows.transform, name, radius, height, wind, speed, assets);

            // 범위 표시는 한 부모 아래에 통째로 모은다 — 나중에 옵션으로 끌 때 이 하나만 끄면
            // 화살표와 흐름만 남는다.
            var bounds = new GameObject("Bounds");
            bounds.transform.SetParent(go.transform, worldPositionStays: false);
            CreateWindBounds(bounds.transform, radius, height, speed, assets);

            var visualizer = go.AddComponent<LOP.WindVolumeVisualizer>();
            visualizer.ArrowsRoot = arrows.transform;
            visualizer.BoundsRoot = bounds;

            return go;
        }

        // 레이저는 그리지 않는다(뷰는 별도 작업) — 여기서는 판정 마커만 굽는다.
        internal static GameObject CreateLaserVolume(Transform parent, in LaserSpec spec)
        {
            var go = new GameObject(spec.Name);
            if (parent != null)
            {
                go.transform.SetParent(parent, worldPositionStays: false);
            }
            go.transform.localPosition = spec.Pivot;

            var marker = go.AddComponent<LOP.LaserVolume>();
            marker.Length = spec.Length;
            marker.Radius = spec.Radius;
            marker.StartAngleDegrees = spec.StartAngleDegrees;
            marker.AngularSpeedDegreesPerTick = spec.AngularSpeedDegreesPerTick;
            marker.SweepHalfRangeDegrees = spec.SweepHalfRangeDegrees;
            marker.Period = spec.Period;
            marker.OnTicks = spec.OnTicks;
            marker.Phase = spec.Phase;
            return go;
        }

        /// <summary>
        /// 문 하나 — 허브(<see cref="LOP.DoorVolume"/>) 밑에 패널 둘.
        ///
        /// <para><b>레이저와 달리 콜라이더를 남긴다.</b> 문은 닫히는 동안 사람을 밀어내는 <b>벽</b>이라,
        /// 레이저 마커처럼 콜라이더를 지우면 밀려나지도 않고 그냥 통과해 버린다.</para>
        ///
        /// <para>허브에는 회전을 넣지 않는다 — 패널 오프셋이 이미 <c>AxisAngle</c>로 월드 축 기준
        /// 방향을 잡으므로 부모가 또 돌면 자식 로컬 좌표가 두 번 꺾인다.</para>
        /// </summary>
        internal static GameObject CreateDoorVolume(Transform parent, in DoorSpec spec, Material material)
        {
            var go = new GameObject(spec.Name);
            if (parent != null)
            {
                go.transform.SetParent(parent, worldPositionStays: false);
            }
            go.transform.localPosition = spec.Center;
            go.transform.localRotation = Quaternion.identity;

            var marker = go.AddComponent<LOP.DoorVolume>();
            marker.HalfWidth = spec.HalfWidth;
            marker.HalfDepth = spec.HalfDepth;
            marker.Thickness = PanelThickness;
            marker.AxisAngleDegrees = spec.AxisAngleDegrees;
            marker.Period = spec.Period;
            marker.OpenTicks = spec.OpenTicks;
            marker.MoveTicks = spec.MoveTicks;
            marker.Phase = spec.Phase;

            marker.PanelA = CreateDoorPanel(go.transform, spec.Name + "_A", spec, material);
            marker.PanelB = CreateDoorPanel(go.transform, spec.Name + "_B", spec, material);

            //  씬에 저장되는 자세 하나는 있어야 한다. 런타임에는 매 틱 다시 잡힌다.
            marker.Pose(0d);
            return go;
        }

        private static Transform CreateDoorPanel(Transform parent, string name,
                                                 in DoorSpec spec, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, worldPositionStays: false);
            box.transform.localRotation = LOP.DoorVolume.PanelRotation(spec.AxisAngleDegrees);
            //  판정 상자(DoorGeometry)의 반치수는 (HalfWidth/2, Thickness/2, HalfDepth)다 —
            //  크기는 그 두 배. 여기가 어긋나면 보이는 도형과 죽이는 도형이 갈린다.
            box.transform.localScale = new Vector3(spec.HalfWidth, PanelThickness, spec.HalfDepth * 2f);
            box.layer = LayerMask.NameToLayer("Default");   // sweep 마스크가 보는 레이어
            if (material != null)
            {
                box.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            return box.transform;
        }

        private static void CreateWindArrows(Transform parent, string name, float radius, float height,
                                             Vector3 wind, float speed, WindVisualAssets assets)
        {
            int count = Mathf.Clamp(Mathf.RoundToInt(radius * height / ArrowCountDivisor),
                                    ArrowCountMin, ArrowCountMax);
            Material material = assets.ArrowFor(speed);
            Quaternion rotation = Quaternion.LookRotation(wind / speed);

            for (int k = 0; k < count; k++)
            {
                // 황금각 나선 — 난수 없이 고르게 흩어진다. 다시 구워도 같은 자리에 나온다.
                float t = (k + 0.5f) / count;
                float angle = k * 2.39996f;
                float r = radius * Mathf.Sqrt(t);

                var arrow = new GameObject($"{name}_Arrow{k}");
                arrow.transform.SetParent(parent, worldPositionStays: false);
                arrow.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * r, (t - 0.5f) * height, Mathf.Sin(angle) * r);
                arrow.transform.localRotation = rotation;
                // 길이 = 바람이 1초에 미는 거리. 세기가 곧 화살표 크기라 범례가 필요 없다.
                arrow.transform.localScale = Vector3.one * speed;

                arrow.AddComponent<MeshFilter>().sharedMesh = assets.Arrow;
                arrow.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        private static void CreateWindBounds(Transform parent, float radius, float height,
                                             float speed, WindVisualAssets assets)
        {
            var shell = new GameObject("Shell");
            shell.transform.SetParent(parent, worldPositionStays: false);
            // 원본은 반지름 0.5·높이 1이라 지름과 높이를 그대로 스케일로 준다.
            shell.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);

            shell.AddComponent<MeshFilter>().sharedMesh = assets.Shell;
            shell.AddComponent<MeshRenderer>().sharedMaterial = assets.ShellFor(speed);
        }

        /// <summary>
        /// 바람 때문에 아무 자세로도 못 지나가는 구간이 있으면 그 설명을, 없으면 null을 준다.
        /// 역풍은 밀린 거리와 필요 이동이 더해져 구간을 막을 수 있는데, 그러면 에러 없이
        /// 판이 안 끝나는 것으로만 보인다. 표(<see cref="Winds"/>)를 굽기 전에 검사할 때 쓴다.
        /// </summary>
        internal static string FindImpassableSection() => FindImpassableSection(Winds);

        /// <summary>
        /// 위와 같지만 볼륨을 표 대신 <paramref name="winds"/>로 받는다. 볼륨은 씬에서 디자이너가
        /// 손으로 만지므로, 구운 맵을 읽어 이 검사를 돌리려면 표가 아니라 데이터가 필요하다.
        /// </summary>
        internal static string FindImpassableSection(IReadOnlyList<WindSpec> winds)
            => FindImpassableSection(winds, safeOnly: false);

        /// <summary>
        /// <paramref name="safeOnly"/>가 참이면 <b>안전한 구멍만</b>으로 각 구간을 지날 수 있는지
        /// 본다. 스펙 §3.2 ②("문을 하나도 못 뚫어도 판이 끝난다")는 안전한 레인 하나가 끝까지
        /// 열려 있기를 요구하므로, "네 조합 중 아무거나 하나"로는 그 요구를 증명하지 못한다 —
        /// 빠른 구멍만 뚫려 있어도 통과로 보이기 때문이다.
        /// </summary>
        internal static string FindImpassableSection(IReadOnlyList<WindSpec> winds, bool safeOnly)
        {
            for (int i = 1; i < Shelves.Length; i++)
            {
                float upperY = Shelves[i - 1].Y;
                float lowerY = Shelves[i].Y;
                float drop = upperY - lowerY;

                bool anyPasses = false;
                foreach (Hole prevHole in Shelves[i - 1].Holes)
                {
                    if (safeOnly && prevHole.HasDoor)
                    {
                        continue;
                    }
                    foreach (Hole hole in Shelves[i].Holes)
                    {
                        if (safeOnly && hole.HasDoor)
                        {
                            continue;
                        }
                        float requiredX = hole.X - prevHole.X;
                        float requiredZ = hole.Z - prevHole.Z;

                        if (PosturePasses(winds, upperY, lowerY, drop, requiredX, requiredZ, hole.Half,
                                          SpreadFallSpeed, SpreadMoveSpeed, SpreadTurnAccel, SpreadWindLag) ||
                            PosturePasses(winds, upperY, lowerY, drop, requiredX, requiredZ, hole.Half,
                                          DiveFallSpeed, DiveMoveSpeed, DiveTurnAccel, DiveWindLag))
                        {
                            anyPasses = true;
                            break;
                        }
                    }
                    if (anyPasses)
                    {
                        break;
                    }
                }

                if (anyPasses == false)
                {
                    return safeOnly
                        ? $"{upperY:0} → {lowerY:0} 구간을 안전한 구멍만으로는 못 지나간다. 바람 표나 안전한 구멍 자리를 고쳐라."
                        : $"{upperY:0} → {lowerY:0} 구간을 대자로도 다이브로도 못 지나간다. 바람 표를 고쳐라.";
                }
            }
            return null;
        }

        /// <summary>
        /// 바람에 밀린 자리에서 구멍까지 자기 힘으로 얼마나 <b>모자라나</b>. 0 이하면 닿는다.
        /// 바람 검사와 검사 ③이 같은 이 한 벌을 쓴다 — 두 벌이면 한쪽만 바람을 보게 된다.
        /// </summary>
        private static float PostureShortfall(IReadOnlyList<WindSpec> winds,
                                              float upperY, float lowerY, float drop,
                                              float requiredX, float requiredZ, float holeHalf,
                                              float fallSpeed, float moveSpeed, float turnAccel, float lag)
        {
            float driftX = 0f;
            float driftZ = 0f;
            for (int w = 0; w < winds.Count; w++)
            {
                var spec = winds[w];
                float overlap = Overlap(upperY, lowerY, spec);
                if (overlap <= 0f)
                {
                    continue;
                }
                float tailHeight = TailHeight(lowerY, spec);
                // 세로 바람은 옆으로 안 민다 — 낙하 속도를 바꾸지만 그 영향은 작아 여기선 안 본다.
                driftX += SkydiveWindReach.DriftDistance(spec.Wind.x, overlap, fallSpeed, lag, tailHeight);
                driftZ += SkydiveWindReach.DriftDistance(spec.Wind.z, overlap, fallSpeed, lag, tailHeight);
            }

            // 구멍 반쪽만큼은 덤이다 — 중심까지 안 가도 가장자리로 들어가면 통과다.
            float reach = SkydiveWindReach.SelfReach(moveSpeed, turnAccel, drop, fallSpeed) + holeHalf;
            return SkydiveWindReach.Shortfall(requiredX, requiredZ, driftX, driftZ, reach);
        }

        private static bool PosturePasses(IReadOnlyList<WindSpec> winds,
                                          float upperY, float lowerY, float drop,
                                          float requiredX, float requiredZ, float holeHalf,
                                          float fallSpeed, float moveSpeed, float turnAccel, float lag)
            => PostureShortfall(winds, upperY, lowerY, drop, requiredX, requiredZ, holeHalf,
                                fallSpeed, moveSpeed, turnAccel, lag) <= 0f;

        // "이 자리에서 저 구멍까지, 바람까지 넣어서 이 자세로 얼마나 모자라나". 0 이하면 닿는다.
        private static float DiveShortfall(IReadOnlyList<WindSpec> winds, float upperY, float lowerY,
                                           Vector2 from, in Hole hole)
            => PostureShortfall(winds, upperY, lowerY, upperY - lowerY,
                                hole.X - from.x, hole.Z - from.y, hole.Half,
                                DiveFallSpeed, DiveMoveSpeed, DiveTurnAccel, DiveWindLag);

        private static float SpreadShortfall(IReadOnlyList<WindSpec> winds, float upperY, float lowerY,
                                             Vector2 from, in Hole hole)
            => PostureShortfall(winds, upperY, lowerY, upperY - lowerY,
                                hole.X - from.x, hole.Z - from.y, hole.Half,
                                SpreadFallSpeed, SpreadMoveSpeed, SpreadTurnAccel, SpreadWindLag);

        // 볼륨이 이 구간과 겹치는 세로 길이.
        private static float Overlap(float upperY, float lowerY, in WindSpec spec)
        {
            float top = Mathf.Min(upperY, spec.Center.y + spec.Height * 0.5f);
            float bottom = Mathf.Max(lowerY, spec.Center.y - spec.Height * 0.5f);
            return Mathf.Max(0f, top - bottom);
        }

        // 밴드 바닥에서 구간 바닥까지 — 볼륨을 벗어난 뒤 바람이 빠지는 동안 밀 수 있는 여유.
        // 다음 구간으로 넘어가는 몫은 Overlap과 같은 방식으로 이 구간 경계에서 잘린다.
        private static float TailHeight(float lowerY, in WindSpec spec)
        {
            float bandBottom = Mathf.Max(lowerY, spec.Center.y - spec.Height * 0.5f);
            return bandBottom - lowerY;
        }

        // 구멍을 얼마나 촘촘히 훑을지. 한 변을 이만큼 나눈다.
        private const int GateGridSteps = 12;
        // 몇 틱까지 봐야 "언젠가 열린다"를 말할 수 있나. 표의 가장 긴 주기보다 넉넉히 크게.
        private const int GateSampleTicks = 240;
        // 통과하려면 몸이 들어갈 자리가 있어야 한다.
        // internal — TbSkydiveConfig와 값이 같은지 EditMode 테스트가 대조한다(SkydiveWindLagConsistencyTests).
        internal const float BodyRadiusForGateCheck = 0.4f;
        // 문 크러시 판정은 몸을 선 캡슐로 본다 — 키를 알아야 세로 겹침을 잰다.
        // internal — TbSkydiveConfig와 값이 같은지 EditMode 테스트가 대조한다(SkydiveWindLagConsistencyTests).
        internal const float BodyHeightForCrushCheck = 1.8f;

        /// <summary>
        /// 어느 선반의 구멍이 <b>한 번도 안 열리면</b> 그 설명을, 다 열리면 null을 준다.
        /// 이걸 놓치면 에러 하나 없이 판이 안 끝난다.
        /// </summary>
        internal static string FindBlockedGate() => FindBlockedGate(Lasers);

        internal static string FindBlockedGate(IReadOnlyList<LaserSpec> lasers)
        {
            for (int i = 0; i < Shelves.Length; i++)
            {
                Shelf shelf = Shelves[i];
                // 이 구멍으로 내려오는 길목 — 바로 위 선반(맨 위는 스폰)부터 이 선반까지.
                // Build()가 기둥을 세울 때 쓰는 것과 같은 관계다(위→아래로 적힌 Shelves 순서에 의존).
                float upperY = i == 0 ? LOP.SkydiveCourseLayout.SpawnY : Shelves[i - 1].Y;

                //  구멍이 둘이어도 각자 따로 본다("하나만 열리면 됨"이 아니다). 스펙 §3.2 ②가
                //  안전한 구멍만으로 완주할 수 있기를 요구하므로 안전한 구멍이 영영 막히면 안
                //  되고, 빠른 구멍이 영영 막히면 갈림길 자체가 없어져 이 슬라이스가 무의미해진다.
                foreach (Hole hole in shelf.Holes)
                {
                    if (GateEverOpens(shelf.Y, hole, upperY, lasers) == false)
                    {
                        return $"선반 {shelf.Y:0}의 구멍({hole.X:0},{hole.Z:0})이 한 번도 열리지 않는다";
                    }
                }
            }
            return null;
        }

        private static bool GateEverOpens(float shelfY, in Hole hole, float upperY, IReadOnlyList<LaserSpec> lasers)
        {
            var beams = new List<LOP.Laser>();
            for (int i = 0; i < lasers.Count; i++)
            {
                // 이 구간(선반~바로 위 선반) 안에 피벗이 있는 레이저만 이 구멍의 문지기다 —
                // 다른 구간의 빔은 여기까지 닿지 않으니 막는지 안 막는지와 무관하다.
                float pivotY = lasers[i].Pivot.y;
                if (pivotY <= shelfY || pivotY > upperY)
                {
                    continue;
                }
                beams.Add(lasers[i].ToLaser());
            }

            for (int tick = 0; tick < GateSampleTicks; tick++)
            {
                ScanHole(hole, beams, tick, out bool anyClear, out _);
                if (anyClear)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 구멍 안을 격자로 훑어, 이 틱에 빔에 <b>닿는 점</b>과 <b>안 닿는 점</b>이 각각 있는지 본다.
        /// 문지기 검사는 "안 닿는 점이 하나라도 있나"(=언젠가 열린다), 안전한 구멍 검사는 "닿는 점이
        /// 하나라도 있나"(=쓸린다)를 묻는다 — 두 물음이 같은 이 한 벌을 쓴다.
        /// </summary>
        private static void ScanHole(in Hole hole, List<LOP.Laser> beams, int tick,
                                     out bool anyClear, out bool anyLit)
        {
            anyClear = false;
            anyLit = false;
            float step = hole.Half * 2f / GateGridSteps;

            for (int ix = 0; ix <= GateGridSteps; ix++)
            {
                for (int iz = 0; iz <= GateGridSteps; iz++)
                {
                    float x = hole.X - hole.Half + ix * step;
                    float z = hole.Z - hole.Half + iz * step;

                    bool lit = false;
                    for (int b = 0; b < beams.Count; b++)
                    {
                        LOP.Laser beam = beams[b];
                        if (LOP.LaserGeometry.Lit(beam, tick) == false)
                        {
                            continue;
                        }
                        LOP.LaserGeometry.SegmentAt(beam, tick, out var a, out var bb);
                        //  빔은 수평이고 낙하는 수직이라, 구멍의 기둥이 막혔는지는 XZ 평면에서
                        //  정해진다. Y를 지우고 재면 3D 루틴을 그대로 다시 쓸 수 있다.
                        var flatPoint = new System.Numerics.Vector3(x, 0f, z);
                        var flatA = new System.Numerics.Vector3(a.X, 0f, a.Z);
                        var flatB = new System.Numerics.Vector3(bb.X, 0f, bb.Z);
                        float d = LOP.LaserSweep.SegmentDistance(flatPoint, flatPoint, flatA, flatB);
                        if (d <= BodyRadiusForGateCheck + beam.Radius)
                        {
                            lit = true;
                            break;
                        }
                    }

                    if (lit)
                    {
                        anyLit = true;
                    }
                    else
                    {
                        anyClear = true;
                    }
                }
            }
        }

        /// <summary>
        /// 판 위에 세운 문지기가 <b>안전한 구멍</b>을 쓸면 그 설명을, 아니면 null을 준다.
        /// 안전한 길의 존재 이유는 "문 타이밍을 못 맞춰도 느리게나마 끝낼 수 있다"(스펙 §3.2 ②)인데,
        /// 그 구멍 위로 빔이 지나가면 안전한 길이 도로 타이밍을 요구한다 — 갈림길이 한쪽으로 기운다.
        /// <see cref="FindBlockedGate"/>는 "언젠가 열리나"만 보므로 이것을 못 본다.
        ///
        /// <para><b>대상은 판 위에 피벗을 둔 빔(문지기)뿐이다.</b> 벽(±<see cref="SlabHalf"/>)에서
        /// 뻗는 격자·쓸기는 구간을 통째로 가로질러 두 구멍을 비슷하게 덮으므로 갈림길을 기울이지
        /// 않는다(재 본 값은 fix2 보고서에 있다 — 여기 적으면 표를 만질 때 같이 안 고쳐진다). 반대로 벽에서
        /// 뻗는 빔을 안전한 구멍에만 겨눠 세우면 이 검사는 못 잡는다 — 그런 표를 쓰게 되면 이 조건을
        /// 다시 볼 것.</para>
        /// </summary>
        internal static string FindLaserOnSafeHole() => FindLaserOnSafeHole(Lasers);

        internal static string FindLaserOnSafeHole(IReadOnlyList<LaserSpec> lasers)
        {
            for (int i = 0; i < Shelves.Length; i++)
            {
                Shelf shelf = Shelves[i];
                float upperY = i == 0 ? LOP.SkydiveCourseLayout.SpawnY : Shelves[i - 1].Y;

                foreach (Hole hole in shelf.Holes)
                {
                    if (hole.HasDoor)
                    {
                        continue;
                    }
                    for (int li = 0; li < lasers.Count; li++)
                    {
                        LaserSpec spec = lasers[li];
                        float pivotY = spec.Pivot.y;
                        if (pivotY <= shelf.Y || pivotY > upperY)
                        {
                            continue;   // 다른 구간의 빔은 이 구멍까지 닿지 않는다
                        }
                        if (Mathf.Abs(spec.Pivot.x) >= SlabHalf || Mathf.Abs(spec.Pivot.z) >= SlabHalf)
                        {
                            continue;   // 벽에서 뻗는 격자·쓸기 — 위 설명 참고
                        }

                        var beams = new List<LOP.Laser> { spec.ToLaser() };
                        for (int tick = 0; tick < GateSampleTicks; tick++)
                        {
                            ScanHole(hole, beams, tick, out _, out bool anyLit);
                            if (anyLit)
                            {
                                return $"{spec.Name}가 선반 {shelf.Y:0}의 안전한 구멍({hole.X:0},{hole.Z:0})을 쓴다 — " +
                                       "안전한 길이 도로 타이밍을 요구하게 된다. 문지기는 빠른 구멍 쪽에 세워라";
                            }
                        }
                    }
                }
            }
            return null;
        }

        // 표 두 개(구멍·문)가 같은 자리를 가리키는지 잴 때의 허용 오차(미터). 좌표는 사람이 손으로
        // 적는 정수라 이보다 가까우면 같은 자리를 뜻한 것이다.
        private const float DoorMatchEpsilon = 0.001f;

        // 각도를 잴 때의 허용 오차(도). 위 상수는 미터용이라 각도에 쓰면 단위가 섞인다 —
        // 회전 검사(FindDoorPanelMismatch)가 쓰는 Quaternion.Angle 허용치와 같은 값이다.
        private const float DoorAngleEpsilon = 0.01f;

        private static bool SameSpot(in DoorSpec door, float shelfY, in Hole hole)
            => Mathf.Abs(door.Center.y - shelfY) < DoorMatchEpsilon
            && Mathf.Abs(door.Center.x - hole.X) < DoorMatchEpsilon
            && Mathf.Abs(door.Center.z - hole.Z) < DoorMatchEpsilon;

        private static bool TryFindHole(IReadOnlyList<Shelf> shelves, in DoorSpec door,
                                        out float shelfY, out Hole hole)
        {
            for (int i = 0; i < shelves.Count; i++)
            {
                foreach (Hole candidate in shelves[i].Holes)
                {
                    if (SameSpot(door, shelves[i].Y, candidate))
                    {
                        shelfY = shelves[i].Y;
                        hole = candidate;
                        return true;
                    }
                }
            }
            shelfY = 0f;
            hole = default;
            return false;
        }

        /// <summary>
        /// 문과 구멍이 1:1로 맞지 않으면 그 설명을, 맞으면 null을 준다. 구멍 표와 문 표가 따로
        /// 있어서 한쪽만 고치면 조용히 어긋나는데, 그러면 <b>빠른 구멍인데 문이 없어</b> 갈림길이
        /// 사라지거나(스펙 §1) <b>안전한 구멍에 문이 붙어</b> 안전한 길이 도로 타이밍을 요구한다
        /// (스펙 §3.2 ②). 둘 다 에러 없이 게임만 달라진다.
        /// </summary>
        internal static string FindDoorHoleMismatch() => FindDoorHoleMismatch(Shelves, Doors);

        internal static string FindDoorHoleMismatch(IReadOnlyList<Shelf> shelves, IReadOnlyList<DoorSpec> doors)
        {
            for (int i = 0; i < shelves.Count; i++)
            {
                Shelf shelf = shelves[i];
                foreach (Hole hole in shelf.Holes)
                {
                    int count = 0;
                    for (int d = 0; d < doors.Count; d++)
                    {
                        if (SameSpot(doors[d], shelf.Y, hole))
                        {
                            count++;
                        }
                    }

                    if (hole.HasDoor && count != 1)
                    {
                        return $"선반 {shelf.Y:0}의 빠른 구멍({hole.X:0},{hole.Z:0})에 문이 {count}개다 — 정확히 하나여야 한다";
                    }
                    if (hole.HasDoor == false && count != 0)
                    {
                        return $"선반 {shelf.Y:0}의 안전한 구멍({hole.X:0},{hole.Z:0})에 문이 있다 — " +
                               "안전한 길은 타이밍 없이 갈 수 있어야 한다";
                    }
                }
            }

            for (int d = 0; d < doors.Count; d++)
            {
                if (TryFindHole(shelves, doors[d], out _, out _) == false)
                {
                    return $"{doors[d].Name}가 어떤 구멍 자리에도 없다 — 판 한복판에 벽만 서게 된다";
                }
            }
            return null;
        }

        /// <summary>
        /// 문 치수가 자기 구멍과 안 맞으면 그 설명을, 맞으면 null을 준다.
        ///
        /// <para>닫혔을 때 두 패널이 채우는 것은 문 로컬 x <c>[-HalfWidth, +HalfWidth]</c> ×
        /// z <c>[-HalfDepth, +HalfDepth]</c>다. 구멍은 반폭 <c>Half</c>의 정사각형이므로
        /// <b>둘 다 <c>Half</c>와 같아야</b> 딱 덮인다 — 크면 판을 침범하고, 작으면 닫혀 있는데
        /// 옆으로 빠져나갈 수 있다.</para>
        ///
        /// <para>같은 이유로 미끄러지는 축은 <b>90°의 배수</b>여야 한다. 정사각형은 90° 회전에만
        /// 자기 자신으로 돌아오므로, 45°짜리 문은 닫아도 구멍 네 모서리가 뚫린 채로 남는다.</para>
        /// </summary>
        internal static string FindDoorSizeMismatch() => FindDoorSizeMismatch(Shelves, Doors);

        internal static string FindDoorSizeMismatch(IReadOnlyList<Shelf> shelves, IReadOnlyList<DoorSpec> doors)
        {
            for (int d = 0; d < doors.Count; d++)
            {
                DoorSpec door = doors[d];
                if (TryFindHole(shelves, door, out _, out Hole hole) == false)
                {
                    continue;   // 짝이 없는 문은 FindDoorHoleMismatch의 몫이다
                }

                if (Mathf.Abs(door.HalfWidth - hole.Half) > DoorMatchEpsilon)
                {
                    return $"{door.Name}의 HalfWidth({door.HalfWidth:0.##})가 구멍 반폭({hole.Half:0.##})과 다르다 — " +
                           "닫혀도 구멍을 못 덮거나 판을 침범한다";
                }
                if (Mathf.Abs(door.HalfDepth - hole.Half) > DoorMatchEpsilon)
                {
                    return $"{door.Name}의 HalfDepth({door.HalfDepth:0.##})가 구멍 반폭({hole.Half:0.##})과 다르다 — " +
                           "닫혀도 구멍을 못 덮거나 판을 침범한다";
                }

                float nearestMultiple = Mathf.Round(door.AxisAngleDegrees / 90f) * 90f;
                if (Mathf.Abs(door.AxisAngleDegrees - nearestMultiple) > DoorAngleEpsilon)
                {
                    return $"{door.Name}의 미끄러지는 축이 {door.AxisAngleDegrees:0.##}°다 — " +
                           "정사각 구멍이라 90의 배수가 아니면 닫혀도 네 모서리가 뚫린다";
                }
            }
            return null;
        }

        /// <summary>
        /// 완전히 닫히는 구간이 없는 문이 있으면 그 설명을, 다 닫히면 null을 준다.
        ///
        /// <para><c>ClosedTicks</c>가 0 이하면 열림 정도가 0에 닿지 않아 크러시 판정이 영영 안 걸린다 —
        /// 문이 장식이 되고 갈림길이 사라지는데 아무 에러도 안 난다(스펙 §2).</para>
        /// </summary>
        internal static string FindDoorNeverCloses() => FindDoorNeverCloses(Doors);

        internal static string FindDoorNeverCloses(IReadOnlyList<DoorSpec> doors)
        {
            for (int d = 0; d < doors.Count; d++)
            {
                if (doors[d].ClosedTicks <= 0)
                {
                    return $"{doors[d].Name}는 완전히 닫히는 구간이 없다(닫힘 {doors[d].ClosedTicks}틱) — " +
                           "크러시가 영영 안 걸려 문이 장식이 된다";
                }
            }
            return null;
        }

        /// <summary>
        /// 문 패널이 같은 선반의 <b>안전한 구멍</b>을 막으면 그 설명을, 다 비켜 있으면 null을 준다.
        ///
        /// <para>이 태스크가 판 위에 <b>새 벽</b>을 놓았다 — 물러난 패널은 구멍 밖 판 위에 콜라이더로
        /// 남는다. 그 자리가 안전한 구멍 위면 그 구멍이 벽으로 막혀 "안전한 길은 타이밍 없이 갈 수
        /// 있다"(스펙 §3.2 ②)가 깨진다. 닫힌 자세도 함께 재는 것은 두 구멍이 겹치게 적히면 같은 일이
        /// 벌어지기 때문이다.</para>
        ///
        /// <para>패널은 돌아간 상자, 구멍은 축에 정렬된 정사각형이라 XZ 평면에서 분리축 넷(월드 x·z,
        /// 패널의 두 축)으로 가른다. 반치수는 <see cref="LOP.DoorGeometry.PanelOverlaps"/>가 쓰는 것과
        /// 같은 값이다 — 여기서 다른 값을 쓰면 재는 상자와 죽이는 상자가 갈린다.</para>
        /// </summary>
        internal static string FindDoorPanelOnSafeHole() => FindDoorPanelOnSafeHole(Shelves, Doors);

        internal static string FindDoorPanelOnSafeHole(IReadOnlyList<Shelf> shelves, IReadOnlyList<DoorSpec> doors)
        {
            for (int i = 0; i < shelves.Count; i++)
            {
                Shelf shelf = shelves[i];
                foreach (Hole hole in shelf.Holes)
                {
                    if (hole.HasDoor)
                    {
                        continue;
                    }
                    for (int d = 0; d < doors.Count; d++)
                    {
                        if (Mathf.Abs(doors[d].Center.y - shelf.Y) > DoorMatchEpsilon)
                        {
                            continue;   // 다른 선반의 문은 400m 위아래라 이 구멍과 무관하다
                        }

                        LOP.Door door = doors[d].ToDoor();
                        for (int index = 0; index < 2; index++)
                        {
                            for (int k = 0; k < 2; k++)
                            {
                                float openness = k;   // 0 = 닫힘, 1 = 완전히 물러남
                                if (PanelClearsHole(door, index, openness, hole))
                                {
                                    continue;
                                }
                                return $"{doors[d].Name}의 {(k == 0 ? "닫힌" : "물러난")} 패널이 " +
                                       $"선반 {shelf.Y:0}의 안전한 구멍({hole.X:0},{hole.Z:0})을 막는다 — " +
                                       "안전한 길은 타이밍 없이 갈 수 있어야 한다";
                            }
                        }
                    }
                }
            }
            return null;
        }

        // 패널 상자와 구멍 사각형이 XZ에서 떨어져 있나. 축 하나라도 둘을 갈라 놓으면 안 겹친다.
        private static bool PanelClearsHole(in LOP.Door door, int index, float openness, in Hole hole)
        {
            System.Numerics.Vector3 center = LOP.DoorGeometry.PanelCenter(door, index, openness);
            var slide = new Vector2(Mathf.Cos(door.AxisAngle), Mathf.Sin(door.AxisAngle));
            var across = new Vector2(-slide.y, slide.x);
            float halfSlide = door.HalfWidth * 0.5f;    // 패널 하나는 덮는 폭의 절반이다
            var delta = new Vector2(hole.X - center.X, hole.Z - center.Z);

            Vector2[] axes = { new Vector2(1f, 0f), new Vector2(0f, 1f), slide, across };
            for (int a = 0; a < axes.Length; a++)
            {
                Vector2 axis = axes[a];
                float panelReach = halfSlide * Mathf.Abs(Vector2.Dot(slide, axis))
                                 + door.HalfDepth * Mathf.Abs(Vector2.Dot(across, axis));
                float holeReach = hole.Half * (Mathf.Abs(axis.x) + Mathf.Abs(axis.y));
                if (Mathf.Abs(Vector2.Dot(delta, axis)) > panelReach + holeReach)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 부활 지점이 어느 문의 패널 부피와 겹치면 그 설명을, 다 비켜 있으면 null을 준다.
        ///
        /// <para><b>닫힌 자세와 물러난 자세를 둘 다</b> 본다. 닫힌 패널은 구멍을 채우므로
        /// <see cref="FindInvalidRespawn()"/>이 이미 걸러 주지만, <b>물러난</b> 패널은 구멍 밖 판 위에
        /// 콜라이더로 남는다 — 그 자리에 사람을 세우면 벽 속에서 부활한다. 스펙 §3.6이 "체크포인트는
        /// 안전 구역이고 맵이 그것을 보장한다"로 정해 뒀으므로 무적 같은 장치가 아니라 이 검사로 막는다.
        /// </para>
        ///
        /// <para>규격은 <c>SkydiveDoorSystem</c>이 쓰는 캡슐 그대로다(축을 반지름만큼 안으로 당김).
        /// 여러 명이 같이 죽으면 부활 지점 둘레로 흩뿌려지므로 그 반경까지 몸을 부풀려 잰다.</para>
        /// </summary>
        internal static string FindRespawnInDoorPanel() => FindRespawnInDoorPanel(Shelves, Doors);

        internal static string FindRespawnInDoorPanel(IReadOnlyList<Shelf> shelves, IReadOnlyList<DoorSpec> doors)
        {
            float radius = BodyRadiusForGateCheck + LOP.SkydiveRespawn.SpreadRadius;

            for (int i = 0; i < shelves.Count; i++)
            {
                if (LOP.SkydiveCourseLayout.RespawnPoints.TryGetValue(shelves[i].Y, out Vector3 point) == false)
                {
                    continue;   // 표에 아예 없는 것은 FindInvalidRespawn이 잡는다
                }

                var bottom = new System.Numerics.Vector3(
                    point.x, point.y + BodyRadiusForGateCheck, point.z);
                var top = new System.Numerics.Vector3(
                    point.x, point.y + BodyHeightForCrushCheck - BodyRadiusForGateCheck, point.z);

                for (int d = 0; d < doors.Count; d++)
                {
                    LOP.Door door = doors[d].ToDoor();
                    for (int k = 0; k < 2; k++)
                    {
                        float openness = k;   // 0 = 닫힘, 1 = 완전히 물러남
                        if (LOP.DoorGeometry.PanelOverlaps(door, openness, bottom, top, radius) == false)
                        {
                            continue;
                        }
                        return $"{doors[d].Name}의 {(k == 0 ? "닫힌" : "물러난")} 패널이 " +
                               $"선반 {shelves[i].Y:0}의 부활 지점({point.x:0},{point.z:0})과 겹친다 — " +
                               "벽 속에서 부활한다";
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 구운 문 패널이 <b>판정이 보는 상자</b>와 어긋나면 그 설명을, 같으면 null을 준다.
        /// 이 프로젝트는 이미 한 번 "그린 도형과 판정 도형이 달라" 사고를 냈다 — 주석으로 지키지
        /// 않고 구운 결과를 되읽어 대조한다.
        ///
        /// <para>재는 것은 <b>움직이지 않는 것들</b>이다 — 허브 회전, 콜라이더의 유무·크기·회전.
        /// 패널이 매 틱 놓이는 <i>자리</i>는 <c>DoorVolume.Pose</c>가 판정과 같은 식으로 계산하므로
        /// 여기서 또 재면 자기 자신과 비교하는 셈이라 아무것도 못 잡는다.</para>
        ///
        /// <para>표가 아니라 <b>만들어진 오브젝트</b>를 재므로 옛 코스를 지우기 전에는 돌 수 없다.
        /// 잡는 것도 표의 실수가 아니라 굽는 코드의 실수다.</para>
        /// </summary>
        internal static string FindDoorPanelMismatch(LOP.DoorVolume volume, in DoorSpec spec)
        {
            if (volume == null)
            {
                return "문이 없다";
            }
            if (Quaternion.Angle(volume.transform.rotation, Quaternion.identity) > DoorAngleEpsilon)
            {
                return $"{volume.name}의 허브가 돌아가 있다 — 패널 오프셋이 이미 월드 축 기준이라 두 번 꺾인다";
            }

            //  판정이 실제로 쓰는 값과 표를 대조한다. ToDoor가 transform.position을 중심으로 삼으므로,
            //  굽기가 자리를 잘못 넣으면 판정도 그 자리를 따라가 표를 재는 검사 셋이 전부 초록이 된다.
            LOP.Door baked = volume.ToDoor();
            LOP.Door wanted = spec.ToDoor();

            float centerGap = (baked.Center - wanted.Center).Length();
            if (centerGap > DoorMatchEpsilon)
            {
                return $"{volume.name}가 표의 자리({spec.Center.x:0.##},{spec.Center.y:0.##},{spec.Center.z:0.##})에서 " +
                       $"{centerGap:0.###}m 벗어나 있다 — 판정도 이 자리를 따라가므로 표를 재는 검사가 못 본다";
            }

            string drift = FindDoorFieldDrift(baked, wanted);
            if (drift != null)
            {
                return $"{volume.name}의 {drift}가 표와 다르다 — 판정이 표 아닌 값으로 돈다";
            }

            if (volume.PanelA != null && ReferenceEquals(volume.PanelA, volume.PanelB))
            {
                return $"{volume.name}의 두 패널이 같은 오브젝트다 — 한쪽만 움직여 구멍 절반이 영영 열린 채로 남는다";
            }

            var expectedSize = new Vector3(volume.HalfWidth, volume.Thickness, volume.HalfDepth * 2f);
            Quaternion expectedRotation = LOP.DoorVolume.PanelRotation(volume.AxisAngleDegrees);

            for (int index = 0; index < 2; index++)
            {
                Transform panel = index == 0 ? volume.PanelA : volume.PanelB;
                if (panel == null)
                {
                    return $"{volume.name}의 패널 {index}가 비어 있다";
                }

                var box = panel.GetComponent<BoxCollider>();
                if (box == null)
                {
                    return $"{panel.name}에 콜라이더가 없다 — 문은 벽이라 닫히는 동안 밀어내지 못한다";
                }
                if (box.isTrigger)
                {
                    return $"{panel.name}의 콜라이더가 트리거다 — 벽이 아니게 된다";
                }

                Vector3 size = Vector3.Scale(box.size, panel.lossyScale);
                if ((size - expectedSize).magnitude > 0.001f)
                {
                    return $"{panel.name}의 콜라이더 크기 {size}가 판정 상자 {expectedSize}와 다르다";
                }
                if (Quaternion.Angle(panel.rotation, expectedRotation) > DoorAngleEpsilon)
                {
                    return $"{panel.name}의 회전이 미끄러지는 축({volume.AxisAngleDegrees:0.##}도)과 다르다";
                }
            }
            return null;
        }

        // 구운 문과 표가 어느 항목에서 갈리는지. 첫 항목의 이름과 두 값을 준다.
        private static string FindDoorFieldDrift(in LOP.Door baked, in LOP.Door wanted)
        {
            if (Mathf.Abs(baked.HalfWidth - wanted.HalfWidth) > DoorMatchEpsilon)
            {
                return $"HalfWidth({baked.HalfWidth:0.###} ≠ {wanted.HalfWidth:0.###})";
            }
            if (Mathf.Abs(baked.HalfDepth - wanted.HalfDepth) > DoorMatchEpsilon)
            {
                return $"HalfDepth({baked.HalfDepth:0.###} ≠ {wanted.HalfDepth:0.###})";
            }
            if (Mathf.Abs(baked.Thickness - wanted.Thickness) > DoorMatchEpsilon)
            {
                return $"Thickness({baked.Thickness:0.###} ≠ {wanted.Thickness:0.###})";
            }
            if (Mathf.Abs((baked.AxisAngle - wanted.AxisAngle) * Mathf.Rad2Deg) > DoorAngleEpsilon)
            {
                return $"AxisAngle({baked.AxisAngle * Mathf.Rad2Deg:0.##}도 ≠ {wanted.AxisAngle * Mathf.Rad2Deg:0.##}도)";
            }
            if (baked.Period != wanted.Period)
            {
                return $"Period({baked.Period} ≠ {wanted.Period})";
            }
            if (baked.OpenTicks != wanted.OpenTicks)
            {
                return $"OpenTicks({baked.OpenTicks} ≠ {wanted.OpenTicks})";
            }
            if (baked.MoveTicks != wanted.MoveTicks)
            {
                return $"MoveTicks({baked.MoveTicks} ≠ {wanted.MoveTicks})";
            }
            if (baked.Phase != wanted.Phase)
            {
                return $"Phase({baked.Phase} ≠ {wanted.Phase})";
            }
            return null;
        }

        /// <summary>
        /// 부활 지점이 판 밖이거나 구멍 안이면 그 설명을, 다 멀쩡하면 null을 준다.
        /// 검사 대상은 <b>서버가 실제로 쓰는 표</b>(<c>LOP.SkydiveCourseLayout.RespawnPoints</c>)다 —
        /// 빌더 안에 사본을 두고 그걸 검사하면, 정작 사람을 세우는 값은 아무도 안 본 것이 된다.
        /// </summary>
        internal static string FindInvalidRespawn() => FindInvalidRespawn(Shelves);

        internal static string FindInvalidRespawn(IReadOnlyList<Shelf> shelves)
        {
            for (int i = 0; i < shelves.Count; i++)
            {
                Shelf shelf = shelves[i];

                if (LOP.SkydiveCourseLayout.RespawnPoints.TryGetValue(shelf.Y, out Vector3 point) == false)
                {
                    return $"선반 {shelf.Y:0}의 부활 지점이 SkydiveCourseLayout에 없다";
                }

                if (Mathf.Abs(point.x) > SlabHalf || Mathf.Abs(point.z) > SlabHalf)
                {
                    return $"선반 {shelf.Y:0}의 부활 지점이 판 밖이다";
                }

                if (Mathf.Abs(point.y - shelf.Y) > 0.001f)
                {
                    return $"선반 {shelf.Y:0}의 부활 지점 고도가 선반과 다르다";
                }

                //  구멍이 둘이니 어느 쪽 구멍과도 안 겹쳐야 한다 — 하나만 보고 통과시키면
                //  부활 지점이 다른 쪽 구멍 안에 세워질 수 있다.
                foreach (Hole hole in shelf.Holes)
                {
                    bool insideHole = Mathf.Abs(point.x - hole.X) <= hole.Half
                                   && Mathf.Abs(point.z - hole.Z) <= hole.Half;
                    if (insideHole)
                    {
                        return $"선반 {shelf.Y:0}의 부활 지점이 구멍({hole.X:0},{hole.Z:0}) 안이다 — 세우자마자 빠진다";
                    }
                }

                //  기둥에 겹치면 부활한 몸이 기둥에 박힌다.
                bool onPillar = Mathf.Abs(Mathf.Abs(point.x) - PillarOffset) < PillarSide
                             && Mathf.Abs(Mathf.Abs(point.z) - PillarOffset) < PillarSide;
                if (onPillar)
                {
                    return $"선반 {shelf.Y:0}의 부활 지점이 기둥과 겹친다";
                }
            }
            return null;
        }

        /// <summary>
        /// 구멍이 모서리 기둥과 겹치면 그 설명을, 다 비켜 있으면 null을 준다. 기둥이 구멍을
        /// 가로막으면 그 선반은 통과할 수 없는데, 표를 고치는 사람이 기둥 자리를 기억하고
        /// 있으리라 기대할 수 없어서 검사로 둔다.
        /// </summary>
        internal static string FindHoleOnPillar() => FindHoleOnPillar(Shelves);

        internal static string FindHoleOnPillar(IReadOnlyList<Shelf> shelves)
        {
            float pillarHalf = PillarSide * 0.5f;
            for (int i = 0; i < shelves.Count; i++)
            {
                Shelf shelf = shelves[i];
                foreach (Hole hole in shelf.Holes)
                {
                    foreach (float px in new[] { -PillarOffset, PillarOffset })
                    {
                        foreach (float pz in new[] { -PillarOffset, PillarOffset })
                        {
                            if (Mathf.Abs(hole.X - px) < hole.Half + pillarHalf
                             && Mathf.Abs(hole.Z - pz) < hole.Half + pillarHalf)
                            {
                                return $"선반 {shelf.Y:0}의 구멍({hole.X:0},{hole.Z:0})이 기둥({px:0},{pz:0})과 겹친다 — 기둥이 길을 막는다";
                            }
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 빌더의 선반 고도·스폰 고도가 <c>LOP.SkydiveCourseLayout</c>과 어긋나면 그 설명을 준다.
        /// 굽는 쪽과 판정하는 쪽이 다른 코스를 보면 부활이 허공에 사람을 세운다.
        /// 스폰 고도는 이제 빌더가 사본을 갖지 않고 <c>LOP.SkydiveCourseLayout.SpawnY</c>를 직접
        /// 쓰므로(값 자체는 어긋날 수 없다), 여기서는 그 스폰이 첫 선반보다 높은지를 본다 —
        /// 아니면 첫 낙하 구간 자체가 성립하지 않는다.
        /// </summary>
        internal static string FindShelfLayoutDrift()
        {
            var layout = LOP.SkydiveCourseLayout.ShelfYs;
            if (layout.Count != Shelves.Length)
            {
                return $"선반 개수가 다르다 — 빌더 {Shelves.Length}, SkydiveCourseLayout {layout.Count}";
            }
            for (int i = 0; i < Shelves.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < layout.Count; j++)
                {
                    if (Mathf.Abs(layout[j] - Shelves[i].Y) < 0.001f)
                    {
                        found = true;
                        break;
                    }
                }
                if (found == false)
                {
                    return $"선반 {Shelves[i].Y:0}이 SkydiveCourseLayout에 없다";
                }
            }

            if (Shelves.Length > 0 && LOP.SkydiveCourseLayout.SpawnY <= Shelves[0].Y)
            {
                return $"스폰 고도({LOP.SkydiveCourseLayout.SpawnY:0})가 첫 선반({Shelves[0].Y:0})보다 낮거나 같다";
            }
            return null;
        }

        /// <summary>한 틱에 너무 크게 도는 레이저가 있으면 그 설명을, 없으면 null을 준다.</summary>
        internal static string FindTooFastLaser(IReadOnlyList<LaserSpec> lasers)
        {
            for (int i = 0; i < lasers.Count; i++)
            {
                float speed = Mathf.Abs(lasers[i].AngularSpeedDegreesPerTick);
                if (speed > MaxAngularSpeedDegreesPerTick)
                {
                    return $"{lasers[i].Name}가 한 틱에 {speed:0.#}° 돈다 — " +
                           $"{MaxAngularSpeedDegreesPerTick:0.#}°를 넘으면 눈으로 못 읽는다";
                }
            }
            return null;
        }

        /// <summary>
        /// 구멍과 구멍 사이가 실제로 닿는 거리인지, 빠른/안전 구멍이 요구하는 자세가 실제로
        /// 갈리는지 검사한다(2026-09-06-skydive-doors-and-branching-design §3.2).
        /// </summary>
        [MenuItem("LOP/Skydive/코스 검사")]
        public static void VerifyMenu()
        {
            if (Verify(out string report))
            {
                Debug.Log($"[Skydive] 코스 검사\n{report}");
            }
            else
            {
                Debug.LogError($"[Skydive] 코스 검사\n{report}");
            }
        }

        // 구멍이 둘이 되면서 굽기 전 검사가 하나에서 셋으로 늘었다(스펙 §3.2) —
        // ① 각 선반에 도달 가능한 구멍이 있는가(전체 경로) ② 안전한 구멍만으로 완주 가능한가
        // ③ 빠른 구멍은 다이브로만, 안전한 구멍은 다이브로는 안 닿는가.
        private static bool Verify(out string report)
        {
            var lines = new List<string>();
            bool ok = true;

            bool generalOk = ReachableChain(safeOnly: false, out string generalReport);
            ok &= generalOk;
            lines.Add("① 전체 경로(빠른/안전 구멍 아무거나):");
            lines.Add(generalReport);

            bool safeOk = ReachableChain(safeOnly: true, out string safeReport);
            ok &= safeOk;
            lines.Add("② 안전 경로만(문을 하나도 못 뚫어도 끝까지):");
            lines.Add(safeReport);

            string splitIssue = FindRouteNotSplit();
            if (splitIssue != null)
            {
                ok = false;
                lines.Add($"③  [X] {splitIssue}");
            }
            else
            {
                lines.Add("③  빠른 구멍은 다이브로만, 안전한 구멍은 다이브로 안 닿는다 — 통과.");
            }

            var sb = new StringBuilder();
            sb.AppendLine(ok ? "통과 가능한 코스다." : "통과 불가능한 구간이 있다.");
            foreach (string line in lines)
            {
                sb.AppendLine(line);
            }
            report = sb.ToString();
            return ok;
        }

        // 한 선반에서 구멍 하나를 잰 결과. 값은 "얼마나 모자라나"(부족)다 — 0 이하면 닿는다.
        // 앞 선반에서 실제로 갈 수 있었던 자리들 중 가장 잘 닿는 자리 기준.
        private readonly struct HoleStep
        {
            public readonly Hole Hole;
            public readonly float SpreadShortfall;
            public readonly float DiveShortfall;

            public HoleStep(in Hole hole, float spreadShortfall, float diveShortfall)
            {
                Hole = hole;
                SpreadShortfall = spreadShortfall;
                DiveShortfall = diveShortfall;
            }

            //  어느 자세로든 닿으면 간 것이다 — 바람이 있으면 대자가 더 밀려 다이브만 닿는 자리도 생긴다.
            public bool Reached => SpreadShortfall <= 0f || DiveShortfall <= 0f;
        }

        // 선반 하나를 지나는 한 걸음.
        private readonly struct ShelfStep
        {
            public readonly Shelf Shelf;
            public readonly List<HoleStep> Holes;      // 이번 선반에서 볼 구멍들(safeOnly 필터 적용 후)
            public readonly List<Vector2> DeadEnds;    // 앞 선반 자리 중 어느 구멍에도 못 닿은 자리

            public ShelfStep(in Shelf shelf, List<HoleStep> holes, List<Vector2> deadEnds)
            {
                Shelf = shelf;
                Holes = holes;
                DeadEnds = deadEnds;
            }

            public bool AnyReached
            {
                get
                {
                    foreach (HoleStep h in Holes)
                    {
                        if (h.Reached)
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
        }

        //  검사 ①과 ③이 같은 이 한 벌로 사슬을 굴린다. 두 벌로 두면 한쪽만 느슨해진다 —
        //  실제로 그랬다: 한쪽은 도달 가능한 구멍만 다음 칸 출발점으로 삼았는데 다른 쪽은
        //  도달 불가능한 구멍까지 출발점으로 삼아, 갈 수 없는 자리에서 재고 통과시켰다.
        //  규칙은 "어딘가에서 닿으면 됨"이 아니라 "실제로 갈 수 있었던 자리에서 닿아야 함"이다.
        private static List<ShelfStep> WalkShelves(bool safeOnly, IReadOnlyList<Shelf> shelves,
                                                  IReadOnlyList<WindSpec> winds)
        {
            var steps = new List<ShelfStep>();

            //  출발은 스폰 한 점이다.
            var from = new List<Vector2> { new Vector2(0f, 0f) };
            float previousY = LOP.SkydiveCourseLayout.SpawnY;

            foreach (Shelf shelf in shelves)
            {
                var holes = new List<HoleStep>();
                foreach (Hole hole in shelf.Holes)
                {
                    if (safeOnly && hole.HasDoor)
                    {
                        continue;   // ② 문을 하나도 못 뚫는 사람의 경로
                    }
                    float spread = float.MaxValue;
                    float dive = float.MaxValue;
                    foreach (Vector2 p in from)
                    {
                        spread = Mathf.Min(spread, SpreadShortfall(winds, previousY, shelf.Y, p, hole));
                        dive = Mathf.Min(dive, DiveShortfall(winds, previousY, shelf.Y, p, hole));
                    }
                    holes.Add(new HoleStep(hole, spread, dive));
                }

                //  스펙 §3.2 ①은 "각 구멍에서 적어도 하나의 다음 구멍에 닿는가"다. 앞 선반의
                //  구멍 하나하나가 각각 다음 칸을 가져야 한다 — 그중 누군가에게서만 닿으면
                //  나머지로 내려간 사람은 갇히는데 검사는 초록이 된다.
                var deadEnds = new List<Vector2>();
                foreach (Vector2 p in from)
                {
                    bool any = false;
                    foreach (HoleStep h in holes)
                    {
                        if (SpreadShortfall(winds, previousY, shelf.Y, p, h.Hole) <= 0f
                         || DiveShortfall(winds, previousY, shelf.Y, p, h.Hole) <= 0f)
                        {
                            any = true;
                            break;
                        }
                    }
                    if (any == false)
                    {
                        deadEnds.Add(p);
                    }
                }

                steps.Add(new ShelfStep(shelf, holes, deadEnds));

                var next = new List<Vector2>();
                foreach (HoleStep h in holes)
                {
                    if (h.Reached)
                    {
                        next.Add(new Vector2(h.Hole.X, h.Hole.Z));
                    }
                }
                if (next.Count == 0)
                {
                    break;   // 더 내려갈 수 없다 — 이 걸음까지만 보고한다
                }
                from = next;
                previousY = shelf.Y;
            }

            return steps;
        }

        // 부족(+)이면 그만큼 모자라고, 음수면 그만큼 남는다. 리포트는 판정과 같은 값을 적는다.
        private static string Margin(float shortfall)
            => shortfall <= 0f ? $"여유 {-shortfall:0.0}m" : $"부족 {shortfall:0.0}m";

        internal static bool ReachableChain(bool safeOnly, out string report)
            => ReachableChain(safeOnly, Shelves, Winds, out report);

        internal static bool ReachableChain(bool safeOnly, IReadOnlyList<Shelf> shelves,
                                            IReadOnlyList<WindSpec> winds, out string report)
        {
            var lines = new List<string>();
            bool ok = true;

            foreach (ShelfStep step in WalkShelves(safeOnly, shelves, winds))
            {
                if (step.Holes.Count == 0)
                {
                    lines.Add(safeOnly
                        ? $"  [X] y={step.Shelf.Y:0}에 안전한 구멍이 아예 없다"
                        : $"  [X] y={step.Shelf.Y:0}에 구멍이 하나도 없다");
                    ok = false;
                    break;
                }

                foreach (HoleStep h in step.Holes)
                {
                    //  판정과 같은 값을 그대로 적는다. 무풍 다이브 사거리를 적었을 때는 리포트가
                    //  ③의 판정과 반대되는 숫자를 찍어("안전한 구멍이 다이브로 닿는다"로 읽힌다)
                    //  이 슬라이스가 되잡으려던 오해를 리포트가 다시 만들었다.
                    lines.Add($"{(h.Reached ? "     " : "  [X]")} y={step.Shelf.Y:0} " +
                              $"{(h.Hole.HasDoor ? "빠른" : "안전")}({h.Hole.X:0},{h.Hole.Z:0}): " +
                              $"대자 {Margin(h.SpreadShortfall)} / 다이브 {Margin(h.DiveShortfall)}");
                }

                foreach (Vector2 p in step.DeadEnds)
                {
                    lines.Add($"  [X] y={step.Shelf.Y:0}: 앞 구멍({p.x:0},{p.y:0})에서 닿는 구멍이 하나도 없다 — 막다른 길");
                    ok = false;
                }

                if (step.AnyReached == false)
                {
                    lines.Add($"  [X] y={step.Shelf.Y:0}에 닿는 구멍이 없다{(safeOnly ? " (안전 경로)" : "")}");
                    ok = false;
                    break;
                }
            }

            report = string.Join("\n", lines);
            return ok;
        }

        //  ③ 두 길이 실제로 다른 자세를 요구하는가. 이 조건이 성립하면 "빠른 길이 진짜 빠른가"를
        //  따로 증명할 필요가 없다 — 다이브로 갈 수 있다는 것 자체가 더 빠르다는 뜻이다.
        //  바람을 반드시 함께 넣는다: 순풍이 미는 자리에 안전한 구멍을 두면 다이브가 공짜로
        //  실려 가 도달해 버려, 무풍으로만 재면 성질이 깨진 표가 초록으로 통과한다.
        internal static string FindRouteNotSplit() => FindRouteNotSplit(Shelves, Winds);

        internal static string FindRouteNotSplit(IReadOnlyList<Shelf> shelves, IReadOnlyList<WindSpec> winds)
        {
            foreach (ShelfStep step in WalkShelves(safeOnly: false, shelves, winds))
            {
                foreach (HoleStep h in step.Holes)
                {
                    //  가장 잘 닿는 자리로 잰다. 빠른 구멍은 "어느 자리에서든 다이브로 갈 수
                    //  있으면 된다", 안전한 구멍은 "어느 자리에서도 다이브로는 못 간다"라서
                    //  둘 다 최솟값이 기준이 된다 — HoleStep이 이미 그 최솟값을 들고 있다.
                    float shortfall = h.DiveShortfall;

                    if (h.Hole.HasDoor && shortfall > 0f)
                    {
                        return $"y={step.Shelf.Y:0}의 빠른 구멍({h.Hole.X:0},{h.Hole.Z:0})이 " +
                               $"다이브로 안 닿는다({shortfall:0.0}m 모자란다)";
                    }
                    if (h.Hole.HasDoor == false && shortfall <= 0f)
                    {
                        return $"y={step.Shelf.Y:0}의 안전한 구멍({h.Hole.X:0},{h.Hole.Z:0})이 " +
                               $"다이브로도 닿는다({-shortfall:0.0}m 여유) — 문이 무의미해진다";
                    }
                }
            }
            return null;
        }
    }
}
