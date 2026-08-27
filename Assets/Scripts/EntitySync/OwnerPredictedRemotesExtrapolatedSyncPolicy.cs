using System;

namespace LOP
{
    /// <summary>
    /// 내 새는 예측하고 남은 외삽한다(Flappy Race). 남의 플랩 입력이 클라로 오지 않으므로 남을
    /// 시뮬로 굴리면 "계속 추락"이라는 틀린 궤적이 나온다 — 굴리는 대신 마지막 속도로 이어 그린다.
    /// </summary>
    public class OwnerPredictedRemotesExtrapolatedSyncPolicy : IEntitySyncPolicy
    {
        private readonly Func<string> _localEntityId;

        public OwnerPredictedRemotesExtrapolatedSyncPolicy(Func<string> localEntityId)
        {
            _localEntityId = localEntityId;
        }

        public EntitySyncMode For(GameFramework.World.Entity entity)
        {
            if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
            {
                return EntitySyncMode.Interpolated;
            }
            string localEntityId = _localEntityId();
            // 내 id를 아직 모르면(입장 직후) 남의 몸을 내 것으로 착각해 예측하면 안 되므로 전부 외삽으로 뗀다.
            return string.IsNullOrEmpty(localEntityId) == false && entity.Id == localEntityId
                ? EntitySyncMode.Predicted
                : EntitySyncMode.Extrapolated;
        }
    }
}
