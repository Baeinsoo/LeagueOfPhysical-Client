using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Flappy Race의 플레이어 몸(새)을 만든다. 캐릭터와 달리 체력·마나·레벨·어빌리티가 없다 —
    /// 이 게임에는 그런 개념이 없으므로 안 붙인다.
    /// </summary>
    public class FlappyBirdCreator : ICharacterCreator
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly FlappyConfig config;

        public FlappyBirdCreator(
            IGameDataStore gameDataStore,
            IPlayerContext playerContext,
            GameFramework.World.EntityRegistry entityRegistry,
            FlappyConfig config)
        {
            this.gameDataStore = gameDataStore;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.config = config;
        }

        public void Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            // EntityBinder가 뷰·보간을 붙일 때 보는 값이라 Character로 둔다 — 새도 플레이어가 조종하는 몸이다.
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new MotionContributions());
            // 새 몸은 시뮬이 쓰는 그 값(TbFlappyConfig)에서 온다 — 물리 팔로워가 다른 몸을 세우면
            // 겹침 밀어내기가 시뮬이 모르는 위치 점프를 만든다.
            worldEntity.Add(new GameFramework.World.CapsuleShape(config.BodyRadius, config.BodyHeight));
            worldEntity.Add(new FinishState());
            worldEntity.Add(new FinishPlacement());
            // 지금까지 EntityBinder가 하드코딩하던 값을 그대로 옮긴 것 — 거동 변화 없음.
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));
            worldEntity.Add(new FlappyStun());
            worldEntity.Add(new FlappyDash());

            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;
            //  입력 버퍼는 모든 새가 갖는다. 내 것은 PlayerInputManager가 채우고 시뮬이 소비한다.
            //  남의 것도 서버가 되뿌린 입력으로 채워지지만 이 게임에선 읽히지 않는다 — 남의 새는
            //  굴리지 않고 보간으로 그리기 때문이다(동기화 정책은 FlappyRaceLifetimeScope 참고).
            //  Simulated는 EntityBinder가 그 정책을 보고 붙인다.
            worldEntity.Add(new InputBuffer());
            entityRegistry.Add(worldEntity);

            if (isUserEntity)
            {
                playerContext.entityId = creationData.entityId;   // .actor는 EntityBinder가 뷰 생성 후 세팅
            }

            Debug.Log($"[World] Registered flappy bird {worldEntity.Id}");
        }
    }
}
