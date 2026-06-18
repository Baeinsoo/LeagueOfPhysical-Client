using System;
using GameFramework;
using LOP.Event.Entity;
using R3;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 플레이어 HUD ViewModel. 게임 스코프 resolver로 생성되어 IPlayerContext를 주입받는다.
    /// 엔티티의 HP/MP/EXP/Level을 R3 ReactiveProperty로 노출 → View가 구독(폴링 없음).
    /// MP/EXP/Level은 컴포넌트 PropertyChange(반응형, M2a 패턴)로, HP는 EntityDamage 이벤트로 갱신한다.
    /// HP 초기값은 World.Entity의 World.Health(순수 C# 코어 — HP 진실원본)에서 읽는다(Model A — pull).
    /// </summary>
    public class CharacterHudViewModel : IDisposable
    {
        private readonly LOPEntity _entity;
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly ManaComponent _mana;
        private readonly LevelComponent _level;

        private readonly ReactiveProperty<int> _hp = new(0);
        private readonly ReactiveProperty<int> _maxHp = new(1);
        private readonly ReactiveProperty<int> _mp = new(0);
        private readonly ReactiveProperty<int> _maxMp = new(1);
        private readonly ReactiveProperty<long> _exp = new(0);
        private readonly ReactiveProperty<long> _expToNext = new(1);
        private readonly ReactiveProperty<int> _levelValue = new(1);

        public ReadOnlyReactiveProperty<int> Hp => _hp;
        public ReadOnlyReactiveProperty<int> MaxHp => _maxHp;
        public ReadOnlyReactiveProperty<int> Mp => _mp;
        public ReadOnlyReactiveProperty<int> MaxMp => _maxMp;
        public ReadOnlyReactiveProperty<long> Exp => _exp;
        public ReadOnlyReactiveProperty<long> ExpToNext => _expToNext;
        public ReadOnlyReactiveProperty<int> Level => _levelValue;

        public CharacterHudViewModel(IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry)
        {
            _entityRegistry = entityRegistry;
            _entity = playerContext.entity;
            if (_entity == null)
            {
                Debug.LogWarning("[CharacterHudViewModel] playerContext.entity가 null입니다. 유저 엔티티 생성 후 열어야 합니다.");
                return;
            }

            _mana = _entity.GetEntityComponent<ManaComponent>();
            _level = _entity.GetEntityComponent<LevelComponent>();

            PushAll();

            EventBus.Default.Subscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnPropertyChange);
            EventBus.Default.Subscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityDamage);
        }

        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(ManaComponent.currentMP): _mp.Value = _mana.currentMP; break;
                case nameof(ManaComponent.maxMP): _maxMp.Value = _mana.maxMP; break;
                case nameof(LevelComponent.level): _levelValue.Value = _level.level; break;
                case nameof(LevelComponent.currentExp): _exp.Value = _level.currentExp; break;
                case nameof(LevelComponent.expToNextLevel): _expToNext.Value = _level.expToNextLevel; break;
            }
        }

        private void OnEntityDamage(EntityDamage entityDamage)
        {
            _hp.Value = (int)entityDamage.remainingHP;
        }

        private void PushAll()
        {
            GameFramework.World.Entity worldEntity = _entityRegistry.Get(_entity.entityId);
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            if (health != null)
            {
                _hp.Value = health.Current;
                _maxHp.Value = health.Max;
            }
            if (_mana != null)
            {
                _mp.Value = _mana.currentMP;
                _maxMp.Value = _mana.maxMP;
            }
            if (_level != null)
            {
                _exp.Value = _level.currentExp;
                _expToNext.Value = _level.expToNextLevel;
                _levelValue.Value = _level.level;
            }
        }

        public void Dispose()
        {
            if (_entity != null)
            {
                EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnPropertyChange);
                EventBus.Default.Unsubscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityDamage);
            }

            _hp.Dispose();
            _maxHp.Dispose();
            _mp.Dispose();
            _maxMp.Dispose();
            _exp.Dispose();
            _expToNext.Dispose();
            _levelValue.Dispose();
        }
    }
}
