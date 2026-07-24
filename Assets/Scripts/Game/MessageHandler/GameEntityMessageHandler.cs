using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using UnityEngine;

namespace LOP
{
    public class GameEntityMessageHandler : MessageHandlerBase
    {
        private readonly IRunner runner;
        private readonly IPlayerContext playerContext;
        private readonly IGameDataStore gameDataStore;
        private readonly EntitySpawner entitySpawner;
        private readonly ActorRegistry actorRegistry;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.HealthSystem healthSystem;
        private readonly GameFramework.World.ManaSystem manaSystem;
        private readonly GameFramework.World.LevelSystem levelSystem;
        private readonly GameFramework.World.StatsSystem statsSystem;
        private readonly Reconciler reconciler;
        private readonly RemoteInterpolationClock remoteInterpolationClock;

        private readonly ISubscriber<EntitySnapsToC> snapsSubscriber;
        private readonly ISubscriber<EntitySpawnToC> spawnSubscriber;
        private readonly ISubscriber<EntityDespawnToC> despawnSubscriber;
        private readonly ISubscriber<UserEntitySnapToC> userSnapSubscriber;
        private readonly ISubscriber<StatAllocationToC> statAllocationSubscriber;

        private readonly IPublisher<string, EntityHealthChanged> healthChangedPublisher;
        private readonly IPublisher<string, EntityManaChanged> manaChangedPublisher;
        private readonly IPublisher<string, EntityLevelChanged> levelChangedPublisher;
        private readonly IPublisher<string, EntityStatPointsChanged> statPointsChangedPublisher;
        private readonly IPublisher<string, EntityStatChanged> statChangedPublisher;

        // 스냅이 틱당 여러 메시지로 청킹돼 온다 → 도착 기록을 틱당 1회로 dedupe(간격 기반 추정기 왜곡 방지).
        private long lastRecordedArrivalTick = long.MinValue;

        public GameEntityMessageHandler(
            IRunner runner,
            IPlayerContext playerContext,
            IGameDataStore gameDataStore,
            EntitySpawner entitySpawner,
            ActorRegistry actorRegistry,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.HealthSystem healthSystem,
            GameFramework.World.ManaSystem manaSystem,
            GameFramework.World.LevelSystem levelSystem,
            GameFramework.World.StatsSystem statsSystem,
            Reconciler reconciler,
            RemoteInterpolationClock remoteInterpolationClock,
            ISubscriber<EntitySnapsToC> snapsSubscriber,
            ISubscriber<EntitySpawnToC> spawnSubscriber,
            ISubscriber<EntityDespawnToC> despawnSubscriber,
            ISubscriber<UserEntitySnapToC> userSnapSubscriber,
            ISubscriber<StatAllocationToC> statAllocationSubscriber,
            IPublisher<string, EntityHealthChanged> healthChangedPublisher,
            IPublisher<string, EntityManaChanged> manaChangedPublisher,
            IPublisher<string, EntityLevelChanged> levelChangedPublisher,
            IPublisher<string, EntityStatPointsChanged> statPointsChangedPublisher,
            IPublisher<string, EntityStatChanged> statChangedPublisher)
        {
            this.runner = runner;
            this.playerContext = playerContext;
            this.gameDataStore = gameDataStore;
            this.entitySpawner = entitySpawner;
            this.actorRegistry = actorRegistry;
            this.entityRegistry = entityRegistry;
            this.healthSystem = healthSystem;
            this.manaSystem = manaSystem;
            this.levelSystem = levelSystem;
            this.statsSystem = statsSystem;
            this.reconciler = reconciler;
            this.remoteInterpolationClock = remoteInterpolationClock;
            this.snapsSubscriber = snapsSubscriber;
            this.spawnSubscriber = spawnSubscriber;
            this.despawnSubscriber = despawnSubscriber;
            this.userSnapSubscriber = userSnapSubscriber;
            this.statAllocationSubscriber = statAllocationSubscriber;
            this.healthChangedPublisher = healthChangedPublisher;
            this.manaChangedPublisher = manaChangedPublisher;
            this.levelChangedPublisher = levelChangedPublisher;
            this.statPointsChangedPublisher = statPointsChangedPublisher;
            this.statChangedPublisher = statChangedPublisher;
        }

