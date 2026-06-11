using System;
using GameFramework;
using LOP.Event.Entity;
using R3;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 스탯 패널 ViewModel. 게임 스코프 resolver로 생성되므로 IPlayerContext를 생성자 주입받는다.
    /// 엔티티의 EventBus PropertyChange를 R3 ReactiveProperty로 브리지(폴링 제거) → View가 구독.
    /// </summary>
    public class StatsViewModel : IDisposable
    {
        private readonly IPlayerContext _playerContext;
        private readonly LOPEntity _entity;
        private readonly StatsComponent _stats;
        private readonly UserComponent _user;

        private readonly ReactiveProperty<int> _strength = new(0);
        private readonly ReactiveProperty<int> _dexterity = new(0);
        private readonly ReactiveProperty<int> _intelligence = new(0);
        private readonly ReactiveProperty<int> _vitality = new(0);
        private readonly ReactiveProperty<int> _statPoints = new(0);
        private readonly ReadOnlyReactiveProperty<bool> _canAllocate;

        public ReadOnlyReactiveProperty<int> Strength => _strength;
        public ReadOnlyReactiveProperty<int> Dexterity => _dexterity;
        public ReadOnlyReactiveProperty<int> Intelligence => _intelligence;
        public ReadOnlyReactiveProperty<int> Vitality => _vitality;
        public ReadOnlyReactiveProperty<int> StatPoints => _statPoints;
        public ReadOnlyReactiveProperty<bool> CanAllocate => _canAllocate;

        public StatsViewModel(IPlayerContext playerContext)
        {
            _playerContext = playerContext;
            _canAllocate = _statPoints.Select(p => p > 0).ToReadOnlyReactiveProperty(false);

            _entity = playerContext.entity;
            if (_entity == null)
            {
                Debug.LogWarning("[StatsViewModel] playerContext.entity가 null입니다. 유저 엔티티 생성 후 열어야 합니다.");
                return;
            }

            _stats = _entity.GetEntityComponent<StatsComponent>();
            _user = _entity.GetEntityComponent<UserComponent>();

            PushAll();
            EventBus.Default.Subscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnPropertyChange);
        }

        public void Allocate(string statName)
        {
            if (_user == null || _user.statPoints == 0)
            {
                return;
            }

            _playerContext.session.Send(new StatAllocationToS
            {
                SessionId = _playerContext.session.sessionId,
                Stat = statName,
            });
        }

        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(StatsComponent.strength): _strength.Value = _stats.strength; break;
                case nameof(StatsComponent.dexterity): _dexterity.Value = _stats.dexterity; break;
                case nameof(StatsComponent.intelligence): _intelligence.Value = _stats.intelligence; break;
                case nameof(StatsComponent.vitality): _vitality.Value = _stats.vitality; break;
                case nameof(UserComponent.statPoints): _statPoints.Value = _user.statPoints; break;
            }
        }

        private void PushAll()
        {
            _strength.Value = _stats.strength;
            _dexterity.Value = _stats.dexterity;
            _intelligence.Value = _stats.intelligence;
            _vitality.Value = _stats.vitality;
            _statPoints.Value = _user.statPoints;
        }

        public void Dispose()
        {
            if (_entity != null)
            {
                EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnPropertyChange);
            }

            _strength.Dispose();
            _dexterity.Dispose();
            _intelligence.Dispose();
            _vitality.Dispose();
            _statPoints.Dispose();
            _canAllocate.Dispose();
        }
    }
}
