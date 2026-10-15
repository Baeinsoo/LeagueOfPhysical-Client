using System.Collections.Generic;

namespace LOP
{
    /// <summary>서버 결과 메시지 + 경기 정보 → 결과 단계가 방 없이 그릴 수 있는 결과.</summary>
    public static class MatchResultCapture
    {
        public static MatchResult From(MatchEndedToC message, string matchId, Match match)
        {
            var participants = new MatchParticipantResult[message.Placements.Count];
            for (int i = 0; i < participants.Length; i++)
            {
                var p = message.Placements[i];
                participants[i] = new MatchParticipantResult
                {
                    userId = p.UserId,
                    placement = p.Placement,
                    //  proto map은 null이 아니라 빈 컬렉션으로 온다 — 그대로 복사해도 안전하다.
                    stats = new Dictionary<string, int>(p.Stats),
                    displayName = p.DisplayName ?? string.Empty,
                    look = PlayerLookFromProto.From(p.Look, p.DisplayName, p.AccountLevel),
                };
            }

            var result = new MatchResult
            {
                matchId = matchId,
                participants = participants,
                hasRatingChange = message.HasRatingChange,
                myMmrBefore = message.MyMmrBefore,
                myMmrAfter = message.MyMmrAfter,
                queueId = match?.queueId ?? 0,
            };

            var rounds = match?.rounds;
            if (rounds != null && rounds.Length > 0)
            {
                var round = rounds[MatchSceneResolver.CurrentRoundIndex(rounds.Length)];
                result.gameModeId = round.gameModeId;
                result.mapId = round.mapId;
            }
            return result;
        }
    }
}
