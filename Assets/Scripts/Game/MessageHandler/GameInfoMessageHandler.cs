using GameFramework;
using MessagePipe;
using UnityEngine;

namespace LOP
{
    public class GameInfoMessageHandler : MessageHandlerBase
    {
        private readonly EntitySpawner entitySpawner;
        private readonly PlayerInputManager playerInputManager;
        private readonly MatchSeed matchSeed;
        private readonly IGameDataStore gameDataStore;
        private readonly ISubscriber<GameInfoToC> gameInfoSubscriber;

        public GameInfoMessageHandler(EntitySpawner entitySpawner, PlayerInputManager playerInputManager, MatchSeed matchSeed, IGameDataStore gameDataStore, ISubscriber<GameInfoToC> gameInfoSubscriber)
        {
            this.entitySpawner = entitySpawner;
            this.playerInputManager = playerInputManager;
            this.matchSeed = matchSeed;
            this.gameDataStore = gameDataStore;
            this.gameInfoSubscriber = gameInfoSubscriber;
        }

        protected override void Subscribe() => Track(gameInfoSubscriber.Subscribe(OnGameInfoToC));

        private void OnGameInfoToC(GameInfoToC gameInfoToC)
        {
            matchSeed.Set(gameInfoToC.GameInfo.MatchSeed);

            // 아래 Spawn이 EntityBinder를 통해 userEntityId를 읽는다. 그 값을 채우는 GameDataStore도
            // 이 메시지의 구독자일 뿐이라, 그쪽이 먼저 불린다는 보장이 없다 — MessagePipe는 핸들러를
            // 배열 인덱스 순서로 부르는데 구독 해제된 자리를 재사용하므로, 한 세션에서 매치를 몇 판
            // 반복하면 구독 순서와 호출 순서가 어긋난다. 그래서 남을 기다리지 않고 여기서 직접 채운다
            // (같은 메시지의 같은 값이라 누가 먼저 쓰든 결과는 같다).
            gameDataStore.userEntityId = gameInfoToC.EntityId;

            foreach (var entityCreationData in gameInfoToC.GameInfo.EntityCreationDatas)
            {
                switch (entityCreationData.CreationDataCase)
                {
                    case EntityCreationData.CreationDataOneofCase.CharacterCreationData:
                        entitySpawner.Spawn(new CharacterCreationData
                        {
                            entityId = entityCreationData.CharacterCreationData.BaseEntityCreationData.EntityId,
                            position = MapperConfig.mapper.Map<Vector3>(entityCreationData.CharacterCreationData.BaseEntityCreationData.Position),
                            rotation = MapperConfig.mapper.Map<Vector3>(entityCreationData.CharacterCreationData.BaseEntityCreationData.Rotation),
                            velocity = MapperConfig.mapper.Map<Vector3>(entityCreationData.CharacterCreationData.BaseEntityCreationData.Velocity),
                            characterCode = entityCreationData.CharacterCreationData.CharacterCode,
                            visualId = entityCreationData.CharacterCreationData.VisualId,

                            maxHP = entityCreationData.CharacterCreationData.MaxHP,
                            currentHP = entityCreationData.CharacterCreationData.CurrentHP,
                            maxMP = entityCreationData.CharacterCreationData.MaxMP,
                            currentMP = entityCreationData.CharacterCreationData.CurrentMP,
                            level = entityCreationData.CharacterCreationData.Level,
                            currentExp = entityCreationData.CharacterCreationData.CurrentExp,
                            strength = entityCreationData.CharacterCreationData.Strength,
                            dexterity = entityCreationData.CharacterCreationData.Dexterity,
                            intelligence = entityCreationData.CharacterCreationData.Intelligence,
                            vitality = entityCreationData.CharacterCreationData.Vitality,
                        });
                        break;

                    case EntityCreationData.CreationDataOneofCase.ItemCreationData:
                        entitySpawner.Spawn(new ItemCreationData
                        {
                            entityId = entityCreationData.ItemCreationData.BaseEntityCreationData.EntityId,
                            position = MapperConfig.mapper.Map<Vector3>(entityCreationData.ItemCreationData.BaseEntityCreationData.Position),
                            rotation = MapperConfig.mapper.Map<Vector3>(entityCreationData.ItemCreationData.BaseEntityCreationData.Rotation),
                            velocity = MapperConfig.mapper.Map<Vector3>(entityCreationData.ItemCreationData.BaseEntityCreationData.Velocity),
                            itemCode = entityCreationData.ItemCreationData.ItemCode,
                            visualId = entityCreationData.ItemCreationData.VisualId,
                        });
                        break;
                }
            }

            playerInputManager.SetSequenceNumber(gameInfoToC.ExpectedNextSequence);
        }
    }
}
