using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class RoomDataContext : IRoomDataContext
    {
        public Type[] subscribedTypes => new Type[]
        {
            typeof(GetMatchResponse),
            typeof(RoomJoinableResponse),
        };

        private Dictionary<Type, Action<object>> updateHandlers;

        public Room room { get; set; }
        public Match match { get; set; }

        public RoomDataContext()
        {
            updateHandlers = new Dictionary<Type, Action<object>>
            {
                { typeof(GetMatchResponse), data => HandleGetMatch((GetMatchResponse)data) },
                { typeof(RoomJoinableResponse), data => HandleRoomJoinable((RoomJoinableResponse)data) },
            };
        }

        public void UpdateData<T>(T data)
        {
            if (updateHandlers.TryGetValue(data.GetType(), out var handler))
            {
                handler(data);
            }
        }

        private void HandleGetMatch(GetMatchResponse response)
        {
            match = MapperConfig.mapper.Map<Match>(response.match);
        }

        private void HandleRoomJoinable(RoomJoinableResponse response)
        {
            room = MapperConfig.mapper.Map<Room>(response.room);
        }

        public void Clear()
        {
            room = null;
            match = null;
            updateHandlers.Clear();
        }
    }
}
