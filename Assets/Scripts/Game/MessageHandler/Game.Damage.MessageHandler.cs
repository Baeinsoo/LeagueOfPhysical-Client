using GameFramework;
using LOP.Event.Entity;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameDamageMessageHandler : IGameMessageHandler
    {
        [Inject]
        private IGameEngine gameEngine;

        public void Register()
        {
            EventBus.Default.Subscribe<DamageEventToC>(nameof(IMessage), OnDamageEventToC);
        }

        public void Unregister()
        {
            EventBus.Default.Unsubscribe<DamageEventToC>(nameof(IMessage), OnDamageEventToC);
        }

        private async void OnDamageEventToC(DamageEventToC damageEventToC)
        {
            LOPEntity attackerEntity = gameEngine.entityManager.GetEntity<LOPEntity>(damageEventToC.AttackerId);
            LOPEntity targetEntity = gameEngine.entityManager.GetEntity<LOPEntity>(damageEventToC.TargetId);

            ServerStateReconciler serverStateReconciler = targetEntity.gameObject.GetComponent<ServerStateReconciler>();
            while (serverStateReconciler != null && serverStateReconciler.currentT < damageEventToC.Tick)
            {
                await System.Threading.Tasks.Task.Yield();
            }

            EventBus.Default.Publish(
                EventTopic.EntityId<LOPEntity>(targetEntity.entityId),
                new EntityDamage(damageEventToC.IsDodged, damageEventToC.IsCritical, damageEventToC.Damage, damageEventToC.RemainingHP, damageEventToC.IsDead)
            );
        }
    }
}
