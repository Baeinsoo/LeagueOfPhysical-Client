using System;
using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using R3;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 플레이어 HUD ViewModel. 게임 스코프 resolver로 생성되어 IPlayerContext를 주입받는다.
    /// 엔티티의 HP/MP/EXP/Level을 R3 ReactiveProperty로 노출 → View가 구독(폴링 없음).
    /// HP: 초기값 World.Health pull, 라이브 EntityHealthChanged 이벤트로 갱신.
    /// MP: 초기값 World.Mana pull, 라이브 EntityManaChanged 이벤트로 갱신.
    /// EXP/Level: 초기값 World.Level pull, 라이브 EntityLevelChanged 이벤트로 갱신.
    /// </summary>
    public class CharacterHudViewModel : IDisposable
    {
        private readonly LOPEntity _entity;
        private readonly GameFramework.World.EntityRegistry _entityRegistry;

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

        private IDisposable _subscriptions;

        public CharacterHudViewModel(
            IPlayerContext playerContext,
            GameFramework.World.EntityRegistry entityRegistry,
            ISubscriber<string, EntityHealthChanged> healthSubscriber,
            ISubscriber<string, EntityManaChanged> manaSubscriber,
            ISubscriber<string, EntityLevelChanged> levelSubscriber)
        {
            _entityRegistry = entityRegistry;
            _entity = playerContext.entity;
            if (_entity == null)
            {
                Debug.LogWarning("[CharacterHudViewModel] playerContext.entity가 null입니다. 유저 엔티티 생성 후 열어야 합니다.");
                return;
            }

            PushAll();

            var bag = MessagePipe.DisposableBag.CreateBuilder();
            healthSubscriber.Subscribe(_entity.entityId, OnEntityHealthChanged).AddTo(bag);
            manaSubscriber.Subscribe(_entity.entityId, OnEntityManaChanged).AddTo(bag);
            levelSubscriber.Subscribe(_entity.entityId, OnEntityLevelChanged).AddTo(bag);
            _subscriptions = bag.Build();
        }

        private void OnEntityLevelChanged(EntityLevelChanged e)
        {
            _levelValue.Value = e.level;
            _exp.Value = e.currentExp;
            _expToNext.Value = e.expToNext;
        }

        private void OnEntityHealthChanged(EntityHealthChanged e)
        {
            _hp.Value = e.current;
            _maxHp.Value = e.max;
        }

        private void OnEntityManaChanged(EntityManaChanged e)
        {
            _mp.Value = e.current;
            _maxMp.Value = e.max;
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
            GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
            if (mana != null)
            {
                _mp.Value = mana.Current;
                _maxMp.Value = mana.Max;
            }
            GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
            if (level != null)
            {
                _exp.Value = level.Exp;
                _expToNext.Value = level.ExpToNext;
                _levelValue.Value = level.Value;
            }
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();

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
