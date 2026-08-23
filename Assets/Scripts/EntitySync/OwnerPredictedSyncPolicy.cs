using System;

namespace LOP
{
    /// <summary>
    /// 내 엔티티만 예측하고 나머지는 보간한다(FlapWang). Unity Netcode for Entities의
    /// <c>GhostMode.OwnerPredicted</c>에 대응한다.
    /// </summary>
    public class OwnerPredictedSyncPolicy : IEntitySyncPolicy
    {
        private readonly Func<string> _localEntityId;

        public OwnerPredictedSyncPolicy(Func<string> localEntityId)
        {
            _localEntityId = localEntityId;
        }

        public EntitySyncMode For(GameFramework.World.Entity entity)
        {
            string localEntityId = _localEntityId();
            if (string.IsNullOrEmpty(localEntityId))
            {
                return EntitySyncMode.Interpolated;
            }
            return entity.Id == localEntityId ? EntitySyncMode.Predicted : EntitySyncMode.Interpolated;
        }
    }
}
