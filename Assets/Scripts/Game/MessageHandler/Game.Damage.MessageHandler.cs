using GameFramework;
using VContainer;

namespace LOP
{
    public class GameDamageMessageHandler : IGameMessageHandler
    {
        [Inject]
        private GameFramework.World.WorldEventBuffer worldEventBuffer;

        public void Register()
        {
            EventBus.Default.Subscribe<DamageEventToC>(nameof(IMessage), OnDamageEventToC);
        }

        public void Unregister()
        {
            EventBus.Default.Unsubscribe<DamageEventToC>(nameof(IMessage), OnDamageEventToC);
        }

        private void OnDamageEventToC(DamageEventToC msg)
        {
            // 와이어 → 코어 연출 이벤트 변환 어댑터. HP/죽음은 스냅샷에서 파생되므로 여기선 연출만.
            worldEventBuffer.Append(new GameFramework.World.DamageDealtEvent(
                targetId:   msg.TargetId,
                attackerId: msg.AttackerId,
                amount:     (int)msg.Damage,
                isCritical: msg.IsCritical,
                isDodged:   msg.IsDodged
            ));
        }
    }
}
