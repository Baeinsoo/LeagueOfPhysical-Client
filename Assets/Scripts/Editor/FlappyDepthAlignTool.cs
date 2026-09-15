using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 장애물의 <b>보이는 면</b>을 판정면(z=0) 앞쪽으로 당긴다. 두께는 그대로 두고 뻗는 방향만
    /// 카메라 쪽으로 바꾼다. <b>콜라이더는 한 톨도 안 움직인다</b> — 트랜스폼을 옮긴 만큼
    /// <c>BoxCollider.center</c>를 반대로 밀어 월드 위치를 제자리에 고정한다.
    ///
    /// <para><b>왜 하나.</b> 원근 카메라는 판정면보다 <b>뒤</b>에 있는 면을 소실점 쪽으로 당겨
    /// 그린다. 그래서 뒤로 뻗은 두께만큼 틈이 화면에서 좁아 보인다 — 화면 가운데서 26cm,
    /// 가장자리에서 44cm. 통과 여유가 3~5cm뿐인 맵이라 눈으로는 못 지나갈 틈처럼 보인다.
    /// 보이는 면이 전부 판정면 앞으로 오면 가장 안쪽 실루엣이 곧 판정 단면이라 그 왜곡이 사라진다.</para>
    ///
    /// <para><b>왜 콜라이더가 아니라 렌더러를 옮기나.</b> 처음엔 콜라이더째로 옮겼는데, 그러면
    /// 매 틱 도는 밀어내기(<c>Depenetrate</c>)의 <b>최소 탈출 방향이 바뀐다</b>. 박스가
    /// z [-1.25, +1.25]일 때는 z로 빠져나가는 값이 1.70m라 절대 최소가 아니어서 밀어내기가 늘
    /// 옆으로 났는데, z [-2.5, 0]으로 옮기면 뒷면으로 0.45m면 빠져나가서 깊게 파묻힌 새가
    /// 옆이 아니라 <b>+z로</b> 밀린다. 그 z는 곧바로 0으로 다시 물리므로 겹침이 안 풀린다.
    /// 맵 검사에는 밀어내기 단계가 없어 이 차이를 <b>구조적으로 못 본다</b> — 검사가 초록이라고
    /// 안전한 게 아니다. 렌더러만 옮기면 이 문제가 <i>생길 수가 없다</i>: 콜라이더 월드
    /// bounds가 그대로면 물리에서 달라질 것이 없다. 게임플레이 중립이 <i>주장</i>이 아니라
    /// <i>정의</i>가 된다.</para>
    ///
    /// <para><b>어느 것을 볼지·두께를 어떻게 셀지는 여기서 정하지 않는다</b> — 그 규칙은
    /// <see cref="LOP.MapTools.BlockDepthScan"/>에 한 벌만 두고, 재는 쪽
    /// (<see cref="FlappyMapPlayabilityCheck"/>)과 고치는 쪽(여기)이 같은 것을 부른다. 규칙이
    /// 둘로 갈라지면 서로 다른 것을 보면서 맞췄다고 착각한다.</para>
    /// </summary>
    public static class FlappyDepthAlignTool
    {
        //  맵 지형 씬. 게임 씬으로 열면 맵 도구가 대화상자를 띄워 에디터 메인 스레드가 멎는다.
        private const string MapScenePath = "Assets/Art/Scenes/FlappyRaceMap.unity";

        //  새의 몸 반지름 — 판정에 관여하는 z대역 [-r, +r]을 정한다. 맵 검사가 MasterData에서
        //  읽는 값과 같다(TbFlappyConfig.BodyRadius = 0.45).
        private const float BodyRadius = 0.45f;

        //  이만큼까지는 맞은 것으로 본다. 부동소수점 찌꺼기로 무한히 다시 옮기지 않기 위해서다.
        private const float Tolerance = 0.01f;

        //  콜라이더가 제자리인지 볼 때 쓰는 눈. 정확히 같기를 요구할 수는 없다 — 회전한 부모를
        //  거쳐 월드 좌표를 쓰고 되읽으면 float 왕복 찌꺼기가 수 마이크로미터 남는다(실측 4e-06).
        private const float BoundsEpsilon = 1e-4f;

        [MenuItem("LOP/Debug/Flappy 판정면 정렬")]
        public static void Align() => Run();

        /// <summary>사람 없이 도는 백그라운드 잡(<c>unity cmd eval_file --detach</c>)에서 부른다.</summary>
        public static void RunHeadless() => Run();

        //  옮기기 전 콜라이더 하나의 자리. 트랜프롬 위치까지 같이 적는 이유는 <b>꺼진</b>
        //  콜라이더 때문이다 — 꺼진 콜라이더의 bounds는 원점의 크기 0짜리라 실제로 끌려가도
        //  안 움직인 것처럼 보인다. 트랜스폼 위치는 켜짐과 무관하게 진실을 말한다.
        private readonly struct ColliderAnchor
        {
            public readonly Collider Collider;
            public readonly Vector3 TransformPosition;
            public readonly Bounds Bounds;
            public readonly bool BoundsMeaningful;

            public ColliderAnchor(Collider collider)
            {
                Collider = collider;
                TransformPosition = collider.transform.position;
                Bounds = collider.bounds;
                BoundsMeaningful = collider.enabled;
            }
        }

        private static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                MapScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            Physics.SyncTransforms();

            int mapMask = LayerMask.GetMask("Default");
            var moved = new List<string>();
            var skipped = new List<string>();
            var failures = new List<string>();
            int alreadyAligned = 0;
            int background = 0;

            //  ── 0. 옮기기 전 콜라이더 전부의 자리를 적어 둔다 ───────────────────────────
            //  레이어·트리거를 안 가린다. 지킬 불변식이 "판정 블록의 콜라이더가 안 움직인다"가
            //  아니라 <b>"씬의 어떤 콜라이더도 안 움직인다"</b>이기 때문이다 — 결승선 트리거가
            //  딸려 가는 것도 똑같이 게임플레이 변경이다.
            var anchors = new List<ColliderAnchor>();
            foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                anchors.Add(new ColliderAnchor(collider));
            }

            //  ── 1. 렌더러를 앞으로 당긴다 ────────────────────────────────────────────
            //  조상을 먼저 옮긴다. 자식을 먼저 맞춰 놓고 나중에 그 부모를 옮기면 자식이 딸려가
            //  다시 어긋난다 — 순서에 결과가 걸리지 않게 깊이 오름차순으로 돈다.
            var renderers = new List<Renderer>(Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None));
            renderers.Sort((a, b) => Depth(a.transform).CompareTo(Depth(b.transform)));

            var movedTransforms = new List<(Transform Transform, Vector3 Position)>();

            foreach (var renderer in renderers)
            {
                if ((mapMask & (1 << renderer.gameObject.layer)) == 0)
                {
                    continue;
                }
                //  안 그려지는 면은 틈을 좁아 보이게 할 수 없다.
                if (renderer.enabled == false)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                if (LOP.MapTools.BlockDepthScan.IsGameplayBlock(bounds, BodyRadius) == false)
                {
                    background++;
                    continue;   // 배경 — 새가 지나는 z대역 밖이라 이 틈과 상관없다
                }

                float backDepth = LOP.MapTools.BlockDepthScan.BackDepth(bounds);
                if (backDepth <= Tolerance)
                {
                    alreadyAligned++;
                    continue;
                }

                //  <b>틈을 만드는 면만 옮긴다</b> — 뒤를 단단한(트리거 아닌) BoxCollider가 받치는 것.
                //  코인·결승선 배너처럼 그려지기만 하는 것은 틈의 가장자리가 아니라, 뒤로 뻗어
                //  있어도 지나갈 틈을 잘못 보이게 할 수가 없다. 옮기는 것 자체도 위험이 다른
                //  별개 변경이라 뒤 슬라이스 몫이다. 재는 쪽은 그래도 이것들을 세어 참고 줄로
                //  알린다 — 빼되 감추지 않는다.
                var box = renderer.GetComponent<BoxCollider>();
                var solid = renderer.GetComponent<Collider>();
                if (solid == null || solid.isTrigger || solid.enabled == false)
                {
                    skipped.Add($"{renderer.name} (렌더 전용 — 틈을 안 만든다)  두께 {backDepth:F3}");
                    continue;
                }
                if (box == null)
                {
                    //  중심을 보정할 수 없는 모양이라 콜라이더를 제자리에 못 박지 못한다.
                    //  임의로 옮기면 판정이 따라 움직인다 — 그게 이 설계가 막으려는 바로 그것이다.
                    skipped.Add($"{renderer.name} (BoxCollider가 아님 — 중심 보정 불가)  두께 {backDepth:F3}");
                    continue;
                }

                //  x나 y로 돌아간 조상이 있으면 z를 옮기는 것이 뜻대로 안 된다(두께가 z축에
                //  안 놓여 있다). 임의 판단하지 말고 건너뛰고 보고한다 — 사람이 봐야 한다.
                if (HasNonZRotation(renderer.transform))
                {
                    skipped.Add($"{renderer.name} (x/y 회전)  두께 {backDepth:F3}");
                    continue;
                }

                Transform t = renderer.transform;
                Undo.RecordObject(t, "판정면 정렬");
                movedTransforms.Add((t, t.position));
                //  <b>월드 좌표</b>로 옮긴다. bounds도 월드라 둘이 같은 공간이어야 한다 —
                //  localPosition을 건드리면 부모에 배율/회전이 있을 때 조용히 어긋난다.
                Vector3 p = t.position;
                p.z -= backDepth;
                t.position = p;
                EditorUtility.SetDirty(t);
                Physics.SyncTransforms();

                moved.Add($"{renderer.name}  Δz={-backDepth:F3}  →  렌더 max.z={renderer.bounds.max.z:F4}");
            }

            //  ── 2. 딸려간 콜라이더를 제자리로 되돌린다 ───────────────────────────────
            //  옮긴 오브젝트 자신의 콜라이더뿐 아니라 <b>그 아래 딸린 것 전부</b>를 본다.
            var movedCenters = new List<(BoxCollider Box, Vector3 Center)>();
            for (int i = 0; i < anchors.Count; i++)
            {
                ColliderAnchor a = anchors[i];
                if (a.Collider == null)
                {
                    continue;
                }
                Vector3 drift = a.Collider.transform.position - a.TransformPosition;
                if (drift.sqrMagnitude < 1e-12f)
                {
                    continue;   // 안 끌려갔다 — 되돌릴 것이 없다
                }
                if (a.Collider is BoxCollider dragged)
                {
                    Undo.RecordObject(dragged, "판정면 정렬 — 콜라이더 제자리 고정");
                    movedCenters.Add((dragged, dragged.center));
                    //  끌려간 만큼 로컬 중심을 반대로 민다. InverseTransformVector가 부모의
                    //  회전·배율을 알아서 되돌려 준다 — 배율을 손으로 나누지 않는다.
                    dragged.center -= dragged.transform.InverseTransformVector(drift);
                    EditorUtility.SetDirty(dragged);
                }
                else
                {
                    failures.Add($"{a.Collider.name} — BoxCollider가 아닌데 끌려갔다(중심 보정 불가)");
                }
            }
            Physics.SyncTransforms();

            //  ── 3. 불변식: 어떤 콜라이더도 안 움직였나 ───────────────────────────────
            for (int i = 0; i < anchors.Count; i++)
            {
                ColliderAnchor a = anchors[i];
                if (a.Collider == null || a.BoundsMeaningful == false)
                {
                    continue;
                }
                Bounds now = a.Collider.bounds;
                float off = Mathf.Max(
                    (now.min - a.Bounds.min).magnitude, (now.max - a.Bounds.max).magnitude);
                if (off > BoundsEpsilon)
                {
                    failures.Add($"{a.Collider.name} — 월드 bounds가 {off:F5}m 어긋났다");
                }
            }

            //  검사에 걸리면 <b>추측하지 않고 통째로 되돌린다.</b> 부분만 살려 두면 어느 블록이
            //  어떤 상태인지 아무도 모르는 씬이 남는다.
            if (failures.Count > 0)
            {
                for (int i = 0; i < movedCenters.Count; i++) { movedCenters[i].Box.center = movedCenters[i].Center; }
                for (int i = 0; i < movedTransforms.Count; i++) { movedTransforms[i].Transform.position = movedTransforms[i].Position; }
                Physics.SyncTransforms();
                moved.Clear();
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            var log = new StringBuilder();
            log.AppendLine($"옮긴 렌더러 {moved.Count}개 · 건너뛴 것 {skipped.Count}개"
                         + $" · 이미 맞아 있던 것 {alreadyAligned}개 · 배경(판정 밖) {background}개");
            log.AppendLine($"콜라이더 제자리 고정 {movedCenters.Count}개 · 불변식 위반 {failures.Count}개"
                         + (failures.Count > 0 ? "  ← 전부 되돌렸다" : ""));
            log.AppendLine();
            log.AppendLine("[옮김]");
            foreach (string m in moved) { log.AppendLine("  " + m); }
            log.AppendLine();
            log.AppendLine("[건너뜀]");
            foreach (string s in skipped) { log.AppendLine("  " + s); }
            log.AppendLine();
            log.AppendLine("[불변식 위반]");
            foreach (string f in failures) { log.AppendLine("  " + f); }

            //  Logs/에 두는 이유: git이 무시하면서 유니티가 안 비운다. 콘솔은 도메인 리로드에
            //  지워지고 Temp/는 리로드 때 통째로 지워진다.
            string path = Path.Combine(
                Path.GetDirectoryName(UnityEngine.Application.dataPath), "Logs", "FlappyDepthAlign.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, log.ToString());
            Debug.Log($"[판정면 정렬] 옮김 {moved.Count} · 건너뜀 {skipped.Count}"
                    + $" · 위반 {failures.Count} — {path}");
        }

        /// <summary>계층에서 몇 번째 깊이인가. 조상부터 옮기려고 정렬 키로 쓴다.</summary>
        private static int Depth(Transform t)
        {
            int depth = 0;
            for (Transform cur = t.parent; cur != null; cur = cur.parent) { depth++; }
            return depth;
        }

        /// <summary>z축 둘레 회전은 z 범위를 안 바꾸므로 괜찮다. x나 y로 돌면 얘기가 다르다.</summary>
        private static bool HasNonZRotation(Transform t)
        {
            for (Transform cur = t; cur != null; cur = cur.parent)
            {
                Vector3 e = cur.localEulerAngles;
                if (Mathf.Abs(Mathf.DeltaAngle(e.x, 0f)) > 0.01f) { return true; }
                if (Mathf.Abs(Mathf.DeltaAngle(e.y, 0f)) > 0.01f) { return true; }
            }
            return false;
        }
    }
}
