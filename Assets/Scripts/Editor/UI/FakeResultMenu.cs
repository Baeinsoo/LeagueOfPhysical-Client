using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace LOP.EditorTools
{
    /// <summary>
    /// Play 중 로비에서 판을 돌리지 않고 결과 단계를 본다 — 가짜 8인 결과(나 2위, 1명 나감)를 결과 저장소에 넣고 결과 씬을 연다.
    /// 앱 상태는 로비 그대로라 확인을 누르면 결과 단계가 로비로 보내지 못한다 — 그래서 확인 때 로비 씬을 직접 다시 연다.
    /// </summary>
    public static class FakeResultMenu
    {
        [MenuItem("LOP/UI/가짜 결과로 결과 단계 열기 (일반)")]
        public static void OpenCasual() => Open(ranked: false);

        [MenuItem("LOP/UI/가짜 결과로 결과 단계 열기 (랭크)")]
        public static void OpenRanked() => Open(ranked: true);

        [MenuItem("LOP/UI/가짜 결과로 결과 단계 열기 (일반)", true)]
        [MenuItem("LOP/UI/가짜 결과로 결과 단계 열기 (랭크)", true)]
        public static bool CanOpen() => EditorApplication.isPlaying;

        private static void Open(bool ranked)
        {
            var root = LifetimeScope.Find<RootLifetimeScope>();
            if (root == null)
            {
                Debug.LogWarning("[FakeResultMenu] Root 스코프를 못 찾았다 — 입구 씬부터 Play했는지 확인.");
                return;
            }

            var container = root.Container;
            var users = container.Resolve<IUserDataStore>();
            var tables = container.Resolve<LOP.MasterData.LOPMasterData>().Tables;
            string me = users.user?.id ?? "me";

            int queueId = 0;
            foreach (var q in tables.TbQueue.DataList)
            {
                if (q.HasVisibleRank == ranked) { queueId = q.Id; break; }
            }
            int gameModeId = tables.TbGameMode.DataList.Count > 0 ? tables.TbGameMode.DataList[0].Id : 0;
            int mapId = tables.TbMap.DataList.Count > 0 ? tables.TbMap.DataList[0].Id : 0;

            var participants = new List<MatchParticipantResult>();
            for (int i = 0; i < 8; i++)
            {
                bool isMe = i == 1;
                var stats = new Dictionary<string, int>();
                if (i == 7) stats[MatchStatKeys.Left] = 1;
                participants.Add(new MatchParticipantResult
                {
                    userId = isMe ? me : $"fake-{i}",
                    placement = i + 1,
                    stats = stats,
                    displayName = isMe ? users.user?.displayName ?? string.Empty : $"{i + 1}P 선수",
                });
            }

            container.Resolve<IMatchResultDataStore>().result = new MatchResult
            {
                //  가짜 판 id — 로비 조회는 실패하고 랭크·보상 줄은 숨는다(조회 실패 경로도 함께 본다).
                matchId = "fake-match",
                queueId = queueId,
                gameModeId = gameModeId,
                mapId = mapId,
                participants = participants.ToArray(),
                //  "내 기록" 카드 보기(spec §7) — 경신 한 줄(도장), 첫 기록 한 줄.
                myRecords = new[]
                {
                    new RecordOutcome(FlappyRecordMetrics.FinishTime, 52.04d, compared: true, hasPrevious: true, previous: 52.4d),
                    new RecordOutcome(FlappyRecordMetrics.FlapStreak, 18d, compared: true, hasPrevious: false, previous: 0d),
                },
            };
            ResultFlow.LeaveOverride = () => SceneManager.LoadScene("Lobby");
            SceneManager.LoadScene(PostMatch.ResultSceneName);
        }
    }
}
