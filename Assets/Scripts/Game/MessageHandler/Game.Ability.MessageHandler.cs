using GameFramework;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 서버의 어빌리티 발동 연출(AbilityActivatedToC)을 코어 연출 이벤트로 재수화하는 어댑터(클라).
    /// 내 캐릭은 로컬 예측이 이미 넣었으므로 서버 사본은 스킵(레거시 OnActionStartToC와 동일).
    /// </summary>
    public class GameAbilityMessageHandler : IGameMessageHandler
    {
        [Inject]
        private GameFramework.World.WorldEventBuffer worldEventBuffer;

        [Inject]
        private IPlayerContext playerContext;

        public void Initialize()
        {
            EventBus.Default.Subscribe<AbilityActivatedToC>(nameof(IMessage), OnAbilityActivatedToC);
        }

        public void Dispose()
        {
            EventBus.Default.Unsubscribe<AbilityActivatedToC>(nameof(IMessage), OnAbilityActivatedToC);
        }

        private void OnAbilityActivatedToC(AbilityActivatedToC msg)
        {
            // 내 캐릭은 ApplyInput 예측이 이미 연출 이벤트를 넣었다 → 서버 사본 스킵(중복 방지).
            if (playerContext.entity != null && msg.EntityId == playerContext.entity.entityId)
            {
                return;
            }

            worldEventBuffer.Append(new GameFramework.World.AbilityActivatedEvent(msg.EntityId, msg.AbilityId));
        }
    }
}
