using GameFramework;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameInfoMessageHandler : IGameMessageHandler
    {
        [Inject]
        private EntitySpawner entitySpawner;

        [Inject]
        private PlayerInputManager playerInputManager;

        [Inject]
        private MatchSeed matchSeed;

        [Inject]
        private ISubscriber<GameInfoToC> gameInfoSubscriber;

        private System.IDisposable subscription;

        public void Initialize()
        {
            subscription = gameInfoSubscriber.Subscribe(OnGameInfoToC);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }

        private void OnGameInfoToC(GameInfoToC gameInfoToC)
        {
            matchSeed.Set(gameInfoToC.GameInfo.MatchSeed);

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
