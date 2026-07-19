using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class CharacterCreator : IEntityCreator<LOPActor, CharacterCreationData>
    {
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IPlayerContext playerContext;
        [Inject] private IObjectResolver objectResolver;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private AbilitySystem abilitySystem;
        [Inject] private LOP.MasterData.LOPMasterData md;

        public LOPActor Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new MasterDataRef(creationData.characterCode));
            worldEntity.Add(new Appearance(creationData.visualId));

            var worldHealth = new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP };
            worldEntity.Add(worldHealth);
            worldEntity.Add(new GameFramework.World.Mana(creationData.maxMP) { Current = creationData.currentMP });
            worldEntity.Add(new GameFramework.World.Level { Value = creationData.level, Exp = creationData.currentExp, ExpToNext = 100 });
            var worldStats = new GameFramework.World.Stats();
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Strength] = creationData.strength;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Dexterity] = creationData.dexterity;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Intelligence] = creationData.intelligence;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Vitality] = creationData.vitality;
            var characterMasterData = md.Tables.TbCharacter.Get(creationData.characterCode);
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed] = characterMasterData.Speed;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.JumpPower] = characterMasterData.JumpPower;
            worldEntity.Add(worldStats);
            worldEntity.Add(new Abilities());
            worldEntity.Add(new StatusEffects());
            worldEntity.Add(new MotionContributions());

            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;
            if (isUserEntity)
            {
                // 입력으로 조종되는 엔티티(내 캐릭)만. 클라 시뮬 대상=예측하는 내 캐릭만(Simulated).
                worldEntity.Add(new InputBuffer());
                worldEntity.Add(new GameFramework.World.Simulated());
            }
            entityRegistry.Add(worldEntity);

            abilitySystem.Grant(worldEntity, 1);
            abilitySystem.Grant(worldEntity, 2);   // dash
            abilitySystem.Grant(worldEntity, 3);   // attack
            if (isUserEntity)
            {
                abilitySystem.Grant(worldEntity, 4);   // 내 캐릭 전용 테스트 툴(G키)
            }

            // 앵커: 뷰 컴포넌트(물리/모델/보간/장식)는 EntityBinder가 EntityCreated 반응으로 붙인다.
            GameObject root = new GameObject($"Actor_{creationData.entityId}");
            LOPActor actor = root.AddComponent<LOPActor>();
            objectResolver.Inject(actor);
            actor.Initialize(creationData);
            if (isUserEntity)
            {
                playerContext.entityId = creationData.entityId;   // .actor는 스포너가 뷰 생성 후 세팅
            }

            Debug.Log($"[World] Registered entity {worldEntity.Id} Health={worldHealth.Current}/{worldHealth.Max}");
            return actor;
        }
    }
}
