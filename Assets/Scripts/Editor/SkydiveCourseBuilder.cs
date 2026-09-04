using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 열려 있는 <c>SkydiveMap</c> 씬에 낙하 코스를 굽는다 — 구멍 하나씩 뚫린 선반과, 속도감을
    /// 보여 주는 모서리 기둥.
    ///
    /// 표(<c>Shelves</c>)가 곧 코스 설계다. 숫자를 고치고 다시 구우면 되고, 리뷰어는 표만 보면 된다.
    /// 굽기 전에 <see cref="SkydiveReach"/>로 구멍 사이가 실제로 닿는 거리인지 검사한다.
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
        private const float PillarOffset = 60f;   // 구멍 가장자리 최대 37보다 멀어 길을 막지 않는다

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

        // 화살표 밀도. 개수는 부피(반지름×높이)에 비례한다 — 세기가 아니라 "큰 볼륨에서
        // 성기지 않게"를 위한 값이다. 반지름25×높이120(작은 기둥)에서 14개가 나오게 골랐다.
        private const float ArrowCountDivisor = 250f;
        private const int ArrowCountMin = 14;
        private const int ArrowCountMax = 200;

        private readonly struct Shelf
        {
            public readonly float Y;
            public readonly float HoleX;
            public readonly float HoleZ;
            public readonly float HoleHalf;

            public Shelf(float y, float holeX, float holeZ, float holeSide)
            {
                Y = y;
                HoleX = holeX;
                HoleZ = holeZ;
                HoleHalf = holeSide * 0.5f;
            }
        }

        // 코스 설계 그 자체. 위에서 아래 순서로 적는다.
        // 부활 지점은 여기 없다 — 서버가 그 값으로 사람을 세우므로 LOP.SkydiveCourseLayout이
        // 진실원본이고, 여기서 또 적으면 두 곳이 조용히 어긋난다.
        private static readonly Shelf[] Shelves =
        {
            new Shelf(2600f, 0f, 0f, 30f),      // 스폰 바로 아래 — 아무것도 안 해도 지나간다
            new Shelf(2200f, 30f, 0f, 24f),     // 옆으로 가는 걸 가르치는 구간(다이브로도 닿는다)
            new Shelf(1800f, 30f, 30f, 20f),
            new Shelf(1400f, -25f, 30f, 20f),   // 여기부터 넷은 다이브로 곧장 가면 못 닿는다
            new Shelf(1000f, -25f, -30f, 16f),
            new Shelf(600f, 30f, -25f, 16f),
            new Shelf(200f, 0f, 25f, 16f),
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

            // 마지막 구멍(0,25) 위의 기둥 — 착지를 패러세일로 때우지 못하게 한다.
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
        // 문지기는 구멍 중심을 피벗으로 삼아 구멍 위를 쓸고, 격자는 통로를 가로지른다.
        internal static readonly LaserSpec[] Lasers =
        {
            // 2600 위: 없음 — 조작을 익히는 자리

            // 2200 구멍(30,0) 문지기 — 느린 회전. 반대편으로 들어가면 된다.
            new LaserSpec("Laser_2200_Gate", new Vector3(30f, 2215f, 0f),
                          length: 26f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 4f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),

            // 1800 구멍(30,30) 문지기 — 왕복. 오는 것이 보인다.
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

            // 1400 구멍(-25,30) 문지기 + 통로 격자
            new LaserSpec("Laser_1400_Gate", new Vector3(-25f, 1415f, 30f),
                          length: 22f, radius: 0.6f,
                          startAngleDegrees: 45f, angularSpeedDegreesPerTick: 7f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            new LaserSpec("Laser_1250_Bar", new Vector3(-100f, 1250f, -20f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),

            // 1000 구멍(-25,-30) 문지기 + 점멸 격자(리듬)
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

            // 600 구멍(30,-25) 문지기 — 빠르게
            new LaserSpec("Laser_600_Gate", new Vector3(30f, 615f, -25f),
                          length: 22f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 11f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            new LaserSpec("Laser_450_Blink", new Vector3(100f, 450f, -40f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 180f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 30, onTicks: 15, phase: 0),

            // 200 구멍(0,25) — 셋을 합친다
            new LaserSpec("Laser_200_Gate", new Vector3(0f, 215f, 25f),
                          length: 22f, radius: 0.6f,
                          startAngleDegrees: 120f, angularSpeedDegreesPerTick: 12f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            new LaserSpec("Laser_320_Sweep", new Vector3(-100f, 320f, 0f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 5f,
                          sweepHalfRangeDegrees: 40f, period: 0, onTicks: 0, phase: 0),
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

            string blockedGate = FindBlockedGate();
            if (blockedGate != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {blockedGate}. 씬은 바뀌지 않았다.");
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

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Skydive] 코스를 구웠다 — 선반 {Shelves.Length}개. 씬을 저장해라.\n{report}");
        }

        // 선반 = 구멍을 둘러싼 판 네 장. 하나의 큰 판에 구멍을 뚫을 수는 없어서(상자 콜라이더는
        // 볼록한 덩어리뿐) 테두리로 만든다.
        private static void BuildShelf(Transform parent, in Shelf shelf, Material material)
        {
            float northStart = shelf.HoleZ + shelf.HoleHalf;
            float southEnd = shelf.HoleZ - shelf.HoleHalf;
            float eastStart = shelf.HoleX + shelf.HoleHalf;
            float westEnd = shelf.HoleX - shelf.HoleHalf;

            string prefix = $"Shelf_{shelf.Y:0}";
            AddBox(parent, $"{prefix}_N", material,
                new Vector3(0f, shelf.Y, (northStart + SlabHalf) * 0.5f),
                new Vector3(SlabHalf * 2f, SlabThickness, SlabHalf - northStart));
            AddBox(parent, $"{prefix}_S", material,
                new Vector3(0f, shelf.Y, (southEnd - SlabHalf) * 0.5f),
                new Vector3(SlabHalf * 2f, SlabThickness, southEnd + SlabHalf));
            AddBox(parent, $"{prefix}_E", material,
                new Vector3((eastStart + SlabHalf) * 0.5f, shelf.Y, shelf.HoleZ),
                new Vector3(SlabHalf - eastStart, SlabThickness, shelf.HoleHalf * 2f));
            AddBox(parent, $"{prefix}_W", material,
                new Vector3((westEnd - SlabHalf) * 0.5f, shelf.Y, shelf.HoleZ),
                new Vector3(westEnd + SlabHalf, SlabThickness, shelf.HoleHalf * 2f));
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
        {
            for (int i = 1; i < Shelves.Length; i++)
            {
                float upperY = Shelves[i - 1].Y;
                float lowerY = Shelves[i].Y;
                float drop = upperY - lowerY;
                float requiredX = Shelves[i].HoleX - Shelves[i - 1].HoleX;
                float requiredZ = Shelves[i].HoleZ - Shelves[i - 1].HoleZ;
                float holeHalf = Shelves[i].HoleHalf;

                if (PosturePasses(winds, upperY, lowerY, drop, requiredX, requiredZ, holeHalf,
                                  SpreadFallSpeed, SpreadMoveSpeed, SpreadTurnAccel, SpreadWindLag) ||
                    PosturePasses(winds, upperY, lowerY, drop, requiredX, requiredZ, holeHalf,
                                  DiveFallSpeed, DiveMoveSpeed, DiveTurnAccel, DiveWindLag))
                {
                    continue;
                }

                return $"{upperY:0} → {lowerY:0} 구간을 대자로도 다이브로도 못 지나간다. 바람 표를 고쳐라.";
            }
            return null;
        }

        private static bool PosturePasses(IReadOnlyList<WindSpec> winds,
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

            // 구멍 반쪽만큼은 덤이다 — Verify()와 같은 셈(§7.4). 중심까지 안 가도 가장자리로
            // 들어가면 통과다.
            float reach = SkydiveWindReach.SelfReach(moveSpeed, turnAccel, drop, fallSpeed) + holeHalf;
            return SkydiveWindReach.CanReach(requiredX, requiredZ, driftX, driftZ, reach);
        }

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
                if (GateEverOpens(shelf, upperY, lasers) == false)
                {
                    return $"선반 {shelf.Y:0}의 구멍이 한 번도 열리지 않는다";
                }
            }
            return null;
        }

        private static bool GateEverOpens(in Shelf shelf, float upperY, IReadOnlyList<LaserSpec> lasers)
        {
            var beams = new List<LOP.Laser>();
            for (int i = 0; i < lasers.Count; i++)
            {
                // 이 구간(선반~바로 위 선반) 안에 피벗이 있는 레이저만 이 구멍의 문지기다 —
                // 다른 구간의 빔은 여기까지 닿지 않으니 막는지 안 막는지와 무관하다.
                float pivotY = lasers[i].Pivot.y;
                if (pivotY <= shelf.Y || pivotY > upperY)
                {
                    continue;
                }
                beams.Add(lasers[i].ToLaser());
            }

            for (int tick = 0; tick < GateSampleTicks; tick++)
            {
                if (HoleHasClearPoint(shelf, beams, tick))
                {
                    return true;
                }
            }
            return false;
        }

        // 구멍 안을 격자로 훑어, 켜져 있는 모든 빔에서 충분히 떨어진 점이 하나라도 있으면 열린 것이다.
        private static bool HoleHasClearPoint(in Shelf shelf, List<LOP.Laser> beams, int tick)
        {
            float step = shelf.HoleHalf * 2f / GateGridSteps;

            for (int ix = 0; ix <= GateGridSteps; ix++)
            {
                for (int iz = 0; iz <= GateGridSteps; iz++)
                {
                    float x = shelf.HoleX - shelf.HoleHalf + ix * step;
                    float z = shelf.HoleZ - shelf.HoleHalf + iz * step;
                    var point = new System.Numerics.Vector3(x, shelf.Y, z);

                    bool clear = true;
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
                        var flatPoint = new System.Numerics.Vector3(point.X, 0f, point.Z);
                        var flatA = new System.Numerics.Vector3(a.X, 0f, a.Z);
                        var flatB = new System.Numerics.Vector3(bb.X, 0f, bb.Z);
                        float d = LOP.LaserSweep.SegmentDistance(flatPoint, flatPoint, flatA, flatB);
                        if (d <= BodyRadiusForGateCheck + beam.Radius)
                        {
                            clear = false;
                            break;
                        }
                    }
                    if (clear)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 부활 지점이 판 밖이거나 구멍 안이면 그 설명을, 다 멀쩡하면 null을 준다.
        /// 검사 대상은 <b>서버가 실제로 쓰는 표</b>(<c>LOP.SkydiveCourseLayout.RespawnPoints</c>)다 —
        /// 빌더 안에 사본을 두고 그걸 검사하면, 정작 사람을 세우는 값은 아무도 안 본 것이 된다.
        /// </summary>
        internal static string FindInvalidRespawn()
        {
            for (int i = 0; i < Shelves.Length; i++)
            {
                Shelf shelf = Shelves[i];

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

                bool insideHole = Mathf.Abs(point.x - shelf.HoleX) <= shelf.HoleHalf
                               && Mathf.Abs(point.z - shelf.HoleZ) <= shelf.HoleHalf;
                if (insideHole)
                {
                    return $"선반 {shelf.Y:0}의 부활 지점이 구멍 안이다 — 세우자마자 빠진다";
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

        /// <summary>구멍과 구멍 사이가 실제로 닿는 거리인지 검사한다(스펙 §7.4).</summary>
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

        private static bool Verify(out string report)
        {
            var lines = new List<string>();
            bool ok = true;

            float previousX = 0f;
            float previousZ = 0f;
            float previousY = LOP.SkydiveCourseLayout.SpawnY;

            foreach (var shelf in Shelves)
            {
                float fall = previousY - shelf.Y;
                float gap = new Vector2(shelf.HoleX - previousX, shelf.HoleZ - previousZ).magnitude;
                float spread = SkydiveReach.MaxHorizontal(fall, SpreadFallSpeed, SpreadMoveSpeed, SpreadTurnAccel);
                float dive = SkydiveReach.MaxHorizontal(fall, DiveFallSpeed, DiveMoveSpeed, DiveTurnAccel);

                //  구멍 반쪽만큼은 덤이다 — 중심까지 안 가도 가장자리로 들어가면 통과다.
                bool reachable = gap <= spread + shelf.HoleHalf;
                ok &= reachable;

                string note = reachable
                    ? (gap > dive + shelf.HoleHalf ? "  (다이브로는 못 닿음 — 의도)" : "")
                    : "  [X] 대자로도 못 닿는다";
                lines.Add($"y={shelf.Y:0}: 이동 {gap:0.0}m / 대자 {spread:0.0}m / 다이브 {dive:0.0}m{note}");

                previousX = shelf.HoleX;
                previousZ = shelf.HoleZ;
                previousY = shelf.Y;
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
    }
}
