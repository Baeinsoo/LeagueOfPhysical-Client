using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>Archery의 플레이어 몸(클라). 걷지 않으므로 이동 부품이 없다.</summary>
    public class ArcheryPlayerCreator : ICharacterCreator
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public ArcheryPlayerCreator(IGameDataStore gameDataStore, IPlayerContext playerContext,
                                    GameFramework.World.EntityRegistry entityRegistry)
        {
            this.gameDataStore = gameDataStore;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
        }

        public void Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity());
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new ArcheryAim());

            // 남의 몸도 입력 버퍼를 갖는다 — 서버가 남의 입력을 되뿌려 주고(EntityInputBroadcastSystem)
            // RemoteInputSystem이 여기에 채운다. 이 게임은 남도 굴리므로(CharactersPredictedSyncPolicy)
            // 그 입력이 실제로 읽혀서 남의 화살도 내 화면에서 같은 규칙으로 날아간다.
            worldEntity.Add(new InputBuffer());
            entityRegistry.Add(worldEntity);

            if (gameDataStore.userEntityId == creationData.entityId)
            {
                playerContext.entityId = creationData.entityId;
            }

            Debug.Log($"[World] Registered archer {worldEntity.Id}");
        }
    }
}
