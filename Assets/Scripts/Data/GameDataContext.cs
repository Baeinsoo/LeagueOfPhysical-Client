using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class GameDataContext : IGameDataContext
    {
        public Type[] subscribedTypes => new Type[]
        {
            typeof(GameInfoResponse),
        };

        private Dictionary<Type, Action<object>> updateHandlers;

        public Player player { get; set; }
        public GameInfo gameInfo { get; set; }

        public GameDataContext()
        {
            player = new Player();

            updateHandlers = new Dictionary<Type, Action<object>>
            {
                { typeof(GameInfoResponse), data => HandleGameInfo((GameInfoResponse)data) },
            };
        }

        public void UpdateData<T>(T data)
        {
            if (updateHandlers.TryGetValue(data.GetType(), out var handler))
            {
                handler(data);
            }
        }

        private void HandleGameInfo(GameInfoResponse response)
        {
            player.entityId = response.EntityId;
            gameInfo = response.GameInfo;
        }

        public void Clear()
        {
            player.Clear();
            gameInfo = null;
            updateHandlers.Clear();
        }
    }
}
