using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;

namespace LOP
{
    public class GameDataContext : IGameDataContext
    {
        public Type[] subscribedTypes => new Type[]
        {
            typeof(GameInfoToC),
        };

        private Dictionary<Type, Action<object>> updateHandlers;

        public GameInfo gameInfo { get; set; }
        public string userEntityId { get; set; }

        public GameDataContext(IDataContextManager dataContextManager)
        {
            dataContextManager.Register(this);

            updateHandlers = new Dictionary<Type, Action<object>>
            {
                { typeof(GameInfoToC), data => HandleGameInfo((GameInfoToC)data) },
            };
        }

        public void UpdateData<T>(T data)
        {
            if (updateHandlers.TryGetValue(data.GetType(), out var handler))
            {
                handler(data);
            }
        }

        private void HandleGameInfo(GameInfoToC gameInfoToC)
        {
            gameInfo = gameInfoToC.GameInfo;
            userEntityId = gameInfoToC.EntityId;
        }

        public void Clear()
        {
            gameInfo = null;
            updateHandlers.Clear();
        }
    }
}
