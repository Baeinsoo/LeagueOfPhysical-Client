using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 스냅샷을 프레젠테이션 EventBus로 송출하는 egress sink(클라). 코어 상태·새 이벤트 안 만듦.
    ///   DamageDealtEvent      → EntityDamage (피격자 숫자/Hit 연출)
    ///   AbilityActivatedEvent → AbilityActivated (시전자 발동 애니; abilityId→cue는 클라 마스터데이터로 해석)
    /// </summary>
    public class WorldEventSink : GameFramework.World.IEventSink
    {
        private readonly LOP.MasterData.LOPMasterData md;
        private readonly IPublisher<string, EntityDamage> entityDamagePublisher;
        private readonly IPublisher<string, AbilityActivated> abilityActivatedPublisher;

        public WorldEventSink(LOP.MasterData.LOPMasterData md, IPublisher<string, EntityDamage> entityDamagePublisher, IPublisher<string, AbilityActivated> abilityActivatedPublisher)
        {
            this.md = md;
            this.entityDamagePublisher = entityDamagePublisher;
            this.abilityActivatedPublisher = abilityActivatedPublisher;
        }

        public void Emit(IReadOnlyList<GameFramework.World.WorldEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case GameFramework.World.DamageDealtEvent dde:
                        entityDamagePublisher.Publish(dde.targetId, new EntityDamage(dde.isDodged, dde.isCritical, dde.amount));
                        break;

                    case GameFramework.World.AbilityActivatedEvent ae:
                    {
                        // abilityId → cue(클라 전용 마스터데이터 컬럼). 빈 cue면 연출 없음(dash/haste).
                        string cue = md.Tables.TbAbility.GetOrDefault(ae.abilityId)?.Cue;
                        if (string.IsNullOrEmpty(cue) == false)
                        {
                            abilityActivatedPublisher.Publish(ae.entityId, new AbilityActivated(cue));
                        }
                        break;
                    }
                }
            }
        }
    }
}
