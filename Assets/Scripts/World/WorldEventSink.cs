using GameFramework;
using LOP.Event.Entity;
using System.Collections.Generic;
using VContainer;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 스냅샷을 프레젠테이션 EventBus로 송출하는 egress sink(클라). 코어 상태·새 이벤트 안 만듦.
    ///   DamageDealtEvent      → EntityDamage (피격자 숫자/Hit 연출)
    ///   AbilityActivatedEvent → AbilityActivated (시전자 발동 애니; abilityId→cue는 클라 마스터데이터로 해석)
    /// </summary>
    public class WorldEventSink : GameFramework.World.IEventSink
    {
        [Inject]
        private LOP.MasterData.LOPMasterData md;

        public void Emit(IReadOnlyList<GameFramework.World.WorldEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case GameFramework.World.DamageDealtEvent dde:
                        EventBus.Default.Publish(
                            EventTopic.EntityId<LOPEntity>(dde.targetId),
                            new EntityDamage(dde.isDodged, dde.isCritical, dde.amount)
                        );
                        break;

                    case GameFramework.World.AbilityActivatedEvent ae:
                    {
                        // abilityId → cue(클라 전용 마스터데이터 컬럼). 빈 cue면 연출 없음(dash/haste).
                        string cue = md.Tables.TbAbility.GetOrDefault(ae.abilityId)?.Cue;
                        if (string.IsNullOrEmpty(cue) == false)
                        {
                            EventBus.Default.Publish(
                                EventTopic.EntityId<LOPEntity>(ae.entityId),
                                new AbilityActivated(cue)
                            );
                        }
                        break;
                    }
                }
            }
        }
    }
}
