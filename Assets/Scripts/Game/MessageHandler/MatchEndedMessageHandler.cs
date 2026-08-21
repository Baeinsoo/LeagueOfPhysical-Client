using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 서버의 매치 종료 통보를 받아 ① 결과를 Root 스토어에 남기고 ② 러너를 종료 상태로 보낸다.
    /// 로비 씬 로드는 LOPRoom이 러너 상태 변화를 보고 수행하므로 여기서 씬을 만지지 않는다.
    /// </summary>
    public class MatchEndedMessageHandler : MessageHandlerBase
    {
        private readonly LOPRunner runner;
        private readonly IRoomDataStore roomDataStore;
        private readonly IMatchResultDataStore matchResultDataStore;
        private readonly ISubscriber<MatchEndedToC> matchEndedSubscriber;

        public MatchEndedMessageHandler(
            LOPRunner runner,
            IRoomDataStore roomDataStore,
            IMatchResultDataStore matchResultDataStore,
            ISubscriber<MatchEndedToC> matchEndedSubscriber)
        {
            this.runner = runner;
            this.roomDataStore = roomDataStore;
            this.matchResultDataStore = matchResultDataStore;
            this.matchEndedSubscriber = matchEndedSubscriber;
        }

        protected override void Subscribe() => Track(matchEndedSubscriber.Subscribe(OnMatchEnded));

        private void OnMatchEnded(MatchEndedToC message)
        {
            var participants = new MatchParticipantResult[message.Placements.Count];
            for (int i = 0; i < message.Placements.Count; i++)
            {
                participants[i] = new MatchParticipantResult
                {
                    userId = message.Placements[i].UserId,
                    placement = message.Placements[i].Placement,
                };
            }

            matchResultDataStore.result = new MatchResult
            {
                matchId = roomDataStore.room?.matchId,
                participants = participants,
                hasRatingChange = message.HasRatingChange,
                myMmrBefore = message.MyMmrBefore,
                myMmrAfter = message.MyMmrAfter,
            };

            runner.EndMatch();
        }
    }
}
