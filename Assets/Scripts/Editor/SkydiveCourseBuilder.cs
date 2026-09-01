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

        // 스폰 고도. 첫 선반까지의 거리를 검사할 때 쓴다.
        // 젤다와 같은 종단속도(60)로 올리면서 코스도 3000m로 늘렸다 — 1000m는 17초면 끝난다.
        private const float SpawnY = 3000f;

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

            var scene = EditorSceneManager.GetActiveScene();
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Scenes/floor.mat");

            GameObject root = GameObject.Find(CourseRootName);
            if (root != null)
            {
                Object.DestroyImmediate(root);   // 다시 구울 때 옛 코스가 겹쳐 남지 않게
            }
            root = new GameObject(CourseRootName);

            for (int i = 0; i < Shelves.Length; i++)
            {
                BuildShelf(root.transform, Shelves[i], material);
                float upperY = i == 0 ? SpawnY : Shelves[i - 1].Y;
                BuildPillars(root.transform, Shelves[i].Y, upperY, material);
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
            float previousY = SpawnY;

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
