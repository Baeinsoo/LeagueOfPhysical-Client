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
            // 와이어 → 코어 이벤트 변환 어댑터. 슬라이스 3에선 레거시 메시지를 코어 도메인으로 격리.
            worldEventBuffer.Append(new GameFramework.World.DamageDealtEvent(
                targetId:   msg.TargetId,
                attackerId: msg.AttackerId,
                amount:     (int)msg.Damage,
                isCritical: msg.IsCritical,
                isDodged:   msg.IsDodged,
                remaining:  (int)msg.RemainingHP,
                isDead:     msg.IsDead
            ));

            if (msg.IsDead)
            {
                worldEventBuffer.Append(new GameFramework.World.DeathEvent(
                    victimId:   msg.TargetId,
                    attackerId: msg.AttackerId
                ));
            }
        }
    }
}