        protected override void Subscribe()
        {
            Track(snapsSubscriber.Subscribe(OnEntitySnapsToC));
            Track(spawnSubscriber.Subscribe(OnEntitySpawnToC));
            Track(despawnSubscriber.Subscribe(OnEntityDespawnToC));
            Track(userSnapSubscriber.Subscribe(OnUserEntitySnapToC));
            Track(statAllocationSubscriber.Subscribe(OnStatAllocationToC));
        }

        private void OnEntitySnapsToC(EntitySnapsToC entitySnapsToC)
        {
            if (runner.gameState < RunnerState.Playing)
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
                if (actorRegistry.TryGet(serverEntitySnap.EntityId, out var actor) == false)
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

                if (playerContext.entityId == actor.entityId)
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

                    if (entityRegistry.Contains(entityId))
                    {
                        Debug.LogWarning($"Entity {entityId} already exists");
                        return;
                    }

                    entitySpawner.Spawn(new CharacterCreationData
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

                    if (entityRegistry.Contains(itemEntityId))
                    {
                        Debug.LogWarning($"Entity {itemEntityId} already exists");
                        return;
                    }

                    entitySpawner.Spawn(new ItemCreationData
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
            if (entityRegistry.Contains(entityDespawnToC.EntityId))
            {
                entitySpawner.Despawn(entityDespawnToC.EntityId);
            }
            else
            {
                Debug.LogWarning($"Entity {entityDespawnToC.EntityId} not found for despawn");
            }
        }

        private void OnUserEntitySnapToC(UserEntitySnapToC userEntitySnapToC)
        {
            if (playerContext.entityId == null)
            {
                return;
            }

            GameFramework.World.Entity worldEntity = entityRegistry.Get(playerContext.entityId);
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            if (health != null)
            {
                int prevCurrent = health.Current;
                int prevMax = health.Max;
                healthSystem.ApplyAuthoritativeState(health, userEntitySnapToC.MaxHP, userEntitySnapToC.CurrentHP);
                if (health.Current != prevCurrent || health.Max != prevMax)
                {
                    healthChangedPublisher.Publish(playerContext.entityId, new EntityHealthChanged(health.Current, health.Max));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Health not found for entity {playerContext.entityId}");
            }
            GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
            if (mana != null)
            {
                int prevCurrent = mana.Current;
                int prevMax = mana.Max;
                manaSystem.ApplyAuthoritativeState(mana, userEntitySnapToC.MaxMP, userEntitySnapToC.CurrentMP);
                if (mana.Current != prevCurrent || mana.Max != prevMax)
                {
                    manaChangedPublisher.Publish(playerContext.entityId, new EntityManaChanged(mana.Current, mana.Max));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Mana not found for entity {playerContext.entityId}");
            }
            GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
            if (level != null)
            {
                int prevValue = level.Value;
                long prevExp = level.Exp;
                levelSystem.ApplyAuthoritativeState(level, userEntitySnapToC.Level, userEntitySnapToC.CurrentExp);
                if (level.Value != prevValue || level.Exp != prevExp)
                {
                    levelChangedPublisher.Publish(playerContext.entityId, new EntityLevelChanged(level.Value, level.Exp, level.ExpToNext));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Level not found for entity {playerContext.entityId}");
            }
            GameFramework.World.Stats stats = worldEntity?.Get<GameFramework.World.Stats>();
            if (stats != null)
            {
                int prevUnspent = stats.UnspentPoints;
                statsSystem.SetUnspent(stats, userEntitySnapToC.StatPoints);
                if (stats.UnspentPoints != prevUnspent)
                {
                    statPointsChangedPublisher.Publish(playerContext.entityId, new EntityStatPointsChanged(stats.UnspentPoints));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Stats not found for entity {playerContext.entityId}");
            }
        }

        private void OnStatAllocationToC(StatAllocationToC statAllocationToC)
        {
            if (playerContext.entityId == null)
            {
                return;
            }

            GameFramework.World.Stats stats = entityRegistry.Get(playerContext.entityId)?.Get<GameFramework.World.Stats>();
            if (stats == null)
            {
                Debug.LogWarning($"[World] StatAllocation: Stats not found for entity {playerContext.entityId}");
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
            statChangedPublisher.Publish(playerContext.entityId, new EntityStatChanged(statType, effectiveValue));
        }
    }
}
