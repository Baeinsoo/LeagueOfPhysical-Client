using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class CharacterCreator : IEntityCreator<LOPEntity, CharacterCreationData>
    {
        [Inject]
        private IGameDataStore gameDataStore;

        [Inject]
        private IPlayerContext playerContext;

        [Inject]
        private IObjectResolver objectResolver;

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private AbilitySystem abilitySystem;

        [Inject]
        private LOP.MasterData.LOPMasterData md;

        public LOPEntity Create(CharacterCreationData creationData)
        {
            GameObject root = new GameObject($"Character_{creationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");

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

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            objectResolver.Inject(entity);
            entity.LinkWorldMotion(
                worldEntity.Get<GameFramework.World.Transform>(),
                worldEntity.Get<GameFramework.World.Velocity>());
            entity.Initialize(creationData);

            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;

            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            objectResolver.Inject(physicsComponent);
            // 모든 캐릭터 kinematic — 우리가 직접 이동시킨다. 내 캐릭=예측(KinematicMoveSystem), 남=스냅 팔로워.
            physicsComponent.Initialize(true, false);

            LOPEntityController controller = root.CreateChildWithComponent<LOPEntityController>();
            objectResolver.Inject(controller);
            controller.SetEntity(entity);

            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(entity);

            if (isUserEntity)
            {
                playerContext.entity = entity;
                playerContext.entityView = view;

                LocalEntityInterpolator interpolator = entity.gameObject.AddComponent<LocalEntityInterpolator>();
                objectResolver.Inject(interpolator);
                interpolator.entity = entity;
                interpolator.entityView = view;
            }
            else
            {
                RemoteEntityInterpolator interpolator = entity.gameObject.AddComponent<RemoteEntityInterpolator>();
                objectResolver.Inject(interpolator);
                interpolator.entity = entity;
                interpolator.entityView = view;
            }

            // --- World Core (병렬·추가) — Health/Mana/Level/Stats/Abilities. Transform/Velocity는 위에서 생성(파사드 백킹). ---
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
            // 물리 핸들(rb/콜라이더)을 공유 컴포넌트로 — 공유 MotionBridge가 이걸로 겹침해소·rb 반영(per-side LOPEntity 안 만짐).
            worldEntity.Add(new PhysicsBody(physicsComponent.entityRigidbody, (CapsuleCollider)physicsComponent.entityColliders[0]));
            if (isUserEntity)
            {
                // 입력으로 조종되는 엔티티(내 캐릭)만 — 호스트가 매 틱 커맨드를 채우고 MovementSystem이 읽는다.
                worldEntity.Add(new InputBuffer());
                // 클라 시뮬 대상 = 예측하는 내 캐릭만. 남/NPC는 Simulated 아님 → 스냅샷 보간 전용.
                worldEntity.Add(new GameFramework.World.Simulated());
            }
            entityRegistry.Add(worldEntity);

            // 3d: 헤이스트 어빌리티 부여(발동은 입력 트리거 — AbilityActivator). TEMP: 전체 부여, 캐릭터별 셋은 후속.
            abilitySystem.Grant(worldEntity, 1);
            abilitySystem.Grant(worldEntity, 2);   // dash (TEMP 전체 부여)
            abilitySystem.Grant(worldEntity, 3);   // attack (TEMP 전체 부여)
            if (isUserEntity)
            {
                abilitySystem.Grant(worldEntity, 4);   // 전역 공격 — 내 캐릭 전용 테스트 툴(G키)
            }

            Debug.Log($"[World] Registered entity {worldEntity.Id} Health={worldHealth.Current}/{worldHealth.Max}");
            // --- end World Core slice 1+B ---

            return entity;
        }
    }
}
