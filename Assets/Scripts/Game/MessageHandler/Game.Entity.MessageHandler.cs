using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameEntityMessageHandler : IGameMessageHandler
    {
        [Inject] private IPlayerContext playerContext;
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IRunner runner;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private GameFramework.World.HealthSystem healthSystem;
        [Inject] private GameFramework.World.ManaSystem manaSystem;
        [Inject] private GameFramework.World.LevelSystem levelSystem;
        [Inject] private GameFramework.World.StatsSystem statsSystem;
        [Inject] private Reconciler reconciler;
        [Inject] private RemoteInterpolationClock remoteInterpolationClock;

        [Inject] private ISubscriber<EntitySnapsToC> snapsSubscriber;
        [Inject] private ISubscriber<EntitySpawnToC> spawnSubscriber;
        [Inject] private ISubscriber<EntityDespawnToC> despawnSubscriber;
        [Inject] private ISubscriber<UserEntitySnapToC> userSnapSubscriber;
        [Inject] private ISubscriber<StatAllocationToC> statAllocationSubscriber;

        [Inject] private IPublisher<string, EntityHealthChanged> healthChangedPublisher;
        [Inject] private IPublisher<string, EntityManaChanged> manaChangedPublisher;
        [Inject] private IPublisher<string, EntityLevelChanged> levelChangedPublisher;
        [Inject] private IPublisher<string, EntityStatPointsChanged> statPointsChangedPublisher;
        [Inject] private IPublisher<string, EntityStatChanged> statChangedPublisher;

        // 스냅이 틱당 여러 메시지로 청킹돼 온다 → 도착 기록을 틱당 1회로 dedupe(간격 기반 추정기 왜곡 방지).
        private long lastRecordedArrivalTick = long.MinValue;

        private System.IDisposable subscriptions;

        public void Initialize()
        {
            var bag = DisposableBag.CreateBuilder();
            snapsSubscriber.Subscribe(OnEntitySnapsToC).AddTo(bag);
            spawnSubscriber.Subscribe(OnEntitySpawnToC).AddTo(bag);
            despawnSubscriber.Subscribe(OnEntityDespawnToC).AddTo(bag);
            userSnapSubscriber.Subscribe(OnUserEntitySnapToC).AddTo(bag);
            statAllocationSubscriber.Subscribe(OnStatAllocationToC).AddTo(bag);
            subscriptions = bag.Build();
        }

        public void Dispose()
        {
            subscriptions?.Dispose();
        }

        private void OnEntitySnapsToC(EntitySnapsToC entitySnapsToC)
        {
            if (Runner.current == null)
            {
                return;
            }

            if (entitySnapsToC.Tick > lastRecordedArrivalTick)
            {
                remoteInterpolationClock.RecordArrival(entitySnapsToC.Tick, UnityEngine.Time.timeAsDouble);
                lastRecordedArrivalTick = entitySnapsToC.Tick;
            }

            foreach (var serverEntitySnap in entitySnapsToC.EntitySnaps.OrEmpty())
            {
                if (runner.entityManager.TryGetEntity<LOPActor>(serverEntitySnap.EntityId, out var actor) == false)
                {
                    Debug.LogWarning($"Entity {serverEntitySnap.EntityId} not found");
                    continue;
                }

                EntitySnap entitySnap = MapperConfig.mapper.Map<EntitySnap>(serverEntitySnap);
                entitySnap.tick = entitySnapsToC.Tick;
                entitySnap.timestamp = entitySnapsToC.Tick * gameDataStore.gameInfo.Interval;

                entitySnap.contributions.Clear();
                foreach (var pc in serverEntitySnap.MotionContributions.OrEmpty())
                {
                    entitySnap.contributions.Add(new MotionContribution(
                        new System.Numerics.Vector3(pc.Horizontal.X, pc.Horizontal.Y, pc.Horizontal.Z),
                        (MotionContributionMode)pc.Mode, pc.Priority, pc.StartTick, pc.EndTick, pc.DecayPerTick));
                }

                if (playerContext.actor.entityId == actor.entityId)
                {
                    reconciler.AddServerSnap(entitySnap);
                }
                else
                {
                    GameFramework.World.Health health = entityRegistry.Get(serverEntitySnap.EntityId)?.Get<GameFramework.World.Health>();
                    if (health != null)
                    {
                        int prevCurrent = health.Current;
                        int prevMax = health.Max;
                        healthSystem.ApplyAuthoritativeState(health, serverEntitySnap.MaxHP, serverEntitySnap.CurrentHP);
                        if (health.Current != prevCurrent || health.Max != prevMax)
                        {
                            healthChangedPublisher.Publish(serverEntitySnap.EntityId, new EntityHealthChanged(health.Current, health.Max));
                        }
                    }

                    actor.GetComponent<RemoteEntityInterpolator>().AddServerEntitySnap(entitySnap);
                }
            }
        }

        private void OnEntitySpawnToC(EntitySpawnToC entitySpawnToC)
        {
            switch (entitySpawnToC.EntityCreationData.CreationDataCase)
            {
                case EntityCreationData.CreationDataOneofCase.CharacterCreationData:
                    string entityId = entitySpawnToC.EntityCreationData.CharacterCreationData.BaseEntityCreationData.EntityId;

                    if (runner.entityManager.TryGetEntity<LOPActor>(entityId, out var actor))
                    {
                        Debug.LogWarning($"Entity {entityId} already exists");
                        return;
                    }

                    runner.entityManager.CreateEntity<LOPActor, CharacterCreationData>(new CharacterCreationData
                    {
                        entityId = entityId,
                        position = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.CharacterCreationData.BaseEntityCreationData.Position),
                        rotation = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.CharacterCreationData.BaseEntityCreationData.Rotation),
                        velocity = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.CharacterCreationData.BaseEntityCreationData.Velocity),
                        characterCode = entitySpawnToC.EntityCreationData.CharacterCreationData.CharacterCode,
                        visualId = entitySpawnToC.EntityCreationData.CharacterCreationData.VisualId,

                        maxHP = entitySpawnToC.EntityCreationData.CharacterCreationData.MaxHP,
                        currentHP = entitySpawnToC.EntityCreationData.CharacterCreationData.CurrentHP,
                        maxMP = entitySpawnToC.EntityCreationData.CharacterCreationData.MaxMP,
                        currentMP = entitySpawnToC.EntityCreationData.CharacterCreationData.CurrentMP,
                        level = entitySpawnToC.EntityCreationData.CharacterCreationData.Level,
                        currentExp = entitySpawnToC.EntityCreationData.CharacterCreationData.CurrentExp,
                        strength = entitySpawnToC.EntityCreationData.CharacterCreationData.Strength,
                        dexterity = entitySpawnToC.EntityCreationData.CharacterCreationData.Dexterity,
                        intelligence = entitySpawnToC.EntityCreationData.CharacterCreationData.Intelligence,
                        vitality = entitySpawnToC.EntityCreationData.CharacterCreationData.Vitality,
                    });
                    break;

                case EntityCreationData.CreationDataOneofCase.ItemCreationData:
                    string itemEntityId = entitySpawnToC.EntityCreationData.ItemCreationData.BaseEntityCreationData.EntityId;

                    if (runner.entityManager.TryGetEntity<LOPActor>(itemEntityId, out var item))
                    {
                        Debug.LogWarning($"Entity {itemEntityId} already exists");
                        return;
                    }

                    runner.entityManager.CreateEntity<LOPActor, ItemCreationData>(new ItemCreationData
                    {
                        entityId = itemEntityId,
                        position = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.ItemCreationData.BaseEntityCreationData.Position),
                        rotation = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.ItemCreationData.BaseEntityCreationData.Rotation),
                        velocity = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.ItemCreationData.BaseEntityCreationData.Velocity),
                        itemCode = entitySpawnToC.EntityCreationData.ItemCreationData.ItemCode,
                        visualId = entitySpawnToC.EntityCreationData.ItemCreationData.VisualId,
                    });
                    break;
            }
        }

        private void OnEntityDespawnToC(EntityDespawnToC entityDespawnToC)
        {
            if (runner.entityManager.TryGetEntity<LOPActor>(entityDespawnToC.EntityId, out var actor))
            {
                runner.entityManager.DeleteEntityById(entityDespawnToC.EntityId);
            }
            else
            {
                Debug.LogWarning($"Entity {entityDespawnToC.EntityId} not found for despawn");
            }
        }

        private void OnUserEntitySnapToC(UserEntitySnapToC userEntitySnapToC)
        {
            if (playerContext.actor == null)
            {
                return;
            }

            GameFramework.World.Entity worldEntity = entityRegistry.Get(playerContext.actor.entityId);
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            if (health != null)
            {
                int prevCurrent = health.Current;
                int prevMax = health.Max;
                healthSystem.ApplyAuthoritativeState(health, userEntitySnapToC.MaxHP, userEntitySnapToC.CurrentHP);
                if (health.Current != prevCurrent || health.Max != prevMax)
                {
                    healthChangedPublisher.Publish(playerContext.actor.entityId, new EntityHealthChanged(health.Current, health.Max));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Health not found for entity {playerContext.actor.entityId}");
            }
            GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
            if (mana != null)
            {
                int prevCurrent = mana.Current;
                int prevMax = mana.Max;
                manaSystem.ApplyAuthoritativeState(mana, userEntitySnapToC.MaxMP, userEntitySnapToC.CurrentMP);
                if (mana.Current != prevCurrent || mana.Max != prevMax)
                {
                    manaChangedPublisher.Publish(playerContext.actor.entityId, new EntityManaChanged(mana.Current, mana.Max));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Mana not found for entity {playerContext.actor.entityId}");
            }
            GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
            if (level != null)
            {
                int prevValue = level.Value;
                long prevExp = level.Exp;
                levelSystem.ApplyAuthoritativeState(level, userEntitySnapToC.Level, userEntitySnapToC.CurrentExp);
                if (level.Value != prevValue || level.Exp != prevExp)
                {
                    levelChangedPublisher.Publish(playerContext.actor.entityId, new EntityLevelChanged(level.Value, level.Exp, level.ExpToNext));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Level not found for entity {playerContext.actor.entityId}");
            }
            GameFramework.World.Stats stats = worldEntity?.Get<GameFramework.World.Stats>();
            if (stats != null)
            {
                int prevUnspent = stats.UnspentPoints;
                statsSystem.SetUnspent(stats, userEntitySnapToC.StatPoints);
                if (stats.UnspentPoints != prevUnspent)
                {
                    statPointsChangedPublisher.Publish(playerContext.actor.entityId, new EntityStatPointsChanged(stats.UnspentPoints));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Stats not found for entity {playerContext.actor.entityId}");
            }
        }

        private void OnStatAllocationToC(StatAllocationToC statAllocationToC)
        {
            if (playerContext.actor == null)
            {
                return;
            }

            GameFramework.World.Stats stats = entityRegistry.Get(playerContext.actor.entityId)?.Get<GameFramework.World.Stats>();
            if (stats == null)
            {
                Debug.LogWarning($"[World] StatAllocation: Stats not found for entity {playerContext.actor.entityId}");
                return;
            }

            int statType;
            // wire stat 문자열은 소문자 필드명("strength" 등) — 서버 Slice 3 계약과 일치.
            switch (statAllocationToC.Stat)
            {
                case "strength": statType = (int)GameFramework.World.EntityStatType.Strength; break;
                case "dexterity": statType = (int)GameFramework.World.EntityStatType.Dexterity; break;
                case "intelligence": statType = (int)GameFramework.World.EntityStatType.Intelligence; break;
                case "vitality": statType = (int)GameFramework.World.EntityStatType.Vitality; break;
                default: return;
            }

            statsSystem.SetBase(stats, statType, statAllocationToC.StatValue);
            int effectiveValue = Mathf.RoundToInt(statsSystem.GetValue(stats, statType));
            statChangedPublisher.Publish(playerContext.actor.entityId, new EntityStatChanged(statType, effectiveValue));
        }
    }
}
