using GameFramework;
using LOP.Event.Entity;
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

        public void Initialize()
        {
            EventBus.Default.Subscribe<EntitySnapsToC>(nameof(IMessage), OnEntitySnapsToC);
            EventBus.Default.Subscribe<EntitySpawnToC>(nameof(IMessage), OnEntitySpawnToC);
            EventBus.Default.Subscribe<EntityDespawnToC>(nameof(IMessage), OnEntityDespawnToC);
            EventBus.Default.Subscribe<UserEntitySnapToC>(nameof(IMessage), OnUserEntitySnapToC);
            EventBus.Default.Subscribe<StatAllocationToC>(nameof(IMessage), OnStatAllocationToC);
        }

        public void Dispose()
        {
            EventBus.Default.Unsubscribe<EntitySnapsToC>(nameof(IMessage), OnEntitySnapsToC);
            EventBus.Default.Unsubscribe<EntitySpawnToC>(nameof(IMessage), OnEntitySpawnToC);
            EventBus.Default.Unsubscribe<EntityDespawnToC>(nameof(IMessage), OnEntityDespawnToC);
            EventBus.Default.Unsubscribe<UserEntitySnapToC>(nameof(IMessage), OnUserEntitySnapToC);
            EventBus.Default.Unsubscribe<StatAllocationToC>(nameof(IMessage), OnStatAllocationToC);
        }

        private void OnEntitySnapsToC(EntitySnapsToC entitySnapsToC)
        {
            if (Runner.current == null)
            {
                return;
            }

            foreach (var serverEntitySnap in entitySnapsToC.EntitySnaps.OrEmpty())
            {
                if (runner.entityManager.TryGetEntity<LOPEntity>(serverEntitySnap.EntityId, out var entity) == false)
                {
                    Debug.LogWarning($"Entity {serverEntitySnap.EntityId} not found");
                    continue;
                }

                EntitySnap entitySnap = MapperConfig.mapper.Map<EntitySnap>(serverEntitySnap);
                entitySnap.tick = entitySnapsToC.Tick;
                entitySnap.timestamp = entitySnapsToC.Tick * gameDataStore.gameInfo.Interval;

                if (playerContext.entity.entityId == entity.entityId)
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
                            EventBus.Default.Publish(
                                EventTopic.EntityId<LOPEntity>(serverEntitySnap.EntityId),
                                new EntityHealthChanged(health.Current, health.Max));
                        }
                    }

                    entity.GetComponent<ServerStateReconciler>().AddServerEntitySnap(entitySnap);
                }
            }
        }

        private void OnEntitySpawnToC(EntitySpawnToC entitySpawnToC)
        {
            switch (entitySpawnToC.EntityCreationData.CreationDataCase)
            {
                case EntityCreationData.CreationDataOneofCase.CharacterCreationData:
                    string entityId = entitySpawnToC.EntityCreationData.CharacterCreationData.BaseEntityCreationData.EntityId;

                    if (runner.entityManager.TryGetEntity<LOPEntity>(entityId, out var entity))
                    {
                        Debug.LogWarning($"Entity {entityId} already exists");
                        return;
                    }

                    runner.entityManager.CreateEntity<LOPEntity, CharacterCreationData>(new CharacterCreationData
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

                    if (runner.entityManager.TryGetEntity<LOPEntity>(itemEntityId, out var item))
                    {
                        Debug.LogWarning($"Entity {itemEntityId} already exists");
                        return;
                    }

                    runner.entityManager.CreateEntity<LOPEntity, ItemCreationData>(new ItemCreationData
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
            if (runner.entityManager.TryGetEntity<LOPEntity>(entityDespawnToC.EntityId, out var entity))
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
            if (playerContext.entity == null)
            {
                return;
            }

            GameFramework.World.Entity worldEntity = entityRegistry.Get(playerContext.entity.entityId);
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            if (health != null)
            {
                int prevCurrent = health.Current;
                int prevMax = health.Max;
                healthSystem.ApplyAuthoritativeState(health, userEntitySnapToC.MaxHP, userEntitySnapToC.CurrentHP);
                if (health.Current != prevCurrent || health.Max != prevMax)
                {
                    EventBus.Default.Publish(
                        EventTopic.EntityId<LOPEntity>(playerContext.entity.entityId),
                        new EntityHealthChanged(health.Current, health.Max));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Health not found for entity {playerContext.entity.entityId}");
            }
            GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
            if (mana != null)
            {
                int prevCurrent = mana.Current;
                int prevMax = mana.Max;
                manaSystem.ApplyAuthoritativeState(mana, userEntitySnapToC.MaxMP, userEntitySnapToC.CurrentMP);
                if (mana.Current != prevCurrent || mana.Max != prevMax)
                {
                    EventBus.Default.Publish(
                        EventTopic.EntityId<LOPEntity>(playerContext.entity.entityId),
                        new EntityManaChanged(mana.Current, mana.Max));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Mana not found for entity {playerContext.entity.entityId}");
            }
            GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
            if (level != null)
            {
                int prevValue = level.Value;
                long prevExp = level.Exp;
                levelSystem.ApplyAuthoritativeState(level, userEntitySnapToC.Level, userEntitySnapToC.CurrentExp);
                if (level.Value != prevValue || level.Exp != prevExp)
                {
                    EventBus.Default.Publish(
                        EventTopic.EntityId<LOPEntity>(playerContext.entity.entityId),
                        new EntityLevelChanged(level.Value, level.Exp, level.ExpToNext));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Level not found for entity {playerContext.entity.entityId}");
            }
            GameFramework.World.Stats stats = worldEntity?.Get<GameFramework.World.Stats>();
            if (stats != null)
            {
                int prevUnspent = stats.UnspentPoints;
                statsSystem.SetUnspent(stats, userEntitySnapToC.StatPoints);
                if (stats.UnspentPoints != prevUnspent)
                {
                    EventBus.Default.Publish(
                        EventTopic.EntityId<LOPEntity>(playerContext.entity.entityId),
                        new EntityStatPointsChanged(stats.UnspentPoints));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Stats not found for entity {playerContext.entity.entityId}");
            }
        }

        private void OnStatAllocationToC(StatAllocationToC statAllocationToC)
        {
            if (playerContext.entity == null)
            {
                return;
            }

            GameFramework.World.Stats stats = entityRegistry.Get(playerContext.entity.entityId)?.Get<GameFramework.World.Stats>();
            if (stats == null)
            {
                Debug.LogWarning($"[World] StatAllocation: Stats not found for entity {playerContext.entity.entityId}");
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
            EventBus.Default.Publish(
                EventTopic.EntityId<LOPEntity>(playerContext.entity.entityId),
                new EntityStatChanged(statType, effectiveValue));
        }
    }
}
