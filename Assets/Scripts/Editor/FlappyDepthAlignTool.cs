using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 장애물의 <b>뒷면</b>을 판정면(z=0)에 맞춘다. 두께는 그대로 두고 뻗는 방향만 카메라 쪽으로
    /// 돌린다.
    ///
    /// <para>왜 하나: 원근 카메라는 판정면보다 <b>뒤</b>에 있는 부분을 소실점 쪽으로 당겨 그린다.
    /// 그래서 뒤로 뻗은 두께만큼 틈이 화면에서 좁아 보인다 — 화면 가운데서 26cm, 가장자리에서
    /// 44cm. 통과 여유가 3~5cm뿐인 맵이라 눈으로는 못 지나갈 틈처럼 보인다. 앞으로 뻗으면
    /// 가장 안쪽 실루엣이 곧 판정면이라 그 왜곡이 사라진다.</para>
    ///
    /// <para>게임플레이는 안 바뀐다: 박스는 z방향으로 단면이 일정하고, 옮긴 뒤에도 새의 z대역과
    /// 겹치므로 x/y 스윕이 재는 거리가 같다. 그래도 <b>믿지 말고 검사로 확인한다</b> — 맵 검사를
    /// 다시 돌려 통과 여유가 그대로인지 대조하는 것이 이 변경의 진짜 검증이다.</para>
    ///
    /// <para><b>어느 콜라이더를 볼지·두께를 어떻게 셀지는 여기서 정하지 않는다</b> — 그 규칙은
    /// <see cref="LOP.MapTools.BlockDepthScan"/>에 한 벌만 두고, 재는 쪽
    /// (<see cref="FlappyMapPlayabilityCheck"/>)과 고치는 쪽(여기)이 같은 것을 부른다. 규칙이 둘로
    /// 갈라지면 서로 다른 블록을 보면서 맞췄다고 착각한다.</para>
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

        [MenuItem("LOP/Debug/Flappy 판정면 정렬")]
        public static void Align() => Run();

        /// <summary>사람 없이 도는 백그라운드 잡(<c>unity cmd eval_file --detach</c>)에서 부른다.</summary>
        public static void RunHeadless() => Run();

        private static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                MapScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            int mapMask = LayerMask.GetMask("Default");
            var moved = new List<string>();
            var skipped = new List<string>();
            int alreadyAligned = 0;
            int background = 0;

            //  조상을 먼저 옮긴다. 자식 콜라이더를 먼저 맞춰 놓고 나중에 그 부모를 옮기면 자식이
            //  딸려가 다시 어긋난다 — 순서에 결과가 걸리지 않게 깊이 오름차순으로 돈다.
            var colliders = new List<Collider>(Object.FindObjectsByType<Collider>(FindObjectsSortMode.None));
            colliders.Sort((a, b) => Depth(a.transform).CompareTo(Depth(b.transform)));

            foreach (var collider in colliders)
            {
                //  아래 세 가지를 거르는 이유는 맵 검사의 ScanBlockDepths와 같다: 다른 레이어는
                //  맵 지형이 아니고, 꺼진 콜라이더는 부딪히지 않으며(게다가 bounds가 원점의 크기 0),
                //  트리거는 통과하는 것이지 부딪히는 것이 아니다(결승선을 옮겨 버린다).
                if ((mapMask & (1 << collider.gameObject.layer)) == 0)
                {
                    continue;
                }
                if (collider.enabled == false)
                {
                    continue;
                }
                if (collider.isTrigger)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                if (LOP.MapTools.BlockDepthScan.IsGameplayBlock(bounds, BodyRadius) == false)
                {
                    background++;
                    continue;   // 배경 — 새가 지나는 두께를 안 건드리므로 옮길 이유가 없다
                }

                float backDepth = LOP.MapTools.BlockDepthScan.BackDepth(bounds);
                if (backDepth <= Tolerance)
                {
                    alreadyAligned++;
                    continue;
                }

                //  x나 y로 돌아간 조상이 있으면 z를 옮기는 것이 뜻대로 안 된다(두께가 z축에
                //  안 놓여 있다). 임의 판단하지 말고 건너뛰고 보고한다 — 사람이 봐야 한다.
                if (HasNonZRotation(collider.transform))
                {
                    skipped.Add($"{collider.name} (x/y 회전)");
                    continue;
                }

                Transform t = collider.transform;
                Undo.RecordObject(t, "판정면 정렬");
                //  <b>월드 좌표</b>로 옮긴다. bounds도 월드라 둘이 같은 공간이어야 한다 —
                //  localPosition을 건드리면 부모에 배율/회전이 있을 때 조용히 어긋난다.
                Vector3 p = t.position;
                p.z -= backDepth;
                t.position = p;
                EditorUtility.SetDirty(t);

                //  bounds는 트랜스폼을 물리에 반영해야 갱신된다(Physics.autoSyncTransforms가 꺼져
                //  있으면 옛 값을 돌려준다). 같은 오브젝트에 콜라이더가 둘 달렸을 때 두 번 옮기는
                //  것을 이 한 줄이 막는다 — 두 번째는 갱신된 bounds를 보고 "이미 맞음"으로 빠진다.
                Physics.SyncTransforms();

                moved.Add($"{collider.name}  Δz={-backDepth:F3}  →  max.z={collider.bounds.max.z:F4}");
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            var log = new StringBuilder();
            log.AppendLine($"옮긴 블록 {moved.Count}개 · 건너뛴 블록 {skipped.Count}개"
                         + $" · 이미 맞아 있던 것 {alreadyAligned}개 · 배경(판정 밖) {background}개");
            log.AppendLine();
            log.AppendLine("[옮김]");
            foreach (string m in moved) { log.AppendLine("  " + m); }
            log.AppendLine();
            log.AppendLine("[건너뜀]");
            foreach (string s in skipped) { log.AppendLine("  " + s); }

            //  Logs/에 두는 이유: git이 무시하면서 유니티가 안 비운다. 콘솔은 도메인 리로드에
            //  지워지고 Temp/는 리로드 때 통째로 지워진다.
            string path = Path.Combine(
                Path.GetDirectoryName(UnityEngine.Application.dataPath), "Logs", "FlappyDepthAlign.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, log.ToString());
            Debug.Log($"[판정면 정렬] 옮김 {moved.Count} · 건너뜀 {skipped.Count} — {path}");
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
